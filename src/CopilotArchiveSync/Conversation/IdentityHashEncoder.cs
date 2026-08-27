namespace Brx.CopilotArchiveSync.Conversation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Linq;

using Brx.CopilotArchiveSync.Utils;

internal static class IdentityHashEncoder
{
   private const string EncodedNull = "-1|";
   private const string EncodedEmpty = "0|";

   private static void WriteNull(StreamWriter writer)
   {
      writer.Write(EncodedNull);
   }

   private static void WriteEmpty(StreamWriter writer)
   {
      writer.Write(EncodedEmpty);
   }


   private static void WriteIdentityField(StreamWriter writer, string value)
   {
      if (value == null)
      {
         WriteNull(writer);
         return;
      }

      if (value.Length == 0)
      {
         WriteEmpty(writer);
         return;
      }

      writer.Write(value.Length.ToString(CultureInfo.InvariantCulture));
      writer.Write('|');
      writer.Write(value);
   }

   private static void WriteIdentityField(StreamWriter writer, byte[] data)
   {
      if (data == null)
      {
         WriteNull(writer);
         return;
      }

      if (data.Length == 0)
      {
         WriteEmpty(writer);
         return;
      }

      string base64 = Convert.ToBase64String(data);

      writer.Write(base64.Length.ToString(CultureInfo.InvariantCulture));
      writer.Write('|');
      writer.Write(base64);
   }

   public static string ComputeIdentityHash(IReadOnlyList<CopilotMessage> messages)
   {
      return ComputeIdentityHash(messages, true);
   }

   public static string ComputeIdentityHash(IReadOnlyList<CopilotMessage> messages, bool latestMessageFirst)
   {
      using var sha = SHA256.Create();
      using var crypto = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
      using var writer = new StreamWriter(crypto, new UTF8Encoding(false));

      IEnumerable<CopilotMessage> oldestFewMessages = (latestMessageFirst ? messages.Reverse() : messages).Take(3);

      foreach (var msg in oldestFewMessages)
      {
         // timestamp as text
         WriteIdentityField(writer, msg.Time.ToString("o", CultureInfo.InvariantCulture));

         // author as text (null/empty handled)
         WriteIdentityField(writer, msg.Author);

         // message as text (null/empty handled)
         WriteIdentityField(writer, msg.Message);
      }

      writer.Flush();
      crypto.FlushFinalBlock();

      return HexEncoder.ToHexString(sha.Hash!);
   }

   public static bool IsValidHash(string hash)
   {
      return (hash != null) && (hash.Length == 64) && !hash.Any(c => !IsValidDigit(c));
   }

   private static bool IsValidDigit(char c)
   {
      return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
   }
}
