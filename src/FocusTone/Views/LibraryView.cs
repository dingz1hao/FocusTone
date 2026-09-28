using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FocusTone.Audio;
using FocusTone.Controls;
using FocusTone.Models;
namespace FocusTone.Views;
public sealed class LibraryView : UserControl, IDisposable
{
    private readonly MainWindow host;
    private readonly ListBox list = new();
    private readonly TextBox search = new() { ToolTip = "搜索音频标题" };
    private readonly TextBox title = new();
    private readonly TextBox start = new() { Width = 125 }, end = new() { Width = 125 };
    private readonly WaveformControl waveform = new();
    private readonly TextBlock clock = Ui.Text("00:00.000 / 00:00.000", 14, "#60CDFF");
    private readonly TextBlock detail = Ui.Text("选择音频后即可剪辑", 13, "#A8A8A8");
    private readonly CheckBox loop = new() { Content = "循环选中片段" };
    private readonly Button playback = new();
    private readonly StackPanel editor = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private CancellationTokenSource cancellation = new();
    private AudioItem? selected;
    private bool disposed;
    public LibraryView(MainWindow host, AudioItem? initial)
    {
        this.host = host;
        var root = new DockPanel(); var header = Ui.Heading("音频库 / 片段工作台", "保留原始音频，只保存你选中的时间范围。每条规则还可以拥有独立片段。"); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(255) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); root.Children.Add(grid);
        var left = new DockPanel(); var top = new StackPanel(); top.Children.Add(Ui.AsyncButton("＋ 导入本地音频", Import, host.SetStatus, true)); top.Children.Add(Ui.Text("搜索音频", 12, "#A8A8A8")); top.Children.Add(search); DockPanel.SetDock(top, Dock.Top); left.Children.Add(top); left.Children.Add(list); grid.Children.Add(left);
        editor.Children.Add(Ui.Text("音频标题", 12, "#A8A8A8")); editor.Children.Add(title); editor.Children.Add(detail); waveform.Height = 200; editor.Children.Add(waveform); editor.Children.Add(new Border { Height = 8 }); editor.Children.Add(clock);
        playback = Ui.AsyncButton("▶ 播放", PlayPause, host.SetStatus, true);
        editor.Children.Add(Ui.Row(playback, Ui.AsyncButton("试听选中片段", Preview, host.SetStatus), Ui.Button("■ 停止", () => host.Audio.Stop()))); editor.Children.Add(loop);
        editor.Children.Add(Ui.Text("片段时间  ·  mm:ss.fff 或秒数", 12, "#A8A8A8")); editor.Children.Add(Ui.Row(start, Ui.Text("→", 20), end));
        editor.Children.Add(Ui.Row(Ui.Button("起点 = 指针", () => { waveform.Start = Math.Min(waveform.Position, waveform.End - .001); RangeChanged(); }), Ui.Button("终点 = 指针", () => { waveform.End = Math.Max(waveform.Position, waveform.Start + .001); RangeChanged(); })));
        editor.Children.Add(Ui.Row(Ui.Button("保存标题与片段", Save, true), Ui.Button("绑定快捷键", Bind), Ui.Button("删除音频", Delete)));
        editor.Children.Add(Ui.Text("拖动蓝色边界调整起止时间，点击波形定位。保存片段不会重新编码，也不会覆盖已有规则的独立片段。", 12, "#A8A8A8"));
        var right = new ScrollViewer { Content = Ui.Card(editor) }; Grid.SetColumn(right, 2); grid.Children.Add(right); Content = root;
        editor.IsEnabled = false;
        search.TextChanged += (_, _) => Refresh(); list.SelectionChanged += async (_, _) => await Select((list.SelectedItem as ListBoxItem)?.Tag as AudioItem);
        waveform.RangeChanged += RangeChanged;
        waveform.SeekRequested += seconds => { host.Audio.Seek(seconds); UpdateClock(); };
        timer.Tick += (_, _) => { if (host.Audio.CurrentId == selected?.Id && host.Audio.IsPlaying) { waveform.Position = host.Audio.Position; waveform.InvalidateVisual(); UpdateClock(); } else timer.Stop(); };
        host.Audio.PlaybackChanged += PlaybackChanged;
        IsVisibleChanged += (_, _) => UpdatePlayback();
        Refresh(initial); UpdatePlayback();
    }
    private void Refresh(AudioItem? pick = null)
    {
        pick ??= selected; list.Items.Clear();
        foreach (var item in host.Storage.Data.Library.Where(x => x.Title.Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
        {
            var row = new DockPanel(); var cover = new Image { Width = 40, Height = 40, Margin = new Thickness(0, 0, 10, 0), Stretch = Stretch.UniformToFill };
            if (item.CoverFile != null)
            {
                try { var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(Services.Storage.SafeFile(host.Storage.LibraryDirectory, item.CoverFile)); image.EndInit(); image.Freeze(); cover.Source = image; } catch { }
            }
            DockPanel.SetDock(cover, Dock.Left); row.Children.Add(cover); var labels = new StackPanel(); labels.Children.Add(Ui.Text(item.Title, 14)); labels.Children.Add(Ui.Text(item.DurationText + " · " + item.Source, 11, "#A8A8A8")); row.Children.Add(labels);
            var entry = new ListBoxItem { Content = row, Tag = item }; list.Items.Add(entry); if (item.Id == pick?.Id) list.SelectedItem = entry;
        }
        if (host.Storage.Data.Library.Count == 0) host.SetStatus("音频库为空，点击“导入本地音频”开始。");
    }
    private async Task Select(AudioItem? item)
    {
        cancellation.Cancel(); cancellation.Dispose(); cancellation = new CancellationTokenSource(); var token = cancellation.Token;
        selected = item; editor.IsEnabled = item != null;
        UpdatePlayback();
        if (item == null) return;
        title.Text = item.Title; detail.Text = item.Source + " · " + item.DurationText;
        waveform.Duration = item.Duration; waveform.Start = item.Start; waveform.End = item.End; waveform.Position = 0; waveform.Peaks = []; RangeChanged(); UpdateClock();
        host.SetStatus("正在后台解析波形…");
        try
        {
            var peaks = await WaveformService.LoadAsync(host.Storage.AudioPath(item), Path.Combine(host.Storage.CacheDirectory, item.Id + ".wave"), token);
            if (token.IsCancellationRequested || disposed) return; waveform.Peaks = peaks; waveform.InvalidateVisual(); host.SetStatus("波形已就绪 · 拖动边界选择片段");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Services.Log.Error("解析波形", ex); host.SetStatus("音频无法读取，请检查文件或编码格式。"); }
    }
    private void UpdateClock() => clock.Text = $"{TimeText.Format(waveform.Position)} / {TimeText.Format(waveform.Duration)}";
    private void PlaybackChanged() => Dispatcher.BeginInvoke(() => { if (!disposed) UpdatePlayback(); });
    private void UpdatePlayback()
    {
        var own = selected != null && host.Audio.CurrentId == selected.Id;
        playback.Content = own && host.Audio.IsPlaying ? "Ⅱ 暂停" : own && host.Audio.IsPaused ? "▶ 继续" : "▶ 播放";
        if (IsVisible && own && host.Audio.IsPlaying) timer.Start(); else timer.Stop();
    }
    private void RangeChanged() { start.Text = TimeText.Format(waveform.Start); end.Text = TimeText.Format(waveform.End); waveform.InvalidateVisual(); }
    private void ReadRange()
    {
        if (selected == null) return;
        if (!TimeText.TryParse(start.Text, out var from) || !TimeText.TryParse(end.Text, out var to)) throw new ArgumentException("请输入 mm:ss.fff 格式，例如 01:12.500。");
        TimeText.ValidateRange(from, to, selected.Duration); waveform.Start = from; waveform.End = to; waveform.InvalidateVisual();
    }
    private async Task Import()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "音频文件|*.mp3;*.wav;*.flac;*.m4a;*.ogg", Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        AudioItem? imported = null; var errors = new List<string>();
        foreach (var path in dialog.FileNames)
        {
            host.SetStatus("正在导入 " + Path.GetFileName(path));
            try { imported = await host.Library.ImportAsync(path); } catch (Exception ex) { Services.Log.Error("导入音频", ex); errors.Add(Path.GetFileName(path)); }
        }
        if (disposed) return; Refresh(imported); if (errors.Count > 0) host.SetStatus("部分文件导入失败：" + string.Join("、", errors));
    }
    private async Task PlayPause()
    {
        if (selected == null) return;
        if (host.Audio.CurrentId == selected.Id && (host.Audio.IsPlaying || host.Audio.IsPaused)) { host.Audio.PauseResume(); return; }
        await host.Audio.PlayAsync(host.Storage.AudioPath(selected), 0, selected.Duration, selected.Id); host.Audio.Seek(waveform.Position >= selected.Duration ? 0 : waveform.Position);
    }
    private async Task Preview()
    {
        if (selected == null) return; ReadRange(); await host.Audio.PlayAsync(host.Storage.AudioPath(selected), waveform.Start, waveform.End, selected.Id, loop.IsChecked == true);
    }
    private void Save()
    {
        if (selected == null) return;
        try
        {
            ReadRange(); if (string.IsNullOrWhiteSpace(title.Text)) throw new ArgumentException("标题不能为空。"); selected.Title = title.Text.Trim(); selected.Start = waveform.Start; selected.End = waveform.End; host.Storage.Save(); Refresh(selected); host.SetStatus("已保存音频标题和默认片段。");
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }
    private void Bind()
    {
        if (selected == null) return;
        try { ReadRange(); var dialog = new RuleDialog(host, null, selected, waveform.Start, waveform.End) { Owner = host }; if (dialog.ShowDialog() == true) host.SetStatus("已创建规则。切换到目标程序后按快捷键即可触发。"); }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }
    private void Delete()
    {
        if (selected == null || MessageBox.Show("删除这段音频及引用它的规则？原始导入文件不受影响。", "删除音频", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { cancellation.Cancel(); host.Audio.Stop(); host.Library.Delete(selected); selected = null; Refresh(); editor.IsEnabled = false; host.SetStatus("已删除音频及相关规则。"); } catch (Exception ex) { Ui.Error(ex.Message); }
    }
    public void Dispose() { disposed = true; host.Audio.PlaybackChanged -= PlaybackChanged; timer.Stop(); cancellation.Cancel(); cancellation.Dispose(); }
}
