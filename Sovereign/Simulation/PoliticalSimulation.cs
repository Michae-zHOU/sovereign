using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sovereign.Simulation;

// All political weights, institutions and enactment probabilities are original prototype balance.
// They are not historical census figures or copied game parameters.
public static class PoliticalCatalog
{
    public static IReadOnlyList<InterestGroupDefinition> InterestGroups { get; } = new[]
    {
        new InterestGroupDefinition("landowners", "地主", "土地所有者与传统地方权力"),
        new InterestGroupDefinition("industrialists", "工业家", "资本所有者与工业投资者"),
        new InterestGroupDefinition("rural_folk", "乡村民众", "农民、小农与乡村劳动者"),
        new InterestGroupDefinition("intelligentsia", "知识分子", "识字阶层、教育者与改革倡议者"),
        new InterestGroupDefinition("devout", "教会与宗教人士", "宗教组织与传统社会服务网络"),
        new InterestGroupDefinition("armed_forces", "军队", "军官、军人及其社会支持者"),
        new InterestGroupDefinition("petite_bourgeoisie", "小市民", "店主、工匠与城市中产"),
        new InterestGroupDefinition("trade_unions", "工会", "工业劳动者的组织与政治诉求")
    };
    public static IReadOnlyList<LawGroupDefinition> LawGroups { get; } = new[]
    {
        new LawGroupDefinition("economy", "经济制度"), new LawGroupDefinition("land", "土地制度"),
        new LawGroupDefinition("education", "教育制度"), new LawGroupDefinition("police", "警务制度"),
        new LawGroupDefinition("labor", "劳动权益"), new LawGroupDefinition("health", "医疗制度"),
        new LawGroupDefinition("governance", "统治原则"), new LawGroupDefinition("voting", "投票权")
    };
    public static IReadOnlyList<PoliticalLawDefinition> Laws { get; } = new[]
    {
        Law("traditionalism", "economy", "传统经济", "盈利分红的10%进入投资；传统精英保有较强影响。", new[] {2,-2,1,-1,1,0,0,-1}),
        Law("interventionism", "economy", "国家干预", "盈利分红的25%进入投资；工业家、小市民与改革者倾向支持。", new[] {-1,1,0,1,0,0,1,1}),
        Law("laissez_faire", "economy", "自由放任", "盈利分红的45%进入投资；强化资本政治力量，劳动组织倾向反对。", new[] {-2,2,-1,1,-1,0,1,-2}),
        Law("serfdom", "land", "农奴制", "地主政治力量×4；农民政治力量×0.5。", new[] {2,-1,-2,-2,1,0,0,-2}),
        Law("tenant_farming", "land", "佃农制", "地主政治力量×2；农民政治力量×0.8。", new[] {1,1,0,-1,0,0,1,-1}),
        Law("homesteading", "land", "自耕农制", "地主失去土地权力加成，农民政治力量×1.5。", new[] {-2,0,2,2,-1,0,1,2}),
        Law("no_schools", "education", "无公共学校", "不设教育机构；保留人口的基础识字增长。", new[] {1,0,-1,-2,0,0,0,-1}),
        Law("religious_schools", "education", "宗教学校", "开放教育机构，最高3级；教育强度为公共学校的50%，增强宗教人士力量。", new[] {0,0,1,-1,2,0,1,1}, "education", 3, .5m, "primary_schools"),
        Law("public_schools", "education", "公共学校", "开放最高5级教育机构；提高人口识字与教育机会。", new[] {-1,1,1,2,-2,0,1,2}, "education", 5, 1m, "primary_schools"),
        Law("no_police", "police", "无专门警务", "不设警务机构；不获得警务带来的每日骚乱抑制。", new[] {-1,-1,1,1,0,-2,0,1}),
        Law("local_police", "police", "地方警务", "开放最高3级警务机构；较低警务强度，并增强地主力量。", new[] {2,0,0,-1,1,1,1,-1}, "police", 3, .5m),
        Law("dedicated_police", "police", "专职警察", "开放最高5级警务机构；每日降低骚乱，需持续官僚与财政支持。", new[] {-1,2,-1,0,0,2,2,-1}, "police", 5, 1m),
        Law("no_labor_rights", "labor", "无劳动保障", "没有劳动保障机构，工会倾向反对。", new[] {1,2,-1,-1,0,0,0,-2}),
        Law("child_labor_limits", "labor", "限制童工", "开放最高3级劳动机构；以较弱的劳动保障强度改善劳动者生活。", new[] {0,-1,1,1,1,0,1,1}, "labor", 3, .5m, "labor_protection"),
        Law("worker_protection", "labor", "劳动保护", "开放最高5级劳动机构；改善生活与不满，安全规程降低部分即时产出。", new[] {-1,-2,1,2,1,0,0,2}, "labor", 5, 1m, "labor_protection"),
        Law("no_healthcare", "health", "无公共医疗", "不设医疗机构；不获得公共卫生增长与生活改善。", new[] {1,1,0,-1,0,0,0,-1}),
        Law("charity_hospitals", "health", "慈善医院", "开放最高3级医疗机构，公共医疗50%的效果；宗教人士倾向支持。", new[] {0,0,1,0,2,0,1,1}, "health", 3, .5m, "public_health"),
        Law("public_healthcare", "health", "公共医疗", "开放最高5级医疗机构；改善人口健康、生活与增长。", new[] {-1,-1,2,2,-1,1,0,2}, "health", 5, 1m, "public_health"),
        Law("autocracy", "governance", "君主专制", "地主政治力量与执政稳定加成；政府必须包含地主或军队。", new[] {2,0,-1,-2,1,2,0,-2}),
        Law("constitutional_monarchy", "governance", "君主立宪", "执政合法性更依赖集团代表性与联合政府的一致程度。", new[] {-1,2,0,1,0,0,2,0}),
        Law("presidential_republic", "governance", "总统共和", "扩大非传统精英的执政空间；合法性由代表性与支持度决定。", new[] {-2,1,1,2,-1,-1,1,2}),
        Law("no_voting", "voting", "无选举", "政治力量主要由财富及社会身份决定。", new[] {2,0,-2,-2,1,2,0,-2}),
        Law("landed_voting", "voting", "地产投票", "地主额外获得政治力量；拥有地产的集团偏好此制度。", new[] {2,1,-1,-1,0,1,0,-1}),
        Law("wealth_voting", "voting", "财产投票", "财富达到15级的群体获得更多政治力量；低财富群体受限。", new[] {-1,2,-1,1,0,0,2,-1}),
        Law("universal_voting", "voting", "普遍投票", "政治力量更重视人口，降低财富对政治力量的放大。", new[] {-2,-1,2,2,0,-1,1,2})
    };
    public static IReadOnlyList<InstitutionDefinition> InstitutionDefinitions { get; } = new[]
    {
        new InstitutionDefinition("education", "教育", 20, 12m),
        new InstitutionDefinition("police", "警务", 20, 6m),
        new InstitutionDefinition("labor", "劳动监察", 20, 8m),
        new InstitutionDefinition("health", "医疗", 20, 16m)
    };
    public static IReadOnlyDictionary<string, string> LegacyReformLawMap { get; } = new Dictionary<string, string>
    { ["primary_schools"] = "public_schools", ["labor_protection"] = "worker_protection", ["public_health"] = "public_healthcare" };
    private static PoliticalLawDefinition Law(string id, string group, string name, string description, int[] scores,
        string institution = "", int maximumLevel = 0, decimal strength = 0m, string legacyReform = "") =>
        new(id, group, name, description, InterestGroups.Select((g, i) => (g.Id, Score: scores[i])).ToDictionary(x => x.Id, x => x.Score), institution, maximumLevel, strength, legacyReform);
    public static PoliticalLawDefinition FindLaw(string id) => Laws.Single(l => l.Id == id);
    public static InterestGroupDefinition FindGroup(string id) => InterestGroups.Single(g => g.Id == id);
}

