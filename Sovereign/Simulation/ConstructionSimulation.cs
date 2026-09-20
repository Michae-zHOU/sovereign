using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Sovereign.Simulation;

/// <summary>Authored prototype balance; the shared queue follows the documented Victoria 3 construction model.</summary>
public static class ConstructionCatalog
{
    public const string SectorId = "construction_sector";
    public const string RailwayId = "railway";
    public const decimal BaseWeeklyCapacity = 10m;
    public const decimal MaxProjectWeeklyPoints = 10m;
    public const int MaximumQueueLength = 500;
    public const int WorkersPerSector = 1000;
    public static IReadOnlyDictionary<string, decimal> Costs { get; } = new Dictionary<string, decimal>
    {
        ["farm"] = 60m, ["lumber"] = 80m, ["coal_mine"] = 120m, ["iron_mine"] = 120m,
        ["toolworks"] = 160m, ["textile"] = 140m, [SectorId] = 80m, [RailwayId] = 160m
    };
    public static IReadOnlyList<ConstructionMethodDefinition> Methods { get; } = new[]
    {
        new ConstructionMethodDefinition("wood", "木架建筑", 2m, 14m,
            new Dictionary<string, decimal> { ["timber"] = 14m, ["tools"] = 1m }),
        new ConstructionMethodDefinition("iron", "铁架建筑", 5m, 21m,
            new Dictionary<string, decimal> { ["timber"] = 10m, ["iron"] = 8m, ["tools"] = 3m })
    };
    public static ConstructionMethodDefinition Method(string id) => Methods.Single(m => m.Id == id);
    public static string Name(string id) => id == SectorId ? "Construction sectors" : id == RailwayId ? "铁路" : Catalog.Industries.Single(i => i.Id == id).Name;
}

internal sealed record ConstructionDaySnapshot(ConstructionStatus Status, IReadOnlyDictionary<string, decimal> MethodLevels, decimal PrivateDispatchLimit);

public sealed record ConstructionMethodDefinition(string Id, string Name, decimal WeeklyPoints, decimal WeeklyWages,
    IReadOnlyDictionary<string, decimal> WeeklyInputs);
public sealed record ConstructionProjectForecast(string Id, decimal WeeklyAllocated, decimal WeeklyProgress,
    int? EstimatedDays, bool Waiting, bool Paused);
public sealed record ConstructionInputForecast(string GoodId, decimal WeeklyDemand, decimal WeeklyUsed, decimal Stock, decimal UnitPrice);
public sealed record ConstructionStatus(decimal BaseWeeklyCapacity, decimal WeeklyCapacity, decimal WeeklyAllocated,
    decimal WeeklyProgress, decimal WeeklyCost, decimal WeeklyMaterialCost, decimal WeeklyWages, decimal ShortageFactor,
    IReadOnlyList<ConstructionProjectForecast> Projects, IReadOnlyList<ConstructionInputForecast> Inputs)
{
    public decimal WeeklyGovernmentCost { get; init; }
    public decimal WeeklyPrivateCost { get; init; }
    public decimal WeeklyPrivateMaterialCost { get; init; }
    public decimal WeeklyPrivateWages { get; init; }
    public decimal GovernmentWeeklyProgress { get; init; }
    public decimal PrivateWeeklyProgress { get; init; }
}

public sealed partial class CountryState
{
    public decimal InvestmentPool { get; set; }
    public decimal DailyInvestmentContribution { get; set; }
    public decimal DailyPrivateConstructionSpending { get; set; }
    public bool PrivateConstructionEnabled { get; set; } = true;
    [JsonIgnore] internal ConstructionDaySnapshot? ConstructionDayPlan { get; set; }
}

public sealed partial class ConstructionProject
{
    public bool PrivateInvestment { get; set; }
}

public sealed partial class SimulationEngine
{
    private sealed record ConstructionAllocation(ConstructionProject Project, decimal FreePoints, decimal SectorPoints);

