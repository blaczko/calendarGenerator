using CalendarGenerator.Core.Templates;

namespace CalendarGenerator.Core.Models;

public sealed class MonthPhotos
{
    public int Month { get; set; }

    /// <summary>Up to 12 photographs for this month.</summary>
    public List<PhotoItem> Photos { get; set; } = new();

    /// <summary>Last applied preset id (for UI selection).</summary>
    public string TemplateId { get; set; } = "soft-beige";

    /// <summary>Editable appearance for this month. Null until first customize/load.</summary>
    public PageTheme? Theme { get; set; }

    public PageTheme GetOrCreateTheme(TemplateLibrary library, IEnumerable<TemplateDefinition>? customTemplates = null)
    {
        if (Theme is not null)
            return Theme;

        Theme = PageTheme.FromTemplate(library.Resolve(TemplateId, customTemplates));
        return Theme;
    }
}
