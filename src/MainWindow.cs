using System.Numerics;
using AetherCompass.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AetherCompass;

public sealed class MainWindow : Window
{
    private static readonly Vector4 Accent = new(.40f, .84f, .79f, 1);
    private static readonly Vector4 Muted = new(.65f, .69f, .74f, 1);
    private static readonly Vector4 Amber = new(.98f, .76f, .42f, 1);
    private readonly Configuration config;
    private readonly ProgressStore store;
    private readonly Action save;
    private readonly PlannerEngine engine = new();
    private CharacterSnapshot? snapshot;
    private PlanResult? plan;
    private CharacterProgress? progress;
    private string? readError;
    public string? SaveError { get; set; }
    public bool RequestWeekly { get; set; }

    public MainWindow(Configuration config, ProgressStore store, Action save) : base("Aether Compass###AetherCompass")
    {
        this.config = config; this.store = store; this.save = save;
        Size = new Vector2(790, 650);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(590, 410), MaximumSize = new Vector2(float.MaxValue) };
    }

    public void SetSnapshot(CharacterSnapshot? value, string? error)
    {
        snapshot = value; readError = error;
        progress = value is { ContentId: > 0 } ? store.GetOrCreate(value.ContentId) : null;
        Replan();
    }

    private void Replan() => plan = snapshot is not null && progress is not null
        ? engine.Evaluate(snapshot, progress, config.Preferences, DateTimeOffset.UtcNow) : null;

    public override void Draw()
    {
        ImGui.TextColored(Accent, "AETHER COMPASS");
        ImGui.SameLine(); ImGui.TextColored(Muted, "  Ta prochaine étape en Éorzéa");
        if (SaveError is not null) ImGui.TextWrapped(SaveError);
        if (snapshot is null || plan is null || progress is null)
        {
            ImGui.Spacing(); ImGui.TextWrapped(readError is null ? "Connecte-toi à un personnage pour analyser sa progression." : "Analyse temporairement en pause.");
            if (!string.IsNullOrWhiteSpace(readError)) ImGui.TextWrapped(readError);
            ImGui.TextColored(Muted, "Aucune donnée d'un autre joueur n'est analysée.");
            return;
        }
        ImGui.TextUnformatted($"{snapshot.Name}  ·  {snapshot.Job} {snapshot.Level}  ·  iLvl {snapshot.AverageItemLevel?.ToString() ?? "?"}");
        ImGui.TextWrapped(plan.StageExplanation);
        ImGui.Spacing();
        var currency = snapshot.Tomestones;
        var earned = currency.EarnedThisWeek;
        var cap = currency.WeeklyCap;
        var caption = earned.HasValue && cap is > 0 ? $"Mémoquartz limités : {earned} / {cap} cette semaine" : "Mémoquartz limités : acquisition hebdomadaire inconnue";
        ImGui.ProgressBar(earned.HasValue && cap is > 0 ? Math.Clamp((float)earned.Value / cap.Value, 0, 1) : 0, new Vector2(-1, ImGui.GetFrameHeight()), caption);
        var reset = NextWeekly(DateTimeOffset.UtcNow);
        var remaining = reset - DateTimeOffset.UtcNow;
        ColoredWrapped(Muted, $"Reset : mardi 08:00 UTC · dans {(int)remaining.TotalDays} j {remaining.Hours} h · stock : {currency.Stock?.ToString() ?? "?"}");
        ImGui.Separator();
        if (!ImGui.BeginTabBar("CompassTabs")) return;
        if (ImGui.BeginTabItem("Objectifs")) { DrawGoals(); ImGui.EndTabItem(); }
        var weeklyFlags = RequestWeekly ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        if (ImGui.BeginTabItem("Hebdomadaire", weeklyFlags)) { RequestWeekly = false; DrawWeekly(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Équipement")) { DrawGear(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Progression")) { DrawUnlocks(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Préférences")) { DrawPreferences(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    private void DrawGoals()
    {
        ImGui.Spacing();
        if (plan!.Recommendations.Count == 0) ImGui.TextWrapped("Aucun objectif disponible dans ce profil. Vérifie les prérequis et les préférences ci-dessous.");
        var rank = 0;
        foreach (var goal in plan.Recommendations)
        {
            ImGui.PushID(goal.Objective.Id);
            ImGui.TextColored(Accent, $"{++rank:00}"); ImGui.SameLine();
            ImGui.TextWrapped(goal.Objective.Title);
            ImGui.TextColored(goal.Status == ObjectiveStatus.NeedsVerification ? Amber : Muted,
                $"{StatusLabel(goal.Status)} · environ {goal.Objective.Minutes} min{(goal.Objective.RewardItemLevel is { } il ? $" · récompense i{il}" : "")}");
            ImGui.TextWrapped(goal.Objective.Description);
            foreach (var reason in goal.Reasons) ImGui.TextWrapped($"• {reason}");
            foreach (var blocker in goal.Blockers) ColoredWrapped(Amber, blocker);
            if (ImGui.CollapsingHeader("Détails et suivi"))
            {
                DrawActivityControls(goal);
                ImGui.TextWrapped($"Référence : patch {goal.Objective.Patch}, vérifié le {goal.Objective.VerifiedDate:dd/MM/yyyy}.");
                foreach (var source in goal.Objective.SourceUrls) ImGui.TextWrapped(source);
            }
            ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing(); ImGui.PopID();
        }
        if (ImGui.CollapsingHeader($"Prérequis et objectifs écartés ({plan.Blocked.Count})"))
        {
            foreach (var goal in plan.Blocked)
            {
                ImGui.TextWrapped(goal.Objective.Title);
                foreach (var blocker in goal.Blockers) ImGui.TextWrapped($"  • {blocker}");
                ImGui.Spacing();
            }
        }
        if (ImGui.CollapsingHeader($"Déjà terminé ({plan.Completed.Count})"))
            foreach (var goal in plan.Completed) { ImGui.PushID(goal.Objective.Id); ImGui.TextWrapped(goal.Objective.Title); DrawActivityControls(goal); ImGui.PopID(); }
        if (ImGui.CollapsingHeader("Fiabilité des données")) DrawDataNotes();
    }

    private IEnumerable<ObjectiveRecommendation> AllGoals() => plan!.Recommendations.Concat(plan.Blocked).Concat(plan.Completed).DistinctBy(x => x.Objective.Id);

    private void DrawWeekly()
    {
        ImGui.Spacing(); ImGui.TextWrapped("Le butin et la récompense de complétion sont suivis séparément. Une donnée inconnue ne signifie pas que l'activité reste à faire.");
        foreach (var goal in AllGoals().Where(x => x.Objective.Cadence == ResetCadence.Weekly).OrderBy(x => x.Objective.Id).ToArray())
        {
            ImGui.PushID(goal.Objective.Id);
            var state = PlannerEngine.GetCompletionState(goal.Objective, snapshot!, progress!, DateTimeOffset.UtcNow);
            ImGui.TextColored(state == KnowledgeState.Yes ? Accent : state == KnowledgeState.Unknown ? Amber : Muted,
                state == KnowledgeState.Yes ? "FAIT" : state == KnowledgeState.No ? "À FAIRE" : "INCONNU");
            ImGui.SameLine(); ImGui.TextWrapped(goal.Objective.Title);
            var live = goal.CompletionSource == CompletionProvenance.Observed;
            ImGui.TextColored(Muted, live ? "Source : jeu" : goal.CompletionSource == CompletionProvenance.Manual && state != KnowledgeState.Unknown ? "Source : déclaration manuelle" : "Source : non vérifiée pour cette semaine");
            foreach (var blocker in goal.Blockers) ImGui.TextWrapped(blocker);
            DrawActivityControls(goal);
            ImGui.Separator(); ImGui.PopID();
        }
        ImGui.Spacing(); ImGui.TextWrapped("Savage Heavyweight : répétable dans le catalogue 7.56, sans verrou hebdomadaire. Le carnet de Khloe est facultatif dans les préférences.");
    }

    private void DrawActivityControls(ObjectiveRecommendation goal)
    {
        if (goal.Objective.Repeatable)
        {
            ImGui.TextColored(Muted, "Répétable : réévaluation selon ton équipement et ta progression.");
            return;
        }
        if (goal.Objective.Id == "mnemonics-cap")
        {
            ImGui.TextWrapped("Le plafond dépend du compteur d'acquisition en jeu ; dépenser des mémoquartz ne le remet pas à zéro.");
            return;
        }
        if (goal.CompletionSource == CompletionProvenance.Observed)
        {
            ImGui.TextColored(Muted, "État lu en jeu : le suivi se met à jour automatiquement.");
            return;
        }
        if (ImGui.SmallButton("Déclarer fait")) { progress!.Complete(goal.Objective.Id, DateTimeOffset.UtcNow); Changed(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("À faire")) { progress!.SetIncomplete(goal.Objective.Id, DateTimeOffset.UtcNow); Changed(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Effacer ma déclaration")) { progress!.ClearCompletion(goal.Objective.Id); Changed(); }
        ImGui.TextColored(Muted, "La lecture du jeu est prioritaire lorsqu'elle est disponible.");
    }

    private void DrawGear()
    {
        ImGui.Spacing();
        if (plan!.WeakestSlot is { } weakest) ImGui.TextWrapped($"À examiner en premier : {weakest.Slot} — {weakest.ItemName} (i{weakest.ItemLevel}).");
        ImGui.TextWrapped("L'iLvl repère les écarts d'équipement. Il ne calcule pas le meilleur équipement par statistiques, matérias ou vitesse.");
        if (ImGui.BeginTable("Gear", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Emplacement", ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableSetupColumn("Pièce équipée");
            ImGui.TableSetupColumn("iLvl", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableHeadersRow();
            foreach (var gear in snapshot!.Gear)
            {
                ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.TextUnformatted(gear.Slot);
                ImGui.TableNextColumn(); ImGui.TextWrapped(gear.ItemName);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(gear.ItemLevel.ToString());
            }
            ImGui.EndTable();
        }
        if (snapshot!.Gear.Count == 0) ImGui.TextColored(Amber, "Équipement non disponible pour le moment.");
        ImGui.Spacing();
        ImGui.TextWrapped("Repères d'accès : Mistwake i735 · normal Heavyweight i745 · Expert i750 · Windurst i755 · Extreme The Unmaking i770. Les quêtes et déblocages restent requis.");
    }

    private void DrawUnlocks()
    {
        ImGui.Spacing(); ImGui.TextWrapped("Déblocages et quêtes utilisés par le plan. Les noms des contenus servent à identifier précisément les activités. Une déclaration complète uniquement une lecture inconnue.");
        var keys = AllGoals().SelectMany(x => new[] { x.Objective.UnlockKey, x.Objective.RequiredQuestKey }).Where(x => x is not null).Cast<string>()
            .Concat(snapshot!.Quests.Keys).Concat(snapshot.Unlocks.Keys).Distinct().Order().ToArray();
        foreach (var key in keys)
        {
            ImGui.PushID(key);
            var known = snapshot.Quests.TryGetValue(key, out var q) ? q : snapshot.Unlocks.GetValueOrDefault(key);
            var manual = progress!.ManualUnlocks.GetValueOrDefault(key);
            ImGui.TextUnformatted(FriendlyKey(key));
            ImGui.TextColored(known == KnowledgeState.Unknown ? Amber : Muted,
                known == KnowledgeState.Yes ? "Jeu : terminé / débloqué" : known == KnowledgeState.No ? "Jeu : prérequis non rempli" : "Jeu : inconnu");
            if (known == KnowledgeState.Unknown)
            {
                var selection = (int)manual;
                ImGui.SetNextItemWidth(260);
                if (ImGui.Combo("##manual", ref selection, "Non renseigné\0Non terminé / verrouillé\0Terminé / débloqué\0"))
                { progress.ManualUnlocks[key] = (KnowledgeState)selection; Changed(); }
            }
            ImGui.Separator(); ImGui.PopID();
        }
        DrawDataNotes();
    }

    private void DrawPreferences()
    {
        ImGui.Spacing();
        var prefs = config.Preferences;
        var ambition = (int)prefs.Ambition;
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("Difficulté souhaitée", ref ambition, "Détente / normal\0Jusqu'à Extrême\0Jusqu'à Savage\0")) { config.Preferences = prefs with { Ambition = (PlayAmbition)ambition }; Changed(); prefs = config.Preferences; }
        var focus = (int)prefs.Focus;
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("Priorité", ref focus, "Équilibrée\0Équipement\0Histoire\0Hebdomadaire\0")) { config.Preferences = prefs with { Focus = (ObjectiveFocus)focus }; Changed(); prefs = config.Preferences; }
        var minutes = prefs.SessionMinutes;
        ImGui.SetNextItemWidth(260);
        if (ImGui.SliderInt("Temps disponible", ref minutes, 15, 180, "%d min")) { config.Preferences = prefs with { SessionMinutes = minutes }; Changed(); prefs = config.Preferences; }
        var wt = prefs.IncludeWondrousTails;
        if (ImGui.Checkbox("Inclure le carnet de Khloe", ref wt)) { config.Preferences = prefs with { IncludeWondrousTails = wt }; Changed(); }
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        ImGui.TextWrapped("Les durées sont des estimations hors file d'attente. La difficulté choisie exprime une préférence, pas une évaluation de ta maîtrise des combats.");
        ImGui.TextWrapped("Analyse locale du personnage connecté. Aucune donnée envoyée à un serveur. La progression manuelle est séparée par personnage ; les préférences sont communes au plugin.");
        ImGui.TextWrapped("Catalogue 7.56 · vérification documentaire du 20/09/2026. Si le jeu a changé de patch, vérifie les plafonds et restrictions dans ses notes officielles.");
        ImGui.TextWrapped("Version 0.1.0 expérimentale. Compilation et logique vérifiées hors jeu ; validation en jeu requise.");
        DrawDataNotes();
    }

    private void DrawDataNotes()
    {
        foreach (var warning in plan!.DataWarnings) ImGui.TextWrapped($"• {warning}");
        if (readError is not null) ImGui.TextWrapped(readError);
        ImGui.TextWrapped("Un changement de zone peut rendre certaines données temporairement indisponibles. Aucune supposition n'est faite sur les quêtes ou les weekly des autres joueurs.");
    }

    private void Changed() { save(); Replan(); }
    private static void ColoredWrapped(Vector4 color, string text) { ImGui.PushTextWrapPos(0); ImGui.TextColored(color, text); ImGui.PopTextWrapPos(); }
    private static string StatusLabel(ObjectiveStatus status) => status switch { ObjectiveStatus.Available => "Accessible", ObjectiveStatus.NeedsVerification => "À vérifier", ObjectiveStatus.Completed => "Terminé", _ => "Prérequis" };
    private static string FriendlyKey(string key) => key switch
    {
        "dawntrail" => "Épopée Dawntrail de base", "current-msq" => "Épopée actuelle (7.56)",
        "expert" => "Roulette Expert", "mistwake" => "Mistwake", "clyteum" => "The Clyteum",
        "heavyweight-normal" => "AAC Heavyweight — premier combat normal", "heavyweight" => "AAC Heavyweight — quatrième combat normal",
        "windurst" => "Windurst: The Third Walk", "unmaking-extreme" => "The Unmaking (Extreme)",
        "relic" => "Armes Phantom", "wondrous-tails" => "Carnet de Khloe", _ => key
    };
    private static DateTimeOffset NextWeekly(DateTimeOffset now)
    {
        var utc = now.UtcDateTime;
        var next = new DateTimeOffset(utc.Date.AddHours(8), TimeSpan.Zero).AddDays(((int)DayOfWeek.Tuesday - (int)utc.DayOfWeek + 7) % 7);
        return next <= now ? next.AddDays(7) : next;
    }
}
