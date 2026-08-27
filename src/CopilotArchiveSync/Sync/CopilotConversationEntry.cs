namespace Brx.CopilotArchiveSync.Sync;

using System;
using System.IO;
using Brx.CopilotArchiveSync.Conversation;
using Brx.CopilotArchiveSync.Metadata;
using Brx.CopilotArchiveSync.Utils;

public class CopilotConversationEntry
{
   public Source Source { get; set; }
   public string IdentityHash { get; set; }
   public string ContentHash { get; set; }
   public long ContentSize { get; set; }
   public string FilePath { get; set; }

   public static CopilotConversationEntry CreateFrom(CopilotConversation conversation, Source source)
   {
      string fileName = GenerateFileName(conversation);

      var entry = new CopilotConversationEntry
      {
         Source = source,
         IdentityHash = conversation.IdentityHash,
         ContentHash = null,   // remote has no ADS; local loader fills this
         ContentSize = -1,     // sentinel: unknown size
         FilePath = fileName   // filename only, no folder path
      };

      return entry;
   }

   public static CopilotConversationEntry LoadFrom(string filePath, Source source)
   {
      var info = new FileInfo(filePath);

      return new CopilotConversationEntry
      {
         Source = source,
         FilePath = filePath,
         ContentSize = info.Length,
         IdentityHash = HashMetadataHelper.ReadIdentityHash(filePath),
         ContentHash = HashMetadataHelper.ReadContentHash(filePath)
      };
   }

   internal static string GenerateFileName(CopilotConversation conversation)
   {
      var fileNamePrefix = $"{conversation.FirstMessage.Time:yyyy-MM-dd}-";
      var fileNameSuffix = $" ({conversation.FoldedIdentityHash}){CopilotConversation.OutputFileExt}";
      var fileNameMaxLenght = PathLimits.MaxPracticalFileName - fileNamePrefix.Length - fileNameSuffix.Length;
      var fileName = fileNamePrefix + FileNameSanitizer.SanitizeTitle(conversation.Title, fileNameMaxLenght) + fileNameSuffix;
      return fileName;
   }
}
