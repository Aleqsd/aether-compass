namespace AetherCompass.Core;

/// <summary>Deterministic recommendations; missing evidence is never a negative observation.</summary>
public sealed class PlannerEngine
{
    public PlanResult Evaluate(CharacterSnapshot snapshot, CharacterProgress progress, Preferences preferences, DateTimeOffset now)
    {
        if (snapshot.ContentId == 0 || snapshot.ContentId != progress.ContentId)
            throw new ArgumentException("Snapshot and progress must belong to the same logged-in character.");

        var warnings = new List<string>();
        if (snapshot.AverageItemLevel is null) warnings.Add("Niveau d'objet moyen non lu : les seuils d'accès restent à vérifier.");
        if (snapshot.Tomestones.EarnedThisWeek is null) warnings.Add("Gains hebdomadaires non lus : le stock ne permet pas de les reconstituer.");
        if (snapshot.Gear.Count == 0) warnings.Add("Pièces équipées non lues : les améliorations par emplacement restent à vérifier.");
        if (snapshot.IsCombatJob is null) warnings.Add("Type de job non déterminé : choisir un job de combat pour un plan fiable.");
        if (snapshot.Gear.Any(x => !x.IsAppropriateForJob)) warnings.Add("Une pièce ne correspond pas au job actif ; vérifier le set équipé.");
        if (snapshot.Level <= 0) warnings.Add("Niveau réel du job non lu.");
        if (snapshot.ObservedAt > now) warnings.Add("Horodatage d'observation dans le futur : les compteurs périodiques ne sont pas utilisés.");

        var weakest = snapshot.Gear.Where(x => (x.ItemLevel > 0 || x.ItemId == 0) && x.IsAppropriateForJob)
            .OrderBy(x => x.ItemLevel).ThenBy(x => x.Slot, StringComparer.Ordinal).FirstOrDefault();
        var all = new List<ObjectiveRecommendation>();
        foreach (var objective in ObjectiveCatalog.All)
        {
            if (objective.Optional && !preferences.IncludeWondrousTails) continue;
            var reasons = new List<string>();
            var blockers = new List<string>();
            var verification = false;
            var completion = GetCompletionState(objective, snapshot, progress, now);
            var completionSource = GetCompletionSource(objective, snapshot, progress, now);

            if (!objective.Repeatable && completion == KnowledgeState.Yes)
            {
                reasons.Add(completionSource == CompletionProvenance.Manual ? "Terminé selon votre suivi manuel." : "Terminé selon les données observées.");
                all.Add(new ObjectiveRecommendation(objective, 0, ObjectiveStatus.Completed, reasons, blockers, completionSource));
                continue;
            }

            if (snapshot.IsCombatJob == false) blockers.Add("Passer sur un job de combat pour ce plan de progression.");
            else if (snapshot.IsCombatJob is null) { verification = true; reasons.Add("Type de job inconnu."); }
            if (snapshot.Level > 0 && snapshot.Level < objective.MinimumLevel)
                blockers.Add($"Niveau {objective.MinimumLevel} requis ; niveau réel {snapshot.Level}.");
            else if (snapshot.Level <= 0) { verification = true; reasons.Add("Niveau réel à vérifier."); }
            if (preferences.Ambition < objective.MinimumAmbition)
                blockers.Add($"Objectif réservé au profil {objective.MinimumAmbition} ou supérieur.");
            if (preferences.SessionMinutes < objective.Minutes)
                blockers.Add($"Environ {objective.Minutes} min à prévoir ; session de {preferences.SessionMinutes} min.");

            if (objective.MinimumItemLevel > 0)
            {
                if (snapshot.AverageItemLevel is null) { verification = true; reasons.Add($"Accès i{objective.MinimumItemLevel} : iLv actuel inconnu."); }
                else if (snapshot.AverageItemLevel < objective.MinimumItemLevel)
                    blockers.Add($"i{objective.MinimumItemLevel} requis ; i{snapshot.AverageItemLevel} équipé.");
                else reasons.Add($"Seuil d'entrée i{objective.MinimumItemLevel} atteint.");
                if (snapshot.Gear.Any(x => !x.IsAppropriateForJob)) blockers.Add("Corriger les pièces incompatibles avec le job avant cet objectif.");
            }

            if (objective.RequiredQuestKey is { } questKey)
                CheckGate(ResolveFact(snapshot.Quests, progress, questKey), $"Épopée/prérequis « {questKey} »", blockers, reasons, ref verification);
            if (objective.UnlockKey is { } unlockKey && !objective.Id.StartsWith("unlock-", StringComparison.Ordinal))
                CheckGate(ResolveFact(snapshot.Unlocks, progress, unlockKey), $"Accès « {unlockKey} »", blockers, reasons, ref verification);

            if (!objective.Repeatable && completion == KnowledgeState.Unknown)
            {
                verification = true;
                reasons.Add(objective.Cadence == ResetCadence.None
                    ? "Progression non confirmée ; vérifier le journal ou compléter le suivi manuel."
                    : "État de la période inconnu : vérifier en jeu ou renseigner le suivi manuel.");
            }
            else if (!objective.Repeatable && completion == KnowledgeState.No)
                reasons.Add(completionSource == CompletionProvenance.Manual ? "À faire selon votre suivi manuel de la période." : "Non terminé selon les données observées.");

            if (objective.Id == "mnemonics-spend")
            {
                if (snapshot.Tomestones.Stock is null) { verification = true; reasons.Add("Stock de mémoquartz non lu."); }
                else if (snapshot.Tomestones.Stock <= 0) blockers.Add("Aucun mémoquartz en stock pour un échange.");
                else reasons.Add($"{snapshot.Tomestones.Stock} mémoquartz en stock ; vérifier les coûts exacts chez le vendeur.");
            }
            if (objective.Id == "mnemonics-cap")
            {
                if (snapshot.Tomestones.Stock >= 2000) blockers.Add("Stock au maximum (2 000) : dépenser avant de chercher à gagner d'autres mémoquartz.");
                if (snapshot.Tomestones.EarnedThisWeek is { } earned && snapshot.Tomestones.WeeklyCap is > 0 and var cap && ResetClock.IsCurrent(snapshot.ObservedAt, now, ResetCadence.Weekly))
                    reasons.Add($"Gains : {earned}/{cap}, reste {Math.Max(0, cap-earned)} cette semaine. Le stock est indépendant.");
                else reasons.Add("Plafond de référence 7.56 : 900 ; progression personnelle non déduite du stock.");
                if (snapshot.Tomestones.Stock is >= 1800 and < 2000) reasons.Add($"Stock presque plein : capacité restante {2000-snapshot.Tomestones.Stock} avant de dépenser.");
            }

            var score = objective.Priority;
            if (preferences.Focus != ObjectiveFocus.Balanced && preferences.Focus == objective.Focus) { score += 20; reasons.Add("Correspond à votre priorité."); }
            if (objective.Id == "msq" && ResolveFact(snapshot.Quests, progress, "dawntrail") == KnowledgeState.No)
            {
                score += 30;
                reasons.Add("L'épopée ouvre les activités de niveau 100.");
            }
            if (objective.Id == "gear-review")
            {
                if (weakest is null)
                {
                    verification = true;
                    reasons.Add("Équipement compatible non lu : vérifier le set avant de chercher les améliorations.");
                }
                else
                {
                    reasons.Add($"Emplacement le plus bas parmi les pièces lues : {weakest.Slot}, i{weakest.ItemLevel} ({weakest.ItemName}).");
                    if (snapshot.AverageItemLevel is { } average && weakest.ItemLevel >= average-5 && !snapshot.Gear.Any(x => !x.IsAppropriateForJob))
                    {
                        score -= 80;
                        reasons.Add("Pas d'écart marqué parmi les emplacements lus : privilégier les objectifs et échanges ciblés.");
                    }
                }
            }
            var rewardCandidate = snapshot.Gear.Where(x => x.IsAppropriateForJob && (x.ItemLevel > 0 || x.ItemId == 0) && objective.RewardSlots.Contains(x.Kind))
                .OrderBy(x => x.ItemLevel).FirstOrDefault();
            if (objective.RewardItemLevel is { } reward && rewardCandidate is not null)
            {
                if (rewardCandidate.ItemLevel < reward) { score += Math.Min(15, (reward-rewardCandidate.ItemLevel)/5); reasons.Add($"Récompense i{reward} à comparer à {rewardCandidate.Slot} i{rewardCandidate.ItemLevel} ; disponibilité et stats à confirmer."); }
                else
                {
                    score -= 60;
                    reasons.Add($"Aucun gain d'iLv évident : les pièces admissibles connues sont déjà i{reward} ou plus.");
                    if (objective.Id == "gear-catchup") blockers.Add("Aucune amélioration d'iLv identifiée pour les emplacements lus via ce rattrapage.");
                }
            }
            else if (objective.RewardItemLevel is { } unknownReward) reasons.Add($"Récompense i{unknownReward} ; emplacements admissibles non lus, gain à vérifier.");
            if (objective.Id == "mnemonics-spend" && snapshot.Tomestones.Stock >= 1800) { score += 30; reasons.Add("Libérer de la capacité avant les prochains gains."); }
            if (objective.Cadence != ResetCadence.None && ResetClock.NextReset(now, objective.Cadence) is { } reset)
            {
                reasons.Add($"Prochain reset : {reset:dd/MM/yyyy HH:mm} UTC.");
                if (reset-now < TimeSpan.FromHours(24)) score += objective.Cadence == ResetCadence.Weekly ? 15 : 5;
            }
            if (objective.MinimumAmbition > PlayAmbition.Casual) reasons.Add("Le profil choisi exprime votre envie ; il ne mesure pas votre maîtrise du combat.");
            if (verification) score -= 25;
            var status = blockers.Count > 0 ? ObjectiveStatus.Blocked : verification ? ObjectiveStatus.NeedsVerification : ObjectiveStatus.Available;
            all.Add(new ObjectiveRecommendation(objective, score, status, reasons, blockers, completionSource));
        }

        var stage = GetStage(snapshot);
        return new PlanResult(stage, ExplainStage(stage),
            all.Where(x => x.Status is ObjectiveStatus.Available or ObjectiveStatus.NeedsVerification).OrderByDescending(x => x.Score).ThenBy(x => x.Objective.Id, StringComparer.Ordinal).ToArray(),
            all.Where(x => x.Status == ObjectiveStatus.Blocked).OrderByDescending(x => x.Score).ToArray(),
            all.Where(x => x.Status == ObjectiveStatus.Completed).ToArray(), weakest, warnings);
    }

