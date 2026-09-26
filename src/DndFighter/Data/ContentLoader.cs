using System.Text.Json;
using System.Text.Json.Serialization;
using DndFighter.Combat.Behaviors;
using DndFighter.Combat.Effects;
using DndFighter.Engine;
using DndFighter.Input;
using Microsoft.Xna.Framework.Graphics;

namespace DndFighter.Data;

/// <summary>
/// Находит и загружает всех персонажей и арены из папки Content.
/// Чтобы добавить персонажа, достаточно создать новую папку в Content/characters.
/// </summary>
public sealed class ContentLoader
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>null — режим без графики (самопроверка): текстуры не загружаются.</summary>
    private readonly GraphicsDevice? _device;

    public string Root { get; }
    public List<CharacterDef> Characters { get; } = new();
    public List<StageDef> Stages { get; } = new();
    /// <summary>Ошибки загрузки — показываются на титульном экране, чтобы сразу видеть опечатки в JSON.</summary>
    public List<string> Errors { get; } = new();

    public ContentLoader(GraphicsDevice? device, string root)
    {
        _device = device;
        Root = root;
    }

    public void LoadAll()
    {
        Characters.Clear();
        Stages.Clear();
        Errors.Clear();

        foreach (var dir in SubDirs(Path.Combine(Root, "characters")))
        {
            try { Characters.Add(LoadCharacter(dir)); }
            catch (Exception e) { Errors.Add($"{Path.GetFileName(dir)}: {Short(e)}"); }
        }

        foreach (var dir in SubDirs(Path.Combine(Root, "stages")))
        {
            try { Stages.Add(LoadStage(dir)); }
            catch (Exception e) { Errors.Add($"STAGE {Path.GetFileName(dir)}: {Short(e)}"); }
        }

        if (Stages.Count == 0) Stages.Add(new StageDef { Id = "empty", Name = "Пустота" });
    }

    public CharacterDef LoadCharacter(string dir)
    {
        var def = ReadJson<CharacterDef>(Path.Combine(dir, "character.json"));
        def.Directory = dir;
        if (string.IsNullOrEmpty(def.Id)) def.Id = Path.GetFileName(dir);
        if (string.IsNullOrEmpty(def.Name)) def.Name = def.Id;

        // Приёмы: moves.json (массив) + любые *.json в папке moves/ (объект или массив).
        var moveFiles = new List<string>();
        var single = Path.Combine(dir, "moves.json");
        if (File.Exists(single)) moveFiles.Add(single);
        var movesDir = Path.Combine(dir, "moves");
        if (Directory.Exists(movesDir))
            moveFiles.AddRange(Directory.GetFiles(movesDir, "*.json").OrderBy(f => f));

        foreach (var file in moveFiles)
        {
            var text = File.ReadAllText(file);
            try
            {
                using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
                    { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    def.Moves.AddRange(JsonSerializer.Deserialize<List<MoveDef>>(text, JsonOptions) ?? new());
                else
                    def.Moves.Add(JsonSerializer.Deserialize<MoveDef>(text, JsonOptions) ?? new());
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"{Path.GetFileName(file)}: {e.Message}");
            }
        }

        for (int i = 0; i < def.Moves.Count; i++)
        {
            var m = def.Moves[i];
            if (string.IsNullOrEmpty(m.Id)) m.Id = m.Input;
            if (string.IsNullOrEmpty(m.Name)) m.Name = m.Id;
            try { m.Command = MoveCommand.Parse(m.Input); }
            catch (FormatException e) { throw new InvalidDataException($"приём '{m.Id}': {e.Message}"); }
            if (m.Command.IsThrow) m.Type = MoveType.Throw;
            foreach (var e in m.Effects)
                if (!EffectRegistry.Has(e.Type))
                    throw new InvalidDataException($"приём '{m.Id}': неизвестный эффект '{e.Type}'");
            if (!def.Animations.ContainsKey(m.AnimationName))
                throw new InvalidDataException($"приём '{m.Id}': нет анимации '{m.AnimationName}'");
        }
        if (def.Behavior != null && !BehaviorRegistry.Has(def.Behavior))
            throw new InvalidDataException($"неизвестное поведение '{def.Behavior}'");
        def.MovesByPriority = def.Moves
            .Select((m, index) => (m, index))
            .OrderByDescending(t => TypePriority(t.m.Type))
            .ThenByDescending(t => t.m.Command.Motion.Length)
            .ThenByDescending(t => t.m.Command.Buttons.Length)
            .ThenByDescending(t => t.m.Command.IsSpecificDirection ? 1 : 0)
            .ThenBy(t => t.index)
            .Select(t => t.m)
            .ToList();

        foreach (var (name, sheet) in def.Sheets)
        {
            var path = Path.Combine(dir, sheet.File);
            if (!File.Exists(path)) throw new FileNotFoundException(null, path);
            if (_device != null) def.Textures[name] = TextureLoader.Load(_device, path);
        }

        foreach (var (name, anim) in def.Animations)
            if (!def.Sheets.ContainsKey(anim.Sheet))
                throw new InvalidDataException($"анимация '{name}' ссылается на неизвестный лист '{anim.Sheet}'");

        return def;
    }

    private StageDef LoadStage(string dir)
    {
        var def = ReadJson<StageDef>(Path.Combine(dir, "stage.json"));
        if (string.IsNullOrEmpty(def.Id)) def.Id = Path.GetFileName(dir);
        if (def.Background != null && _device != null)
            def.BackgroundTexture = TextureLoader.Load(_device, Path.Combine(dir, def.Background));
        return def;
    }

    public static T ReadJson<T>(string path)
    {
        var text = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(text, JsonOptions)
               ?? throw new InvalidDataException($"пустой файл {Path.GetFileName(path)}");
    }

    private static int TypePriority(MoveType t) => t switch
    {
        MoveType.Super => 4,
        MoveType.Special => 3,
        MoveType.Throw => 2,
        _ => 1,
    };

    private static IEnumerable<string> SubDirs(string path) =>
        Directory.Exists(path) ? Directory.GetDirectories(path).OrderBy(d => d) : Enumerable.Empty<string>();

    private static string Short(Exception e) => e is FileNotFoundException f ? $"нет файла {Path.GetFileName(f.FileName)}" : e.Message;
}
