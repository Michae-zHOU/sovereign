using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sovereign.Simulation;

/// <summary>A deterministic daily economy. All economic starting values are game-balanced estimates.</summary>
public sealed partial class SimulationEngine
{
    public WorldState State { get; set; } = new();
    public CountryState Player => State.Countries.Single(c => c.Id == State.PlayerCountryId);

    public static SimulationEngine NewGame(string countryId)
    {
        if (!GeographyCatalog.Countries.Any(c => c.Id == countryId)) throw new ArgumentException("Choose a country in the scenario catalog.", nameof(countryId));
        var engine = new SimulationEngine { State = new WorldState { PlayerCountryId = countryId } };
        foreach (var d in GeographyCatalog.Countries)
            engine.State.Countries.Add(CreateCountry(d.Id, d.Name, d.Adjective, d.InitialPopulation, d.InitialTreasury, d.InitialLiteracy, d.InitialPrestige, d.IndustryLevels));
        foreach (var good in Catalog.Goods) engine.State.ExternalGoods[good] = 80_000m;
        foreach (var c in engine.State.Countries)
        {
            foreach (var other in engine.State.Countries.Where(o => o.Id != c.Id))
            {
                c.Relations[other.Id] = c.Id == "JAP" || other.Id == "JAP" ? -10 : 10;
                c.DiplomaticCooldowns[other.Id] = 0;
            }
            RecordHistory(c, engine.State.Date);
        }
        engine.AddLog("A new decade begins. Build an economy, improve daily life, and guide your nation's reforms.");
        engine.AddLog($"Scenario: {GeographyCatalog.Countries.Count} countries, {GeographyCatalog.Cities.Count} cities, 1836–1846. Populations, regional groupings and economic outcomes are authored estimates, not census records.");
        return engine;
    }

    private static CountryState CreateCountry(string id, string name, string adjective, long population,
        decimal treasury, decimal literacy, decimal prestige, params int[] levels)
    {
        var c = new CountryState { Id = id, Name = name, Adjective = adjective, Population = population,
            Treasury = treasury, Literacy = literacy, Prestige = prestige, Unrest = id == "JAP" ? 20m : 12m,
            LivingStandard = id == "GBR" ? 14m : id == "PRU" ? 12m : 10m };
        for (int i = 0; i < Catalog.Industries.Count; i++) c.Industries[Catalog.Industries[i].Id] = levels[i];
        InitializeGeography(c);
        InitializeConstructionSectors(c);
        foreach (var good in Catalog.Goods)
        {
            c.Goods[good] = TargetStock(c, good);
            c.Prices[good] = Catalog.BasePrices[good];
            c.Production[good] = Catalog.Industries.Where(x => x.Produces == good).Sum(x => x.Output * c.Industries[x.Id]);
            c.Consumption[good] = HouseholdNeed(c, good) + IndustrialNeed(c, good);
            c.Trade[good] = 0;
        }
        InitializeCityEconomies(c);
        c.Gdp = c.Production.Sum(p => p.Value * Catalog.BasePrices[p.Key]) * 365m;
        c.TaxRevenue = c.Gdp / 365m * c.TaxRate / 100m;
        c.Spending = population / 1_000_000m * 5m + 35m;
        c.ConstructionWages = PlanConstruction(c).Status.WeeklyWages / 7m;
        c.ConstructionSpending = c.ConstructionWages;
        c.Spending += c.ConstructionSpending;
        c.DailyBalance = c.TaxRevenue - c.Spending;
        InitializeOrderEconomy(c);
        return c;
    }

