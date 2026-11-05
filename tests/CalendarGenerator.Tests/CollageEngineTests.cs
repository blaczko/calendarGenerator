using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.Photos;

namespace CalendarGenerator.Tests;

public class CollageEngineTests
{
    private readonly CollageEngine _engine = new();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Layout_CoversRequestedPhotoCount(int count)
    {
        var photos = Enumerable.Range(0, count)
            .Select(i => new PhotoItem
            {
                AspectRatio = i % 2 == 0 ? 1.5 : 0.7
            })
            .ToList();

        var layout = _engine.Build(photos);
        Assert.Equal(count, layout.Slots.Count);
        Assert.All(layout.Slots, s =>
        {
            Assert.True(s.Bounds.Width > 0);
            Assert.True(s.Bounds.Height > 0);
            Assert.InRange(s.Bounds.X, 0, 1);
            Assert.InRange(s.Bounds.Y, 0, 1);
            Assert.True(s.Bounds.X + s.Bounds.Width <= 1.02);
            Assert.True(s.Bounds.Y + s.Bounds.Height <= 1.02);
        });
    }

    [Fact]
    public void MixedOrientations_ProduceValidSixLayout()
    {
        var photos = new List<PhotoItem>
        {
            new() { AspectRatio = 1.6 },
            new() { AspectRatio = 1.5 },
            new() { AspectRatio = 0.7 },
            new() { AspectRatio = 0.65 },
            new() { AspectRatio = 1.4 },
            new() { AspectRatio = 0.8 }
        };

        var layout = _engine.Build(photos);
        Assert.Equal(6, layout.Slots.Count);
    }

    [Fact]
    public void SlotAspect_MatchesPhotoAspect()
    {
        var photos = new List<PhotoItem>
        {
            new() { AspectRatio = 1.5 },
            new() { AspectRatio = 0.67 },
            new() { AspectRatio = 1.2 }
        };

        // Normalized width/height encode pixel aspect against a square container (aspect=1).
        const double containerAspect = 1.0;
        var layout = _engine.Build(photos, containerAspect);
        foreach (var slot in layout.Slots)
        {
            var photo = photos.Single(p => p.Id == slot.PhotoId);
            var slotAspect = slot.Bounds.Width / slot.Bounds.Height;
            var expectedNorm = photo.AspectRatio / containerAspect;
            Assert.InRange(slotAspect, expectedNorm * 0.98, expectedNorm * 1.02);
        }
    }

    [Fact]
    public void SixPhotos_FillMostOfTheArea()
    {
        var photos = Enumerable.Range(0, 6)
            .Select(i => new PhotoItem { AspectRatio = i % 2 == 0 ? 1.4 : 0.75 })
            .ToList();

        var layout = _engine.Build(photos);
        var covered = layout.Slots.Sum(s => s.Bounds.Width * s.Bounds.Height);
        Assert.True(covered > 0.55, $"Expected dense collage, covered={covered:F2}");
    }
}
