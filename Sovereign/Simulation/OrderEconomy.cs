using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign.Simulation;

public sealed partial class SimulationEngine
{
    private static Dictionary<string, decimal> EmptyOrders() => Catalog.Goods.ToDictionary(g => g, _ => 0m);

    public static decimal OrderPrice(string good, decimal buy, decimal sell)
    {
        if (buy < 0m || sell < 0m || !Catalog.BasePrices.ContainsKey(good)) throw new ArgumentException("Invalid market orders.");
        decimal imbalance = buy == sell ? 0m : Math.Min(buy, sell) == 0m ? (buy > sell ? 1m : -1m) :
            Clamp((buy - sell) / Math.Min(buy, sell), -1m, 1m);
        // Price bounds follow the reference structure; slope is an authored calibration for this scenario.
        return Catalog.BasePrices[good] * (1m + .75m * imbalance);
    }

    public static decimal GetInputAvailability(CountryState country, string good) => InputAvailability(
        country.BuyOrders.GetValueOrDefault(good), country.SellOrders.GetValueOrDefault(good));

    public static decimal GetRegionalInputAvailability(CountryState country, RegionState region, string good) =>
        InputAvailability(region.BuyOrders.GetValueOrDefault(good), region.SellOrders.GetValueOrDefault(good)) * (1m - region.MarketAccess) +
        GetInputAvailability(country, good) * region.MarketAccess;

    private static Dictionary<string, Dictionary<string, decimal>> ProductionInputFactors(CountryState country) =>
        country.Cities.ToDictionary(city => city.Id, city => Catalog.Industries.ToDictionary(industry => industry.Id,
            industry => ProductionCatalog.Get(industry.Id, city.Buildings[industry.Id].MethodId).Inputs.Count == 0 ? 1m :
                ProductionCatalog.Get(industry.Id, city.Buildings[industry.Id].MethodId).Inputs.Keys.Min(g =>
                    GetRegionalInputAvailability(country, country.Regions.Single(r => r.Id == city.RegionId), g))));

    private static decimal InputAvailability(decimal buy, decimal sell) => buy <= 0m ? 1m :
        // A shortage reduces throughput; orders are not physically rationed from a stockpile.
        Clamp(1m - Math.Max(0m, 1m - sell / buy) * .75m, .25m, 1m);

    private static void InitializeOrderEconomy(CountryState country, bool preserveLegacySnapshot = false)
    {
        InitializeProduction(country);
        country.BuyOrders = EmptyOrders(); country.SellOrders = EmptyOrders();
        country.ImportOrders = EmptyOrders(); country.ExportOrders = EmptyOrders();
        foreach (var region in country.Regions)
        {
            region.BuyOrders = EmptyOrders(); region.SellOrders = EmptyOrders();
            region.SubsistenceProduction = EmptyOrders();
            region.LocalPrices = country.Prices.ToDictionary(p => p.Key, p => p.Value);
        }
        ReconcilePops(country);
        InitializePolitics(country);
        UpdateInfrastructure(country);
        foreach (var pop in country.Pops)
        {
            pop.DailyIncome = PopulationCatalog.Needs(pop).Sum(p => p.Value * Catalog.BasePrices[p.Key]) * 1.05m;
            pop.BuyOrders = EmptyOrders();
        }
        if (preserveLegacySnapshot)
        {
            // Preserve the old recorded day. The following daily tick switches to order settlement.
            foreach (var region in country.Regions)
            foreach (var good in Catalog.Goods)
            {
                region.BuyOrders[good] = country.Consumption[good] * region.Population / country.Population;
                region.SellOrders[good] = country.Cities.Where(c => c.RegionId == region.Id).Sum(c => c.Production[good]);
            }
            foreach (var good in Catalog.Goods)
            { country.BuyOrders[good] = country.Regions.Sum(r => r.BuyOrders[good]); country.SellOrders[good] = country.Regions.Sum(r => r.SellOrders[good]); }
            return;
        }
        // Populate the market display immediately without advancing time or charging money.
        AssembleOrders(country);
        PriceMarkets(country);
        foreach (var city in country.Cities)
        {
            var region = country.Regions.Single(r => r.Id == city.RegionId);
            city.Production = EmptyOrders(); city.Gdp = city.Artisans * .003m * 365m;
            foreach (var industry in Catalog.Industries)
            {
                var method = ProductionCatalog.Get(industry.Id, city.Buildings[industry.Id].MethodId);
                decimal scale = BuildingScale(country, city, industry.Id);
                foreach (var output in method.Outputs) city.Production[output.Key] += output.Value * scale;
                city.Gdp += Math.Max(0m, method.Outputs.Sum(p => p.Value * scale * region.LocalPrices[p.Key]) - method.Inputs.Sum(p => p.Value * scale * region.LocalPrices[p.Key])) * 365m;
            }
        }
        foreach (var region in country.Regions) region.SubsistenceGdp = (region.SubsistenceProduction.Sum(p => p.Value * region.LocalPrices[p.Key]) + region.SubsistenceNonMarketValue) * 365m;
        foreach (var good in Catalog.Goods)
        { country.Production[good] = country.Cities.Sum(c => c.Production[good]) + country.Regions.Sum(r => r.SubsistenceProduction[good]); country.Consumption[good] = country.BuyOrders[good]; }
        country.Gdp = country.Cities.Sum(c => c.Gdp) + country.Regions.Sum(r => r.SubsistenceGdp);
        SynchronizeGeography(country);
    }

