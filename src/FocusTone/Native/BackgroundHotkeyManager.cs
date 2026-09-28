using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace FocusTone.Native;

/// <summary>Own message loop keeps high polling-rate mouse input off WPF's UI thread.</summary>
public sealed class BackgroundHotkeyManager : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct RawDevice { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(IntPtr raw, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    private sealed class InputWindow(BackgroundHotkeyManager owner) : System.Windows.Forms.NativeWindow
    {
        public void Open() => CreateHandle(new System.Windows.Forms.CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE
        public void Close() => DestroyHandle();
        protected override void WndProc(ref System.Windows.Forms.Message message)
        {
            if (message.Msg == 0x00FF)
            {
                try { owner.ReadInput(message.LParam); }
                catch (Exception ex) { Services.Log.Error("处理原始输入", ex); }
            }
            base.WndProc(ref message);
        }
    }

    private readonly Thread worker;
    private readonly ManualResetEventSlim ready = new(false);
    private readonly KeyEdges down = new();
    private IntPtr buffer;
    private uint workerThreadId;
    private Exception? startupError;
    private bool disposed;
    public event Action<string>? Pressed;

    public BackgroundHotkeyManager()
    {
        worker = new Thread(Run) { Name = "FocusTone Raw Input", IsBackground = true };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        ready.Wait();
        if (startupError != null) throw new InvalidOperationException("原始输入监听未能启动。", startupError);
    }

    private void Run()
    {
        InputWindow? window = null;
        var registered = false;
        try
        {
            workerThreadId = GetCurrentThreadId();
            buffer = Marshal.AllocHGlobal(256);
            window = new InputWindow(this);
            window.Open();
            var devices = new[]
            {
                new RawDevice { Page = 1, Usage = 6, Flags = 0x100, Target = window.Handle },
                new RawDevice { Page = 1, Usage = 2, Flags = 0x100, Target = window.Handle }
            };
            if (!RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf<RawDevice>())) throw new Win32Exception();
            registered = true;
            ready.Set();
            System.Windows.Forms.Application.Run();
        }
        catch (Exception ex)
        {
            startupError = ex;
            ready.Set();
            if (!disposed) Services.Log.Error("原始输入线程", ex);
        }
        finally
        {
            if (registered)
                RegisterRawInputDevices([new() { Page = 1, Usage = 6, Flags = 1 }, new() { Page = 1, Usage = 2, Flags = 1 }], 2, (uint)Marshal.SizeOf<RawDevice>());
            window?.Close();
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            down.Clear();
        }
    }

    private void ReadInput(IntPtr raw)
    {
        uint size = 256;
        var header = (uint)(IntPtr.Size == 8 ? 24 : 16);
        var count = GetRawInputData(raw, 0x10000003, buffer, ref size, header);
        if (count == uint.MaxValue || count < header + 16) return;
        var type = Marshal.ReadInt32(buffer);
        var offset = (int)header;
        if (type == 1)
        {
            var key = (ushort)Marshal.ReadInt16(buffer, offset + 6);
            var flags = (ushort)Marshal.ReadInt16(buffer, offset + 2);
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
    }

    private void Transition(int key, bool pressed)
    {
        if (!down.Update(key, pressed) || key is 16 or 17 or 18 or 160 or 161 or 162 or 163 or 164 or 165 or 91 or 92) return;
        bool Held(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
        if (Held(91) || Held(92)) return;
        var name = key == 0x10005 ? "Mouse4" : key == 0x10006 ? "Mouse5" : KeyInterop.KeyFromVirtualKey(key).ToString();
        var hotkey = (Held(17) ? "Ctrl+" : "") + (Held(16) ? "Shift+" : "") + (Held(18) ? "Alt+" : "") + name;
        Pressed?.Invoke(hotkey);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (workerThreadId != 0) PostThreadMessage(workerThreadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
        if (Thread.CurrentThread != worker) worker.Join(TimeSpan.FromSeconds(2));
        ready.Dispose();
    }
}
