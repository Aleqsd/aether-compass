using AetherCompass.Core;

var now = new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);
var engine = new PlannerEngine();
var tests = new List<(string Name, Action Run)>();

CharacterSnapshot Character(int level = 100, int? ilvl = 780, ulong contentId = 123)
    => new()
    {
        ContentId = contentId, Name = "Test Character", Job = "DRG", JobId = 22,
        Level = level, IsCombatJob = true, AverageItemLevel = ilvl, ObservedAt = now,
        Quests = new Dictionary<string, KnowledgeState> { ["dawntrail"] = KnowledgeState.Yes, ["current-msq"] = KnowledgeState.Yes },
        Unlocks = ObjectiveCatalog.All.Where(x => x.UnlockKey != null).Select(x => x.UnlockKey!).Distinct().ToDictionary(x => x, _ => KnowledgeState.Yes),
        Gear = [new("Arme", "Lance", 100, 780, Kind: GearSlotKind.Weapon), new("Torse", "Mail", 101, 770, Kind: GearSlotKind.Armor), new("Bague", "Ring", 102, 760, Kind: GearSlotKind.Accessory)],
        Tomestones = new CurrencyState { EarnedThisWeek = 200, WeeklyCap = 900, Stock = 500 },
    };

CharacterProgress Progress(ulong contentId = 123) => new() { ContentId = contentId };
Preferences Prefs(PlayAmbition ambition = PlayAmbition.Savage, int minutes = 120) => new() { Ambition = ambition, SessionMinutes = minutes };
ObjectiveDefinition Def(string id) => ObjectiveCatalog.All.Single(x => x.Id == id);
ObjectiveRecommendation Find(PlanResult result, string id) => result.Recommendations.Concat(result.Blocked).Concat(result.Completed).Single(x => x.Objective.Id == id);
PlanResult Plan(CharacterSnapshot snapshot, CharacterProgress? progress = null, Preferences? preferences = null, DateTimeOffset? at = null)
    => engine.Evaluate(snapshot, progress ?? Progress(snapshot.ContentId), preferences ?? Prefs(), at ?? now);
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new InvalidOperationException(message); }
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}; got {actual}"); }
void Test(string name, Action run) => tests.Add((name, run));

