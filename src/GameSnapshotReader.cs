using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AetherCompass.Core;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using InstanceRow = Lumina.Excel.Sheets.InstanceContent;
using NativeInstanceContent = FFXIVClientStructs.FFXIV.Client.Game.UI.InstanceContent;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace AetherCompass;

/// <summary>
/// Read-only adapter. Call only from the framework update thread, at a throttled cadence.
/// Never retains native pointers, requests game actions, or reads another character.
/// </summary>
public sealed unsafe class GameSnapshotReader
{
    private readonly IClientState clientState;
    private readonly IObjectTable objects;
    private readonly IDataManager data;
    private readonly ICondition condition;
    private readonly IPlayerState playerState;
    private readonly IUnlockState unlockState;
    private readonly IPluginLog log;
    private readonly Dictionary<string, Quest> questRows = new();
    private readonly Dictionary<string, InstanceRow> dutyRows = new();
    private readonly List<InstanceRow> expertDuties = new();
    private readonly Dictionary<string, PropertyInfo?> jobCategoryProperties = new();
    private readonly List<string> catalogWarnings = new();
    private readonly List<string> readWarnings = new();
    private bool catalogLoaded;
    private byte? expertRouletteId;
    private uint? limitedTomestoneItemId;
    private string? loggedError;

    private static readonly IReadOnlyDictionary<string, string> QuestNames = new Dictionary<string, string>
    {
        ["dawntrail"] = "Dawntrail",
        ["current-msq"] = "Windborne",
        ["relic"] = "A Phantom Reborn",
        ["wondrous-tails"] = "Keeping Up with the Aliapohs",
    };

    private static readonly IReadOnlyDictionary<string, string> DutyNames = new Dictionary<string, string>
    {
        ["mistwake"] = "Mistwake",
        ["clyteum"] = "The Clyteum",
        ["heavyweight-normal"] = "AAC Heavyweight M1",
        ["heavyweight"] = "AAC Heavyweight M4",
        ["windurst"] = "Windurst: The Third Walk",
        ["unmaking-extreme"] = "The Unmaking (Extreme)",
        ["heavyweight-savage-1"] = "AAC Heavyweight M1 (Savage)",
        ["heavyweight-savage-2"] = "AAC Heavyweight M2 (Savage)",
        ["heavyweight-savage-3"] = "AAC Heavyweight M3 (Savage)",
        ["heavyweight-savage-4"] = "AAC Heavyweight M4 (Savage)",
    };

    private static readonly (int Index, string Name)[] EquipmentSlots =
    [
        (0, "Arme"), (1, "Main secondaire"), (2, "Tête"), (3, "Torse"),
        (4, "Mains"), (6, "Jambes"), (7, "Pieds"), (8, "Boucles d'oreilles"),
        (9, "Collier"), (10, "Bracelet"), (11, "Bague droite"), (12, "Bague gauche"),
    ];

    public GameSnapshotReader(IClientState clientState, IObjectTable objects, IDataManager data,
        ICondition condition, IPlayerState playerState, IUnlockState unlockState, IPluginLog log)
    {
        this.clientState = clientState;
        this.objects = objects;
        this.data = data;
        this.condition = condition;
        this.playerState = playerState;
        this.unlockState = unlockState;
        this.log = log;
    }

    public string? LastError { get; private set; }
    public string? GameVersion { get; private set; }

