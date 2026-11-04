namespace CalendarGenerator.Core.Photos;

public sealed class CollageSlot
{
    public required Guid PhotoId { get; init; }
    public required CollageRect Bounds { get; init; }
}
