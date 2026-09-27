using FocusTone.Providers;
using Xunit;
namespace FocusTone.Tests;
public class PublicProviderTests
{
    [Theory]
    [InlineData("https://www.bilibili.com/video/BV1234567890/",true)]
    [InlineData("https://search.bilibili.com/all",true)]
    [InlineData("https://bilibili.com.evil.example/",false)]
    [InlineData("http://www.bilibili.com/",false)]
    [InlineData("file:///C:/secrets",false)]
    public void NavigationOnlyAllowsOfficialHttps(string address,bool expected) => Assert.Equal(expected,new BilibiliProvider().AllowsNavigation(new Uri(address)));
    [Fact] public void SearchEscapesQuery() { var uri=new BilibiliProvider().Search("music&other=value"); Assert.Contains("%26",uri.AbsoluteUri); Assert.Equal("search.bilibili.com",uri.Host); }
    [Fact] public void RejectsMalformedBvid() => Assert.Throws<ArgumentException>(()=>new BilibiliProvider().Video("../../private"));
}
