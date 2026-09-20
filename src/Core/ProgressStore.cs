using System.Text.Json;

namespace AetherCompass.Core;

/// <summary>Per-character manual facts. A weekly checkmark expires by UTC reset, not by login.</summary>
public sealed class ProgressStore
{
    public int Version { get; set; } = 1;
    public Dictionary<ulong, CharacterProgress> Characters { get; set; } = new();

    public CharacterProgress GetOrCreate(ulong contentId)
    {
        if (contentId == 0) throw new ArgumentOutOfRangeException(nameof(contentId), "A logged-in character is required.");
        if (!Characters.TryGetValue(contentId, out var progress))
            Characters.Add(contentId, progress = new CharacterProgress { ContentId = contentId });
        return progress;
    }

    public static ProgressStore Load(string path)
    {
        if (!File.Exists(path)) return new ProgressStore();
        var store = JsonSerializer.Deserialize<ProgressStore>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Character progress file is empty.");
        if (store.Version != 1) throw new InvalidDataException($"Unsupported progress version: {store.Version}.");
        if (store.Characters is null) throw new InvalidDataException("Character progress collection is null.");
        foreach (var pair in store.Characters)
        {
            if (pair.Key == 0 || pair.Value is null || pair.Value.ContentId != pair.Key)
                throw new InvalidDataException("Character identifiers do not match the progress records.");
            var progress = pair.Value;
            if (progress.Completions is null || progress.ManualActivityStates is null || progress.ManualUnlocks is null)
                throw new InvalidDataException("Character progress contains a null collection.");
            if (progress.Completions.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value is null || !Enum.IsDefined(x.Value.Provenance))
                || progress.ManualActivityStates.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value is null || !Enum.IsDefined(x.Value.State) || !Enum.IsDefined(x.Value.Provenance))
                || progress.ManualUnlocks.Any(x => string.IsNullOrWhiteSpace(x.Key) || !Enum.IsDefined(x.Value)))
                throw new InvalidDataException("Character progress contains an invalid observation.");
        }
        return store;
    }

    public void Save(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, fullPath, overwrite: true);
    }
}
