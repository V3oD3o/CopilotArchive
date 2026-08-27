namespace Brx.CopilotArchiveSync.Metadata;

using System.IO;
using System.Text;

using Brx.CopilotArchiveSync.Utils;


internal static class HashMetadataHelper
{
   private const string IdentityStreamSuffix = ":identity_hash";
   private const string ContentStreamSuffix = ":content_hash";

   public static void WriteIdentityHash(string filePath, string hash)
   {
      WriteHash(filePath, IdentityStreamSuffix, hash);
   }

   public static void WriteContentHash(string filePath, string hash)
   {
      WriteHash(filePath, ContentStreamSuffix, hash);
   }

   private static void WriteHash(string filePath, string streamName, string hash)
   {
      string fullPath = Path.GetFullPath(filePath);
      EnsureFileExists(fullPath);
      string longPath = LongPath.Decorate(fullPath);


      using var fs = new FileStream(longPath + streamName, FileMode.Create, FileAccess.Write, FileShare.None);
      using var writer = new StreamWriter(fs, new UTF8Encoding(false));

      writer.Write(hash);
   }

   public static string ReadIdentityHash(string filePath)
   {
      return ReadStreamOrNull(filePath + IdentityStreamSuffix);
   }

   public static string ReadContentHash(string filePath)
   {
      return ReadStreamOrNull(filePath + ContentStreamSuffix);
   }

   private static string ReadStreamOrNull(string streamPath)
   {
      string longPath = LongPath.Decorate(streamPath);
      if (!File.Exists(longPath))
         return null;

      using var fs = new FileStream(longPath, FileMode.Open, FileAccess.Read, FileShare.Read);
      using var reader = new StreamReader(fs, new UTF8Encoding(false));

      return reader.ReadToEnd();
   }

   private static void EnsureFileExists(string filePath)
   {
      if (!File.Exists(filePath))
         throw new IOException("Base file does not exist: " + filePath);
   }
}