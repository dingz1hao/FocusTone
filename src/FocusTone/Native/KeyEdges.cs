namespace FocusTone.Native;

/// <summary>One press event per physical down/up cycle, including Raw Input repeats.</summary>
public sealed class KeyEdges
{
    private readonly HashSet<int> down = [];
    public bool Update(int key, bool pressed)
    {
        if (!pressed) { down.Remove(key); return false; }
        return down.Add(key);
    }
    public void Clear() => down.Clear();
}
