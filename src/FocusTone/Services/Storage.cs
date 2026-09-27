using System.IO;
using System.Text.Json;
using FocusTone.Models;

namespace FocusTone.Services;

public sealed class Storage
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public string Root { get; }
    public string LibraryDirectory => Path.Combine(Root, "library");
    public string CacheDirectory => Path.Combine(Root, "cache");
    public AppData Data { get; private set; } = new();
    public string? RecoveryWarning { get; private set; }
    public Storage(string? root = null)
    {
        Root = root ?? Environment.GetEnvironmentVariable("FOCUSTONE_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusTone");
        Directory.CreateDirectory(Root); Directory.CreateDirectory(LibraryDirectory); Directory.CreateDirectory(CacheDirectory);
        Log.DirectoryPath = Path.Combine(Root, "logs");
        var path = Path.Combine(Root, "settings.json");
        if (!File.Exists(path)) return;
        try { Data = Read(path); }
        catch (Exception ex)
        {
            Log.Error("读取配置", ex);
            File.Copy(path, path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", true);
            if (File.Exists(path + ".bak")) { Data = Read(path + ".bak"); RecoveryWarning = "配置损坏，已恢复上次备份。损坏文件已保留。"; }
            else throw new InvalidDataException("配置文件损坏，已保留副本。请从备份恢复 settings.json。", ex);
        }
    }
    private static AppData Read(string path)
    {
        var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path)) ?? throw new InvalidDataException("空配置");
        if (data.SchemaVersion != 1 || data.Library is null || data.Rules is null || data.Settings is null) throw new InvalidDataException("配置版本或结构不受支持");
        data.Settings.Volume = float.IsFinite(data.Settings.Volume) ? Math.Clamp(data.Settings.Volume, 0, 1) : .7f;
        return data;
    }
    public void Save()
    {
        var path = Path.Combine(Root, "settings.json"); var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Data, Json));
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
    }
    public string AudioPath(AudioItem item) => SafeFile(LibraryDirectory, item.FileName);
    public static string SafeFile(string directory, string file)
    {
        if (string.IsNullOrWhiteSpace(file) || Path.GetFileName(file) != file) throw new InvalidDataException("无效的媒体文件名");
        return Path.Combine(directory, file);
    }
}
public static class Log
{
    public static string DirectoryPath { get; set; } = Path.Combine(Path.GetTempPath(), "FocusTone-logs");
    private static readonly object Gate = new();
    public static void Error(string operation, Exception ex)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                // Never log URLs, cookies or exception messages containing remote query tokens.
                File.AppendAllText(Path.Combine(DirectoryPath, $"{DateTime.UtcNow:yyyy-MM-dd}.log"), $"{DateTime.UtcNow:O} {operation}: {ex.GetType().Name} HResult={ex.HResult:X}\n{ex.StackTrace}\n");
                foreach (var file in Directory.GetFiles(DirectoryPath, "*.log").OrderDescending().Skip(7)) File.Delete(file);
            }
        }
        catch { /* Logging must not take down the desktop app. */ }
    }
}
