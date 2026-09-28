using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FocusTone.Audio;
using FocusTone.Native;
using FocusTone.Services;
using FocusTone.Views;
namespace FocusTone;
public sealed class MainWindow : Window
{
    public Storage Storage { get; } = new();
    public AudioEngine Audio { get; } = new();
    public AudioLibrary Library { get; }
    public RuleEngine Rules { get; }
    private BackgroundHotkeyManager? hotkeys;
    private readonly TrayService tray;
    private readonly ContentControl content = new();
    private readonly TextBlock status = Ui.Text("就绪 · 导入音频，创建第一条规则", 13);
    private readonly TextBlock indicator = Ui.Text("● 快捷键监听中", 12, "#71DBB3");
    private Button? stopButton;
    private readonly List<Button> navigation = [];
    private bool exiting;
    public event Action<string>? RawKey;
    public MainWindow()
    {
        Style = (Style)System.Windows.Application.Current.FindResource(typeof(Window));
        Title = "FocusTone · 前台快捷键音频"; Width = 1220; Height = 820; MinWidth = 1000; MinHeight = 700; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Library = new AudioLibrary(Storage); Rules = new RuleEngine(Storage, Audio);
        Audio.Volume = Storage.Data.Settings.Volume; Audio.DeviceId = Storage.Data.Settings.OutputDeviceId;
        Rules.Status += SetStatus; Audio.Failed += text => Dispatcher.BeginInvoke(() => SetStatus(text));
        Audio.PlaybackChanged += () => Dispatcher.BeginInvoke(() => UpdatePlaybackButton());
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) }); root.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar = new DockPanel { Margin = new Thickness(14, 24, 14, 18) };
        var footer = new StackPanel(); footer.Children.Add(indicator); footer.Children.Add(Ui.Text("FocusTone  ·  v1.0", 11, "#A8A8A8")); DockPanel.SetDock(footer, Dock.Bottom); sidebar.Children.Add(footer);
        var nav = new StackPanel(); nav.Children.Add(Ui.Text("FocusTone", 23, "#F5F5F5")); nav.Children.Add(Ui.Text("应用专属声音快捷键", 11, "#A8A8A8"));
        nav.Children.Add(new Border { Height = 28 });
        var homeNav = Ui.NavButton("\uE80F", "工作台", ShowHome);
        var libraryNav = Ui.NavButton("\uE8D6", "音频库与剪辑", () => ShowLibrary());
        var rulesNav = Ui.NavButton("\uE8B7", "快捷键规则", ShowRules);
        var biliNav = Ui.NavButton("\uE714", "Bilibili", () => Navigate(new BilibiliView(this)));
        var settingsNav = Ui.NavButton("\uE713", "设置", () => Navigate(new SettingsView(this)));
        navigation.AddRange([homeNav, libraryNav, rulesNav, biliNav, settingsNav]);
        foreach (var button in navigation) nav.Children.Add(button);
        sidebar.Children.Add(nav);
        var side = new Border { Background = Ui.Brush("SurfaceBrush"), Child = sidebar }; root.Children.Add(side);
        var body = new DockPanel { Margin = new Thickness(28, 26, 28, 18) }; Grid.SetColumn(body, 1); root.Children.Add(body);
        var bottom = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        stopButton = Ui.Button("○ 当前未播放", () =>
        {
            if (!Audio.IsPlaying && !Audio.IsPaused) { SetStatus("当前没有正在播放的音频。"); return; }
            stopButton!.Content = "正在停止…";
            stopButton.IsEnabled = false;
            Dispatcher.BeginInvoke(() =>
            {
                Audio.Stop();
                SetStatus("已停止播放");
                stopButton.IsEnabled = true;
                UpdatePlaybackButton();
            }, DispatcherPriority.Background);
        });
        DockPanel.SetDock(stopButton, Dock.Right); bottom.Children.Add(stopButton); bottom.Children.Add(status); DockPanel.SetDock(bottom, Dock.Bottom); body.Children.Add(bottom); body.Children.Add(content); Content = root;
        tray = new TrayService(this);
        SourceInitialized += (_, _) =>
        {
            FluentWindow.Apply(new WindowInteropHelper(this).Handle);
            try { hotkeys = new BackgroundHotkeyManager(); hotkeys.Pressed += key => Dispatcher.BeginInvoke(() => { RawKey?.Invoke(key); Rules.Trigger(key); }); }
            catch (Exception ex) { Log.Error("Raw Input 注册", ex); Storage.Data.Settings.HotkeysEnabled = false; SetStatus("按键监听注册失败，请重启软件。音频库仍可使用。"); }
            UpdateIndicator();
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && Storage.Data.Settings.MinimizeToTray) Hide(); };
        Closing += OnClosing; ShowHome();
        if (Storage.RecoveryWarning != null) Loaded += (_, _) => SetStatus(Storage.RecoveryWarning);
    }
    public void SetStatus(string text) { status.Text = text; status.ToolTip = text; }
    private void UpdatePlaybackButton()
    {
        if (stopButton == null) return;
        var active = Audio.IsPlaying || Audio.IsPaused;
        stopButton.Content = Audio.IsPaused ? "Ⅱ 已暂停 · 点击停止" : active ? "● 正在播放 · 点击停止" : "○ 当前未播放";
        stopButton.Background = active ? Ui.Brush("AccentBrush") : Ui.Brush("ControlBrush");
        stopButton.Foreground = active ? Brushes.Black : Ui.Brush("TextBrush");
    }
    public void UpdateIndicator() { indicator.Text = Storage.Data.Settings.HotkeysEnabled ? "● 快捷键监听中" : "○ 快捷键已暂停"; tray.Refresh(); }
    public void Navigate(UIElement page)
    {
        if (content.Content is IDisposable previous) previous.Dispose(); content.Content = page;
        var index = page is LibraryView ? 1 : page is RulesView ? 2 : page is BilibiliView ? 3 : page is SettingsView ? 4 : 0;
        for (var i = 0; i < navigation.Count; i++) navigation[i].Tag = i == index ? "Active" : null;
    }
    public void ShowLibrary(Models.AudioItem? selected = null) => Navigate(new LibraryView(this, selected));
    public void ShowRules() => Navigate(new RulesView(this));
    public void ShowHome()
    {
        var page = new StackPanel(); page.Children.Add(Ui.Heading("工作台", "只在目标应用处于前台时播放指定音频片段。"));
        var hero = new StackPanel(); hero.Children.Add(Ui.Text("快捷开始", 13, "#60CDFF")); hero.Children.Add(Ui.Text("选择应用，按键播放。", 28)); hero.Children.Add(Ui.Text("导入本地音频，在波形中圈出片段，然后绑定快捷键。", 14, "#A8A8A8")); hero.Children.Add(Ui.Row(Ui.Button("导入音频", () => ShowLibrary(), true), Ui.Button("创建规则", ShowRules))); page.Children.Add(Ui.Card(hero));
        var stats = Ui.Row(Ui.Text($"{Storage.Data.Library.Count}  段音频", 22), new Border { Width = 40 }, Ui.Text($"{Storage.Data.Rules.Count(r => r.Enabled)}  条启用规则", 22)); page.Children.Add(Ui.Card(stats));
        var steps = new StackPanel(); steps.Children.Add(Ui.Text("使用流程", 18)); steps.Children.Add(Ui.Text("1   导入 MP3 / WAV / FLAC / M4A / OGG\n\n2   选择片段并试听\n\n3   选择应用并录入快捷键", 15, "#C8C8C8")); page.Children.Add(Ui.Card(steps)); Navigate(new ScrollViewer { Content = page });
    }
    public void Restore() { Show(); WindowState = WindowState.Normal; Activate(); }
    public void Exit() { exiting = true; Close(); }
    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (!exiting && Storage.Data.Settings.MinimizeToTray) { args.Cancel = true; Hide(); return; }
        if (content.Content is IDisposable disposable) disposable.Dispose();
        hotkeys?.Dispose(); tray.Dispose(); Audio.Dispose();
        try { Storage.Save(); } catch (Exception ex) { Log.Error("退出保存", ex); }
        System.Windows.Application.Current.Shutdown();
    }
}
