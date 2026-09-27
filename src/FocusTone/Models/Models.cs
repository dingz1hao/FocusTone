using System.Text.Json.Serialization;
using System.Windows.Media;

namespace FocusTone.Models;

public enum TriggerBehavior { Restart, IgnoreWhilePlaying, ReplaceCurrent }
public sealed class AudioItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Source { get; set; } = "本地文件";
    public string? CoverFile { get; set; }
    public double Duration { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    [JsonIgnore] public string Range => $"{TimeText.Format(Start)} → {TimeText.Format(End)}";
    [JsonIgnore] public string DurationText => TimeText.Format(Duration);
    public override string ToString() => Title;
}
public sealed class AudioRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProcessPath { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string Hotkey { get; set; } = "Z";
    public Guid AudioId { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    public bool Enabled { get; set; } = true;
    public TriggerBehavior Behavior { get; set; }
    [JsonIgnore] public string Range => $"{TimeText.Format(Start)} → {TimeText.Format(End)}";
}
public sealed class Settings
{
    public bool HotkeysEnabled { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public float Volume { get; set; } = .7f;
    public string? OutputDeviceId { get; set; }
    public bool BilibiliEnabled { get; set; } = true;
}
public sealed class AppData
{
    public int SchemaVersion { get; set; } = 1;
    public Settings Settings { get; set; } = new();
    public List<AudioItem> Library { get; set; } = [];
    public List<AudioRule> Rules { get; set; } = [];
}
public sealed record ProcessItem(string Name, string ProcessName, string Path, ImageSource? Icon)
{
    public override string ToString() => $"{Name} · {ProcessName}";
}
public static class TimeText
{
    public static string Format(double seconds) => $"{(int)(Math.Max(0, seconds) / 60):00}:{Math.Max(0, seconds) % 60:00.000}";
    public static bool TryParse(string text, out double seconds)
    {
        seconds = 0;
        var parts = text.Trim().Split(':');
        if (parts.Length == 1) return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds) && double.IsFinite(seconds) && seconds >= 0;
        if (parts.Length != 2 || !int.TryParse(parts[0], out var minutes) || minutes < 0 || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rest) || !double.IsFinite(rest) || rest < 0 || rest >= 60) return false;
        seconds = minutes * 60 + rest; return true;
    }
    public static void ValidateRange(double start, double end, double duration)
    {
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start || end > duration + .001) throw new ArgumentException("片段范围无效：终点须大于起点，且不能超过音频时长。");
    }
}
