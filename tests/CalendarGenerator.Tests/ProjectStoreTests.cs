using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.Storage;

namespace CalendarGenerator.Tests;

public class ProjectStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var root = Path.Combine(Path.GetTempPath(), "CalendarGeneratorTests", Guid.NewGuid().ToString("N"));
        var store = new ProjectStore(root);
        var project = new CalendarProject
        {
            Name = "Teszt",
            Year = 2027,
            Language = "hu"
        };
        project.Events.Add(new CustomEvent
        {
            Date = new DateOnly(2027, 5, 15),
            Description = "Boti születésnapja"
        });
        project.GetMonth(3).TemplateId = "marble";

        var path = await store.SaveAsync(project);
        var loaded = await store.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal("Teszt", loaded!.Name);
        Assert.Equal(2027, loaded.Year);
        Assert.Single(loaded.Events);
        Assert.Equal("marble", loaded.GetMonth(3).TemplateId);
    }
}