    public void AdvanceDays(int days)
    {
        if (days < 0 || days > 100_000) throw new ArgumentOutOfRangeException(nameof(days));
        for (int i = 0; i < days && !State.Finished && State.PendingEvent == null; i++)
        {
            foreach (var c in State.Countries.OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                if (c.Id != State.PlayerCountryId && State.DayNumber % 30 == 0) RunAi(c);
                if (State.DayNumber % 30 == 0) MaybeQueuePrivateConstruction(c);
                TickOrderEconomy(c);
                TickConstruction(c);
                c.InvestmentPool += c.DailyInvestmentContribution;
                ReconcilePops(c);
                TickPolitics(c);
                if (c.Treasury > 2000m && c.Debt > 0m)
                {
                    decimal repaid = Math.Min(c.Debt, Math.Max(0m, c.DailyBalance) * .5m);
                    c.Treasury -= repaid; c.Debt -= repaid;
                }
                TickResearch(c);
                foreach (var target in c.DiplomaticCooldowns.Keys.ToArray())
                    c.DiplomaticCooldowns[target] = Math.Max(0, c.DiplomaticCooldowns[target] - 1);
            }
            State.Date = State.Date.AddDays(1);
            State.DayNumber++;
            if (State.Date.Day == 1)
                foreach (var c in State.Countries) RecordHistory(c, State.Date);
            CheckEvents();
            if (State.Date >= Catalog.EndDate)
            {
                State.Finished = true;
                AddLog("The decade is complete. Review the nation you built, or begin a new campaign.");
            }
        }
    }

    private static decimal HouseholdNeed(CountryState c, string good) => c.Population / 1_000_000m * (good switch
        { "grain" => 8m, "timber" => .5m, "clothes" => 1.6m, _ => 0m });
    private static decimal IndustrialNeed(CountryState c, string good) => Catalog.Industries.Sum(i =>
        i.Inputs.TryGetValue(good, out var amount) ? amount * c.Industries.GetValueOrDefault(i.Id) : 0m);
    private static decimal TargetStock(CountryState c, string good) => Math.Max(good == "tools" ? 150m : 100m,
        (HouseholdNeed(c, good) + IndustrialNeed(c, good)) * 10m);
    private static decimal Clamp(decimal value, decimal minimum, decimal maximum) => Math.Max(minimum, Math.Min(maximum, value));

    public CommandResult SetTaxRate(int percent)
    {
        if (State.Finished) return Fail("The campaign is complete.");
        if (percent < 5 || percent > 50) return Fail("Tax rate must be between 5% and 50%.");
        Player.TaxRate = percent;
        return Ok($"Income tax set to {percent}%. High taxes reduce household purchasing power.");
    }

    public CommandResult EnactReform(string reformId) => PoliticalCatalog.LegacyReformLawMap.TryGetValue(reformId, out var law)
        ? StartLawEnactment(law) : Fail("未知的改革。");

    public CommandResult SetResearch(string technologyId) => SetResearchFor(Player, technologyId);
    private CommandResult SetResearchFor(CountryState c, string technologyId)
    {
        if (State.Finished) return Fail("The campaign is complete.");
        var tech = Catalog.Technologies.FirstOrDefault(t => t.Id == technologyId);
        if (tech == null) return Fail("Unknown technology.");
        if (c.Technologies.Contains(technologyId)) return Fail("This technology is already known.");
        if (c.ResearchId == technologyId) return Fail("This technology is already being researched.");
        if (technologyId == "steam_power" && !c.Technologies.Contains("mechanical_tools")) return Fail("Research Mechanical tools first.");
        c.ResearchId = technologyId; c.ResearchProgress = 0m;
        return Ok($"Researching {tech.Name}. Switching research resets current progress. Upkeep: 6 per day.");
    }

    private void TickResearch(CountryState c)
    {
        if (c.ResearchId.Length == 0) return;
        var t = Catalog.Technologies.Single(t => t.Id == c.ResearchId);
        c.ResearchProgress += .45m + c.Literacy / 100m;
        if (c.ResearchProgress < t.Days) return;
        c.Technologies.Add(t.Id); c.ResearchId = ""; c.ResearchProgress = 0m; c.Prestige += 3m;
        if (c.Id == State.PlayerCountryId) AddLog($"Research complete: {t.Name}.");
    }

    public CommandResult SetTrade(string goodId, int direction)
    {
        if (State.Finished) return Fail("The campaign is complete.");
        if (!Catalog.Goods.Contains(goodId)) return Fail("Unknown good.");
        if (direction < -1 || direction > 1) return Fail("Trade direction must be -1 (export), 0 (off), or 1 (import).");
        Player.Trade[goodId] = direction;
        return Ok($"{goodId}: {(direction == 1 ? "imports enabled" : direction == -1 ? "exports enabled" : "trade stopped")}. 贸易政策通过商人的进出口订单影响市场；本阶段运输容量为简化参数。");
    }

