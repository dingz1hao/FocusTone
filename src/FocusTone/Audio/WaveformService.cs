using System.IO;
using NAudio.Wave;
namespace FocusTone.Audio;
public static class WaveformService
{
    public static Task<float[]> LoadAsync(string path, string cachePath, CancellationToken cancellation) => Task.Run(() =>
    {
        const int bins = 2400;
        if (File.Exists(cachePath))
        {
            var data = File.ReadAllBytes(cachePath);
            if (data.Length == bins * sizeof(float)) { var cached = new float[bins]; Buffer.BlockCopy(data, 0, cached, 0, data.Length); if (cached.All(float.IsFinite)) return cached; }
        }
        using var reader = AudioDecoder.Open(path);
        var samples = reader.ToSampleProvider(); var buffer = new float[16384]; var peaks = new float[bins];
        var total = Math.Max(1, reader.TotalTime.TotalSeconds * samples.WaveFormat.SampleRate * samples.WaveFormat.Channels);
        long index = 0; int read;
        while ((read = samples.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var i = 0; i < read; i++, index++)
            {
                var bin = Math.Min(bins - 1, (int)(index * bins / total));
                peaks[bin] = Math.Max(peaks[bin], Math.Min(1, Math.Abs(buffer[i])));
            }
        }
        cancellation.ThrowIfCancellationRequested();
        var bytes = new byte[bins * sizeof(float)]; Buffer.BlockCopy(peaks, 0, bytes, 0, bytes.Length);
        File.WriteAllBytes(cachePath + ".tmp", bytes); File.Move(cachePath + ".tmp", cachePath, true);
        return peaks;
    }, cancellation);
}
