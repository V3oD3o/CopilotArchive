namespace Brx.CopilotArchiveSync.Sync;

using System;
using System.Collections.Generic;
using System.IO;

public sealed class CopilotConversationFolder
{
   public string FolderPath { get; }
   public string SearchPattern { get; }
   public Dictionary<string, CopilotConversationEntry> Entries { get; }          
   public List<CopilotConversationEntry> Duplicates { get; }
       
   private CopilotConversationFolder(string folderPath, string searchPattern)
   {
      Entries = new Dictionary<string, CopilotConversationEntry>(StringComparer.OrdinalIgnoreCase);
      Duplicates = new List<CopilotConversationEntry>();
      FolderPath = folderPath;
      SearchPattern = searchPattern;
   }

   public static CopilotConversationFolder LoadFrom(string folderPath, string searchPattern, Source source) 
   {
      var folder = new CopilotConversationFolder(folderPath, searchPattern);

      foreach (var filePath in Directory.EnumerateFiles(folderPath, searchPattern, SearchOption.TopDirectoryOnly))
      {
         var entry = CopilotConversationEntry.LoadFrom(filePath, source);

         if (string.IsNullOrEmpty(entry.IdentityHash))
            continue;

         if (folder.Entries.TryGetValue(entry.IdentityHash, out var existing))
         {
            // longest file wins
            if (entry.ContentSize > existing.ContentSize)
            {
               // existing becomes duplicate
               folder.Duplicates.Add(existing);

               // replace with the longer entry
               folder.Entries[entry.IdentityHash] = entry;
            }
            else
            {
               // new entry is the loser
               folder.Duplicates.Add(entry);
            }

            continue;
         }

         folder.Entries.Add(entry.IdentityHash, entry);
      }

      return folder;
   }

}
