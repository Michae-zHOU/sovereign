using System.Text.Json.Nodes;
using Sovereign.Simulation;

internal static class QingProvinceChecks
{
    public static void Geography()
    {
        Check(QingProvinceCatalog.Provinces.Count == 27, "Eighteen provinces and nine frontier administration groups");
        Check(QingProvinceCatalog.Provinces.Count(p => p.IsProvince) == 18, "Exactly eighteen Qing provinces");
        var game = SimulationEngine.NewGame("QNG"); game.Player.PrivateConstructionEnabled = false;
        Check(game.Player.Regions.Count == 27 && game.Player.Regions.All(r => r.RuralPopulation > 0), "All provinces carry real simulation populations");
        Check(game.Player.Population == 400_000_000, "Province allocation preserves national population exactly");
        foreach (var city in GeographyCatalog.Cities.Where(c => c.CountryId == "QNG"))
        {
            var province = QingProvinceCatalog.ProvinceAt(city.Longitude, city.Latitude);
            Check(province?.Id == city.RegionId, $"{city.Id} maps into {city.RegionId}, got {province?.Id ?? "ocean"}");
            Check(game.GetCity(city.Id).RegionId == city.RegionId, "Definition and active city administrative membership agree");
        }
        foreach (var province in QingProvinceCatalog.Provinces)
            Check(QingProvinceCatalog.ProvinceAt(province.Longitude, province.Latitude)?.Id == province.Id,
                "Region navigation coordinate selects " + province.Name);
        var points = new[]
        {
            (120.2, 23.0, "QNG_fujian"), (110.3, 19.5, "QNG_guangdong"), (106.2, 38.5, "QNG_gansu"),
            (101.77, 36.62, "QNG_gansu"), (108.94, 34.34, "QNG_shaanxi"), (126.55, 43.84, "QNG_jilin")
        };
        foreach (var (longitude, latitude, expected) in points)
            Check(QingProvinceCatalog.ProvinceAt(longitude, latitude)?.Id == expected, "Historical province geography at " + longitude + "," + latitude);
        Check(QingProvinceCatalog.ProvinceAt(0, 0) == null && QingProvinceCatalog.ProvinceAt(130, 30) == null &&
            QingProvinceCatalog.ProvinceAt(double.NaN, 30) == null, "Ocean, remote and non-finite coordinates do not select a province");
        Check(QingProvinceCatalog.Province("QNG_ili").Administration != "行省" &&
            QingProvinceCatalog.Province("QNG_rehe").Administration != "行省", "Frontier administrations are not labeled anachronistic provinces");
        Check(game.Player.Regions.Single(r => r.Id == "QNG_gansu").UrbanPopulation == 0, "Province with no modeled city remains available as a rural administration");
        SimulationEngine.LoadJson(game.SaveJson());
    }

    public static void Migration()
    {
        // Generated from the actual 0.5 release source archive, before province changes, with two active projects.
        string fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-v4-qing.json"));
        var original = JsonNode.Parse(fixture)!["State"]!;
        var oldCountry = original["Countries"]!.AsArray().Single(c => c!["Id"]!.GetValue<string>() == "QNG")!;
        var game = SimulationEngine.LoadJson(fixture);
        var current = System.Text.Json.JsonSerializer.SerializeToNode(game.Player)!;
        Check(game.State.SaveVersion == 6 && game.Player.Regions.Count == 27, "Released save migrates to province schema");
        foreach (var property in oldCountry.AsObject().Where(p => p.Key is not ("Cities" or "Regions" or "Construction")))
            Check(JsonNode.DeepEquals(property.Value, current[property.Key]), "Country state preserved: " + property.Key);
        var oldQueue = oldCountry["Construction"]!.AsArray();
        for (int index = 0; index < oldQueue.Count; index++)
        {
            var migrated = current["Construction"]![index]!;
            foreach (var property in oldQueue[index]!.AsObject())
                Check(JsonNode.DeepEquals(property.Value, migrated[property.Key]), "Queued project field preserved: " + property.Key);
            Check(!game.Player.Construction[index].PrivateInvestment, "Legacy government projects remain public");
        }
        foreach (var oldCity in oldCountry["Cities"]!.AsArray())
        {
            var sameCity = current["Cities"]!.AsArray().Single(c => c!["Id"]!.GetValue<string>() == oldCity!["Id"]!.GetValue<string>())!;
            foreach (var property in oldCity!.AsObject().Where(p => p.Key != "RegionId"))
                Check(JsonNode.DeepEquals(property.Value, sameCity[property.Key]), "City field preserved: " + property.Key);
        }
        Check(game.Player.Regions.Sum(r => r.RuralPopulation) == oldCountry["Regions"]!.AsArray().Sum(r => r!["RuralPopulation"]!.GetValue<long>()), "Every rural resident preserved");
        Check(game.Player.Construction.Count == 2 && game.Player.Construction[0].ProgressPoints > 0m, "Current construction targets and progress retained");
        var replay = SimulationEngine.LoadJson(game.SaveJson());
        Check(replay.CanonicalDigest() == game.CanonicalDigest(), "Province save roundtrip exactly reproduces migrated state");
        for (int day = 0; day < 90; day++)
        {
            if (game.State.PendingEvent is { } ev) game.ResolveEvent(ev.Options[^1].Id);
            if (replay.State.PendingEvent is { } other) replay.ResolveEvent(other.Options[^1].Id);
            game.AdvanceDays(1); replay.AdvanceDays(1);
        }
        Check(replay.CanonicalDigest() == game.CanonicalDigest(), "Province state replays identically across demographic updates and project completions");
        Check(game.Player.Population == game.Player.Regions.Sum(r => r.Population), "Province population remains conserved after migration and growth");
    }

    private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
}
