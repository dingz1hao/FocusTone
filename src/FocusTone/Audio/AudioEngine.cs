using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using FocusTone.Models;
namespace FocusTone.Audio;
public sealed class AudioEngine : IDisposable
{
    private WasapiOut? output;
    private SegmentStream? stream;
    private MMDevice? device;
    private VolumeSampleProvider? gain;
    private int generation;
    private float volume = .7f;
    public string? DeviceId { get; set; }
    public float Volume { get => volume; set { volume = Math.Clamp(value, 0, 1); if (gain != null) gain.Volume = volume; } }
    public bool IsPlaying => output?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => output?.PlaybackState == PlaybackState.Paused;
    public double Position => stream?.AbsoluteSeconds ?? 0;
    public Guid? CurrentId { get; private set; }
    public event Action<string>? Failed;
    public event Action? PlaybackChanged;
    public static List<KeyValuePair<string, string>> Devices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<KeyValuePair<string, string>> { new("", "跟随 Windows 默认设备") };
        foreach (var item in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) { using (item) result.Add(new(item.ID, item.FriendlyName)); }
        return result;
    }
    public async Task PlayAsync(string path, double start, double end, Guid? id = null, bool loop = false, TriggerBehavior behavior = TriggerBehavior.ReplaceCurrent)
    {
        if (behavior == TriggerBehavior.IgnoreWhilePlaying && (IsPlaying || IsPaused)) return;
        if (behavior == TriggerBehavior.Restart && CurrentId != null && CurrentId != id && (IsPlaying || IsPaused)) return;
        Stop(); var ticket = generation;
        var decoded = await Task.Run(() => AudioDecoder.Open(path));
        if (ticket != generation) { decoded.Dispose(); return; }
        try
        {
            stream = new SegmentStream(decoded, start, end, loop);
            using var enumerator = new MMDeviceEnumerator();
            device = string.IsNullOrEmpty(DeviceId) ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : enumerator.GetDevice(DeviceId);
            output = new WasapiOut(device, AudioClientShareMode.Shared, true, 35);
            gain = new VolumeSampleProvider(stream.ToSampleProvider()) { Volume = Volume };
            output.Init(gain.ToWaveProvider()); CurrentId = id;
            output.PlaybackStopped += (_, e) => { PlaybackChanged?.Invoke(); if (e.Exception != null) { Services.Log.Error("音频设备播放", e.Exception); Failed?.Invoke("音频设备不可用，请在设置中重新选择输出设备。"); } };
            output.Play(); PlaybackChanged?.Invoke();
        }
        catch { decoded.Dispose(); Stop(); throw; }
    }
    public void PauseResume() { if (IsPlaying) output?.Pause(); else if (IsPaused) output?.Play(); PlaybackChanged?.Invoke(); }
    public void Seek(double seconds) => stream?.Seek(seconds);
    public void Stop() { generation++; output?.Stop(); output?.Dispose(); output = null; gain = null; stream?.Dispose(); stream = null; device?.Dispose(); device = null; CurrentId = null; PlaybackChanged?.Invoke(); }
    public void Dispose() => Stop();
}
