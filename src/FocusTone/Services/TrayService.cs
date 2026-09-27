namespace FocusTone.Services;
public sealed class TrayService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon icon;
    private readonly System.Windows.Forms.ToolStripMenuItem pause;
    private readonly MainWindow window;
    private readonly System.Drawing.Icon applicationIcon;
    public TrayService(MainWindow window)
    {
        this.window = window;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开主界面", null, (_, _) => window.Restore());
        pause = new System.Windows.Forms.ToolStripMenuItem("暂停所有快捷键");
        pause.Click += (_, _) => { window.Storage.Data.Settings.HotkeysEnabled = !window.Storage.Data.Settings.HotkeysEnabled; window.UpdateIndicator(); try { window.Storage.Save(); } catch (Exception ex) { Log.Error("托盘设置保存", ex); window.SetStatus("设置保存失败。"); } };
        menu.Items.Add(pause); menu.Items.Add("停止当前音频", null, (_, _) => window.Audio.Stop()); menu.Items.Add(new System.Windows.Forms.ToolStripSeparator()); menu.Items.Add("退出", null, (_, _) => window.Exit());
        using var resource = System.Windows.Application.GetResourceStream(new Uri("Assets/FocusTone.ico", UriKind.Relative)).Stream;
        applicationIcon = new System.Drawing.Icon(resource);
        icon = new System.Windows.Forms.NotifyIcon { Icon = applicationIcon, Text = "FocusTone", Visible = true, ContextMenuStrip = menu };
        icon.DoubleClick += (_, _) => window.Restore();
    }
    public void Refresh() => pause.Checked = !window.Storage.Data.Settings.HotkeysEnabled;
    public void Dispose() { icon.Visible = false; icon.ContextMenuStrip?.Dispose(); icon.Dispose(); applicationIcon.Dispose(); }
}