    public static KnowledgeState GetCompletionState(ObjectiveDefinition objective, CharacterSnapshot snapshot, CharacterProgress progress, DateTimeOffset now)
    {
        EnsureSameCharacter(snapshot, progress);
        if (objective.Repeatable) return KnowledgeState.Unknown;
        var live = LiveCompletion(objective, snapshot, progress, now);
        if (live != KnowledgeState.Unknown) return live;
        if (progress.ManualActivityStates.TryGetValue(objective.Id, out var activity) && ResetClock.IsCurrent(activity.At, now, objective.Cadence)) return activity.State;
        if (progress.Completions.TryGetValue(objective.Id, out var completion) && ResetClock.IsCurrent(completion.CompletedAt, now, objective.Cadence)) return KnowledgeState.Yes;
        return KnowledgeState.Unknown;
    }

    private static CompletionProvenance? GetCompletionSource(ObjectiveDefinition objective, CharacterSnapshot snapshot, CharacterProgress progress, DateTimeOffset now)
    {
        if (objective.Repeatable) return null;
        var liveWithoutManual = LiveCompletion(objective, snapshot, new CharacterProgress { ContentId = snapshot.ContentId }, now);
        if (liveWithoutManual != KnowledgeState.Unknown) return CompletionProvenance.Observed;
        if (LiveCompletion(objective, snapshot, progress, now) != KnowledgeState.Unknown) return CompletionProvenance.Manual;
        if (progress.ManualActivityStates.TryGetValue(objective.Id, out var activity) && ResetClock.IsCurrent(activity.At, now, objective.Cadence)) return activity.Provenance;
        if (progress.Completions.TryGetValue(objective.Id, out var completion) && ResetClock.IsCurrent(completion.CompletedAt, now, objective.Cadence)) return completion.Provenance;
        return null;
    }