public sealed record InterestGroupDefinition(string Id, string Name, string Description);
public sealed record LawGroupDefinition(string Id, string Name);
public sealed record PoliticalLawDefinition(string Id, string GroupId, string Name, string Description,
    IReadOnlyDictionary<string, int> Preferences, string InstitutionId, int MaximumInstitutionLevel, decimal InstitutionStrength, string LegacyReformId);
public sealed record InstitutionDefinition(string Id, string Name, int BureaucracyPerLevel, decimal DailyCostPerLevel);
public sealed record LawSupport(decimal Support, decimal Opposition, decimal Neutral, decimal AdvanceChance,
    decimal SetbackChance, int StageDays, IReadOnlyList<string> SupportingGroups, IReadOnlyList<string> OpposingGroups);
public sealed class InterestGroupState
{
    public string Id { get; set; } = "";
    public decimal Clout { get; set; }
    public decimal Approval { get; set; }
    public decimal PoliticalPower { get; set; }
}
public sealed class LawEnactmentState
{
    public string LawId { get; set; } = "";
    public int Stage { get; set; } = 1;
    public int DaysInStage { get; set; }
    public int StageDays { get; set; }
    public int Setbacks { get; set; }
    public int StalledDays { get; set; }
    public int Rounds { get; set; }
    public string LastOutcome { get; set; } = "法案已提出，等待审议。";
}
public sealed partial class CountryState
{
    public bool PoliticsInitialized { get; set; }
    public Dictionary<string, string> Laws { get; set; } = new();
    public List<string> GovernmentGroups { get; set; } = new();
    public List<InterestGroupState> InterestGroups { get; set; } = new();
    public LawEnactmentState? LawEnactment { get; set; }
    public Dictionary<string, int> InstitutionLevels { get; set; } = new();
    public uint PoliticalRandomState { get; set; }
    public decimal Legitimacy { get; set; }
    public int BureaucracyCapacity { get; set; }
    public int BureaucracyUsed { get; set; }
    public decimal InstitutionSpending { get; set; }
    public int GovernmentReformCooldown { get; set; }
    public string LastLawOutcome { get; set; } = "尚未提出法案。";
}

