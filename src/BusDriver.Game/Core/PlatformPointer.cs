namespace BusDriver.Game.Core;

/// <summary>
/// One pointer sample from a platform head, in 0..1 across the game view.
/// </summary>
public readonly record struct PointerSample(int Id, float X, float Y, bool Down);

/// <summary>
/// Optional native pointer feed.  MonoGame's iOS TouchPanel reports positions
/// that do not line up with the drawn frame on a scene-based launch, so the
/// iOS head pushes touches from its own view instead and the game maps those
/// normalised coordinates onto the virtual canvas itself.  When no head
/// registers samples the game falls back to TouchPanel as usual.
/// </summary>
public static class PlatformPointer
{
    private static readonly object Gate = new();
    private static readonly List<PointerSample> Pending = new();

    /// <summary>Set by a platform head once it starts pushing samples.</summary>
    public static bool InUse { get; set; }

    public static void Push(int id, float x, float y, bool down)
    {
        lock (Gate)
        {
            Pending.Add(new PointerSample(id, x, y, down));
        }
    }

    public static void PushAll(IEnumerable<PointerSample> samples)
    {
        lock (Gate)
        {
            Pending.AddRange(samples);
        }
    }

    public static void Drain(List<PointerSample> into)
    {
        lock (Gate)
        {
            if (Pending.Count == 0)
            {
                return;
            }

            into.AddRange(Pending);
            Pending.Clear();
        }
    }
}
