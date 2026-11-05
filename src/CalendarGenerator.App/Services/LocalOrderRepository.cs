using System.IO;
using System.Text.Json;
using CalendarGenerator.Core.Orders;

namespace CalendarGenerator.App.Services;

public sealed class LocalOrderRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IReadOnlyList<OrderRecord> LoadAll(string sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
            return [];

        var list = new List<OrderRecord>();
        foreach (var yearDir in Directory.GetDirectories(sourceRoot))
        {
            foreach (var orderDir in Directory.GetDirectories(yearDir))
            {
                var meta = FindMetaJson(orderDir);
                if (meta is null) continue;
                try
                {
                    var json = File.ReadAllText(meta);
                    var order = JsonSerializer.Deserialize<OrderRecord>(json, JsonOptions);
                    if (order is null) continue;
                    order.LocalFolderPath = orderDir;
                    if (string.IsNullOrWhiteSpace(order.OrderId))
                        order.OrderId = Path.GetFileName(orderDir);
                    list.Add(order);
                }
                catch
                {
                    // skip corrupt
                }
            }
        }

        return list
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.OrderId)
            .ToList();
    }

    public void Save(OrderRecord order)
    {
        if (string.IsNullOrWhiteSpace(order.LocalFolderPath))
            throw new InvalidOperationException("LocalFolderPath hiányzik.");
        var metaDir = Path.Combine(order.LocalFolderPath, "00_Megrendelo_adatai");
        Directory.CreateDirectory(metaDir);
        var path = Path.Combine(metaDir, "megrendeles.json");
        order.UpdatedAt = DateTime.UtcNow.ToString("O");
        File.WriteAllText(path, JsonSerializer.Serialize(order, JsonOptions));
    }

    private static string? FindMetaJson(string orderDir)
    {
        var preferred = Path.Combine(orderDir, "00_Megrendelo_adatai", "megrendeles.json");
        if (File.Exists(preferred)) return preferred;
        var alt = Path.Combine(orderDir, "order.json");
        if (File.Exists(alt)) return alt;
        return Directory.EnumerateFiles(orderDir, "megrendeles.json", SearchOption.AllDirectories)
            .FirstOrDefault();
    }
}