public sealed partial class SimulationEngine
{
    public static void InitializePolitics(CountryState c)
    {
        if (c.PoliticsInitialized) return; // Never reset an existing campaign's law or random state.
        c.Laws = new Dictionary<string, string>
        {
            ["economy"] = c.Id is "GBR" or "USA" ? "interventionism" : "traditionalism",
            ["land"] = c.Id is "QNG" or "RUS" or "JAP" ? "serfdom" : "tenant_farming",
            ["education"] = "no_schools", ["police"] = "local_police", ["labor"] = "no_labor_rights",
            ["health"] = "no_healthcare", ["governance"] = c.Id == "USA" ? "presidential_republic" : c.Id == "GBR" ? "constitutional_monarchy" : "autocracy",
            ["voting"] = c.Id is "USA" or "GBR" ? "wealth_voting" : "no_voting"
        };
        c.GovernmentGroups = c.Id is "GBR" or "USA" ? new() { "industrialists", "petite_bourgeoisie", "landowners" }
            : new() { "landowners", "armed_forces", "devout" };
        c.InstitutionLevels = PoliticalCatalog.InstitutionDefinitions.ToDictionary(x => x.Id, x => x.Id == "police" ? 1 : 0);
        // The earlier prototype's earned reforms survive migration; they are not re-enacted or charged again.
        foreach (var reform in c.Reforms.OrderBy(x => x, StringComparer.Ordinal))
            if (PoliticalCatalog.LegacyReformLawMap.TryGetValue(reform, out string? lawId))
            {
                var law = PoliticalCatalog.FindLaw(lawId); c.Laws[law.GroupId] = law.Id;
                c.InstitutionLevels[law.InstitutionId] = 1;
            }
        uint seed = 2166136261u;
        foreach (char ch in c.Id) seed = unchecked((seed ^ ch) * 16777619u);
        c.PoliticalRandomState = seed == 0u ? 1u : seed;
        c.PoliticsInitialized = true;
        RefreshPolitics(c);
    }

    public static decimal GetInvestmentContributionRate(CountryState c) => c.Laws.GetValueOrDefault("economy") switch
    { "laissez_faire" => .45m, "interventionism" => .25m, _ => .10m };
    public static decimal GetInstitutionEffect(CountryState c, string institutionId)
    {
        var law = PoliticalCatalog.Laws.FirstOrDefault(l => l.InstitutionId == institutionId && c.Laws.GetValueOrDefault(l.GroupId) == l.Id);
        if (law == null) return 0m;
        decimal efficiency = c.BureaucracyUsed > 0 ? Math.Min(1m, c.BureaucracyCapacity / (decimal)c.BureaucracyUsed) : 1m;
        return c.InstitutionLevels.GetValueOrDefault(institutionId) * law.InstitutionStrength * efficiency;
    }
    public static decimal GetEducationEffect(CountryState c) => GetInstitutionEffect(c, "education");
    public static decimal GetHealthcareEffect(CountryState c) => GetInstitutionEffect(c, "health");
    public static decimal GetLaborEffect(CountryState c) => GetInstitutionEffect(c, "labor");

