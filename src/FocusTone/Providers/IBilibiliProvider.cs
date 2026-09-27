namespace FocusTone.Providers;
public interface IBilibiliProvider
{
    Uri Search(string keyword);
    Uri Video(string bvid);
    bool AllowsNavigation(Uri uri);
}
