using Sovereign.Simulation;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;

internal static class RegionalChecks
{
    public static void CityConstruction()
    {
        var game = SimulationEngine.NewGame("PRU"); game.Player.PrivateConstructionEnabled = false;
        var baseline = SimulationEngine.NewGame("PRU"); baseline.Player.PrivateConstructionEnabled = false;
        var breslau = game.GetCity("PRU_breslau");
        var berlin = game.GetCity("PRU_berlin");
        int cityFarm = breslau.Industries["farm"], capitalFarm = berlin.Industries["farm"];
        long workers = breslau.IndustrialWorkers;
        Check(game.BuildInCity(breslau.Id, "farm").Success, "Local farm starts");
        Check(game.BuildInCity(berlin.Id, "lumber").Success, "Second city starts concurrently");
        Check(breslau.Construction.Single().CityId == breslau.Id, "Local queue points to actual project");
        var before = game.CanonicalDigest();
        Check(!game.BuildInCity("FRA_paris", "farm").Success, "Foreign construction rejected");
        Check(!game.BuildInCity("missing", "farm").Success, "Unknown city rejected");
        Check(before == game.CanonicalDigest(), "Rejected commands are atomic");
        game.AdvanceDays(45); baseline.AdvanceDays(45);
        Check(breslau.Industries["farm"] == cityFarm + 1 && berlin.Industries["farm"] == capitalFarm, "Capacity added only to selected city");
        Check(berlin.Construction.Single().ProgressPoints > 0m && berlin.Construction.Single().ProgressPoints < berlin.Construction.Single().RequiredPoints, "Different cities share national capacity rather than independent timers");
        Check(breslau.IndustrialWorkers > workers, "Completed industry hires local workers");
        game.AdvanceDays(1); baseline.AdvanceDays(1);
        Check(breslau.Production["grain"] > baseline.GetCity(breslau.Id).Production["grain"], "Local industry produces actual market goods");
        foreach (var good in Catalog.Goods) Check(game.Player.Production[good] == game.Player.Cities.Sum(c => c.Production[good]) + game.Player.Regions.Sum(r => r.SubsistenceProduction.GetValueOrDefault(good)), "National production equals city production");
        Check(game.Player.Gdp == game.Player.Cities.Sum(c => c.Gdp) + game.Player.Regions.Sum(r => r.SubsistenceGdp), "City value added aggregates into national GDP");
        Check(game.Player.Industries["farm"] == game.Player.Cities.Sum(c => c.Industries["farm"]), "Capacity reconciles");
        var loaded = SimulationEngine.LoadJson(game.SaveJson());
        Check(loaded.GetCity(berlin.Id).Construction.Single().ProgressPoints == berlin.Construction.Single().ProgressPoints, "Local point progress restored after load");
    }

    public static void PopulationMigration()
    {
        var game = SimulationEngine.NewGame("GBR");
        game.AdvanceDays(30);
        var country = game.Player;
        long beforeNational = country.Population;
        var beforeCities = country.Cities.Select(c => c.Population).ToArray();
        var beforeRural = country.Regions.Select(r => r.RuralPopulation).ToArray();
        game.AdvanceDays(1);
        Check(game.State.Date == new DateTime(1836, 2, 1), "Monthly boundary reached");
        decimal growth = .000008m + SimulationEngine.GetHealthcareEffect(country) * .000003m - (1m - country.NeedsFulfilled) * .000015m;
        long cityGrowth = beforeCities.Sum(p => (long)(p * growth));
        long ruralGrowth = beforeRural.Sum(p => (long)(p * growth));
        long moved = country.Cities.Sum(c => c.MigrationLastMonth);
        Check(moved > 0, "Economic opportunity attracts rural migrants");
        Check(country.Population == beforeNational + cityGrowth + ruralGrowth, "Internal migration creates no population");
        Check(country.Cities.Sum(c => c.Population) == beforeCities.Sum() + cityGrowth + moved, "Urban gains reconcile exactly");
        Check(country.Regions.Sum(r => r.RuralPopulation) == beforeRural.Sum() + ruralGrowth - moved, "Rural losses reconcile exactly");
        foreach (var c in game.State.Countries)
        {
            Check(c.Population == c.Cities.Sum(city => city.Population) + c.Regions.Sum(r => r.RuralPopulation), "National population reconciles");
            foreach (var city in c.Cities)
                Check(city.Dependents + city.Artisans + city.IndustrialWorkers + city.ConstructionWorkers + city.Unemployed == city.Population, "Local employment cohorts reconcile");
        }
        SimulationEngine.LoadJson(game.SaveJson());
    }

