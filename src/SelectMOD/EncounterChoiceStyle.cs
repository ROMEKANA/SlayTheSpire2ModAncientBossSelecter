using Godot;

namespace SelectMOD;

internal static class EncounterChoiceStyle
{
    private static Theme? theme;

    internal static void Apply(OptionButton choice)
    {
        theme ??= Build();
        choice.Theme = theme;
        choice.Alignment = HorizontalAlignment.Center;
        choice.GetPopup().Theme = theme;
    }

    private static Theme Build()
    {
        var result = new Theme();
        var normal = Box("#2b424f");
        var hover = Box("#375767");
        var gold = new Color("#ffd16a");
        foreach (var state in new[] { "normal", "disabled" })
            result.SetStylebox(state, "OptionButton", normal);
        foreach (var state in new[] { "hover", "pressed", "hover_pressed" })
            result.SetStylebox(state, "OptionButton", hover);
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color" })
            result.SetColor(name, "OptionButton", gold);
        result.SetStylebox("panel", "PopupMenu", Box("#12212a"));
        result.SetStylebox("hover", "PopupMenu", hover);
        result.SetColor("font_color", "PopupMenu", new Color("#edf2ef"));
        result.SetColor("font_hover_color", "PopupMenu", gold);
        result.SetConstant("v_separation", "PopupMenu", 8);
        var arrow = AscensionIcon.Render("""<svg xmlns="http://www.w3.org/2000/svg" width="18" height="14"><path fill="#f5bc46" d="M1 2h16L9 13Z"/></svg>""");
        if (arrow != null) result.SetIcon("arrow", "OptionButton", arrow);
        return result;
    }

    private static StyleBoxFlat Box(string color) => new()
    {
        BgColor = new Color(color), ContentMarginLeft = 9, ContentMarginRight = 9,
        ContentMarginTop = 3, ContentMarginBottom = 3
    };
}
