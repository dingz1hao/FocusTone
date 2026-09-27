using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
    private HotkeyManager? hotkeys;
    private readonly TrayService tray;
    private readonly ContentControl content = new();
    private readonly TextBlock status = Ui.Text("就绪 · 导入音频，创建第一条规则", 13);
    private readonly TextBlock indicator = Ui.Text("● 快捷键监听中", 12, "#71DBB3");
    private bool exiting;
    public event Action<string>? RawKey;
    public MainWindow()
    {
        Style = (Style)System.Windows.Application.Current.FindResource(typeof(Window));
        Title = "FocusTone · 前台快捷键音频"; Width = 1220; Height = 820; MinWidth = 1000; MinHeight = 700; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Library = new AudioLibrary(Storage); Rules = new RuleEngine(Storage, Audio);
        Audio.Volume = Storage.Data.Settings.Volume; Audio.DeviceId = Storage.Data.Settings.OutputDeviceId;
        Rules.Status += SetStatus; Audio.Failed += text => Dispatcher.BeginInvoke(() => SetStatus(text));
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(218) }); root.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar = new DockPanel { Margin = new Thickness(18, 26, 18, 18) };
        var footer = new StackPanel(); footer.Children.Add(indicator); footer.Children.Add(Ui.Text("Windows 原生音频工作台\nv1.0.0", 11, "#6E7C98")); DockPanel.SetDock(footer, Dock.Bottom); sidebar.Children.Add(footer);
        var nav = new StackPanel(); nav.Children.Add(Ui.Text("◈  FocusTone", 24, "#B4A2FF")); nav.Children.Add(Ui.Text("让每个瞬间，有自己的声音", 11, "#9BA8C3"));
        nav.Children.Add(new Border { Height = 30 });
        nav.Children.Add(Ui.Button("⌂   工作台", ShowHome)); nav.Children.Add(Ui.Button("♫   音频库 / 剪辑", () => ShowLibrary())); nav.Children.Add(Ui.Button("⌘   快捷键规则", ShowRules)); nav.Children.Add(Ui.Button("▷   Bilibili 发现", () => Navigate(new BilibiliView(this)))); nav.Children.Add(Ui.Button("⚙   设置", () => Navigate(new SettingsView(this)))); sidebar.Children.Add(nav);
        var side = new Border { Background = Ui.Brush("SurfaceBrush"), Child = sidebar }; root.Children.Add(side);
        var body = new DockPanel { Margin = new Thickness(30, 26, 30, 18) }; Grid.SetColumn(body, 1); root.Children.Add(body);
        var bottom = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        var stop = Ui.Button("■ 停止音频", () => { Audio.Stop(); SetStatus("已停止播放"); }); DockPanel.SetDock(stop, Dock.Right); bottom.Children.Add(stop); bottom.Children.Add(status); DockPanel.SetDock(bottom, Dock.Bottom); body.Children.Add(bottom); body.Children.Add(content); Content = root;
        tray = new TrayService(this);
        SourceInitialized += (_, _) =>
        {
            try { hotkeys = new HotkeyManager(new WindowInteropHelper(this).Handle); hotkeys.Pressed += key => { RawKey?.Invoke(key); Rules.Trigger(key); }; }
            catch (Exception ex) { Log.Error("Raw Input 注册", ex); Storage.Data.Settings.HotkeysEnabled = false; SetStatus("按键监听注册失败，请重启软件。音频库仍可使用。"); }
            UpdateIndicator();
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && Storage.Data.Settings.MinimizeToTray) Hide(); };
        Closing += OnClosing; ShowHome();
        if (Storage.RecoveryWarning != null) Loaded += (_, _) => SetStatus(Storage.RecoveryWarning);
    }
    public void SetStatus(string text) { status.Text = text; status.ToolTip = text; }
    public void UpdateIndicator() { indicator.Text = Storage.Data.Settings.HotkeysEnabled ? "● 快捷键监听中" : "○ 快捷键已暂停"; tray.Refresh(); }
    public void Navigate(UIElement page)
    {
        if (content.Content is IDisposable previous) previous.Dispose(); content.Content = page; Ui.Animate(page);
    }
    public void ShowLibrary(Models.AudioItem? selected = null) => Navigate(new LibraryView(this, selected));
    public void ShowRules() => Navigate(new RulesView(this));
    public void ShowHome()
    {
        var page = new StackPanel(); page.Children.Add(Ui.Heading("把声音，交给这一刻。", "选择应用，绑定按键，只在它位于前台时播放。"));
        var hero = new StackPanel(); hero.Children.Add(Ui.Text("FOCUS. PRESS. PLAY.", 12, "#B4A2FF")); hero.Children.Add(Ui.Text("你的游戏时刻\n值得一段专属配乐。", 32)); hero.Children.Add(Ui.Text("非破坏性剪辑 · 本地播放 · 不触碰游戏内部数据", 14, "#9BA8C3")); hero.Children.Add(Ui.Row(Ui.Button("＋ 导入第一段音频", () => ShowLibrary(), true), Ui.Button("创建快捷键规则 →", ShowRules))); page.Children.Add(Ui.Card(hero));
        var stats = Ui.Row(Ui.Text($"{Storage.Data.Library.Count}  段音频", 22), new Border { Width = 40 }, Ui.Text($"{Storage.Data.Rules.Count(r => r.Enabled)}  条启用规则", 22)); page.Children.Add(Ui.Card(stats));
        var steps = new StackPanel(); steps.Children.Add(Ui.Text("三步开始", 18)); steps.Children.Add(Ui.Text("01   导入 MP3 / WAV / FLAC / M4A / OGG\n\n02   在波形上选取片段，试听并保存\n\n03   选择正在运行的应用，录入快捷键", 15, "#B9C5DB")); page.Children.Add(Ui.Card(steps)); Navigate(new ScrollViewer { Content = page });
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
