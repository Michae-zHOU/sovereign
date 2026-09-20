using System.Collections.Generic;
using System.Linq;

namespace Sovereign.Simulation;

public sealed partial class CountryState
{
    public Dictionary<string, decimal> BuyOrders { get; set; } = new();
    public Dictionary<string, decimal> SellOrders { get; set; } = new();
    public Dictionary<string, decimal> ImportOrders { get; set; } = new();
    public Dictionary<string, decimal> ExportOrders { get; set; } = new();
    public List<PopGroup> Pops { get; set; } = new();
    public decimal TariffRevenue { get; set; }
    public decimal GovernmentDividends { get; set; }
    public decimal MarketPriceImpact { get; set; } = .75m;
}

public sealed partial class RegionState
{
    public Dictionary<string, decimal> BuyOrders { get; set; } = new();
    public Dictionary<string, decimal> SellOrders { get; set; } = new();
    public Dictionary<string, decimal> LocalPrices { get; set; } = new();
    public Dictionary<string, decimal> SubsistenceProduction { get; set; } = new();
    public decimal SubsistenceGdp { get; set; }
    public decimal SubsistenceNonMarketValue { get; set; }
    public decimal Infrastructure { get; set; }
    public decimal InfrastructureUsage { get; set; }
    public decimal MarketAccess { get; set; } = 1m;
    public int RailwayLevels { get; set; }
}

// Each row is a household cohort including dependents. No per-person tick or allocation.
public sealed class PopGroup
{
    public string RegionId { get; set; } = "";
    public string ProfessionId { get; set; } = "";
    public long Population { get; set; }
    public long Workforce { get; set; }
    public decimal Wealth { get; set; }
    public decimal Literacy { get; set; }
    public decimal DailyIncome { get; set; }
    public decimal DailyTaxes { get; set; }
    public decimal DailyExpenses { get; set; }
    public decimal NeedsFulfilled { get; set; } = 1m;
    public decimal Radicals { get; set; }
    public decimal Loyalists { get; set; }
    public Dictionary<string, decimal> BuyOrders { get; set; } = new();
}

public static class PopulationCatalog
{
    public const decimal SubsistenceValuePerTenThousand = 4m;
    public static IReadOnlyDictionary<string, string> Professions { get; } = new Dictionary<string, string>
    {
        ["peasants"] = "农民", ["laborers"] = "劳工", ["machinists"] = "技工",
        ["shopkeepers"] = "店主", ["capitalists"] = "资本家", ["aristocrats"] = "贵族", ["unemployed"] = "失业者"
    };
    public static decimal StartingWealth(string id) => id switch
    { "peasants" => 8m, "laborers" => 10m, "machinists" => 14m, "shopkeepers" => 13m, "capitalists" => 22m, "aristocrats" => 24m, _ => 6m };
    // Six-good scenario basket, authored in the same scaled units as the building recipes.
    public static Dictionary<string, decimal> Needs(PopGroup pop)
    {
        decimal n = pop.Population / 10_000m;
        decimal comfort = .4m + pop.Wealth / 20m;
        return new() { ["grain"] = n * 6m, ["timber"] = n * .5m * comfort,
            ["clothes"] = n * .8m * comfort, ["tools"] = n * .025m * comfort };
    }
}