    private static void ReconcilePops(CountryState country)
    {
        var existing = country.Pops.ToDictionary(p => (p.RegionId, p.ProfessionId));
        var result = new List<PopGroup>();
        foreach (var region in country.Regions)
        {
            var cities = country.Cities.Where(c => c.RegionId == region.Id).ToArray();
            long urban = cities.Sum(c => c.Population);
            long owners = urban / 500;
            long shopkeepers = Math.Min(urban - owners, (long)(cities.Sum(c => c.Artisans) / .45m));
            long industrialHouseholds = Math.Min(urban - owners - shopkeepers,
                (long)(cities.Sum(c => c.IndustrialWorkers + c.ConstructionWorkers) / .45m));
            long skilledWorkers = cities.Sum(c => c.Buildings.Sum(b => (long)(b.Value.Workers *
                ProductionCatalog.Get(b.Key, b.Value.MethodId).SkilledFraction)));
            long skilled = Math.Min(industrialHouseholds, (long)(skilledWorkers / .45m));
            var counts = new Dictionary<string, long>
            {
                ["aristocrats"] = region.RuralPopulation / 200,
                ["peasants"] = region.RuralPopulation - region.RuralPopulation / 200,
                ["capitalists"] = owners, ["shopkeepers"] = shopkeepers,
                ["machinists"] = skilled, ["laborers"] = industrialHouseholds - skilled,
                ["unemployed"] = urban - owners - shopkeepers - industrialHouseholds
            };
            foreach (var profession in PopulationCatalog.Professions.Keys)
            {
                bool has = existing.TryGetValue((region.Id, profession), out var pop);
                pop ??= new PopGroup { RegionId = region.Id, ProfessionId = profession,
                    Wealth = PopulationCatalog.StartingWealth(profession), Literacy = country.Literacy,
                    BuyOrders = EmptyOrders() };
                pop.Population = counts[profession]; pop.Workforce = (long)(pop.Population * .45m);
                if (!has) pop.DailyIncome = PopulationCatalog.Needs(pop).Sum(p => p.Value * Catalog.BasePrices[p.Key]) * 1.05m;
                result.Add(pop);
            }
        }
        country.Pops = result;
    }

    private static void UpdateInfrastructure(CountryState country)
    {
        foreach (var region in country.Regions)
        {
            var cities = country.Cities.Where(c => c.RegionId == region.Id).ToArray();
            decimal natural = region.Id == GeographyCatalog.City(GeographyCatalog.Country(country.Id).CapitalCityId).RegionId ? 20m : 10m;
            region.Infrastructure = natural + cities.Length * 3m + region.RailwayLevels * 20m;
            region.InfrastructureUsage = Math.Max(1m, cities.Sum(c => c.Industries.Values.Sum() + c.ConstructionSectors * 2));
            region.MarketAccess = Clamp(region.Infrastructure / region.InfrastructureUsage, 0m, 1m);
        }
        country.MarketPriceImpact = country.Technologies.Contains("railways") ? .85m : .75m;
    }

    private static decimal BuildingScale(CountryState country, CityState city, string industryId)
    {
        var account = city.Buildings[industryId];
        var method = ProductionCatalog.Get(industryId, account.MethodId);
        decimal levels = account.Workers / (decimal)method.JobsPerLevel;
        decimal scaleBonus = 1m + Math.Min(.3m, Math.Max(0, city.Industries[industryId] - 1) * .01m);
        return levels * scaleBonus * (1m - country.Unrest / 400m) * (1m - GetLaborEffect(country) * .008m);
    }