    public CommandResult Diplomacy(string otherCountryId, string action)
    {
        var offer = DiplomaticOffers(otherCountryId).FirstOrDefault(o => o.Id == action);
        if (offer == null) return Fail("Unknown diplomatic action.");
        if (!offer.Available) return Fail(offer.BlockedReason);
        var other = State.Countries.First(c => c.Id == otherCountryId);
        decimal cost = offer.Cost;
        Player.Treasury -= cost;
        int change = action switch { "improve" => 15, "trade_agreement" => 8, "mobilize" => -30, _ => 15 };
        Player.Relations[otherCountryId] = Math.Clamp(Player.Relations[otherCountryId] + change, -100, 100);
        other.Relations[Player.Id] = Player.Relations[otherCountryId];
        Player.DiplomaticCooldowns[otherCountryId] = 90;
        if (action == "trade_agreement") { Player.TradePacts.Add(other.Id); other.TradePacts.Add(Player.Id); }
        if (action == "mobilize")
        {
            Player.MobilizedAgainst = otherCountryId; Player.Prestige += 4m;
            Player.TradePacts.Remove(other.Id); other.TradePacts.Remove(Player.Id);
        }
        if (action == "deescalate") { Player.MobilizedAgainst = ""; Player.Prestige = Math.Max(0m, Player.Prestige - 4m); }
        AddLog($"Diplomacy with {other.Name}: {action.Replace('_', ' ')}. Relations {Player.Relations[other.Id].ToString("+0;-0;0", CultureInfo.InvariantCulture)}.");
        return Ok(action == "mobilize" ? "Armed pressure applied: +45 daily upkeep, higher unrest. This slice has no battles or conquest." : "Diplomatic mission completed.");
    }

    private void RunAi(CountryState c)
    {
        if (c.ResearchId.Length == 0)
        {
            var tech = Catalog.Technologies.FirstOrDefault(t => !c.Technologies.Contains(t.Id));
            if (tech != null) SetResearchFor(c, tech.Id);
        }
        foreach (var good in Catalog.Goods)
            c.Trade[good] = c.Prices[good] > Catalog.BasePrices[good] * 1.2m ? 1
                : c.Prices[good] < Catalog.BasePrices[good] * .8m ? -1 : 0;
        if (c.Construction.Count == 0 && c.Treasury > 10000m)
        {
            var industry = Catalog.Industries.OrderByDescending(i => c.Prices[i.Produces] / Catalog.BasePrices[i.Produces]).ThenBy(i => i.Id, StringComparer.Ordinal).First();
            var city = c.Cities.OrderBy(x => (decimal)x.Industries.Values.Sum() / Math.Max(1, x.Workforce)).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            BuildFor(c, industry.Id, city.Id);
        }
        if (State.DayNumber % 180 == 0 && c.LawEnactment == null)
        {
            var reform = PoliticalCatalog.Laws.Where(l => GetLawEnactmentBlockReason(l.Id, c).Length == 0)
                .OrderByDescending(l => GetLawSupport(l.Id, c).Support).ThenBy(l => l.Id, StringComparer.Ordinal).FirstOrDefault();
            if (reform != null) StartLawEnactmentFor(c, reform.Id);
        }
        if (c.Unrest > 35m) c.TaxRate = Math.Max(15, c.TaxRate - 1);
        else if (c.DailyBalance < -10m) c.TaxRate = Math.Min(30, c.TaxRate + 1);
    }

    private static void RecordHistory(CountryState c, DateTime date)
    {
        c.History.Add(new HistoryPoint { Date = date, Gdp = c.Gdp, Treasury = c.Treasury, LivingStandard = c.LivingStandard });
        if (c.History.Count > 150) c.History.RemoveAt(0);
    }
    private void AddLog(string message)
    {
        State.Log.Add($"{State.Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} · {message}");
        if (State.Log.Count > 120) State.Log.RemoveAt(0);
    }
    private static CommandResult Ok(string message) => new(true, message);
    private static CommandResult Fail(string message) => new(false, message);
}
