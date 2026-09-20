using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Sovereign.Simulation;

public sealed class WorldState
{
    public int SaveVersion { get; set; } = 6;
    public string PlayerCountryId { get; set; } = "GBR";
    public DateTime Date { get; set; } = new(1836, 1, 1);
    public int DayNumber { get; set; }
    public List<CountryState> Countries { get; set; } = new();
    public List<string> Log { get; set; } = new();
    public bool Finished { get; set; }
    public GameEvent? PendingEvent { get; set; }
    public HashSet<string> CompletedEvents { get; set; } = new();
    // A finite external trading partner: transfers conserve both stock and money.
    public Dictionary<string, decimal> ExternalGoods { get; set; } = new();
    public decimal ExternalTreasury { get; set; } = 2_000_000m;
}

public sealed partial class CountryState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Adjective { get; set; } = "";
    public long Population { get; set; }
    public decimal Treasury { get; set; }
    public decimal Debt { get; set; }
    public decimal Gdp { get; set; }
    public decimal LivingStandard { get; set; } = 12m;
    public decimal Literacy { get; set; }
    public decimal Unrest { get; set; }
    public decimal DailyBalance { get; set; }
    public decimal TaxRevenue { get; set; }
    public decimal Spending { get; set; }
    public decimal DailyTradeBalance { get; set; }
    public decimal HouseholdIncome { get; set; }
    public decimal NeedsFulfilled { get; set; } = 1m;
    public decimal Prestige { get; set; }
    public int TaxRate { get; set; } = 22;
    public Dictionary<string, decimal> Goods { get; set; } = new();
    public Dictionary<string, decimal> Prices { get; set; } = new();
    public Dictionary<string, decimal> Production { get; set; } = new();
    public Dictionary<string, decimal> Consumption { get; set; } = new();
    public Dictionary<string, int> Industries { get; set; } = new();
    public Dictionary<string, int> Trade { get; set; } = new();
    public List<ConstructionProject> Construction { get; set; } = new();
    public bool ConstructionPaused { get; set; }
    public int NextConstructionId { get; set; } = 1;
    public decimal ConstructionSpending { get; set; }
    public decimal ConstructionMaterialSpending { get; set; }
    public decimal ConstructionWages { get; set; }
    public decimal ConstructionPoints { get; set; }
    public HashSet<string> Reforms { get; set; } = new();
    public HashSet<string> Technologies { get; set; } = new();
    public string ResearchId { get; set; } = "";
    public decimal ResearchProgress { get; set; }
    public Dictionary<string, int> Relations { get; set; } = new();
    public Dictionary<string, int> DiplomaticCooldowns { get; set; } = new();
    public HashSet<string> TradePacts { get; set; } = new();
    public string MobilizedAgainst { get; set; } = "";
    public List<HistoryPoint> History { get; set; } = new();
    public List<RegionState> Regions { get; set; } = new();
    public List<CityState> Cities { get; set; } = new();
    [JsonIgnore] public long UrbanPopulation => Cities.Sum(c => c.Population);
    [JsonIgnore] public decimal Urbanization => Population > 0 ? UrbanPopulation * 100m / Population : 0m;
}

public sealed partial class RegionState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public long RuralPopulation { get; set; }
    public long UrbanPopulation { get; set; }
    public decimal Gdp { get; set; }
    [JsonIgnore] public long Population => RuralPopulation + UrbanPopulation;
}

public sealed partial class CityState
{
    public string Id { get; set; } = "";
    public string RegionId { get; set; } = "";
    public string Name { get; set; } = "";
    public long Population { get; set; }
    public long Workforce { get; set; }
    public long Employed { get; set; }
    public long IndustrialWorkers { get; set; }
    public long ConstructionWorkers { get; set; }
    public int ConstructionSectors { get; set; }
    public string ConstructionMethodId { get; set; } = "wood";
    public long Artisans { get; set; }
    public long Dependents { get; set; }
    public long MigrationLastMonth { get; set; }
    public decimal LivingStandard { get; set; }
    public decimal Literacy { get; set; }
    public decimal Gdp { get; set; }
    public Dictionary<string, int> Industries { get; set; } = new();
    public Dictionary<string, decimal> Production { get; set; } = new();
    [JsonIgnore] public List<ConstructionProject> Construction { get; set; } = new();
    [JsonIgnore] public long Unemployed => Workforce - Employed;
    [JsonIgnore] public decimal EmploymentRate => Workforce > 0 ? Employed * 100m / Workforce : 0m;
    [JsonIgnore] public decimal Urbanization => 100m;
}

public sealed partial class ConstructionProject
{
    public string Id { get; set; } = "";
    public string CityId { get; set; } = "";
    public string IndustryId { get; set; } = "";
    public string Name { get; set; } = "";
    public int DaysRemaining { get; set; }
    public int TotalDays { get; set; }
    public decimal RequiredPoints { get; set; }
    public decimal ProgressPoints { get; set; }
    public bool Paused { get; set; }
    public bool LegacyPrepaid { get; set; }
}

public sealed class HistoryPoint
{
    public DateTime Date { get; set; }
    public decimal Gdp { get; set; }
    public decimal Treasury { get; set; }
    public decimal LivingStandard { get; set; }
}

public sealed class GameEvent
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string HistoricalContext { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public List<EventOption> Options { get; set; } = new();
}

public sealed class EventOption
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed record CommandResult(bool Success, string Message);

public sealed record IndustryDefinition(string Id, string Name, string Description, decimal Cost, int Days,
    string Produces, decimal Output, IReadOnlyDictionary<string, decimal> Inputs,
    decimal TimberCost, decimal ToolCost);
public sealed record ReformDefinition(string Id, string Name, string Description, decimal Cost, decimal DailyCost);
public sealed record TechnologyDefinition(string Id, string Name, string Description, int Days);