    private static KnowledgeState LiveCompletion(ObjectiveDefinition objective, CharacterSnapshot snapshot, CharacterProgress progress, DateTimeOffset now)
    {
        if (objective.Id == "msq") return ResolveFact(snapshot.Quests, progress, "current-msq");
        if (objective.Id.StartsWith("unlock-", StringComparison.Ordinal) && objective.UnlockKey is { } key) return ResolveFact(snapshot.Unlocks, progress, key);
        if (!ResetClock.IsCurrent(snapshot.ObservedAt, now, objective.Cadence)) return KnowledgeState.Unknown;
        if (objective.Id == "mnemonics-cap")
        {
            if (snapshot.Tomestones.EarnedThisWeek is not { } earned || snapshot.Tomestones.WeeklyCap is not > 0) return KnowledgeState.Unknown;
            return earned >= snapshot.Tomestones.WeeklyCap ? KnowledgeState.Yes : KnowledgeState.No;
        }
        return snapshot.Weekly.GetValueOrDefault(objective.Id, KnowledgeState.Unknown);
    }

    private static KnowledgeState ResolveFact(IReadOnlyDictionary<string, KnowledgeState> live, CharacterProgress progress, string key)
    {
        var observed = live.GetValueOrDefault(key, KnowledgeState.Unknown);
        return observed != KnowledgeState.Unknown ? observed : progress.ManualUnlocks.GetValueOrDefault(key, KnowledgeState.Unknown);
    }

