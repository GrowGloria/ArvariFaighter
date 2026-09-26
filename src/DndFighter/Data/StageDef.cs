using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Graphics;

namespace DndFighter.Data;

/// <summary>Арена. Загружается из Content/stages/&lt;id&gt;/stage.json.</summary>
public sealed class StageDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Ширина арены в пикселях (экран — 480).</summary>
    public int Width { get; set; } = 640;
    /// <summary>Y пола на экране.</summary>
    public int FloorY { get; set; } = 244;
    public string? Background { get; set; }
    public string BackgroundColor { get; set; } = "#2a2a3a";

    [JsonIgnore] public Texture2D? BackgroundTexture { get; set; }
}
