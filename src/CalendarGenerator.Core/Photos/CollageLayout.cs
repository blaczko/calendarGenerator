namespace CalendarGenerator.Core.Photos;

public sealed class CollageLayout
{
    public required IReadOnlyList<CollageSlot> Slots { get; init; }
    public double Gap { get; init; } = 0.012;
}
