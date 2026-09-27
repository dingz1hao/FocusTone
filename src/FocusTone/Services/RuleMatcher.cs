using FocusTone.Models;

namespace FocusTone.Services;
public static class RuleMatcher
{
    public static AudioRule? Find(IEnumerable<AudioRule> rules, string key, string? foregroundPath) =>
        string.IsNullOrEmpty(foregroundPath) ? null : rules.FirstOrDefault(r =>
            r.Enabled && r.Hotkey == key && string.Equals(r.ProcessPath, foregroundPath, StringComparison.OrdinalIgnoreCase));
}
