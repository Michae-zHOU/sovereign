using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Sovereign.Simulation;

public sealed record ProductionMethodDefinition(string Id, string Name, IReadOnlyDictionary<string, decimal> Inputs,
    IReadOnlyDictionary<string, decimal> Outputs, int JobsPerLevel, decimal SkilledFraction, string RequiredTechnology);

/// <summary>Original prototype recipes and staffing, expressed per building level per day.</summary>
public static class ProductionCatalog
{
    public const string DefaultMethodId = "basic";
    private static Dictionary<string, decimal> Goods(params (string Good, decimal Amount)[] entries) => entries.ToDictionary(x => x.Good, x => x.Amount);
    private static ProductionMethodDefinition P(string id, string name, string output, decimal quantity, int jobs, decimal skilled, string tech, params (string, decimal)[] inputs) =>
        new(id, name, Goods(inputs), Goods((output, quantity)), jobs, skilled, tech);
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<ProductionMethodDefinition>> Recipes =
        new Dictionary<string, IReadOnlyList<ProductionMethodDefinition>>
        {
            ["farm"] = new[] { P("basic", "畜力耕作", "grain", 18m, 2500, .02m, ""), P("mechanized", "铁制农具", "grain", 30m, 2100, .08m, "mechanical_tools", ("tools", .5m)), P("steam", "蒸汽农机", "grain", 46m, 1600, .16m, "steam_power", ("tools", .8m), ("coal", 2m)) },
            ["lumber"] = new[] { P("basic", "手工伐木", "timber", 12m, 2500, .02m, ""), P("mechanized", "机械锯木", "timber", 22m, 2000, .10m, "mechanical_tools", ("tools", .5m)), P("steam", "蒸汽锯木", "timber", 35m, 1500, .18m, "steam_power", ("tools", .8m), ("coal", 2m)) },
            ["coal_mine"] = new[] { P("basic", "人工采煤", "coal", 10m, 2500, .03m, ""), P("mechanized", "机械凿煤", "coal", 19m, 2300, .10m, "mechanical_tools", ("tools", .6m)), P("steam", "蒸汽排水", "coal", 32m, 1800, .20m, "steam_power", ("tools", 1m), ("iron", 1m)) },
            ["iron_mine"] = new[] { P("basic", "炭火冶铁", "iron", 7m, 2500, .04m, "", ("coal", 3m)), P("mechanized", "机械开采", "iron", 14m, 2300, .12m, "mechanical_tools", ("coal", 4m), ("tools", .6m)), P("steam", "蒸汽采炼", "iron", 24m, 1900, .22m, "steam_power", ("coal", 7m), ("tools", 1m)) },
            ["toolworks"] = new[] { P("basic", "手工锻造", "tools", 6m, 2500, .06m, "", ("iron", 4m), ("coal", 2m), ("timber", 2m)), P("mechanized", "机械机床", "tools", 12m, 2200, .18m, "mechanical_tools", ("iron", 7m), ("coal", 3m), ("timber", 2m)), P("steam", "蒸汽机床", "tools", 20m, 1700, .30m, "steam_power", ("iron", 11m), ("coal", 6m), ("timber", 3m)) },
            ["textile"] = new[] { P("basic", "手工织机", "clothes", 10m, 2500, .04m, "", ("tools", .7m)), P("mechanized", "机械织机", "clothes", 20m, 2100, .12m, "mechanical_tools", ("tools", 1.2m), ("timber", 1m)), P("steam", "蒸汽纺织", "clothes", 34m, 1600, .24m, "steam_power", ("tools", 1.8m), ("coal", 3m)) }
        };
    public static IReadOnlyList<ProductionMethodDefinition> MethodsFor(string industryId) => Recipes.TryGetValue(industryId, out var methods) ? methods : Array.Empty<ProductionMethodDefinition>();
    public static ProductionMethodDefinition Get(string industryId, string methodId) => MethodsFor(industryId).Single(m => m.Id == methodId);
}

public sealed class BuildingAccount
{
    public string MethodId { get; set; } = ProductionCatalog.DefaultMethodId;
    public long Workers { get; set; }
    public decimal EmploymentScale { get; set; } = 1m;
    public decimal DailyRevenue { get; set; }
    public decimal DailyInputCost { get; set; }
    public decimal DailyWages { get; set; }
    public decimal DailyProfit { get; set; }
    public decimal DailyDividends { get; set; }
    public decimal CashReserves { get; set; }
    public decimal Debt { get; set; }
    public int PrivateLevels { get; set; }
}

public sealed partial class CityState
{
    public Dictionary<string, BuildingAccount> Buildings { get; set; } = new();
}

public sealed partial class SimulationEngine
{
    public static void InitializeProduction(CountryState country)
    {
        foreach (var city in country.Cities)
        {
            foreach (var industry in Catalog.Industries)
                if (!city.Buildings.ContainsKey(industry.Id)) city.Buildings[industry.Id] = new BuildingAccount { PrivateLevels = city.Industries[industry.Id] };
            RefreshProductionEmployment(city);
        }
    }

