using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    // An unmodified opening scenario provides the comparison even after loading a save.
    // It is never advanced and never replaces the player's state.
    private CountryState? _qingOpeningReference;

    private void QingOverview()
    {
        var country = _engine.Player;
        _qingOpeningReference ??= SimulationEngine.NewGame("QNG").Player;
        var opening = _qingOpeningReference;
        var emperor = HistoricalLeaders.Get(country.Id, _engine.State.Date);

        var court = Panel(_right, new Color("ded3b8"), new Color("aa9060"), 12);
        var courtBox = VBox(court, 4);
        var portraitRow = Row(courtBox, 14);
        portraitRow.AddChild(LeaderPortrait("QNG", 138, 188));
        var courtIdentity = VBox(portraitRow, 6); courtIdentity.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        courtIdentity.AddChild(L("大清朝廷", 23, Ink, true));
        courtIdentity.AddChild(Para(Localization.Tr(emperor.Name), 19, Gold));
        courtIdentity.AddChild(Para(Localization.Date(_engine.State.Date), 12, Muted));
        courtIdentity.AddChild(Para("发展生产 · 富国裕民", 12, Muted));
        var courtLinks = Row(courtBox, 6);
        QingCompactButton(courtLinks, "觐见皇帝", () => OpenLeader("QNG"));
        QingCompactButton(courtLinks, "大清舆图", () => ExploreCountry("QNG"));

        Section(_right, "国计民生", true);
        Pair(_right, "国库 / 公债", Money(country.Treasury) + " / " + Money(country.Debt), true);
        Pair(_right, "每日收支", QingSignedMoney(country.DailyBalance), true,
            country.DailyBalance < 0 ? new Color("a74733") : new Color("426448"));
        Pair(_right, "人口 / 城镇化", Compact(country.Population) + $" / {country.Urbanization:0.0}%", true);
        Pair(_right, "城镇就业 / 劳动力", Compact(country.Cities.Sum(c => c.Employed)) + " / " + Compact(country.Cities.Sum(c => c.Workforce)), true);
        _right.AddChild(Para($"1836 开局：人口 {Compact(opening.Population)}，识字率 {opening.Literacy:0.0}%。以下指标随本局模拟更新。", 11, new Color("726954")));

        Section(_right, "三项施政方向", true);
        QingTradePriority(country, opening);
        QingIndustryPriority(country, opening);
        QingEducationPriority(country, opening);

        ShowQueue();
        Rule(_right, true);
        _right.AddChild(Para($"当前大清收录 {country.Cities.Count} 座城市、{country.Regions.Count} 个场景地区。人口、行业等级与财政均为游戏估计；改革和建设是玩家的模拟选择。", 11, new Color("746c59")));
    }

    private void QingTradePriority(CountryState country, CountryState opening)
    {
        var box = QingPriorityCard("一", "广州商贸与粮食供应");
        decimal grainBalance = country.Production.GetValueOrDefault("grain") - country.Consumption.GetValueOrDefault("grain");
        Pair(box, "粮食价格", $"£{country.Prices.GetValueOrDefault("grain"):0.00}", true);
        box.AddChild(Para($"开局 £{opening.Prices.GetValueOrDefault("grain"):0.00}；当前贸易前每日余缺 {grainBalance:+0.0;−0.0;0.0}。", 11, new Color("70674f")));
        box.AddChild(Para("结合市场买卖订单与民生需求安排贸易。广州使用本地价格，当前贸易政策仍按全国设置。", 12, Ink));
        var row = Row(box, 6);
        QingCompactButton(row, "查看广州", () => SelectCity("QNG_canton"));
        QingCompactButton(row, "管理全国市场", () => QingOpenTab("Markets"));
    }

    private void QingIndustryPriority(CountryState country, CountryState opening)
    {
        var city = country.Cities.First(c => c.Id == "QNG_suzhou");
        var openingCity = opening.Cities.First(c => c.Id == city.Id);
        var industry = Catalog.Industries.First(i => i.Id == "textile");
        int pending = country.Construction.Count(c => c.CityId == city.Id && c.IndustryId == industry.Id);
        var box = QingPriorityCard("二", "苏州织造与城镇就业");
        Pair(box, "纺织厂等级", $"{openingCity.Industries.GetValueOrDefault(industry.Id)} → {city.Industries.GetValueOrDefault(industry.Id)}", true);
        box.AddChild(Para($"左为开局，右为当前；待建 {pending} 级。苏州待业 {Compact(city.Unemployed)} 人。", 11, new Color("70674f")));
        box.AddChild(Para($"扩建一级需要 {ConstructionCatalog.Costs[industry.Id]:0} 建造点，使用全国建造力；材料与工资随施工持续结算。投产还需工具供给。", 12, Ink));
        var row = Row(box, 6);
        QingCompactButton(row, "进入苏州", () => OpenCityDetail(city.Id));
        var build = QingCompactButton(row, "扩建纺织厂", () => Act(() => _engine.BuildInCity(city.Id, industry.Id)), true);
        build.TooltipText = "在苏州扩建一级纺织厂，加入全国队列；排队不预扣整笔工程款。";
        build.Disabled = _engine.State.Finished;
    }

    private void QingEducationPriority(CountryState country, CountryState opening)
    {
        var reform = Catalog.Reforms.First(r => r.Id == "primary_schools");
        bool enacted = country.Reforms.Contains(reform.Id);
        var box = QingPriorityCard("三", "兴办初等教育");
        Pair(box, "识字率", $"{opening.Literacy:0.0}% → {country.Literacy:0.0}%", true);
        box.AddChild(Para(enacted ? "初等教育已经施行；识字率将随时间逐步变化。" : "先在朝廷争取支持，提出学校法案；经三阶段审议通过后，按教育机构等级持续支付经费。", 12, Ink));
        var row = Row(box, 6);
        QingCompactButton(row, "查看人口", () => QingOpenTab("Population"));
        var enact = QingCompactButton(row, enacted ? "已施行" : "审议教育制度", () => { _politicalView = "laws"; _politicalLawGroup = "education"; QingOpenTab("Politics"); }, !enacted);
        enact.Disabled = enacted || _engine.State.Finished;
    }

    private VBoxContainer QingPriorityCard(string number, string title)
    {
        var card = Panel(_right, new Color("e6decc"), new Color("c7b99a"), 10);
        var box = VBox(card, 5);
        box.AddChild(L(number + " · " + title, 17, Ink, true));
        return box;
    }

    private Button QingCompactButton(Node parent, string caption, Action action, bool primary = false)
    {
        var button = B(caption, action, primary, true);
        button.CustomMinimumSize = new Vector2(0, 34);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        button.AddThemeFontSizeOverride("font_size", 14);
        parent.AddChild(button);
        return button;
    }

    private void QingOpenTab(string tab)
    {
        _tab = tab;
        _rightScroll.ScrollVertical = 0;
        RefreshPanels();
    }

    private string QingSignedMoney(decimal amount) => (amount > 0 ? "+" : "") + Money(amount);
}