    public static void AllCountriesReplay()
    {
        foreach (var country in GeographyCatalog.Countries)
        {
            var first = SimulationEngine.NewGame(country.Id);
            first.BuildInCity(first.Player.Cities[1].Id, "farm");
            first.AdvanceDays(35);
            var second = SimulationEngine.LoadJson(first.SaveJson());
            first.AdvanceDays(14); second.AdvanceDays(14);
            Check(first.CanonicalDigest() == second.CanonicalDigest(), country.Id + " local economies replay identically");
        }
    }

    public static void LegacyMigration()
    {
        // This fixture was produced by the released v0.2 simulation, extracted from Sovereign-Source-0.2.zip.
        string fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-v1.json"));
        var original = JsonNode.Parse(fixture)!["State"]!;
        var oldPlayer = original["Countries"]!.AsArray().Single(c => c!["Id"]!.GetValue<string>() == "GBR")!;
        var game = SimulationEngine.LoadJson(fixture);
        Check(game.State.SaveVersion == 6 && game.State.Countries.Count == 12, "Legacy world expanded and versioned");
        Check(game.Player.Population == oldPlayer["Population"]!.GetValue<long>(), "Legacy population preserved");
        Check(game.Player.Treasury == oldPlayer["Treasury"]!.GetValue<decimal>(), "Legacy treasury preserved");
        Check(game.Player.TaxRate == 29 && game.Player.Trade["tools"] == 1 && game.Player.Reforms.Contains("primary_schools"), "Player economic choices preserved");
        Check(game.Player.ResearchId == "mechanical_tools" && game.Player.ResearchProgress == oldPlayer["ResearchProgress"]!.GetValue<decimal>(), "Research preserved");
        Check(game.Player.Relations["PRU"] == 25 && game.State.CompletedEvents.Contains("industrial_petition"), "Diplomacy and event choices preserved");
        Check(game.Player.Construction.Single().CityId == "GBR_london" && game.Player.Construction.Single().IndustryId == "textile", "Legacy queue assigned to capital");
        foreach (var good in Catalog.Goods) Check(game.Player.Goods[good] == oldPlayer["Goods"]![good]!.GetValue<decimal>(), "Legacy inventories preserved");
        foreach (var industry in Catalog.Industries) Check(game.Player.Industries[industry.Id] == oldPlayer["Industries"]![industry.Id]!.GetValue<int>(), "Legacy capacity conserved");
        foreach (var good in Catalog.Goods) Check(Math.Abs(game.Player.Production[good] - game.Player.Cities.Sum(c => c.Production[good]) - game.Player.Regions.Sum(r => r.SubsistenceProduction.GetValueOrDefault(good))) < .000000001m, "Migrated local output reconciles immediately");
        Check(Math.Abs(game.Player.Gdp - game.Player.Cities.Sum(c => c.Gdp)) < .000000001m, "Migrated local GDP reconciles immediately");
        var replay = SimulationEngine.LoadJson(game.SaveJson());
        game.AdvanceDays(15); replay.AdvanceDays(15);
        Check(game.CanonicalDigest() == replay.CanonicalDigest(), "Migrated state saves and replays deterministically");
    }

    public static void InvalidGeography()
    {
        static void Reject(Action<SimulationEngine> mutate)
        {
            var game = SimulationEngine.NewGame("GBR"); mutate(game);
            try { game.SaveJson(); } catch (InvalidDataException) { return; }
            throw new Exception("Malformed local state was accepted");
        }
        Reject(g => g.Player.Cities[0].Id = g.Player.Cities[1].Id);
        Reject(g => g.Player.Cities[0].RegionId = "FRA_north");
        Reject(g => g.Player.Cities[0].Population++);
        Reject(g => g.Player.Cities[0].Employed++);
        Reject(g => g.Player.Cities[0].Production["grain"]++);
        Reject(g => g.Player.Regions[0].RuralPopulation = -1);
        Reject(g => g.Player.Cities[0].Industries["farm"]++);
        Reject(g => { g.Build("farm"); g.Player.Construction[0].CityId = "FRA_paris"; });
    }

