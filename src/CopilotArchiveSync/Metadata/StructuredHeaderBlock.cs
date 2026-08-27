namespace Brx.CopilotArchiveSync.Metadata;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

public sealed class StructuredHeaderBlock
{
   private sealed class Head
   {
      public readonly StructuredHeader First;
      public StructuredHeader Last;

      public Head(StructuredHeader header)
      {
         ArgumentNullException.ThrowIfNull(header);

         First = header;
         Last = header;
      }
   }

   private readonly StringComparer _comparer;
   private readonly Dictionary<string, Head> _map;
   private StructuredHeader _first;
   private StructuredHeader _last;

   private readonly List<StructuredHeader> _fields = new List<StructuredHeader>();
   private readonly List<StructuredHeader> _duplicates = new List<StructuredHeader>();

   public IEnumerable<StructuredHeader> Fields => _fields;
   public IEnumerable<StructuredHeader> Duplicates => _duplicates;

   public StructuredHeaderBlock(StringComparer comparer)
   {
      _comparer = comparer ?? StringComparer.Ordinal;
      _map = new Dictionary<string, Head>(_comparer);
   }

   public static StructuredHeaderBlock LoadFrom(IEnumerable<StructuredHeader> items, StringComparer comparer)
   {
      var block = new StructuredHeaderBlock(comparer);

      foreach (var header in items)
      {
         block.Append(header);
      }

      return block;
   }

   public static StructuredHeaderBlock LoadFrom(IDictionary<string, string> dictionary, StringComparer comparer)
   {
      var block = new StructuredHeaderBlock(comparer);

      foreach (var entry in dictionary)
      {
         var header = new StructuredHeader(entry.Key, entry.Value);
         block.Append(header);
      }

      return block;
   }

   public static StructuredHeaderBlock LoadFrom(TextReader reader, StringComparer comparer)
   {
      // YAML frontmatter must start with triple dash ---
      string line = reader.ReadLine();
      if (line != "---")
         return null;

      var block = new StructuredHeaderBlock(comparer);
      StructuredHeader lastExplicit = null;

      while ((line = reader.ReadLine()) != null)
      {
         if (line == "---")
            break;

         if (line.Length == 0)
            continue;

         if (line[0] == ' ' || line[0] == '\t')
         {
            if (lastExplicit == null)
               throw new InvalidDataException("Continuation without preceding header.");

            var continuation = new StructuredHeader(null, line.Substring(1));
            block.Append(continuation);
            continue;
         }

         int colonIndex = line.IndexOf(':');
         if (colonIndex <= 0)
            throw new InvalidDataException("Invalid header line: " + line);

         string key = line.Substring(0, colonIndex);
         string value = line.Substring(colonIndex + 1).TrimStart();

         var header = new StructuredHeader(key, value);
         block.Append(header);

         lastExplicit = header;
      }

      return block;
   }

   private void Append(StructuredHeader header)
   {
      ArgumentNullException.ThrowIfNull(header);

      // Document order chain
      if (_first == null)
      {
         _first = header;
         _last = header;
      }
      else
      {
         _last!.Next = header;
         _last = header;
      }

      _fields.Add(header);

      // Duplicate chain only for explicit headers
      if (header.Key != null)
      {
         if (!_map.TryGetValue(header.Key, out var head))
         {
            _map[header.Key] = new Head(header);

            // This is the first occurrence
            header.IsDuplicate = false;
         }
         else
         {
            head.Last.NextDuplicate = header;
            head.Last = header;

            // This is a duplicate
            header.IsDuplicate = true;
            _duplicates.Add(header);
         }
      }
   }

   public bool TryGet(string key, [NotNullWhen(true)] out string value)
   {
      if (_map.TryGetValue(key, out var head))
      {
         value = head.First!.ValueLine;
         return true;
      }

      value = null;
      return false;
   }

   public IEnumerable<StructuredHeader> EnumerateDuplicates(string key)
   {
      if (_map.TryGetValue(key, out var head))
      {
         var node = head.First;
         while (node != null)
         {
            yield return node;
            node = node.NextDuplicate;
         }
      }
   }

   public IEnumerable<string> EnumerateValueLines(string key)
   {
      if (_map.TryGetValue(key, out var head))
      {
         foreach (var line in head.First!.EnumerateValueLines())
            yield return line;
      }
   }

   public string GetConcatenatedValue(string key, string separator)
   {
      if (_map.TryGetValue(key, out var head))
         return head.First.GetConcatenatedValue(separator);

      return null;
   }

   public IEnumerable<StructuredHeader> EnumerateDocumentOrder()
   {
      var node = _first;
      while (node != null)
      {
         yield return node;
         node = node.Next;
      }
   }

   public void WriteTo(TextWriter writer)
   {
      writer.WriteLine("---");

      foreach (var kv in _map)
         WriteField(writer, kv.Value.First);

      writer.WriteLine("---");
      writer.WriteLine();
   }

   private static void WriteField(TextWriter writer, StructuredHeader header)
   {
      writer.WriteLine(header.Key + ": " + header.ValueLine);

      var node = header.Next;
      while (node != null && node.Key == null)
      {
         writer.WriteLine(" " + node.ValueLine);
         node = node.Next;
      }
   }
}