    private static void AssembleOrders(CountryState country, Dictionary<string, Dictionary<string, decimal>>? outputFactors = null)
    {
        outputFactors ??= ProductionInputFactors(country);
        foreach (var region in country.Regions)
        { region.BuyOrders = EmptyOrders(); region.SellOrders = EmptyOrders(); region.SubsistenceProduction = EmptyOrders(); }
        foreach (var city in country.Cities)
        {
            var region = country.Regions.Single(r => r.Id == city.RegionId);
            foreach (var industry in Catalog.Industries)
            {
                var method = ProductionCatalog.Get(industry.Id, city.Buildings[industry.Id].MethodId);
                decimal scale = BuildingScale(country, city, industry.Id);
                decimal shortage = outputFactors[city.Id][industry.Id];
                foreach (var input in method.Inputs) region.BuyOrders[input.Key] += input.Value * scale;
                foreach (var output in method.Outputs) region.SellOrders[output.Key] += output.Value * scale * shortage;
            }
        }
        foreach (var region in country.Regions)
        {
            decimal peasants = country.Pops.Single(p => p.RegionId == region.Id && p.ProfessionId == "peasants").Population / 10_000m;
            // Nonmarket household output is valued once in GDP and household means, never sold again as goods.
            region.SubsistenceNonMarketValue = peasants * PopulationCatalog.SubsistenceValuePerTenThousand;
            region.SubsistenceProduction["grain"] = peasants * 7.5m;
            region.SubsistenceProduction["timber"] = peasants * .4m;
            region.SubsistenceProduction["clothes"] = peasants * .2m;
            foreach (var good in Catalog.Goods) region.SellOrders[good] += region.SubsistenceProduction[good];
        }
        foreach (var pop in country.Pops)
        {
            var region = country.Regions.Single(r => r.Id == pop.RegionId);
            var needs = PopulationCatalog.Needs(pop);
            decimal targetExpense = needs.Sum(n => n.Value * region.LocalPrices[n.Key]);
            decimal affordable = targetExpense > 0m ? Clamp(pop.DailyIncome / targetExpense, .05m, 1m) : 0m;
            pop.BuyOrders = EmptyOrders();
            foreach (var need in needs)
            { pop.BuyOrders[need.Key] = need.Value * affordable; region.BuyOrders[need.Key] += pop.BuyOrders[need.Key]; }
        }
        var capitalRegion = country.Regions.Single(r => r.Id == country.Cities.Single(c => c.Id == GeographyCatalog.Country(country.Id).CapitalCityId).RegionId);
        foreach (var demand in GetConstructionDemand(country)) capitalRegion.BuyOrders[demand.Key] += demand.Value;
        // Railway operating goods remain local and require actual running costs in the national accounts.
        foreach (var region in country.Regions)
        { region.BuyOrders["coal"] += region.RailwayLevels * 2m; region.BuyOrders["tools"] += region.RailwayLevels * .2m; }
        country.ImportOrders = EmptyOrders(); country.ExportOrders = EmptyOrders();
        foreach (var good in Catalog.Goods)
        {
            decimal capacity = 4m * (country.Technologies.Contains("railways") ? 1.5m : 1m) * (1m + country.TradePacts.Count * .2m);
            if (country.Trade[good] == 1) { country.ImportOrders[good] = capacity; capitalRegion.SellOrders[good] += capacity; }
            if (country.Trade[good] == -1) { country.ExportOrders[good] = capacity; capitalRegion.BuyOrders[good] += capacity; }
            country.BuyOrders[good] = country.Regions.Sum(r => r.BuyOrders[good]);
            country.SellOrders[good] = country.Regions.Sum(r => r.SellOrders[good]);
        }
    }

    private static void PriceMarkets(CountryState country)
    {
        foreach (var good in Catalog.Goods) country.Prices[good] = OrderPrice(good, country.BuyOrders[good], country.SellOrders[good]);
        foreach (var region in country.Regions)
        foreach (var good in Catalog.Goods)
        {
            decimal weight = country.MarketPriceImpact * region.MarketAccess;
            region.LocalPrices[good] = OrderPrice(good, region.BuyOrders[good], region.SellOrders[good]) * (1m - weight) + country.Prices[good] * weight;
        }
    }

