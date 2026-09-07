
namespace Brx.CopilotArchiveSearch.Chunked;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

public sealed class ChunkedUtf8TextWriter : TextWriter
{
   private readonly List<ReadOnlyMemory<byte>> _segments = new();
   private readonly int _chunkSize;
   private readonly Encoder _encoder;

   private byte[] _current;
   private int _pos;

   public ChunkedUtf8TextWriter(int chunkSize = 8192)
   {
      _chunkSize = chunkSize;
      _current = new byte[_chunkSize];
      _encoder = Encoding.UTF8.GetEncoder();
   }

   public override Encoding Encoding => Encoding.UTF8;

   public override void Write(char value)
   {
      Span<char> one = stackalloc char[1] { value };
      Write(one);
   }

   public override void Write(string? value)
   {
      if (value is null)
         return;

      Write(value.AsSpan());
   }

   public override void Write(char[] buffer, int index, int count)
   {
      Write(buffer.AsSpan(index, count));
   }

   public override void Write(ReadOnlySpan<char> chars)
   {
      while (!chars.IsEmpty)
      {
         // Ensure at least 4 bytes of space before encoding
         if (_current.Length - _pos < 4)
            FlushChunk();

         Span<byte> dest = _current.AsSpan(_pos);

         _encoder.Convert(
             chars,
             dest,
             flush: false,
             out int charsUsed,
             out int bytesUsed,
             out bool completed);

         chars = chars.Slice(charsUsed);
         _pos += bytesUsed;

         if (!completed)
         {
            // Encoder needs more space
            FlushChunk();
         }
      }
   }

   private void FlushChunk()
   {
      if (_pos == 0)
         return;

      _segments.Add(new ReadOnlyMemory<byte>(_current, 0, _pos));
      _current = new byte[_chunkSize];
      _pos = 0;
   }

   private void FlushEncoder()
   {
      bool done = false;

      while (!done)
      {
         if (_pos == _current.Length)
            FlushChunk();

         Span<byte> dest = _current.AsSpan(_pos);

         _encoder.Convert(
             ReadOnlySpan<char>.Empty,
             dest,
             flush: true,
             out int charsUsed,
             out int bytesUsed,
             out bool completed);

         _pos += bytesUsed;
         done = completed;

         if (!completed && _pos == _current.Length)
            FlushChunk();
      }
   }

   public ReadOnlySequence<byte> ToSequence()
   {
      FlushEncoder();
      FlushChunk();

      SequenceSegment? first = null;
      SequenceSegment? last = null;

      foreach (var mem in _segments)
      {
         var seg = new SequenceSegment(mem);

         if (last is null)
         {
            first = seg;
            last = seg;
         }
         else
         {
            last.SetNext(seg);
            last = seg;
         }
      }

      if (first is null)
         return ReadOnlySequence<byte>.Empty;

      return new ReadOnlySequence<byte>(first, 0, last!, last!.Memory.Length);
   }

   private sealed class SequenceSegment : ReadOnlySequenceSegment<byte>
   {
      public SequenceSegment(ReadOnlyMemory<byte> memory)
      {
         Memory = memory;
      }

      public void SetNext(SequenceSegment next)
      {
         Next = next;
         next.RunningIndex = RunningIndex + Memory.Length;
      }
   }
}