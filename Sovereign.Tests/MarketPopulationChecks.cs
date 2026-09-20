using Sovereign.Simulation;

internal static class MarketPopulationChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(decimal expected, decimal actual, string message) => Check(Math.Abs(expected - actual) < .000000001m, message + $": expected {expected}, got {actual}");
    private static void Advance(SimulationEngine game, int days)
    {
        var target = game.State.Date.AddDays(days);
        while (game.State.Date < target && !game.State.Finished)
        {
            if (game.State.PendingEvent is { } ev) Check(game.ResolveEvent(ev.Options[^1].Id).Success, "Resolve affordable historical choice");
            game.AdvanceDays((target - game.State.Date).Days);
        }
    }

    public static void PricingAndLocalMarkets()
    {
        Near(2m, SimulationEngine.OrderPrice("grain", 100, 100), "Balanced orders yield base price");
        Near(3.5m, SimulationEngine.OrderPrice("grain", 200, 100), "Demand excess caps at +75%");
        Near(.5m, SimulationEngine.OrderPrice("grain", 100, 200), "Supply excess floors at -75%");
        Near(2.75m, SimulationEngine.OrderPrice("grain", 150, 100), "Intermediate slope remains continuous");
        Near(2m, SimulationEngine.OrderPrice("grain", 0, 0), "Empty market does not divide by zero");
        Near(3.5m, SimulationEngine.OrderPrice("grain", 1, 0), "Absent supply reaches ceiling");
        var scarce = SimulationEngine.NewGame("PRU"); var district = scarce.Player.Regions[0];
        scarce.Player.BuyOrders["grain"] = 200m; scarce.Player.SellOrders["grain"] = 120m;
        district.BuyOrders["grain"] = 100m; district.SellOrders["grain"] = 60m; district.MarketAccess = 1m;
        Near(.7m, SimulationEngine.GetRegionalInputAvailability(scarce.Player, district, "grain"), "Connected provinces share the national shortage instead of each reusing the entire national supply");
        district.MarketAccess = 0m; district.SellOrders["grain"] = 0m;
        Near(.25m, SimulationEngine.GetRegionalInputAvailability(scarce.Player, district, "grain"), "An isolated province cannot borrow unavailable national supply");
        var game = SimulationEngine.NewGame("QNG");
        var region = game.Player.Regions.Single(r => r.Id == "QNG_jiangsu");
        var city = game.Player.Cities.First(c => c.RegionId == region.Id);
        city.Industries["textile"] = 60;
        game.AdvanceDays(1);
        Check(region.MarketAccess < 1m, "Industrial concentration consumes regional infrastructure");
        foreach (var good in Catalog.Goods)
        {
            decimal weight = game.Player.MarketPriceImpact * region.MarketAccess;
            decimal expected = SimulationEngine.OrderPrice(good, region.BuyOrders[good], region.SellOrders[good]) * (1m - weight) + game.Player.Prices[good] * weight;
            Near(expected, region.LocalPrices[good], "Local and national prices blend by access");
        }
        Check(Catalog.Goods.Any(g => Math.Abs(region.LocalPrices[g] - game.Player.Prices[g]) > .001m), "Local supply produces different prices");
        Check(!game.ExpandRailway(region.Id).Success, "Railways require technology");
        game.Player.Technologies.Add("railways");
        int before = region.RailwayLevels;
        Check(game.ExpandRailway(region.Id).Success && region.RailwayLevels == before, "Railways enter construction, not instant upgrade");
        SimulationEngine.LoadJson(game.SaveJson());
    }

    public static void OrdersAndHouseholdAccounts()
    {
        var game = SimulationEngine.NewGame("PRU");
        game.Player.PrivateConstructionEnabled = false;
        var before = game.Player.Cities.SelectMany(city => city.Buildings.Select(b => (city.Id, b.Key, b.Value.CashReserves, b.Value.Debt))).ToDictionary(x => (x.Id, x.Key));
        game.AdvanceDays(1);
        foreach (var country in game.State.Countries)
        {
            Check(country.Pops.Sum(p => p.Population) == country.Population, "All people belong to exactly one cohort");
            foreach (var region in country.Regions)
                Check(country.Pops.Where(p => p.RegionId == region.Id).Sum(p => p.Population) == region.Population, "Cohorts reconcile per region");
            foreach (var good in Catalog.Goods)
            {
                Near(country.BuyOrders[good], country.Regions.Sum(r => r.BuyOrders[good]), "Buy orders aggregate");
                Near(country.SellOrders[good], country.Regions.Sum(r => r.SellOrders[good]), "Sell orders aggregate");
                Near(country.SellOrders[good] - country.ImportOrders[good], country.Production[good], "Only domestic outputs count as production");
                Near(country.BuyOrders[good] - country.ExportOrders[good], country.Consumption[good], "Exports are not domestic consumption");
            }
            Near(country.TaxRevenue, country.Pops.Sum(p => p.DailyTaxes), "Collected taxes reconcile to households");
            Near(country.HouseholdIncome, country.Pops.Sum(p => p.DailyIncome), "Household incomes aggregate");
            Check(country.Pops.All(p => p.NeedsFulfilled is >= 0m and <= 1m && p.Radicals + p.Loyalists <= 1m), "Cohort wellbeing is bounded");
        }
        foreach (var city in game.Player.Cities)
        foreach (var pair in city.Buildings)
        {
            var a = pair.Value; var prior = before[(city.Id, pair.Key)];
            Near(a.DailyRevenue - a.DailyInputCost - a.DailyWages, a.DailyProfit, "Profit is revenue minus operating costs");
            Near(a.DailyProfit, a.CashReserves - prior.CashReserves - a.Debt + prior.Debt + a.DailyDividends, "Firm losses and distributions reconcile to reserves and debt");
        }
        Check(game.Player.Pops.Where(p => p.Population > 0).Select(p => p.Wealth).Distinct().Count() > 3, "Households have differentiated wealth");
        var loaded = SimulationEngine.LoadJson(game.SaveJson());
        Advance(game, 120); Advance(loaded, 120);
        Check(game.CanonicalDigest() == loaded.CanonicalDigest(), "Economic and political state replay exactly");
    }

    public static void MerchantOrdersAndLegacyStocks()
    {
        var import = SimulationEngine.NewGame("GBR"); var baseline = SimulationEngine.NewGame("GBR");
        import.SetTrade("tools", 1); import.AdvanceDays(1); baseline.AdvanceDays(1);
        Near(4m, import.Player.ImportOrders["tools"], "Trade policy adds merchant sell orders");
        Check(import.Player.Prices["tools"] <= baseline.Player.Prices["tools"], "Imports cannot raise same-day national tool price");
        Near(import.Player.TariffRevenue, Catalog.Goods.Sum(g => (import.Player.ImportOrders[g] + import.Player.ExportOrders[g]) * import.Player.Prices[g] * .05m), "Government receives tariffs, not full merchant proceeds");
        Near(import.State.ExternalTreasury, baseline.State.ExternalTreasury, "Legacy treasury no longer finances merchant orders");
        var a = SimulationEngine.NewGame("PRU"); var b = SimulationEngine.NewGame("PRU");
        foreach (var country in b.State.Countries) foreach (var good in Catalog.Goods) country.Goods[good] = 0m;
        a.AdvanceDays(1); b.AdvanceDays(1);
        Check(a.CanonicalDigest() == b.CanonicalDigest(), "Physical legacy stocks cannot affect the new order economy");
    }

    public static void DamagedEconomicSaves()
    {
        Action<SimulationEngine>[] damage = {
            g => g.Player.BuyOrders["grain"] = -1m,
            g => g.Player.Pops[0].Population++,
            g => g.Player.Pops[0].Wealth = 31m,
            g => g.Player.Pops[0].Radicals = 2m,
            g => g.Player.Pops[0].BuyOrders.Remove("tools"),
            g => g.Player.Regions[0].MarketAccess = 1.01m,
            g => g.Player.Regions[0].LocalPrices["grain"] = 0m,
            g => g.Player.SellOrders["grain"]++,
            g => g.Player.InvestmentPool = -1m
        };
        foreach (var mutate in damage)
        {
            var game = SimulationEngine.NewGame("QNG"); mutate(game);
            try { game.SaveJson(); throw new Exception("Invalid economic state was accepted"); }
            catch (InvalidDataException) { }
        }
    }
}
