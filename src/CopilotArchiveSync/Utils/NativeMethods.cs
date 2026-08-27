namespace Brx.CopilotArchiveSync.Utils;

using System;
using System.Runtime.InteropServices;
using System.Security;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
   [Flags]
   internal enum MoveFileFlags : uint
   {
      MOVEFILE_REPLACE_EXISTING = 0x1,
      MOVEFILE_COPY_ALLOWED = 0x2,
      MOVEFILE_WRITE_THROUGH = 0x8
   }

   internal const uint KF_FLAG_DEFAULT = 0x00000000;

   private const string Shell32_Dll = "shell32.dll";
   private const string Kernel32_Dll = "kernel32.dll";

   [DllImport(Shell32_Dll, CharSet = CharSet.Unicode, ExactSpelling = true)]
   internal static extern int SHGetKnownFolderPath(
      [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
      uint dwFlags,
      IntPtr hToken,
      out IntPtr ppszPath
   );

   [DllImport(Kernel32_Dll, SetLastError = true, CharSet = CharSet.Unicode)]
   internal static extern bool MoveFileEx(
      string lpExistingFileName,
      string lpNewFileName,
      MoveFileFlags dwFlags
   );
}
