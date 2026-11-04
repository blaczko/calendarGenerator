namespace CalendarGenerator.Core.Photos;

public readonly record struct CollageRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
