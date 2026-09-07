namespace Brx.CopilotArchiveSearch;

using System;
using System.Buffers;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Timer = System.Timers.Timer;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Web.WebView2.Core;

using Brx.CopilotArchiveSearch.Chunked;
using Brx.CopilotArchiveSearch.Markdown;
using Brx.CopilotArchiveSearch.Search;
using Brx.CopilotArchiveSearch.Utils;
using SearchResult = Brx.CopilotArchiveSearch.Search.SearchResult;

public partial class MainWindow : Window
{
   private class PreviewContent
   {
      public static readonly PreviewContent Empty = new() 
      { 
         Content = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("<html></html>"))
      };  
      
      public DateTime LastWriteTimeUtc { get; init; }
      public ReadOnlySequence<byte> Content { get; set; }

      public Stream CreateStream()
      {
         return new ForwardOnlySequenceStream(Content);
      }
   }


   private const string DefaultArchiveFolder = @"%USERPROFILE%\Documents\CopilotArchive";
   private const string PreviewBaseUrl = "http://memory/preview?";
   private const int HtmlCacheSizeMB = 200;

   private readonly Timer _debounceTimer;
   private readonly MemoryCache _previewCache;

   private readonly string ArchiveFolder;

   [AllowNull]
   private CancellationTokenSource _previewCts;
   private Task _webViewInitTask;

   public MainWindow()
   {
      InitializeComponent();
      
      _debounceTimer = new Timer(250)
      {
         AutoReset = false
      };
      _debounceTimer.Elapsed += (_, __) => Dispatcher.Invoke(RunSearch);

      _previewCache = new MemoryCache(
         new MemoryCacheOptions()
         {
            SizeLimit = HtmlCacheSizeMB * 1024 * 1024,
         }
      );

      ArchiveFolder = Environment.ExpandEnvironmentVariables(ConfigurationManager.AppSettings["ArchiveFolderPath"] ?? DefaultArchiveFolder);

      // Initialize WebView2
      PreviewBrowser.DefaultBackgroundColor = Color.FromArgb(30, 30, 30); // #1e1e1e
      _webViewInitTask = InitializeWebViewAsync();
   }

   protected override void OnSourceInitialized(EventArgs e)
   {
      base.OnSourceInitialized(e);

      var hwnd = new WindowInteropHelper(this).Handle;
      int useDark = 1;
      int res;

      // Windows 10 1809–1909
      res = NativeMethods.DwmSetWindowAttribute(
          hwnd,
          NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1,
          ref useDark,
          sizeof(int));

      // Windows 10 2004+, Windows 11
      res = NativeMethods.DwmSetWindowAttribute(
          hwnd,
          NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
          ref useDark,
          sizeof(int));
   }

   private async Task InitializeWebViewAsync()
   {
      await PreviewBrowser.EnsureCoreWebView2Async();

      PreviewBrowser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Document);

      PreviewBrowser.CoreWebView2.WebResourceRequested += PreviewBrowser_CoreWebView2_WebResourceRequested;
   }

   private void PreviewBrowser_CoreWebView2_WebResourceRequested(object? s, CoreWebView2WebResourceRequestedEventArgs e)
   {
      var relative = Uri.UnescapeDataString(new Uri(e.Request.Uri).Query.Substring(1));
      var fullPath = Path.Combine(ArchiveFolder, relative);
      var token = _previewCts.Token;

      var deferral = e.GetDeferral();

      Task.Run(async () =>
      {
         PreviewContent? preview = null;
         Exception? error = null;

         try
         {
            preview = await HandlePreviewRequestAsync(fullPath, token);
         }
         catch (OperationCanceledException)
         {
            // Expected — user changed selection
         }
         catch (Exception ex)
         {
            error = ex;
            Log(ex, nameof(PreviewBrowser_CoreWebView2_WebResourceRequested));
         }
         finally
         {

            await Dispatcher.InvokeAsync(() =>
            {
               try
               {
                  if (error != null)
                  {
                     e.Response = CreatePreviewResponse(PreviewContent.Empty);
                  }
                  else
                  {
                     e.Response = (preview == null || token.IsCancellationRequested)
                         ? CreatePreviewResponse(PreviewContent.Empty)
                         : CreatePreviewResponse(preview);
                  }
               }
               finally
               {
                  deferral.Complete();
               }
            });
         }
      });
   }

   private CoreWebView2WebResourceResponse CreatePreviewResponse(PreviewContent preview)
   {
      return PreviewBrowser.CoreWebView2.Environment.CreateWebResourceResponse(preview.CreateStream(), 200, "OK", "Content-Type: text/html");
   }

   private async Task<PreviewContent?> HandlePreviewRequestAsync(string fullPath, CancellationToken token)
   {
      // Phase 1 cancellation
      if (token.IsCancellationRequested)
         return null;

      var info = new FileInfo(fullPath);
      var ts = info.LastWriteTimeUtc;

      // Cache hit
      if (_previewCache.TryGetValue(fullPath, out PreviewContent? cached) && cached != null && cached.LastWriteTimeUtc == ts)
      {
         return cached;
      }

      // Phase 2: heavy work
      var preview = await BuildPreviewContentAsync(fullPath, ts, token);

      // Always store the result — even if cancelled after generation
      _previewCache.Set(
         fullPath,
         preview,
         new MemoryCacheEntryOptions
         {
            Size = preview.Content.Length,
            SlidingExpiration = TimeSpan.FromMinutes(20)
         }
      );

      return preview;
   }

   private static async Task<PreviewContent> BuildPreviewContentAsync(string path, DateTime ts, CancellationToken token)
   {
      // Safe point #1
      token.ThrowIfCancellationRequested();

      // Async + cancellable file read
      var markdown = await ReadAllTextAsync(path, token);

      // Safe point #2
      token.ThrowIfCancellationRequested();

      var writer = new ChunkedUtf8TextWriter(8192);

      // Phase 2 begins — non‑interruptible
      MarkdownRenderer.Render(markdown, writer);

      return new PreviewContent
      {
         LastWriteTimeUtc = ts,
         Content = writer.ToSequence()
      };
   }

   private static async Task<string> ReadAllTextAsync(string path, CancellationToken token)
   {
      // Open file for async reading
      await using var stream = new FileStream(
          path,
          FileMode.Open,
          FileAccess.Read,
          FileShare.Read,
          bufferSize: 4096,
          useAsync: true);

      using var reader = new StreamReader(stream);

      // This is cancellable
      return await reader.ReadToEndAsync(token);
   }

   private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
   {
      _debounceTimer.Stop();
      _debounceTimer.Start();
   }

   private void RunSearch()
   {
      var query = SearchBox.Text.Trim();
      if (query.Length < 2)
         return;

      bool? queryAsPrefix = IndexedSearch.GetQueryMode(query) switch
      {
         IndexedSearch.QueryMode.RunAsPrefix => true,
         IndexedSearch.QueryMode.RunAsIs => false,
         _ => null
      };

      if (!queryAsPrefix.HasValue)
         return;
      
      var results = IndexedSearch.Search(ArchiveFolder, query, queryAsPrefix.Value)
                                 .OrderByDescending(result => result.Path)
                                 .ToList();

      ResultsList.ItemsSource = results;
      ResultsList.UpdateLayout();
   }

   private async void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
   {
      if (ResultsList.SelectedItem is SearchResult result)
         await LoadPreviewAsync(result.Path!);
   }

   public async Task LoadPreviewAsync(string path)
   {
      _previewCts?.Cancel();
      _previewCts = new CancellationTokenSource();
      var token = _previewCts.Token;

      string url;
      try
      {
         url = PreviewBaseUrl + Uri.EscapeDataString(Path.GetRelativePath(ArchiveFolder, path));
      }
      catch (Exception ex) when (ex is ArgumentException or UriFormatException)
      {
         // Invalid path or invalid URI — do not navigate
         Log(ex, nameof(LoadPreviewAsync));
         return;
      }

      try
      {
         await _webViewInitTask;

         token.ThrowIfCancellationRequested();

         PreviewBrowser.CoreWebView2.Navigate(url);
      }
      catch (OperationCanceledException)
      {
         // User changed selection — ignore
      }
      catch (Exception ex)
      {
         Log(ex, nameof(LoadPreviewAsync));
      }
   }

   private static void Log(Exception ex, string context) 
   { 
      Trace.WriteLine($"[{context}] {ex}"); 
   }
}