    private static void InitializeConstructionSectors(CountryState country)
    {
        // Starting construction industry is an authored scenario estimate, not a historical census.
        var capital = country.Cities.Single(c => c.Id == GeographyCatalog.Country(country.Id).CapitalCityId);
        capital.ConstructionSectors = country.Id switch { "GBR" => 4, "QNG" => 3, "PRU" or "FRA" or "USA" => 2, _ => 1 };
        RefreshCityWorkforce(capital);
    }

    public CommandResult Build(string industryId) => BuildFor(Player, industryId);
    public CommandResult BuildInCity(string cityId, string industryId)
    {
        if (!Player.Cities.Any(city => city.Id == cityId)) return Fail("Choose a city in your own country.");
        return BuildFor(Player, industryId, cityId);
    }
    public CommandResult BuildConstructionSector(string cityId) => BuildInCity(cityId, ConstructionCatalog.SectorId);

    public static int GetCompletedConstructionLevels(CountryState country, CityState city, string industryId) => industryId switch
    {
        ConstructionCatalog.SectorId => city.ConstructionSectors,
        ConstructionCatalog.RailwayId => country.Regions.Single(r => r.Id == city.RegionId).RailwayLevels,
        _ => city.Industries[industryId]
    };

    public static int GetQueuedConstructionLevels(CountryState country, CityState city, string industryId) =>
        country.Construction.Count(p => p.IndustryId == industryId && (industryId == ConstructionCatalog.RailwayId
            ? country.Cities.Any(c => c.Id == p.CityId && c.RegionId == city.RegionId) : p.CityId == city.Id));

    private CommandResult BuildFor(CountryState country, string industryId, string? cityId = null)
    {
        if (State.Finished) return Fail("The campaign is complete.");
        if (!ConstructionCatalog.Costs.TryGetValue(industryId, out decimal required)) return Fail("Unknown industry.");
        if (country.NextConstructionId >= int.MaxValue - 1) return Fail("建造项目编号已用尽。");
        var city = country.Cities.Single(c => c.Id == (cityId ?? GeographyCatalog.Country(country.Id).CapitalCityId));
        if (country.Construction.Count >= ConstructionCatalog.MaximumQueueLength) return Fail("全国待建队列已达 500 项技术上限。");
        if (industryId == ConstructionCatalog.RailwayId && !country.Technologies.Contains("railways")) return Fail("需要先研究早期铁路技术。");
        int levels = GetCompletedConstructionLevels(country, city, industryId);
        int limit = industryId == ConstructionCatalog.RailwayId ? 20 : 100;
        if (levels + GetQueuedConstructionLevels(country, city, industryId) >= limit)
        {
            if (industryId == ConstructionCatalog.RailwayId) return Fail("每省铁路上限为20级，包含已在建项目。");
            return Fail("A city supports up to 100 levels per industry.");
        }
        var project = new ConstructionProject
        {
            Id = country.Id + "_construction_" + country.NextConstructionId++, CityId = city.Id,
            IndustryId = industryId, Name = ConstructionCatalog.Name(industryId), RequiredPoints = required
        };
        country.Construction.Add(project);
        SynchronizeGeography(country);
        if (country.Id == State.PlayerCountryId) AddLog($"{city.Name}：{project.Name} 加入全国建造队列；按实际施工持续结算。");
        return Ok("已加入全国建造队列。建造力按队列顺序分配，开工后持续支付材料与工资。");
    }

    public CommandResult SetConstructionPaused(bool paused)
    {
        if (State.Finished) return Fail("The campaign is complete.");
        Player.ConstructionPaused = paused;
        return Ok(paused ? "政府建造已暂停；私人投资继续，闲置营建部门仍支付工资。" : "政府建造已恢复。");
    }

    public CommandResult SetProjectPaused(string projectId, bool paused)
    {
        if (State.Finished) return Fail("The campaign is complete.");
        var project = Player.Construction.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return Fail("建造项目不存在。");
        if (project.PrivateInvestment) return Fail("私人投资项目由投资者管理，无法手动暂停。");
        project.Paused = paused;
        return Ok(paused ? "该项目已暂停。" : "该项目已恢复。");
    }

