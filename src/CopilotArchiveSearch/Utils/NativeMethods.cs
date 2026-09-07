namespace Brx.CopilotArchiveSearch.Utils;

using System;
using System.Runtime.InteropServices;
using System.Security;

[SuppressUnmanagedCodeSecurity]
internal class NativeMethods
{
   internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
   internal const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

   private const string DwmApi_Dll = "dwmapi.dll";

   [DllImport(DwmApi_Dll, PreserveSig = true)]
   internal static extern int DwmSetWindowAttribute(
      IntPtr hwnd,
      int attr,
      ref int attrValue,
      int attrSize
   );
}