Test("Weekly boundary changes at Tuesday 08:00 UTC", () =>
{
    var boundary = new DateTimeOffset(2026, 9, 22, 8, 0, 0, TimeSpan.Zero);
    Equal(boundary.AddDays(-7), ResetClock.PeriodStart(boundary.AddTicks(-1), ResetCadence.Weekly));
    Equal(boundary, ResetClock.PeriodStart(boundary, ResetCadence.Weekly));
    Equal(boundary.AddDays(7), ResetClock.NextReset(boundary, ResetCadence.Weekly)!.Value);
});
Test("Daily boundary changes at 15:00 UTC", () =>
{
    var boundary = new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.Zero);
    Equal(boundary.AddDays(-1), ResetClock.PeriodStart(boundary.AddTicks(-1), ResetCadence.Daily));
    Equal(boundary, ResetClock.PeriodStart(boundary, ResetCadence.Daily));
});
Test("French DST offsets do not move weekly resets", () =>
{
    var winter = new DateTimeOffset(2026, 3, 24, 9, 0, 0, TimeSpan.FromHours(1));
    var summer = new DateTimeOffset(2026, 3, 31, 10, 0, 0, TimeSpan.FromHours(2));
    Equal(8, ResetClock.PeriodStart(winter, ResetCadence.Weekly).Hour);
    Equal(8, ResetClock.PeriodStart(summer, ResetCadence.Weekly).Hour);
    Equal(TimeSpan.FromDays(7), ResetClock.PeriodStart(summer, ResetCadence.Weekly) - ResetClock.PeriodStart(winter, ResetCadence.Weekly));
});
Test("Manual weekly completion expires to Unknown at reset", () =>
{
    var p = Progress();
    p.Complete("alliance-loot", now);
    Equal(KnowledgeState.Yes, PlannerEngine.GetCompletionState(Def("alliance-loot"), Character(), p, now));
    Equal(KnowledgeState.Unknown, PlannerEngine.GetCompletionState(Def("alliance-loot"), Character(), p, new DateTimeOffset(2026,9,22,8,0,0,TimeSpan.Zero)));
});
Test("Daily and weekly manual records expire independently", () =>
{
    var p = Progress(); p.Complete("expert-roulette", now); p.Complete("alliance-loot", now);
    var tomorrow = now.AddDays(1);
    Equal(KnowledgeState.Unknown, PlannerEngine.GetCompletionState(Def("expert-roulette"), Character(), p, tomorrow));
    Equal(KnowledgeState.Yes, PlannerEngine.GetCompletionState(Def("alliance-loot"), Character(), p, tomorrow));
});
Test("Explicit incomplete and cleared state are different", () =>
{
    var p = Progress(); p.SetIncomplete("alliance-loot", now);
    Equal(KnowledgeState.No, PlannerEngine.GetCompletionState(Def("alliance-loot"), Character(), p, now));
    Equal(ObjectiveStatus.Available, Find(Plan(Character(),p), "alliance-loot").Status);
    p.ClearCompletion("alliance-loot");
    Equal(KnowledgeState.Unknown, PlannerEngine.GetCompletionState(Def("alliance-loot"), Character(), p, now));
});
Test("Prior-week explicit incomplete expires too", () =>
{
    var p = Progress(); p.SetIncomplete("alliance-loot", now.AddDays(-7));
    Equal(KnowledgeState.Unknown, PlannerEngine.GetCompletionState(Def("alliance-loot"), Character(), p, now));
});
Test("Unknown loot is never reported available or completed", () =>
{
    var recommendation = Find(Plan(Character()), "alliance-loot");
    Equal(ObjectiveStatus.NeedsVerification, recommendation.Status);
    Check(recommendation.Reasons.Any(x => x.Contains("inconnu")));
});
Test("Alliance loot and coin are independent", () =>
{
    var p = Progress(); p.Complete("alliance-loot", now);
    var result = Plan(Character(), p);
    Equal(ObjectiveStatus.Completed, Find(result,"alliance-loot").Status);
    Equal(ObjectiveStatus.NeedsVerification, Find(result,"alliance-coin").Status);
});
Test("Known live weekly state takes precedence over manual record", () =>
{
    var p = Progress(); p.Complete("expert-roulette", now);
    var snapshot = Character() with { Weekly = new Dictionary<string, KnowledgeState> { ["expert-roulette"] = KnowledgeState.No } };
    Equal(KnowledgeState.No, PlannerEngine.GetCompletionState(Def("expert-roulette"), snapshot, p, now));
    Equal(CompletionProvenance.Observed, Find(Plan(snapshot,p),"expert-roulette").CompletionSource!.Value);
});
Test("Stale live completion cannot cross weekly reset", () =>
{
    var snapshot = Character() with { ObservedAt = now.AddDays(-7), Weekly = new Dictionary<string, KnowledgeState> { ["alliance-coin"] = KnowledgeState.Yes } };
    Equal(ObjectiveStatus.NeedsVerification, Find(Plan(snapshot),"alliance-coin").Status);
});
Test("Fresh level 100 gets catch-up routes, not inaccessible duties", () =>
{
    var result = Plan(Character(ilvl:690));
    Equal(ProgressStage.Fresh100, result.Stage);
    Equal(ObjectiveStatus.Available, Find(result,"gear-catchup").Status);
    Equal(ObjectiveStatus.Blocked, Find(result,"mistwake").Status);
    Equal(ObjectiveStatus.Blocked, Find(result,"alliance-loot").Status);
});
Test("Level 99 cannot use level 100 duties", () =>
{
    var result = Plan(Character(level:99));
    Equal(ProgressStage.Leveling,result.Stage);
    Equal(ObjectiveStatus.Blocked, Find(result,"mistwake").Status);
});
Test("Advanced Savage remains repeatable after manual completion", () =>
{
    var p = Progress(); p.Complete("heavyweight-savage-4",now);
    var result = Plan(Character(ilvl:790),p);
    Equal(ProgressStage.Advanced,result.Stage);
    Equal(ObjectiveStatus.Available,Find(result,"heavyweight-savage-4").Status);
    Equal(ResetCadence.None,Def("heavyweight-savage-4").Cadence);
});
Test("Casual profile excludes both Extreme and Savage", () =>
{
    var result = Plan(Character(),preferences:Prefs(PlayAmbition.Casual));
    Equal(ObjectiveStatus.Blocked,Find(result,"unmaking-extreme").Status);
    Equal(ObjectiveStatus.Blocked,Find(result,"heavyweight-savage-1").Status);
});
Test("Short sessions exclude long activities", () =>
{
    var result = Plan(Character(),preferences:Prefs(minutes:20));
    Equal(ObjectiveStatus.Blocked,Find(result,"alliance-loot").Status);
    Equal(ObjectiveStatus.Available,Find(result,"heavyweight-normal").Status);
});
Test("Spending stock does not reopen earned weekly cap", () =>
{
    var snapshot = Character() with { Tomestones = new CurrencyState { EarnedThisWeek=900, WeeklyCap=900, Stock=0 } };
    var result = Plan(snapshot);
    Equal(ObjectiveStatus.Completed,Find(result,"mnemonics-cap").Status);
    Equal(ObjectiveStatus.Blocked,Find(result,"mnemonics-spend").Status);
});
Test("Stock alone cannot establish weekly earnings", () =>
{
    var snapshot = Character() with { Tomestones = new CurrencyState { Stock=900, WeeklyCap=900 } };
    Equal(ObjectiveStatus.NeedsVerification,Find(Plan(snapshot),"mnemonics-cap").Status);
});
Test("Full currency stock redirects to spending", () =>
{
    var snapshot = Character() with { Tomestones = new CurrencyState { Stock=2000, EarnedThisWeek=200, WeeklyCap=900 } };
    var result = Plan(snapshot);
    Equal(ObjectiveStatus.Blocked,Find(result,"mnemonics-cap").Status);
    Equal(ObjectiveStatus.Available,Find(result,"mnemonics-spend").Status);
    Check(Find(result,"mnemonics-spend").Reasons.Any(x => x.Contains("capacité")));
});
Test("Unknown ilvl does not claim eligibility", () =>
{
    var result = Plan(Character(ilvl:null));
    Equal(ProgressStage.Unknown,result.Stage);
    Equal(ObjectiveStatus.NeedsVerification,Find(result,"heavyweight-normal").Status);
    Check(result.DataWarnings.Count>0);
});
Test("Observed quest locks survive conflicting manual overrides", () =>
{
    var snapshot = Character() with { Quests = new Dictionary<string, KnowledgeState> { ["dawntrail"] = KnowledgeState.No, ["current-msq"] = KnowledgeState.No } };
    var p = Progress(); p.ManualUnlocks["dawntrail"] = KnowledgeState.Yes;
    var result = Plan(snapshot,p);
    Equal(ObjectiveStatus.Blocked,Find(result,"heavyweight-normal").Status);
    Equal("msq",result.Recommendations[0].Objective.Id);
});
Test("Manual quest evidence fills missing native data", () =>
{
    var snapshot = Character() with { Quests = new Dictionary<string, KnowledgeState>() };
    var p = Progress(); p.ManualUnlocks["dawntrail"] = KnowledgeState.Yes;
    Equal(ObjectiveStatus.Available,Find(Plan(snapshot,p),"heavyweight-normal").Status);
});
Test("Per-floor unlocks prevent access to later Savage fights", () =>
{
    var unlocks = new Dictionary<string, KnowledgeState>(Character().Unlocks) { ["heavyweight-savage-4"] = KnowledgeState.No };
    var result = Plan(Character() with { Unlocks=unlocks });
    Equal(ObjectiveStatus.Available,Find(result,"heavyweight-savage-1").Status);
    Equal(ObjectiveStatus.Blocked,Find(result,"heavyweight-savage-4").Status);
});
Test("Normal M1 access does not imply holoblade M4 access", () =>
{
    var unlocks = new Dictionary<string, KnowledgeState>(Character().Unlocks) { ["heavyweight"] = KnowledgeState.No };
    var result = Plan(Character() with { Unlocks=unlocks });
    Equal(ObjectiveStatus.Available,Find(result,"heavyweight-normal").Status);
    Equal(ObjectiveStatus.Blocked,Find(result,"heavyweight-holoblade").Status);
});
Test("Gathering and crafting jobs receive no combat recommendations", () =>
{
    var result = Plan(Character() with { Job="MIN", JobId=16, IsCombatJob=false });
    Equal(ProgressStage.NonCombat,result.Stage);
    Equal(0,result.Recommendations.Count);
});
Test("Incompatible gear is excluded from upgrade targets", () =>
{
    var result = Plan(Character() with { Gear=[new("Torse","Wrong class",1,1,false,GearSlotKind.Armor), new("Arme","Lance",2,770,true,GearSlotKind.Weapon)] });
    Equal("Arme",result.WeakestSlot!.Slot);
    Equal(ObjectiveStatus.Blocked,Find(result,"heavyweight-normal").Status);
});
Test("Empty equipped slot is the weakest slot", () =>
{
    var result = Plan(Character() with { Gear=[new("Bague","Vide",0,0,Kind:GearSlotKind.Accessory), new("Arme","Lance",2,780,Kind:GearSlotKind.Weapon)] });
    Equal("Bague",result.WeakestSlot!.Slot);
    Equal(0,result.WeakestSlot.ItemLevel);
});
Test("Weapon reward does not improve a weak ring", () =>
{
    var result = Plan(Character() with { Gear=[new("Arme","Great lance",1,795,Kind:GearSlotKind.Weapon),new("Bague","Old ring",2,740,Kind:GearSlotKind.Accessory)] });
    var extreme = Find(result,"unmaking-extreme");
    Check(!extreme.Reasons.Any(x => x.Contains("Bague")));
    Check(extreme.Reasons.Any(x => x.Contains("Aucun gain")));
});
Test("Alliance armor reward does not improve a weak accessory", () =>
{
    var result = Plan(Character() with { Gear=[new("Torse","Armor",1,790,Kind:GearSlotKind.Armor),new("Bague","Old ring",2,740,Kind:GearSlotKind.Accessory)] });
    var alliance = Find(result,"alliance-loot");
    Check(!alliance.Reasons.Any(x => x.Contains("Bague")));
    Check(alliance.Reasons.Any(x => x.Contains("Aucun gain")));
});
Test("Optional Wondrous Tails is only shown when selected", () =>
{
    Check(!Plan(Character()).Recommendations.Any(x => x.Objective.Id=="wondrous-tails"));
    Equal(ObjectiveStatus.NeedsVerification,Find(Plan(Character(),preferences:Prefs() with { IncludeWondrousTails=true }),"wondrous-tails").Status);
});
Test("Fully geared characters prioritize useful activities over generic gear review", () =>
{
    var snapshot = Character(ilvl:795) with { Gear = [new("Arme","Weapon",1,795,Kind:GearSlotKind.Weapon),new("Torse","Armor",2,795,Kind:GearSlotKind.Armor),new("Bague","Ring",3,795,Kind:GearSlotKind.Accessory)] };
    var result = Plan(snapshot);
    Check(result.Recommendations[0].Objective.Id != "gear-review");
    Equal(ObjectiveStatus.Blocked,Find(result,"gear-catchup").Status);
    Check(Find(result,"mnemonics-cap").Score > Find(result,"gear-review").Score);
});
Test("Unknown gear never implies no upgrade is possible", () =>
{
    var result = Plan(Character() with { Gear = [] });
    Equal(ObjectiveStatus.NeedsVerification,Find(result,"gear-review").Status);
    var catchup = Find(result,"gear-catchup");
    Check(catchup.Status != ObjectiveStatus.Blocked);
    Check(!catchup.Reasons.Any(x => x.Contains("Aucun gain")));
    Check(!catchup.Blockers.Any(x => x.Contains("Aucune amélioration")));
});
Test("Progress does not leak between characters", () =>
{
    var store = new ProgressStore(); store.GetOrCreate(123).Complete("alliance-loot",now);
    var first = Plan(Character(contentId:123),store.GetOrCreate(123));
    var second = Plan(Character(contentId:456),store.GetOrCreate(456));
    Equal(ObjectiveStatus.Completed,Find(first,"alliance-loot").Status);
    Equal(ObjectiveStatus.NeedsVerification,Find(second,"alliance-loot").Status);
    try { Plan(Character(contentId:456),store.GetOrCreate(123)); throw new Exception("Cross-character state accepted"); }
    catch (ArgumentException) { }
});
Test("Manual progress round-trips with provenance, timestamp and character", () =>
{
    var path = Path.Combine(Path.GetTempPath(),$"aether-compass-tests-{Guid.NewGuid():N}.json");
    try
    {
        var store = new ProgressStore(); var p = store.GetOrCreate(123);
        p.Complete("alliance-loot",now); p.SetIncomplete("alliance-coin",now); p.ManualUnlocks["windurst"] = KnowledgeState.Yes;
        store.GetOrCreate(456).Complete("heavyweight-holoblade",now);
        store.Save(path); var loaded = ProgressStore.Load(path); var loadedProgress = loaded.GetOrCreate(123);
        Equal(2,loaded.Characters.Count);
        Equal(now,loadedProgress.Completions["alliance-loot"].CompletedAt);
        Equal(CompletionProvenance.Manual,loadedProgress.Completions["alliance-loot"].Provenance);
        Equal(KnowledgeState.No,loadedProgress.ManualActivityStates["alliance-coin"].State);
        Equal(KnowledgeState.Yes,loadedProgress.ManualUnlocks["windurst"]);
        Equal(ObjectiveStatus.Completed,Find(Plan(Character(),loadedProgress),"alliance-loot").Status);
        Check(!loadedProgress.Completions.ContainsKey("heavyweight-holoblade"));
    }
    finally { if (File.Exists(path)) File.Delete(path); }
});
Test("Future completion timestamps cannot hide objectives", () =>
{
    var p = Progress(); p.Complete("alliance-loot",now.AddHours(1));
    Equal(ObjectiveStatus.NeedsVerification,Find(Plan(Character(),p),"alliance-loot").Status);
});
Test("Malformed null progress is rejected without changing the file", () =>
{
    var path = Path.Combine(Path.GetTempPath(),$"aether-compass-corrupt-{Guid.NewGuid():N}.json");
    var malformedSamples = new[] { "{\"Characters\":null}", "{\"Characters\":{\"123\":null}}", "{\"Characters\":{\"123\":{\"ContentId\":123,\"Completions\":null}}}", "{\"Characters\":{\"123\":{\"ContentId\":123,\"ManualUnlocks\":{\"expert\":7}}}}" };
    try
    {
        foreach (var contents in malformedSamples)
        {
            File.WriteAllText(path,contents);
            try { ProgressStore.Load(path); throw new Exception("Malformed state accepted"); }
            catch (InvalidDataException) { }
            Equal(contents,File.ReadAllText(path));
        }
    }
    finally { if (File.Exists(path)) File.Delete(path); }
});
Test("Catalogue references patch dates and preserves reward restrictions", () =>
{
    Check(ObjectiveCatalog.All.All(x => x.SourceUrls.Count>0 && x.SourceUrls.All(y => y.StartsWith("https://"))));
    Equal(ObjectiveCatalog.All.Count,ObjectiveCatalog.All.Select(x => x.Id).Distinct().Count());
    Equal(ResetCadence.Weekly,Def("heavyweight-holoblade").Cadence);
    Equal(ResetCadence.None,Def("heavyweight-normal").Cadence);
    Check(Def("heavyweight-normal").Repeatable);
    Equal(795,Def("heavyweight-savage-4").RewardItemLevel!.Value);
});

var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}"); }
}
Console.WriteLine($"{tests.Count-failed}/{tests.Count} scenarios passed.");
return failed == 0 ? 0 : 1;
