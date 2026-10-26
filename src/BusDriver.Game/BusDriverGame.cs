using BusDriver.Core;
using BusDriver.Game.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BusDriver.Game;

public enum GameScreen
{
    Splash,
    Title,
    Difficulty,
    Fleet,
    Routes,
    Help,
    Playing,
    Paused,
    Results,
    Error
}

/// <summary>
/// Bus Driver: the shared MonoGame application.  It renders at a fixed
/// 1280x720 virtual resolution, letterboxed onto whatever the phone provides,
/// and is driven entirely by touch (with keyboard shortcuts for development).
/// </summary>
public sealed class BusDriverGame : Microsoft.Xna.Framework.Game
{
    public const int VirtualWidth = 1280;
    public const int VirtualHeight = 720;

    private readonly GraphicsDeviceManager _graphics;
    private readonly InputState _input = new();
    private SpriteBatch _batch = null!;
    private Assets _assets = null!;
    private Ui _ui = null!;
    private WorldRenderer _world = null!;
    private GameAudio _audio = null!;
    private RenderTarget2D _scene = null!;
    private GameScreen _screen = GameScreen.Splash;

    private double _splashTimer;
    private double _resultDelay;
    private float _time;
    private float _messageTimer;
    private string _message = string.Empty;
    private Color _messageColour = Color.White;

    private DifficultyLevel _difficulty = DifficultyLevel.Medium;
    private BusType _busType = BusType.SingleDecker;
    private Route _route = RouteCatalog.Get("2");
    private BusDriverSession? _session;
    private bool _cruise;
    private Vector2 _camera;
    private GameScreen _helpReturn = GameScreen.Title;

    private readonly Rectangle _startButton = new(430, 330, 420, 92);
    private readonly Rectangle _routesButton = new(430, 440, 420, 78);
    private readonly Rectangle _helpButton = new(430, 536, 420, 78);
    private readonly Rectangle _pauseButton = new(12, 110, 62, 50);
    private readonly Rectangle _backButton = new(40, 632, 190, 60);

    private readonly Rectangle _steerLeft = new(92, 560, 104, 104);
    private readonly Rectangle _steerRight = new(212, 560, 104, 104);
    private readonly Rectangle _accelerator = new(1074, 574, 152, 100);
    private readonly Rectangle _brake = new(1074, 458, 152, 100);
    private readonly Rectangle _reverse = new(1074, 346, 152, 90);
    private readonly Rectangle _doors = new(476, 594, 328, 88);

    private readonly Rectangle _resumeButton = new(490, 292, 300, 72);
    private readonly Rectangle _restartButton = new(490, 382, 300, 72);
    private readonly Rectangle _quitButton = new(490, 472, 300, 72);

    private readonly Rectangle _againButton = new(214, 598, 250, 62);
    private readonly Rectangle _changeRouteButton = new(482, 598, 300, 62);
    private readonly Rectangle _menuButton = new(800, 598, 250, 62);

