namespace CalendarGenerator.Core.Templates;

public sealed class TemplateLibrary
{
    private readonly IReadOnlyList<TemplateDefinition> _templates =
    [
        Preset("soft-beige", "Puha bézs", "Lágy organikus formák meleg bézs árnyalatokban",
            "#F3E9DA", "#E0CDB4", "#C9A88A", TemplateStyle.SoftOrganic, CalendarFormat.Classic),
        Preset("pastel-gradient", "Pasztell gradiens", "Égbolt–homok átmenet",
            "#E7F3FA", "#F8E9DC", "#D5EADF", TemplateStyle.PastelGradient, CalendarFormat.Elegant,
            font: "Palatino Linotype", title: 34, day: 17),
        Preset("watercolor", "Akvarell", "Puha akvarell foltok",
            "#F7EEE6", "#E2C7B5", "#AFC7BC", TemplateStyle.Watercolor, CalendarFormat.SoftCards,
            font: "Garamond", title: 33),
        Preset("geometric", "Geometria", "Modern geometrikus játék",
            "#F5F1EA", "#D8CFC2", "#9E9588", TemplateStyle.Geometric, CalendarFormat.ModernBold,
            font: "Calibri", title: 30, day: 20, dayColor: "#1A1A1A"),
        Preset("paper-texture", "Papír textúra", "Krémes papírérzet",
            "#F8F3EB", "#E7DDD0", "#CDBFAE", TemplateStyle.PaperTexture, CalendarFormat.Notebook,
            font: "Georgia"),
        Preset("botanical-abstract", "Botanikai absztrakt", "Friss zöld absztrakt ívek",
            "#EAF3E6", "#C9DDBF", "#84A878", TemplateStyle.BotanicalAbstract, CalendarFormat.Elegant,
            font: "Cambria", weekend: "#5588A878"),
        Preset("marble", "Márvány", "Világos márvány erekkel",
            "#F7F7F5", "#E4E3DF", "#B8B7B2", TemplateStyle.Marble, CalendarFormat.Minimal,
            font: "Constantia", title: 36, grid: "#55B0B0B0", panel: "#B3FFFFFF"),
        Preset("earth-tones", "Földszínek", "Terrakotta és homok",
            "#F0E4D4", "#D2B48C", "#A2744F", TemplateStyle.EarthTones, CalendarFormat.Classic,
            font: "Georgia", eventColor: "#8B3A2A"),
        Preset("pastel-waves", "Pasztell hullámok", "Lágy kék-zöld hullámok",
            "#EAF5F8", "#C9E0E8", "#8FB8C7", TemplateStyle.PastelWaves, CalendarFormat.SoftCards,
            font: "Candara"),
        Preset("scandinavian", "Skandináv", "Tiszta minták, nyugalmas zöld",
            "#F5F3EE", "#DDD8CE", "#7F927F", TemplateStyle.Scandinavian, CalendarFormat.Minimal,
            font: "Segoe UI", day: 19),
        Preset("linen", "Vászon", "Szövött hatású meleg vászon",
            "#F2EBE1", "#DACFBF", "#B49F86", TemplateStyle.Linen, CalendarFormat.Notebook),
        Preset("monochrome", "Monokróm", "Grafikus monokróm formák",
            "#F4F4F4", "#D2D2D2", "#7A7A7A", TemplateStyle.Monochrome, CalendarFormat.ModernBold,
            font: "Segoe UI", title: 31, day: 21, dayColor: "#111111"),
        Preset("aurora", "Auróra", "Lágy színes fényátmenet",
            "#EEF2FF", "#FCE7F3", "#C7D2FE", TemplateStyle.Aurora, CalendarFormat.Elegant,
            font: "Palatino Linotype", title: 34),
        Preset("craft-paper", "Kézműves papír", "Meleg kraft papír textúra",
            "#F2E6D4", "#E0C9A8", "#B8956C", TemplateStyle.CraftPaper, CalendarFormat.Classic,
            font: "Georgia", titleColor: "#3D2B1F", dayColor: "#2A1C12"),
        Preset("solid-wash", "Egyszínű mosás", "Egyszerű, tiszta egyszínű háttér",
            "#F7F2EA", "#EFE8DE", "#E4D9C8", TemplateStyle.SolidWash, CalendarFormat.Compact,
            font: "Calibri", title: 28, day: 16),
        Preset("magazine-cream", "Magazin krém", "Nagy cím, szerkesztőségi naptár",
            "#FBF7F0", "#EFE6DA", "#D4C4B0", TemplateStyle.PaperTexture, CalendarFormat.Magazine,
            font: "Georgia", title: 38, day: 17),
        Preset("poster-ink", "Poszter tinta", "Nagy napszámok, merész tipográfia",
            "#F4F4F4", "#E0E0E0", "#9A9A9A", TemplateStyle.Monochrome, CalendarFormat.Poster,
            font: "Segoe UI", title: 34, day: 24, dayColor: "#111111"),
        Preset("circle-soft", "Körök", "Napok körben, lágy kártyák",
            "#F3EEE8", "#E2D5C6", "#C4A882", TemplateStyle.SoftOrganic, CalendarFormat.CircularDays,
            font: "Candara", title: 30)
    ];

    public IReadOnlyList<TemplateDefinition> BuiltIn => _templates;
    public IReadOnlyList<TemplateDefinition> All => _templates;

    public static IReadOnlyList<string> FontChoices { get; } =
    [
        "Georgia", "Cambria", "Palatino Linotype", "Garamond", "Constantia",
        "Calibri", "Segoe UI", "Candara", "Segoe Print", "Comic Sans MS"
    ];

    public TemplateDefinition Get(string id) =>
        _templates.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? _templates[0];

    public TemplateDefinition Resolve(string id, IEnumerable<TemplateDefinition>? customTemplates)
    {
        var custom = customTemplates?.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (custom is not null)
            return custom;

        return Get(id);
    }

    public IReadOnlyList<TemplateDefinition> MergeWithCustom(IEnumerable<TemplateDefinition>? customTemplates)
    {
        var customs = customTemplates?.ToList() ?? [];
        if (customs.Count == 0)
            return _templates;

        return customs.Concat(_templates).ToList();
    }

    private static TemplateDefinition Preset(
        string id,
        string name,
        string description,
        string primary,
        string secondary,
        string accent,
        TemplateStyle style,
        CalendarFormat format,
        string font = "Georgia",
        double title = 32,
        double day = 18,
        string? dayColor = null,
        string? titleColor = null,
        string? eventColor = null,
        string? weekend = null,
        string? grid = null,
        string? panel = null) => new()
    {
        Id = id,
        DisplayName = name,
        Description = description,
        PrimaryHex = primary,
        SecondaryHex = secondary,
        AccentHex = accent,
        Style = style,
        CalendarFormat = format,
        FontFamily = font,
        TitleFontSize = title,
        DayFontSize = day,
        DayColorHex = dayColor ?? "#222222",
        TitleColorHex = titleColor ?? "#2C2C2C",
        EventColorHex = eventColor ?? "#C62828",
        WeekendFillHex = weekend ?? "#66F3EEE6",
        GridLineHex = grid ?? "#88C9BEAE",
        CalendarPanelHex = panel ?? "#E6FFFAFA"
    };
}
