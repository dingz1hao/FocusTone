using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FocusTone.Native;
public static class AppIcons
{
    public static ImageSource ForExecutable(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon != null) return Convert(icon);
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { }
        return Convert(System.Drawing.SystemIcons.Application);
    }
    private static BitmapSource Convert(System.Drawing.Icon icon)
    {
        var image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
        image.Freeze();
        return image;
    }
}
