using Godot;
using Sovereign.Simulation;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovereign.Presentation;

/// <summary>Small, immutable scene instructions. This object does not contain a mesh or instantiate any scene.</summary>
public sealed record SettlementSceneDefinition
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; } = "";
    public required string Name { get; init; } = "";
    public required string CountryId { get; init; } = "";
    public required string RegionId { get; init; } = "";
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required string ArchitectureProfile { get; init; } = "";
    public required int Seed { get; init; }
    public required bool IsPort { get; init; }
    public required DateTime ValidFrom { get; init; }
    public required DateTime ValidToExclusive { get; init; }
    public required string AuthoredScenePath { get; init; } = "";
}

/// <summary>
/// Loads only the requested settlement's small descriptor through Godot's resource filesystem.
/// It does not enumerate descriptors, read index.json, preload meshes or retain a global scene cache.
/// </summary>
public static class SettlementScenes
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    public static SettlementSceneDefinition Load(string cityId)
    {
        var city = GeographyCatalog.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city == null) throw new InvalidDataException("Settlement is not present in the current scenario catalog.");
        string descriptorPath = $"res://Assets/settlements/{cityId}.json";
        if (!Godot.FileAccess.FileExists(descriptorPath)) throw new InvalidDataException($"Missing settlement descriptor: {cityId}.");
        using var file = Godot.FileAccess.Open(descriptorPath, Godot.FileAccess.ModeFlags.Read);
        if (file == null) throw new InvalidDataException($"Cannot read settlement descriptor: {cityId}.");
        if (file.GetLength() > 65536) throw new InvalidDataException("Settlement descriptor exceeds the 64 KiB limit.");
        try
        {
            var result = JsonSerializer.Deserialize<SettlementSceneDefinition>(file.GetAsText(), Options);
            if (result == null || result.SchemaVersion != 1) throw new InvalidDataException("Unsupported settlement descriptor version.");
            if (result.Id != city.Id || result.Name != city.Name || result.CountryId != city.CountryId || result.RegionId != city.RegionId)
                throw new InvalidDataException("Settlement descriptor does not match its catalog identity.");
            if (!double.IsFinite(result.Latitude) || !double.IsFinite(result.Longitude) ||
                result.Latitude < -90 || result.Latitude > 90 || result.Longitude < -180 || result.Longitude > 180 ||
                Math.Abs(result.Latitude - city.Latitude) > .000001 || Math.Abs(result.Longitude - city.Longitude) > .000001)
                throw new InvalidDataException("Settlement descriptor has invalid geographic coordinates.");
            if (result.ArchitectureProfile != GeographyCatalog.Country(city.CountryId).ArchitectureProfile || result.IsPort != city.IsPort || result.Seed < 0)
                throw new InvalidDataException("Settlement scene parameters do not match the scenario.");
            if (result.ValidFrom != Catalog.StartDate || result.ValidToExclusive != Catalog.EndDate.AddDays(1) ||
                result.ValidFrom.Kind != DateTimeKind.Unspecified || result.ValidToExclusive.Kind != DateTimeKind.Unspecified)
                throw new InvalidDataException("Settlement descriptor does not cover the supported campaign dates.");
            string authoredPath = result.AuthoredScenePath ?? throw new InvalidDataException("AuthoredScenePath must be an empty string or a resource path.");
            if (authoredPath.Length > 0)
            {
                if (authoredPath.Length > 512 || !authoredPath.StartsWith("res://Assets/settlements/scenes/", StringComparison.Ordinal) ||
                    authoredPath.Contains("..", StringComparison.Ordinal) || authoredPath.Contains('\\') ||
                    authoredPath.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '/' or ':' or '_' or '-' or '.')) ||
                    !(authoredPath.EndsWith(".glb", StringComparison.Ordinal) || authoredPath.EndsWith(".gltf", StringComparison.Ordinal) ||
                      authoredPath.EndsWith(".tscn", StringComparison.Ordinal) || authoredPath.EndsWith(".scn", StringComparison.Ordinal)))
                    throw new InvalidDataException("Authored scene must use a settlement resource path and a supported scene format.");
                if (!ResourceLoader.Exists(authoredPath)) throw new InvalidDataException("The settlement's authored scene resource is missing.");
            }
            return result;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or OverflowException or ArgumentException)
        {
            throw new InvalidDataException($"Malformed settlement descriptor: {cityId}.", ex);
        }
    }
}