    private static List<InterestGroupState> CalculateGroups(CountryState c)
    {
        var powers = new decimal[8]; var sentiments = new decimal[8]; var people = new decimal[8];
        foreach (var pop in c.Pops.OrderBy(p => p.RegionId, StringComparer.Ordinal).ThenBy(p => p.ProfessionId, StringComparer.Ordinal))
        {
            decimal[] affiliations = pop.ProfessionId switch
            {
                "aristocrats" => new[] { .78m, 0m, 0m, .02m, .10m, .10m, 0m, 0m },
                "capitalists" => new[] { 0m, .78m, 0m, .10m, 0m, .02m, .10m, 0m },
                "shopkeepers" => new[] { 0m, .12m, 0m, .10m, .13m, .05m, .60m, 0m },
                "machinists" => new[] { 0m, .10m, 0m, .18m, 0m, .08m, .20m, .44m },
                "laborers" => new[] { 0m, 0m, .12m, .02m, .12m, .10m, .08m, .56m },
                "unemployed" => new[] { 0m, 0m, .30m, 0m, .20m, 0m, .10m, .40m },
                _ => new[] { .04m, 0m, .62m, .02m, .18m, .04m, .10m, 0m }
            };
            decimal wealth = Clamp(pop.Wealth, 0m, 100m), literacy = Clamp(pop.Literacy, 0m, 100m);
            decimal wealthPower = 1m + wealth * .30m;
            decimal votingPower = c.Laws.GetValueOrDefault("voting") switch
            {
                "universal_voting" => 3m + wealthPower * .2m,
                "wealth_voting" => wealthPower * (wealth >= 15m ? 2m : .25m),
                _ => wealthPower * wealthPower
            };
            for (int i = 0; i < powers.Length; i++)
            {
                decimal share = Math.Max(0, pop.Population) * affiliations[i];
                decimal modifier = i switch
                {
                    0 => c.Laws.GetValueOrDefault("land") switch { "serfdom" => 4m, "tenant_farming" => 2m, _ => 1m },
                    2 => c.Laws.GetValueOrDefault("land") switch { "serfdom" => .5m, "homesteading" => 1.5m, _ => .8m },
                    3 => .25m + literacy / 100m * 1.5m,
                    4 => 1.5m - literacy / 100m * .75m,
                    _ => 1m
                };
                if (i == 0 && c.Laws.GetValueOrDefault("voting") == "landed_voting") modifier *= 2m;
                if (i == 0 && c.Laws.GetValueOrDefault("governance") == "autocracy") modifier *= 1.5m;
                if (i == 0 && c.Laws.GetValueOrDefault("police") == "local_police") modifier *= 1.1m;
                if (i == 1 && c.Laws.GetValueOrDefault("economy") == "laissez_faire") modifier *= 1.25m;
                if (i == 4 && c.Laws.GetValueOrDefault("education") == "religious_schools") modifier *= 1.2m;
                powers[i] += share * votingPower * modifier;
                people[i] += share;
                sentiments[i] += share * (Clamp(pop.Loyalists, 0, 1) - Clamp(pop.Radicals, 0, 1));
            }
        }
        // Only a migration fallback before population cohorts are initialized, never an empirical estimate.
        if (powers.Sum() <= 0m) powers = new[] { 36m, 3m, 18m, 4m, 18m, 12m, 8m, 1m };
        decimal total = powers.Sum();
        var result = new List<InterestGroupState>();
        for (int i = 0; i < powers.Length; i++)
        {
            string id = PoliticalCatalog.InterestGroups[i].Id;
            decimal legalApproval = c.Laws.Values.Sum(l => (decimal)PoliticalCatalog.FindLaw(l).Preferences[id]) * .8m;
            decimal socialApproval = people[i] > 0 ? sentiments[i] / people[i] * 12m : 0m;
            result.Add(new InterestGroupState { Id = id, PoliticalPower = powers[i], Clout = decimal.Round(100m * powers[i] / total, 4),
                Approval = decimal.Round(Clamp(legalApproval + socialApproval + (c.GovernmentGroups.Contains(id) ? 1m : 0m), -20m, 20m), 2) });
        }
        var largest = result.OrderByDescending(g => g.Clout).ThenBy(g => g.Id, StringComparer.Ordinal).First();
        largest.Clout += 100m - result.Sum(g => g.Clout);
        return result;
    }

