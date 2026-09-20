// Copyright (c) 2026 Aleqsd. MIT license; see LICENSE.
using System.Numerics;
using System.Runtime.InteropServices;
using System.Reflection;
using AetherCompass;
using AetherCompass.Core;
using Dalamud.Bindings.ImGui;
using HexaGen.Runtime;

// Renders the production MainWindow against explicit synthetic fixtures, outside FFXIV.
// Usage: Preview <Dalamud directory> <repository root>
internal static unsafe class Program
{
    private sealed record PreviewCase(string Name, int Width, int Height, float Scale = 1,
        bool Weekly = false, bool LoggedIn = true, bool KnownCurrency = true,
        bool ExpandedGoal = false, bool Folded = false);

    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: Preview <Dalamud directory> <repository root>");
        var output = Path.Combine(Path.GetFullPath(args[1]), "docs", "images");
        Directory.CreateDirectory(output);
        using var native = new NativeContext(Path.Combine(Path.GetFullPath(args[0]), "cimgui.dll"));
        NativeLibrary.SetDllImportResolver(typeof(ImGui).Assembly, (name, _, _) => name == "cimgui" ? native.Module : nint.Zero);
        ImGui.InitApi(native);
        PreviewCase[] cases = [
            new("goals-level100", 580, 600),
            new("weekly-unknown", 580, 600, Weekly: true, KnownCurrency: false),
            new("logged-out", 580, 340, LoggedIn: false),
            new("minimum-scale150", 520, 580, 1.5f, Weekly: true, KnownCurrency: false),
            new("goal-expanded", 580, 600, ExpandedGoal: true),
            new("header-folded", 580, 600, Folded: true),
        ];
        foreach (var preview in cases) Render(preview, output);
        Console.WriteLine($"Rendered {cases.Length} production ImGui views using synthetic character fixtures.");
    }

    private static void Render(PreviewCase preview, string output)
    {
        var context = ImGui.CreateContext();
        try
        {
            var textures = new Dictionary<ulong, (byte[] Pixels, int Width, int Height)>();
            ImGui.StyleColorsDark();
            ImGui.GetStyle().ScaleAllSizes(preview.Scale);
            ImGui.GetStyle().WindowPadding = new Vector2(8) * preview.Scale;
            ImGui.GetStyle().WindowRounding = 3 * preview.Scale;
            var io = ImGui.GetIO();
            io.IniFilename = null;
            io.DeltaTime = 1f / 60;
            var width = (int)Math.Ceiling((preview.Width + 32) * preview.Scale);
            var height = (int)Math.Ceiling((preview.Height + 32) * preview.Scale);
            io.DisplaySize = new Vector2(width, height);
            var fontConfig = ImGui.ImFontConfig();
            fontConfig.SizePixels = 17 * preview.Scale;
            ushort[] ranges = [0x20, 0x17f, 0x2000, 0x206f, 0x2190, 0x21ff, 0x2260, 0x2265, 0];
            fixed (ushort* glyphs = ranges)
            {
                io.Fonts.AddFontFromFileTTF(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"), 17 * preview.Scale, fontConfig, glyphs);
                io.Fonts.Build();
            }
            fontConfig.Destroy();

            void UploadFontTextures()
            {
                for (var index = 0; index < io.Fonts.Textures.Size; index++)
                {
                    byte* pixels; int textureWidth, textureHeight;
                    io.Fonts.GetTexDataAsRGBA32(index, &pixels, &textureWidth, &textureHeight);
                    var bytes = new byte[textureWidth * textureHeight * 4];
                    Marshal.Copy((nint)pixels, bytes, 0, bytes.Length);
                    var id = 900000UL + (ulong)index;
                    io.Fonts.SetTexID(index, new ImTextureID(id));
                    textures[id] = (bytes, textureWidth, textureHeight);
                }
            }

            var config = new Configuration { Preferences = new Preferences { SessionMinutes = 60 } };
            var saves = 0;
            var window = new MainWindow(config, new ProgressStore(), () => saves++) { RequestWeekly = preview.Weekly, IsOpen = true };
            window.SetSnapshot(preview.LoggedIn ? Fixture(preview.KnownCurrency) : null, null);
            float overflow = 0;
            Vector2 origin = default, actualSize = default;
            float availableWidth = 0, uiScale = 1;
            void Frame()
            {
                UploadFontTextures();
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(new Vector2(16) * preview.Scale);
                ImGui.SetNextWindowSize(new Vector2(preview.Width, preview.Height) * preview.Scale, ImGuiCond.FirstUseEver);
                var open = true;
                // WindowSystem requires running Dalamud services. This is the same Draw() body.
                window.PreDraw();
                ImGui.Begin("Aether Compass###AetherCompass", ref open, window.Flags);
                origin = ImGui.GetCursorScreenPos();
                availableWidth = ImGui.GetContentRegionAvail().X;
                uiScale = ImGui.GetFontSize() / 17;
                window.Draw();
                actualSize = ImGui.GetWindowSize();
                overflow = ImGui.GetScrollMaxX();
                ImGui.End();
                window.PostDraw();
                ImGui.Render();
            }
            void Click(Vector2 position)
            {
                io.AddMousePosEvent(position.X, position.Y); Frame();
                io.AddMouseButtonEvent(0, true); Frame();
                io.AddMouseButtonEvent(0, false); Frame();
                io.AddMousePosEvent(-100, -100); Frame();
            }
            Vector2 HeaderControl(int index) => origin + new Vector2(availableWidth - 109 * uiScale + (12.5f + 28 * index) * uiScale, 16.5f * uiScale);
            bool IsFolded() => (bool)typeof(MainWindow).GetField("folded", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
            for (var frame = 0; frame < 4; frame++) Frame();
            if (preview.ExpandedGoal)
            {
                Click(origin + new Vector2(80, 190) * uiScale);
                Check(typeof(MainWindow).GetField("expandedGoal", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window) is not null,
                    "Clicking the first priority must expand its details.");
            }
            if (preview.Folded)
            {
                Click(HeaderControl(1));
                Check(config.Locked && saves == 1, "Header lock must change and save its preference.");
                Check(window.Flags.HasFlag(ImGuiWindowFlags.NoResize) && !window.Flags.HasFlag(ImGuiWindowFlags.NoInputs), "Lock must retain interactive controls.");
                Click(HeaderControl(0));
                Check(IsFolded() && actualSize.Y < 100 * preview.Scale, "Fold must reduce the window to its header while locked.");
                Click(HeaderControl(0));
                Check(!IsFolded() && Math.Abs(actualSize.Y - preview.Height * preview.Scale) < 1, "Unfold must restore the previous height.");
                Click(HeaderControl(0));
                Check(IsFolded(), "Folded preview must show only the header.");
                height = (int)Math.Ceiling(actualSize.Y + 32 * preview.Scale);
            }
            // Font textures may rebake during a frame: fetch the final atlas after rendering.
            byte* atlas; int atlasWidth, atlasHeight;
            io.Fonts.GetTexDataAsRGBA32(0, &atlas, &atlasWidth, &atlasHeight);
            UploadFontTextures();
            CpuRenderer.Save(ImGui.GetDrawData(), width, height, atlas, atlasWidth, atlasHeight,
                Path.Combine(output, preview.Name + ".png"), textures);
            Console.WriteLine($"{preview.Name}: {width}x{height}; horizontal content overflow={overflow:0.0}px");
            if (preview.Folded)
            {
                Click(HeaderControl(3));
                Check(!window.IsOpen, "Close must work while both locked and folded.");
                Console.WriteLine("Native header clicks passed: lock + save, fold, restore height, close while locked/folded.");
            }
        }
        finally { ImGui.DestroyContext(context); }
    }

    private static CharacterSnapshot Fixture(bool knownCurrency)
    {
        var unlockKeys = new[] { "expert", "mistwake", "clyteum", "heavyweight-normal", "heavyweight", "windurst", "relic" };
        return new CharacterSnapshot
        {
            ContentId = 1, Name = "Aventurière de démonstration", Job = "Paladin", JobId = 19,
            Level = 100, IsCombatJob = true, AverageItemLevel = 762, ObservedAt = DateTimeOffset.UtcNow,
            Gear = [
                new("Arme", "Arme de démonstration", 0, 770, Kind: GearSlotKind.Weapon),
                new("Tête", "Casque de démonstration", 0, 770, Kind: GearSlotKind.Armor),
                new("Torse", "Armure de démonstration", 0, 770, Kind: GearSlotKind.Armor),
                new("Mains", "Gants de démonstration", 0, 765, Kind: GearSlotKind.Armor),
                new("Jambes", "Jambières de démonstration", 0, 770, Kind: GearSlotKind.Armor),
                new("Pieds", "Bottes de démonstration", 0, 750, Kind: GearSlotKind.Armor),
                new("Oreilles", "Boucles de démonstration", 0, 770, Kind: GearSlotKind.Accessory),
                new("Cou", "Collier de démonstration", 0, 755, Kind: GearSlotKind.Accessory),
                new("Poignets", "Bracelet de démonstration", 0, 760, Kind: GearSlotKind.Accessory),
                new("Bague droite", "Bague de démonstration", 0, 755, Kind: GearSlotKind.Accessory),
                new("Bague gauche", "Bague ancienne de démonstration", 0, 740, Kind: GearSlotKind.Accessory),
            ],
            Quests = new Dictionary<string, KnowledgeState> { ["dawntrail"] = KnowledgeState.Yes, ["current-msq"] = KnowledgeState.Yes },
            Unlocks = unlockKeys.ToDictionary(key => key, _ => KnowledgeState.Yes),
            // Empty is deliberately UNKNOWN. A victory does not prove that weekly loot was claimed.
            Weekly = new Dictionary<string, KnowledgeState>(),
            Tomestones = knownCurrency ? new CurrencyState { EarnedThisWeek = 375, WeeklyCap = 900, Stock = 820 }
                : new CurrencyState { WeeklyCap = 900 },
        };
    }

    private sealed class NativeContext(string path) : INativeContext, IDisposable
    {
        public nint Module { get; } = NativeLibrary.Load(path);
        public nint GetProcAddress(string name) => NativeLibrary.GetExport(Module, name);
        public bool TryGetProcAddress(string name, out nint address) => NativeLibrary.TryGetExport(Module, name, out address);
        public bool IsExtensionSupported(string name) => false;
        public void Dispose() => NativeLibrary.Free(Module);
    }
}