    private static void CheckGate(KnowledgeState state, string label, List<string> blockers, List<string> reasons, ref bool verification)
    {
        if (state == KnowledgeState.No) blockers.Add($"{label} non débloqué.");
        if (state == KnowledgeState.Unknown) { verification = true; reasons.Add($"{label} non vérifié."); }
    }

    private static void EnsureSameCharacter(CharacterSnapshot snapshot, CharacterProgress progress)
    {
        if (snapshot.ContentId == 0 || snapshot.ContentId != progress.ContentId)
            throw new ArgumentException("Snapshot and progress must belong to the same logged-in character.");
    }

    private static ProgressStage GetStage(CharacterSnapshot snapshot)
    {
        if (snapshot.IsCombatJob == false) return ProgressStage.NonCombat;
        if (snapshot.Level <= 0) return ProgressStage.Unknown;
        if (snapshot.Level < 100) return ProgressStage.Leveling;
        return snapshot.AverageItemLevel switch { null => ProgressStage.Unknown, <735 => ProgressStage.Fresh100, <760 => ProgressStage.CatchUp, <780 => ProgressStage.Endgame, _ => ProgressStage.Advanced };
    }

    private static string ExplainStage(ProgressStage stage) => stage switch
    {
        ProgressStage.Leveling => "Avant le niveau 100 : avancer l'épopée et le job, puis préparer les accès.",
        ProgressStage.Fresh100 => "Début niveau 100 : terminer l'épopée, vérifier le set et combler les pièces faibles avant les seuils d'entrée.",
        ProgressStage.CatchUp => "Rattrapage : ouvrir les contenus accessibles et améliorer les emplacements qui limitent l'iLv moyen.",
        ProgressStage.Endgame => "Fin de jeu : arbitrer les récompenses hebdomadaires et l'équipement selon les accès et le temps disponible.",
        ProgressStage.Advanced => "Progression avancée : privilégier les améliorations ciblées, puis Extreme ou Savage selon votre préférence.",
        ProgressStage.NonCombat => "Job d'artisanat ou de récolte : sélectionner un job de combat pour les objectifs de fin de jeu couverts ici.",
        _ => "Données insuffisantes pour déterminer un palier fiable.",
    };
}
