using BusDriver.Core;
using Microsoft.Xna.Framework.Audio;

namespace BusDriver.Game.Core;

/// <summary>
/// Engine and weather loops plus one-shot feedback.  Audio is optional: if a
/// device has no output the game keeps going silently.
/// </summary>
public sealed class GameAudio : IDisposable
{
    private readonly Assets _assets;
    private SoundEffectInstance? _engine;
    private SoundEffectInstance? _rain;
    private bool _engineRunning;
    private bool _rainRunning;

    public GameAudio(Assets assets)
    {
        _assets = assets;
        var engine = assets.Sound("engine_loop");
        if (engine is not null)
        {
            _engine = engine.CreateInstance();
            _engine.IsLooped = true;
            _engine.Volume = 0.2f;
            _engine.Pitch = -0.25f;
        }

        var rain = assets.Sound("rain_loop");
        if (rain is not null)
        {
            _rain = rain.CreateInstance();
            _rain.IsLooped = true;
            _rain.Volume = 0.25f;
        }
    }

    public bool Muted { get; set; }

    public void Play(string name, float volume = 0.8f, float pitch = 0f)
    {
        if (Muted)
        {
            return;
        }

        var sound = _assets.Sound(name);
        if (sound is null)
        {
            return;
        }

        try
        {
            sound.Play(Math.Clamp(volume, 0f, 1f), Math.Clamp(pitch, -1f, 1f), 0f);
        }
        catch
        {
            // Never take the game down because the audio device is unavailable.
        }
    }

    public void Update(Bus? bus, WeatherState? weather, bool paused)
    {
        if (bus is null || paused)
        {
            SetEngine(0f, -0.25f);
            SetRain(0f, paused);
            return;
        }

        var ratio = Math.Clamp(MathF.Abs(bus.Speed) / bus.MaxSpeed, 0f, 1f);
        var volume = 0.18f + ratio * 0.42f;
        var pitch = -0.3f + ratio * 0.65f;
        SetEngine(volume, pitch);

        if (weather is { } w)
        {
            var rainVolume = w.Kind switch
            {
                WeatherKind.Rain => 0.22f + 0.2f * w.Intensity,
                WeatherKind.Snow => 0.08f,
                WeatherKind.Fog => 0.05f,
                _ => 0f
            };
            SetRain(rainVolume, false);
        }
    }

    public void StopLoops()
    {
        SetEngine(0f, -0.25f);
        SetRain(0f, true);
    }

    private void SetEngine(float volume, float pitch)
    {
        if (_engine is null || Muted)
        {
            return;
        }

        try
        {
            _engine.Volume = Math.Clamp(volume, 0f, 1f);
            _engine.Pitch = Math.Clamp(pitch, -1f, 1f);
            if (volume > 0.001f)
            {
                if (!_engineRunning)
                {
                    _engine.Play();
                    _engineRunning = true;
                }
            }
            else if (_engineRunning)
            {
                _engine.Pause();
                _engineRunning = false;
            }
        }
        catch
        {
            // Ignore audio device teardown races.
        }
    }

    private void SetRain(float volume, bool forceStop)
    {
        if (_rain is null || Muted)
        {
            return;
        }

        try
        {
            _rain.Volume = Math.Clamp(volume, 0f, 1f);
            if (!forceStop && volume > 0.001f)
            {
                if (!_rainRunning)
                {
                    _rain.Play();
                    _rainRunning = true;
                }
            }
            else if (_rainRunning)
            {
                _rain.Pause();
                _rainRunning = false;
            }
        }
        catch
        {
            // Ignore audio device teardown races.
        }
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _rain?.Dispose();
        _engine = null;
        _rain = null;
    }
}
