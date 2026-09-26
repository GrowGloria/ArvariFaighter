using DndFighter;
using DndFighter.Diagnostics;

if (args.Contains("--selftest"))
    return SelfTest.Run(Path.Combine(AppContext.BaseDirectory, "Content"));

using var game = new FighterGame();
// Отладка: DndFighter.exe --shot out.png --scene title|select|fight|training [--ticks 150]
int shot = Array.IndexOf(args, "--shot");
if (shot >= 0 && shot + 1 < args.Length)
{
    game.ScreenshotPath = args[shot + 1];
    int scene = Array.IndexOf(args, "--scene");
    if (scene >= 0 && scene + 1 < args.Length) game.StartScene = args[scene + 1];
    int ticks = Array.IndexOf(args, "--ticks");
    if (ticks >= 0 && ticks + 1 < args.Length) game.ScreenshotTick = int.Parse(args[ticks + 1]);
}
game.Run();
return 0;
