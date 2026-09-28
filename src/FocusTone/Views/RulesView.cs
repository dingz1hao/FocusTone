using System.Windows;
using System.Windows.Controls;
using FocusTone.Native;
namespace FocusTone.Views;
public sealed class RulesView : UserControl
{
    private readonly MainWindow host;
    private readonly StackPanel items = new();
    public RulesView(MainWindow host)
    {
        this.host = host; var root = new DockPanel(); var header = Ui.Heading("快捷键规则", "绑定保存在 EXE 路径上，应用重启后仍然有效。按住按键只触发一次。"); header.Children.Add(Ui.Row(Ui.Button("＋ 创建规则", Create, true), Ui.AsyncButton("刷新运行状态", Refresh, host.SetStatus))); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header); root.Children.Add(new ScrollViewer { Content = items }); Content = root;
        items.Children.Add(Ui.Card(Ui.Text("正在检查目标程序…", 16)));
        Loaded += async (_, _) => await Refresh();
    }
    private void Create() { if (new RuleDialog(host) { Owner = host }.ShowDialog() == true) _ = Refresh(); }
    private async Task Refresh()
    {
        try
        {
            var running = await ProcessMonitor.RunningPathsAsync(); items.Children.Clear();
            if (host.Storage.Data.Rules.Count == 0) { items.Children.Add(Ui.Card(Ui.Text("还没有规则。先导入音频，再为目标应用绑定一个按键。", 18))); return; }
            foreach (var group in host.Storage.Data.Rules.GroupBy(x => x.ProcessPath))
            {
                var panel = new StackPanel();
                var image = new Image { Width = 32, Height = 32, Margin = new Thickness(0, 0, 12, 8) };
                var executable = group.Key;
                _ = Task.Run(() => AppIcons.ForExecutable(executable)).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully) Dispatcher.BeginInvoke(() => image.Source = task.Result);
                });
                panel.Children.Add(Ui.Row(image, Ui.Text(group.First().ProcessName + (running.Contains(group.Key) ? "   ● 运行中" : "   ○ 未运行"), 19)));
                panel.Children.Add(Ui.Text(group.Key, 11, "#A8A8A8"));
                foreach (var rule in group)
                {
                    var item = host.Storage.Data.Library.FirstOrDefault(x => x.Id == rule.AudioId);
                    var enabled = new CheckBox { IsChecked = rule.Enabled, Content = $"{rule.Hotkey}  →  {item?.Title ?? "音频丢失"}   [{rule.Range}]", FontSize = 15 };
                    enabled.Click += (_, _) => { rule.Enabled = enabled.IsChecked == true; try { host.Storage.Save(); } catch (Exception ex) { Ui.Error(ex.Message); } };
                    panel.Children.Add(enabled); panel.Children.Add(Ui.Row(Ui.Button("编辑", () => { if (new RuleDialog(host, rule) { Owner = host }.ShowDialog() == true) _ = Refresh(); }), Ui.AsyncButton("测试播放", async () => { if (item != null) await host.Audio.PlayAsync(host.Storage.AudioPath(item), rule.Start, rule.End, rule.Id); }, host.SetStatus), Ui.Button("删除", () =>
                    {
                        if (MessageBox.Show("删除这条规则？", "FocusTone", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                        host.Storage.Data.Rules.Remove(rule); try { host.Storage.Save(); _ = Refresh(); } catch (Exception ex) { host.Storage.Data.Rules.Add(rule); Ui.Error(ex.Message); }
                    })));
                }
                items.Children.Add(Ui.Card(panel));
            }
        }
        catch (Exception ex) { Services.Log.Error("刷新规则", ex); host.SetStatus("无法刷新规则列表，请重试。"); }
    }
}
