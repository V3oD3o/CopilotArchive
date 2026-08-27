namespace Brx.CopilotArchiveSync.Conversation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;

using Brx.CopilotArchiveSync.Metadata;
using Brx.CopilotArchiveSync.Utils;
using Brx.CopilotArchiveSync.Sync;

public class CopilotConversation
{
   public const int FormatVersion = 2; // increment when file format is changing (e.g. new header is added)

   public const string OutputFileExt = ".md";

   public const string HeaderTimeFormat = "yyyy-MM-dd HH:mm:ss";
   public const string HeaderTimeUnknown = "Unknown";

   public static class HeaderNames
   {
      public const string Title = "Title";
      public const string Participants = "Participants";
      public const string MessageCount = "Message-Count";
      public const string FirstMessageTime = "First-Message-Time";
      public const string LastMessageTime = "Last-Message-Time";
      public const string FormatVersion = "Format-Version";
   }

   private string _identityHash;
   private string _foldedIdentityHash;

   public CopilotConversation(string title, IReadOnlyList<CopilotMessage> messages)
   {
      Title = title;
      Messages = messages ?? throw new ArgumentNullException(nameof(messages));
   }

   public string Title { get; private set; }

   public IReadOnlyList<CopilotMessage> Messages { get; }

   public CopilotMessage FirstMessage => Messages.Count > 0 ? Messages[^1] : null;

   public CopilotMessage LastMessage => Messages.Count > 0 ? Messages[0] : null;

   public bool IsLatestMessageFirst => true;

   public string IdentityHash
   {
      get
      {
         return _identityHash ??= IdentityHashEncoder.ComputeIdentityHash(Messages, IsLatestMessageFirst);
      }
   }

   public string FoldedIdentityHash
   {
      get
      {
         if (_foldedIdentityHash == null)
         {
            string hash = IdentityHash;
            if (hash != null && (hash.Length == HexEncoder.MD5_HASH_CHARS || hash.Length == HexEncoder.SHA256_HASH_CHARS))
            {
               _foldedIdentityHash = HexEncoder.FoldString(hash, 8);
            }
         }
         return _foldedIdentityHash;
      }
   }

   public CopilotConversationEntry SaveTo(string filePath)
   {
      long contentSize;
      string contentHash;

      // Write main file content and compute content hash
      using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
      {
         // take stream size first (WriteContentAndComputeHash closes the stream)
         contentSize = fs.Length; 
         contentHash = WriteContentAndComputeHash(fs);
      }
      // FileStream is now CLOSED - ADS writes are allowed.

      // Write ADS metadata
      HashMetadataHelper.WriteIdentityHash(filePath, IdentityHash);
      HashMetadataHelper.WriteContentHash(filePath, contentHash);

      return new CopilotConversationEntry
      {
         Source = Source.Remote,
         IdentityHash = IdentityHash,
         ContentHash = contentHash,
         ContentSize = contentSize,
         FilePath = filePath
      };
   }

   private void WriteConversationHeader(TextWriter writer)
   {
      var headerValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
         { HeaderNames.Title, Title },
         { HeaderNames.MessageCount, Messages.Count.ToString(CultureInfo.InvariantCulture) },
         { HeaderNames.FirstMessageTime, FirstMessage?.Time.ToString(HeaderTimeFormat) ?? HeaderTimeUnknown },
         { HeaderNames.LastMessageTime, LastMessage?.Time.ToString(HeaderTimeFormat) ?? HeaderTimeUnknown },
         { HeaderNames.FormatVersion, FormatVersion.ToString() },
      };
      StructuredHeaderBlock headerBlock = StructuredHeaderBlock.LoadFrom(headerValues, StringComparer.OrdinalIgnoreCase);
      headerBlock.WriteTo(writer);
   }

   private string WriteContentAndComputeHash(Stream outputStream)
   {
      using var shaContent = SHA256.Create();
      using var cryptoContent = new CryptoStream(outputStream, shaContent, CryptoStreamMode.Write);
      using var writer = new StreamWriter(cryptoContent, new UTF8Encoding(true));

      WriteConversationHeader(writer);

      // Conversation title
      writer.WriteLine("# Conversation: " + Title);
      writer.WriteLine();

      // Started timestamp
      string started = FirstMessage?.Time.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";

      // Unique participants
      var participants = Messages
          .Select(m => m.Author)
          .Where(a => !string.IsNullOrWhiteSpace(a))
          .Distinct()
          .OrderBy(a => a)
          .ToList();

      string participantList = participants.Count > 0
          ? string.Join(", ", participants)
          : "Unknown";

      // Metadata
      writer.WriteLine("**Started:** " + started + "  ");
      writer.WriteLine("**Messages:** " + Messages.Count + "  ");
      writer.WriteLine("**Participants:** " + participantList + "  ");
      writer.WriteLine();
      writer.WriteLine("---");
      writer.WriteLine();

      // Messages
      // NOTE: Copilot exports messages newest -> oldest. To restore chronological order, we write them in reverse order.
      for (int i = Messages.Count - 1; i >= 0; i--)
      {
         var msg = Messages[i];
         string timestamp = msg.Time.ToString("yyyy-MM-dd HH:mm:ss");
         string author = string.IsNullOrEmpty(msg.Author) ? "Unknown" : msg.Author;

         writer.WriteLine("## " + timestamp + " - " + author);
         writer.WriteLine();
         writer.WriteLine(msg.Message);
         writer.WriteLine();
         writer.WriteLine("---");
         writer.WriteLine();
      }

      writer.Flush();
      cryptoContent.FlushFinalBlock();

      return HexEncoder.ToHexString(shaContent.Hash!);
   }
}
