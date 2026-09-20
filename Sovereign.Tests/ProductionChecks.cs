using Sovereign.Simulation;

internal static class ProductionChecks
{
    public static void MethodsAndEmployment()
    {
        foreach (var industry in Catalog.Industries)
        {
            var methods = ProductionCatalog.MethodsFor(industry.Id);
            Check(methods.Count == 3 && methods.Select(m => m.Id).Distinct().Count() == 3, "Every industry has three distinct authored methods");
            Check(methods.All(m => m.JobsPerLevel > 0 && m.SkilledFraction >= 0m && m.SkilledFraction <= 1m && m.Inputs.Keys.Concat(m.Outputs.Keys).All(Catalog.Goods.Contains)), "Recipes use supported goods and bounded jobs");
            Check(methods[0].RequiredTechnology == "" && methods[1].RequiredTechnology == "mechanical_tools" && methods[2].RequiredTechnology == "steam_power", "Methods have explicit technology gates");
            Check(methods[2].Outputs.Values.Sum() > methods[0].Outputs.Values.Sum() && methods[2].JobsPerLevel < methods[0].JobsPerLevel && methods[2].SkilledFraction > methods[0].SkilledFraction, "Advanced methods change throughput, labor and skill requirements");
        }
        var game = SimulationEngine.NewGame("QNG");
        SimulationEngine.InitializeProduction(game.Player);
        var city = game.GetCity("QNG_beijing");
        Check(city.Buildings.All(p => p.Value.PrivateLevels == city.Industries[p.Key]), "Scenario initial ownership includes existing levels only");
        city.Buildings["farm"].CashReserves = 123m;
        SimulationEngine.InitializeProduction(game.Player);
        Check(city.Buildings["farm"].CashReserves == 123m, "Initialization is idempotent and preserves building accounts");
        game.Player.Technologies.Add("mechanical_tools"); game.Player.Technologies.Add("steam_power");
        decimal treasury = game.Player.Treasury;
        Check(game.SetProductionMethod(city.Id, "farm", "steam").Success && treasury == game.Player.Treasury, "Changing an unlocked method does not silently buy a building");
        city.Literacy = 0m; SimulationEngine.RefreshProductionEmployment(city);
        Check(city.IndustrialWorkers == 0 && city.Buildings.Values.All(a => a.Workers == 0), "No qualified workers means skilled production cannot hire its full recipe");
        city.Literacy = 100m; SimulationEngine.RefreshProductionEmployment(city);
        Check(city.IndustrialWorkers > 0 && city.Buildings.Values.Sum(a => a.Workers) == city.IndustrialWorkers, "Employment is allocated to real building accounts");
        Check(city.Employed == city.Artisans + city.IndustrialWorkers + city.ConstructionWorkers && city.Employed <= city.Workforce && city.Dependents + city.Workforce == city.Population, "Jobs and dependent population reconcile without inventing workers");
        long fullFarmWorkers = city.Buildings["farm"].Workers;
        city.Buildings["farm"].EmploymentScale = .5m; SimulationEngine.RefreshProductionEmployment(city);
        Check(city.Buildings["farm"].Workers < fullFarmWorkers && city.Buildings.Values.Sum(a => a.Workers) == city.IndustrialWorkers, "A loss-driven hiring reduction removes real employed workers");
    }

    public static void RejectionsAndRoundtrip()
    {
        var game = SimulationEngine.NewGame("QNG");
        SimulationEngine.InitializeProduction(game.Player);
        string digest = game.CanonicalDigest();
        Check(!game.SetProductionMethod("GBR_london", "farm", "basic").Success && !game.SetProductionMethod("QNG_beijing", "missing", "basic").Success &&
            !game.SetProductionMethod("QNG_beijing", "farm", "missing").Success && !game.SetProductionMethod("QNG_beijing", "farm", "steam").Success, "Foreign city, invalid recipe and missing technology have rejected commands");
        Check(digest == game.CanonicalDigest(), "Rejected production changes are side effect free");
        game.Player.Technologies.Add("mechanical_tools");
        Check(game.SetProductionMethod("QNG_beijing", "farm", "mechanized").Success, "Technology unlock applies recipe");
        var account = game.GetCity("QNG_beijing").Buildings["farm"];
        account.CashReserves = 41m;
        var loaded = SimulationEngine.LoadJson(game.SaveJson());
        Check(loaded.GetCity("QNG_beijing").Buildings["farm"].MethodId == "mechanized" && loaded.GetCity("QNG_beijing").Buildings["farm"].CashReserves == 41m, "Methods, accounts and staffing survive persistence");
        game.State.Finished = true;
        Check(!game.SetProductionMethod("QNG_beijing", "farm", "basic").Success, "Finished campaigns reject method changes");
        foreach (Action<SimulationEngine> corrupt in new Action<SimulationEngine>[]
        {
            g => g.GetCity("QNG_beijing").Buildings["farm"].PrivateLevels = 101,
            g => g.GetCity("QNG_beijing").Buildings["farm"].EmploymentScale = 0m,
            g => g.GetCity("QNG_beijing").Buildings["farm"].Workers++,
            g => g.GetCity("QNG_beijing").Buildings["farm"].Debt = -1m,
            g => g.Player.InvestmentPool = -1m
        })
        {
            var invalid = SimulationEngine.NewGame("QNG"); corrupt(invalid);
            bool rejected = false;
            try { SimulationEngine.ValidateProduction(invalid.Player); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Malformed ownership, staffing and financial accounts must be rejected");
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
