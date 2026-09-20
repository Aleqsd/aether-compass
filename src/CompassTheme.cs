using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AetherCompass;

/// <summary>Compact meter-style presentation. Pair Push/Pop around the complete window.</summary>
public static class CompassTheme
{
    public static readonly Vector4 Accent = new(.40f, .84f, .79f, 1);
    public static readonly Vector4 Muted = new(.65f, .68f, .72f, 1);
    public static readonly Vector4 Amber = new(.98f, .76f, .42f, 1);

    private static readonly (ImGuiCol Slot, Vector4 Color)[] Colors =
    [
        (ImGuiCol.Text, new(.91f, .92f, .94f, 1)),
        (ImGuiCol.TextDisabled, Muted),
        (ImGuiCol.TextSelectedBg, Accent with { W = .25f }),
        (ImGuiCol.NavHighlight, Accent with { W = .70f }),
        (ImGuiCol.WindowBg, new(.045f, .045f, .05f, .92f)),
        (ImGuiCol.ChildBg, Vector4.Zero),
        (ImGuiCol.PopupBg, new(.065f, .065f, .075f, .98f)),
        (ImGuiCol.Border, new(.23f, .23f, .25f, .70f)),
        (ImGuiCol.BorderShadow, Vector4.Zero),
        (ImGuiCol.FrameBg, new(.12f, .12f, .135f, .78f)),
        (ImGuiCol.FrameBgHovered, new(.19f, .19f, .21f, .85f)),
        (ImGuiCol.FrameBgActive, new(.23f, .23f, .25f, .90f)),
        (ImGuiCol.Button, Vector4.Zero),
        (ImGuiCol.ButtonHovered, new(.19f, .19f, .21f, .70f)),
        (ImGuiCol.ButtonActive, new(.24f, .24f, .26f, .90f)),
        (ImGuiCol.Header, new(.14f, .14f, .16f, .65f)),
        (ImGuiCol.HeaderHovered, new(.20f, .20f, .22f, .80f)),
        (ImGuiCol.HeaderActive, new(.24f, .24f, .26f, .90f)),
        (ImGuiCol.Separator, new(.24f, .24f, .27f, .65f)),
        (ImGuiCol.SeparatorHovered, new(.42f, .45f, .47f, .85f)),
        (ImGuiCol.SeparatorActive, Accent),
        (ImGuiCol.CheckMark, Accent),
        (ImGuiCol.SliderGrab, Accent with { W = .75f }),
        (ImGuiCol.SliderGrabActive, Accent),
        (ImGuiCol.PlotHistogram, Accent),
        (ImGuiCol.PlotHistogramHovered, Accent),
        (ImGuiCol.ScrollbarBg, Vector4.Zero),
        (ImGuiCol.ScrollbarGrab, new(.34f, .34f, .37f, .60f)),
        (ImGuiCol.ScrollbarGrabHovered, new(.44f, .44f, .47f, .80f)),
        (ImGuiCol.ScrollbarGrabActive, new(.54f, .54f, .57f, .90f)),
        (ImGuiCol.ResizeGrip, new(.35f, .35f, .38f, .20f)),
        (ImGuiCol.ResizeGripHovered, new(.48f, .48f, .51f, .65f)),
        (ImGuiCol.ResizeGripActive, Accent with { W = .70f }),
        (ImGuiCol.TableHeaderBg, new(.12f, .12f, .14f, .75f)),
        (ImGuiCol.TableBorderStrong, new(.25f, .25f, .28f, .65f)),
        (ImGuiCol.TableBorderLight, new(.19f, .19f, .22f, .45f)),
        (ImGuiCol.TableRowBg, Vector4.Zero),
        (ImGuiCol.TableRowBgAlt, new(.13f, .13f, .15f, .30f)),
    ];

    private const int StyleVarCount = 15;