    private static decimal CalculateLegitimacy(CountryState c, IReadOnlyList<InterestGroupState> groups)
    {
        var government = groups.Where(g => c.GovernmentGroups.Contains(g.Id)).ToArray();
        decimal clout = government.Sum(g => g.Clout);
        decimal approval = clout > 0m ? government.Sum(g => g.Clout * g.Approval) / clout : -20m;
        decimal discord = 0m;
        for (int i = 0; i < government.Length; i++)
            for (int j = i + 1; j < government.Length; j++)
                discord += c.Laws.Values.Average(l => (decimal)Math.Abs(PoliticalCatalog.FindLaw(l).Preferences[government[i].Id] - PoliticalCatalog.FindLaw(l).Preferences[government[j].Id]));
        decimal regime = c.Laws.GetValueOrDefault("governance") == "autocracy" && c.GovernmentGroups.Contains("landowners") ? 8m : 0m;
        return decimal.Round(Clamp(25m + clout * .7m + approval * .8m - discord * 2m + regime, 0m, 100m), 2);
    }

    private static void RefreshPolitics(CountryState c)
    {
        c.InterestGroups = CalculateGroups(c);
        c.Legitimacy = CalculateLegitimacy(c, c.InterestGroups);
        c.BureaucracyCapacity = 80 + (int)(Clamp(c.Literacy, 0m, 100m) * .6m) + Math.Min(20, c.Cities.Count / 5);
        c.BureaucracyUsed = PoliticalCatalog.InstitutionDefinitions.Sum(i => c.InstitutionLevels.GetValueOrDefault(i.Id) * i.BureaucracyPerLevel);
        c.InstitutionSpending = PoliticalCatalog.InstitutionDefinitions.Sum(i => c.InstitutionLevels.GetValueOrDefault(i.Id) * i.DailyCostPerLevel);
    }

    public LawSupport GetLawSupport(string lawId, CountryState? country = null)
    {
        var c = country ?? Player;
        var law = PoliticalCatalog.FindLaw(lawId);
        var current = PoliticalCatalog.FindLaw(c.Laws[law.GroupId]);
        var groups = CalculateGroups(c);
        var supporters = groups.Where(g => law.Preferences[g.Id] > current.Preferences[g.Id]).ToArray();
        var opponents = groups.Where(g => law.Preferences[g.Id] < current.Preferences[g.Id]).ToArray();
        decimal support = supporters.Sum(g => g.Clout), opposition = opponents.Sum(g => g.Clout);
        decimal legitimacy = CalculateLegitimacy(c, groups);
        decimal advance = decimal.Round(Clamp(12m + support * .65m + legitimacy * .2m - opposition * .3m, 5m, 85m), 2);
        decimal setback = decimal.Round(Math.Min(100m - advance, Clamp(15m + opposition * .55m - support * .1m, 5m, 65m)), 2);
        int days = (int)decimal.Ceiling(30m + (100m - legitimacy) * .45m + opposition * .25m);
        return new(support, opposition, 100m - support - opposition, advance, setback, Math.Clamp(days, 30, 100),
            supporters.Select(g => g.Id).ToArray(), opponents.Select(g => g.Id).ToArray());
    }

    public string GetLawEnactmentBlockReason(string lawId, CountryState? country = null)
    {
        var c = country ?? Player;
        if (State.Finished) return "本局已结束。";
        if (!c.PoliticsInitialized) return "政治制度尚未初始化。";
        var law = PoliticalCatalog.Laws.FirstOrDefault(l => l.Id == lawId);
        if (law == null) return "未知法律。";
        if (c.Laws[law.GroupId] == lawId) return "该法律已施行。";
        if (c.LawEnactment != null) return "已有法案审议中；请完成或撤回。";
        var support = GetLawSupport(lawId, c);
        if (CalculateLegitimacy(c, CalculateGroups(c)) < 15m) return "政府合法性低于15，无法启动立法。";
        if (support.Support < 5m) return "支持该变更的政治力量不足5%。";
        if (!support.SupportingGroups.Any(c.GovernmentGroups.Contains)) return "政府中至少需要一个支持该变更的利益集团。";
        if (law.Id == "autocracy" && !c.GovernmentGroups.Any(g => g is "landowners" or "armed_forces")) return "恢复君主专制须将地主或军队纳入政府。";
        if (law.InstitutionId.Length > 0 && c.InstitutionLevels[law.InstitutionId] == 0 && c.BureaucracyUsed + 20 > c.BureaucracyCapacity)
            return "启用第1级机构需要20官僚点；请先调整机构预算。";
        return "";
    }

