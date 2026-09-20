using Sovereign.Simulation;
using System.Text.Json;

internal static class PoliticalChecks
{
    public static void Run()
    {
        CatalogAndPoliticalPower();
        RejectedCommandsAreAtomic();
        EnactmentStagesAndFailure();
        SaveAndRandomReplay();
        InstitutionBudgets();
        InvalidPoliticalState();
    }

    public static void CatalogAndPoliticalPower()
    {
        var game = SimulationEngine.NewGame("QNG"); var c = game.Player;
        Check(PoliticalCatalog.InterestGroups.Count == 8 && PoliticalCatalog.LawGroups.Count == 8 && PoliticalCatalog.Laws.Count >= 18, "Eight groups and eight law families are playable");
        Check(c.Laws.Count == 8 && c.Laws["land"] == "serfdom" && c.Laws["economy"] == "traditionalism" && c.Laws["governance"] == "autocracy" && c.Laws["education"] == "no_schools", "Qing starts with authored traditional institutions");
        Check(c.InterestGroups.Sum(g => g.Clout) == 100m, "Clout is normalized exactly");
        decimal oldLandowners = Group(c, "landowners").Clout, oldRural = Group(c, "rural_folk").Clout;
        c.Laws["land"] = "homesteading"; game.TickPolitics(c);
        Check(Group(c, "landowners").Clout < oldLandowners && Group(c, "rural_folk").Clout > oldRural, "Land laws transfer relative power from landlords to rural households");
        decimal industrialists = Group(c, "industrialists").Clout;
        foreach (var pop in c.Pops.Where(p => p.ProfessionId == "capitalists")) pop.Wealth += 10m;
        game.TickPolitics(c);
        Check(Group(c, "industrialists").Clout > industrialists, "Capitalists' increased wealth increases industrialist power");
        decimal previousApproval = Group(c, "rural_folk").Approval;
        foreach (var pop in c.Pops) { pop.Radicals = .8m; pop.Loyalists = 0m; }
        game.TickPolitics(c);
        Check(Group(c, "rural_folk").Approval < previousApproval, "Population radicalization reduces political approval");
        var support = game.GetLawSupport("public_schools");
        decimal expected = c.InterestGroups.Where(g => PoliticalCatalog.FindLaw("public_schools").Preferences[g.Id] > PoliticalCatalog.FindLaw("no_schools").Preferences[g.Id]).Sum(g => g.Clout);
        Check(support.Support == expected && support.Support + support.Opposition + support.Neutral == 100m && support.AdvanceChance + support.SetbackChance <= 100m, "Law support equals the relevant population-derived clout, with bounded outcome chances");
        var migrated = SimulationEngine.NewGame("QNG").Player;
        migrated.PoliticsInitialized = false; migrated.Reforms.UnionWith(PoliticalCatalog.LegacyReformLawMap.Keys);
        SimulationEngine.InitializePolitics(migrated);
        Check(migrated.Reforms.Count == 3 && migrated.Laws["education"] == "public_schools" && migrated.Laws["labor"] == "worker_protection" && migrated.Laws["health"] == "public_healthcare" && SimulationEngine.GetHealthcareEffect(migrated) == 1m, "Legacy earned reforms migrate without losing their effects");
        SimulationEngine.ValidatePolitics(migrated);
    }

