using NAudio.Wave;
using NAudio.Vorbis;
namespace FocusTone.Audio;
public static class AudioDecoder
{
    public static WaveStream Open(string path) => System.IO.Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase) ? new VorbisWaveReader(path) : new AudioFileReader(path);
}
// Sample-aligned bounds, independent of UI timers.
public sealed class SegmentStream : WaveStream
{
    private readonly WaveStream source;
    private readonly object gate = new();
    private readonly long start, end;
    public bool Loop { get; set; }
    public SegmentStream(WaveStream source, double from, double to, bool loop = false)
    {
        Models.TimeText.ValidateRange(from, to, source.TotalTime.TotalSeconds);
        this.source = source; Loop = loop;
        start = Align(from); end = Math.Min(source.Length, Align(to)); source.Position = start;
    }
    private long Align(double seconds) => (long)(seconds * WaveFormat.AverageBytesPerSecond / WaveFormat.BlockAlign) * WaveFormat.BlockAlign;
    public override WaveFormat WaveFormat => source.WaveFormat;
    public override long Length => end - start;
    public override long Position { get { lock (gate) return source.Position - start; } set { lock (gate) source.Position = start + Math.Clamp(value / WaveFormat.BlockAlign * WaveFormat.BlockAlign, 0, Length); } }
    public double AbsoluteSeconds => (start + Position) / (double)WaveFormat.AverageBytesPerSecond;
    public void Seek(double seconds) => Position = Align(seconds) - start;
    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (gate)
        {
            var total = 0;
            while (count >= WaveFormat.BlockAlign)
            {
                if (source.Position >= end) { if (!Loop || end <= start) break; source.Position = start; }
                var remaining = (int)Math.Min(count, end - source.Position); remaining -= remaining % WaveFormat.BlockAlign;
                if (remaining <= 0) break;
                var read = source.Read(buffer, offset, remaining); if (read == 0) break;
                total += read; offset += read; count -= read;
            }
            return total;
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
}
