namespace Brx.CopilotArchiveSync.Utils;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class PathLimits
{
   public const int MaxFileName = 255;
   public const int MaxPath = 260;
   public const int MaxPracticalFileName = 128;
}

public static class FileNameSanitizer
{
   private static readonly Dictionary<char, string> LookAlikeMap = new Dictionary<char, string>
   {
      { '/',  "\uFF0F" },
      { '\\', "\uFF3C" },
      { ':',  "\uFF1A" },
      { '?',  "\uFF1F" },
      { '*',  "\u2731" },
      { '"',  "\u201D" },
      { '<',  "\u2039" },
      { '>',  "\u203A" },
      { '|',  "\u00A6" }
   };

   private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
   {
     "CON", "PRN", "AUX", "NUL",
     "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
     "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
   };

   public static string SanitizeTitle(string title, int maxLength = PathLimits.MaxPracticalFileName)
   {
      if (string.IsNullOrWhiteSpace(title))
         return "untitled";

      title = title.Trim();
      char[] invalid = Path.GetInvalidFileNameChars();
      var sb = new StringBuilder(title.Length);

      foreach (char ch in title)
      {
         // Whitespace -> underscore
         if (char.IsWhiteSpace(ch))
         {
            sb.Append('_');
            continue;
         }

         // En-dash / em-dash -> hyphen
         if (ch == '\u2013' || ch == '\u2014')
         {
            sb.Append('-');
            continue;
         }

         // Look-alike replacements
         if (LookAlikeMap.TryGetValue(ch, out string repl))
         {
            sb.Append(repl);
            continue;
         }

         // Invalid filename char -> underscore
         if (Array.IndexOf(invalid, ch) >= 0)
         {
            sb.Append('_');
            continue;
         }

         sb.Append(ch);
      }

      string result = sb.ToString();

      // Replace "..." with Unicode ellipsis
      result = result.Replace("...", "\u2026");

      // Collapse any remaining ".." -> "."
      result = Regex.Replace(result, @"\.{2,}", ".");

      // Collapse "_-_" -> "-"
      result = Regex.Replace(result, "_-_", "-");

      // Collapse multiple underscores and hyphens
      result = Regex.Replace(result, "_{2,}", "_");
      result = Regex.Replace(result, "-{2,}", "-");

      // Trim leading/trailing separators
      result = result.Trim('_', '-');

      // Trim trailing dots/spaces
      result = result.TrimEnd('.', ' ');

      // Enforce max length with surrogate-safe truncation
      if (maxLength > 0 && result.Length > maxLength)
      {
         result = SafeTruncate(result, maxLength);

         // Cleanup after truncation
         result = result.Trim('_', '-');
         result = result.TrimEnd('.', ' ');
      }

      if (string.IsNullOrWhiteSpace(result))
         result = "untitled";

      // Handle reserved DOS device names
      int dotIndex = result.IndexOf('.');
      string nameOnly = dotIndex > 0 ? result.Substring(0, dotIndex) : result;

      if (ReservedNames.Contains(nameOnly))
         result = "_" + result;

      return result;
   }

   private static string SafeTruncate(string s, int maxLength)
   {
      if (s == null || s.Length <= maxLength)
         return s;

      string truncated = s.Substring(0, maxLength);

      // If last char is a high surrogate, drop it
      char last = truncated[truncated.Length - 1];
      if (char.IsHighSurrogate(last))
         truncated = truncated.Substring(0, truncated.Length - 1);

      return truncated;
   }
}
