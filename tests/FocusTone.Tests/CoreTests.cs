using System.IO;
using FocusTone.Audio;
using FocusTone.Models;
using FocusTone.Native;
using FocusTone.Services;
using NAudio.Wave;
using Xunit;
namespace FocusTone.Tests;
public class CoreTests
{
    [Theory]
    [InlineData("01:12.500",72.5)] [InlineData("03:40.000",220)] [InlineData("0.001",.001)]
    public void ParsesMillisecondTimes(string text, double expected) { Assert.True(TimeText.TryParse(text, out var value)); Assert.Equal(expected, value, 5); }
    [Theory]
    [InlineData("NaN")] [InlineData("Infinity")] [InlineData("-1")] [InlineData("00:61.000")] [InlineData("garbage")]
    public void RejectsInvalidTimes(string text) => Assert.False(TimeText.TryParse(text, out _));
    [Theory]
    [InlineData("shift+ctrl+z","Ctrl+Shift+Z")] [InlineData("alt+f1","Alt+F1")] [InlineData("mouse4","Mouse4")] [InlineData("5","D5")]
    public void CanonicalizesHotkeys(string input, string expected) => Assert.Equal(expected, HotkeyManager.Normalize(input));
    [Theory]
    [InlineData("Ctrl+Ctrl+Z")] [InlineData("Win+Z")] [InlineData("LeftCtrl")] [InlineData("NoSuchKey")]
    public void RejectsAmbiguousHotkeys(string input) => Assert.Throws<ArgumentException>(() => HotkeyManager.Normalize(input));
    [Fact]
    public void SegmentStopsExactlyAtFrameBoundary()
    {
        using var source = new RawSourceWaveStream(new MemoryStream(new byte[48000 * 4]), new WaveFormat(48000,16,2));
        using var segment = new SegmentStream(source,.125,.375);
        var buffer = new byte[4096]; var total = 0; int read;
        while ((read = segment.Read(buffer,0,buffer.Length)) > 0) total += read;
        Assert.Equal(12000 * 4, total); Assert.Equal(.375, segment.AbsoluteSeconds, 5);
    }
    [Fact]
    public void LoopFillsBufferWithoutPassingSelection()
    {
        var data = Enumerable.Repeat((byte)42, 400).ToArray();
        using var source = new RawSourceWaveStream(new MemoryStream(data), new WaveFormat(1000,16,1));
        using var segment = new SegmentStream(source,.05,.1,true);
        var buffer = new byte[1000]; Assert.Equal(buffer.Length,segment.Read(buffer,0,buffer.Length)); Assert.All(buffer, value => Assert.Equal((byte)42,value));
    }
    [Fact]
    public void SeekIsClampedToSelection()
    {
        using var source = new RawSourceWaveStream(new MemoryStream(new byte[4000]), new WaveFormat(1000,16,1));
        using var segment = new SegmentStream(source,.5,1.5);
        segment.Seek(0); Assert.Equal(.5, segment.AbsoluteSeconds); segment.Seek(9); Assert.Equal(1.5,segment.AbsoluteSeconds);
    }
    [Fact]
    public void ConfigurationRoundTripsAndRecoversBackup()
    {
        var root = Path.Combine(Path.GetTempPath(),"FocusTone-tests-"+Guid.NewGuid());
        try
        {
            var storage = new Storage(root); storage.Data.Rules.Add(new AudioRule { ProcessPath = @"C:\Games\r5apex.exe", Hotkey = "Ctrl+Z", Start = 72.5, End = 86.8 }); storage.Save(); storage.Data.Settings.Volume = .2f; storage.Save();
            var loaded = new Storage(root); Assert.Single(loaded.Data.Rules); Assert.Equal(.2f,loaded.Data.Settings.Volume); Assert.Equal(72.5,loaded.Data.Rules[0].Start);
            File.WriteAllText(Path.Combine(root,"settings.json"),"broken"); var recovered = new Storage(root); Assert.NotNull(recovered.RecoveryWarning); Assert.Single(recovered.Data.Rules);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public void LibraryPathsCannotEscapeRoot() => Assert.Throws<InvalidDataException>(() => Storage.SafeFile(@"C:\Data", @"..\secret.txt"));
    [Fact]
    public async Task ImportPersistsOwnedCopyAndWaveform()
    {
        var root = Path.Combine(Path.GetTempPath(), "FocusTone-tests-"+Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root,"input.wav");
            using (var writer = new WaveFileWriter(file, WaveFormat.CreateIeeeFloatWaveFormat(48000,1))) for(var i=0;i<48000;i++) writer.WriteSample((float)Math.Sin(i*Math.PI*2*440/48000)*.2f);
            var storage = new Storage(Path.Combine(root,"data")); var library = new AudioLibrary(storage); var item = await library.ImportAsync(file); Assert.True(File.Exists(storage.AudioPath(item))); Assert.Equal(1,item.Duration,3);
            var peaks = await WaveformService.LoadAsync(storage.AudioPath(item),Path.Combine(storage.CacheDirectory,"test.wave"),default); Assert.Equal(2400,peaks.Length); Assert.All(peaks,p=>Assert.InRange(p,0,.201f)); Assert.InRange(peaks.Max(),.199f,.201f);
            var cached = await WaveformService.LoadAsync(storage.AudioPath(item),Path.Combine(storage.CacheDirectory,"test.wave"),default); Assert.Equal(peaks,cached);
            library.Delete(item); Assert.True(File.Exists(file)); Assert.Empty(new Storage(storage.Root).Data.Library);
        }
        finally { Directory.Delete(root,true); }
    }
}