    public static void RejectedCommandsAreAtomic()
    {
        var game = SimulationEngine.NewGame("QNG"); var c = game.Player;
        string before = game.CanonicalDigest();
        Check(!game.StartLawEnactment("unknown").Success && !game.StartLawEnactment("traditionalism").Success, "Unknown and already enacted laws are rejected");
        Check(!game.StartLawEnactment("public_schools").Success, "At least one governing supporter is required");
        Check(!game.CancelLawEnactment().Success && !game.SetGovernment(Array.Empty<string>()).Success && !game.SetGovernment(new[] { "landowners", "landowners" }).Success && !game.SetGovernment(new[] { "missing" }).Success, "Invalid cabinets and cancellation are rejected");
        Check(!game.SetGovernment(new[] { "industrialists" }).Success && !game.SetInstitutionLevel("education", 1).Success && !game.SetInstitutionLevel("unknown", 2).Success && !game.SetInstitutionLevel("police", -1).Success, "Institution unlock and autocracy constraints are enforced");
        Check(before == game.CanonicalDigest(), "All rejected political commands leave every serialized field unchanged");
        Check(game.SetGovernment(new[] { "landowners", "armed_forces", "devout", "intelligentsia" }).Success, "A lawful coalition can be formed");
        decimal treasury = c.Treasury;
        Check(game.StartLawEnactment("public_schools").Success && c.Treasury == treasury && !c.Reforms.Contains("primary_schools") && c.Laws["education"] == "no_schools", "Proposing a bill neither purchases nor instantly enacts a reform");
        before = game.CanonicalDigest();
        Check(!game.StartLawEnactment("religious_schools").Success && !game.SetGovernment(new[] { "landowners", "intelligentsia" }).Success, "Only one bill and a cabinet cooldown are enforced");
        Check(before == game.CanonicalDigest(), "Rejected active-bill commands preserve progress and PRNG");
        uint rng = c.PoliticalRandomState;
        Check(game.CancelLawEnactment().Success && c.LawEnactment == null && c.Laws["education"] == "no_schools" && rng == c.PoliticalRandomState, "Cancellation preserves current law and does not draw randomness");
    }

    public static void EnactmentStagesAndFailure()
    {
        var game = SchoolBill(); var c = game.Player;
        c.PoliticalRandomState = SeedFor(game.GetLawSupport("public_schools"), true);
        decimal cash = c.Treasury;
        for (int stage = 1; stage <= 3; stage++)
        {
            var bill = c.LawEnactment!; int duration = bill.StageDays;
            for (int day = 1; day < duration; day++) game.TickPolitics(c);
            Check(c.LawEnactment?.Stage == stage && c.Laws["education"] == "no_schools" && !c.Reforms.Contains("primary_schools"), "A bill cannot pass before the end of all three timed stages");
            game.TickPolitics(c);
            if (stage < 3) Check(c.LawEnactment?.Stage == stage + 1 && c.LawEnactment.DaysInStage == 0, "Stage success advances exactly once");
        }
        Check(c.LawEnactment == null && c.Laws["education"] == "public_schools" && c.Reforms.Contains("primary_schools") && c.InstitutionLevels["education"] == 1 && c.Treasury == cash, "Passing replaces one law and enables its first institution without a purchase fee");
        SimulationEngine.ValidatePolitics(c);

        var failure = SchoolBill(); var f = failure.Player;
        f.PoliticalRandomState = SeedFor(failure.GetLawSupport("public_schools"), false);
        for (int i = 0; i < 500 && f.LawEnactment != null; i++) failure.TickPolitics(f);
        Check(f.LawEnactment == null && f.Laws["education"] == "no_schools" && !f.Reforms.Contains("primary_schools") && f.LastLawOutcome.Contains("三次挫折"), "Three setbacks fail the law without leaking any enacted effects");
        SimulationEngine.ValidatePolitics(f);
    }

    public static void SaveAndRandomReplay()
    {
        var game = SchoolBill();
        for (int day = 0; day < game.Player.LawEnactment!.StageDays - 1; day++) game.TickPolitics(game.Player);
        var loaded = SimulationEngine.LoadJson(game.SaveJson());
        Check(game.CanonicalDigest() == loaded.CanonicalDigest(), "A pending bill round-trips including PRNG and days-in-stage");
        for (int day = 0; day < 450; day++)
        {
            game.TickPolitics(game.Player); loaded.TickPolitics(loaded.Player);
            Check(game.CanonicalDigest() == loaded.CanonicalDigest(), "Saved political random outcomes replay identically");
        }
        Check(game.Player.LawEnactment == null || game.Player.LawEnactment.Rounds > 0, "Replay actually crosses randomized stage boundaries");
    }