    public CommandResult MoveConstructionProject(string projectId, int newIndex)
    {
        if (State.Finished) return Fail("The campaign is complete.");
        int current = Player.Construction.FindIndex(p => p.Id == projectId);
        if (current < 0 || newIndex < 0 || newIndex >= Player.Construction.Count) return Fail("无效的建造队列位置。");
        var project = Player.Construction[current];
        if (project.PrivateInvestment) return Fail("私人投资队列由投资者管理，无法手动排序。");
        Player.Construction.RemoveAt(current); Player.Construction.Insert(newIndex, project);
        SynchronizeGeography(Player);
        return Ok("建造优先顺序已更新。");
    }

    public CommandResult CancelConstruction(string projectId)
    {
        if (State.Finished) return Fail("The campaign is complete.");
        var project = Player.Construction.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return Fail("建造项目不存在。");
        if (project.PrivateInvestment) return Fail("私人投资项目由投资者管理，无法手动取消。");
        Player.Construction.Remove(project); SynchronizeGeography(Player);
        return Ok("建造已取消；已经消耗的材料与工程进度不返还。");
    }

    public CommandResult SetConstructionMethod(string cityId, string methodId)
    {
        if (State.Finished) return Fail("The campaign is complete.");
        var city = Player.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city == null || !ConstructionCatalog.Methods.Any(m => m.Id == methodId)) return Fail("无效的营建部门或生产方式。");
        if (methodId == "iron" && !Player.Technologies.Contains("mechanical_tools")) return Fail("铁架建筑需要先研究机械工具。");
        city.ConstructionMethodId = methodId;
        return Ok("营建部门生产方式已更新；材料需求和可用建造力随之改变。");
    }

    public ConstructionStatus GetConstructionStatus(string? countryId = null)
    {
        var country = countryId == null ? Player : State.Countries.Single(c => c.Id == countryId);
        return PlanConstruction(country).Status;
    }

    // Before market pricing, submit these daily buy orders once. The daily tick never removes stock.
    public static IReadOnlyDictionary<string, decimal> GetConstructionDemand(CountryState country) =>
        (country.ConstructionDayPlan?.Status ?? PlanConstruction(country, true).Status).Inputs.ToDictionary(i => i.GoodId, i => i.WeeklyDemand / 7m);

    public static void BeginConstructionDay(CountryState country)
    {
        country.ConstructionDayPlan = null;
        // Reserve at the legal price ceiling so today's repricing cannot invalidate submitted orders.
        var plan = PlanConstruction(country, true, reservePriceCeiling: true);
        var methods = ConstructionCatalog.Methods.ToDictionary(m => m.Id, m => country.Cities
            .Where(c => c.ConstructionMethodId == m.Id).Sum(c => c.ConstructionWorkers / (decimal)ConstructionCatalog.WorkersPerSector));
        decimal privateLimit = plan.Allocations.Where(a => a.Project.PrivateInvestment).Sum(a => a.FreePoints + a.SectorPoints);
        country.ConstructionDayPlan = new(plan.Status, methods, privateLimit);
    }

    public CommandResult SetPrivateConstructionEnabled(bool enabled)
    {
        if (State.Finished) return Fail("战役已经结束。");
        Player.PrivateConstructionEnabled = enabled;
        return Ok(enabled ? "私人投资已恢复。" : "私人投资已停工；投资池资金保留。");
    }

    // Called once every thirty days by the economy. No cash or materials are spent on queueing.
    public static bool MaybeQueuePrivateConstruction(CountryState country)
    {
        if (!country.PrivateConstructionEnabled || country.InvestmentPool <= 0m ||
            country.Construction.Count >= ConstructionCatalog.MaximumQueueLength || country.NextConstructionId >= int.MaxValue - 1) return false;
        var candidates = from city in country.Cities
                         from industry in Catalog.Industries
                         let account = city.Buildings[industry.Id]
                         let method = ProductionCatalog.Get(industry.Id, account.MethodId)
                         let levels = city.Industries[industry.Id]
                         where levels > 0 && account.DailyProfit > 0m && city.Unemployed >= method.JobsPerLevel &&
                            levels + country.Construction.Count(p => p.CityId == city.Id && p.IndustryId == industry.Id) < 100
                         orderby account.DailyProfit / levels / ConstructionCatalog.Costs[industry.Id] descending, city.Id, industry.Id
                         select (City: city, Industry: industry);
        var choice = candidates.FirstOrDefault();
        if (choice.City == null) return false;
        country.Construction.Add(new ConstructionProject
        {
            Id = country.Id + "_construction_" + country.NextConstructionId++, CityId = choice.City.Id,
            IndustryId = choice.Industry.Id, Name = choice.Industry.Name,
            RequiredPoints = ConstructionCatalog.Costs[choice.Industry.Id], PrivateInvestment = true
        });
        SynchronizeGeography(country);
        return true;
    }

    private static (ConstructionStatus Status, List<ConstructionAllocation> Allocations) PlanConstruction(CountryState country, bool assumeSupply = false, bool reservePriceCeiling = false)
    {
        decimal privateDemand = country.PrivateConstructionEnabled && country.InvestmentPool > 0m
            ? country.Construction.Where(p => p.PrivateInvestment && !p.Paused).Sum(ProjectDemand) : 0m;
        if (country.ConstructionDayPlan is { } snapshot) privateDemand = Math.Min(privateDemand, snapshot.PrivateDispatchLimit);
        var plan = PlanConstructionCore(country, privateDemand, assumeSupply, reservePriceCeiling);
        decimal budget = country.InvestmentPool * 7m;
        if (plan.Status.WeeklyPrivateCost <= budget) return plan;
        // Limit only private dispatch, returning unused capacity to the government. Choosing the
        // feasible lower bound also makes decimal rounding incapable of overdrawing the pool.
        decimal low = 0m, high = privateDemand;
        var feasible = PlanConstructionCore(country, 0m, assumeSupply, reservePriceCeiling);
        for (int step = 0; step < 24; step++)
        {
            decimal mid = (low + high) / 2m;
            var candidate = PlanConstructionCore(country, mid, assumeSupply, reservePriceCeiling);
            if (candidate.Status.WeeklyPrivateCost <= budget) { low = mid; feasible = candidate; }
            else high = mid;
        }
        return feasible;
    }

    private static decimal ProjectDemand(ConstructionProject project) =>
        Math.Min(ConstructionCatalog.MaxProjectWeeklyPoints, Math.Max(0m, project.RequiredPoints - project.ProgressPoints) * 7m);

    private static (ConstructionStatus Status, List<ConstructionAllocation> Allocations) PlanConstructionCore(CountryState country, decimal privateLimit, bool assumeSupply, bool reservePriceCeiling)
    {
        decimal sectorCapacity = 0m, wages = 0m;
        var methodLevels = ConstructionCatalog.Methods.ToDictionary(m => m.Id, _ => 0m);
        foreach (var city in country.Cities)
        {
            var method = ConstructionCatalog.Method(city.ConstructionMethodId);
            decimal staffed = city.ConstructionWorkers / (decimal)ConstructionCatalog.WorkersPerSector;
            sectorCapacity += staffed * method.WeeklyPoints; wages += staffed * method.WeeklyWages;
            methodLevels[method.Id] += staffed;
        }
        if (country.ConstructionDayPlan is { } snapshot)
        {
            methodLevels = snapshot.MethodLevels.ToDictionary(p => p.Key, p => p.Value);
            sectorCapacity = ConstructionCatalog.Methods.Sum(m => m.WeeklyPoints * methodLevels[m.Id]);
            wages = ConstructionCatalog.Methods.Sum(m => m.WeeklyWages * methodLevels[m.Id]);
        }
        decimal capacity = ConstructionCatalog.BaseWeeklyCapacity + sectorCapacity;
        decimal governmentDemand = country.ConstructionPaused ? 0m : country.Construction.Where(p => !p.PrivateInvestment && !p.Paused).Sum(ProjectDemand);
        decimal privateDemand = Math.Min(privateLimit, country.Construction.Where(p => p.PrivateInvestment && !p.Paused).Sum(ProjectDemand));
        // Original economic law balance: half reserved to either queue; unused reservations can be borrowed.
        decimal governmentQuota = Math.Min(governmentDemand, capacity * .5m), privateQuota = Math.Min(privateDemand, capacity * .5m);
        decimal remainder = capacity - governmentQuota - privateQuota;
        decimal borrowed = Math.Min(remainder, governmentDemand - governmentQuota); governmentQuota += borrowed; remainder -= borrowed;
        privateQuota += Math.Min(remainder, privateDemand - privateQuota);
        decimal totalQuota = governmentQuota + privateQuota;
        decimal freePrivate = totalQuota > 0m ? Math.Min(ConstructionCatalog.BaseWeeklyCapacity, totalQuota) * privateQuota / totalQuota : 0m;
        decimal freeGovernment = Math.Min(ConstructionCatalog.BaseWeeklyCapacity, totalQuota) - freePrivate;
        var allocations = new List<ConstructionAllocation>(country.Construction.Count);
        foreach (var project in country.Construction)
        {
            bool paused = project.Paused || (project.PrivateInvestment ? !country.PrivateConstructionEnabled : country.ConstructionPaused);
            decimal remaining = project.PrivateInvestment ? privateQuota : governmentQuota;
            decimal freeRemaining = project.PrivateInvestment ? freePrivate : freeGovernment;
            decimal requested = paused ? 0m : Math.Min(remaining, ProjectDemand(project));
            decimal free = Math.Min(freeRemaining, requested);
            allocations.Add(new(project, free, requested - free));
            if (project.PrivateInvestment) { privateQuota -= requested; freePrivate -= free; }
            else { governmentQuota -= requested; freeGovernment -= free; }
        }
        decimal privateBillable = allocations.Where(a => a.Project.PrivateInvestment && !a.Project.LegacyPrepaid).Sum(a => a.SectorPoints);
        decimal billable = allocations.Where(a => !a.Project.LegacyPrepaid).Sum(a => a.SectorPoints);
        var demandInputs = Catalog.Goods.ToDictionary(g => g, _ => 0m);
        decimal actualPaid = 0m, remainingBillable = billable;
        foreach (var method in ConstructionCatalog.Methods)
        {
            decimal active = Math.Min(methodLevels[method.Id], remainingBillable / method.WeeklyPoints);
            decimal availability = 1m;
            foreach (var input in method.WeeklyInputs)
            {
                demandInputs[input.Key] += input.Value * active;
                if (!assumeSupply && active > 0m) availability = Math.Min(availability, GetInputAvailability(country, input.Key));
            }
            actualPaid += active * method.WeeklyPoints * availability;
            remainingBillable = Math.Max(0m, remainingBillable - active * method.WeeklyPoints);
        }
        var inputs = Catalog.Goods.Where(g => demandInputs[g] > 0m || ConstructionCatalog.Methods.Any(m => methodLevels[m.Id] > 0m && m.WeeklyInputs.ContainsKey(g)))
            .Select(g => new ConstructionInputForecast(g, demandInputs[g], demandInputs[g], country.SellOrders.GetValueOrDefault(g), reservePriceCeiling ? Catalog.BasePrices[g] * 1.75m : country.Prices[g])).ToArray();
        decimal materials = inputs.Sum(i => i.WeeklyDemand * i.UnitPrice);
        decimal privateFraction = billable > 0m ? privateBillable / billable : 0m;
        decimal privateMaterials = materials * privateFraction;
        decimal privateWages = sectorCapacity > 0m ? wages * privateBillable / sectorCapacity : 0m;
        decimal privatePaidRemaining = actualPaid * privateFraction, governmentPaidRemaining = actualPaid - privatePaidRemaining;
        var forecasts = new List<ConstructionProjectForecast>(allocations.Count);
        decimal governmentProgress = 0m, privateProgress = 0m;
        foreach (var allocation in allocations)
        {
            var project = allocation.Project;
            decimal paidRemaining = project.PrivateInvestment ? privatePaidRemaining : governmentPaidRemaining;
            decimal sector = project.LegacyPrepaid ? allocation.SectorPoints : Math.Min(paidRemaining, allocation.SectorPoints);
            if (!project.LegacyPrepaid)
            {
                if (project.PrivateInvestment) privatePaidRemaining -= sector;
                else governmentPaidRemaining -= sector;
            }
            decimal progress = allocation.FreePoints + sector;
            bool paused = project.Paused || (project.PrivateInvestment ? !country.PrivateConstructionEnabled : country.ConstructionPaused);
            int? days = progress > 0m ? (int)Math.Min(int.MaxValue, Math.Ceiling((project.RequiredPoints - project.ProgressPoints) / progress * 7m)) : null;
            forecasts.Add(new(project.Id, allocation.FreePoints + allocation.SectorPoints, progress, days, progress == 0m && !paused, paused));
            if (project.PrivateInvestment) privateProgress += progress; else governmentProgress += progress;
        }
        decimal privateCost = privateMaterials + privateWages;
        return (new ConstructionStatus(ConstructionCatalog.BaseWeeklyCapacity, capacity, forecasts.Sum(p => p.WeeklyAllocated),
            governmentProgress + privateProgress, materials + wages, materials, wages, billable > 0m ? actualPaid / billable : 1m, forecasts, inputs)
        {
            WeeklyPrivateCost = privateCost, WeeklyGovernmentCost = materials + wages - privateCost,
            WeeklyPrivateMaterialCost = privateMaterials, WeeklyPrivateWages = privateWages,
            GovernmentWeeklyProgress = governmentProgress, PrivateWeeklyProgress = privateProgress
        }, allocations);
    }

    private void TickConstruction(CountryState country)
    {
        var plan = PlanConstruction(country);
        country.ConstructionWages = (plan.Status.WeeklyWages - plan.Status.WeeklyPrivateWages) / 7m;
        country.ConstructionMaterialSpending = (plan.Status.WeeklyMaterialCost - plan.Status.WeeklyPrivateMaterialCost) / 7m;
        country.ConstructionSpending = country.ConstructionWages + country.ConstructionMaterialSpending;
        country.DailyPrivateConstructionSpending = plan.Status.WeeklyPrivateCost / 7m;
        country.InvestmentPool -= country.DailyPrivateConstructionSpending;
        country.ConstructionPoints = plan.Status.WeeklyProgress / 7m;
        country.Spending += country.ConstructionSpending;
        country.DailyBalance -= country.ConstructionSpending;
        country.Treasury -= country.ConstructionSpending;
        if (country.Treasury < 0m) { country.Debt -= country.Treasury; country.Treasury = 0m; }
        for (int index = 0; index < plan.Allocations.Count; index++)
        {
            var project = plan.Allocations[index].Project;
            project.ProgressPoints = Math.Min(project.RequiredPoints, project.ProgressPoints + plan.Status.Projects[index].WeeklyProgress / 7m);
            if (project.ProgressPoints + .00000001m < project.RequiredPoints) continue;
            var city = country.Cities.Single(c => c.Id == project.CityId);
            if (project.IndustryId == ConstructionCatalog.SectorId) city.ConstructionSectors++;
            else if (project.IndustryId == ConstructionCatalog.RailwayId) country.Regions.Single(r => r.Id == city.RegionId).RailwayLevels++;
            else
            {
                city.Industries[project.IndustryId]++;
                if (project.PrivateInvestment) city.Buildings[project.IndustryId].PrivateLevels++;
            }
            country.Construction.Remove(project);
            if (country.Id == State.PlayerCountryId) AddLog($"{project.Name} completed in {city.Name}. Local jobs and productive capacity expanded.");
        }
        SynchronizeGeography(country);
        foreach (var city in country.Cities) RefreshProductionEmployment(city);
        country.ConstructionDayPlan = null;
    }
}
