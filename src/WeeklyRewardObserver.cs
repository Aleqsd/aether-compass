using AetherCompass.Core;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace AetherCompass;

/// <summary>
/// Correlates inventory events with a sustained increase in all personal inventories inside
/// the exact reward duty. Update must run on the framework thread, never from Draw.
/// The first observation, an existing possession and a duty clear are never reward evidence.
/// </summary>
public sealed unsafe class WeeklyRewardObserver : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CatalogRetryInterval = TimeSpan.FromSeconds(30);
    private const string HeavyweightDuty = "AAC Heavyweight M4";
    private const string WindurstDuty = "Windurst: The Third Walk";
    private static readonly (string[] Roles, string[] Pieces)[] ArmorGroups =
    [
        (["Fending", "Maiming"], ["Coronet", "Surcoat", "Gauntlets", "Breeches", "Sollerets"]),
        (["Striking"], ["Crown", "Cyclas", "Gloves", "Halfslops", "Gaiters"]),
        (["Scouting", "Aiming"], ["Bonnet", "Vest", "Armlets", "Culottes", "Poulaines"]),
        (["Casting", "Healing"], ["Chapeau", "Tabard", "Gloves", "Tights", "Shoes"]),
    ];

    // All active personal storage reachable by normal equipment moves. Waist was removed
    // from the game; retainers, saddlebags, mail and company chests are not personal duty bags.
    private static readonly GameInventoryType[] PersonalContainers =
    [
        GameInventoryType.Inventory1, GameInventoryType.Inventory2,
        GameInventoryType.Inventory3, GameInventoryType.Inventory4,
        GameInventoryType.EquippedItems, GameInventoryType.ArmoryMainHand,
        GameInventoryType.ArmoryOffHand, GameInventoryType.ArmoryHead,
        GameInventoryType.ArmoryBody, GameInventoryType.ArmoryHands,
        GameInventoryType.ArmoryLegs, GameInventoryType.ArmoryFeets,
        GameInventoryType.ArmoryEar, GameInventoryType.ArmoryNeck,
        GameInventoryType.ArmoryWrist, GameInventoryType.ArmoryRings,
        GameInventoryType.ArmorySoulCrystal,
    ];
    private static readonly HashSet<GameInventoryType> PersonalContainerSet = [.. PersonalContainers];

    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IObjectTable objects;
    private readonly IDataManager data;
    private readonly ICondition condition;
    private readonly IGameInventory inventory;
    private readonly IPluginLog log;
    private readonly ProgressStore progress;
    private readonly System.Action changed;
    private readonly List<RewardRule> rules = [];
    private readonly Dictionary<uint, uint> conditionByTerritory = [];
    private readonly HashSet<uint> trackedItems = [];
    private readonly HashSet<uint> pendingCandidates = [];
    private readonly List<string> catalogProblems = [];
    private RewardInventoryTracker? tracker;
    private DateTimeOffset nextSample;
    private DateTimeOffset nextCatalogAttempt;
    private ulong pendingContentId;
    private uint pendingTerritoryId;
    private bool pendingInventoryEvent;
    private bool disposed;
    private string? lastLoggedError;

    public WeeklyRewardObserver(IClientState client, IPlayerState player, IObjectTable objects,
        IDataManager data, ICondition condition, IGameInventory inventory, IPluginLog log,
        ProgressStore progress, System.Action changed)
    {
        this.client = client;
        this.player = player;
        this.objects = objects;
        this.data = data;
        this.condition = condition;
        this.inventory = inventory;
        this.log = log;
        this.progress = progress;
        this.changed = changed;
        inventory.InventoryChanged += OnInventoryChanged;
        client.Login += OnLogin;
        client.Logout += OnLogout;
        client.TerritoryChanged += OnTerritoryChanged;
        Status = "Observation des récompenses en attente de connexion.";
    }

    public string? Status { get; private set; }

    public void Update()
    {
        if (disposed)
            return;
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (!TryGetIdentity(out var contentId, out var territoryId))
            {
                ResetSession();
                SetStatus("Observation en pause : personnage ou zone en cours de chargement.");
                return;
            }
            if (tracker is null)
            {
                if (now < nextCatalogAttempt)
                    return;
                nextCatalogAttempt = now + CatalogRetryInterval;
                BuildRules();
                tracker = new RewardInventoryTracker(rules);
            }
            if (!IsSupportedDuty(territoryId))
            {
                ResetSession();
                SetStatus(rules.Count == 0 ? "Aucune récompense reconnue dans les données du client."
                    : "Observation automatique dans AAC Heavyweight M4 et Windurst uniquement.");
                return;
            }
            // Inventory callbacks can run after our framework handler. Process them on the
            // next Update even when the ordinary 250 ms heartbeat is not due yet.
            if (!pendingInventoryEvent && now < nextSample)
                return;
            nextSample = now + SampleInterval;

            if (pendingInventoryEvent && (pendingContentId != contentId || pendingTerritoryId != territoryId))
            {
                ResetSession();
                SetStatus("Nouvelle session : acquisition précédente ignorée.");
                return;
            }
            if (!TryReadCounts(out var counts))
            {
                ResetSession();
                SetStatus("Observation en attente du chargement des sacs et de l'armurerie.");
                return;
            }
            if (!TryGetIdentity(out var checkedContentId, out var checkedTerritoryId)
                || checkedContentId != contentId || checkedTerritoryId != territoryId
                || !IsSupportedDuty(territoryId))
            {
                ResetSession();
                return;
            }

            var observations = tracker.Observe(
                new RewardInventorySample(contentId, territoryId, now, counts),
                pendingCandidates, pendingInventoryEvent);
            ClearPendingEvent();

            foreach (var reward in observations)
            {
                // No evidence from a previous character or territory may enter the store.
                if (!TryGetIdentity(out checkedContentId, out checkedTerritoryId)
                    || checkedContentId != contentId || checkedTerritoryId != territoryId)
                {
                    ResetSession();
                    return;
                }
                if (progress.GetOrCreate(contentId).ObserveReward(reward.ObjectiveId, reward.Evidence, now))
                    changed();
            }
            SetStatus(tracker.IsArmed
                ? "Observation active : les acquisitions confirmées seront enregistrées automatiquement."
                : "Initialisation : attente d'un inventaire stable, sans reprise des objets déjà possédés.");
        }
        catch (Exception exception)
        {
            ResetSession();
            SetStatus("Observation indisponible ; aucune récompense n'a été déduite.");
            LogOnce(exception);
        }
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (disposed || tracker is null || events.Count == 0)
            return;
        try
        {
            if (!TryGetIdentity(out var contentId, out var territoryId) || !IsSupportedDuty(territoryId))
            {
                ResetSession();
                return;
            }
            if (pendingInventoryEvent && (pendingContentId != contentId || pendingTerritoryId != territoryId))
                ResetSession();
            pendingContentId = contentId;
            pendingTerritoryId = territoryId;
            pendingInventoryEvent = true;

            // Copy scalars only: the event collection and its ref-returning Item properties
            // are never retained. Reconciled Moved/Merged/Split events are not acquisitions.
            foreach (var change in events)
            {
                if (!PersonalContainerSet.Contains(change.Item.ContainerType))
                    continue;
                var itemId = change.Item.BaseItemId;
                if (!trackedItems.Contains(itemId) || change.Item.Quantity <= 0)
                    continue;
                if (change is InventoryItemAddedArgs)
                    pendingCandidates.Add(itemId);
                else if (change is InventoryItemChangedArgs modified
                    && (modified.OldItemState.BaseItemId != itemId
                        || modified.Item.Quantity > modified.OldItemState.Quantity))
                    pendingCandidates.Add(itemId);
            }
        }
        catch (Exception exception)
        {
            ResetSession();
            SetStatus("Événement d'inventaire indisponible ; suivi manuel conservé.");
            LogOnce(exception);
        }
    }

    private bool TryGetIdentity(out ulong contentId, out uint territoryId)
    {
        contentId = 0;
        territoryId = 0;
        if (!client.IsLoggedIn || !player.IsLoaded || player.ContentId == 0 || objects.LocalPlayer is null
            || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]
            || condition[ConditionFlag.LoggingOut] || condition[ConditionFlag.CreatingCharacter])
            return false;
        // Combat and cutscenes intentionally remain eligible: that is when loot can arrive.
        contentId = player.ContentId;
        territoryId = client.TerritoryType;
        return territoryId != 0;
    }

    private bool IsSupportedDuty(uint territoryId)
    {
        return conditionByTerritory.TryGetValue(territoryId, out var expectedCondition)
            && data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory)
            && territory.ContentFinderCondition.IsValid
            && territory.ContentFinderCondition.RowId == expectedCondition;
    }

    private bool TryReadCounts(out Dictionary<uint, int> counts)
    {
        counts = trackedItems.ToDictionary(id => id, _ => 0);
        var manager = InventoryManager.Instance();
        if (manager == null || InventoryManager.MemberFunctionPointers.GetInventoryContainer == null)
            return false;
        foreach (var type in PersonalContainers)
        {
            // GameInventoryType mirrors the published InventoryType enum. A non-empty span
            // alone cannot establish loading; first check the authoritative native container.
            var container = manager->GetInventoryContainer((InventoryType)(int)type);
            if (container == null || !container->IsLoaded || container->Items == null
                || container->Size <= 0 || container->Size > 500)
                return false;
            var items = inventory.GetInventoryItems(type);
            if (items.Length != container->Size)
                return false;
            foreach (ref readonly var item in items)
            {
                if (item.IsEmpty || !counts.ContainsKey(item.BaseItemId))
                    continue;
                if (item.Quantity <= 0)
                    return false;
                counts[item.BaseItemId] = checked(counts[item.BaseItemId] + item.Quantity);
            }
        }
        return true;
    }

    private void BuildRules()
    {
        rules.Clear();
        trackedItems.Clear();
        conditionByTerritory.Clear();
        catalogProblems.Clear();
        var wantedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Heavy Holoblade", "Ranperre Coin" };
        foreach (var (roles, pieces) in ArmorGroups)
            foreach (var role in roles)
                foreach (var piece in pieces)
                    wantedNames.Add($"Vana'dielian {piece} of {role}");

        // Decode each sheet name only once, retaining at most two matches for each of our
        // 37 names. Two are enough to reject ambiguity without repeatedly scanning Item.
        var items = new Dictionary<string, Item[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in data.GetExcelSheet<Item>(ClientLanguage.English))
        {
            if (row.RowId == 0)
                continue;
            var name = row.Name.ExtractText().Trim();
            if (!wantedNames.Contains(name))
                continue;
            if (!items.TryGetValue(name, out var existing))
                items[name] = [row];
            else if (existing.Length == 1)
                items[name] = [existing[0], row];
        }
        var heavyweight = ResolveDuty(HeavyweightDuty);
        if (heavyweight is { } heavy)
            AddTokenRule(items, "heavyweight-holoblade", "Heavy Holoblade", heavy.Territory, HeavyweightDuty);

        var windurst = ResolveDuty(WindurstDuty);
        if (windurst is { } alliance)
        {
            AddTokenRule(items, "alliance-coin", "Ranperre Coin", alliance.Territory, WindurstDuty);
            // Exact names from the official Windurst coffer list, not an item-level heuristic:
            // https://na.finalfantasyxiv.com/lodestone/playguide/db/duty/e40698b1f19/
            foreach (var (roles, pieces) in ArmorGroups)
                AddArmorRules(items, alliance.Territory, roles, pieces);
        }
        foreach (var rule in rules)
            trackedItems.Add(rule.ItemId);
        foreach (var territory in conditionByTerritory.Keys.Where(id => rules.All(rule => rule.TerritoryId != id)).ToArray())
            conditionByTerritory.Remove(territory);
    }

    private (uint Territory, uint Condition)? ResolveDuty(string name)
    {
        var matches = data.GetExcelSheet<ContentFinderCondition>(ClientLanguage.English)
            .Where(row => string.Equals(row.Name.ExtractText().Trim(), name, StringComparison.OrdinalIgnoreCase))
            .Take(2).ToArray();
        if (matches.Length != 1 || matches[0].RowId == 0 || !matches[0].TerritoryType.IsValid
            || matches[0].TerritoryType.RowId == 0
            || matches[0].TerritoryType.Value.ContentFinderCondition.RowId != matches[0].RowId)
        {
            catalogProblems.Add(name);
            return null;
        }
        var row = matches[0];
        conditionByTerritory[row.TerritoryType.RowId] = row.RowId;
        return (row.TerritoryType.RowId, row.RowId);
    }

    private void AddTokenRule(IReadOnlyDictionary<string, Item[]> items, string objectiveId, string name, uint territoryId, string dutyName)
    {
        var matches = FindItem(items, name);
        if (matches.Length != 1 || !matches[0].IsUntradable)
        {
            catalogProblems.Add(name);
            return;
        }
        rules.Add(new RewardRule(objectiveId, matches[0].RowId, name, territoryId, dutyName));
    }

    private void AddArmorRules(IReadOnlyDictionary<string, Item[]> items, uint territoryId, string[] roles, string[] pieces)
    {
        foreach (var role in roles)
        {
            for (var slot = 0; slot < pieces.Length; slot++)
            {
                var name = $"Vana'dielian {pieces[slot]} of {role}";
                var matches = FindItem(items, name);
                if (matches.Length != 1 || !matches[0].IsUntradable || matches[0].LevelEquip != 100
                    || matches[0].LevelItem.RowId != 780 || !matches[0].EquipSlotCategory.IsValid
                    || !IsExpectedArmorSlot(matches[0].EquipSlotCategory.Value, slot))
                {
                    catalogProblems.Add(name);
                    continue;
                }
                rules.Add(new RewardRule("alliance-loot", matches[0].RowId, name, territoryId, WindurstDuty));
            }
        }
    }

    private static Item[] FindItem(IReadOnlyDictionary<string, Item[]> items, string name)
        => items.TryGetValue(name, out var matches) ? matches : [];

    private static bool IsExpectedArmorSlot(EquipSlotCategory slot, int index) => index switch
    {
        0 => slot.Head > 0,
        1 => slot.Body > 0,
        2 => slot.Gloves > 0,
        3 => slot.Legs > 0,
        4 => slot.Feet > 0,
        _ => false,
    };

    private void OnLogin() => ResetSession();
    private void OnLogout(int type, int code) => ResetSession();
    private void OnTerritoryChanged(uint territoryId) => ResetSession();

    private void ResetSession()
    {
        tracker?.Reset();
        ClearPendingEvent();
        nextSample = DateTimeOffset.MinValue;
    }

    private void ClearPendingEvent()
    {
        pendingCandidates.Clear();
        pendingInventoryEvent = false;
        pendingContentId = 0;
        pendingTerritoryId = 0;
    }

    private void SetStatus(string message)
    {
        Status = catalogProblems.Count == 0 ? message
            : message + $" Couverture partielle ({catalogProblems.Count} correspondances indisponibles) : "
                + string.Join(", ", catalogProblems.Take(3)) + (catalogProblems.Count > 3 ? "…" : ".");
    }

    private void LogOnce(Exception exception)
    {
        var key = exception.GetType().FullName + ": " + exception.Message;
        if (lastLoggedError == key)
            return;
        lastLoggedError = key;
        log.Warning(exception, "AetherCompass: weekly reward observation unavailable; no completion inferred.");
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        inventory.InventoryChanged -= OnInventoryChanged;
        client.Login -= OnLogin;
        client.Logout -= OnLogout;
        client.TerritoryChanged -= OnTerritoryChanged;
        ResetSession();
    }
}
