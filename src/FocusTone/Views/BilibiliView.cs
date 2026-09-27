using System.IO;
using System.Windows;
using System.Windows.Controls;
using FocusTone.Providers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace FocusTone.Views;
public sealed class BilibiliView : UserControl, IDisposable
{
    private readonly MainWindow host;
    private readonly IBilibiliProvider provider = new BilibiliProvider();
    private readonly WebView2 browser = new();
    private readonly TextBox query = new() { Width = 310, ToolTip = "输入关键词或 BV 号" };
    private readonly TextBlock state = Ui.Text("官方网页搜索与播放 · 不提取或下载平台音频",12,"#9BA8C3");
    private Uri current = new("https://www.bilibili.com/");
    private Task? initialization;
    private bool disposed;
    public BilibiliView(MainWindow host)
    {
        this.host = host;
        var root = new DockPanel();
        var header = Ui.Heading("Bilibili 发现","通过官方页面发现音乐；快捷键音频使用你拥有或获授权的本地文件。");
        header.Children.Add(Ui.Row(query,Ui.AsyncButton("搜索 / 打开 BV",Navigate,SetStatus,true),Ui.Button("在浏览器打开",()=>Ui.OpenUrl(current.ToString()))));
        header.Children.Add(state); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
        var bottom = new StackPanel { Margin = new Thickness(0,12,0,0) };
        bottom.Children.Add(Ui.Row(Ui.AsyncButton("导入已有授权音频",ImportLicensed,SetStatus),Ui.Button("返回",()=> { if(browser.CoreWebView2?.CanGoBack==true) browser.CoreWebView2.GoBack(); })));
        bottom.Children.Add(Ui.Text("标题、UP 主、封面、时长、分 P 和播放由官方页面提供。软件不提取媒体地址、不下载或转换播放流。",12,"#9BA8C3"));
        DockPanel.SetDock(bottom,Dock.Bottom); root.Children.Add(bottom); root.Children.Add(browser); Content=root;
        if(!host.Storage.Data.Settings.BilibiliEnabled) { header.IsEnabled=false; browser.Visibility=Visibility.Collapsed; SetStatus("Bilibili 功能已在设置中关闭。"); }
        else Loaded += async (_,_) => { try { await EnsureBrowser(); } catch(Exception ex) { Services.Log.Error("官方网页初始化",ex); SetStatus("内嵌浏览器不可用。请安装 WebView2 Runtime，或在外部浏览器打开。"); } };
    }
    private void SetStatus(string text) { if(!disposed) { state.Text=text; host.SetStatus(text); } }
    private Task EnsureBrowser() => initialization ??= InitializeBrowser();
    private async Task InitializeBrowser()
    {
        var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(host.Storage.Root,"WebView2"));
        if(disposed) return;
        await browser.EnsureCoreWebView2Async(environment); if(disposed) return;
        var web=browser.CoreWebView2;
        web.Settings.AreDevToolsEnabled=false; web.Settings.AreDefaultContextMenusEnabled=false;
        web.Settings.IsPasswordAutosaveEnabled=false; web.Settings.IsGeneralAutofillEnabled=false;
        web.PermissionRequested += (_,e)=> e.State=CoreWebView2PermissionState.Deny;
        web.DownloadStarting += (_,e)=> { e.Cancel=true; SetStatus("这里只提供官方在线播放，请导入已有授权的音频文件。"); };
        web.NewWindowRequested += (_,e)=> { e.Handled=true; if(Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri)&&provider.AllowsNavigation(uri)) web.Navigate(uri.ToString()); else SetStatus("外部页面请在普通浏览器中打开。"); };
        web.NavigationStarting += (_,e)=> { if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri)||!provider.AllowsNavigation(uri)) { e.Cancel=true; return; } current=uri; SetStatus("正在加载官方页面…"); };
        web.NavigationCompleted += (_,e)=> SetStatus(e.IsSuccess?"官方页面已加载 · 播放遵循平台限制":"页面加载失败，请检查网络或使用外部浏览器。");
        web.ProcessFailed += (_,_)=>SetStatus("网页进程退出，请切换页面后重试。");
        web.Navigate(current.ToString());
    }
    private async Task Navigate()
    {
        if(string.IsNullOrWhiteSpace(query.Text)) return;
        current=query.Text.Trim().StartsWith("BV",StringComparison.Ordinal)?provider.Video(query.Text):provider.Search(query.Text);
        await EnsureBrowser(); if(!disposed) browser.CoreWebView2?.Navigate(current.ToString());
    }
    private async Task ImportLicensed()
    {
        var dialog=new Microsoft.Win32.OpenFileDialog { Title="选择你拥有或已获授权的本地音频",Filter="音频文件|*.mp3;*.wav;*.flac;*.m4a;*.ogg" };
        if(dialog.ShowDialog()!=true) return;
        var item=await host.Library.ImportAsync(dialog.FileName,source:"用户导入 · 请保留授权凭证"); if(!disposed) host.ShowLibrary(item);
    }
    public void Dispose() { disposed=true; browser.Dispose(); }
}
