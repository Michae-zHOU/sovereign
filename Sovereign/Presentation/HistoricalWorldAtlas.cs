using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sovereign.Presentation;

/// <summary>A reference atlas, not a scenario's diplomatic or ownership state.</summary>
public sealed class HistoricalCountry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourceName { get; set; } = "";
    public int SourceYear { get; set; } = 1815;
    public string Description { get; set; } = "";
    public string? ScenarioCountryId { get; set; }
    public float Longitude { get; set; }
    public float Latitude { get; set; }
    public int ColorIndex { get; set; }
    public string ColorHex { get; set; } = "879181";
    public HistoricalPolygon[] Polygons { get; set; } = Array.Empty<HistoricalPolygon>();
    public double Area { get; set; }
}

public sealed class HistoricalPolygon
{
    // Longitude/latitude pairs. Ring 0 is the exterior; subsequent rings are holes.
    public float[][][] Rings { get; set; } = Array.Empty<float[][]>();
    public float[] Bounds { get; set; } = Array.Empty<float>();

    public bool Contains(float longitude, float latitude)
    {
        if (Bounds.Length != 4 || longitude < Bounds[0] || longitude > Bounds[2] || latitude < Bounds[1] || latitude > Bounds[3]) return false;
        if (Rings.Length == 0 || !ContainsRing(Rings[0], longitude, latitude)) return false;
        for (int i = 1; i < Rings.Length; i++) if (ContainsRing(Rings[i], longitude, latitude)) return false;
        return true;
    }

    private static bool ContainsRing(float[][] ring, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            var a = ring[i]; var b = ring[j];
            if ((a[1] > y) != (b[1] > y) && x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]) inside = !inside;
        }
        return inside;
    }
}

public static class HistoricalWorldAtlas
{
    public const int SourceYear = 1815;
    public const string SourceUrl = "https://github.com/aourednik/historical-basemaps/tree/da7a4b735ecef70aebdc9c73e409d8a2500d50f3";
    public const string ReferenceNotice = "疆域参考：1815年 · 非1836年精确边界";
    private static HistoricalCountry[]? _countries;
    private static Dictionary<string, HistoricalCountry> _byId = new();
    private static readonly Dictionary<(int, int), List<(HistoricalCountry Country, HistoricalPolygon Polygon)>> Cells = new();
    public static IReadOnlyList<HistoricalCountry> Countries { get { EnsureLoaded(); return _countries!; } }

    public static HistoricalCountry? Country(string id)
    {
        EnsureLoaded(); return _byId.GetValueOrDefault(id);
    }

    public static HistoricalCountry? At(float longitude, float latitude)
    {
        EnsureLoaded();
        if (!float.IsFinite(longitude) || !float.IsFinite(latitude) || latitude < -90 || latitude > 90) return null;
        longitude = (longitude + 180) % 360; if (longitude < 0) longitude += 360; longitude -= 180;
        if (!Cells.TryGetValue(Cell(longitude, latitude), out var candidates)) return null;
        // Small named enclaves precede surrounding polygons; unassigned areas never hide named ones.
        foreach (var candidate in candidates)
            if (candidate.Polygon.Contains(longitude, latitude)) return candidate.Country;
        return null;
    }

    private static (int, int) Cell(float lon, float lat) => ((int)MathF.Floor(lon / 5), (int)MathF.Floor(lat / 5));

    private static void EnsureLoaded()
    {
        if (_countries != null) return;
        string json = Godot.FileAccess.GetFileAsString("res://Assets/map/historical/atlas_1815.json");
        _countries = JsonSerializer.Deserialize<HistoricalCountry[]>(json) ?? throw new InvalidOperationException("Historical reference atlas could not be read.");
        _byId = _countries.ToDictionary(c => c.Id, StringComparer.Ordinal);
        foreach (var country in _countries.OrderBy(c => c.Id == "H1815_UNASSIGNED").ThenBy(c => c.Area))
        foreach (var polygon in country.Polygons)
        {
            var b = polygon.Bounds;
            for (int x = (int)MathF.Floor(b[0] / 5); x <= (int)MathF.Floor(b[2] / 5); x++)
            for (int y = (int)MathF.Floor(b[1] / 5); y <= (int)MathF.Floor(b[3] / 5); y++)
            {
                if (!Cells.TryGetValue((x, y), out var list)) Cells[(x, y)] = list = new();
                list.Add((country, polygon));
            }
        }
    }
}
