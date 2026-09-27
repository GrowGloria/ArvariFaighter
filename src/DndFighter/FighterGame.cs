using DndFighter.Data;
using DndFighter.Engine;
using DndFighter.Input;
using DndFighter.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DndFighter;

public sealed class FighterGame : Game
{
    public const int VirtualWidth = 480;
    public const int VirtualHeight = 270;
    public const string Title = "БИТВА ГЕРОЕВ";

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _batch = null!;
    private RenderTarget2D _screen = null!;
    private Draw _draw = null!;
    private Scene _scene = null!;
    private Scene? _nextScene;

    public ContentLoader Loader { get; private set; } = null!;
    public ControlsConfig Controls { get; private set; } = null!;
    public MenuInput Menu { get; private set; } = null!;
    public string ContentRoot { get; } = Path.Combine(AppContext.BaseDirectory, "Content");

    // Отладочный снимок экрана (см. Program.cs).
    public string? ScreenshotPath { get; set; }
    public string? StartScene { get; set; }
    public int ScreenshotTick { get; set; } = 150;
    private int _ticks;

    public FighterGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = VirtualWidth * 3,
            PreferredBackBufferHeight = VirtualHeight * 3,
            SynchronizeWithVerticalRetrace = true,
        };
        // Логика файтинга должна идти строго 60 тиков в секунду независимо от FPS.
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        IsMouseVisible = false;
        Window.AllowUserResizing = true;
        Window.Title = Title;
    }

    protected override void Initialize()
    {
        base.Initialize();
        Controls = ControlsConfig.Load(Path.Combine(ContentRoot, "controls.json"));
        Menu = new MenuInput(Controls);
        Loader = new ContentLoader(GraphicsDevice, ContentRoot);
        Loader.LoadAll();
        ChangeScene(CreateStartScene());
    }

    private Scene CreateStartScene()
    {
        var roster = Loader.Characters;
        if (roster.Count == 0 || StartScene == null) return new TitleScene(this);
        var p2 = roster[Math.Min(1, roster.Count - 1)];
        return StartScene switch
        {
            "select" => new SelectScene(this, GameMode.Versus),
            "moves" => new MoveListScene(this, 0, new TitleScene(this)),
            "moves2" => new MoveListScene(this, 1, new TitleScene(this)),
            "fight" => new FightScene(this, GameMode.Versus, roster[0], p2, Loader.Stages[0]),
            "training" => new FightScene(this, GameMode.Training, roster[0], p2, Loader.Stages[0]),
            _ => new TitleScene(this),
        };
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _screen = new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        _draw = new Draw(GraphicsDevice, _batch);
    }

    private bool _nextSceneEnter = true;

    public void ChangeScene(Scene scene) => (_nextScene, _nextSceneEnter) = (scene, true);

    /// <summary>Вернуться на уже открытый экран, не запуская его заново (например, из списка приёмов в паузу боя).</summary>
    public void ResumeScene(Scene scene) => (_nextScene, _nextSceneEnter) = (scene, false);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SetWindowTextW(IntPtr hWnd, string text);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, System.Text.StringBuilder text, int maxCount);

    private bool _titleFixed;
    private int _titleAttempts;

    /// <summary>MonoGame (SDL) на Windows портит кириллицу в заголовке окна («?????»),
    /// поэтому после создания окна выставляем заголовок напрямую через Unicode-API Windows.</summary>
    private void FixWindowTitle()
    {
        _titleFixed = true;
        if (!OperatingSystem.IsWindows()) return;
        var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        if (hwnd == IntPtr.Zero) { _titleFixed = ++_titleAttempts > 120; return; }
        SetWindowTextW(hwnd, Title);
    }

    protected override void Update(GameTime gameTime)
    {
        if (!_titleFixed) FixWindowTitle();

        if (_nextScene != null)
        {
            _scene = _nextScene;
            _nextScene = null;
            if (_nextSceneEnter) _scene.Enter();
        }

        Menu.Update();
        if (Menu.KeyPressed(Keys.F11) || (Menu.KeyPressed(Keys.Enter) && Keyboard.GetState().IsKeyDown(Keys.LeftAlt)))
            _graphics.ToggleFullScreen();

        _scene.Update();
        base.Update(gameTime);
    }

    /// <summary>Отладка: папка для листов боксов (--boxsheet).</summary>
    public string? BoxSheetDir { get; set; }

    protected override void Draw(GameTime gameTime)
    {
        if (BoxSheetDir != null)
        {
            Diagnostics.BoxSheet.Save(GraphicsDevice, _batch, _draw, Loader, BoxSheetDir);
            BoxSheetDir = null;
            Exit();
            return;
        }

        GraphicsDevice.SetRenderTarget(_screen);
        GraphicsDevice.Clear(Color.Black);
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _scene?.Draw(_draw);
        _batch.End();

        if (ScreenshotPath != null && ++_ticks >= ScreenshotTick)
        {
            using (var fs = File.Create(ScreenshotPath)) _screen.SaveAsPng(fs, VirtualWidth, VirtualHeight);
            if (OperatingSystem.IsWindows())
            {
                // Для проверки: какой заголовок окна реально видит Windows.
                var buf = new System.Text.StringBuilder(256);
                GetWindowTextW(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle, buf, buf.Capacity);
                File.WriteAllText(ScreenshotPath + ".title.txt", buf.ToString());
            }
            ScreenshotPath = null;
            Exit();
        }

        // Масштабируем виртуальный экран целым множителем — пиксели остаются чёткими.
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        var bounds = GraphicsDevice.PresentationParameters.Bounds;
        int scale = Math.Max(1, Math.Min(bounds.Width / VirtualWidth, bounds.Height / VirtualHeight));
        int w = VirtualWidth * scale, h = VirtualHeight * scale;
        var dest = new Rectangle((bounds.Width - w) / 2, (bounds.Height - h) / 2, w, h);
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_screen, dest, Color.White);
        _batch.End();

        base.Draw(gameTime);
    }
}
