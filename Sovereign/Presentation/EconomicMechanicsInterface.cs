using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private string _marketPage = "orders", _marketRegion = "", _popRegion = "";
    private string _popView = "details";

    private void EconomyFigures(Control parent, params (string Caption, string Value, Color Color)[] values)
    {
        var row = Row(parent, 8);
        foreach (var value in values)
        {
            var cell = VBox(row, 1); cell.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            cell.AddChild(ConstructionText(value.Caption, 11, Muted, expand: true));
            cell.AddChild(ConstructionText(value.Value, 16, value.Color, expand: true));
            cell.TooltipText = value.Caption + "：" + value.Value;
        }
    }

    private void EconomyRegionSelector(Control parent, CountryState country, string selected, Action<string> choose, bool allRegions)
    {
        var selector = new OptionButton { CustomMinimumSize = new Vector2(0, 34), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        selector.AddThemeFontSizeOverride("font_size", 14);
        selector.AddThemeStyleboxOverride("normal", CabinetSurface("cabinet-row", 6));
        if (allRegions) selector.AddItem("全国人口 · 全部地区");
        foreach (var region in country.Regions) selector.AddItem(Localization.Tr(region.Name));
        int selectedIndex = country.Regions.FindIndex(region => region.Id == selected);
        selector.Selected = allRegions ? selectedIndex + 1 : Math.Max(0, selectedIndex);
        selector.ItemSelected += index =>
        {
            if (country.Id != _engine.Player.Id) return;
            int regionIndex = (int)index - (allRegions ? 1 : 0);
            if (regionIndex < 0) { if (allRegions) choose(""); return; }
            if (regionIndex < country.Regions.Count) choose(country.Regions[regionIndex].Id);
        };
        _atlasSelector = selector; parent.AddChild(selector);
    }

    private void OrderMarketPanel()
    {
        var country = _engine.Player;
        var tabs = Row(_right, 3);
        foreach (var (id, name) in new[] { ("orders", "市场订单"), ("regions", "地区价格与接入") })
        {
            var button = SmallButton(name, () => { _marketPage = id; _rightScroll.ScrollVertical = 0; RefreshPanels(); }, _marketPage == id);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; tabs.AddChild(button);
        }
        if (_marketPage == "regions")
        {
            if (country.Regions.All(r => r.Id != _marketRegion)) _marketRegion = country.Regions[0].Id;
            EconomyRegionSelector(_right, country, _marketRegion, id => { _marketRegion = id; _rightScroll.ScrollVertical = 0; RefreshPanels(); }, false);
            var region = country.Regions.First(r => r.Id == _marketRegion);
            RegionMarketAccess(_right, country, region); LocalMarketRows(_right, country, region); return;
        }
        _right.AddChild(ConstructionText(Localization.Tr(country.Name) + "市场", 21, Cream, true));
        _right.AddChild(Para("价格由买卖订单的差额形成；基础价为比较基准。短缺会降低使用该商品的生产效率。", 12, Muted));
        foreach (string good in Catalog.Goods)
        {
            decimal price = country.Prices.GetValueOrDefault(good, Catalog.BasePrices[good]);
            decimal deviation = price / Catalog.BasePrices[good] - 1m;
            decimal reduction = 1m - SimulationEngine.GetInputAvailability(country, good);
            var panel = CabinetPanel(_right, "cabinet-row", 6); var body = VBox(panel, 5); var heading = Row(body, 7);
            var identity = ConstructionGood(good, Localization.Tr(Title(good))); identity.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; heading.AddChild(identity);
            var quote = ConstructionText($"£{price:0.00}", 19, Gold); quote.CustomMinimumSize = new Vector2(75, 0); quote.HorizontalAlignment = HorizontalAlignment.Right; heading.AddChild(quote);
            var difference = ConstructionText($"{deviation:+0.0%;-0.0%;0.0%}", 13, deviation > 0 ? Red : Green);
            difference.CustomMinimumSize = new Vector2(74, 0); difference.HorizontalAlignment = HorizontalAlignment.Right;
            difference.TooltipText = $"相对基础价格 £{Catalog.BasePrices[good]:0.00}"; heading.AddChild(difference);
            EconomyFigures(body, ("买单 / 日", Compact(country.BuyOrders.GetValueOrDefault(good)), Cream),
                ("卖单 / 日", Compact(country.SellOrders.GetValueOrDefault(good)), Cream),
                ("短缺减产", $"{reduction:0.0%}", reduction > 0 ? Red : Muted));
            var routes = Row(body, 4);
            foreach (var (name, direction) in new[] { ("进口", 1), ("关闭贸易", 0), ("出口", -1) })
            {
                var button = SmallButton(name, () => Act(() => _engine.SetTrade(good, direction)), country.Trade.GetValueOrDefault(good) == direction);
                button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; button.Disabled = _engine.State.Finished;
                button.TooltipText = "调整贸易方向，下一日结算更新订单与贸易额"; routes.AddChild(button);
            }
            body.AddChild(ConstructionText($"进口单 {Compact(country.ImportOrders.GetValueOrDefault(good))}  ·  出口单 {Compact(country.ExportOrders.GetValueOrDefault(good))}", 11, Muted, expand: true));
        }
    }

    private void RegionMarketAccess(Control parent, CountryState country, RegionState region)
    {
        var cities = country.Cities.Where(city => city.RegionId == region.Id).Select(city => city.Id).ToHashSet();
        int pendingRailways = country.Construction.Count(project => project.IndustryId == ConstructionCatalog.RailwayId && cities.Contains(project.CityId));
        var panel = CabinetPanel(parent, "cabinet-row", 7); var body = VBox(panel, 6);
        body.AddChild(ConstructionScene(ConstructionCatalog.RailwayId, 100));
        body.AddChild(ConstructionText(Localization.Tr(region.Name) + " · 市场接入", 18, Cream, true));
        EconomyFigures(body, ("基础设施 / 已用", $"{region.Infrastructure:0.#} / {region.InfrastructureUsage:0.#}", Cream),
            ("市场接入", $"{region.MarketAccess:0.0%}", region.MarketAccess >= 1m ? Green : Gold),
            ("铁路", $"{region.RailwayLevels} 级" + (pendingRailways > 0 ? $" +{pendingRailways} 在建" : ""), Cream));
        Meter(body, (double)(region.MarketAccess * 100m), region.MarketAccess >= 1m ? Green : Gold);
        body.AddChild(ConstructionText($"全国市场价格权重 {country.MarketPriceImpact * region.MarketAccess:0.0%}", 12, Muted, expand: true));
        if (country.Id != _engine.Player.Id) return;
        string unavailable = _engine.State.Finished ? "战役已经结束" : !country.Technologies.Contains("railways") ? "需要先研究铁路" :
            cities.Count == 0 ? "本省尚无可建设城市" : region.RailwayLevels + pendingRailways >= 20 ? "已建及在建铁路达到每地区20级上限" :
            country.Construction.Count >= ConstructionCatalog.MaximumQueueLength ? "全国建造队列已满" : "";
        var railway = SmallButton($"加入铁路建设 · {ConstructionCatalog.Costs[ConstructionCatalog.RailwayId]:0}点", () => Act(() => _engine.ExpandRailway(region.Id)));
        railway.Disabled = unavailable.Length > 0; railway.TooltipText = unavailable.Length > 0 ? unavailable : "在本省城市安排铁路，加入全国政府建造队列；完工后增加20基础设施";
        body.AddChild(railway);
        body.AddChild(Para("铁路与其他建筑共享建造力，施工时支付材料及工资。完工后每级增加20基础设施，并持续支出运营费用。", 11, Muted));
    }

    private void LocalMarketRows(Control parent, CountryState country, RegionState region)
    {
        ProvinceSection(parent, "本地价格");
        parent.AddChild(Para("本地价格综合地区订单与全国市价；基础设施不足会削弱市场接入。", 12, Muted));
        foreach (string good in Catalog.Goods)
        {
            decimal local = region.LocalPrices.GetValueOrDefault(good, Catalog.BasePrices[good]);
            decimal national = country.Prices.GetValueOrDefault(good, Catalog.BasePrices[good]);
            decimal deviation = national > 0m ? local / national - 1m : 0m;
            var panel = CabinetPanel(parent, "cabinet-row", 6); var body = VBox(panel, 4); var row = Row(body, 6);
            var identity = ConstructionGood(good, Localization.Tr(Title(good))); identity.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(identity);
            row.AddChild(ConstructionText($"本地 £{local:0.00}", 16, Gold));
            var delta = ConstructionText($"{deviation:+0.0%;-0.0%;0.0%}", 12, deviation > 0 ? Red : Green);
            delta.CustomMinimumSize = new Vector2(64, 0); delta.HorizontalAlignment = HorizontalAlignment.Right; delta.TooltipText = "相对全国市场价格"; row.AddChild(delta);
            EconomyFigures(body, ("全国市价", $"£{national:0.00}", Cream), ("本地买单", Compact(region.BuyOrders.GetValueOrDefault(good)), Cream),
                ("本地卖单", Compact(region.SellOrders.GetValueOrDefault(good)), Cream));
        }
    }

    private void PopMechanicsPanel()
    {
        var country = _engine.Player;
        if (_popRegion.Length > 0 && country.Regions.All(r => r.Id != _popRegion)) _popRegion = "";
        string scope = _popRegion.Length == 0 ? Localization.Tr(country.Name) : Localization.Tr(country.Regions.First(r => r.Id == _popRegion).Name);
        IllustratedHeader(_right, "population", "人口与阶层", scope + " · 生活、财富与生计", 132);
        var tabs = Row(_right, 3);
        foreach (var (id, name) in new[] { ("overview", "概览"), ("details", "阶层明细") })
        {
            var button = SmallButton(name, () => { _popView = id; _rightScroll.ScrollVertical = 0; RefreshPanels(); }, _popView == id);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            tabs.AddChild(button);
        }
        EconomyRegionSelector(_right, country, _popRegion, id => { _popRegion = id; _rightScroll.ScrollVertical = 0; RefreshPanels(); }, true);
        var pops = country.Pops.Where(p => _popRegion.Length == 0 || p.RegionId == _popRegion).ToArray();
        long population = pops.Sum(p => p.Population);
        var panel = CabinetPanel(_right, "cabinet-row", 7); var body = VBox(panel, 4);
        EconomyFigures(body, ("人口", Compact(population), Cream), ("劳动力", Compact(pops.Sum(p => p.Workforce)), Cream),
            ("平均财富", population > 0 ? (pops.Sum(p => p.Wealth * p.Population) / population).ToString("0.0") : "—", Gold));
        _right.AddChild(Para("各阶层包含就业人口与家属。所得已扣税，农民包含自给产出折值；财富与识字率按人口加权。", 12, Muted));
        if (_popView == "overview") PopStrataOverview(_right, pops);
        else PopRows(_right, pops);
        if (_popRegion.Length > 0) _right.AddChild(SmallButton("查看该地区 →", () => SelectRegion(_popRegion)));
    }

    private void PopStrataOverview(Control parent, PopGroup[] pops)
    {
        long totalPopulation = pops.Sum(p => p.Population);
        decimal fulfilled = totalPopulation > 0 ? pops.Sum(p => p.NeedsFulfilled * p.Population) / totalPopulation : 0m;
        var wellbeing = Row(parent, 6);
        wellbeing.AddChild(ConstructionText("生活状况 · 需求满足", 12, Muted, expand: true));
        wellbeing.AddChild(ConstructionText(totalPopulation > 0 ? fulfilled.ToString("0.0%") : "—", 16, fulfilled >= .8m ? Green : Gold));
        Meter(parent, (double)(fulfilled * 100m), fulfilled >= .8m ? Green : Gold);

        var columns = Row(parent, 6);
        columns.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var strata = new[]
        {
            (Name: "劳动阶层", Portrait: "peasants", Professions: new[] { "peasants", "laborers", "unemployed" }),
            (Name: "中间阶层", Portrait: "machinists", Professions: new[] { "machinists", "shopkeepers" }),
            (Name: "富裕阶层", Portrait: "capitalists", Professions: new[] { "capitalists", "aristocrats" })
        };
        foreach (var stratum in strata)
        {
            var cohort = pops.Where(p => stratum.Professions.Contains(p.ProfessionId)).ToArray();
            long population = cohort.Sum(p => p.Population);
            decimal Mean(Func<PopGroup, decimal> value) => population > 0 ? cohort.Sum(p => value(p) * p.Population) / population : 0m;
            decimal wealth = Mean(p => p.Wealth), needs = Mean(p => p.NeedsFulfilled);
            decimal income = cohort.Sum(p => p.DailyIncome), taxes = cohort.Sum(p => p.DailyTaxes);
            string professions = string.Join("、", stratum.Professions.Select(id => PopulationCatalog.Professions[id]));
            var card = CabinetPanel(columns, "cabinet-row", 6);
            card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            card.SizeFlagsStretchRatio = 1;
            card.ClipContents = true;
            card.TooltipText = $"{stratum.Name}：{professions}\n人口 {population:N0} 人\n人口加权财富 {wealth:0.00} · 需求满足 {needs:0.0%}\n税后日收入 £{income:N2} · 日缴税 £{taxes:N2}";
            var body = VBox(card, 6);
            var title = ConstructionText(stratum.Name, 16, Gold, true, true);
            title.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(title);
            body.AddChild(new TextureRect
            {
                Texture = PopulationPortrait(stratum.Portrait), CustomMinimumSize = new Vector2(0, 136),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            var members = Para(professions, 11, Muted);
            members.HorizontalAlignment = HorizontalAlignment.Center;
            members.CustomMinimumSize = new Vector2(0, 38); body.AddChild(members);
            Figure(body, "人口", Compact(population) + " 人", Cream);
            Figure(body, "平均财富", population > 0 ? wealth.ToString("0.0") : "—", Gold);
            Rule(body);
            Figure(body, "税后收入 / 日", Money(income), Cream);
            Figure(body, "缴税 / 日", Money(taxes), Gold);
            Figure(body, "需求满足", population > 0 ? needs.ToString("0.0%") : "—", needs >= .8m ? Green : Gold);
            Meter(body, (double)(needs * 100m), needs >= .8m ? Green : Gold);
        }
        parent.AddChild(Para("三栏按职业作玩法分类，不对应精确历史阶级，也不按当前财富重新归类。图像为代表职业示意；数值汇总当前筛选人口，财富和需求满足按人口加权。", 11, Muted));

        void Figure(Control column, string caption, string value, Color color)
        {
            var entry = VBox(column, 1);
            var label = ConstructionText(caption, 11, Muted, expand: true);
            label.HorizontalAlignment = HorizontalAlignment.Center; entry.AddChild(label);
            var number = ConstructionText(value, 16, color, expand: true);
            number.HorizontalAlignment = HorizontalAlignment.Center; entry.AddChild(number);
        }
    }

    private void PopRows(Control parent, IEnumerable<PopGroup> source)
    {
        var groups = source.ToArray();
        foreach (var profession in PopulationCatalog.Professions)
        {
            var cohort = groups.Where(p => p.ProfessionId == profession.Key).ToArray();
            long population = cohort.Sum(p => p.Population);
            decimal Mean(Func<PopGroup, decimal> value) => population > 0 ? cohort.Sum(p => value(p) * p.Population) / population : 0m;
            var panel = CabinetPanel(parent, "cabinet-row", 7); var body = VBox(panel, 7); var identity = Row(body, 10);
            var portrait = new PanelContainer
            {
                CustomMinimumSize = new Vector2(80, 96), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin, MouseFilter = Control.MouseFilterEnum.Ignore,
                TooltipText = profession.Value
            };
            portrait.AddThemeStyleboxOverride("panel", Style(new Color("171d20"), new Color("837151"), 0, 2));
            portrait.AddChild(new TextureRect
            {
                Texture = PopulationPortrait(profession.Key), CustomMinimumSize = new Vector2(76, 92),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            identity.AddChild(portrait);
            var summary = VBox(identity, 4); summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            summary.AddChild(ConstructionText(profession.Value, 18, Cream, true, true));
            var count = ConstructionText(Compact(population) + " 人", 20, Gold, expand: true);
            count.TooltipText = profession.Value + $" · {population:N0} 人，包含该阶层人口与家属";
            summary.AddChild(count);
            EconomyFigures(summary, ("财富", population > 0 ? Mean(p => p.Wealth).ToString("0.0") : "—", Gold),
                ("识字率", $"{Mean(p => p.Literacy):0.0}%", Cream), ("激进派", $"{Mean(p => p.Radicals):0.0%}", Red));
            EconomyFigures(body, ("税后收入 / 日", Money(cohort.Sum(p => p.DailyIncome)), Cream),
                ("消费支出 / 日", Money(cohort.Sum(p => p.DailyExpenses)), Cream), ("缴税 / 日", Money(cohort.Sum(p => p.DailyTaxes)), Gold));
            var need = Mean(p => p.NeedsFulfilled);
            Meter(body, (double)(need * 100m), need > .8m ? Green : Gold);
            body.AddChild(Para($"需求满足 {need:0.0%}  ·  忠诚派 {Mean(p => p.Loyalists):0.0%}  ·  劳动力 {Compact(cohort.Sum(p => p.Workforce))}", 11, Muted));
        }
    }

    private void BuildingMechanicsRows(Control parent, CountryState country, CityState city, string industryId)
    {
        if (!city.Buildings.TryGetValue(industryId, out var account)) return;
        var body = VBox(parent, 5); body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var selected = ProductionCatalog.Get(industryId, account.MethodId);
        body.AddChild(ConstructionText("生产方式 · " + selected.Name, 13, Gold, expand: true));
        var methods = Row(body, 3);
        foreach (var method in ProductionCatalog.MethodsFor(industryId))
        {
            bool locked = method.RequiredTechnology.Length > 0 && !country.Technologies.Contains(method.RequiredTechnology);
            string technology = locked ? Localization.Tr(Catalog.Technologies.First(t => t.Id == method.RequiredTechnology).Name) : "";
            var button = SmallButton(method.Name, () => Act(() => _engine.SetProductionMethod(city.Id, industryId, method.Id)), selected.Id == method.Id);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; button.AddThemeFontSizeOverride("font_size", 12);
            button.Disabled = country.Id != _engine.Player.Id || _engine.State.Finished || locked;
            string recipe = "投入 " + (method.Inputs.Count == 0 ? "无" : string.Join("、", method.Inputs.Select(g => Localization.Tr(Title(g.Key)) + " " + g.Value))) +
                "\n产出 " + string.Join("、", method.Outputs.Select(g => Localization.Tr(Title(g.Key)) + " " + g.Value));
            button.TooltipText = (locked ? "需要技术：" + technology + "\n" : "") + recipe + $"\n每级岗位 {method.JobsPerLevel:N0} · 技工占比 {method.SkilledFraction:0%}\n岗位会受城市可用劳工与识字资格限制。";
            methods.AddChild(button);
        }
        var inputs = Row(body, 4);
        inputs.AddChild(ConstructionText("每级配方", 11, Muted));
        foreach (var good in selected.Inputs) inputs.AddChild(ConstructionGood(good.Key, good.Value.ToString("0.#")));
        inputs.AddChild(ConstructionText("→", 12, Gold));
        foreach (var good in selected.Outputs) inputs.AddChild(ConstructionGood(good.Key, good.Value.ToString("0.#")));
        long jobs = (long)city.Industries[industryId] * selected.JobsPerLevel;
        decimal skilledAvailable = Math.Max(0, city.Workforce - city.Artisans) * city.Literacy / 100m;
        decimal skilledDemand = city.Buildings.Sum(b => city.Industries[b.Key] * ProductionCatalog.Get(b.Key, b.Value.MethodId).JobsPerLevel * ProductionCatalog.Get(b.Key, b.Value.MethodId).SkilledFraction);
        EconomyFigures(body, ("就业 / 岗位", $"{Compact(account.Workers)} / {Compact(jobs)}", Cream),
            ("工资 / 日", Money(account.DailyWages), Cream), ("利润 / 日", Money(account.DailyProfit), account.DailyProfit >= 0m ? Green : Red));
        EconomyFigures(body, ("现金储备", Money(account.CashReserves), Gold), ("建筑债务", Money(account.Debt), account.Debt > 0m ? Red : Muted),
            ("分红 / 日", Money(account.DailyDividends), Cream));
        body.AddChild(ConstructionText($"所有权   私有 {account.PrivateLevels} 级  ·  国有 {Math.Max(0, city.Industries[industryId] - account.PrivateLevels)} 级", 12, Muted, expand: true));
        if (skilledDemand > skilledAvailable) body.AddChild(Para($"技工资格不足：全城需求 {Compact(skilledDemand)}，具备资格的劳工约 {Compact(skilledAvailable)}；就业受到限制。", 11, Gold));
        body.AddChild(ConstructionText("收支按最近一日结算；更改配方后下一日更新。", 10, Muted, expand: true));
    }
}
