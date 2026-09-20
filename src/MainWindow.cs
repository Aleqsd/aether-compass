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
    private bool folded;
    private bool restoreSize;
    private Vector2 expandedSize;
    private float headerHeight = 50;
    private int selectedView;
    private bool settings;
    private bool showAllGoals;
    private string? expandedGoal;
    public string? SaveError { get; set; }
    public bool RequestWeekly { get; set; }

    public MainWindow(Configuration config, ProgressStore store, Action save) : base("Aether Compass###AetherCompass")
    {
        this.config = config; this.store = store; this.save = save;
        Size = new Vector2(580, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(520, 340), MaximumSize = new Vector2(float.MaxValue) };
    }

    public void Expand() { if (folded) { folded = false; restoreSize = true; } }
    public override void PreDraw()
    {
        CompassTheme.Push();
        Flags = CompassTheme.Flags(config.Locked, folded);
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(520, folded ? 40 : 340), MaximumSize = new Vector2(float.MaxValue) };
        if (folded) ImGui.SetNextWindowSize(new Vector2(expandedSize.X, headerHeight));
        else if (restoreSize) { ImGui.SetNextWindowSize(expandedSize); restoreSize = false; }
    }
    public override void PostDraw() => CompassTheme.Pop();

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
        DrawHeader();
        if (folded) return;
        if (SaveError is not null) ImGui.TextWrapped(SaveError);
        if (snapshot is null || plan is null || progress is null)
        {
            if (settings) { DrawPreferences(); return; }
            ImGui.Spacing(); ImGui.TextWrapped(readError is null ? "Connecte-toi à un personnage pour analyser sa progression." : "Analyse temporairement en pause.");
            if (!string.IsNullOrWhiteSpace(readError)) ImGui.TextWrapped(readError);
            ImGui.TextColored(Muted, "Aucune donnée d'un autre joueur n'est analysée.");
            return;
        }
        ImGui.TextColored(Muted, StageLabel(plan.Stage));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(plan.StageExplanation);
        var currency = snapshot.Tomestones;
        var earned = currency.EarnedThisWeek;
        var cap = currency.WeeklyCap;
        var caption = earned.HasValue && cap is > 0 ? $"{earned} / {cap} mémoquartz cette semaine" : "Acquisition hebdomadaire à vérifier";
        ImGui.TextUnformatted(caption);
        var s = ImGui.GetFontSize() / 17;
        ImGui.ProgressBar(earned.HasValue && cap is > 0 ? Math.Clamp((float)earned.Value / cap.Value, 0, 1) : 0, new Vector2(-1, 3*s), "");
        var reset = NextWeekly(DateTimeOffset.UtcNow);
        var remaining = reset - DateTimeOffset.UtcNow;
        ColoredWrapped(Muted, $"Reset dans {(int)remaining.TotalDays} j {remaining.Hours} h  ·  stock {currency.Stock?.ToString() ?? "?"}");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset hebdomadaire : mardi 08:00 UTC. Le stock et les gains hebdomadaires sont indépendants.");
        ImGui.Spacing();
        if (RequestWeekly) { selectedView = 1; settings = false; RequestWeekly = false; }
        string[] navigation = ["Priorités", "Cette semaine", "Équipement", "Accès"];
        for (var i = 0; i < navigation.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, 10*s);
            if (CompassTheme.Nav(navigation[i], selectedView == i && !settings)) { selectedView = i; settings = false; }
        }
        ImGui.Separator();
        ImGui.BeginChild("CompassContent", new Vector2(0, -22*s), false);
        if (settings) DrawPreferences();
        else switch (selectedView) { case 0: DrawGoals(); break; case 1: DrawWeekly(); break; case 2: DrawGear(); break; default: DrawUnlocks(); break; }
        ImGui.EndChild();
        ImGui.Separator();
        ImGui.TextColored(Muted, $"7.56  ·  {(settings ? "Préférences" : "Progression locale")}  ·  {snapshot.ObservedAt.ToLocalTime():HH:mm:ss}");
    }

    private void DrawHeader()
    {
        var s = ImGui.GetFontSize() / 17;
        var p = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var toolsWidth = 4*25*s + 3*3*s;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(p + new Vector2(0, 3*s), p + new Vector2(3*s, 32*s), ImGui.GetColorU32(Accent), 1*s);
        draw.AddText(ImGui.GetFont(), 12*s, p + new Vector2(12*s, 0), ImGui.GetColorU32(Muted), "AETHER COMPASS");
        draw.AddText(p + new Vector2(12*s, 16*s), ImGui.GetColorU32(Accent), snapshot is null ? "Ta prochaine étape" : $"{snapshot.Job} {snapshot.Level}  ·  i{snapshot.AverageItemLevel?.ToString() ?? "?"}");
        ImGui.InvisibleButton("Glisser le panneau", new Vector2(Math.Max(40*s, width-toolsWidth-8*s), 36*s));
        if (ImGui.IsItemActive() && !config.Locked && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(config.Locked ? "Position verrouillée" : "Glisser pour déplacer");
        ImGui.SetCursorScreenPos(p + new Vector2(width-toolsWidth, 4*s));
        if (CompassTheme.Tool("Replier", folded ? "expand" : "fold", 25*s, folded))
        {
            if (!folded) expandedSize = ImGui.GetWindowSize(); else restoreSize = true;
            folded = !folded;
        }
        ImGui.SameLine(0, 3*s);
        if (CompassTheme.Tool("Verrouiller la position", config.Locked ? "locked" : "unlock", 25*s, config.Locked)) { config.Locked = !config.Locked; save(); }
        ImGui.SameLine(0, 3*s);
        if (CompassTheme.Tool("Préférences", "settings", 25*s, settings)) { settings = !settings; Expand(); }
        ImGui.SameLine(0, 3*s);
        if (CompassTheme.Tool("Fermer", "close", 25*s)) IsOpen = false;
        ImGui.SetCursorScreenPos(p + new Vector2(0, 42*s));
        headerHeight = 42*s + ImGui.GetStyle().WindowPadding.Y*2;
        if (!folded) { ImGui.Separator(); ImGui.Spacing(); }
    }

    private void DrawGoals()
    {
        ImGui.Spacing();
        if (plan!.Recommendations.Count == 0) ImGui.TextWrapped("Aucun objectif disponible dans ce profil. Vérifie les prérequis et les préférences ci-dessous.");
        var rank = 0;
        foreach (var goal in plan.Recommendations.Take(showAllGoals ? int.MaxValue : 6))
        {
            ImGui.PushID(goal.Objective.Id);
            var s = ImGui.GetFontSize()/17;
            var p = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var titleWidth = width - 97*s;
            var titleHeight = ImGui.CalcTextSize(goal.Objective.Title, false, titleWidth).Y;
            var height = Math.Max(54*s, titleHeight+33*s);
            var selected = expandedGoal == goal.Objective.Id;
            if (ImGui.InvisibleButton("Voir cet objectif", new Vector2(width, height))) expandedGoal = selected ? null : goal.Objective.Id;
            var hover = ImGui.IsItemHovered();
            var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(p, p + new Vector2(width,height-3*s), ImGui.GetColorU32(new Vector4(.12f,.15f,.16f, selected ? .85f : hover ? .65f : .30f)), 2*s);
            draw.AddRectFilled(p, p + new Vector2(2*s,height-3*s), ImGui.GetColorU32(goal.Status == ObjectiveStatus.NeedsVerification ? Amber with { W=.65f } : Accent with { W=.65f }));
            draw.AddText(p+new Vector2(10*s,10*s), ImGui.GetColorU32(Accent), $"{++rank:00}");
            draw.AddText(ImGui.GetFont(), ImGui.GetFontSize(), p+new Vector2(40*s,8*s), ImGui.GetColorU32(ImGuiCol.Text), goal.Objective.Title, titleWidth);
            var duration = $"{goal.Objective.Minutes}m";
            draw.AddText(p+new Vector2(width-ImGui.CalcTextSize(duration).X-10*s,10*s), ImGui.GetColorU32(Muted), duration);
            var meta = $"{StatusLabel(goal.Status)}{(goal.Objective.RewardItemLevel is { } il ? $"  ·  i{il}" : "")}  ·  {(selected ? "replier" : "détails")}";
            draw.AddText(ImGui.GetFont(), 13*s, p+new Vector2(40*s,titleHeight+13*s), ImGui.GetColorU32(goal.Status == ObjectiveStatus.NeedsVerification ? Amber : Muted), meta);
            if (expandedGoal == goal.Objective.Id)
            {
                ImGui.Indent(12*s);
                ImGui.TextWrapped(goal.Objective.Description);
                foreach (var reason in goal.Reasons) ColoredWrapped(Muted, $"• {reason}");
                foreach (var blocker in goal.Blockers) ColoredWrapped(Amber, blocker);
                DrawActivityControls(goal);
                if (ImGui.TreeNode("Sources"))
                {
                    ImGui.TextWrapped($"Patch {goal.Objective.Patch} · vérifié le {goal.Objective.VerifiedDate:dd/MM/yyyy}.");
                    foreach (var source in goal.Objective.SourceUrls) ImGui.TextWrapped(source);
                    ImGui.TreePop();
                }
                ImGui.Unindent(12*s);
            }
            ImGui.Spacing(); ImGui.PopID();
        }
        if (plan.Recommendations.Count > 6 && ImGui.SmallButton(showAllGoals ? "Réduire à 6 priorités" : $"Voir les {plan.Recommendations.Count-6} autres pistes")) showAllGoals = !showAllGoals;
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
        ImGui.Spacing(); ColoredWrapped(Muted, "Butin et pièce d'échange séparés. Inconnu signifie : à vérifier.");
        foreach (var goal in AllGoals().Where(x => x.Objective.Cadence == ResetCadence.Weekly).OrderBy(x => x.Objective.Id).ToArray())
        {
            ImGui.PushID(goal.Objective.Id);
            var state = PlannerEngine.GetCompletionState(goal.Objective, snapshot!, progress!, DateTimeOffset.UtcNow);
            ImGui.TextColored(state == KnowledgeState.Yes ? Accent : state == KnowledgeState.Unknown ? Amber : Muted,
                state == KnowledgeState.Yes ? "FAIT" : state == KnowledgeState.No ? "À FAIRE" : "INCONNU");
            ImGui.SameLine(); ImGui.TextWrapped(goal.Objective.Title);
            var live = goal.CompletionSource == CompletionProvenance.Observed;
            if (live || goal.CompletionSource == CompletionProvenance.Manual && state != KnowledgeState.Unknown)
                ImGui.TextColored(Muted, live ? "Lu en jeu" : "Déclaration manuelle");
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
            ColoredWrapped(Muted, "Compteur du jeu. Dépenser ne remet pas le plafond à zéro.");
            return;
        }
        if (goal.CompletionSource == CompletionProvenance.Observed)
        {
            ImGui.TextColored(Muted, "Suivi automatique.");
            return;
        }
        if (ImGui.SmallButton("Fait")) { progress!.Complete(goal.Objective.Id, DateTimeOffset.UtcNow); Changed(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("À faire")) { progress!.SetIncomplete(goal.Objective.Id, DateTimeOffset.UtcNow); Changed(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Effacer")) { progress!.ClearCompletion(goal.Objective.Id); Changed(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Effacer la déclaration manuelle. Une lecture connue du jeu reste prioritaire.");
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
        foreach (var warning in plan?.DataWarnings ?? Array.Empty<string>()) ImGui.TextWrapped($"• {warning}");
        if (readError is not null) ImGui.TextWrapped(readError);
        ImGui.TextWrapped("Un changement de zone peut rendre certaines données temporairement indisponibles. Aucune supposition n'est faite sur les quêtes ou les weekly des autres joueurs.");
    }

    private void Changed() { save(); Replan(); }
    private static string StageLabel(ProgressStage stage) => stage switch
    {
        ProgressStage.Fresh100 => "NOUVEAU NIVEAU 100", ProgressStage.CatchUp => "RATTRAPAGE",
        ProgressStage.Endgame => "FIN DE JEU", ProgressStage.Advanced => "PROGRESSION AVANCÉE",
        ProgressStage.Leveling => "ÉPOPÉE & NIVEAUX", ProgressStage.NonCombat => "JOB NON COMBATTANT", _ => "À VÉRIFIER"
    };
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
