using AetherCompass.Core;

internal static class RewardTrackerTests
{
    public static void Register(Action<string, Action> add, DateTimeOffset now)
    {
        RewardInventoryTracker Tracker() => new([
            new("alliance-coin", 10, "Coin", 100, "Alliance"),
            new("alliance-loot", 20, "Armor", 100, "Alliance"),
            new("heavyweight-holoblade", 30, "Blade", 200, "M4")]);
        RewardInventorySample Sample(double seconds, int coins = 3, int armor = 0, ulong character = 123, uint territory = 100)
            => new(character, territory, now.AddSeconds(seconds), new Dictionary<uint, int> { [10] = coins, [20] = armor, [30] = 0 });
        RewardInventoryTracker Armed()
        {
            var tracker = Tracker(); tracker.Observe(Sample(0)); tracker.Observe(Sample(2));
            Check(tracker.IsArmed); return tracker;
        }
        add("Reward observer ignores old possession and startup events", () =>
        {
            var t = Tracker();
            Check(t.Observe(Sample(0), new HashSet<uint> { 10 }, true).Count == 0);
            Check(t.Observe(Sample(2), new HashSet<uint> { 10 }, true).Count == 0);
            Check(t.Observe(Sample(3), new HashSet<uint> { 10 }, true).Count == 0);
        });
        add("Reward observer confirms added stack only after sustained net gain", () =>
        {
            var t = Armed();
            Check(t.Observe(Sample(3, 4), new HashSet<uint> { 10 }, true).Count == 0);
            var evidence = t.Observe(Sample(4, 4));
            Check(evidence.Count == 1 && evidence[0].ObjectiveId == "alliance-coin");
            Check(evidence[0].Evidence.ObservedAt == now.AddSeconds(3));
            Check(t.Observe(Sample(5, 4), new HashSet<uint> { 10 }, true).Count == 0);
            Check(t.Observe(Sample(6, 4)).Count == 0);
        });
        add("A heartbeat before the acquisition event does not consume its proof", () =>
        {
            var t = Armed(); t.Observe(Sample(2.9, 4));
            t.Observe(Sample(3, 4), new HashSet<uint> { 10 }, true);
            Check(t.Observe(Sample(4, 4)).Count == 1);
        });
        add("Inventory changes alone without a positive acquisition event stay unknown", () =>
        {
            var t = Armed(); t.Observe(Sample(3, 4), inventoryEvent: true);
            Check(t.Observe(Sample(4, 4)).Count == 0);
            t.Observe(Sample(5, 4), new HashSet<uint> { 10 }, true);
            Check(t.Observe(Sample(6, 4)).Count == 0);
        });
        add("Moves splits merges and equipping have zero aggregate gain", () =>
        {
            var t = Armed();
            for (var second = 3; second <= 5; second++)
                Check(t.Observe(Sample(second), new HashSet<uint> { 10 }, true).Count == 0);
        });
        add("Transient duplicated item during a move is not persisted", () =>
        {
            var t = Armed(); t.Observe(Sample(3, 4), new HashSet<uint> { 10 }, true);
            Check(t.Observe(Sample(3.2)).Count == 0);
            Check(t.Observe(Sample(4)).Count == 0);
        });
        add("Removing and restoring old stock cannot create a reward", () =>
        {
            var t = Armed(); t.Observe(Sample(3, 2), inventoryEvent: true);
            t.Observe(Sample(4), new HashSet<uint> { 10 }, true);
            Check(t.Observe(Sample(5)).Count == 0);
        });
        add("Coin and gear evidence are produced independently", () =>
        {
            var t = Armed(); t.Observe(Sample(3, armor: 1), new HashSet<uint> { 20 }, true);
            var evidence = t.Observe(Sample(4, armor: 1));
            Check(evidence.Count == 1 && evidence[0].ObjectiveId == "alliance-loot");
        });
        add("An unrelated item event cannot confirm a reward", () =>
        {
            var t = Armed(); t.Observe(Sample(3, 4), new HashSet<uint> { 999 }, true);
            Check(t.Observe(Sample(4, 4)).Count == 0);
        });
        add("Character and territory changes discard pending acquisitions", () =>
        {
            foreach (var change in new[] { Sample(4, 4, character: 456), Sample(4, 4, territory: 200), Sample(4, 4, territory: 999) })
            {
                var t = Armed(); t.Observe(Sample(3, 4), new HashSet<uint> { 10 }, true);
                Check(t.Observe(change).Count == 0 && !t.IsArmed);
            }
        });
        add("Logout unavailable inventory and long observation gaps discard pending proof", () =>
        {
            var invalid = new[] { Sample(4, 4, character: 0), Sample(4, 4) with { Counts = new Dictionary<uint, int>() }, Sample(9, 4) };
            foreach (var sample in invalid)
            {
                var t = Armed(); t.Observe(Sample(3, 4), new HashSet<uint> { 10 }, true);
                Check(t.Observe(sample).Count == 0 && !t.IsArmed);
            }
            var reset = Armed(); reset.Reset();
            Check(reset.Observe(Sample(3, 4), new HashSet<uint> { 10 }, true).Count == 0);
        });
        add("Weekly reset cannot attribute an earlier pending acquisition to the next week", () =>
        {
            var boundary = new DateTimeOffset(2026, 9, 22, 8, 0, 0, TimeSpan.Zero);
            var t = Tracker();
            t.Observe(Sample(0) with { At = boundary.AddSeconds(-4) });
            t.Observe(Sample(0) with { At = boundary.AddSeconds(-2) });
            t.Observe(Sample(0, 4) with { At = boundary.AddMilliseconds(-500) }, new HashSet<uint> { 10 }, true);
            Check(t.Observe(Sample(0, 4) with { At = boundary.AddMilliseconds(500) }).Count == 0);
            Check(!t.IsArmed);
        });
        add("Changing startup quantities restarts the stable baseline window", () =>
        {
            var t = Tracker(); t.Observe(Sample(0));
            t.Observe(Sample(1, 4), new HashSet<uint> { 10 }, true);
            Check(t.Observe(Sample(2, 4)).Count == 0 && !t.IsArmed);
            Check(t.Observe(Sample(3, 4)).Count == 0 && t.IsArmed);
        });
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Reward observation assertion failed.");
    }
}