    public CommandResult StartLawEnactment(string lawId) => StartLawEnactmentFor(Player, lawId);
    public CommandResult StartLawEnactmentFor(CountryState c, string lawId)
    {
        string reason = GetLawEnactmentBlockReason(lawId, c);
        if (reason.Length > 0) return Fail(reason);
        var support = GetLawSupport(lawId, c);
        c.LawEnactment = new LawEnactmentState { LawId = lawId, StageDays = support.StageDays };
        c.LastLawOutcome = "开始审议：" + PoliticalCatalog.FindLaw(lawId).Name;
        if (c.Id == State.PlayerCountryId) AddLog(c.LastLawOutcome + "；通过三阶段后生效。无需一次性购买法令。");
        return Ok("法案已提出。每阶段按支持、反对和合法性判定；三次推进后生效，三次挫折则失败。");
    }
    public CommandResult CancelLawEnactment()
    {
        if (State.Finished) return Fail("本局已结束。");
        if (Player.LawEnactment == null) return Fail("没有正在审议的法案。");
        Player.LastLawOutcome = "已撤回：" + PoliticalCatalog.FindLaw(Player.LawEnactment.LawId).Name;
        Player.LawEnactment = null; AddLog(Player.LastLawOutcome);
        return Ok("法案已撤回；现行法律保持不变，已用审议时间不会返还。");
    }
    public CommandResult SetGovernment(IEnumerable<string> groupIds)
    {
        if (State.Finished) return Fail("本局已结束。");
        if (groupIds == null) return Fail("请选择执政集团。");
        var selected = groupIds.ToList();
        if (selected.Count is < 1 or > 4 || selected.Distinct(StringComparer.Ordinal).Count() != selected.Count || selected.Any(id => !PoliticalCatalog.InterestGroups.Any(g => g.Id == id)))
            return Fail("政府须由1至4个不同的有效利益集团组成。");
        if (Player.GovernmentReformCooldown > 0) return Fail($"组阁调整还需等待{Player.GovernmentReformCooldown}天。");
        if (selected.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(Player.GovernmentGroups.OrderBy(x => x, StringComparer.Ordinal))) return Fail("执政集团没有变化。");
        if (Player.Laws["governance"] == "autocracy" && !selected.Any(g => g is "landowners" or "armed_forces")) return Fail("君主专制政府须包含地主或军队。");
        Player.GovernmentGroups = selected.OrderBy(x => x, StringComparer.Ordinal).ToList();
        Player.GovernmentReformCooldown = 30; RefreshPolitics(Player);
        AddLog("政府改组：" + string.Join("、", selected.Select(id => PoliticalCatalog.FindGroup(id).Name)) + $"；合法性{Player.Legitimacy:0.0}。");
        return Ok("政府已改组。新的集团代表性、满意度和分歧决定合法性；30天后可再次改组。");
    }
    public static decimal GetInstitutionUpgradeCost(CountryState c, string institutionId, int targetLevel)
    {
        int delta = Math.Max(0, targetLevel - c.InstitutionLevels.GetValueOrDefault(institutionId));
        return delta * (200m + Math.Min(800m, c.Population / 1_000_000m * 2m));
    }
    public CommandResult SetInstitutionLevel(string institutionId, int level)
    {
        if (State.Finished) return Fail("本局已结束。");
        var definition = PoliticalCatalog.InstitutionDefinitions.FirstOrDefault(i => i.Id == institutionId);
        if (definition == null || level is < 0 or > 5) return Fail("机构或级别无效。");
        var c = Player;
        var law = PoliticalCatalog.Laws.FirstOrDefault(l => l.InstitutionId == institutionId && c.Laws.GetValueOrDefault(l.GroupId) == l.Id);
        if (level > (law?.MaximumInstitutionLevel ?? 0)) return Fail("当前法律未开放该机构级别。");
        int old = c.InstitutionLevels[institutionId];
        if (old == level) return Fail("机构级别没有变化。");
        int proposed = c.BureaucracyUsed + (level - old) * definition.BureaucracyPerLevel;
        if (proposed > c.BureaucracyCapacity) return Fail($"官僚预算不足：需要{proposed}点，当前容量{c.BureaucracyCapacity}点。");
        decimal cost = GetInstitutionUpgradeCost(c, institutionId, level);
        if (c.Treasury < cost) return Fail($"机构扩编需要£{cost:0}，国库不足。");
        c.Treasury -= cost; c.InstitutionLevels[institutionId] = level;
        RefreshPolitics(c);
        return Ok($"{definition.Name}机构调整为{level}级；已付扩编费£{cost:0}，持续支出£{c.InstitutionSpending:0}/日。缩编不返还费用。");
    }

