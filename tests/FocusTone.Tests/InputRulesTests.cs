using FocusTone.Models;
using FocusTone.Native;
using FocusTone.Services;
using Xunit;

namespace FocusTone.Tests;
public class InputRulesTests
{
    [Fact]
    public void HeldKeyTriggersOnlyOnceUntilReleased()
    {
        var edges = new KeyEdges();
        Assert.True(edges.Update(90, true));
        for (var i = 0; i < 100; i++) Assert.False(edges.Update(90, true));
        Assert.False(edges.Update(90, false));
        Assert.True(edges.Update(90, true));
    }
    [Fact]
    public void IndependentKeysAndResetRemainUsable()
    {
        var edges = new KeyEdges();
        Assert.True(edges.Update(90, true));
        Assert.True(edges.Update(0x10005, true));
        Assert.False(edges.Update(90, true));
        edges.Clear();
        Assert.True(edges.Update(90, true));
    }
    [Theory]
    [InlineData(@"C:\Games\Game.exe", "Z", true)]
    [InlineData(@"c:\games\game.EXE", "Z", true)]
    [InlineData(@"D:\Other\Game.exe", "Z", false)]
    [InlineData(@"C:\Games\Game.exe", "Ctrl+Z", false)]
    [InlineData(null, "Z", false)]
    public void ForegroundRequiresExactPathAndChord(string? path, string key, bool expected)
    {
        var rule = new AudioRule { ProcessPath = @"C:\Games\Game.exe", Hotkey = "Z" };
        Assert.Equal(expected, RuleMatcher.Find([rule], key, path) != null);
        rule.Enabled = false;
        Assert.Null(RuleMatcher.Find([rule], key, path));
    }
}
