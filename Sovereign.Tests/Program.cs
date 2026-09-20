using System.Text.Json.Nodes;
using System.Globalization;
using Sovereign.Simulation;

var tests = new (string Name, Action Run)[]
{
    ("Order prices and infrastructure produce local market differences", MarketPopulationChecks.PricingAndLocalMarkets),
    ("Household cohorts and firm accounts reconcile and replay", MarketPopulationChecks.OrdersAndHouseholdAccounts),
    ("Merchant orders replace government inventory trading", MarketPopulationChecks.MerchantOrdersAndLegacyStocks),
    ("Malformed market orders and population saves are rejected", MarketPopulationChecks.DamagedEconomicSaves),
    ("Production methods alter labor, qualifications and input recipes", ProductionChecks.MethodsAndEmployment),
    ("Production method rejection and persistence", ProductionChecks.RejectionsAndRoundtrip),
    ("Private investors fund independent construction and ownership", ConstructionChecks.PrivateInvestmentAndFunding),
    ("Construction freezes daily inputs and railways use the queue", ConstructionChecks.SnapshotAndRailway),
    ("Interest groups, staged legislation and institution budgets", PoliticalChecks.Run),
    ("Diplomatic offers, rejection reasons and charged effects stay consistent", DiplomaticConversationChecks.OfferConsistency),
    ("Leader dialogue follows relations, treaty and saved diplomatic state", DiplomaticConversationChecks.ConversationAndPersistence),
    ("Qing provinces, frontier administrations and city coordinates are selectable", QingProvinceChecks.Geography),
    ("Released version-four saves preserve people, industry and active queues", QingProvinceChecks.Migration),
    ("Authentic version-five saves migrate without rewriting player history", VersionFiveChecks.Run),
    ("Historical leaders, successions and regencies cover all scenario dates", LeaderChecks.Run),
    ("City construction creates local jobs and national goods", RegionalChecks.CityConstruction),
    ("Population growth and internal migration reconcile exactly", RegionalChecks.PopulationMigration),
    ("All twelve local economies replay deterministically after save", RegionalChecks.AllCountriesReplay),
    ("Original version-one saves migrate while preserving player decisions", RegionalChecks.LegacyMigration),
    ("Version-two Qing saves expand without creating population or industry", RegionalChecks.QingVersionTwoMigration),
    ("New Qing settlements produce goods and accept local construction", RegionalChecks.QingExpansion),
    ("Malformed cities, regions, workforces and foreign queues are rejected", RegionalChecks.InvalidGeography),
    ("Twelve countries and 128 cities have valid starting markets", () =>
    {
        foreach (var id in GeographyCatalog.Countries.Select(c => c.Id))
        {
            var game = SimulationEngine.NewGame(id);
            Equal(id, game.Player.Id); Equal(Catalog.StartDate, game.State.Date);
            Equal(12, game.State.Countries.Count);
            Equal(128, game.State.Countries.Sum(c => c.Cities.Count));
            Assert(game.State.Countries.All(c => c.Cities.All(city => city.Industries.Values.Sum() > 0)), "Every city has productive capacity");
            Assert(game.State.Countries.All(c => Catalog.Goods.All(g => c.Goods[g] > 0 && c.Prices[g] > 0)), "Initial stock and prices");
            SimulationEngine.LoadJson(game.SaveJson());
        }
        Throws<ArgumentException>(() => SimulationEngine.NewGame("XYZ"));
    }),
    ("Invalid commands leave state unchanged", () =>
    {
        var game = SimulationEngine.NewGame("GBR");
        var before = game.CanonicalDigest();
        Assert(!game.Build("unknown").Success, "Reject unknown building");
        Assert(!game.SetTaxRate(51).Success && !game.SetTaxRate(-1).Success, "Reject invalid taxes");
        Assert(!game.SetTrade("grain", 4).Success && !game.SetTrade("gold", 1).Success, "Reject invalid trade");
        Assert(!game.EnactReform("unknown").Success && !game.SetResearch("steam_power").Success, "Reject unknown policy / prerequisite");
        Assert(!game.ResolveEvent("unknown").Success && !game.Diplomacy("GBR", "improve").Success, "Reject unavailable commands");
        Assert(!game.Diplomacy("PRU", "unknown").Success, "Reject unknown mission");
        Equal(before, game.CanonicalDigest());
        Throws<ArgumentOutOfRangeException>(() => game.AdvanceDays(-1));
    }),
    ("Construction shares national points and charges actual ongoing expenses", ConstructionChecks.AllocationAndExpenses),
    ("Construction priority, pause, cancellation and completion spillover", ConstructionChecks.QueueControlAndSpillover),
    ("Construction shortages, methods and investment create real capacity", ConstructionChecks.ShortageAndSectorInvestment),
    ("Prepaid construction migration and queued-project deterministic replay", ConstructionChecks.PrepaidMigrationAndReplay),
    ("Invalid construction state and completed-campaign guards", ConstructionChecks.InvalidConstructionAndCampaignGuards),
    ("Historical decisions pause time and apply explicit consequences", () =>
    {
        var game = SimulationEngine.NewGame("GBR");
        game.AdvanceDays(500); Equal(new DateTime(1836, 3, 1), game.State.Date);
        Equal("industrial_petition", game.State.PendingEvent!.Id);
        var digest = game.CanonicalDigest(); game.AdvanceDays(20); Equal(digest, game.CanonicalDigest());
        Assert(!game.ResolveEvent("bogus").Success, "Invalid choice rejected");
        decimal literacy = game.Player.Literacy, cash = game.Player.Treasury;
        Assert(game.ResolveEvent("schools").Success, "Choose schools");
        Equal(literacy + 3m, game.Player.Literacy); Equal(cash - 900m, game.Player.Treasury);
        Assert(game.State.PendingEvent == null && game.State.CompletedEvents.Contains("industrial_petition"), "Resolved once");
        Assert(!game.ResolveEvent("schools").Success, "Cannot repeat choice");
        game.AdvanceDays(1); Equal(new DateTime(1836, 3, 2), game.State.Date);
    }),
    ("Research unlocks selectable industrial production methods", () =>
    {
        var game = SimulationEngine.NewGame("PRU");
        var city = game.Player.Cities.First(c => c.Industries["toolworks"] > 0);
        Assert(!game.SetProductionMethod(city.Id, "toolworks", "mechanized").Success, "Industrial method starts locked");
        Assert(game.SetResearch("mechanical_tools").Success, "Research begins");
        Advance(game, 500);
        Assert(game.Player.Technologies.Contains("mechanical_tools"), "Technology completes");
        Assert(game.SetProductionMethod(city.Id, "toolworks", "mechanized").Success, "Research unlocks an actual recipe");
        Assert(game.SetResearch("steam_power").Success, "Next prerequisite unlocked");
    }),
    ("Diplomacy observes prerequisites, cooldown and mobilization upkeep", () =>
    {
        var game = SimulationEngine.NewGame("GBR");
        Assert(!game.Diplomacy("PRU", "trade_agreement").Success, "Relation requirement");
        Assert(game.Diplomacy("PRU", "improve").Success, "Relations improved");
        Equal(25, game.Player.Relations["PRU"]); Equal(25, game.State.Countries.Single(c => c.Id == "PRU").Relations["GBR"]);
        Assert(!game.Diplomacy("PRU", "improve").Success, "Cooldown enforced");
        Advance(game, 90); Assert(game.Diplomacy("PRU", "trade_agreement").Success, "Trade agreement");
        Assert(game.Player.TradePacts.Contains("PRU"), "Pact retained");
        Advance(game, 90); Assert(game.Diplomacy("PRU", "mobilize").Success, "Mobilization");
        Assert(!game.Player.TradePacts.Contains("PRU"), "Mobilization cancels trade pact");
        var peaceful = SimulationEngine.LoadJson(game.SaveJson()); peaceful.Diplomacy("PRU", "deescalate");
        game.AdvanceDays(1); peaceful.AdvanceDays(1);
        Equal(45m, game.Player.Spending - peaceful.Player.Spending);
        Assert(game.Diplomacy("PRU", "deescalate").Success && game.Player.MobilizedAgainst == "", "De-escalation available");
    }),
    ("Save-load replay produces identical canonical state", () =>
    {
        var first = SimulationEngine.NewGame("JAP");
        first.Build("farm"); first.SetResearch("mechanical_tools"); first.SetTrade("clothes", 1); first.EnactReform("primary_schools");
        Advance(first, 280);
        var second = SimulationEngine.LoadJson(first.SaveJson()); Equal(first.CanonicalDigest(), second.CanonicalDigest());
        Advance(first, 900); Advance(second, 900); Equal(first.CanonicalDigest(), second.CanonicalDigest());
        // A save made on the event modal must resume at that exact unresolved decision.
        var pending = SimulationEngine.NewGame("PRU"); pending.AdvanceDays(90);
        Equal(pending.CanonicalDigest(), SimulationEngine.LoadJson(pending.SaveJson()).CanonicalDigest());
    }),
    ("Simulation and canonical digest are independent of system culture", () =>
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var first = SimulationEngine.NewGame("GBR"); first.Build("farm"); first.Diplomacy("PRU", "improve"); Advance(first, 95);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var second = SimulationEngine.NewGame("GBR"); second.Build("farm"); second.Diplomacy("PRU", "improve"); Advance(second, 95);
            Equal(first.CanonicalDigest(), second.CanonicalDigest());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }),
    ("Corrupt, wrong-version and invalid saves are rejected", () =>
    {
        var game = SimulationEngine.NewGame("GBR"); var json = game.SaveJson();
        Throws<InvalidDataException>(() => SimulationEngine.LoadJson("{"));
        Throws<InvalidDataException>(() => SimulationEngine.LoadJson(""));
        var edited = JsonNode.Parse(json)!; edited["Version"] = 99;
        Throws<InvalidDataException>(() => SimulationEngine.LoadJson(edited.ToJsonString()));
        edited = JsonNode.Parse(json)!; edited["State"]!["Countries"]![0]!["Treasury"] = 9999999;
        Throws<InvalidDataException>(() => SimulationEngine.LoadJson(edited.ToJsonString()));
        game.Player.Goods["grain"] = -1m; Throws<InvalidDataException>(() => game.SaveJson());
    }),
    ("All twelve ten-year campaigns finish with valid city and national accounts", () =>
    {
        Parallel.ForEach(GeographyCatalog.Countries.Select(c => c.Id), new ParallelOptions { MaxDegreeOfParallelism = 3 }, id =>
        {
            var game = SimulationEngine.NewGame(id);
            game.SetResearch("mechanical_tools"); game.EnactReform("primary_schools"); game.Build("farm");
            while (!game.State.Finished)
            {
                Advance(game, 31);
                foreach (var c in game.State.Countries)
                {
                    Assert(c.Goods.Values.All(v => v >= 0m) && c.Treasury >= 0m && c.Debt >= 0m && c.Population > 0, id + " economic invariants");
                    Assert(c.Industries.Values.All(v => v >= 0 && v <= 800), "Industry bounds");
                }
                Assert(game.State.ExternalGoods.Values.All(v => v >= 0m) && game.State.ExternalTreasury >= 0m, "External ledger bounds");
            }
            Equal(Catalog.EndDate, game.State.Date); Equal(3653, game.State.DayNumber);
            if (id is "GBR" or "PRU" or "JAP") Assert(game.State.CompletedEvents.Contains(id == "GBR" ? "chartists" : id == "PRU" ? "accession" : "tenpo"), "Country historical event");
            Assert(game.State.CompletedEvents.Contains("industrial_petition") && game.State.CompletedEvents.Contains("harvest_pressure"), "Shared original policy dilemmas");
            Assert(game.State.Countries.Where(c => c.Id != id).All(c => c.Technologies.Count > 0), "AI researches across all nations");
            Equal(game.CanonicalDigest(), SimulationEngine.LoadJson(game.SaveJson()).CanonicalDigest());
            var digest = game.CanonicalDigest(); game.AdvanceDays(100); Equal(digest, game.CanonicalDigest());
            Assert(!game.Build("farm").Success && !game.SetTaxRate(30).Success, "Completed campaign immutable to commands");
            Console.WriteLine($"    {id}: treasury {game.Player.Treasury:0}; debt {game.Player.Debt:0}; living {game.Player.LivingStandard:0.00}; literacy {game.Player.Literacy:0.0}; GDP {game.Player.Gdp:0}");
        });
    })
};

if (args.Length > 0) tests = tests.Where(t => args.Any(a => t.Name.Contains(a, StringComparison.OrdinalIgnoreCase))).ToArray();

int failures = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + test.Name + "\n" + ex); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} test groups passed.");
return failures == 0 ? 0 : 1;

static void Advance(SimulationEngine game, int days)
{
    var target = game.State.Date.AddDays(days);
    while (game.State.Date < target && !game.State.Finished)
    {
        if (game.State.PendingEvent is { } ev)
        {
            // Last choice is always affordable, so stress runs never become stuck on a cash gate.
            Assert(game.ResolveEvent(ev.Options[^1].Id).Success, "Resolve fallback event option");
        }
        game.AdvanceDays((target - game.State.Date).Days);
    }
}
static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected exception " + typeof(T).Name);
}