    public static void InstitutionBudgets()
    {
        var game = SchoolBill(); var c = game.Player;
        c.PoliticalRandomState = SeedFor(game.GetLawSupport("public_schools"), true);
        for (int i = 0; i < 400 && c.LawEnactment != null; i++) game.TickPolitics(c);
        Check(c.Laws["education"] == "public_schools", "Institution test law passes");
        decimal cash = c.Treasury;
        Check(game.SetInstitutionLevel("education", 0).Success && c.Treasury == cash && SimulationEngine.GetEducationEffect(c) == 0m, "Closing services releases capacity without refunding funds");
        decimal cost = SimulationEngine.GetInstitutionUpgradeCost(c, "education", 1);
        Check(game.SetInstitutionLevel("education", 1).Success && c.Treasury == cash - cost && SimulationEngine.GetEducationEffect(c) == 1m, "Reopening requires a real setup charge and activates services");
        int lastLevel = 1;
        for (int level = 2; level <= 5; level++)
        {
            string before = game.CanonicalDigest();
            var command = game.SetInstitutionLevel("education", level);
            if (!command.Success)
            {
                Check(before == game.CanonicalDigest(), "Over-budget upgrade fails atomically"); break;
            }
            lastLevel = level;
        }
        Check(lastLevel < 5 && c.BureaucracyUsed <= c.BureaucracyCapacity && c.InstitutionSpending == c.InstitutionLevels["education"] * 12m + c.InstitutionLevels["police"] * 6m, "Finite bureaucracy prevents maximizing every institution and daily spending matches staffed levels");
        string digest = game.CanonicalDigest();
        Check(!game.SetInstitutionLevel("education", 6).Success && digest == game.CanonicalDigest(), "Institution level cap cannot be bypassed");
        var police = SimulationEngine.NewGame("QNG"); var withoutPolice = SimulationEngine.LoadJson(police.SaveJson());
        Check(withoutPolice.SetInstitutionLevel("police", 0).Success, "Existing police service can be reduced");
        police.Player.Unrest = withoutPolice.Player.Unrest = 20m;
        police.TickPolitics(police.Player); withoutPolice.TickPolitics(withoutPolice.Player);
        Check(police.Player.Unrest < withoutPolice.Player.Unrest, "Funded police have a real unrest effect");
    }

    public static void InvalidPoliticalState()
    {
        string json = JsonSerializer.Serialize(SimulationEngine.NewGame("QNG").Player);
        void Invalid(Action<CountryState> alter)
        {
            var c = JsonSerializer.Deserialize<CountryState>(json)!; alter(c);
            try { SimulationEngine.ValidatePolitics(c); } catch (InvalidDataException) { return; }
            throw new Exception("Political validation accepted corrupt state.");
        }
        Invalid(c => c.Laws["economy"] = "serfdom");
        Invalid(c => c.Reforms.Add("primary_schools"));
        Invalid(c => c.GovernmentGroups.Add("landowners"));
        Invalid(c => c.InterestGroups[0].Clout += 1m);
        Invalid(c => c.InterestGroups[0].Approval = 21m);
        Invalid(c => c.PoliticalRandomState = 0);
        Invalid(c => c.BureaucracyUsed = -1);
        Invalid(c => c.InstitutionSpending = -1);
        Invalid(c => c.InstitutionLevels["education"] = 1);
        Invalid(c => c.LawEnactment = new() { LawId = "public_schools", Stage = 4, StageDays = 30 });
        Invalid(c => c.LawEnactment = new() { LawId = "public_schools", DaysInStage = 30, StageDays = 30 });
    }

    private static SimulationEngine SchoolBill()
    {
        var game = SimulationEngine.NewGame("QNG");
        Check(game.SetGovernment(new[] { "landowners", "armed_forces", "devout", "intelligentsia" }).Success, "Test cabinet can support education reform");
        Check(game.StartLawEnactment("public_schools").Success, "Test school bill starts with adequate real clout");
        return game;
    }
    private static uint SeedFor(LawSupport support, bool advance)
    {
        for (uint seed = 1; seed < 1_000_000; seed++)
        {
            uint x = seed; bool match = true;
            for (int i = 0; i < 3; i++)
            {
                x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                decimal roll = x / 4294967296m * 100m;
                match &= advance ? roll < support.AdvanceChance : roll >= support.AdvanceChance && roll < support.AdvanceChance + support.SetbackChance;
            }
            if (match) return seed;
        }
        throw new Exception("Could not construct deterministic stage fixture.");
    }
    private static InterestGroupState Group(CountryState c, string id) => c.InterestGroups.Single(g => g.Id == id);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
