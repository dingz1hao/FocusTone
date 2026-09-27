using System.Windows;
using System.Windows.Controls;
using FocusTone.Models;
using FocusTone.Native;
namespace FocusTone.Views;
public sealed class RuleDialog : Window
{
    private readonly MainWindow host;
    private readonly AudioRule? original;
    private readonly ComboBox process = new() { IsEditable = false };
    private readonly ComboBox audio = new() { DisplayMemberPath = "Title" };
    private readonly TextBox path = new() { IsReadOnly = true };
    private readonly TextBox hotkey = new() { Text = "Z" };
    private readonly TextBox from = new() { Width = 145 }, to = new() { Width = 145 };
    private readonly ComboBox behavior = new();
    private readonly TextBlock state = Ui.Text("加载正在运行的程序…", 12, "#B4A2FF");
    private readonly Button record;
    private bool recording;
    private List<ProcessItem> processes = [];
    private readonly TextBox processSearch = new() { ToolTip = "搜索应用名称或进程名称" };
    public RuleDialog(MainWindow host, AudioRule? rule = null, AudioItem? selected = null, double? start = null, double? end = null)
    {
        Style = (Style)System.Windows.Application.Current.FindResource(typeof(Window));
        this.host = host; original = rule; Title = rule == null ? "创建快捷键规则" : "编辑快捷键规则"; Width = 650; Height = 730; MinHeight = 650; ResizeMode = ResizeMode.CanResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(28) }; panel.Children.Add(Ui.Heading(Title, "只在目标 EXE 对应窗口位于前台时触发。"));
        panel.Children.Add(Ui.Text("目标应用 · 输入名称筛选", 12)); panel.Children.Add(processSearch); panel.Children.Add(process); panel.Children.Add(path);
        processSearch.TextChanged += (_, _) => { process.ItemsSource = processes.Where(x => x.Name.Contains(processSearch.Text, StringComparison.OrdinalIgnoreCase) || x.ProcessName.Contains(processSearch.Text, StringComparison.OrdinalIgnoreCase)).ToList(); };
        panel.Children.Add(Ui.Row(Ui.AsyncButton("刷新进程", LoadProcesses, text => state.Text = text), Ui.Button("选择 EXE 文件", Browse)));
        panel.Children.Add(Ui.Text("快捷键（支持手动输入或录入）", 12)); record = Ui.Button("录入下一次按键", () => { recording = !recording; record!.Content = recording ? "等待按键…再次点击取消" : "录入下一次按键"; });
        panel.Children.Add(Ui.Row(hotkey, record)); hotkey.MinWidth = 240;
        panel.Children.Add(Ui.Text("音频", 12)); panel.Children.Add(audio); panel.Children.Add(Ui.Text("此规则独立片段 · mm:ss.fff", 12)); panel.Children.Add(Ui.Row(from, Ui.Text("→", 20), to));
        behavior.ItemsSource = new[] { "重新播放同一规则（其他规则播放中则忽略）", "有音频播放 / 暂停时忽略", "停止当前音频并播放新音频" }; behavior.SelectedIndex = 2; panel.Children.Add(behavior);
        panel.Children.Add(state); panel.Children.Add(Ui.Row(Ui.Button("保存规则", Save, true), Ui.Button("取消", () => Close()))); Content = new ScrollViewer { Content = panel };
        audio.ItemsSource = host.Storage.Data.Library; audio.SelectionChanged += (_, _) => { if (audio.SelectedItem is AudioItem item) { from.Text = TimeText.Format(item.Start); to.Text = TimeText.Format(item.End); } };
        audio.SelectedItem = selected ?? host.Storage.Data.Library.FirstOrDefault(x => x.Id == rule?.AudioId) ?? host.Storage.Data.Library.FirstOrDefault();
        if (rule != null) { hotkey.Text = rule.Hotkey; path.Text = rule.ProcessPath; from.Text = TimeText.Format(rule.Start); to.Text = TimeText.Format(rule.End); behavior.SelectedIndex = (int)rule.Behavior; }
        else if (start.HasValue && end.HasValue) { from.Text = TimeText.Format(start.Value); to.Text = TimeText.Format(end.Value); }
        process.SelectionChanged += (_, _) => { if (process.SelectedItem is ProcessItem item) path.Text = item.Path; };
        Loaded += async (_, _) => { host.Rules.Suspended = true; host.RawKey += RecordKey; await LoadProcesses(); };
        Closed += (_, _) => { host.RawKey -= RecordKey; host.Rules.Suspended = false; };
    }
    private void RecordKey(string key) { if (!recording) return; hotkey.Text = key; recording = false; record.Content = "录入下一次按键"; }
    private async Task LoadProcesses()
    {
        try { var current = path.Text; var items = await ProcessMonitor.ListAsync(); processes = items; process.ItemsSource = items; process.SelectedItem = items.FirstOrDefault(x => x.Path.Equals(current, StringComparison.OrdinalIgnoreCase)); state.Text = $"{items.Count} 个可访问应用 · 受保护进程可能不提供路径"; }
        catch (Exception ex) { Services.Log.Error("进程列表", ex); state.Text = "读取进程失败，可手动选择 EXE。"; }
    }
    private void Browse() { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Windows 程序|*.exe" }; if (dialog.ShowDialog() == true) { process.SelectedItem = null; path.Text = dialog.FileName; } }
    private void Save()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path.Text) || !System.IO.Path.IsPathFullyQualified(path.Text)) throw new ArgumentException("请选择目标程序。");
            if (audio.SelectedItem is not AudioItem item) throw new ArgumentException("请先导入并选择音频。");
            var key = HotkeyManager.Normalize(hotkey.Text);
            if (host.Storage.Data.Rules.Any(r => r.Id != original?.Id && r.ProcessPath.Equals(path.Text, StringComparison.OrdinalIgnoreCase) && r.Hotkey == key)) throw new ArgumentException("此程序已存在相同快捷键，请编辑现有规则。");
            if (!TimeText.TryParse(from.Text, out var start) || !TimeText.TryParse(to.Text, out var end)) throw new ArgumentException("片段时间格式不正确。"); TimeText.ValidateRange(start, end, item.Duration);
            var saved = new AudioRule { Id = original?.Id ?? Guid.NewGuid(), ProcessPath = path.Text, ProcessName = System.IO.Path.GetFileName(path.Text), Hotkey = key, AudioId = item.Id, Start = start, End = end, Enabled = original?.Enabled ?? true, Behavior = (TriggerBehavior)behavior.SelectedIndex };
            var index = original == null ? -1 : host.Storage.Data.Rules.IndexOf(original);
            if (index >= 0) host.Storage.Data.Rules[index] = saved; else host.Storage.Data.Rules.Add(saved);
            try { host.Storage.Save(); } catch { if (index >= 0) host.Storage.Data.Rules[index] = original!; else host.Storage.Data.Rules.Remove(saved); throw; }
            DialogResult = true;
        }
        catch (Exception ex) { state.Text = ex.Message; }
    }
}
