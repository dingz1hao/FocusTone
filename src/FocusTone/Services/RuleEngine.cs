using FocusTone.Audio;
using FocusTone.Native;
namespace FocusTone.Services;
public sealed class RuleEngine(Storage storage, AudioEngine audio)
{
    public bool Suspended { get; set; }
    public event Action<string>? Status;
    public async void Trigger(string hotkey)
    {
        if (Suspended || !storage.Data.Settings.HotkeysEnabled) return;
        var candidates = storage.Data.Rules.Where(r => r.Enabled && r.Hotkey == hotkey).ToList();
        if (candidates.Count == 0) return;
        var rule = RuleMatcher.Find(candidates, hotkey, ProcessMonitor.ForegroundPath());
        if (rule == null) return;
        var item = storage.Data.Library.FirstOrDefault(x => x.Id == rule.AudioId);
        if (item == null) { Status?.Invoke("规则引用的音频已不存在，请重新选择音频。"); return; }
        try { await audio.PlayAsync(storage.AudioPath(item), rule.Start, rule.End, rule.Id, behavior: rule.Behavior); Status?.Invoke($"{rule.ProcessName} · {hotkey} → {item.Title}"); }
        catch (Exception ex) { Log.Error("快捷键播放", ex); Status?.Invoke("播放失败：音频文件或输出设备不可用。"); }
    }
}
