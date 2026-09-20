using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sovereign.Simulation;

internal static class VersionFiveChecks
{
    // Authentic released-source fixture, generated 2026-09-20 from Sovereign-Source-0.8.zip.
    // Archive SHA-256: 6F0E41BC479BD5C5710B8F91A6E6C930A66152F94099ED97CE5700257B2AF4BD.
    // Only the archive's eleven Sovereign/Simulation/*.cs files were compiled, unchanged, in
    // work/version-five-generator. Models.cs SaveVersion and Persistence.cs envelope Version are 5.
    // Sequence: NewGame(QNG), BuildInCity(QNG_beijing,farm), BuildInCity(QNG_suzhou,textile),
    // SetResearch(mechanical_tools), EnactReform(primary_schools), AdvanceDays(12), SaveJson().
    // The old release reloaded and verified its own checksum before writing the untouched JSON.
    // World date 1836-01-13, day 12, twelve countries, 128 cities, two progressing player projects.
    private const string FixtureSha256 = "DEFBDD7A3D14DF6F844CA14E3ACC2CE6B46D1A6CC409E44684068E83D6D9C59C";
    private const string ReleasedChecksum = "4B8761CE3E132503871309D00F93D8811972798F478B87CFA8607E1D3D2B48C1";

    public static void Run()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-v5-qing.json");
        byte[] bytes = File.ReadAllBytes(path);
        Check(Convert.ToHexString(SHA256.HashData(bytes)) == FixtureSha256, "Released fixture bytes changed; do not recreate it with the current engine");
        string fixture = System.Text.Encoding.UTF8.GetString(bytes);
        var envelope = JsonNode.Parse(fixture)!;
        var original = envelope["State"]!;
        Check(envelope["Version"]!.GetValue<int>() == 5 && original["SaveVersion"]!.GetValue<int>() == 5 &&
            envelope["Checksum"]!.GetValue<string>() == ReleasedChecksum, "Fixture retains the original version-five envelope and checksum");
        var oldPlayer = original["Countries"]!.AsArray().Single(country => country!["Id"]!.GetValue<string>() == "QNG")!;
        Check(oldPlayer["Pops"] == null && oldPlayer["PoliticsInitialized"] == null && oldPlayer["Cities"]![0]!["Buildings"] == null,
            "The fixture predates orders, building accounts and political institutions");
        Check(oldPlayer["Construction"]!.AsArray().Count == 2 && oldPlayer["Construction"]!.AsArray().All(project => project!["ProgressPoints"]!.GetValue<decimal>() > 0m),
            "The released fixture includes real unfinished construction, not only an empty new campaign");

        var game = SimulationEngine.LoadJson(fixture);
        Check(game.State.SaveVersion == 6 && game.State.DayNumber == 12, "Version-five campaign migrates once without advancing time");
        var migrated = JsonSerializer.SerializeToNode(game.State)!;
        // Compare every old serialized world, national, regional, city and queued-project field.
        // New fields may be added; only the schema version itself is allowed to replace an old value.
        foreach (var property in original.AsObject().Where(property => property.Key != "SaveVersion"))
            Preserve(property.Value, migrated[property.Key], "State." + property.Key);

        foreach (var country in game.State.Countries)
        {
            Check(Catalog.Goods.All(good => country.BuyOrders.ContainsKey(good) && country.SellOrders.ContainsKey(good)) &&
                country.BuyOrders.Values.Any(value => value > 0m) && country.SellOrders.Values.Any(value => value > 0m), "Migrated country has initialized market orders: " + country.Id);
            Check(country.Pops.Count == country.Regions.Count * PopulationCatalog.Professions.Count && country.Pops.Sum(pop => pop.Population) == country.Population,
                "Household cohorts conserve the old country population: " + country.Id);
            Check(country.PoliticsInitialized && country.Laws.Count == PoliticalCatalog.LawGroups.Count &&
                country.InterestGroups.Count == PoliticalCatalog.InterestGroups.Count && country.GovernmentGroups.Count > 0,
                "Political institutions and government initialize: " + country.Id);
            Check(country.InvestmentPool == 0m && country.DailyPrivateConstructionSpending == 0m && country.Construction.All(project => !project.PrivateInvestment),
                "Migration does not fabricate investment capital or convert active government projects into private projects");
            foreach (var city in country.Cities)
                Check(city.Buildings.Count == Catalog.Industries.Count && city.Buildings.All(account =>
                    account.Value.MethodId == ProductionCatalog.DefaultMethodId && account.Value.PrivateLevels == city.Industries[account.Key] &&
                    account.Value.CashReserves == 0m && account.Value.Debt == 0m) && city.Buildings.Values.Sum(account => account.Workers) == city.IndustrialWorkers,
                    "Default methods and building ownership initialize without creating levels, cash or workers: " + city.Id);
        }
        Check(game.Player.ResearchId == "mechanical_tools" && game.Player.ResearchProgress > 0m && game.Player.Reforms.Contains("primary_schools"),
            "Unfinished research and already-purchased reform survive migration");
        Check(game.Player.Laws["education"] == "public_schools" && game.Player.InstitutionLevels["education"] == 1 && game.Player.LawEnactment == null,
            "The previously purchased education reform becomes an active institution without another bill or enactment process");

        var replay = SimulationEngine.LoadJson(game.SaveJson());
        Check(game.CanonicalDigest() == replay.CanonicalDigest(), "First version-six save/load exactly preserves the migrated campaign");
        DateTime finalDate = game.State.Date.AddDays(90);
        for (int day = 0; day < 90; day++)
        {
            ResolvePending(game); ResolvePending(replay);
            game.AdvanceDays(1); replay.AdvanceDays(1);
        }
        Check(game.State.Date == finalDate && replay.State.Date == finalDate, "Event handling permits ninety complete simulation days");
        Check(game.CanonicalDigest() == replay.CanonicalDigest(), "Migration followed by save/load replays ninety days identically, including events and construction");
        Check(game.State.Countries.All(country => country.Pops.Sum(pop => pop.Population) == country.Population && country.Regions.Sum(region => region.Population) == country.Population),
            "Migrated population remains reconciled after demographic updates");
        Check(game.CanonicalDigest() == SimulationEngine.LoadJson(game.SaveJson()).CanonicalDigest(), "The replayed campaign remains valid and saveable");
    }

    private static void ResolvePending(SimulationEngine game)
    {
        if (game.State.PendingEvent is { } historicalEvent)
            Check(game.ResolveEvent(historicalEvent.Options[^1].Id).Success, "Resolve the final affordable historical event option");
    }

    private static void Preserve(JsonNode? original, JsonNode? current, string path)
    {
        if (original is JsonObject oldObject)
        {
            Check(current is JsonObject, "Legacy object missing: " + path);
            foreach (var property in oldObject) Preserve(property.Value, current![property.Key], path + "." + property.Key);
        }
        else if (original is JsonArray oldArray)
        {
            Check(current is JsonArray && current.AsArray().Count == oldArray.Count, "Legacy array length changed: " + path);
            for (int index = 0; index < oldArray.Count; index++) Preserve(oldArray[index], current![index], path + "[" + index + "]");
        }
        else Check(JsonNode.DeepEquals(original, current), "Legacy value changed: " + path);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
