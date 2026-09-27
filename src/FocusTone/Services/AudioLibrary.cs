using System.IO;
using FocusTone.Audio;
using FocusTone.Models;
namespace FocusTone.Services;
public sealed class AudioLibrary(Storage storage)
{
    public async Task<AudioItem> ImportAsync(string path, string? title = null, string? source = null, CancellationToken cancellation = default)
    {
        var item = await Task.Run(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            using var reader = AudioDecoder.Open(path);
            var duration = reader.TotalTime.TotalSeconds;
            if (duration <= 0 || duration > 14400) throw new InvalidDataException("音频时长须在 0 到 4 小时之间。");
            var item = new AudioItem { Title = title ?? Path.GetFileNameWithoutExtension(path), Duration = duration, End = duration, Source = source ?? "本地文件" };
            item.FileName = item.Id + Path.GetExtension(path).ToLowerInvariant();
            try
            {
                using var tag = TagLib.File.Create(path);
                if (title == null && !string.IsNullOrWhiteSpace(tag.Tag.Title)) item.Title = tag.Tag.Title;
                var picture = tag.Tag.Pictures.FirstOrDefault();
                if (picture != null && picture.Data.Count < 5_000_000)
                {
                    item.CoverFile = item.Id + ".cover";
                    File.WriteAllBytes(Storage.SafeFile(storage.LibraryDirectory, item.CoverFile), picture.Data.Data);
                }
            }
            catch (Exception ex) { Log.Error("音频封面解析", ex); }
            try { File.Copy(path, storage.AudioPath(item)); }
            catch { if (item.CoverFile != null) File.Delete(Storage.SafeFile(storage.LibraryDirectory, item.CoverFile)); throw; }
            return item;
        }, cancellation);
        storage.Data.Library.Add(item);
        try { storage.Save(); }
        catch { storage.Data.Library.Remove(item); File.Delete(storage.AudioPath(item)); throw; }
        return item;
    }
    public void Delete(AudioItem item)
    {
        var rules = storage.Data.Rules.Where(r => r.AudioId == item.Id).ToList();
        storage.Data.Library.Remove(item); storage.Data.Rules.RemoveAll(r => r.AudioId == item.Id);
        try { storage.Save(); }
        catch { storage.Data.Library.Add(item); storage.Data.Rules.AddRange(rules); throw; }
        File.Delete(storage.AudioPath(item));
        if (item.CoverFile != null) File.Delete(Storage.SafeFile(storage.LibraryDirectory, item.CoverFile));
        File.Delete(Path.Combine(storage.CacheDirectory, item.Id + ".wave"));
    }
}
