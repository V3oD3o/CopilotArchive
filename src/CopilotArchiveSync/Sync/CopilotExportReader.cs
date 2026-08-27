namespace Brx.CopilotArchiveSync.Sync;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Brx.CopilotArchiveSync.Conversation;
using Brx.FlatFileMonger;

internal static class CopilotExportReader
{
   private static class ColumnNames
   {
      public static readonly string Conversation = "Conversation";
      public static readonly string Time = "Time";
      public static readonly string Author = "Author";
      public static readonly string Message = "Message";
   }

   internal static Dictionary<string, CopilotConversation> Load(string inputFilePath)
   {
      var options = new CsvFormatOptions()
      {
         Encoding = Encoding.UTF8,
         HasHeaderRow = true,
         Delimiter = ',',
         QuoteChar = '"',
         PreserveWhiteSpace = false,
         NewLineMode = NewLineModeEnum.Auto,
      };

      var builders = new Dictionary<string, CopilotConversationBuilder>();

      using (CsvReader reader = new CsvReader(new StreamReader(inputFilePath, true), options))
      {
         if (reader.ReadHeader())
         {
            while (reader.Read())
            {
               string message = reader[ColumnNames.Message];
               if (string.IsNullOrEmpty(message))
               {
                  continue;
               }
               string title = reader[ColumnNames.Conversation];
               if (string.IsNullOrEmpty(title))
               {
                  title = "Untitled";
               }

               if (!builders.TryGetValue(title, out CopilotConversationBuilder builder))
               {
                  builder = new CopilotConversationBuilder(title);
                  builders.Add(title, builder);
               }
               builder.AddMessage(
                  new CopilotMessage()
                  {
                     Time = DateTime.Parse(reader[ColumnNames.Time]),
                     Author = reader[ColumnNames.Author],
                     Message = message
                  }
               );
            }
         }
      }

      return builders.ToDictionary(
          kv => kv.Key,
          kv => kv.Value.Build()
      );
   }
}