    public void TickPolitics(CountryState c)
    {
        if (!c.PoliticsInitialized) InitializePolitics(c);
        if (c.GovernmentReformCooldown > 0) c.GovernmentReformCooldown--;
        RefreshPolitics(c);
        c.Unrest = Clamp(c.Unrest - GetInstitutionEffect(c, "police") * .025m, 0m, 100m);
        var enactment = c.LawEnactment;
        if (enactment == null) return;
        var support = GetLawSupport(enactment.LawId, c);
        if (support.Support < 5m || c.Legitimacy < 15m || !support.SupportingGroups.Any(c.GovernmentGroups.Contains)
            || enactment.LawId == "autocracy" && !c.GovernmentGroups.Any(g => g is "landowners" or "armed_forces"))
        {
            enactment.StalledDays++;
            enactment.LastOutcome = $"政治支持不足，审议暂停（{enactment.StalledDays}/90天）。";
            if (enactment.StalledDays >= 90) FinishLaw(c, false, "连续90天缺乏执政支持，法案搁置。");
            return;
        }
        enactment.StalledDays = 0;
        if (++enactment.DaysInStage < enactment.StageDays) return;
        enactment.DaysInStage = 0; enactment.Rounds++;
        decimal roll = NextPoliticalRoll(c);
        if (roll < support.AdvanceChance)
        {
            var target = PoliticalCatalog.FindLaw(enactment.LawId);
            bool missingCapacity = target.InstitutionId.Length > 0 && c.InstitutionLevels[target.InstitutionId] == 0 && c.BureaucracyUsed + 20 > c.BureaucracyCapacity;
            if (enactment.Stage == 3 && missingCapacity)
            { enactment.Setbacks++; enactment.LastOutcome = "执行筹备受挫：第1级机构所需官僚预算不足。"; }
            else if (enactment.Stage == 3) { FinishLaw(c, true, "三阶段审议完成。"); return; }
            else { enactment.Stage++; enactment.LastOutcome = "审议推进，进入第" + enactment.Stage + "阶段。"; }
        }
        else if (roll < support.AdvanceChance + support.SetbackChance)
        { enactment.Setbacks++; enactment.LastOutcome = $"审议受挫（{enactment.Setbacks}/3）；下一轮继续。"; }
        else enactment.LastOutcome = "辩论未决：未推进也未增加挫折，下一轮继续。";
        if (enactment.Setbacks >= 3) { FinishLaw(c, false, "累计三次挫折，法案失败。"); return; }
        if (enactment.Rounds >= 18) { FinishLaw(c, false, "十八轮审议仍未通过，法案到期。"); return; }
        enactment.StageDays = support.StageDays;
        c.LastLawOutcome = PoliticalCatalog.FindLaw(enactment.LawId).Name + "：" + enactment.LastOutcome;
        if (c.Id == State.PlayerCountryId) AddLog(c.LastLawOutcome);
    }
    private static decimal NextPoliticalRoll(CountryState c)
    {
        uint x = c.PoliticalRandomState;
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        c.PoliticalRandomState = x;
        return x / 4294967296m * 100m;
    }
    private void FinishLaw(CountryState c, bool passed, string reason)
    {
        var law = PoliticalCatalog.FindLaw(c.LawEnactment!.LawId);
        if (passed)
        {
            var old = PoliticalCatalog.FindLaw(c.Laws[law.GroupId]);
            c.Laws[law.GroupId] = law.Id;
            if (old.LegacyReformId.Length > 0) c.Reforms.Remove(old.LegacyReformId);
            if (old.InstitutionId.Length > 0 && law.InstitutionId.Length == 0) c.InstitutionLevels[old.InstitutionId] = 0;
            if (law.InstitutionId.Length > 0)
                c.InstitutionLevels[law.InstitutionId] = Math.Clamp(c.InstitutionLevels[law.InstitutionId], 1, law.MaximumInstitutionLevel);
            if (law.LegacyReformId.Length > 0) c.Reforms.Add(law.LegacyReformId);
            RefreshPolitics(c);
        }
        c.LastLawOutcome = law.Name + (passed ? "已通过。" : "未通过。") + reason;
        c.LawEnactment = null;
        if (c.Id == State.PlayerCountryId) AddLog(c.LastLawOutcome);
    }