    public static void QingVersionTwoMigration()
    {
        // Produced by the actual released v0.3 source, retained in the fixture generator.
        string fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-v2-qing.json"));
        var original = JsonNode.Parse(fixture)!["State"]!;
        var oldCountry = original["Countries"]!.AsArray().Single(c => c!["Id"]!.GetValue<string>() == "QNG")!;
        var game = SimulationEngine.LoadJson(fixture);
        var current = System.Text.Json.JsonSerializer.SerializeToNode(game.Player)!;
        Check(game.State.SaveVersion == 6 && game.Player.Cities.Count == 40, "Qing v2 save gains all 32 cities");
        Check(game.State.Countries.Sum(c => c.Cities.Count) == 128, "Expanded world has 128 cities");
        foreach (var property in oldCountry.AsObject().Where(p => p.Key is not ("Cities" or "Regions" or "Construction")))
            Check(JsonNode.DeepEquals(property.Value, current[property.Key]), "National state preserved: " + property.Key);
        foreach (var oldCity in oldCountry["Cities"]!.AsArray())
        {
            var sameCity = current["Cities"]!.AsArray().Single(c => c!["Id"]!.GetValue<string>() == oldCity!["Id"]!.GetValue<string>());
            foreach (var property in oldCity!.AsObject().Where(p => p.Key != "RegionId"))
                Check(JsonNode.DeepEquals(property.Value, sameCity![property.Key]), "Existing city field preserved exactly: " + property.Key);
        }
        var oldIds = oldCountry["Cities"]!.AsArray().Select(c => c!["Id"]!.GetValue<string>()).ToHashSet();
        var additions = game.Player.Cities.Where(c => !oldIds.Contains(c.Id)).ToArray();
        Check(additions.Length == 32 && additions.All(c => c.Industries.Values.Sum() == 0 && c.Gdp == 0m && c.Production.Values.Sum() == 0m), "Migration creates no free capacity or output");
        long originalRural = oldCountry["Regions"]!.AsArray().Sum(r => r!["RuralPopulation"]!.GetValue<long>());
        Check(originalRural - game.Player.Regions.Sum(r => r.RuralPopulation) == additions.Sum(c => c.Population), "New city residents are transferred from the old rural pool before province allocation");
        Check(game.Player.Population == game.Player.Regions.Sum(r => r.Population), "Province migration keeps every resident");
        var replay = SimulationEngine.LoadJson(game.SaveJson());
        Check(game.CanonicalDigest() == replay.CanonicalDigest(), "Migrated v3 roundtrip is exact");
        Check(game.BuildInCity("QNG_shanghai", "farm").Success && replay.BuildInCity("QNG_shanghai", "farm").Success, "Added settlement accepts real construction");
        for (int day = 0; day < 350; day++)
        {
            if (game.State.PendingEvent is { } ev) game.ResolveEvent(ev.Options[^1].Id);
            if (replay.State.PendingEvent is { } same) replay.ResolveEvent(same.Options[^1].Id);
            game.AdvanceDays(1); replay.AdvanceDays(1);
        }
        Check(game.GetCity("QNG_shanghai").Industries["farm"] == 1 && game.GetCity("QNG_shanghai").Production["grain"] > 0m, "Migrated added city becomes productive after construction");
        Check(game.CanonicalDigest() == replay.CanonicalDigest(), "Expanded legacy state replays deterministically");
    }

    public static void QingExpansion()
    {
        var game = SimulationEngine.NewGame("QNG"); game.Player.PrivateConstructionEnabled = false;
        Check(game.Player.Cities.Count == 40 && game.Player.Regions.Count == 27, "Qing expansion coverage");
        var definition = GeographyCatalog.Country("QNG");
        Check(game.Player.Population == definition.InitialPopulation, "Expanded urban coverage preserves total population");
        for (int index = 0; index < Catalog.Industries.Count; index++)
            Check(game.Player.Industries[Catalog.Industries[index].Id] == definition.IndustryLevels[index], "Initial capacity distributed, not inflated");
        Check(game.Player.Cities.All(c => c.Industries.Values.Sum() > 0), "All 40 new-game settlements have real production capacity");
        int initial = game.GetCity("QNG_chengdu").Industries["farm"];
        Check(game.BuildInCity("QNG_chengdu", "farm").Success, "Expanded western settlement accepts construction");
        game.AdvanceDays(46);
        Check(game.GetCity("QNG_chengdu").Industries["farm"] == initial + 1, "Expanded settlement project completes");
        foreach (var good in Catalog.Goods)
            Check(Math.Abs(game.Player.Production[good] - game.Player.Cities.Sum(c => c.Production[good]) - game.Player.Regions.Sum(r => r.SubsistenceProduction.GetValueOrDefault(good))) < .000000001m, "All 40 city outputs feed national markets");
        Check(game.Player.Population == game.Player.Regions.Sum(r => r.RuralPopulation) + game.Player.Cities.Sum(c => c.Population), "Expanded population balances after running");
        SimulationEngine.LoadJson(game.SaveJson());
    }

    private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
}
