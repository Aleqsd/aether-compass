namespace AetherCompass.Core;

public sealed record RewardRule(string ObjectiveId, uint ItemId, string ItemName, uint TerritoryId, string DutyName);
public sealed record RewardInventorySample(ulong ContentId, uint TerritoryId, DateTimeOffset At,
    IReadOnlyDictionary<uint, int> Counts);
public sealed record ObservedReward(string ObjectiveId, RewardEvidence Evidence);

/// <summary>
/// Correlates a positive inventory event with a sustained net gain inside its exact duty.
/// Counts cover all personal bags, armoury and equipped slots. A first snapshot is never a reward.
/// </summary>
public sealed class RewardInventoryTracker
{
    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Confirmation = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumGap = TimeSpan.FromSeconds(5);
    private readonly Dictionary<uint, RewardRule[]> rulesByTerritory;
    private readonly Dictionary<uint, int> highWater = new();
    private readonly Dictionary<uint, int> lastCounts = new();
    private readonly Dictionary<uint, PendingGain> pending = new();
    private RewardInventorySample? previous;
    private DateTimeOffset stableSince;
    public bool IsArmed { get; private set; }

    private sealed record PendingGain(DateTimeOffset At, DateTimeOffset StableSince, int Count);

    public RewardInventoryTracker(IReadOnlyList<RewardRule> rules)
    {
        if (rules.Any(x => x.ItemId == 0 || x.TerritoryId == 0 || string.IsNullOrWhiteSpace(x.ObjectiveId)
            || string.IsNullOrWhiteSpace(x.ItemName) || string.IsNullOrWhiteSpace(x.DutyName))
            || rules.GroupBy(x => (x.TerritoryId, x.ItemId)).Any(x => x.Count() != 1))
            throw new ArgumentException("Reward rules must identify one reward per item and territory.", nameof(rules));
        rulesByTerritory = rules.GroupBy(x => x.TerritoryId).ToDictionary(x => x.Key, x => x.ToArray());
    }

    public void Reset()
    {
        previous = null;
        highWater.Clear(); lastCounts.Clear(); pending.Clear();
        IsArmed = false;
    }

    public IReadOnlyList<ObservedReward> Observe(RewardInventorySample sample,
        IReadOnlySet<uint>? acquisitionCandidates = null, bool inventoryEvent = false)
    {
        if (sample.ContentId == 0 || !rulesByTerritory.TryGetValue(sample.TerritoryId, out var rules)
            || rules.Any(x => !sample.Counts.TryGetValue(x.ItemId, out var count) || count < 0))
        {
            Reset();
            return [];
        }
        if (previous is null || previous.ContentId != sample.ContentId || previous.TerritoryId != sample.TerritoryId
            || sample.At < previous.At || sample.At - previous.At > MaximumGap
            || ResetClock.PeriodStart(sample.At, ResetCadence.Weekly) != ResetClock.PeriodStart(previous.At, ResetCadence.Weekly))
        {
            Reset();
            foreach (var rule in rules) highWater[rule.ItemId] = lastCounts[rule.ItemId] = sample.Counts[rule.ItemId];
            stableSince = sample.At;
            previous = sample;
            return [];
        }
        previous = sample;
        if (!IsArmed)
        {
            foreach (var rule in rules)
            {
                var count = sample.Counts[rule.ItemId];
                if (count != lastCounts[rule.ItemId]) stableSince = sample.At;
                lastCounts[rule.ItemId] = count;
                highWater[rule.ItemId] = Math.Max(highWater[rule.ItemId], count);
            }
            IsArmed = sample.At - stableSince >= Warmup;
            return [];
        }

        var observed = new List<ObservedReward>();
        foreach (var rule in rules)
        {
            var count = sample.Counts[rule.ItemId];
            var baseline = highWater[rule.ItemId];
            if (count <= baseline) pending.Remove(rule.ItemId);
            // Events are candidates, not proof. A reconciled move/split/merge alone cannot start one.
            if (inventoryEvent && acquisitionCandidates?.Contains(rule.ItemId) == true && count > baseline
                && !pending.ContainsKey(rule.ItemId))
                pending[rule.ItemId] = new PendingGain(sample.At, sample.At, count);

            if (pending.TryGetValue(rule.ItemId, out var gain))
            {
                if (count != gain.Count)
                    pending[rule.ItemId] = gain = gain with { Count = count, StableSince = sample.At };
                if (sample.At - gain.StableSince >= Confirmation)
                {
                    observed.Add(new ObservedReward(rule.ObjectiveId,
                        new RewardEvidence(gain.At, rule.ItemId, rule.ItemName, rule.TerritoryId, rule.DutyName,
                            "Événement d'inventaire et gain net confirmé dans l'instance")));
                    highWater[rule.ItemId] = count;
                    pending.Remove(rule.ItemId);
                }
            }
            // Never lower this maximum: removing and restoring an old item is not a new reward.
            // Heartbeat-only samples do not consume a gain before its inventory event is delivered.
            else if (inventoryEvent) highWater[rule.ItemId] = Math.Max(baseline, count);
        }
        return observed;
    }
}
