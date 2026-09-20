using System;
using System.Collections.Generic;

namespace AetherCompass.Core;

public enum KnowledgeState { Unknown, No, Yes }
public enum PlayAmbition { Casual, Extreme, Savage }
public enum ObjectiveFocus { Balanced, Gearing, Story, Weekly }
public enum ObjectiveStatus { Available, NeedsVerification, Completed, Blocked }
public enum ResetCadence { None, Daily, Weekly }
public enum ProgressStage { Leveling, Fresh100, CatchUp, Endgame, Advanced, NonCombat, Unknown }
public enum CompletionProvenance { Manual, Observed, RewardObserved }
public enum GearSlotKind { Unknown, Weapon, Armor, Accessory }

public sealed record GearSlot(string Slot, string ItemName, uint ItemId, int ItemLevel,
    bool IsAppropriateForJob = true, GearSlotKind Kind = GearSlotKind.Unknown);

public sealed record CurrencyState
{
    public int? EarnedThisWeek { get; init; }
    public int? WeeklyCap { get; init; }
    public int? Stock { get; init; }
}

public sealed record CharacterSnapshot
{
    public ulong ContentId { get; init; }
    public string Name { get; init; } = "";
    public string Job { get; init; } = "";
    public uint JobId { get; init; }
    public int Level { get; init; }
    public bool? IsCombatJob { get; init; }
    public int? AverageItemLevel { get; init; }
    public IReadOnlyList<GearSlot> Gear { get; init; } = Array.Empty<GearSlot>();
    public IReadOnlyDictionary<string, KnowledgeState> Quests { get; init; } = new Dictionary<string, KnowledgeState>();
    public IReadOnlyDictionary<string, KnowledgeState> Unlocks { get; init; } = new Dictionary<string, KnowledgeState>();
    public IReadOnlyDictionary<string, KnowledgeState> Weekly { get; init; } = new Dictionary<string, KnowledgeState>();
    public CurrencyState Tomestones { get; init; } = new();
    public DateTimeOffset ObservedAt { get; init; }
}

public sealed record Preferences
{
    public PlayAmbition Ambition { get; init; } = PlayAmbition.Casual;
    public int SessionMinutes { get; init; } = 45;
    public ObjectiveFocus Focus { get; init; } = ObjectiveFocus.Balanced;
    public bool IncludeWondrousTails { get; init; }
}

public sealed record CompletionRecord(DateTimeOffset CompletedAt, CompletionProvenance Provenance);
public sealed record ActivityRecord(KnowledgeState State, DateTimeOffset At,
    CompletionProvenance Provenance = CompletionProvenance.Manual);
public sealed record RewardEvidence(DateTimeOffset ObservedAt, uint ItemId, string ItemName,
    uint TerritoryId, string DutyName, string Source);

public sealed class CharacterProgress
{
    public ulong ContentId { get; set; }
    public Dictionary<string, CompletionRecord> Completions { get; set; } = new();
    public Dictionary<string, ActivityRecord> ManualActivityStates { get; set; } = new();
    public Dictionary<string, KnowledgeState> ManualUnlocks { get; set; } = new();
    public Dictionary<string, RewardEvidence> ObservedRewards { get; set; } = new();

    /// <summary>
    /// Records the first valid reward observed in the current weekly period. The adapter is
    /// responsible for matching the acquired item and duty to the objective before calling.
    /// Historical evidence survives manual edits and is replaced only in a new period.
    /// </summary>
    public bool ObserveReward(string objectiveId, RewardEvidence evidence, DateTimeOffset? now = null)
    {
        var observedNow = now ?? DateTimeOffset.UtcNow;
        if (ContentId == 0 || string.IsNullOrWhiteSpace(objectiveId) || !IsValidRewardEvidence(evidence)
            || !ObjectiveCatalog.All.Any(x => x.Id == objectiveId && x.Cadence == ResetCadence.Weekly && !x.Repeatable)
            || !ResetClock.IsCurrent(evidence.ObservedAt, observedNow, ResetCadence.Weekly))
            return false;
        if (ObservedRewards.TryGetValue(objectiveId, out var previous) && IsValidRewardEvidence(previous)
            && ResetClock.IsCurrent(previous.ObservedAt, observedNow, ResetCadence.Weekly))
            return false;
        ObservedRewards[objectiveId] = evidence;
        return true;
    }

    internal static bool IsValidRewardEvidence(RewardEvidence? evidence)
        => evidence is not null && evidence.ObservedAt != default && evidence.ItemId != 0 && evidence.TerritoryId != 0
            && !string.IsNullOrWhiteSpace(evidence.ItemName) && !string.IsNullOrWhiteSpace(evidence.DutyName)
            && !string.IsNullOrWhiteSpace(evidence.Source);

    public void Complete(string objectiveId, DateTimeOffset at, CompletionProvenance provenance = CompletionProvenance.Manual)
    {
        if (provenance == CompletionProvenance.RewardObserved)
            throw new ArgumentException("Observed rewards require their own evidence record.", nameof(provenance));
        Completions[objectiveId] = new CompletionRecord(at, provenance);
        ManualActivityStates[objectiveId] = new ActivityRecord(KnowledgeState.Yes, at, provenance);
    }

    public void SetIncomplete(string objectiveId, DateTimeOffset at)
    {
        Completions.Remove(objectiveId);
        ManualActivityStates[objectiveId] = new ActivityRecord(KnowledgeState.No, at);
    }

    public void ClearCompletion(string objectiveId)
    {
        Completions.Remove(objectiveId);
        ManualActivityStates.Remove(objectiveId);
    }
}

public sealed record ObjectiveDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public int MinimumLevel { get; init; } = 100;
    public int MinimumItemLevel { get; init; }
    public int? RewardItemLevel { get; init; }
    public IReadOnlyList<GearSlotKind> RewardSlots { get; init; } = [GearSlotKind.Weapon, GearSlotKind.Armor, GearSlotKind.Accessory];
    public int Minutes { get; init; }
    public PlayAmbition MinimumAmbition { get; init; }
    public ObjectiveFocus Focus { get; init; }
    public ResetCadence Cadence { get; init; }
    public string? UnlockKey { get; init; }
    public string? RequiredQuestKey { get; init; }
    public int Priority { get; init; }
    public bool Optional { get; init; }
    public bool Repeatable { get; init; }
    public string Patch { get; init; } = "7.56";
    public DateOnly VerifiedDate { get; init; } = new(2026, 9, 20);
    public IReadOnlyList<string> SourceUrls { get; init; } = Array.Empty<string>();
}

public sealed record ObjectiveRecommendation(ObjectiveDefinition Objective, int Score,
    ObjectiveStatus Status, IReadOnlyList<string> Reasons, IReadOnlyList<string> Blockers,
    CompletionProvenance? CompletionSource = null);

public sealed record PlanResult(ProgressStage Stage, string StageExplanation,
    IReadOnlyList<ObjectiveRecommendation> Recommendations,
    IReadOnlyList<ObjectiveRecommendation> Blocked,
    IReadOnlyList<ObjectiveRecommendation> Completed,
    GearSlot? WeakestSlot, IReadOnlyList<string> DataWarnings);
