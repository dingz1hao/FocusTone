using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace FocusTone.Native;
public sealed class HotkeyManager : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct RawDevice { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(IntPtr raw, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private readonly HwndSource source;
    private readonly KeyEdges down = new();
    private readonly IntPtr buffer = Marshal.AllocHGlobal(256);
    public event Action<string>? Pressed;
    public HotkeyManager(IntPtr hwnd)
    {
        source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("窗口尚未创建");
        var devices = new[] { new RawDevice { Page = 1, Usage = 6, Flags = 0x100, Target = hwnd }, new RawDevice { Page = 1, Usage = 2, Flags = 0x100, Target = hwnd } };
        if (!RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf<RawDevice>())) { Marshal.FreeHGlobal(buffer); throw new Win32Exception(); }
        source.AddHook(WndProc);
    }
    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x00FF) return IntPtr.Zero;
        uint size = 256; var header = (uint)(IntPtr.Size == 8 ? 24 : 16);
        var count = GetRawInputData(lParam, 0x10000003, buffer, ref size, header);
        if (count == uint.MaxValue || count < header + 16) return IntPtr.Zero;
        var type = Marshal.ReadInt32(buffer); var offset = (int)header;
        if (type == 1)
        {
            var key = (ushort)Marshal.ReadInt16(buffer, offset + 6); var flags = (ushort)Marshal.ReadInt16(buffer, offset + 2);
            if (key != 255) Transition(key, (flags & 1) == 0);
        }
        else if (type == 0)
        {
            var flags = (ushort)Marshal.ReadInt16(buffer, offset + 4);
            if ((flags & 0x40) != 0) Transition(0x10005, true);
            if ((flags & 0x80) != 0) Transition(0x10005, false);
            if ((flags & 0x100) != 0) Transition(0x10006, true);
            if ((flags & 0x200) != 0) Transition(0x10006, false);
        }
        return IntPtr.Zero;
    }
    private void Transition(int key, bool pressed)
    {
        if (!down.Update(key, pressed) || key is 16 or 17 or 18 or 160 or 161 or 162 or 163 or 164 or 165 or 91 or 92) return;
        bool Held(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
        if (Held(91) || Held(92)) return;
        var name = key == 0x10005 ? "Mouse4" : key == 0x10006 ? "Mouse5" : KeyInterop.KeyFromVirtualKey(key).ToString();
        Pressed?.Invoke((Held(17) ? "Ctrl+" : "") + (Held(16) ? "Shift+" : "") + (Held(18) ? "Alt+" : "") + name);
    }
    public static string Normalize(string text)
    {
        var parts = text.Trim().Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new ArgumentException("请输入快捷键，例如 Z 或 Ctrl+Shift+F1。");
        var modifiers = parts.SkipLast(1).Select(x => x.ToLowerInvariant()).ToHashSet();
        if (modifiers.Any(x => x is not ("ctrl" or "shift" or "alt")) || modifiers.Count != parts.Length - 1) throw new ArgumentException("修饰键支持 Ctrl、Shift、Alt。");
        var name = parts[^1];
        if (name.Equals("Mouse4", StringComparison.OrdinalIgnoreCase)) name = "Mouse4";
        else if (name.Equals("Mouse5", StringComparison.OrdinalIgnoreCase)) name = "Mouse5";
        else
        {
            if (name.Length == 1 && char.IsAsciiDigit(name[0])) name = "D" + name;
            if (!Enum.TryParse<Key>(name, true, out var key) || key is Key.None or Key.System or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin || KeyInterop.VirtualKeyFromKey(key) == 0) throw new ArgumentException("无法识别按键。示例：Z、F1、Ctrl+Z、Mouse4。");
            name = key.ToString();
        }
        return (modifiers.Contains("ctrl") ? "Ctrl+" : "") + (modifiers.Contains("shift") ? "Shift+" : "") + (modifiers.Contains("alt") ? "Alt+" : "") + name;
    }
    public void Dispose()
    {
        source.RemoveHook(WndProc);
        RegisterRawInputDevices([new() { Page = 1, Usage = 6, Flags = 1 }, new() { Page = 1, Usage = 2, Flags = 1 }], 2, (uint)Marshal.SizeOf<RawDevice>());
        Marshal.FreeHGlobal(buffer); down.Clear();
    }
}
