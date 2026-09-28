using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FocusTone.Audio;
namespace FocusTone.Views;
public sealed class SettingsView : UserControl
{
    public SettingsView(MainWindow host)
    {
        var settings = host.Storage.Data.Settings; var panel = new StackPanel(); panel.Children.Add(Ui.Heading("设置", "轻量后台运行，声音与操作都由你掌控。"));
        var general = new StackPanel(); general.Children.Add(Ui.Text("常规", 20));
        var hotkeys = new CheckBox { Content = "启用所有快捷键", IsChecked = settings.HotkeysEnabled }; var tray = new CheckBox { Content = "最小化 / 关闭主窗口时留在系统托盘", IsChecked = settings.MinimizeToTray }; var startup = new CheckBox { Content = "登录 Windows 时启动", IsChecked = settings.StartWithWindows }; general.Children.Add(hotkeys); general.Children.Add(tray); general.Children.Add(startup); panel.Children.Add(Ui.Card(general));
        var audio = new StackPanel(); audio.Children.Add(Ui.Text("声音输出", 20)); var volume = new Slider { Minimum = 0, Maximum = 100, Value = settings.Volume * 100, Margin = new Thickness(0, 10, 0, 12) }; var value = Ui.Text($"音量 {volume.Value:0}%"); audio.Children.Add(value); audio.Children.Add(volume); var devices = new ComboBox { DisplayMemberPath = "Value", SelectedValuePath = "Key" }; try { devices.ItemsSource = AudioEngine.Devices(); devices.SelectedValue = settings.OutputDeviceId ?? ""; } catch (Exception ex) { Services.Log.Error("枚举音频输出", ex); } audio.Children.Add(devices); audio.Children.Add(Ui.Text("设备切换在下次播放生效。蓝牙设备可能产生额外延迟。", 12, "#A8A8A8")); panel.Children.Add(Ui.Card(audio));
        var bili = new StackPanel(); bili.Children.Add(Ui.Text("Bilibili", 20)); var enabled = new CheckBox { Content = "启用 Bilibili 官方网页搜索和播放", IsChecked = settings.BilibiliEnabled }; bili.Children.Add(enabled); bili.Children.Add(Ui.Text("通过独立 WebView2 页面访问官网。登录由网站处理，本软件不读取 Cookie，不提取或下载平台音频。仅导入你拥有或已获授权的本地文件。", 13, "#A8A8A8")); panel.Children.Add(Ui.Card(bili));
        var storage = new StackPanel(); storage.Children.Add(Ui.Text("数据与缓存", 20)); storage.Children.Add(new TextBox { Text = host.Storage.Root, IsReadOnly = true }); storage.Children.Add(Ui.Row(Ui.Button("打开数据目录", () => Ui.OpenUrl(host.Storage.Root)), Ui.Button("清理缓存", () =>
        {
            host.Audio.Stop(); try { foreach (var file in Directory.GetFiles(host.Storage.CacheDirectory)) File.Delete(file); host.SetStatus("已清理音频与波形缓存，音频库、规则和网页登录状态保留。"); } catch (Exception ex) { Services.Log.Error("清理缓存", ex); host.SetStatus("部分缓存正在使用，稍后重试。"); }
        }))); storage.Children.Add(Ui.Text("如需自定义目录，设置 FOCUSTONE_DATA_DIR 后重启。迁移前退出软件，并复制整个数据目录到新位置。", 12, "#A8A8A8")); panel.Children.Add(Ui.Card(storage));
        volume.ValueChanged += (_, _) => { value.Text = $"音量 {volume.Value:0}%"; host.Audio.Volume = (float)(volume.Value / 100); };
        panel.Children.Add(Ui.Button("保存设置", () =>
        {
            try
            {
                using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (startup.IsChecked == true) run.SetValue("FocusTone", $"\"{Environment.ProcessPath}\""); else run.DeleteValue("FocusTone", false);
                settings.StartWithWindows = startup.IsChecked == true; settings.HotkeysEnabled = hotkeys.IsChecked == true; settings.MinimizeToTray = tray.IsChecked == true; settings.Volume = (float)(volume.Value / 100); settings.OutputDeviceId = devices.SelectedValue as string; settings.BilibiliEnabled = enabled.IsChecked == true; host.Audio.DeviceId = settings.OutputDeviceId; host.Storage.Save(); host.UpdateIndicator(); host.SetStatus("设置已保存。");
            }
            catch (Exception ex) { Services.Log.Error("保存设置", ex); host.SetStatus("设置未能完整保存，请检查数据目录和启动项权限。"); }
        }, true)); Content = new ScrollViewer { Content = panel };
    }
}



