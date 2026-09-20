using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private string _politicalView = "overview", _politicalLawGroup = "economy";
    private HashSet<string>? _governmentDraft;
    private SimulationEngine? _politicsDraftEngine;
    private string _politicsDraftCountryId = "";

    private void PoliticalMechanicsPanel(VBoxContainer box)
    {
        var c = _engine.Player;
        if (!ReferenceEquals(_politicsDraftEngine, _engine) || _politicsDraftCountryId != c.Id)
        {
            _governmentDraft = null; _politicsDraftEngine = _engine; _politicsDraftCountryId = c.Id;
        }
        if (_politicalView == "overview")
        {
            var heading = Row(box, 12);
            var title = L("朝政概览", 26, Gold, true); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; heading.AddChild(title);
            heading.AddChild(L(Localization.Date(_engine.State.Date), 14, Muted));
        }
        else IllustratedHeader(box, "politics", "政府与立法", Localization.Tr(c.Name) + " · 权力、制度与改革", 176);
        var status = CabinetPanel(box, "cabinet-row", 9);
        EconomyFigures(status, ("合法性", $"{c.Legitimacy:0.0} / 100", c.Legitimacy >= 50 ? Green : Gold),
            ("官僚预算", $"{c.BureaucracyUsed} / {c.BureaucracyCapacity}", Cream),
            ("机构支出 / 日", Money(c.InstitutionSpending), Cream));
        status.TooltipText = "人口财富、职业与法律决定政治力量；这些权重与制度分类是原创玩法简化。";
        var navigation = Row(box, 5);
        foreach (var (id, label) in new[] { ("overview", "概览"), ("laws", "法律"), ("government", "组阁与集团"), ("institutions", "机构"), ("budget", "财政") })
        {
            string tab = id; var button = B(label, () => { _politicalView = tab; RefreshPanels(); }, _politicalView == id);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; navigation.AddChild(button);
        }
        if (_politicalView == "overview") { PoliticalOverviewPanel(box); return; }
        if (c.LawEnactment is { } enactment) PoliticalEnactmentPanel(box, enactment);
        else box.AddChild(Para(c.LastLawOutcome, 14, Muted));
        Rule(box);
        if (_politicalView == "government") PoliticalGovernmentPanel(box);
        else if (_politicalView == "institutions") PoliticalInstitutionsPanel(box);
        else if (_politicalView == "budget") PoliticalBudgetPanel(box);
        else PoliticalLawsPanel(box);
    }

    private void PoliticalOverviewPanel(VBoxContainer box)
    {
        var c = _engine.Player;
        Button PageButton(string text, string page)
        {
            var button = B(text, () => { _politicalView = page; _rightScroll.ScrollVertical = 0; RefreshPanels(); });
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.SetMeta("focus_key", "politics:overview:" + page);
            return button;
        }

        var columns = Row(box, 12); columns.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var leaderCard = CabinetPanel(columns, "cabinet-row", 12);
        leaderCard.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; leaderCard.SizeFlagsStretchRatio = 1;
        var leaderBox = VBox(leaderCard, 8);
        leaderBox.AddChild(L("国家领袖", 20, Gold, true));
        var portrait = LeaderPortrait(c.Id, 220, 300);
        portrait.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter; leaderBox.AddChild(portrait);
        var leader = HistoricalLeaders.Get(c.Id, _engine.State.Date);
        var leaderName = Para(leader.Name, 24, Cream); leaderName.HorizontalAlignment = HorizontalAlignment.Center; leaderBox.AddChild(leaderName);
        var leaderTitle = Para(leader.Title, 15, Gold); leaderTitle.HorizontalAlignment = HorizontalAlignment.Center; leaderBox.AddChild(leaderTitle);
        if (!string.IsNullOrWhiteSpace(leader.SecondaryOffice)) leaderBox.AddChild(Para(leader.SecondaryOffice, 12, Muted));
        leaderBox.AddChild(Para("现行政体 · " + PoliticalCatalog.FindLaw(c.Laws["governance"]).Name, 14, Muted));
        var inspect = B("查看可动三维人物", () => OpenLeader(c.Id), true);
        inspect.SetMeta("focus_key", "politics:overview:leader:" + c.Id); leaderBox.AddChild(inspect);

        var groupsCard = CabinetPanel(columns, "cabinet-row", 12);
        groupsCard.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; groupsCard.SizeFlagsStretchRatio = 1.65f;
        var groupsBox = VBox(groupsCard, 8);
        groupsBox.AddChild(L("利益集团", 20, Gold, true));
        groupsBox.AddChild(Para("执政联盟 · " + PoliticalGroupNames(c.GovernmentGroups), 13, Muted));
        var groups = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        groups.AddThemeConstantOverride("h_separation", 8); groups.AddThemeConstantOverride("v_separation", 8); groupsBox.AddChild(groups);
        foreach (var definition in PoliticalCatalog.InterestGroups)
        {
            var group = c.InterestGroups.Single(state => state.Id == definition.Id);
            bool governing = c.GovernmentGroups.Contains(group.Id);
            var card = CabinetPanel(groups, "cabinet-row", 8); card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var content = VBox(card, 4);
            var identity = Row(content, 6);
            var symbol = PoliticalGroupPicture(group.Id); symbol.CustomMinimumSize = new Vector2(44, 44); identity.AddChild(symbol);
            var text = VBox(identity, 2); text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var name = L(definition.Name, 15, Cream, true); name.ClipText = true; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            text.AddChild(name);
            text.AddChild(L(governing ? "执政" : "在野", 12, governing ? Green : Muted));
            card.TooltipText = definition.Name + " · " + definition.Description;
            Pair(content, "政治力量", $"{group.Clout:0.0}%");
            Meter(content, (double)group.Clout, governing ? Gold : Muted);
            Pair(content, "满意度", $"{group.Approval:+0.0;-0.0;0.0}", valueColor: group.Approval >= 0 ? Green : Red);
        }
        groupsBox.AddChild(PageButton("管理执政联盟", "government"));

        var stateCard = CabinetPanel(columns, "cabinet-row", 12);
        stateCard.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; stateCard.SizeFlagsStretchRatio = 1;
        var stateBox = VBox(stateCard, 7);
        stateBox.AddChild(L("民意与政治状态", 20, Gold, true));
        long population = c.Pops.Sum(pop => pop.Population);
        decimal radicalPopulation = c.Pops.Sum(pop => pop.Radicals * pop.Population);
        decimal loyalPopulation = c.Pops.Sum(pop => pop.Loyalists * pop.Population);
        decimal radicalShare = radicalPopulation / Math.Max(1L, population);
        decimal loyalShare = loyalPopulation / Math.Max(1L, population);
        Pair(stateBox, "激进派", $"{radicalShare:0.0%}", valueColor: Red);
        Meter(stateBox, (double)(radicalShare * 100m), Red);
        stateBox.AddChild(Para("约 " + Compact(radicalPopulation) + " 人", 13, Muted));
        Pair(stateBox, "忠诚派", $"{loyalShare:0.0%}", valueColor: Green);
        Meter(stateBox, (double)(loyalShare * 100m), Green);
        stateBox.AddChild(Para("约 " + Compact(loyalPopulation) + " 人 · 按人口群体人数加权", 13, Muted));
        Rule(stateBox);
        stateBox.AddChild(L("法律审议", 18, Gold, true));
        if (c.LawEnactment is { } enactment)
        {
            stateBox.AddChild(Para(PoliticalCatalog.FindLaw(enactment.LawId).Name, 17, Cream));
            stateBox.AddChild(Para($"阶段 {enactment.Stage} / 3 · 挫折 {enactment.Setbacks} / 3", 13, Muted));
            Meter(stateBox, 100d * enactment.DaysInStage / Math.Max(1, enactment.StageDays), Gold);
            stateBox.AddChild(Para($"本轮进度 {enactment.DaysInStage} / {enactment.StageDays} 天", 13, Muted));
        }
        else stateBox.AddChild(Para("当前没有正在审议的法案。", 14, Muted));
        stateBox.AddChild(PageButton("查看法律与改革", "laws"));
        Rule(stateBox);
        stateBox.AddChild(L("公共机构", 18, Gold, true));
        int activeInstitutions = c.InstitutionLevels.Count(entry => entry.Value > 0);
        Pair(stateBox, "已启用", $"{activeInstitutions} / {PoliticalCatalog.InstitutionDefinitions.Count} 项");
        Pair(stateBox, "机构总级数", c.InstitutionLevels.Values.Sum().ToString());
        stateBox.AddChild(PageButton("管理公共机构", "institutions"));
        stateBox.AddChild(PageButton("查看公共财政", "budget"));
    }

    private void PoliticalEnactmentPanel(VBoxContainer box, LawEnactmentState enactment)
    {
        var card = CabinetPanel(box, "cabinet-row", 12); var content = VBox(card, 5);
        var law = PoliticalCatalog.FindLaw(enactment.LawId); var support = _engine.GetLawSupport(law.Id);
        var title = Row(content, 10); var label = L("正在审议 · " + law.Name, 19, Gold, true);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; title.AddChild(label);
        title.AddChild(B("撤回法案", () => Act(_engine.CancelLawEnactment)));
        Pair(content, "阶段", $"{enactment.Stage} / 3　　挫折 {enactment.Setbacks} / 3");
        Pair(content, "本轮进度", $"{enactment.DaysInStage} / {enactment.StageDays}天");
        Meter(content, 100d * enactment.DaysInStage / Math.Max(1, enactment.StageDays), Gold);
        Pair(content, "支持 / 反对", $"{support.Support:0.0}% / {support.Opposition:0.0}%");
        Pair(content, "本轮推进 / 挫折 / 辩论", $"{support.AdvanceChance:0.0}% / {support.SetbackChance:0.0}% / {100m - support.AdvanceChance - support.SetbackChance:0.0}%");
        content.AddChild(Para(enactment.LastOutcome, 14, Cream));
        content.AddChild(Para("每轮结束才判定结果；三次阶段推进后替换现行法律。三次挫折、长期失去执政支持或十八轮未决会使法案失败。存档保留进度与随机状态。", 13, Muted));
    }

    private void PoliticalLawsPanel(VBoxContainer box)
    {
        var c = _engine.Player;
        var categories = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        categories.AddThemeConstantOverride("h_separation", 5); categories.AddThemeConstantOverride("v_separation", 5); box.AddChild(categories);
        foreach (var group in PoliticalCatalog.LawGroups)
        {
            string id = group.Id; var button = B(group.Name, () => { _politicalLawGroup = id; RefreshPanels(); }, _politicalLawGroup == id);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; categories.AddChild(button);
        }
        string groupName = PoliticalCatalog.LawGroups.Single(g => g.Id == _politicalLawGroup).Name;
        box.AddChild(L(groupName + " · 现行：" + PoliticalCatalog.FindLaw(c.Laws[_politicalLawGroup]).Name, 20, Gold, true));
        foreach (var law in PoliticalCatalog.Laws.Where(l => l.GroupId == _politicalLawGroup))
        {
            bool enacted = c.Laws[law.GroupId] == law.Id;
            var card = CabinetPanel(box, "cabinet-row", 12); var content = VBox(card, 5);
            var title = Row(content, 10); var label = L(law.Name + (enacted ? "  ✓ 现行法律" : ""), 18, enacted ? Green : Cream, true);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; title.AddChild(label);
            string id = law.Id;
            string blocked = _engine.GetLawEnactmentBlockReason(id);
            var enact = B(enacted ? "已施行" : "提出法案", () => Act(() => _engine.StartLawEnactment(id)), !enacted);
            enact.Disabled = blocked.Length > 0; enact.TooltipText = blocked; title.AddChild(enact);
            content.AddChild(Para(law.Description, 14, Muted));
            if (!enacted)
            {
                var support = _engine.GetLawSupport(id);
                Pair(content, "支持 / 反对 / 中立", $"{support.Support:0.0}% / {support.Opposition:0.0}% / {support.Neutral:0.0}%");
                Pair(content, "预计每阶段", support.StageDays + "天　（至少三个阶段）");
                content.AddChild(Para("支持：" + PoliticalGroupNames(support.SupportingGroups) + "\n反对：" + PoliticalGroupNames(support.OpposingGroups), 13, Muted));
                if (blocked.Length > 0) content.AddChild(Para(blocked, 13, Gold));
            }
        }
    }

    private void PoliticalGovernmentPanel(VBoxContainer box)
    {
        var c = _engine.Player;
        _governmentDraft ??= new HashSet<string>(c.GovernmentGroups);
        box.AddChild(Para("选择1至4个利益集团组成政府，然后提交改组。政府须包含支持目标法律的集团。君主专制还需要地主或军队参与。", 14, Muted));
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); box.AddChild(grid);
        foreach (var definition in PoliticalCatalog.InterestGroups)
        {
            var group = c.InterestGroups.Single(g => g.Id == definition.Id);
            var card = CabinetPanel(grid, "cabinet-row", 10); card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var content = VBox(card, 4); string id = definition.Id;
            var identity = Row(content, 7); identity.AddChild(PoliticalGroupPicture(id));
            var heading = new CheckBox { Text = definition.Name, ButtonPressed = _governmentDraft.Contains(id),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                TooltipText = definition.Name + " · " + definition.Description };
            heading.AddThemeFontSizeOverride("font_size", 16); heading.AddThemeFontOverride("font", _body);
            heading.AddThemeColorOverride("font_color", Cream);
            heading.Toggled += selected => { if (selected) _governmentDraft!.Add(id); else _governmentDraft!.Remove(id); };
            identity.AddChild(heading);
            Pair(content, "政治力量", $"{group.Clout:0.0}%"); Meter(content, (double)group.Clout, Gold);
            Pair(content, "满意度", $"{group.Approval:+0.0;-0.0;0.0} / 20", valueColor: group.Approval >= 0 ? Green : Red);
            content.AddChild(Para(c.GovernmentGroups.Contains(id) ? "当前执政" : "当前在野", 13, c.GovernmentGroups.Contains(id) ? Green : Muted));
            content.AddChild(Para(definition.Description, 12, Muted));
        }
        var actions = Row(box, 8);
        var apply = B(c.GovernmentReformCooldown > 0 ? $"{c.GovernmentReformCooldown}天后可改组" : "提交政府改组", () =>
        {
            var result = _engine.SetGovernment(_governmentDraft!.ToArray());
            if (result.Success) _governmentDraft = null;
            Act(() => result);
        }, true);
        apply.Disabled = c.GovernmentReformCooldown > 0 || _engine.State.Finished; actions.AddChild(apply);
        actions.AddChild(B("恢复当前名单", () => { _governmentDraft = new HashSet<string>(c.GovernmentGroups); RefreshPanels(); }));
        box.AddChild(Para("合法性由执政集团政治力量、满意度、政体与集团间法律分歧共同计算。人口财富与职业构成会随经济变化而改变政治力量。", 13, Muted));
    }

    private void PoliticalInstitutionsPanel(VBoxContainer box)
    {
        var c = _engine.Player;
        Pair(box, "剩余官僚点", (c.BureaucracyCapacity - c.BureaucracyUsed) + "点");
        box.AddChild(Para("每级占用20官僚点，并持续产生财政支出。提高机构要支付扩编费；缩编释放官僚点但不退款。机构效果随级别提高；0级表示未提供服务。", 14, Muted));
        foreach (var institution in PoliticalCatalog.InstitutionDefinitions)
        {
            string id = institution.Id; int level = c.InstitutionLevels[id];
            var law = PoliticalCatalog.Laws.FirstOrDefault(l => l.InstitutionId == id && c.Laws[l.GroupId] == l.Id);
            int maxLevel = law?.MaximumInstitutionLevel ?? 0;
            var card = CabinetPanel(box, "cabinet-row", 10); var content = VBox(card, 4);
            var title = Row(content, 10); var label = L(institution.Name + $"机构 · {level} / {maxLevel}级", 18, Gold, true);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; title.AddChild(label);
            var minus = B("− 一级", () => Act(() => _engine.SetInstitutionLevel(id, level - 1)));
            minus.Disabled = level == 0 || _engine.State.Finished; title.AddChild(minus);
            decimal cost = SimulationEngine.GetInstitutionUpgradeCost(c, id, level + 1);
            var plus = B("＋ 一级", () => Act(() => _engine.SetInstitutionLevel(id, level + 1)));
            plus.Disabled = level >= maxLevel || c.BureaucracyUsed + institution.BureaucracyPerLevel > c.BureaucracyCapacity || c.Treasury < cost || _engine.State.Finished;
            plus.TooltipText = $"需扩编费{Money(cost)}、{institution.BureaucracyPerLevel}官僚点；日支出增加{Money(institution.DailyCostPerLevel)}。";
            title.AddChild(plus);
            Pair(content, "当前开支 / 官僚点", Money(level * institution.DailyCostPerLevel) + " / 日　·　" + level * institution.BureaucracyPerLevel + "点");
            Pair(content, "有效服务强度", $"{SimulationEngine.GetInstitutionEffect(c, id):0.0}");
            if (law == null) content.AddChild(Para("当前法律尚未开放该机构。请先通过相应法律。", 13, Muted));
            else content.AddChild(Para("法律依据：" + law.Name + $"。扩编一级需{Money(cost)}；官僚容量随识字率与地方行政基础提高。", 13, Muted));
        }
    }
    private void PoliticalBudgetPanel(VBoxContainer box)
    {
        var c = _engine.Player;
        box.AddChild(L("国库与公共财政", 20, Gold, true));
        Pair(box, "国库", Money(c.Treasury));
        Pair(box, "公共债务", Money(c.Debt));
        Pair(box, "家庭税收 / 日", Money(c.TaxRevenue));
        Pair(box, "关税收入 / 日", Money(c.TariffRevenue));
        Pair(box, "政府所有权分红 / 日", Money(c.GovernmentDividends));
        Pair(box, "公共支出 / 日", Money(c.Spending));
        Pair(box, "其中：机构支出 / 日", Money(c.InstitutionSpending));
        Pair(box, "其中：政府建设 / 日", Money(c.ConstructionSpending));
        Pair(box, "每日结余", Money(c.DailyBalance), valueColor: c.DailyBalance >= 0m ? Green : Red);
        Rule(box);
        box.AddChild(L("税收政策", 20, Gold, true));
        Pair(box, "当前税率", c.TaxRate + "%");
        var taxes = Row(box, 5);
        foreach (int rate in new[] { 5, 15, 22, 35, 50 })
        {
            int selected = rate;
            var button = B(rate + "%", () => Act(() => _engine.SetTaxRate(selected)), c.TaxRate == rate);
            button.Disabled = _engine.State.Finished; button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; taxes.AddChild(button);
        }
        box.AddChild(Para("税率会改变家庭可支配收入与购买能力。提高税率可补充公共收入，也可能降低财富并激化不满。", 14, Muted));
        Rule(box);
        box.AddChild(L("私人投资", 20, Gold, true));
        Pair(box, "投资池余额", Money(c.InvestmentPool));
        Pair(box, "盈利分红投入比例", $"{SimulationEngine.GetInvestmentContributionRate(c):0%}");
        Pair(box, "投资池流入 / 日", Money(c.DailyInvestmentContribution));
        Pair(box, "私人建设支出 / 日", Money(c.DailyPrivateConstructionSpending));
        box.AddChild(Para("私人投资池由盈利分红注入，私人建设从该池支付。经济制度决定投入比例；私人资金不会直接计入国库。", 14, Muted));
    }
    private static string PoliticalGroupNames(IEnumerable<string> ids)
    {
        var names = ids.Select(id => PoliticalCatalog.FindGroup(id).Name).ToArray();
        return names.Length == 0 ? "无" : string.Join("、", names);
    }
}
