namespace Brx.CopilotArchiveSync.Utils;

using System;
using System.IO;

public static class LongPath
{
   private const string PrefixLocal = @"\\?\";
   private const string PrefixUnc = @"\\?\UNC\";

   public static string Decorate(string path)
   {
      if (string.IsNullOrWhiteSpace(path))
         throw new ArgumentException("Path is null or empty.", nameof(path));

      // Already extended -> return as-is
      if (path.StartsWith(PrefixLocal, StringComparison.OrdinalIgnoreCase))
         return path;

      // Extract ADS suffix (if any)
      string ads = null;
      int adsIndex = FindAdsSeparator(path);

      if (adsIndex > 0)
      {
         ads = path.Substring(adsIndex);       // includes ':'
         path = path.Substring(0, adsIndex);   // base path only
      }

      // Normalize base path using .NET (handles relative paths)
      string full = Path.GetFullPath(path);

      // Convert normalized base path to extended syntax
      string extended;

      if (full.StartsWith(@"\\"))
      {
         // UNC -> \\?\UNC\server\share\rest
         extended = PrefixUnc + full.Substring(2);
      }
      else
      {
         // Local -> \\?\C:\path
         extended = PrefixLocal + full;
      }

      // Re-attach ADS suffix exactly as-is
      if (ads != null)
         extended += ads;

      return extended;
   }

   // Finds the ADS separator ':' that is NOT the drive-letter colon.
   private static int FindAdsSeparator(string path)
   {
      // Detect valid drive letter colon (ASCII alpha only)
      bool hasDriveLetter =
          path.Length >= 2 &&
          path[1] == ':' &&
          ((path[0] >= 'A' && path[0] <= 'Z') ||
           (path[0] >= 'a' && path[0] <= 'z'));

      int driveColonIndex = hasDriveLetter ? 1 : -1;

      // Find last occurrences of separators
      int lastSlash = path.LastIndexOf('/');
      int lastBack = path.LastIndexOf('\\');
      int lastDot = path.LastIndexOf('.');

      int lastPathSep = Math.Max(lastSlash, lastBack);

      // Find last colon
      int lastColon = path.LastIndexOf(':');

      // No colon -> no ADS
      if (lastColon < 0)
         return -1;

      // Drive letter colon -> ignore
      if (lastColon == driveColonIndex)
         return -1;

      // ADS colon must be after the last path separator
      if (lastColon < lastPathSep)
         return -1;

      // ADS colon must be after the last dot (file extension)
      if (lastColon < lastDot)
         return -1;

      return lastColon;
   }
}
