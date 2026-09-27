using System.Windows;
using FocusTone.Services;
namespace FocusTone;
public partial class App : System.Windows.Application
{
    private Mutex? mutex;
    private EventWaitHandle? activation;
    private RegisteredWaitHandle? activationRegistration;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, "Local\\FocusTone.Desktop", out var first);
        if (!first)
        {
            try { using var signal = EventWaitHandle.OpenExisting("Local\\FocusTone.Desktop.Activate"); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("FocusTone 正在启动，请稍后从系统托盘打开。", "FocusTone"); }
            Shutdown(); return;
        }
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\FocusTone.Desktop.Activate");
        activationRegistration = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => Dispatcher.BeginInvoke(() => (MainWindow as FocusTone.MainWindow)?.Restore()), null, Timeout.Infinite, false);
        DispatcherUnhandledException += (_, args) => { Log.Error("界面异常", args.Exception); MessageBox.Show("操作未完成，请重试。诊断日志已保存到数据目录。", "FocusTone"); args.Handled = true; };
        try { var window = new MainWindow(); MainWindow = window; window.Show(); }
        catch (Exception ex) { Log.Error("启动", ex); MessageBox.Show($"启动失败：{ex.Message}", "FocusTone"); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { activationRegistration?.Unregister(null); activation?.Dispose(); mutex?.Dispose(); base.OnExit(e); }
}
