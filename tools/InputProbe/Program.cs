using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FocusTone.InputProbe;
public static class Program
{
    [STAThread] public static void Main()
    {
        var app=new Application();
        var message=new TextBlock { Text="FocusTone 输入验证窗口\n\n可以将规则绑定到本 EXE。\n按下 F8 / F9 检查原始按键是否仍然到达目标。\n这里只统计次数，不保存任何键盘记录。",Foreground=Brushes.White,FontSize=19,Margin=new Thickness(28),TextWrapping=TextWrapping.Wrap };
        var window=new Window { Title="FocusTone Input Probe",Width=560,Height=300,Background=new SolidColorBrush(Color.FromRgb(20,25,40)),Content=message };
        var count=0; window.KeyDown+=(_,e)=> { count++; message.Text=$"已收到 {count} 次按下事件\n本次：{e.Key}\n自动重复：{e.IsRepeat}\n\n目标窗口仍正常收到按键。"; };
        app.Run(window);
    }
}
