namespace CalendarGenerator.Core.Templates;

public enum CalendarFormat
{
    Classic = 0,
    Elegant = 1,
    ModernBold = 2,
    Minimal = 3,
    SoftCards = 4,
    Compact = 5,
    Notebook = 6,
    Magazine = 7,
    DotGrid = 8,
    Banner = 9,
    OutlineFrame = 10,
    CircularDays = 11,
    Poster = 12,
    Folio = 13
}

public enum TitleAlignmentMode
{
    Center = 0,
    Left = 1,
    Right = 2
}

public enum DayAlignMode
{
    Left = 0,
    Center = 1
}

public enum HeaderStyleMode
{
    Default = 0,
    SolidBar = 1,
    Pills = 2,
    Underline = 3,
    None = 4
}

public static class CalendarFormatInfo
{
    public static IReadOnlyList<(CalendarFormat Format, string Name, string Description)> All { get; } =
    [
        (CalendarFormat.Classic, "Klasszikus", "Puha rács, hétvége kiemelés"),
        (CalendarFormat.Elegant, "Elegáns", "Légies, vékony vonalak"),
        (CalendarFormat.ModernBold, "Modern vastag", "Erős számok, tiszta fejléc"),
        (CalendarFormat.Minimal, "Minimál", "Szinte keret nélküli"),
        (CalendarFormat.SoftCards, "Puha kártyák", "Lekerekített napok hézagokkal"),
        (CalendarFormat.Compact, "Kompakt", "Sűrűbb rács"),
        (CalendarFormat.Notebook, "Füzet", "Vonalazott jegyzetfüzet"),
        (CalendarFormat.Magazine, "Magazin", "Nagy balra zárt cím, letisztult rács"),
        (CalendarFormat.DotGrid, "Pont rács", "Pontok a vonalak helyett"),
        (CalendarFormat.Banner, "Banner fejléc", "Színes sáv a napok felett"),
        (CalendarFormat.OutlineFrame, "Keretes", "Vastag külső keret, belső nélkül"),
        (CalendarFormat.CircularDays, "Körös napok", "Napok körben kiemelve"),
        (CalendarFormat.Poster, "Poszter", "Hatalmas napszámok"),
        (CalendarFormat.Folio, "Fólió", "Dupla vonal, klasszikus nyomtatvány")
    ];
}
