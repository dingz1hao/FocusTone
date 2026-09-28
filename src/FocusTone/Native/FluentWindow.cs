using System.Runtime.InteropServices;

namespace FocusTone.Native;

internal static class FluentWindow
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public static void Apply(IntPtr handle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        Set(handle, 20, 1); // dark caption
        Set(handle, 33, 2); // rounded window corners
        Set(handle, 38, 2); // Mica backdrop in the non-client area
    }

    private static void Set(IntPtr handle, int attribute, int value)
    {
        try { DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)); }
        catch (Exception ex) { Services.Log.Error("设置 Windows 11 窗口外观", ex); }
    }
}
