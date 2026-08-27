namespace Brx.CopilotArchiveSync.Metadata;

using System.Collections.Generic;
using System.Text;

public sealed class StructuredHeader
{
   public string Key { get; }                            // null means continuation line
   public string ValueLine { get; }                      // raw line content
   public bool IsDuplicate { get; internal set; }
   public StructuredHeader Next { get; internal set; }   // next line in document order
   public StructuredHeader NextDuplicate { get; internal set; }

   public bool IsContinued => Next != null && Next.Key == null;
   public bool IsUnique => NextDuplicate == null && !IsDuplicate;

   public StructuredHeader(string key, string valueLine)
   {
      Key = key;
      ValueLine = valueLine;
   }

   public IEnumerable<StructuredHeader> EnumerateDuplicates()
   {
      var node = this;
      while (node != null)
      {
         yield return node;
         node = node.NextDuplicate;
      }
   }

   public IEnumerable<string> EnumerateValueLines()
   {
      yield return ValueLine;

      var node = Next;
      while (node != null && node.Key == null)
      {
         yield return node.ValueLine;
         node = node.Next;
      }
   }

   public string GetConcatenatedValue(string separator)
   {
      separator ??= string.Empty;

      StructuredHeader header;
      string inject;

      int sum = 0;
      int cnt = 0;

      header = this;
      inject = string.Empty;
      do
      {
         cnt++;
         sum += inject.Length;
         if (header.ValueLine != null)
         {
            sum += header.ValueLine.Length;
         }
         header = header.Next;
         inject = separator;
      } while (header != null && header.Key == null);

      if (cnt == 1)
      {
         return ValueLine;
      }

      var sb = new StringBuilder();

      header = this;
      inject = string.Empty;
      do
      {
         sb.Append(inject);
         if (header.ValueLine != null)
         {
            sb.Append(header.ValueLine);
         }
         header = header.Next;
         inject = separator;
      } while (header != null && header.Key == null);

      return sb.ToString();
   }

   public List<string> GetValueLinesAsList()
   {
      var list = new List<string>();
      foreach (var line in EnumerateValueLines())
         list.Add(line);
      return list;
   }
}