    public static void RefreshProductionEmployment(CityState city)
    {
        city.Workforce = (long)(city.Population * .45m);
        city.Dependents = city.Population - city.Workforce;
        city.Artisans = Math.Min(city.Workforce, (long)(city.Population * .22m));
        long available = city.Workforce - city.Artisans;
        foreach (var industry in Catalog.Industries)
            if (!city.Buildings.ContainsKey(industry.Id)) city.Buildings[industry.Id] = new BuildingAccount { PrivateLevels = city.Industries[industry.Id] };
        decimal jobs = Catalog.Industries.Sum(i => (decimal)city.Industries[i.Id] * ProductionCatalog.Get(i.Id, city.Buildings[i.Id].MethodId).JobsPerLevel * city.Buildings[i.Id].EmploymentScale);
        decimal skilledJobs = Catalog.Industries.Sum(i => (decimal)city.Industries[i.Id] * ProductionCatalog.Get(i.Id, city.Buildings[i.Id].MethodId).JobsPerLevel * ProductionCatalog.Get(i.Id, city.Buildings[i.Id].MethodId).SkilledFraction * city.Buildings[i.Id].EmploymentScale);
        decimal skilledAvailable = available * Math.Clamp(city.Literacy / 100m, 0m, 1m);
        decimal fill = jobs > 0m ? Math.Min(1m, available / jobs) : 1m;
        if (skilledJobs > 0m) fill = Math.Min(fill, skilledAvailable / skilledJobs);
        city.IndustrialWorkers = 0;
        foreach (var industry in Catalog.Industries)
        {
            var account = city.Buildings[industry.Id];
            account.Workers = (long)(city.Industries[industry.Id] * ProductionCatalog.Get(industry.Id, account.MethodId).JobsPerLevel * account.EmploymentScale * fill);
            city.IndustrialWorkers += account.Workers;
        }
        city.ConstructionWorkers = Math.Min(available - city.IndustrialWorkers, (long)city.ConstructionSectors * ConstructionCatalog.WorkersPerSector);
        city.Employed = city.Artisans + city.IndustrialWorkers + city.ConstructionWorkers;
    }

    public static void ValidateProduction(CountryState country)
    {
        static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        static bool Money(decimal value) => value >= 0m && value <= 1_000_000_000_000_000m;
        Require(Money(country.InvestmentPool) && Money(country.DailyInvestmentContribution) && Money(country.DailyPrivateConstructionSpending), "Invalid investment pool accounts.");
        foreach (var city in country.Cities)
        {
            Require(city.Buildings != null && city.Buildings.Count == Catalog.Industries.Count && Catalog.Industries.All(i => city.Buildings.ContainsKey(i.Id)), "Incomplete building accounts.");
            foreach (var industry in Catalog.Industries)
            {
                var account = city.Buildings![industry.Id];
                Require(account != null, "Missing building account.");
                var method = ProductionCatalog.MethodsFor(industry.Id).FirstOrDefault(m => m.Id == account!.MethodId);
                Require(method != null && (method.RequiredTechnology.Length == 0 || country.Technologies.Contains(method.RequiredTechnology)), "Invalid or locked production method.");
                Require(account!.EmploymentScale >= .05m && account.EmploymentScale <= 1m && account.Workers >= 0 &&
                    account.PrivateLevels >= 0 && account.PrivateLevels <= city.Industries[industry.Id], "Invalid building staffing or ownership.");
                Require(Money(account.DailyRevenue) && Money(account.DailyInputCost) && Money(account.DailyWages) && Money(account.DailyDividends) && Money(account.CashReserves) && Money(account.Debt) &&
                    Math.Abs(account.DailyProfit) <= 1_000_000_000_000_000m && account.DailyProfit == account.DailyRevenue - account.DailyInputCost - account.DailyWages &&
                    account.DailyDividends <= Math.Max(0m, account.DailyProfit), "Invalid building financial accounts.");
            }
            var expected = new CityState { Population = city.Population, Literacy = city.Literacy, ConstructionSectors = city.ConstructionSectors,
                Industries = city.Industries, Buildings = city.Buildings!.ToDictionary(p => p.Key, p => new BuildingAccount { MethodId = p.Value.MethodId, EmploymentScale = p.Value.EmploymentScale }) };
            RefreshProductionEmployment(expected);
            Require(city.Workforce == expected.Workforce && city.Dependents == expected.Dependents && city.Artisans == expected.Artisans && city.IndustrialWorkers == expected.IndustrialWorkers &&
                city.ConstructionWorkers == expected.ConstructionWorkers && city.Employed == expected.Employed && city.Buildings!.All(p => p.Value.Workers == expected.Buildings[p.Key].Workers), "Building employment does not reconcile with local labor and skills.");
        }
        Require(country.Construction.All(p => !p.PrivateInvestment || (!p.LegacyPrepaid && p.IndustryId != ConstructionCatalog.SectorId && p.IndustryId != ConstructionCatalog.RailwayId && !p.Paused)), "Invalid private construction project.");
    }

    public CommandResult SetProductionMethod(string cityId, string industryId, string methodId)
    {
        if (State.Finished) return Fail("战役已经结束，无法调整生产方式。");
        var city = Player.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city == null) return Fail("只能调整本国城市的生产方式。");
        if (!city.Industries.ContainsKey(industryId) || ProductionCatalog.MethodsFor(industryId).Count == 0) return Fail("该建筑类型不存在。");
        var method = ProductionCatalog.MethodsFor(industryId).FirstOrDefault(m => m.Id == methodId);
        if (method == null) return Fail("该生产方式不适用于此建筑。");
        if (method.RequiredTechnology.Length > 0 && !Player.Technologies.Contains(method.RequiredTechnology))
            return Fail("尚未掌握所需技术：" + (method.RequiredTechnology == "mechanical_tools" ? "机械工具" : "蒸汽动力"));
        city.Buildings[industryId].MethodId = methodId;
        RefreshProductionEmployment(city);
        ReconcilePops(Player);
        return Ok("生产方式已调整；投入、产出、岗位与技工需求从下一次结算生效。");
    }
}
