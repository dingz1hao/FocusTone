using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FocusTone.Models;

namespace FocusTone.Native;
public static class ProcessMonitor
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    public static string? ForegroundPath() { GetWindowThreadProcessId(GetForegroundWindow(), out var pid); return PathFor(pid); }
    public static string? PathFor(uint pid)
    {
        var handle = OpenProcess(0x1000, false, pid); // QUERY_LIMITED_INFORMATION only; never VM_READ.
        if (handle == IntPtr.Zero) return null;
        try { uint size = 32768; var buffer = new StringBuilder((int)size); return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null; }
        finally { CloseHandle(handle); }
    }
    public static Task<HashSet<string>> RunningPathsAsync() => Task.Run(() =>
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try { var path = PathFor((uint)process.Id); if (path != null) paths.Add(path); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
            }
        }
        return paths;
    });
    public static Task<List<ProcessItem>> ListAsync() => Task.Run(() =>
    {
        var result = new Dictionary<string, ProcessItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var path = PathFor((uint)process.Id);
                    if (path == null || result.ContainsKey(path)) continue;
                    var name = FileVersionInfo.GetVersionInfo(path).FileDescription;
                    var image = AppIcons.ForExecutable(path);
                    result[path] = new ProcessItem(string.IsNullOrWhiteSpace(name) ? process.ProcessName : name, System.IO.Path.GetFileName(path), path, image);
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or System.IO.IOException) { }
            }
        }
        return result.Values.OrderBy(x => x.Name).ToList();
    });
}
