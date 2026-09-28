using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace FocusTone.Views;
public static class Ui
{
    public static TextBlock Text(string text, double size = 14, string? color = null) => new() { Text = text, FontSize = size, Margin = new Thickness(0, 0, 0, 8), Foreground = color == null ? Brush("TextBrush") : (Brush)new BrushConverter().ConvertFromString(color)! };
    public static Brush Brush(string key) => (Brush)System.Windows.Application.Current.FindResource(key);
    public static Button Button(string label, Action action, bool primary = false)
    {
        var button = new Button { Content = label }; if (primary) button.Style = (Style)System.Windows.Application.Current.FindResource("PrimaryButton");
        button.Click += (_, _) => action(); return button;
    }
    public static Button NavButton(string glyph, string label, Action action)
    {
        var icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 17, Foreground = Brush("TextBrush"), Margin = new Thickness(0, 0, 14, 0) };
        var content = Row(icon, new TextBlock { Text = label, Foreground = Brush("TextBrush"), VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = content, Style = (Style)System.Windows.Application.Current.FindResource("NavigationButton") };
        button.Click += (_, _) => action();
        return button;
    }
    public static Button AsyncButton(string label, Func<Task> action, Action<string> status, bool primary = false)
    {
        var button = new Button { Content = label }; if (primary) button.Style = (Style)System.Windows.Application.Current.FindResource("PrimaryButton");
        button.Click += async (_, _) => { button.IsEnabled = false; try { await action(); } catch (OperationCanceledException) { status("操作已取消。"); } catch (Exception ex) { Services.Log.Error(label, ex); status(ex.Message); } finally { button.IsEnabled = true; } }; return button;
    }
    public static StackPanel Row(params UIElement[] children) { var row = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var child in children) row.Children.Add(child); return row; }
    public static Border Card(UIElement child) => new() { Background = Brush("SurfaceBrush"), BorderBrush = Brush("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 14), Child = child };
    public static StackPanel Heading(string title, string subtitle) { var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) }; panel.Children.Add(Text(title, 28)); panel.Children.Add(Text(subtitle, 13, "#A8A8A8")); return panel; }
    public static void Animate(UIElement target)
    {
        var animation = new DoubleAnimation(.45, 1, TimeSpan.FromMilliseconds(180));
        animation.Completed += (_, _) => target.BeginAnimation(UIElement.OpacityProperty, null);
        target.BeginAnimation(UIElement.OpacityProperty, animation);
    }
    public static void OpenUrl(string url) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    public static void Error(string message) => MessageBox.Show(message, "FocusTone", MessageBoxButton.OK, MessageBoxImage.Information);
}
