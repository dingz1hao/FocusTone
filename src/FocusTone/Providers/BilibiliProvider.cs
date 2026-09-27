using System.Text.RegularExpressions;
namespace FocusTone.Providers;
// Official navigation only. No media URL extraction or downloading.
public sealed class BilibiliProvider : IBilibiliProvider
{
    public Uri Search(string keyword) => new("https://search.bilibili.com/all?keyword=" + Uri.EscapeDataString(keyword.Trim()));
    public Uri Video(string bvid)
    {
        if (!Regex.IsMatch(bvid.Trim(), "^BV[0-9A-Za-z]{10}$")) throw new ArgumentException("请输入有效 BV 号。");
        return new Uri("https://www.bilibili.com/video/" + bvid.Trim() + "/");
    }
    public bool AllowsNavigation(Uri uri) => uri.Scheme == "https" && (uri.Host == "bilibili.com" || uri.Host.EndsWith(".bilibili.com",StringComparison.OrdinalIgnoreCase));
}