    public static void Push()
    {
        foreach (var (slot, color) in Colors) ImGui.PushStyleColor(slot, color);
        var scale = ImGui.GetFontSize() / 17f;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5, 3) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(7, 5) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(5, 4) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5, 4) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 3 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 2 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 2 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 6 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 1 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, 8 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 1 * scale);
    }

    public static void Pop()
    {
        ImGui.PopStyleVar(StyleVarCount);
        ImGui.PopStyleColor(Colors.Length);
    }

    public static ImGuiWindowFlags Flags(bool locked, bool folded)
    {
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoFocusOnAppearing;
        return locked || folded ? flags | ImGuiWindowFlags.NoResize : flags;
    }

    /// <summary>Vector controls adapted from the MIT-licensed CycleOpener GuidePanel.</summary>
    public static bool Tool(string id, string glyph, float size, bool selected = false)
    {
        var position = ImGui.GetCursorScreenPos();
        if (selected) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.13f, .22f, .21f, .65f));
        var clicked = ImGui.Button("##" + id, new Vector2(size));
        if (selected) ImGui.PopStyleColor();
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(selected ? Accent : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
        var scale = size / 29f;
        var center = position + new Vector2(size / 2);
        void Line(float x, float y, float endX, float endY) => draw.AddLine(
            center + new Vector2(x, y) * scale, center + new Vector2(endX, endY) * scale, color, 1.5f * scale);

        switch (glyph)
        {
            case "close":
                Line(-4, -4, 4, 4); Line(-4, 4, 4, -4);
                break;
            case "fold":
                Line(-5, 0, 5, 0);
                break;
            case "expand":
                Line(-5, 0, 5, 0); Line(0, -5, 0, 5);
                break;
            case "settings":
                for (var i = 0; i < 32; i++)
                {
                    var radius = i % 4 is 1 or 2 ? 8.5f : 6f;
                    draw.PathLineTo(center + new Vector2(MathF.Cos(i * MathF.PI / 16), MathF.Sin(i * MathF.PI / 16)) * radius * scale);
                }
                draw.PathStroke(color, ImDrawFlags.Closed, 1.6f * scale);
                draw.AddCircle(center, 2.6f * scale, color, 24, 1.5f * scale);
                break;
            case "locked":
            case "unlock":
                draw.AddRect(center + new Vector2(-6.5f, 0) * scale, center + new Vector2(6.5f, 8) * scale,
                    color, 2 * scale, ImDrawFlags.None, 1.8f * scale);
                var open = glyph == "unlock";
                var archX = open ? 3f : 0f;
                var archY = open ? -3f : -1f;
                Line(archX - 4.5f, 0, archX - 4.5f, archY);
                for (var i = 0; i < 16; i++)
                {
                    var start = MathF.PI + i * MathF.PI / 16;
                    var end = start + MathF.PI / 16;
                    Line(archX + MathF.Cos(start) * 4.5f, archY + MathF.Sin(start) * 4.5f,
                        archX + MathF.Cos(end) * 4.5f, archY + MathF.Sin(end) * 4.5f);
                }
                Line(archX + 4.5f, archY, archX + 4.5f, open ? -1 : 0);
                draw.AddCircleFilled(center + new Vector2(0, 3.5f) * scale, 1.25f * scale, color, 12);
                Line(0, 4, 0, 6);
                break;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(glyph switch
            {
                "close" => "Fermer",
                "fold" => "Replier",
                "expand" => "Déplier",
                "locked" => "Déverrouiller la position",
                "unlock" => "Verrouiller la position",
                "settings" => "Préférences",
                _ => id,
            });
        return clicked;
    }

    public static bool Nav(string label, bool selected)
    {
        var scale = ImGui.GetFontSize() / 17f;
        var position = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize(label);
        var size = new Vector2(textSize.X + 12 * scale, ImGui.GetFrameHeight() + 3 * scale);
        ImGui.PushStyleColor(ImGuiCol.Text, selected ? Accent : Muted);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        var clicked = ImGui.Button(label + "##compass-nav", size);
        ImGui.PopStyleColor(2);
        if (selected)
        {
            var inset = 6 * scale;
            ImGui.GetWindowDrawList().AddLine(position + new Vector2(inset, size.Y - 1),
                position + new Vector2(size.X - inset, size.Y - 1), ImGui.GetColorU32(Accent), 2 * scale);
        }
        return clicked;
    }
}
