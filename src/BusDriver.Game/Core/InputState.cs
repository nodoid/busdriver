using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace BusDriver.Game.Core;

/// <summary>
/// Touch-first input.  Every touch is mapped from the letterboxed back buffer
/// into the 1280x720 virtual canvas, so buttons sit where they are drawn on
/// every screen size and safe-area margin.  A tap completes on release, which
/// is also what iOS actually delivers reliably through MonoGame.
/// </summary>
public sealed class InputState
{
    private sealed class Track
    {
        public Vector2 Start;
        public Vector2 Last;
    }

    private readonly List<Vector2> _taps = new();
    private readonly List<Vector2> _held = new();
    private readonly Dictionary<int, Track> _tracks = new();
    private readonly HashSet<Keys> _keys = new();
    private readonly HashSet<Keys> _previousKeys = new();
    private readonly List<PointerSample> _samples = new();
    private float _scale = 1f;
    private Vector2 _offset;
    private float _screenWidth = 1f;
    private float _screenHeight = 1f;
    private bool _loggedFirstTouch;
    private bool _loggedTransform;

    public IReadOnlyList<Vector2> Taps => _taps;

    /// <summary>Where the most recent completed tap landed, in virtual pixels.</summary>
    public Vector2? LastTap { get; private set; }

    public IReadOnlyList<Vector2> Held => _held;

    public bool AnyTap => _taps.Count > 0;

    public Vector2 FirstTap => _taps.Count > 0 ? _taps[0] : Vector2.Zero;

    public void SetTransform(int backBufferWidth, int backBufferHeight, int virtualWidth, int virtualHeight)
    {
        _screenWidth = Math.Max(1, backBufferWidth);
        _screenHeight = Math.Max(1, backBufferHeight);
        _scale = MathF.Min(
            backBufferWidth / (float)virtualWidth,
            backBufferHeight / (float)virtualHeight);
        _offset = new Vector2(
            (backBufferWidth - virtualWidth * _scale) * 0.5f,
            (backBufferHeight - virtualHeight * _scale) * 0.5f);

        if (!_loggedTransform)
        {
            _loggedTransform = true;
            StartupLog.Write(
                $"input transform {backBufferWidth}x{backBufferHeight} scale {_scale:0.000} " +
                $"offset {_offset.X:0},{_offset.Y:0}");
        }
    }

    public void Update()
    {
        _taps.Clear();
        _held.Clear();

        if (PlatformPointer.InUse)
        {
            _samples.Clear();
            PlatformPointer.Drain(_samples);
            foreach (var sample in _samples)
            {
                // Samples arrive normalised across the whole game view, so the
                // letterbox transform below puts them on the virtual canvas.
                var screen = new Vector2(sample.X * _screenWidth, sample.Y * _screenHeight);
                Process(sample.Id, screen, sample.Down);
            }
        }
        else
        {
            var touches = TouchPanel.GetState();
            foreach (var touch in touches)
            {
                if (touch.State == TouchLocationState.Invalid)
                {
                    continue;
                }

                Process(touch.Id, touch.Position, touch.State != TouchLocationState.Released);
            }

            // If the panel reports nothing (a missed release, a phone call), drop
            // stale tracks so the next press starts cleanly.
            if (touches.Count == 0)
            {
                _tracks.Clear();
            }
        }

        // A finger that is held still produces no new events, so the held set
        // is rebuilt from every press still down rather than from this frame's
        // samples alone.
        _held.Clear();
        foreach (var track in _tracks.Values)
        {
            _held.Add(track.Last);
        }

        var keyboard = Keyboard.GetState();
        _previousKeys.Clear();
        foreach (var key in _keys)
        {
            _previousKeys.Add(key);
        }

        _keys.Clear();
        foreach (var key in keyboard.GetPressedKeys())
        {
            _keys.Add(key);
        }
    }

    /// <summary>Feeds one pointer position (in screen pixels) through the tap state machine.</summary>
    private void Process(int id, Vector2 screen, bool down)
    {
        var point = ScreenToVirtual(screen);

        if (!down)
        {
            if (_tracks.Remove(id, out var released) &&
                Vector2.Distance(released.Start, point) < 48f)
            {
                _taps.Add(point);
                LastTap = point;
            }

            return;
        }

        if (!_tracks.TryGetValue(id, out var active))
        {
            if (!_loggedFirstTouch)
            {
                _loggedFirstTouch = true;
                StartupLog.Write($"first pointer at {screen.X:0},{screen.Y:0} -> {point.X:0},{point.Y:0}");
            }

            active = new Track { Start = point, Last = point };
            _tracks[id] = active;
        }

        active.Last = point;
    }

    public Vector2 ScreenToVirtual(Vector2 screen) => (screen - _offset) / _scale;

    public bool WasTapped(Rectangle rectangle) =>
        _taps.Any(p => rectangle.Contains(p));

    public bool IsHeld(Rectangle rectangle) =>
        _held.Any(p => rectangle.Contains(p));

    public bool KeyDown(Keys key) => _keys.Contains(key);

    public bool KeyPressed(Keys key) => _keys.Contains(key) && !_previousKeys.Contains(key);

    public bool AnyKeyPressed => _keys.Any(k => !_previousKeys.Contains(k));

    public bool ConfirmPressed =>
        KeyPressed(Keys.Space) || KeyPressed(Keys.Enter);

    public bool BackPressed => KeyPressed(Keys.Escape);

    /// <summary>Synthetic tap used by device smoke tests.</summary>
    public void SimulateTap(Vector2 point)
    {
        _taps.Clear();
        _held.Clear();
        _taps.Add(point);
    }
}
