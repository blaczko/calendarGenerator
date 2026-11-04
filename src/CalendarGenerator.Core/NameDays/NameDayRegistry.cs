namespace CalendarGenerator.Core.NameDays;

public sealed class NameDayRegistry
{
    private readonly Dictionary<string, INameDayProvider> _providers;

    public NameDayRegistry(IEnumerable<INameDayProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.SourceId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<INameDayProvider> All => _providers.Values.ToList();

    public INameDayProvider Get(string sourceId) =>
        _providers.TryGetValue(sourceId, out var provider)
            ? provider
            : _providers.Values.First();
}
