namespace Brx.CopilotArchiveSync.Utils;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class HexEncoder
{
   public const int MD5_HASH_BYTES = 16;
   public const int MD5_HASH_CHARS = MD5_HASH_BYTES * 2;
   public const int MD5_HASH_BITS = MD5_HASH_BYTES * 8;

   public const int SHA256_HASH_BYTES = 32;
   public const int SHA256_HASH_CHARS = SHA256_HASH_BYTES * 2;
   public const int SHA256_HASH_BITS = SHA256_HASH_BYTES * 8;

   private static readonly char[] HexDigits = "0123456789abcdef".ToCharArray();

   // ------------------------------------------------------------
   // IEnumerable<char> iterator
   // ------------------------------------------------------------
   public static IEnumerable<char> GetHexChars(byte[] bytes)
   {
      for (int i = 0; i < bytes.Length; i++)
      {
         byte b = bytes[i];
         yield return HexDigits[b >> 4];
         yield return HexDigits[b & 0xF];
      }
   }

   // ------------------------------------------------------------
   // StringBuilder
   // ------------------------------------------------------------
   public static void WriteHex(StringBuilder sb, byte[] bytes)
   {
      for (int i = 0; i < bytes.Length; i++)
      {
         byte b = bytes[i];
         sb.Append(HexDigits[b >> 4]);
         sb.Append(HexDigits[b & 0xF]);
      }
   }

   // ------------------------------------------------------------
   // StreamWriter 
   // ------------------------------------------------------------
   public static void WriteHex(StreamWriter writer, byte[] bytes)
   {
      for (int i = 0; i < bytes.Length; i++)
      {
         byte b = bytes[i];
         writer.Write(HexDigits[b >> 4]);
         writer.Write(HexDigits[b & 0xF]);
      }
   }

   // ------------------------------------------------------------
   // string returners
   // ------------------------------------------------------------
   public static string ToHexString(byte[] bytes)
   {
      ArgumentNullException.ThrowIfNull(bytes);

      return ToHexString(bytes, 0, bytes.Length);
   }

   public static string ToHexString(byte[] bytes, int byteIndex, int byteCount)
   {
      int hexDigitCount = byteCount * 2;
      char[] buffer = new char[hexDigitCount];

      if (ToHexChars(bytes, byteIndex, byteCount, buffer, 0, hexDigitCount) != hexDigitCount)
      {
         // this should not happen
         throw new InternalBufferOverflowException();
      }

      return new string(buffer, 0, hexDigitCount);
   }

   // ------------------------------------------------------------
   // Write into a caller-provided char[] slice (best effort)
   // ------------------------------------------------------------
   // Returns: number of chars written
   //
   public static int ToHexChars(
       byte[] bytes,
       int byteIndex,
       int maxByteCount,
       char[] chars,
       int charIndex,
       int maxCharIndex)
   {
      ArgumentNullException.ThrowIfNull(bytes);
      ArgumentNullException.ThrowIfNull(chars);

      // Structural index validation only
      if (byteIndex < 0 || byteIndex > bytes.Length)
         throw new ArgumentOutOfRangeException(nameof(byteIndex));
      if (maxByteCount < 0)
         throw new ArgumentOutOfRangeException(nameof(maxByteCount));

      if (charIndex < 0 || charIndex > chars.Length)
         throw new ArgumentOutOfRangeException(nameof(charIndex));
      if (maxCharIndex < 0)
         throw new ArgumentOutOfRangeException(nameof(maxCharIndex));

      // maxCharIndex is an upper bound, not a contract -> clamp
      if (maxCharIndex > chars.Length)
         maxCharIndex = chars.Length;

      // If end <= start -> empty slice
      if (maxCharIndex <= charIndex)
         return 0;

      // --- Best effort begins here ---

      // How many bytes are actually available?
      int availableBytes = bytes.Length - byteIndex;
      if (availableBytes < 0)
         availableBytes = 0;

      int bytesToProcess = maxByteCount < availableBytes
          ? maxByteCount
          : availableBytes;

      // How many chars can we write?
      int availableChars = maxCharIndex - charIndex;

      // Each byte -> 2 chars
      int maxBytesByCharLimit = availableChars / 2;

      // Best‑effort: take the smaller of the two limits
      if (bytesToProcess > maxBytesByCharLimit)
         bytesToProcess = maxBytesByCharLimit;

      int ci = charIndex;
      int bi = byteIndex;

      for (int i = 0; i < bytesToProcess; i++)
      {
         byte b = bytes[bi++];
         chars[ci++] = HexDigits[b >> 4];
         chars[ci++] = HexDigits[b & 0xF];
      }

      return ci - charIndex;
   }

