namespace Brx.CopilotArchiveSync.Utils;

using System;
using System.IO;
using System.Runtime.InteropServices;

internal static class NativeFile
{
   public static void AtomicReplace(string source, string dest)
   {
      bool ok = NativeMethods.MoveFileEx(
          source,
          dest,
          NativeMethods.MoveFileFlags.MOVEFILE_REPLACE_EXISTING |
          NativeMethods.MoveFileFlags.MOVEFILE_WRITE_THROUGH
      );

      if (!ok)
      {
         throw new IOException($"MoveFileEx failed: {source} -> {dest}, error=0x{Marshal.GetLastWin32Error():X8}");
      }
   }
}