    public static void ValidatePolitics(CountryState c)
    {
        static void Ensure(bool condition, string message) { if (!condition) throw new InvalidDataException("政治存档无效：" + message); }
        Ensure(c.PoliticsInitialized, "政治制度未初始化。");
        Ensure(c.Laws != null && c.Laws.Count == PoliticalCatalog.LawGroups.Count && PoliticalCatalog.LawGroups.All(g => c.Laws.TryGetValue(g.Id, out string? id) && PoliticalCatalog.Laws.Any(l => l.Id == id && l.GroupId == g.Id)), "法律ID或互斥组错误。");
        Ensure(c.Reforms != null && c.Reforms.SetEquals(c.Laws!.Values.Select(PoliticalCatalog.FindLaw).Where(l => l.LegacyReformId.Length > 0).Select(l => l.LegacyReformId)), "旧改革标记与现行法律不一致。");
        Ensure(c.GovernmentGroups != null && c.GovernmentGroups.Count is >= 1 and <= 4 && c.GovernmentGroups.Distinct(StringComparer.Ordinal).Count() == c.GovernmentGroups.Count && c.GovernmentGroups.All(id => PoliticalCatalog.InterestGroups.Any(g => g.Id == id)), "执政集团无效或重复。");
        Ensure(c.InterestGroups != null && c.InterestGroups.Count == 8 && c.InterestGroups.All(g => g != null) && c.InterestGroups.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() == 8 && c.InterestGroups.All(g => PoliticalCatalog.InterestGroups.Any(d => d.Id == g.Id) && g.Clout is >= 0m and <= 100m && g.Approval is >= -20m and <= 20m && g.PoliticalPower >= 0m) && c.InterestGroups.Sum(g => g.Clout) == 100m, "政治力量或满意度无效。");
        Ensure(c.Legitimacy is >= 0m and <= 100m && c.PoliticalRandomState != 0u && c.GovernmentReformCooldown is >= 0 and <= 30, "合法性、随机状态或冷却无效。");
        Ensure(c.InstitutionLevels != null && c.InstitutionLevels.Count == 4 && PoliticalCatalog.InstitutionDefinitions.All(i => c.InstitutionLevels.TryGetValue(i.Id, out int level) && level is >= 0 and <= 5), "机构ID或级别错误。");
        foreach (var institution in PoliticalCatalog.InstitutionDefinitions)
        {
            var law = PoliticalCatalog.Laws.FirstOrDefault(l => l.InstitutionId == institution.Id && c.Laws![l.GroupId] == l.Id);
            Ensure(c.InstitutionLevels![institution.Id] <= (law?.MaximumInstitutionLevel ?? 0), "机构级别超过现行法律许可。");
        }
        Ensure(c.BureaucracyCapacity is >= 80 and <= 160 && c.BureaucracyUsed >= 0 && c.BureaucracyUsed <= c.BureaucracyCapacity && c.InstitutionSpending >= 0m, "官僚预算或机构支出无效。");
        Ensure(c.BureaucracyUsed == PoliticalCatalog.InstitutionDefinitions.Sum(i => c.InstitutionLevels![i.Id] * i.BureaucracyPerLevel) && c.InstitutionSpending == PoliticalCatalog.InstitutionDefinitions.Sum(i => c.InstitutionLevels![i.Id] * i.DailyCostPerLevel), "机构预算与级别不一致。");
        Ensure(c.LastLawOutcome != null && c.LastLawOutcome.Length <= 1000, "法律结果文字无效。");
        Ensure(c.Laws!["governance"] != "autocracy" || c.GovernmentGroups!.Any(g => g is "landowners" or "armed_forces"), "君主专制政府缺乏必要执政集团。");
        if (c.LawEnactment is { } e)
        {
            var law = PoliticalCatalog.Laws.FirstOrDefault(l => l.Id == e.LawId);
            Ensure(law != null && c.Laws![law.GroupId] != e.LawId, "审议的法律无效或已生效。");
            Ensure(e.Stage is >= 1 and <= 3 && e.StageDays is >= 30 and <= 100 && e.DaysInStage >= 0 && e.DaysInStage < e.StageDays && e.Setbacks is >= 0 and < 3 && e.StalledDays is >= 0 and < 90 && e.Rounds is >= 0 and < 18 && e.Stage - 1 + e.Setbacks <= e.Rounds && e.LastOutcome != null && e.LastOutcome.Length <= 1000, "审议阶段、时长或挫折记录无效。");
        }
    }
}