    public BusDriverGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = VirtualWidth,
            PreferredBackBufferHeight = VirtualHeight,
            SynchronizeWithVerticalRetrace = true,
            IsFullScreen = true,
            HardwareModeSwitch = false
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        InactiveSleepTime = TimeSpan.FromMilliseconds(20);
    }

    public IReadOnlyList<string> AssetErrors => _assets?.Errors ?? Array.Empty<string>();

    protected override void LoadContent()
    {
        try
        {
            StartupLog.Write("LoadContent begin");
            _batch = new SpriteBatch(GraphicsDevice);
            StartupLog.Write("SpriteBatch ready");
            _assets = new Assets(GraphicsDevice);
            _assets.LoadAll();
            StartupLog.Write(
                $"assets: {_assets.LoadedTextureCount} textures, " +
                $"{_assets.LoadedFontCount} fonts, {_assets.LoadedSoundCount} sounds");
            foreach (var error in _assets.Errors)
            {
                StartupLog.Write($"asset error: {error}");
            }

            _ui = new Ui(_assets, _batch);
            _world = new WorldRenderer(_assets, _ui);
            StartupLog.Write("renderers ready");
            _audio = new GameAudio(_assets);
            StartupLog.Write("audio ready");
            _scene = new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight);
            StartupLog.Write("render target ready");

            if (_assets.LoadedFontCount == 0 || _assets.LoadedTextureCount < 10)
            {
                _screen = GameScreen.Error;
            }

            // Device smoke-test hook: jump straight into a route when the
            // launch environment asks for it, so a physical iPhone can be
            // checked without a second person tapping the menu.
            if (Environment.GetEnvironmentVariable("BUSDRIVER_AUTOSTART") == "1")
            {
                var routeNumber = Environment.GetEnvironmentVariable("BUSDRIVER_ROUTE") ?? "2";
                try
                {
                    var levelName = Environment.GetEnvironmentVariable("BUSDRIVER_LEVEL");
                    if (!string.IsNullOrWhiteSpace(levelName) &&
                        Enum.TryParse<DifficultyLevel>(levelName, true, out var level))
                    {
                        _difficulty = level;
                    }

                    _route = RouteCatalog.Get(routeNumber);
                    StartSession();
                    StartupLog.Write($"autostart {_route.Number}");
                }
                catch (Exception ex)
                {
                    StartupLog.Exception("autostart failed", ex);
                }
            }

            // Capture hook: lets the store screenshots open any screen without
            // a finger on the glass.
            var screenName = Environment.GetEnvironmentVariable("BUSDRIVER_SCREEN");
            if (!string.IsNullOrWhiteSpace(screenName) &&
                Enum.TryParse<GameScreen>(screenName, true, out var requested))
            {
                _screen = requested;
            }
        }
        catch (Exception ex)
        {
            StartupLog.Exception("LoadContent failed", ex);
            throw;
        }
    }

    protected override void UnloadContent()
    {
        _audio?.Dispose();
        _scene?.Dispose();
        _assets?.Dispose();
        _batch?.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        var dt = (float)Math.Min(0.1, gameTime.ElapsedGameTime.TotalSeconds);
        _time += dt;
        _messageTimer = MathF.Max(0, _messageTimer - dt);

        var viewport = GraphicsDevice.Viewport;
        _input.SetTransform(viewport.Width, viewport.Height, VirtualWidth, VirtualHeight);
        _input.Update();

        switch (_screen)
        {
            case GameScreen.Splash:
                UpdateSplash(gameTime);
                break;
            case GameScreen.Title:
                UpdateTitle();
                break;
            case GameScreen.Difficulty:
                UpdateDifficulty();
                break;
            case GameScreen.Fleet:
                UpdateFleet();
                break;
            case GameScreen.Routes:
                UpdateRoutes();
                break;
            case GameScreen.Help:
                UpdateHelp();
                break;
            case GameScreen.Playing:
                UpdatePlaying(dt);
                break;
            case GameScreen.Paused:
                UpdatePaused();
                break;
            case GameScreen.Results:
                UpdateResults();
                break;
            case GameScreen.Error:
                UpdateError();
                break;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_scene);
        GraphicsDevice.Clear(new Color(10, 14, 20));

        switch (_screen)
        {
            case GameScreen.Splash:
                DrawSplash();
                break;
            case GameScreen.Title:
                DrawTitle();
                break;
            case GameScreen.Difficulty:
                DrawDifficulty();
                break;
            case GameScreen.Fleet:
                DrawFleet();
                break;
            case GameScreen.Routes:
                DrawRoutes();
                break;
            case GameScreen.Help:
                DrawHelp();
                break;
            case GameScreen.Playing:
            case GameScreen.Paused:
                DrawPlaying();
                break;
            case GameScreen.Results:
                DrawResults();
                break;
            case GameScreen.Error:
                DrawError();
                break;
        }

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        var viewport = GraphicsDevice.Viewport;
        var scale = MathF.Min(viewport.Width / (float)VirtualWidth, viewport.Height / (float)VirtualHeight);
        var width = (int)(VirtualWidth * scale);
        var height = (int)(VirtualHeight * scale);
        var destination = new Rectangle(
            (viewport.Width - width) / 2,
            (viewport.Height - height) / 2,
            width,
            height);

        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        _batch.Draw(_scene, destination, Color.White);
        _batch.End();

        base.Draw(gameTime);
    }

    // ------------------------------------------------------------------ flow
    private void UpdateSplash(GameTime gameTime)
    {
        _splashTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (_splashTimer > 1.9 || _input.AnyTap || _input.ConfirmPressed)
        {
            _splashTimer = 0;
            _screen = GameScreen.Title;
            _audio.Play("ui_confirm", 0.7f);
        }

        _audio.Update(null, null, true);
    }

    private void UpdateTitle()
    {
        _audio.Update(null, null, true);
        if (_input.WasTapped(_startButton) || _input.ConfirmPressed)
        {
            Click();
            _screen = GameScreen.Fleet;
        }
        else if (_input.WasTapped(_routesButton))
        {
            Click();
            _screen = GameScreen.Routes;
        }
        else if (_input.WasTapped(_helpButton))
        {
            Click();
            _helpReturn = GameScreen.Title;
            _screen = GameScreen.Help;
        }
    }

    private void UpdateDifficulty()
    {
        _audio.Update(null, null, true);
        if (TappedBack())
        {
            _screen = GameScreen.Fleet;
            return;
        }

        for (var i = 0; i < DifficultyProfile.All.Count; i++)
        {
            if (!_input.WasTapped(DifficultyCard(i)))
            {
                continue;
            }

            Click();
            _difficulty = DifficultyProfile.All[i].Level;
            _screen = GameScreen.Routes;
            return;
        }
    }

    private void UpdateFleet()
    {
        _audio.Update(null, null, true);
        if (TappedBack())
        {
            _screen = GameScreen.Title;
            return;
        }

        for (var i = 0; i < BusTypeProfile.All.Count; i++)
        {
            if (!_input.WasTapped(FleetCard(i)))
            {
                continue;
            }

            Click();
            _busType = BusTypeProfile.All[i].Type;
            _screen = GameScreen.Difficulty;
            return;
        }
    }

    private void UpdateRoutes()
    {
        _audio.Update(null, null, true);
        if (TappedBack())
        {
            _screen = GameScreen.Difficulty;
            return;
        }

        for (var i = 0; i < RouteCatalog.Routes.Count; i++)
        {
            if (!_input.WasTapped(RouteCard(i)))
            {
                continue;
            }

            Click();
            _route = RouteCatalog.Routes[i];
            StartSession();
            return;
        }
    }

    private void UpdateHelp()
    {
        _audio.Update(null, null, true);
        if (TappedBack() || _input.BackPressed)
        {
            _screen = _helpReturn;
        }
    }

    private void UpdatePlaying(float dt)
    {
        if (_session is null)
        {
            _screen = GameScreen.Title;
            return;
        }

        if (_input.BackPressed || _input.WasTapped(_pauseButton))
        {
            Click();
            _screen = GameScreen.Paused;
            _audio.Update(null, null, true);
            return;
        }

        var steer = 0f;
        if (_input.IsHeld(_steerLeft) || _input.KeyDown(Keys.A) || _input.KeyDown(Keys.Left))
        {
            steer -= 1f;
        }

        if (_input.IsHeld(_steerRight) || _input.KeyDown(Keys.D) || _input.KeyDown(Keys.Right))
        {
            steer += 1f;
        }

        var goHeld = _input.IsHeld(_accelerator) ||
                     _input.KeyDown(Keys.W) || _input.KeyDown(Keys.Up);
        var brake = _input.IsHeld(_brake) ||
                    _input.KeyDown(Keys.S) || _input.KeyDown(Keys.Down);

        // GO is a latch: one tap sets the bus running, and the driver keeps
        // going until the brake or a steering button cancels it.
        if (_input.WasTapped(_accelerator) || _input.KeyPressed(Keys.W))
        {
            _cruise = true;
        }

        if (brake || steer != 0f || _input.IsHeld(_reverse))
        {
            _cruise = false;
        }

        var accelerate = goHeld || _cruise;

        // BRAKE always holds the bus still; reverse is its own control so a bus
        // waiting at a stop does not creep backwards.
        var reverse = _input.IsHeld(_reverse) && _session.Bus.Speed <= 6f;
        DriveInput drive;
        if (reverse)
        {
            drive = new DriveInput(-0.55f, steer, false);
        }
        else
        {
            drive = new DriveInput(accelerate ? 1f : 0f, steer, brake);
        }

        var doors = _input.WasTapped(_doors) || _input.KeyPressed(Keys.Space);
        _session.Update(dt, drive, doors);
        HandleEvents(_session.DrainEvents());

        var lookAhead = new Vector2(
            MathF.Cos(_session.Bus.Heading),
            MathF.Sin(_session.Bus.Heading)) *
            (70f * Math.Clamp(MathF.Abs(_session.Bus.Speed) / _session.Bus.MaxSpeed, 0f, 1f));
        var target = ToXna(_session.Bus.Position) + lookAhead;
        target.X = Math.Clamp(target.X, VirtualWidth / 2f, TownMap.WorldWidth - VirtualWidth / 2f);
        target.Y = Math.Clamp(target.Y, VirtualHeight / 2f, TownMap.WorldHeight - VirtualHeight / 2f);
        _camera = Vector2.Lerp(_camera, target, 1f - MathF.Exp(-7f * dt));

        _audio.Update(_session.Bus, _session.Weather.Current, false);

        if (_session.IsFinished)
        {
            _resultDelay += dt;
            if (_resultDelay > 1.35)
            {
                _resultDelay = 0;
                _screen = GameScreen.Results;
                _audio.StopLoops();
            }
        }
    }

    private void UpdatePaused()
    {
        _audio.Update(null, null, true);
        if (_input.BackPressed || _input.WasTapped(_resumeButton))
        {
            Click();
            _screen = GameScreen.Playing;
        }
        else if (_input.WasTapped(_restartButton))
        {
            Click();
            StartSession();
        }
        else if (_input.WasTapped(_quitButton))
        {
            Click();
            _session = null;
            _audio.StopLoops();
            _screen = GameScreen.Routes;
        }
    }

    private void UpdateResults()
    {
        _audio.Update(null, null, true);
        if (_input.WasTapped(_againButton))
        {
            Click();
            StartSession();
        }
        else if (_input.WasTapped(_changeRouteButton))
        {
            Click();
            _session = null;
            _screen = GameScreen.Routes;
        }
        else if (_input.WasTapped(_menuButton))
        {
            Click();
            _session = null;
            _screen = GameScreen.Title;
        }
        else if (_input.BackPressed)
        {
            _session = null;
            _screen = GameScreen.Title;
        }
    }

    private void UpdateError()
    {
        if (_input.AnyTap || _input.ConfirmPressed)
        {
            _screen = GameScreen.Title;
        }
    }

    private void StartSession()
    {
        _session = new BusDriverSession(
            DifficultyProfile.For(_difficulty),
            _route,
            (int)(DateTime.UtcNow.Ticks % int.MaxValue),
            _busType);
        _camera = ToXna(_session.Bus.Position);
        _screen = GameScreen.Playing;
        // The gameplay capture run pulls away on its own.
        _cruise = Environment.GetEnvironmentVariable("BUSDRIVER_CRUISE") == "1";
        _resultDelay = 0;
        _message = $"{_route.Number} {_route.Name} - shift started";
        _messageColour = new Color(255, 232, 168);
        _messageTimer = 3f;
        _audio.Play("horn", 0.35f);
    }

    private void Click() => _audio.Play("ui_click", 0.55f);

    private bool TappedBack()
    {
        if (!_input.WasTapped(_backButton))
        {
            return false;
        }

        Click();
        return true;
    }

    private void HandleEvents(IReadOnlyList<SessionEvent> events)
    {
        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case SessionEventKind.DoorsOpened:
                    _audio.Play("door_open", 0.7f);
                    break;
                case SessionEventKind.DoorsRefused:
                    _audio.Play("buzzer", 0.25f);
                    _message = e.Message;
                    _messageColour = new Color(255, 174, 116);
                    _messageTimer = 2.2f;
                    break;
                case SessionEventKind.StopServed:
                    _audio.Play("bell", 0.55f);
                    _message = e.Message;
                    _messageColour = new Color(255, 236, 190);
                    _messageTimer = 3.2f;
                    break;
                case SessionEventKind.OnTime:
                    _audio.Play("chime", 0.45f);
                    _message = e.Message;
                    _messageColour = new Color(132, 234, 154);
                    _messageTimer = 2.6f;
                    break;
                case SessionEventKind.Late:
                    _audio.Play("buzzer", 0.45f);
                    _message = e.Message;
                    _messageColour = new Color(255, 146, 116);
                    _messageTimer = 2.6f;
                    break;
                case SessionEventKind.TripComplete:
                    _audio.Play("ui_confirm", 0.7f);
                    _message = e.Message;
                    _messageColour = new Color(255, 218, 92);
                    _messageTimer = 3.2f;
                    break;
                case SessionEventKind.Crash:
                    _audio.Play("crash", 0.8f);
                    _message = e.Message;
                    _messageColour = new Color(255, 130, 100);
                    _messageTimer = 2.2f;
                    break;
                case SessionEventKind.Recovered:
                    _message = e.Message;
                    _messageColour = new Color(190, 210, 236);
                    _messageTimer = 2.2f;
                    break;
                case SessionEventKind.Layover:
                    _message = e.Message + " - the next trip is the return";
                    _messageColour = new Color(188, 210, 230);
                    _messageTimer = 3f;
                    break;
                case SessionEventKind.ShiftEnd:
                    _audio.Play("fanfare", 0.75f);
                    _audio.StopLoops();
                    break;
            }
        }
    }

    // ------------------------------------------------------------------ drawing
    private void DrawSplash()
    {
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(_assets.Tex("splash"),
            new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.White);
        _batch.End();
    }

    private void DrawTitle()
    {
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(_assets.Tex("title_bg"),
            new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.White);
        _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(8, 12, 18) * 0.28f);

        var logo = _assets.Tex("logo");
        _batch.Draw(logo, new Rectangle(188, 12, 904, 284), Color.White);

        _ui.Button(_startButton, "START A SHIFT", highlighted: true, font: "medium");
        _ui.Button(_routesButton, "ROUTE MAP");
        _ui.Button(_helpButton, "HOW TO DRIVE");

        _ui.TextCentred("small",
            $"{BusTypeProfile.For(_busType).Name.ToUpperInvariant()}  •  " +
            $"{DifficultyProfile.For(_difficulty).Name.ToUpperInvariant()}  •  16 ROUTES",
            640, 628, new Color(238, 198, 70));
        _ui.TextCentred("tiny",
            "SCARTHORPE TRANSPORT  •  1985  •  16 ROUTES  •  NO TICKETS, JUST TIMETABLES",
            640, 678, new Color(156, 166, 174));
        _batch.End();
    }

    private void DrawDifficulty()
    {
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(_assets.Tex("title_bg"),
            new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.White);
        _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(6, 10, 16) * 0.76f);

        _ui.TextCentred("large", "CHOOSE YOUR SHIFT", 640, 34, new Color(246, 236, 208));
        _ui.TextCentred("small", "Every level changes the timetable, the traffic and the weather.",
            640, 104, new Color(168, 182, 194));

        for (var i = 0; i < DifficultyProfile.All.Count; i++)
        {
            var profile = DifficultyProfile.All[i];
            var rect = DifficultyCard(i);
            var tint = profile.Level == _difficulty
                ? new Color(70, 82, 96)
                : new Color(20, 27, 36);
            _ui.Panel(rect, tint);
            _ui.Fill(new Rectangle(rect.X + 8, rect.Y + 10, 10, rect.Height - 20),
                RouteColour(profile.Level));

            _ui.Text("medium", profile.Name, new Vector2(rect.X + 38, rect.Y + 18),
                new Color(246, 236, 208));
            _ui.Text("small", profile.Blurb, new Vector2(rect.X + 38, rect.Y + 70),
                new Color(172, 184, 196), 0.78f);
            _ui.Text("tiny",
                $"{profile.ShiftMinutes} min shift   •   ±{profile.ScheduleToleranceSeconds}s   •   " +
                $"{profile.TrafficVehicles} vehicles",
                new Vector2(rect.Right - 24, rect.Y + 22),
                new Color(238, 198, 70), 0.95f, TextAlign.Right);
            _ui.Text("tiny",
                $"passenger demand {profile.PassengerBase}-{profile.PassengerBase + profile.PassengerSpread}   •   " +
                $"{BusTypeProfile.For(_busType).Capacity} seats on the {BusTypeProfile.For(_busType).Name}",
                new Vector2(rect.Right - 24, rect.Y + 52),
                new Color(150, 164, 176), 0.9f, TextAlign.Right);
        }

        _ui.Button(_backButton, "BACK", font: "small");
        _batch.End();
    }

    private void DrawFleet()
    {
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(_assets.Tex("title_bg"),
            new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.White);
        _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(6, 10, 16) * 0.78f);

        _ui.TextCentred("large", "CHOOSE YOUR BUS", 640, 34, new Color(246, 236, 208));
        _ui.TextCentred("small", "The depot has three vehicles.  Bigger buses earn more fares, but they are slower and harder to place.",
            640, 104, new Color(168, 182, 194));

        for (var i = 0; i < BusTypeProfile.All.Count; i++)
        {
            var bus = BusTypeProfile.All[i];
            var rect = FleetCard(i);
            var tint = bus.Type == _busType ? new Color(70, 82, 96) : new Color(20, 27, 36);
            _ui.Panel(rect, tint);
            _ui.Fill(new Rectangle(rect.X + 8, rect.Y + 10, 10, rect.Height - 20),
                BusColour(bus.Type));

            _ui.Text("medium", bus.Name, new Vector2(rect.X + 38, rect.Y + 16),
                new Color(246, 236, 208));
            _ui.Text("small", bus.Description, new Vector2(rect.X + 38, rect.Y + 70),
                new Color(172, 184, 196), 0.62f);
            _ui.Text("medium", $"{bus.Capacity} seats", new Vector2(rect.Right - 30, rect.Y + 18),
                new Color(238, 198, 70), 0.9f, TextAlign.Right);
            _ui.Text("tiny", $"{bus.Length:0}px body   •   {bus.MaxSpeed:0} px/s   •   steering {bus.Steering:0.00}",
                new Vector2(rect.Right - 30, rect.Y + 56), new Color(150, 164, 176),
                0.9f, TextAlign.Right);
        }

        _ui.Button(_backButton, "BACK", font: "small");
        _batch.End();
    }

    private void DrawRoutes()
    {
        var profile = DifficultyProfile.For(_difficulty);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(_assets.Tex("title_bg"),
            new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.White);
        _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(6, 10, 16) * 0.82f);

        _ui.TextCentred("large", "SELECT A ROUTE", 640, 26, new Color(246, 236, 208));
        _ui.TextCentred("small",
            $"{BusTypeProfile.For(_busType).Name.ToUpperInvariant()}  •  " +
            $"{profile.Name.ToUpperInvariant()} SHIFT  •  {profile.ShiftMinutes} MINUTES  •  " +
            "FOUR HERITAGE RUNS + TWELVE NEW SERVICES",
            640, 96, new Color(238, 198, 70), 0.8f);

        for (var i = 0; i < RouteCatalog.Routes.Count; i++)
        {
            var route = RouteCatalog.Routes[i];
            var rect = RouteCard(i);
            _ui.Panel(rect, new Color(18, 24, 32));
            _ui.Fill(new Rectangle(rect.X + 6, rect.Y + 8, 9, rect.Height - 16), Ui.FromArgb(route.Colour));

            _ui.Text("medium", route.Number,
                new Vector2(rect.X + 24, rect.Y + 26), Ui.FromArgb(route.Colour));
            _ui.Text("small", route.Name,
                new Vector2(rect.X + 86, rect.Y + 20), new Color(242, 236, 218), 0.66f);
            _ui.Text("tiny",
                $"{route.Stops.Count} stops  •  {route.FirstStop.Name}",
                new Vector2(rect.X + 86, rect.Y + 62), new Color(150, 164, 176), 0.58f);
            if (route.Heritage)
            {
                _ui.Text("tiny", "HERITAGE", new Vector2(rect.Right - 14, rect.Y + 12),
                    new Color(238, 198, 70), 0.85f, TextAlign.Right);
            }
        }

        _ui.Button(_backButton, "BACK", font: "small");
        _ui.Text("tiny", "Tap a route to clock on.  Every route ends at a terminus where everyone alights.",
            new Vector2(1204, 662), new Color(150, 164, 176), 0.9f, TextAlign.Right);
        _batch.End();
    }

    private void DrawHelp()
    {
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(_assets.Tex("title_bg"),
            new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.White);
        _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(6, 10, 16) * 0.82f);

        var panel = new Rectangle(110, 24, 1060, 660);
        _ui.Panel(panel, new Color(16, 22, 30));
        _ui.TextCentred("large", "HOW TO DRIVE", 640, 40, new Color(246, 236, 208));

        var text =
            "THE CONTROLS\n" +
            "Tap GO to pull away - the bus keeps going until you BRAKE or steer. Hold GO for more power.\n" +
            "BRAKE slows and holds the bus, REV backs up, ◀ and ▶ steer, DOORS opens at the next stop.\n" +
            "The bus only turns while moving, and bad weather makes it slower to stop.\n\n" +
            "THE TIMETABLE\n" +
            "Each stop has a due time at the top of the screen. Arrive within tolerance for green points.\n" +
            "Late arrivals cost points, so leave the terminus on time and keep moving.\n\n" +
            "PASSENGERS\n" +
            "At a normal stop people get off and on at the same time. Watch the queues and the free seats.\n" +
            "The minibus seats 16, the single-decker 32 and the double-decker 56; choose on the depot screen.\n\n" +
            "TERMINUSES\n" +
            "The old goods depots are now terminuses. Everyone alights there and NOBODY boards.\n" +
            "After a short layover the bus turns round for the return trip.\n\n" +
            "TRAFFIC AND WEATHER\n" +
            "Cars, taxis, vans and lorries use the left-hand lanes. Cutting them up dents the bus.\n" +
            "Buses belong on the tarmac: mounting the pavement is a crash, and so is a wall.\n" +
            "Rain, fog and snow reduce grip and visibility; the engine note and windscreen tell you what is coming.\n\n" +
            "SCORING\n" +
            "Fares + punctuality + trip bonuses, less a penalty for every percent of damage.\n" +
            "The shift ends when the clock runs out or the bus is written off.";

        _ui.TextBlock("small", text, new Vector2(panel.X + 48, panel.Y + 112),
            new Color(206, 214, 222), 0.56f, 1.15f);

        _ui.Button(_backButton, "BACK", font: "small");
        _batch.End();
    }

    private void DrawPlaying()
    {
        if (_session is null)
        {
            return;
        }

        var transform = Matrix.CreateTranslation(
            -_camera.X + VirtualWidth / 2f,
            -_camera.Y + VirtualHeight / 2f,
            0f);

        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, transform);
        _world.Draw(_batch, _session, _camera, _time);
        _batch.End();

        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        DrawWeatherOverlay();
        DrawHud();

        if (_messageTimer > 0)
        {
            var width = MathF.Max(320, _ui.TextWidth("body", _message, 0.8f) + 60);
            var box = new Rectangle((int)(640 - width / 2), 112, (int)width, 50);
            _ui.Panel(box, new Color(18, 24, 30) * 0.86f);
            _ui.TextCentred("body", _message, 640, 122, _messageColour, 0.8f);
        }

        if (_screen == GameScreen.Paused)
        {
            DrawPausedOverlay();
        }

        _batch.End();
    }

    private void DrawWeatherOverlay()
    {
        var weather = _session!.Weather.Current;
        if (_session.Route.IsNight)
        {
            _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight),
                new Color(8, 12, 40) * 0.30f);
        }

        switch (weather.Kind)
        {
            case WeatherKind.Rain:
                _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight),
                    new Color(28, 42, 56) * 0.18f);
                for (var i = 0; i < 150; i++)
                {
                    var x = (i * 97 + _time * 560f) % (VirtualWidth + 80) - 40;
                    var y = (i * 53 + _time * 820f + i * i * 0.7f) % (VirtualHeight + 40) - 20;
                    _ui.Line(new Vector2(x, y), new Vector2(x - 9, y + 30),
                        new Color(168, 202, 224) * 0.52f, 2.4f);
                }

                break;
            case WeatherKind.Snow:
                _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight),
                    new Color(220, 232, 242) * 0.10f);
                for (var i = 0; i < 130; i++)
                {
                    var x = (i * 61 + MathF.Sin(_time * 0.6f + i) * 28 + _time * 34f) %
                            (VirtualWidth + 20) - 10;
                    var y = (i * 89 + _time * 48f) % (VirtualHeight + 20) - 10;
                    var size = 3 + i % 4;
                    _ui.Fill(new Rectangle((int)x, (int)y, size, size),
                        new Color(248, 252, 255) * 0.75f);
                }

                break;
            case WeatherKind.Fog:
                _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight),
                    new Color(184, 194, 202) * ((1f - weather.Visibility) * 0.62f));
                break;
        }
    }

    private void DrawHud()
    {
        var session = _session!;
        var routeColour = Ui.FromArgb(session.Route.Colour);

        _ui.Fill(new Rectangle(0, 0, VirtualWidth, 100), new Color(10, 14, 20) * 0.88f);
        _ui.Fill(new Rectangle(0, 98, VirtualWidth, 3), new Color(238, 198, 70) * 0.7f);

        var badge = new Rectangle(14, 10, 92, 78);
        _ui.Fill(badge, routeColour);
        _ui.TextCentred("medium", session.Route.Number, badge.Center.X, 16, Color.White);
        _ui.TextCentred("tiny", "ROUTE", badge.Center.X, 66, new Color(255, 255, 255) * 0.8f);

        _ui.Text("body", session.Route.Name, new Vector2(120, 4), new Color(246, 236, 208), 0.9f);
        _ui.Text("tiny", "NEXT STOP", new Vector2(120, 44), new Color(150, 164, 176));
        _ui.Text("medium", session.NextStop?.Name ?? (session.IsAwaitingLayover
                ? $"LAYOVER at {session.Route.LastStop.Name}"
                : "SHIFT OVER"),
            new Vector2(120, 56), new Color(255, 244, 208), 0.85f);

        var due = session.NextScheduled;
        if (due is not null)
        {
            var delay = session.DelaySeconds;
            var tolerance = session.Difficulty.ScheduleToleranceSeconds;
            var colour = delay <= tolerance
                ? new Color(140, 236, 158)
                : new Color(255, 140, 110);
            var delayText = delay >= 0 ? $"{delay:0}s late" : $"{-delay:0}s early";
            _ui.Text("small", $"DUE {due.Due:hh\\:mm\\:ss}   {delayText}",
                new Vector2(620, 8), colour, 0.82f);
        }

        _ui.Text("small", $"SCORE {session.Score.Total}", new Vector2(620, 42),
            new Color(238, 198, 70), 0.82f);

        _ui.Text("small", $"SEATS {session.Bus.PassengerCount}/{session.Bus.Capacity}",
            new Vector2(620, 72), new Color(206, 214, 222), 0.7f);

        var damageWidth = 170;
        _ui.Text("tiny", "DAMAGE", new Vector2(788, 6), new Color(150, 164, 176));
        _ui.Fill(new Rectangle(788, 26, damageWidth, 15), new Color(42, 48, 56));
        _ui.Fill(new Rectangle(788, 26, (int)(damageWidth * session.Bus.Damage / 100f), 15),
            Color.Lerp(new Color(120, 220, 140), new Color(230, 70, 50), session.Bus.Damage / 100f));
        _ui.Text("tiny", $"{session.Bus.SpeedMph:0} mph", new Vector2(788, 52),
            new Color(206, 214, 222));

        _ui.Fill(new Rectangle(788, 76, damageWidth, 7), new Color(42, 48, 56));
        _ui.Fill(new Rectangle(788, 76,
                (int)(damageWidth * Math.Clamp(session.Bus.SpeedMph / 32f, 0f, 1f)), 7),
            new Color(120, 190, 236));

        var weather = session.Weather.Current;
        var weatherTexture = _assets.Tex($"weather_{weather.Kind.ToString().ToLowerInvariant()}");
        _batch.Draw(weatherTexture, new Rectangle(1006, 6, 46, 46), Color.White);
        _ui.Text("small", weather.Label, new Vector2(1058, 8), new Color(206, 214, 222), 0.75f);
        _ui.Text("tiny", $"GRIP {weather.Grip:0.00}", new Vector2(1058, 38),
            new Color(150, 164, 176));

        _ui.Text("medium", session.Now.ToString(@"hh\:mm"), new Vector2(1264, 2),
            new Color(246, 236, 208), 0.8f, TextAlign.Right);
        _ui.Text("tiny", $"SHIFT LEFT {session.ShiftRemaining.Minutes:00}:{session.ShiftRemaining.Seconds:00}",
            new Vector2(1264, 48), new Color(150, 164, 176), 0.95f, TextAlign.Right);
        _ui.Text("tiny", $"FARES {session.Score.FaresText}", new Vector2(1264, 70),
            new Color(238, 198, 70), 0.95f, TextAlign.Right);

        DrawMinimap();
        DrawTurnHint(session);
        _ui.Button(_pauseButton, "II", highlighted: false, font: "small");

        _ui.TextCentred("small", session.StatusText, 640, 524, new Color(246, 236, 208), 0.9f);

        if (session.CanOpenDoors)
        {
            _ui.Button(_doors, "DOORS  ▲", highlighted: true, font: "medium");
        }
        else
        {
            _ui.Button(_doors, "DOORS", pressed: true, font: "medium",
                textColour: new Color(120, 118, 108));
        }

        _ui.Button(_steerLeft, "◀", font: "medium");
        _ui.Button(_steerRight, "▶", font: "medium");
        _ui.Button(_accelerator, _cruise ? "GO ▲" : "GO", highlighted: true, pressed: _cruise,
            font: "medium");
        _ui.Button(_brake, "BRAKE", font: "small");
        _ui.Button(_reverse, "REV", font: "small", highlighted: false,
            textColour: session.Bus.Speed <= 6f ? null : new Color(120, 118, 108));
    }

    /// <summary>
    /// The turn prompt.  Easy announces every junction; Medium only the ones
    /// where the route actually turns; Hard leaves it to the route map.
    /// </summary>
    private void DrawTurnHint(BusDriverSession session)
    {
        var guidance = session.Difficulty.Guidance;
        var stop = session.NextStop;
        if (guidance == GuidanceLevel.None || stop is null)
        {
            return;
        }

        var hint = RouteGuidance.Hint(session.Bus.Position, session.Bus.Heading, stop.Position);
        if (guidance == GuidanceLevel.TurnsOnly && hint.Direction == TurnDirection.StraightOn)
        {
            return;
        }

        var (arrow, label) = hint.Direction switch
        {
            TurnDirection.Left => ("◀", "TURN LEFT"),
            TurnDirection.Right => ("▶", "TURN RIGHT"),
            TurnDirection.TurnAround => ("▼", "TURN AROUND"),
            _ => ("▲", "STRAIGHT ON")
        };

        var box = new Rectangle(408, 172, 464, 96);
        _ui.Panel(box, new Color(14, 20, 28) * 0.88f);
        _ui.Fill(new Rectangle(box.X + 6, box.Y + 8, 8, box.Height - 16),
            new Color(238, 198, 70));

        _ui.Text("large", arrow, new Vector2(box.X + 34, box.Y + 16), new Color(255, 218, 92));
        _ui.Text("medium", label, new Vector2(box.X + 104, box.Y + 14), new Color(246, 236, 208));
        _ui.Text("small", $"in {(int)MathF.Round(hint.MetresToJunction)} m",
            new Vector2(box.X + 104, box.Y + 52), new Color(168, 182, 194));
        _ui.Text("tiny", stop.Name, new Vector2(box.Right - 20, box.Y + 58),
            new Color(238, 198, 70), 0.9f, TextAlign.Right);
    }

    private void DrawMinimap()
    {
        var session = _session!;
        var rect = new Rectangle(1000, 108, 264, 160);
        _ui.Panel(rect, new Color(12, 18, 26) * 0.82f);

        const float mapScale = 0.0338f;
        var mapWidth = TownMap.WorldWidth * mapScale;
        var mapHeight = TownMap.WorldHeight * mapScale;
        var origin = new Vector2(
            rect.X + (rect.Width - mapWidth) / 2f,
            rect.Y + (rect.Height - mapHeight) / 2f);

        Vector2 Map(Vector2 world) => origin + world * mapScale;

        foreach (var x in TownMap.RoadCentresX)
        {
            _ui.Line(Map(new Vector2(x, 0)), Map(new Vector2(x, TownMap.WorldHeight)),
                new Color(100, 112, 124) * 0.7f, 1.2f);
        }

        foreach (var y in TownMap.RoadCentresY)
        {
            _ui.Line(Map(new Vector2(0, y)), Map(new Vector2(TownMap.WorldWidth, y)),
                new Color(100, 112, 124) * 0.7f, 1.2f);
        }

        foreach (var stop in session.Route.Stops)
        {
            var point = Map(ToXna(stop.Position));
            var colour = stop.Id == session.NextStop?.Id
                ? new Color(255, 218, 92)
                : Ui.FromArgb(session.Route.Colour);
            var size = stop.Id == session.NextStop?.Id ? 8 : 5;
            _ui.Fill(new Rectangle((int)point.X - size / 2, (int)point.Y - size / 2, size, size),
                colour);
        }

        var busPoint = Map(ToXna(session.Bus.Position));
        _ui.Fill(new Rectangle((int)busPoint.X - 4, (int)busPoint.Y - 4, 8, 8),
            new Color(255, 248, 230));
    }

    private void DrawPausedOverlay()
    {
        _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(0, 0, 0) * 0.68f);
        _ui.Panel(new Rectangle(350, 112, 580, 462), new Color(18, 24, 32));
        _ui.TextCentred("large", "PAUSED", 640, 146, new Color(246, 236, 208));
        _ui.TextCentred("small", _session is null ? string.Empty : $"{_session.Route.Number}  {_session.Route.Name}",
            640, 230, new Color(238, 198, 70));
        _ui.Button(_resumeButton, "RESUME", highlighted: true);
        _ui.Button(_restartButton, "RESTART SHIFT");
        _ui.Button(_quitButton, "QUIT TO ROUTES");
    }

    private void DrawResults()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _batch.Draw(_assets.Tex("title_bg"),
            new Rectangle(0, 0, VirtualWidth, VirtualHeight), Color.White);
        _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(6, 10, 16) * 0.84f);
        _ui.Panel(new Rectangle(190, 24, 900, 664), new Color(18, 24, 32));

        _ui.TextCentred("large", "SHIFT OVER", 640, 40, new Color(246, 236, 208));
        _ui.TextCentred("small",
            $"{session.Route.Number}  {session.Route.Name}  •  " +
            $"{session.Bus.Profile.Name.ToUpperInvariant()}  •  " +
            $"{session.Difficulty.Name.ToUpperInvariant()}  •  {session.FinishReason}",
            640, 122, new Color(238, 198, 70));

        _ui.TextCentred("huge", $"{session.Score.Total}", 640, 150, new Color(255, 218, 92));

        var left = 250;
        var right = 1030;
        var y = 312;
        var lines = new (string Label, string Value, Color Colour)[]
        {
            ("Fares collected", session.Score.FaresText, new Color(206, 214, 222)),
            ("Punctuality", session.Score.PunctualityPoints.ToString("+0;-0;0"), new Color(206, 214, 222)),
            ("Trip bonuses", $"+{session.Score.TripBonus}", new Color(206, 214, 222)),
            ("Damage penalty", $"-{session.Score.DamagePenalty}", new Color(255, 146, 116)),
            ("Passengers carried / delivered",
                $"{session.Score.PassengersCarried} / {session.Score.PassengersDelivered}",
                new Color(206, 214, 222)),
            ("On time / late stops",
                $"{session.Score.OnTimeStops} / {session.Score.LateStops}",
                new Color(206, 214, 222)),
            ("Trips completed", session.Score.TripsCompleted.ToString(), new Color(206, 214, 222)),
        };

        foreach (var (label, value, colour) in lines)
        {
            _ui.Text("body", label, new Vector2(left, y), new Color(154, 168, 180), 0.82f);
            _ui.Text("body", value, new Vector2(right, y), colour, 0.82f, TextAlign.Right);
            y += 38;
        }

        _ui.Fill(new Rectangle(left, y + 4, right - left, 2), new Color(120, 132, 144) * 0.5f);
        _ui.Text("body", "TOTAL SCORE", new Vector2(left, y + 18), new Color(238, 198, 70), 0.9f);
        _ui.Text("body", session.Score.Total.ToString(), new Vector2(right, y + 18),
            new Color(255, 218, 92), 0.9f, TextAlign.Right);

        _ui.Button(_againButton, "DRIVE AGAIN", highlighted: true, font: "small");
        _ui.Button(_changeRouteButton, "CHANGE ROUTE", font: "small");
        _ui.Button(_menuButton, "MAIN MENU", font: "small");
        _batch.End();
    }

    private void DrawError()
    {
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _ui.Fill(new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(12, 16, 22));
        _ui.TextCentred("large", "CONTENT ERROR", 640, 70, new Color(255, 146, 116));
        _ui.TextCentred("small", "One or more assets could not be loaded.", 640, 160,
            new Color(206, 214, 222));
        var y = 220f;
        foreach (var error in _assets.Errors.Take(14))
        {
            _ui.Text("small", error, new Vector2(140, y), new Color(255, 190, 150), 0.8f);
            y += 32;
        }

        _ui.TextCentred("small", "Tap to continue anyway.", 640, 650, new Color(238, 198, 70));
        _batch.End();
    }

    // ------------------------------------------------------------------ layout
    private static Rectangle DifficultyCard(int index) =>
        new(190, 142 + index * 158, 900, 132);

    private static Rectangle FleetCard(int index) =>
        new(190, 150 + index * 150, 900, 126);

    private static Rectangle RouteCard(int index)
    {
        const int width = 282;
        const int height = 110;
        const int gapX = 16;
        const int gapY = 12;
        const int x0 = 52;
        const int y0 = 120;
        var column = index % 4;
        var row = index / 4;
        return new Rectangle(
            x0 + column * (width + gapX),
            y0 + row * (height + gapY),
            width,
            height);
    }

    private static Vector2 ToXna(System.Numerics.Vector2 value) => new(value.X, value.Y);

    private static Color RouteColour(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => new Color(108, 198, 132),
        DifficultyLevel.Medium => new Color(238, 198, 70),
        _ => new Color(232, 104, 84)
    };

    private static Color BusColour(BusType type) => type switch
    {
        BusType.Minibus => new Color(108, 198, 132),
        BusType.SingleDecker => new Color(238, 198, 70),
        _ => new Color(216, 68, 58)
    };
}
