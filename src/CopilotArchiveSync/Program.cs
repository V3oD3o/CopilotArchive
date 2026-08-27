namespace Brx.CopilotArchiveSync;

using System;
using System.IO;
using System.Linq;
using System.Text;

using Brx.CopilotArchiveSync.Sync;
using Brx.CopilotArchiveSync.Utils;
using Brx.FlatFileMonger;

internal class Program
{
   private static class ColumnNames
   {
      public static readonly string Conversation = "Conversation";
      public static readonly string Time = "Time";
      public static readonly string Author = "Author";
      public static readonly string Message = "Message";
   }

   private const string DefaultInputFileName = "copilot-activity-history.csv";
   private const string DownloadedFilePattern = "copilot-activity-history*.csv";

   public static void Main(string[] args)
   {
      var workingFolderPath = (args.Length > 0) ? args[0] : Directory.GetCurrentDirectory();
      var inputFileArg = (args.Length > 1) ? args[1] : DefaultInputFileName;
      var inputFilePath = Path.IsPathRooted(inputFileArg) ? inputFileArg : Path.Combine(workingFolderPath, inputFileArg);
      var latestDownloadFile = GetLatestDownloadFileInfo(DownloadedFilePattern);
      if (latestDownloadFile != null)
      {
         FileInfo inputFile = File.Exists(inputFilePath) ? new FileInfo(inputFilePath) : null;
         if (inputFile == null || inputFile.LastWriteTimeUtc < latestDownloadFile.LastWriteTimeUtc)
         {
            if (inputFile == null)
            {
               Console.WriteLine($"Copying latest downloaded archive CSV to working folder: {latestDownloadFile.Name}");
               File.Copy(latestDownloadFile.FullName, inputFilePath, false);
            }
            else
            {
               Console.WriteLine($"  Existing archive CSV: [{inputFile.LastWriteTimeUtc}] {inputFile.Name} ");
               Console.WriteLine($" Latest downloaded CSV: [{latestDownloadFile.LastWriteTimeUtc}] {latestDownloadFile.Name} ");
               if (Confirm("Do you want to overwrite existing archive CSV with newer downloaded file?"))
               {
                  string archiveFilePath = GetArchiveFilePath(inputFile);
                  Console.WriteLine($"Renaming old archive CSV to: {Path.GetFileName(archiveFilePath)}");
                  File.Move(inputFilePath, archiveFilePath);
                  Console.WriteLine($"Overwriting old archive CSV with latest downloaded file: {latestDownloadFile.Name}");
                  File.Copy(latestDownloadFile.FullName, inputFilePath, true);
               }
            }
         }
         else if (inputFile.LastWriteTimeUtc > latestDownloadFile.LastWriteTimeUtc)
         {
            Console.WriteLine($"Existing archive CSV in working folder is newer than latest downloaded file: {latestDownloadFile.Name}");
         }
      }
      else if (!File.Exists(inputFilePath))
      {
         Console.WriteLine($"Input file not found: {inputFilePath}");
         return;
      }

      var options = new CsvFormatOptions()
      {
         Encoding = Encoding.UTF8,
         HasHeaderRow = true,
         Delimiter = ',',
         QuoteChar = '"',
         PreserveWhiteSpace = false,
         NewLineMode = NewLineModeEnum.Auto,
      };

      Console.WriteLine($"Reading history file: {Path.GetFileName(inputFilePath)}");

      var conversations = CopilotExportReader.Load(inputFilePath);

      Console.WriteLine($"Number of conversations found: {conversations.Count}");

      var folderName = Path.GetFileNameWithoutExtension(DefaultInputFileName);
      var localRoot = Path.Combine(workingFolderPath, folderName);
      var backupRoot = localRoot + ".bak";
      Directory.CreateDirectory(backupRoot);
      var sync = new SyncEngine(localRoot, backupRoot);

      sync.Run(conversations, Console.WriteLine);
   }

   private static string GetArchiveFilePath(FileInfo inputFile)
   {
      string dateTag = inputFile.LastWriteTime.ToString("yyyy-MM-dd");
      string archiveFilePath = Path.ChangeExtension(inputFile.FullName, dateTag + inputFile.Extension);
      if (File.Exists(archiveFilePath))
      {
         int i = 0;
         string altArchiveFilePath;
         do
         {
            altArchiveFilePath = Path.ChangeExtension(archiveFilePath, (++i).ToString() + inputFile.Extension);
         } while (File.Exists(altArchiveFilePath));
         archiveFilePath = altArchiveFilePath;
      }
      return archiveFilePath;
   }

   private static FileInfo GetLatestDownloadFileInfo(string pattern)
   {
      var downloadFolderPath = KnownFolders.GetKnownFolderPath(KnownFolders.Guids.Downloads);
      if (!string.IsNullOrEmpty(downloadFolderPath))
      {
         var files = Directory.GetFiles(downloadFolderPath, pattern);
         if (files.Length > 0)
         {
            var latest = files
               .Select(f => new FileInfo(f))
               .OrderByDescending(f => f.LastWriteTimeUtc)
               .First();

            return latest;
         }
      }
      return null;
   }

   public static bool Confirm(string title)
   {
      ConsoleKey response;
      do
      {
         Console.Write($"{title} [y/n] ");
         response = Console.ReadKey(false).Key;
         if (response != ConsoleKey.Enter)
         {
            Console.WriteLine();
         }
      } while (response != ConsoleKey.Y && response != ConsoleKey.N);

      return (response == ConsoleKey.Y);
   }
}
