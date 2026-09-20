using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sovereign.Simulation;

public sealed partial class SimulationEngine
{
    private static void ValidateOrderEconomy(CountryState c)
    {
        static void Check(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
        static bool Value(decimal n) => n is >= 0m and <= 1_000_000_000_000m;
        static bool Orders(Dictionary<string, decimal>? d) => d != null && d.Count == Catalog.Goods.Count && Catalog.Goods.All(d.ContainsKey) && d.Values.All(Value);
        Check(Orders(c.BuyOrders) && Orders(c.SellOrders) && Orders(c.ImportOrders) && Orders(c.ExportOrders), "Invalid market orders.");
        Check(c.MarketPriceImpact is >= 0m and <= 1m && Value(c.TariffRevenue) && Value(c.GovernmentDividends), "Invalid market accounts.");
        Check(Value(c.HouseholdIncome) && Value(c.TaxRevenue) && Value(c.Spending) && Math.Abs(c.DailyBalance) <= 1_000_000_000_000m && Math.Abs(c.DailyTradeBalance) <= 1_000_000_000_000m, "Invalid daily fiscal accounts.");
        Check(c.Pops != null && c.Pops.Count == c.Regions.Count * PopulationCatalog.Professions.Count && c.Pops.All(p => p != null), "Invalid population cohorts.");
        foreach (var region in c.Regions)
        {
            Check(Orders(region.BuyOrders) && Orders(region.SellOrders) && Orders(region.LocalPrices) && Orders(region.SubsistenceProduction), "Invalid regional market.");
            Check(region.LocalPrices.Values.All(p => p > 0m) && Value(region.SubsistenceGdp) && Value(region.SubsistenceNonMarketValue), "Invalid local prices or rural accounts.");
            Check(Value(region.Infrastructure) && Value(region.InfrastructureUsage) && region.MarketAccess is >= 0m and <= 1m && region.RailwayLevels is >= 0 and <= 20, "Invalid infrastructure.");
            var pops = c.Pops!.Where(p => p.RegionId == region.Id).ToArray();
            Check(pops.Length == PopulationCatalog.Professions.Count && pops.Select(p => p.ProfessionId).Order().SequenceEqual(PopulationCatalog.Professions.Keys.Order()), "Invalid or duplicate profession IDs.");
            Check(pops.Sum(p => p.Population) == region.Population, "Pop cohorts do not reconcile with regional population.");
            foreach (var pop in pops)
            {
                Check(pop.Population is >= 0 and <= 2_000_000_000 && pop.Workforce == (long)(pop.Population * .45m), "Invalid cohort population or workforce.");
                Check(pop.Wealth is >= 1m and <= 30m && pop.Literacy is >= 0m and <= 100m && pop.NeedsFulfilled is >= 0m and <= 1m, "Invalid cohort wellbeing.");
                Check(pop.Radicals is >= 0m and <= 1m && pop.Loyalists is >= 0m and <= 1m && pop.Radicals + pop.Loyalists <= 1m, "Invalid political loyalties.");
                Check(Value(pop.DailyIncome) && Value(pop.DailyTaxes) && Value(pop.DailyExpenses) && Orders(pop.BuyOrders), "Invalid household accounts.");
            }
        }
        foreach (var good in Catalog.Goods)
        {
            Check(Math.Abs(c.BuyOrders[good] - c.Regions.Sum(r => r.BuyOrders[good])) < .000000001m && Math.Abs(c.SellOrders[good] - c.Regions.Sum(r => r.SellOrders[good])) < .000000001m, "National and local orders differ.");
        }
        Check(Value(c.InvestmentPool) && Value(c.DailyInvestmentContribution) && Value(c.DailyPrivateConstructionSpending) && c.Construction.All(p => !p.PrivateInvestment || !p.LegacyPrepaid), "Invalid private investment accounts.");
    }
}
