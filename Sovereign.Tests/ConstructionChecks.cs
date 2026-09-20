using Sovereign.Simulation;
using System.Text.Json.Nodes;
using System.Reflection;

internal static class ConstructionChecks
{
    private static SimulationEngine Scenario()
    {
        var game = SimulationEngine.NewGame("QNG");
        SimulationEngine.InitializeProduction(game.Player);
        foreach (var good in Catalog.Goods) { game.Player.BuyOrders[good] = 100m; game.Player.SellOrders[good] = 100m; }
        return game;
    }
    private static void Tick(SimulationEngine game) => typeof(SimulationEngine).GetMethod("TickConstruction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, new object[] { game.Player });

    public static void AllocationAndExpenses()
    {
        var game = Scenario(); decimal cash = game.Player.Treasury;
        var goods = new Dictionary<string, decimal>(game.Player.Goods);
        Check(game.BuildInCity("QNG_beijing", "farm").Success && game.BuildInCity("QNG_suzhou", "textile").Success, "Two projects queued");
        Check(cash == game.Player.Treasury && goods.All(p => game.Player.Goods[p.Key] == p.Value), "Queueing spends no upfront cash or materials");
        var forecast = game.GetConstructionStatus();
        Check(forecast.WeeklyCapacity == 16m && forecast.WeeklyAllocated == 16m, "National base plus staffed sector output");
        Check(forecast.Projects[0].WeeklyProgress == 10m && forecast.Projects[1].WeeklyProgress == 6m, "Ordered shared capacity respects single-project cap");
        var demand = SimulationEngine.GetConstructionDemand(game.Player);
        Check(demand["timber"] == 6m && demand["tools"] == 3m / 7m, "Active staffed sectors submit daily material buy orders");
        var consumption = new Dictionary<string, decimal>(game.Player.Consumption);
        Tick(game);
        Check(game.Player.Construction[0].ProgressPoints == 10m / 7m && game.Player.Construction[1].ProgressPoints == 6m / 7m, "Daily accrual uses weekly construction rate");
        Check(goods.All(p => game.Player.Goods[p.Key] == p.Value) && consumption.All(p => game.Player.Consumption[p.Key] == p.Value), "Construction tick does not remove stock or double count previously submitted orders");
        Check(Math.Abs(cash - game.Player.Treasury - game.Player.ConstructionSpending) < .000000001m && Math.Abs(game.Player.ConstructionSpending - forecast.WeeklyGovernmentCost / 7m) < .000000001m, "Government pays its exact material and wage share");
        game.SetConstructionPaused(true); var progress = game.Player.Construction.Sum(p => p.ProgressPoints); Tick(game);
        Check(game.Player.Construction.Sum(p => p.ProgressPoints) == progress && game.Player.ConstructionMaterialSpending == 0m && game.Player.ConstructionWages == 6m, "Idle government projects use no materials but sector workers remain paid");
        var sameCity = Scenario(); sameCity.BuildInCity("QNG_beijing", "farm"); sameCity.BuildInCity("QNG_beijing", "textile");
        Check(sameCity.GetConstructionStatus().WeeklyProgress == forecast.WeeklyProgress, "Cross-city queueing creates no extra construction capacity");
    }

    public static void QueueControlAndSpillover()
    {
        var game = Scenario();
        for (int i = 0; i < 7; i++) Check(game.BuildInCity("QNG_beijing", "farm").Success, "Old five-project city cap removed");
        string first = game.Player.Construction[0].Id, second = game.Player.Construction[1].Id;
        Check(game.MoveConstructionProject(second, 0).Success, "Move priority by stable ID");
        Check(game.GetConstructionStatus().Projects[0].Id == second && game.GetConstructionStatus().Projects[0].WeeklyProgress == 10m, "Promoted project receives first allocation");
        Check(game.SetProjectPaused(second, true).Success && game.GetConstructionStatus().Projects[1].WeeklyProgress == 10m, "Paused projects release construction to next project");
        game.SetProjectPaused(second, false); game.Player.Construction[0].ProgressPoints = game.Player.Construction[0].RequiredPoints - .5m;
        var plan = game.GetConstructionStatus();
        Check(plan.Projects[0].WeeklyProgress == 3.5m && plan.Projects[1].WeeklyProgress == 10m && plan.Projects[2].WeeklyProgress == 2.5m, "Completion-day unused capacity passes down the queue");
        int initial = game.GetCity("QNG_beijing").Industries["farm"], privateLevels = game.GetCity("QNG_beijing").Buildings["farm"].PrivateLevels;
        Tick(game);
        Check(game.GetCity("QNG_beijing").Industries["farm"] == initial + 1 && game.Player.Construction.All(p => p.Id != second), "Completes exactly one building level");
        Check(game.GetCity("QNG_beijing").Buildings["farm"].PrivateLevels == privateLevels, "Government completed levels do not become privately owned");
        decimal spentCash = game.Player.Treasury; var spentGoods = new Dictionary<string, decimal>(game.Player.Goods);
        Check(game.CancelConstruction(first).Success && game.Player.Treasury == spentCash && spentGoods.All(p => game.Player.Goods[p.Key] == p.Value), "Cancellation cannot refund already consumed resources");
        var digest = game.CanonicalDigest();
        Check(!game.MoveConstructionProject("unknown", 0).Success && !game.MoveConstructionProject(game.Player.Construction[0].Id, -1).Success && !game.SetProjectPaused("unknown", true).Success && !game.CancelConstruction("unknown").Success, "Invalid commands rejected");
        Check(digest == game.CanonicalDigest(), "Rejected queue commands leave state unchanged");
    }

    public static void ShortageAndSectorInvestment()
    {
        var game = Scenario();
        Check(!game.SetConstructionMethod("QNG_beijing", "iron").Success, "Iron-frame prerequisite enforced");
        game.Player.Technologies.Add("mechanical_tools");
        Check(game.SetConstructionMethod("QNG_beijing", "iron").Success, "Iron-frame method unlocked");
        for (int i = 0; i < 3; i++) game.BuildInCity("QNG_beijing", "farm");
        Check(game.GetConstructionStatus().WeeklyCapacity == 25m, "Iron frames increase staffed sector output");
        game.Player.SellOrders["iron"] = 0m;
        decimal availability = SimulationEngine.GetInputAvailability(game.Player, "iron");
        var shortage = game.GetConstructionStatus();
        Check(shortage.ShortageFactor == availability && shortage.WeeklyProgress == 10m + 15m * availability, "Order shortfall penalizes sector throughput while free base continues");
        game.Player.Goods["iron"] = 9_000_000m;
        Check(game.GetConstructionStatus().WeeklyProgress == shortage.WeeklyProgress, "A stockpile cannot bypass market order shortage");
        Tick(game);
        Check(game.Player.ConstructionMaterialSpending > 0m && game.Player.ConstructionWages == 9m, "Input orders are paid at market prices even when shortages penalize output");
        var mixed = Scenario(); mixed.Player.Technologies.Add("mechanical_tools");
        mixed.GetCity("QNG_suzhou").ConstructionSectors = 1; SimulationEngine.RefreshProductionEmployment(mixed.GetCity("QNG_suzhou"));
        mixed.SetConstructionMethod("QNG_suzhou", "iron");
        for (int i = 0; i < 3; i++) mixed.Build("farm");
        mixed.Player.SellOrders["iron"] = 0m;
        Check(mixed.GetConstructionStatus().WeeklyProgress == 16m + 5m * SimulationEngine.GetInputAvailability(mixed.Player, "iron"), "Iron shortage does not penalize wooden-frame output");
        var investment = Scenario();
        Check(investment.BuildConstructionSector("QNG_suzhou").Success, "Sector itself requires queue investment");
        Check(investment.GetCity("QNG_suzhou").ConstructionSectors == 0 && investment.GetConstructionStatus().WeeklyCapacity == 16m, "Unfinished sector creates no free capacity");
        investment.Player.Construction[0].ProgressPoints = ConstructionCatalog.Costs[ConstructionCatalog.SectorId] - 1m; Tick(investment);
        Check(investment.GetCity("QNG_suzhou").ConstructionSectors == 1 && investment.GetCity("QNG_suzhou").ConstructionWorkers == 1000 && investment.GetConstructionStatus().WeeklyCapacity == 18m, "Completed sector hires local workers and increases capacity");
    }

    public static void PrivateInvestmentAndFunding()
    {
        var game = Scenario(); game.Player.InvestmentPool = 1000m;
        game.Build("farm"); game.BuildInCity("QNG_suzhou", "textile"); game.Player.Construction[1].PrivateInvestment = true;
        string privateId = game.Player.Construction[1].Id;
        var status = game.GetConstructionStatus();
        Check(status.GovernmentWeeklyProgress == 8m && status.PrivateWeeklyProgress == 8m, "Both queues receive half the shared capacity");
        Check(status.WeeklyPrivateCost > 0m && status.WeeklyCost == status.WeeklyGovernmentCost + status.WeeklyPrivateCost, "Private material and workforce share is separate from government budget");
        decimal treasury = game.Player.Treasury, pool = game.Player.InvestmentPool; Tick(game);
        Check(Math.Abs(pool - game.Player.InvestmentPool - game.Player.DailyPrivateConstructionSpending) < .000000001m && Math.Abs(treasury - game.Player.Treasury - game.Player.ConstructionSpending) < .000000001m, "Private spending debits only the investment pool");
        Check(!game.CancelConstruction(privateId).Success && !game.SetProjectPaused(privateId, true).Success && !game.MoveConstructionProject(privateId, 0).Success, "Private queue cannot be edited as government projects");
        game.SetConstructionPaused(true);
        Check(game.GetConstructionStatus().PrivateWeeklyProgress == 10m && game.GetConstructionStatus().GovernmentWeeklyProgress == 0m, "Private investors borrow unused government share and respect project cap");
        game.SetConstructionPaused(false); game.SetPrivateConstructionEnabled(false);
        Check(game.GetConstructionStatus().PrivateWeeklyProgress == 0m && game.GetConstructionStatus().GovernmentWeeklyProgress == 10m, "Disabled private construction returns its share without erasing projects");
        game.SetPrivateConstructionEnabled(true); game.Player.InvestmentPool = .01m;
        status = game.GetConstructionStatus();
        Check(status.WeeklyPrivateCost <= .07m && status.PrivateWeeklyProgress < 8m, "An insufficient pool limits private dispatch before billing");
        Tick(game); Check(game.Player.InvestmentPool >= 0m, "Private funding cannot create debt or overdraw its pool");
        game.Player.InvestmentPool = 0m;
        Check(game.GetConstructionStatus().PrivateWeeklyProgress == 0m && game.GetConstructionStatus().GovernmentWeeklyProgress == 10m, "Empty private pool releases capacity to government");
        game.Player.InvestmentPool = 100m;
        int privateLevels = game.GetCity("QNG_suzhou").Buildings["textile"].PrivateLevels;
        game.Player.Construction.Single(p => p.Id == privateId).ProgressPoints = ConstructionCatalog.Costs["textile"] - .1m;
        Tick(game);
        Check(game.GetCity("QNG_suzhou").Buildings["textile"].PrivateLevels == privateLevels + 1, "Completed private project adds one privately owned level");
        var candidate = Scenario(); candidate.Player.InvestmentPool = 100m;
        foreach (var city in candidate.Player.Cities) foreach (var account in city.Buildings.Values) account.DailyProfit = 0m;
        Check(!SimulationEngine.MaybeQueuePrivateConstruction(candidate.Player), "Investors do not automatically build unprofitable industries");
        candidate.GetCity("QNG_beijing").Buildings["farm"].DailyProfit = 100m;
        Check(SimulationEngine.MaybeQueuePrivateConstruction(candidate.Player) && candidate.Player.Construction[^1].PrivateInvestment && candidate.Player.Construction[^1].CityId == "QNG_beijing" && candidate.Player.InvestmentPool == 100m, "Profitable city is selected independently with no upfront pool charge");
    }

    public static void SnapshotAndRailway()
    {
        var game = Scenario(); game.Build("farm"); game.Build("lumber");
        SimulationEngine.BeginConstructionDay(game.Player);
        var demand = SimulationEngine.GetConstructionDemand(game.Player);
        game.GetCity("QNG_beijing").ConstructionWorkers = 0;
        Check(SimulationEngine.GetConstructionDemand(game.Player).All(p => p.Value == demand[p.Key]), "Employment changes cannot rewrite submitted construction buy orders");
        Tick(game);
        Check(game.Player.ConstructionPoints == 16m / 7m, "Construction settles the workforce that submitted the daily orders");
        var privateGame = Scenario(); privateGame.Build("farm"); privateGame.Build("lumber"); privateGame.Player.Construction[1].PrivateInvestment = true;
        SimulationEngine.BeginConstructionDay(privateGame.Player);
        privateGame.Player.InvestmentPool = 100m; Tick(privateGame);
        Check(privateGame.Player.Construction[1].ProgressPoints == 0m && privateGame.Player.DailyPrivateConstructionSpending == 0m, "A same-day contribution cannot fund material orders that were never submitted");
        var repriced = Scenario(); repriced.Build("farm"); repriced.Build("lumber"); repriced.Player.Construction[1].PrivateInvestment = true;
        repriced.Player.InvestmentPool = 10m; SimulationEngine.BeginConstructionDay(repriced.Player);
        decimal initialPrivate = repriced.GetConstructionStatus().PrivateWeeklyProgress;
        foreach (var good in Catalog.Goods) repriced.Player.Prices[good] *= 100m;
        Check(repriced.GetConstructionStatus().WeeklyPrivateCost <= 70m && repriced.GetConstructionStatus().PrivateWeeklyProgress < initialPrivate, "Market repricing scales down private dispatch before it can overspend the frozen funding");
        Tick(repriced); Check(repriced.Player.InvestmentPool >= 0m, "Settlement remains solvent after an input price jump");
        var bounded = Scenario(); bounded.Build("farm"); bounded.Build("lumber"); bounded.Player.Construction[1].PrivateInvestment = true;
        bounded.Player.InvestmentPool = 5m;
        foreach (var good in Catalog.Goods) bounded.Player.Prices[good] = Catalog.BasePrices[good] * .25m;
        SimulationEngine.BeginConstructionDay(bounded.Player);
        var submitted = SimulationEngine.GetConstructionDemand(bounded.Player);
        foreach (var good in Catalog.Goods) bounded.Player.Prices[good] = Catalog.BasePrices[good] * 1.75m;
        var settlement = bounded.GetConstructionStatus();
        Check(settlement.Inputs.All(input => Math.Abs(input.WeeklyDemand / 7m - submitted[input.GoodId]) < .000000001m), "Repricing across the full legal range preserves submitted material quantities");
        Check(settlement.WeeklyPrivateCost <= 35m, "Ceiling reservation guarantees the committed orders remain affordable");
        Tick(bounded);
        Check(bounded.Player.InvestmentPool >= 0m, "Frozen orders settle without overdrawing investment funds");
        var railway = Scenario();
        Check(!railway.BuildInCity("QNG_beijing", ConstructionCatalog.RailwayId).Success, "Railway construction has a technology prerequisite");
        railway.Player.Technologies.Add("railways");
        var city = railway.GetCity("QNG_beijing"); var region = railway.Player.Regions.Single(r => r.Id == city.RegionId);
        int industryLevels = city.Industries.Values.Sum(); decimal treasury = railway.Player.Treasury;
        Check(railway.BuildInCity(city.Id, ConstructionCatalog.RailwayId).Success && railway.Player.Treasury == treasury && region.RailwayLevels == 0, "Railway joins the construction queue without upfront cost or instant completion");
        railway.Player.Construction[0].ProgressPoints = 159.9m; Tick(railway);
        Check(region.RailwayLevels == 1 && city.Industries.Values.Sum() == industryLevels, "Completed railway increases regional infrastructure without creating a seventh industry");
        region.RailwayLevels = 19;
        var neighbor = railway.Player.Cities.First(c => c.RegionId == city.RegionId && c.Id != city.Id);
        Check(railway.BuildInCity(city.Id, ConstructionCatalog.RailwayId).Success && !railway.BuildInCity(neighbor.Id, ConstructionCatalog.RailwayId).Success, "Railway level cap counts pending construction across all cities in the same province");
    }
    public static void PrepaidMigrationAndReplay()
    {
        string fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-v3-construction.json"));
        var oldCountry = JsonNode.Parse(fixture)!["State"]!["Countries"]!.AsArray().Single(c => c!["Id"]!.GetValue<string>() == "QNG")!;
        var game = SimulationEngine.LoadJson(fixture);
        Check(game.State.SaveVersion == 6 && game.Player.Construction.Count == 2 && game.Player.Construction.All(p => p.LegacyPrepaid), "Released v3 projects migrate as prepaid construction");
        Check(game.Player.Treasury == oldCountry["Treasury"]!.GetValue<decimal>() && game.Player.Cities.All(c => c.ConstructionSectors == 0), "Migration preserves cash and creates no free sector buildings or payroll");
        for (int i = 0; i < 2; i++)
        {
            var old = oldCountry["Construction"]![i]!; var current = game.Player.Construction[i];
            Check(current.ProgressPoints == current.RequiredPoints * (old["TotalDays"]!.GetValue<int>() - old["DaysRemaining"]!.GetValue<int>()) / old["TotalDays"]!.GetValue<int>(), "Legacy completed share preserved");
        }
        game.AdvanceDays(1);
        Check(game.Player.ConstructionSpending == 0m && game.Player.ConstructionPoints > 0m, "Prepaid projects progress without charging again");
        game.BuildConstructionSector("QNG_beijing"); game.SetProjectPaused(game.Player.Construction[1].Id, true);
        var replay = SimulationEngine.LoadJson(game.SaveJson());
        for (int day = 0; day < 200; day++)
        {
            if (game.State.PendingEvent is { } ev) game.ResolveEvent(ev.Options[^1].Id);
            if (replay.State.PendingEvent is { } same) replay.ResolveEvent(same.Options[^1].Id);
            game.AdvanceDays(1); replay.AdvanceDays(1);
        }
        Check(game.CanonicalDigest() == replay.CanonicalDigest(), "Partial progress, priority, pause, prepaid flags and IDs survive deterministic replay");
    }

    public static void InvalidConstructionAndCampaignGuards()
    {
        static void Reject(Action<SimulationEngine> mutate)
        {
            var game = SimulationEngine.NewGame("QNG"); game.Build("farm"); mutate(game);
            try { game.SaveJson(); } catch (InvalidDataException) { return; }
            throw new Exception("Invalid construction state accepted");
        }
        Reject(g => g.Player.Construction[0].RequiredPoints++);
        Reject(g => g.Player.Construction[0].ProgressPoints = -1m);
        Reject(g => g.Player.Construction[0].CityId = "GBR_london");
        Reject(g => { g.Build("farm"); g.Player.Construction[1].Id = g.Player.Construction[0].Id; });
        Reject(g => g.Player.NextConstructionId = 1);
        Reject(g => g.Player.Construction[0].LegacyPrepaid = true);
        Reject(g => g.Player.Construction[0].TotalDays = 45);
        Reject(g => g.Player.Cities[0].ConstructionMethodId = "missing");
        Reject(g => g.Player.Cities[0].ConstructionWorkers++);
        Reject(g => g.Player.ConstructionSpending++);
        var complete = SimulationEngine.NewGame("QNG"); complete.Build("farm"); complete.State.Date = Catalog.EndDate; complete.State.DayNumber = 3653; complete.State.Finished = true;
        string id = complete.Player.Construction[0].Id, digest = complete.CanonicalDigest();
        Check(!complete.BuildConstructionSector("QNG_beijing").Success && !complete.SetConstructionMethod("QNG_beijing", "wood").Success &&
            !complete.SetConstructionPaused(true).Success && !complete.SetProjectPaused(id, true).Success && !complete.CancelConstruction(id).Success && !complete.MoveConstructionProject(id, 0).Success,
            "All new construction commands honor campaign completion");
        Check(complete.CanonicalDigest() == digest, "Completed world remains unchanged");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}