   // ------------------------------------------------------------
   // FoldHash overloads
   // ------------------------------------------------------------
   public static string FoldHash(byte[] hash, int targetByteLength)
   {
      ArgumentNullException.ThrowIfNull(hash);

      return FoldHash(hash, 0, hash.Length, targetByteLength);
   }

   public static string FoldHash(byte[] hash, int offset, int length, int targetByteLength)
   {
      if (hash == null)
         throw new ArgumentNullException(nameof(hash));

      if (offset < 0 || offset > hash.Length)
         throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)} is out of range");

      if (length <= 0 || offset + length > hash.Length)
         throw new ArgumentOutOfRangeException(nameof(length), $"{nameof(length)} is out of range");

      if (targetByteLength <= 0)
         throw new ArgumentOutOfRangeException(nameof(targetByteLength), $"{nameof(targetByteLength)} must be positive");

      if (length % targetByteLength != 0)
         throw new ArgumentException($"{nameof(targetByteLength)} must divide {nameof(length)} exactly", nameof(targetByteLength));

      int ratio = length / targetByteLength;

      if ((ratio & (ratio - 1)) != 0)
         throw new ArgumentException($"{nameof(length)} / {nameof(targetByteLength)} must be a power of two", nameof(targetByteLength));

      // XOR folding in-place
      int currentLength = length;

      while (currentLength > targetByteLength)
      {
         int half = currentLength / 2;

         for (int i = 0; i < half; i++)
            hash[offset + i] ^= hash[offset + i + half];

         currentLength = half;
      }

      return ToHexString(hash, offset, currentLength);
   }

   private const string Alphabet36 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

   public static string FoldString(string value, int targetLength)
   {
      if (value == null)
         throw new ArgumentNullException(nameof(value));

      if (targetLength <= 0)
         throw new ArgumentOutOfRangeException(nameof(targetLength), $"{nameof(targetLength)} must be positive");

      if (value.Length % targetLength != 0)
         throw new ArgumentException($"{nameof(targetLength)} must divide {nameof(value.Length)} exactly", nameof(targetLength));

      int ratio = value.Length / targetLength;
      if ((ratio & (ratio - 1)) != 0)
         throw new ArgumentException($"{nameof(value.Length)} / {nameof(targetLength)} must be a power of two", nameof(targetLength));

      if (value.Length == targetLength)
         return value;

      int currentLength = value.Length;
      int half = currentLength / 2;
      var chars = new char[half];

      for (int i = 0; i < half; i++)
      {
         chars[i] = FoldChar(value[i], value[half + i]);
      }

      currentLength = half;

      while (currentLength > targetLength)
      {
         half = currentLength / 2;
         for (int i = 0; i < half; i++)
         {
            chars[i] = FoldChar(chars[i], chars[half + i]);
         }
         currentLength = half;
      }

      return new string(chars, 0, currentLength);
   }

   private static char FoldChar(char a, char b)
   {
      int xor = a ^ b;
      int idx = xor % Alphabet36.Length;
      if (idx < 0)
      {
         idx += Alphabet36.Length;
      }
      return Alphabet36[idx];
   }
}