    public CharacterSnapshot? Read()
    {
        LastError = null;
        readWarnings.Clear();
        if (!clientState.IsLoggedIn || !playerState.IsLoaded || objects.LocalPlayer is null || playerState.ContentId == 0)
            return null;
        if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]
            || condition[ConditionFlag.LoggingOut] || condition[ConditionFlag.InCombat]
            || condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene]
            || condition[ConditionFlag.WatchingCutscene78] || condition[ConditionFlag.CreatingCharacter])
        {
            LastError = "Lecture en pause pendant le combat, une cinématique ou un changement de zone.";
            return null;
        }

        try
        {
            var contentId = playerState.ContentId;
            var jobRef = playerState.ClassJob;
            if (!jobRef.IsValid || playerState.Level <= 0
                || objects.LocalPlayer.ClassJob.RowId != jobRef.RowId)
                return null;
            var job = jobRef.Value;
            var jobAbbreviation = job.Abbreviation.ExtractText();
            // Level comes from PlayerState, not the potentially level-synced actor object.
            var level = playerState.Level;
            var name = playerState.CharacterName;

            if (!catalogLoaded)
                Observe("Catalogue", LoadCatalog);
            var quests = ReadQuests();
            var unlocks = ReadUnlocks(quests);
            var weekly = new Dictionary<string, KnowledgeState>();
            Observe("Bonus quotidien Expert", () =>
            {
                var instance = NativeInstanceContent.Instance();
                if (expertRouletteId is { } rouletteId && instance != null
                    && NativeInstanceContent.MemberFunctionPointers.IsRouletteIncomplete != null)
                    weekly["expert-roulette"] = State(instance->IsRouletteComplete(rouletteId));
            });

            // Clearing a raid never proves its reward was claimed. Those records remain Unknown.
            var gear = ReadGear(job.RowId, jobAbbreviation);
            int? itemLevel = null;
            Observe("Niveau d'objet", () =>
            {
                var ui = UIState.Instance();
                if (ui != null && ui->CurrentItemLevel > 0)
                    itemLevel = ui->CurrentItemLevel;
            });
            Observe("Version du jeu", () =>
            {
                var framework = NativeFramework.Instance();
                GameVersion = framework == null ? null : framework->GameVersionString;
            });
            var currency = ReadTomestones();

            // A logout or character switch invalidates the entire observation.
            if (!clientState.IsLoggedIn || !playerState.IsLoaded || playerState.ContentId != contentId)
                return null;

            LastError = string.Join(" · ", catalogWarnings.Concat(readWarnings));
            if (LastError.Length == 0)
                LastError = null;
            return new CharacterSnapshot
            {
                ContentId = contentId, Name = name, JobId = job.RowId, Job = jobAbbreviation,
                Level = level, IsCombatJob = job.Role != 0, AverageItemLevel = itemLevel,
                Gear = gear, Quests = quests, Unlocks = unlocks, Weekly = weekly,
                Tomestones = currency, ObservedAt = DateTimeOffset.UtcNow,
            };
        }
        catch (Exception exception)
        {
            LastError = "Lecture indisponible : " + exception.Message;
            LogOnce(exception);
            return null;
        }
    }

    private void LoadCatalog()
    {
        questRows.Clear();
        dutyRows.Clear();
        expertDuties.Clear();
        catalogWarnings.Clear();

        // English sheet lookup is independent of the user's client language. A unique exact
        // match is mandatory: a missing or ambiguous name must never become a negative unlock.
        var quests = data.GetExcelSheet<Quest>(ClientLanguage.English);
        foreach (var (key, name) in QuestNames)
        {
            var matches = quests.Where(q => NameEquals(q.Name.ExtractText(), name)).Take(2).ToArray();
            if (matches.Length == 1)
                questRows[key] = matches[0];
            else
                catalogWarnings.Add($"Quête non résolue : {name}");
        }

        var duties = data.GetExcelSheet<ContentFinderCondition>(ClientLanguage.English).ToArray();
        foreach (var (key, name) in DutyNames)
        {
            var matches = duties.Where(d => NameEquals(d.Name.ExtractText(), name)).Take(2).ToArray();
            if (matches.Length == 1 && matches[0].Content.TryGetValue<InstanceRow>(out var instance)
                && instance.RowId != 0)
                dutyRows[key] = instance;
            else
                catalogWarnings.Add($"Contenu non résolu : {name}");
        }

        // Discover the Expert pool from this client's data instead of freezing two dungeon IDs.
        var expertCandidates = duties.Where(d => d.ExpertRoulette && d.IsInDutyFinder).ToArray();
        foreach (var row in expertCandidates)
        {
            if (!row.Content.TryGetValue<InstanceRow>(out var instance) || instance.RowId == 0)
            {
                expertDuties.Clear();
                break;
            }
            expertDuties.Add(instance);
        }
        var rouletteMatches = data.GetExcelSheet<Lumina.Excel.Sheets.ContentRoulette>(ClientLanguage.English)
            .Where(r => NameEquals(r.Name.ExtractText(), "Duty Roulette: Expert")
                && r.RowId is > 0 and <= byte.MaxValue && r.CompletionArrayIndex >= 0)
            .Take(2).ToArray();
        expertRouletteId = rouletteMatches.Length == 1 ? (byte)rouletteMatches[0].RowId : null;
        if (expertRouletteId == null || expertDuties.Count == 0)
            catalogWarnings.Add("Roulette Expert non résolue dans les données du client");

        // The current capped currency is selected by sheet metadata; no hard-coded item ID.
        var limitedItems = data.GetExcelSheet<TomestonesItem>()
            .Where(t => t.Tomestones.IsValid && t.Tomestones.Value.WeeklyLimit > 0 && t.Item.RowId != 0)
            .Select(t => t.Item.RowId).Distinct().Take(2).ToArray();
        limitedTomestoneItemId = limitedItems.Length == 1 ? limitedItems[0] : null;
        catalogLoaded = true;
    }

    private Dictionary<string, KnowledgeState> ReadQuests()
    {
        var result = new Dictionary<string, KnowledgeState>();
        if (QuestManager.MemberFunctionPointers.IsQuestComplete == null)
            return result;
        foreach (var (key, row) in questRows)
            Observe($"Quête {key}", () => result[key] = State(unlockState.IsQuestCompleted(row)));
        return result;
    }

    private Dictionary<string, KnowledgeState> ReadUnlocks(IReadOnlyDictionary<string, KnowledgeState> quests)
    {
        var result = new Dictionary<string, KnowledgeState>();
        foreach (var key in new[] { "dawntrail", "relic", "wondrous-tails" })
            if (quests.TryGetValue(key, out var state))
                result[key] = state;
        if (UIState.MemberFunctionPointers.IsInstanceContentUnlocked != null)
            foreach (var (key, row) in dutyRows)
                Observe($"Accès {key}", () => result[key] = State(unlockState.IsInstanceContentUnlocked(row)));

        // Expert requires first completion of the current pool, not merely dungeon unlocks.
        if (expertDuties.Count > 0 && UIState.MemberFunctionPointers.IsInstanceContentCompleted != null)
            Observe("Accès Expert", () => result["expert"] = State(expertDuties.All(
                row => UIState.IsInstanceContentCompleted(row.RowId))));
        return result;
    }

    private IReadOnlyList<GearSlot> ReadGear(uint jobId, string abbreviation)
    {
        var result = new List<GearSlot>();
        Observe("Équipement", () =>
        {
            var inventory = InventoryManager.Instance();
            if (inventory == null || InventoryManager.MemberFunctionPointers.GetInventoryContainer == null)
                return;
            var equipped = inventory->GetInventoryContainer(InventoryType.EquippedItems);
            if (equipped == null || !equipped->IsLoaded || equipped->Items == null || equipped->Size < 13)
                return;
            var items = data.GetExcelSheet<Item>();
            foreach (var (index, slotName) in EquipmentSlots)
            {
                var item = equipped->Items[index];
                if (item.IsSymbolic)
                    continue; // Do not follow unverified linked-inventory pointers.
                // Most combat jobs have no off-hand. A missing shield is meaningful for GLA/PLD.
                if (index == 1 && item.ItemId == 0 && jobId is not (1 or 19))
                    continue;
                if (item.ItemId == 0)
                {
                    result.Add(new GearSlot(slotName, "Emplacement vide", 0, 0, Kind: SlotKind(index)));
                    continue;
                }
                if (!items.TryGetRow(item.ItemId, out var row) || row.LevelItem.RowId == 0)
                    continue;
                result.Add(new GearSlot(slotName, row.Name.ExtractText(), row.RowId,
                    checked((int)row.LevelItem.RowId), IsAppropriate(row, abbreviation), SlotKind(index)));
            }
        });
        return result;
    }

    private bool IsAppropriate(Item item, string abbreviation)
    {
        if (!jobCategoryProperties.TryGetValue(abbreviation, out var property))
        {
            property = typeof(ClassJobCategory).GetProperty(abbreviation);
            jobCategoryProperties[abbreviation] = property;
        }
        // Equipping already guarantees basic eligibility; only claim a mismatch when verified.
        return !item.ClassJobCategory.IsValid || property?.GetValue(item.ClassJobCategory.Value) is not false;
    }

    private CurrencyState ReadTomestones()
    {
        int? earned = null, cap = null, stock = null;
        Observe("Mémoquartz hebdomadaires", () =>
        {
            var inventory = InventoryManager.Instance();
            if (inventory == null)
                return;
            if (InventoryManager.MemberFunctionPointers.GetLimitedTomestoneWeeklyLimit != null)
            {
                var value = InventoryManager.GetLimitedTomestoneWeeklyLimit();
                if (value > 0)
                    cap = value;
            }
            if (InventoryManager.MemberFunctionPointers.GetLimitedTomestoneCount != null
                && InventoryManager.MemberFunctionPointers.GetSpecialItemId != null)
            {
                var value = inventory->GetWeeklyAcquiredTomestoneCount();
                if (value >= 0 && (cap == null || value <= cap))
                    earned = value;
            }
            if (limitedTomestoneItemId is { } itemId && InventoryManager.MemberFunctionPointers.GetTomestoneCount != null)
            {
                var value = inventory->GetTomestoneCount(itemId);
                if (value <= int.MaxValue)
                    stock = (int)value;
            }
        });
        return new CurrencyState { EarnedThisWeek = earned, WeeklyCap = cap, Stock = stock };
    }

    private void Observe(string section, System.Action action)
    {
        try { action(); }
        catch (Exception exception)
        {
            readWarnings.Add($"{section} indisponible");
            LogOnce(exception);
        }
    }

    private void LogOnce(Exception exception)
    {
        var error = exception.GetType().FullName + ": " + exception.Message;
        if (error == loggedError)
            return;
        loggedError = error;
        log.Warning(exception, "AetherCompass: data unavailable; keeping state unknown.");
    }

    private static KnowledgeState State(bool value) => value ? KnowledgeState.Yes : KnowledgeState.No;
    private static GearSlotKind SlotKind(int index) => index <= 1 ? GearSlotKind.Weapon
        : index <= 7 ? GearSlotKind.Armor : GearSlotKind.Accessory;
    private static bool NameEquals(string left, string right)
        => string.Equals(left.Trim(), right, StringComparison.OrdinalIgnoreCase);
}
