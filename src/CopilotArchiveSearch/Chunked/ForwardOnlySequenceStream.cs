namespace Brx.CopilotArchiveSearch.Chunked;

using System;
using System.Buffers;
using System.IO;

public sealed class ForwardOnlySequenceStream : Stream
{
   private readonly ReadOnlySequence<byte> _sequence;
   private ReadOnlySequence<byte> _remaining;
   private long _position;
   private readonly long _length;
   private bool _finished;

   public ForwardOnlySequenceStream(ReadOnlySequence<byte> sequence)
   {
      _sequence = sequence;
      _remaining = sequence;
      _length = sequence.Length;
      _position = 0;
      _finished = sequence.IsEmpty;
   }

   public override bool CanRead => true;
   public override bool CanSeek => false; // forward-only
   public override bool CanWrite => false;

   public override long Length => _length;

   public override long Position
   {
      get => _position;
      set => throw new NotSupportedException();
   }

   public override int Read(byte[] buffer, int offset, int count)
   {
      if (_finished || count == 0)
         return 0;

      var slice = _remaining;
      if (slice.IsEmpty)
      {
         _finished = true;
         return 0;
      }

      int totalRead = 0;

      while (count > 0 && !slice.IsEmpty)
      {
         var span = slice.FirstSpan;
         int toCopy = Math.Min(span.Length, count);

         span.Slice(0, toCopy).CopyTo(buffer.AsSpan(offset, toCopy));

         offset += toCopy;
         count -= toCopy;
         totalRead += toCopy;

         slice = slice.Slice(toCopy);
      }

      _remaining = slice;
      _position += totalRead;

      if (_remaining.IsEmpty)
         _finished = true;

      return totalRead;
   }

   public override long Seek(long offset, SeekOrigin origin)
   {
      // Allow only no-op seek: Seek(0, Begin) when already at position 0
      if (origin == SeekOrigin.Begin && offset == 0 && _position == 0)
         return 0;

      throw new NotSupportedException();
   }

   public override void Flush() { }

   public override void SetLength(long value) =>
       throw new NotSupportedException();

   public override void Write(byte[] buffer, int offset, int count) =>
       throw new NotSupportedException();
}