    private void TickOrderEconomy(CountryState country)
    {
        SynchronizeGeography(country);
        foreach (var city in country.Cities) RefreshProductionEmployment(city);
        ReconcilePops(country); UpdateInfrastructure(country);
        BeginConstructionDay(country);
        // Take one daily snapshot. Settlement below uses these exact quantities, never a second shortage calculation.
        var outputFactors = ProductionInputFactors(country);
        AssembleOrders(country, outputFactors); PriceMarkets(country);
        country.TariffRevenue = Catalog.Goods.Sum(g => (country.ImportOrders[g] + country.ExportOrders[g]) * country.Prices[g] * .05m);
        country.DailyTradeBalance = Catalog.Goods.Sum(g => (country.ExportOrders[g] - country.ImportOrders[g]) * country.Prices[g]);
        country.DailyInvestmentContribution = 0m; country.GovernmentDividends = 0m;
        var incomes = country.Pops.ToDictionary(p => (p.RegionId, p.ProfessionId), _ => 0m);
        foreach (var region in country.Regions)
        {
            decimal ruralIncome = region.SubsistenceProduction.Sum(p => p.Value * region.LocalPrices[p.Key]);
            region.SubsistenceGdp = (ruralIncome + region.SubsistenceNonMarketValue) * 365m;
            incomes[(region.Id, "peasants")] += ruralIncome * .85m + region.SubsistenceNonMarketValue;
            incomes[(region.Id, "aristocrats")] += ruralIncome * .15m;
        }
        foreach (var city in country.Cities)
        {
            var region = country.Regions.Single(r => r.Id == city.RegionId);
            city.Production = EmptyOrders(); city.Gdp = 0m;
            foreach (var industry in Catalog.Industries)
            {
                var account = city.Buildings[industry.Id]; var method = ProductionCatalog.Get(industry.Id, account.MethodId);
                decimal scale = BuildingScale(country, city, industry.Id), shortage = outputFactors[city.Id][industry.Id];
                account.DailyRevenue = method.Outputs.Sum(p => p.Value * scale * shortage * region.LocalPrices[p.Key]);
                account.DailyInputCost = method.Inputs.Sum(p => p.Value * scale * region.LocalPrices[p.Key]);
                decimal desiredWages = account.Workers * .004m * (1m + method.SkilledFraction * .5m);
                decimal available = Math.Max(0m, account.DailyRevenue - account.DailyInputCost) + account.CashReserves;
                account.DailyWages = Math.Min(desiredWages, available);
                account.DailyProfit = account.DailyRevenue - account.DailyInputCost - account.DailyWages;
                decimal positive = Math.Max(0m, account.DailyProfit);
                if (account.DailyProfit < 0m)
                {
                    decimal loss = -account.DailyProfit;
                    account.Debt += Math.Max(0m, loss - account.CashReserves);
                    account.CashReserves = Math.Max(0m, account.CashReserves - loss);
                }
                decimal repayment = Math.Min(account.Debt, positive);
                account.Debt -= repayment; positive -= repayment;
                decimal retained = Math.Min(positive * .2m, Math.Max(0m, city.Industries[industry.Id] * 100m - account.CashReserves));
                account.CashReserves += retained;
                account.DailyDividends = positive - retained;
                account.EmploymentScale = Clamp(account.EmploymentScale + (account.DailyProfit < 0m && account.CashReserves == 0m ? -.03m : account.DailyProfit > 0m ? .02m : 0m), .05m, 1m);
                decimal privateShare = city.Industries[industry.Id] > 0 ? account.PrivateLevels / (decimal)city.Industries[industry.Id] : 0m;
                decimal ownerIncome = account.DailyDividends * privateShare;
                decimal investment = ownerIncome * GetInvestmentContributionRate(country);
                country.DailyInvestmentContribution += investment;
                country.GovernmentDividends += account.DailyDividends - ownerIncome;
                incomes[(city.RegionId, industry.Id is "farm" or "lumber" ? "aristocrats" : "capitalists")] += ownerIncome - investment;
                decimal skilledWages = account.DailyWages * method.SkilledFraction * 1.5m / (1m + method.SkilledFraction * .5m);
                incomes[(city.RegionId, "machinists")] += skilledWages;
                incomes[(city.RegionId, "laborers")] += account.DailyWages - skilledWages;
                foreach (var output in method.Outputs) city.Production[output.Key] += output.Value * scale * shortage;
                city.Gdp += Math.Max(0m, account.DailyRevenue - account.DailyInputCost) * 365m;
            }
            decimal serviceIncome = city.Artisans * .003m;
            incomes[(city.RegionId, "shopkeepers")] += serviceIncome;
            // Uncatalogued local services are value added, never invented sell orders for industrial goods.
            city.Gdp += serviceIncome * 365m;
            incomes[(city.RegionId, "laborers")] += city.ConstructionWorkers / (decimal)ConstructionCatalog.WorkersPerSector * ConstructionCatalog.Method(city.ConstructionMethodId).WeeklyWages / 7m;
        }

        country.TaxRevenue = 0m; country.HouseholdIncome = 0m;
        foreach (var pop in country.Pops)
        {
            var region = country.Regions.Single(r => r.Id == pop.RegionId);
            decimal gross = incomes[(pop.RegionId, pop.ProfessionId)];
            decimal collection = pop.ProfessionId == "peasants" ? .15m : .8m;
            pop.DailyTaxes = gross * country.TaxRate / 100m * collection;
            pop.DailyIncome = Math.Max(0m, gross - pop.DailyTaxes);
            pop.DailyExpenses = pop.BuyOrders.Sum(p => p.Value * region.LocalPrices[p.Key]);
            decimal desired = PopulationCatalog.Needs(pop).Sum(p => p.Value * region.LocalPrices[p.Key]);
            decimal foodAvailability = GetRegionalInputAvailability(country, region, "grain");
            pop.NeedsFulfilled = desired <= 0m ? 1m : Clamp(Math.Min(pop.DailyIncome, pop.DailyExpenses) / desired * foodAvailability, 0m, 1m);
            decimal oldWealth = pop.Wealth;
            decimal surplus = desired > 0m ? (pop.DailyIncome - desired) / desired : 0m;
            pop.Wealth = Clamp(pop.Wealth + Clamp(surplus, -1m, 1m) * .015m, 1m, 30m);
            pop.Radicals = Clamp(pop.Radicals + Math.Max(0m, oldWealth - pop.Wealth) * .1m + (1m - pop.NeedsFulfilled) * .0004m - GetInstitutionEffect(country, "police") * .00001m, 0m, 1m);
            pop.Loyalists = Clamp(pop.Loyalists + Math.Max(0m, pop.Wealth - oldWealth) * .05m - pop.Radicals * .0002m, 0m, 1m - pop.Radicals);
            pop.Literacy = Clamp(pop.Literacy + .0015m + GetEducationEffect(country) * .012m, 0m, 99m);
            country.TaxRevenue += pop.DailyTaxes; country.HouseholdIncome += pop.DailyIncome;
        }
        country.LivingStandard = country.Pops.Sum(p => p.Wealth * p.Population) / country.Population;
        country.Literacy = country.Pops.Sum(p => p.Literacy * p.Population) / country.Population;
        country.NeedsFulfilled = country.Pops.Sum(p => p.NeedsFulfilled * p.Population) / country.Population;
        country.Unrest = country.Pops.Sum(p => p.Radicals * p.Population) / country.Population * 100m;
        foreach (var good in Catalog.Goods)
        {
            country.Production[good] = country.Cities.Sum(c => c.Production[good]) + country.Regions.Sum(r => r.SubsistenceProduction[good]);
            country.Consumption[good] = country.BuyOrders[good] - country.ExportOrders[good];
            // Kept solely for old-save compatibility. No game rule reads this legacy stock field.
            country.Goods[good] = 0m;
        }
        country.Gdp = country.Cities.Sum(c => c.Gdp) + country.Regions.Sum(r => r.SubsistenceGdp);
        country.Spending = country.Population / 1_000_000m * 5m + 35m + country.InstitutionSpending +
            (country.ResearchId != "" ? 6m : 0m) + (country.MobilizedAgainst != "" ? 45m : 0m) + country.Debt * .00012m +
            country.Regions.Sum(r => r.RailwayLevels * (r.LocalPrices["coal"] * 2m + r.LocalPrices["tools"] * .2m + 5m));
        country.DailyBalance = country.TaxRevenue + country.TariffRevenue + country.GovernmentDividends - country.Spending;
        country.Treasury += country.DailyBalance;
        if (country.Treasury < 0m) { country.Debt -= country.Treasury; country.Treasury = 0m; }
        TickDemographics(country, .000008m + GetHealthcareEffect(country) * .000003m - (1m - country.NeedsFulfilled) * .000015m);
    }

    public CommandResult ExpandRailway(string regionId)
    {
        if (State.Finished) return Fail("本局已结束。");
        var region = Player.Regions.FirstOrDefault(r => r.Id == regionId);
        if (region == null) return Fail("只能修建本国铁路。");
        if (!Player.Technologies.Contains("railways")) return Fail("需要先研究铁路。");
        var city = Player.Cities.Where(c => c.RegionId == regionId).OrderByDescending(c => c.Population).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        if (city == null) return Fail("该地区尚无可施工城市。");
        return BuildFor(Player, ConstructionCatalog.RailwayId, city.Id);
    }
}
