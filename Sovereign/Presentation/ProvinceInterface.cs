using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private string _provincePage = "overview";

    private RegionState? DrawerProvince => _tab == "Atlas" && _selectedRegion.Length > 0 && _selectedCity.Length == 0
        ? ExploredCountry.Regions.FirstOrDefault(r => r.Id == _selectedRegion) : null;
    private bool IsProvinceDrawer => DrawerProvince != null;
    private string GetProvinceDrawerTitle() => DrawerProvince is RegionState region ? Localization.Tr(region.Name) : "";
    private string GetProvinceDrawerSubtitle() => IsProvinceDrawer ? Localization.Tr(ExploredCountry.Name) + "的地区" : "";
    private string GetProvinceDrawerCountryId() => IsProvinceDrawer ? ExploredCountry.Id : "";

    private Label ProvinceLabel(string text, int size = 14, Color? color = null, bool serif = false)
    {
        var label = L(text, size, color ?? Cream, serif);
        label.AddThemeFontSizeOverride("font_size", size);
        label.ClipText = true; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        return label;
    }

    private PanelContainer ProvinceBlock(Control parent, int padding = 8)
    {
        var block = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        block.AddThemeStyleboxOverride("panel", Style(new Color("353b43"), new Color("535957"), 0, padding));
        parent.AddChild(block); return block;
    }

    private void ProvinceSection(Node parent, string title)
    {
        var header = new PanelContainer();
        header.AddThemeStyleboxOverride("panel", Style(new Color("44494e"), new Color("63655e"), 0, 5));
        var label = ProvinceLabel(title, 17, Cream, true); label.HorizontalAlignment = HorizontalAlignment.Center;
        header.AddChild(label); parent.AddChild(header);
    }

    private void ProvinceMetric(Control parent, string icon, string label, string value, string note = "")
    {
        var block = ProvinceBlock(parent, 8); block.CustomMinimumSize = new Vector2(0, 82);
        block.TooltipText = label + "：" + value + (note.Length > 0 ? "\n" + note : "");
        var row = Row(block, 8);
        row.AddChild(new TextureRect { Texture = UiIcon(icon), CustomMinimumSize = new Vector2(44, 44),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore });
        var body = VBox(row, 2); body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(ProvinceLabel(label, 14, Cream));
        body.AddChild(ProvinceLabel(value, 22, Gold, true));
        if (note.Length > 0) body.AddChild(ProvinceLabel(note, 11, Muted));
    }

    private GridContainer ProvinceGrid(Control parent, int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 4); grid.AddThemeConstantOverride("v_separation", 4);
        parent.AddChild(grid); return grid;
    }

    private void ProvincePanel(CountryState country, RegionState region)
    {
        var cities = country.Cities.Where(c => c.RegionId == region.Id).OrderByDescending(c => c.Population).ToArray();
        var body = VBox(_right, 5); body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var tabs = Row(body, 1);
        foreach (var (id, title) in new[] { ("overview", "概览"), ("buildings", "建筑"), ("population", "人口"), ("prices", "本地价格"), ("information", "信息") })
        {
            var button = SmallButton(title, () => { _provincePage = id; _rightScroll.ScrollVertical = 0; RefreshPanels(); }, _provincePage == id);
            button.CustomMinimumSize = new Vector2(0, 34); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.AddThemeFontSizeOverride("font_size", 14); tabs.AddChild(button);
        }
        switch (_provincePage)
        {
            case "buildings": ProvinceBuildings(body, country, region, cities); break;
            case "population": ProvincePopulation(body, country, region, cities); break;
            case "prices": ProvincePrices(body, country, region); break;
            case "information": ProvinceInformation(body, country, region, cities); break;
            default: ProvinceOverview(body, country, region, cities); break;
        }
    }

    private void ProvinceOverview(Control parent, CountryState country, RegionState region, CityState[] cities)
    {
        var landscape = new TextureRect { Texture = GD.Load<Texture2D>("res://Assets/illustrations/region-landscape.png"), CustomMinimumSize = new Vector2(0, 232),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TooltipText = "原创地区风景示意，不代表具体城市的历史地貌。", MouseFilter = Control.MouseFilterEnum.Stop };
        parent.AddChild(landscape);
        var title = new Label { Text = Localization.Tr(region.Name), HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = Control.MouseFilterEnum.Ignore };
        title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); title.OffsetRight = -12; title.OffsetBottom = -10;
        title.AddThemeFontOverride("font", _serif); title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color("eee3c4"));
        title.AddThemeColorOverride("font_shadow_color", new Color("22282b"));
        title.AddThemeConstantOverride("shadow_offset_x", 2); title.AddThemeConstantOverride("shadow_offset_y", 2); landscape.AddChild(title);

        var main = ProvinceGrid(parent, 2);
        ProvinceMetric(main, "markets", "已模拟城市产出", Money(cities.Sum(c => c.Gdp)), "年化产出");
        ProvinceMetric(main, "industry", "建筑", cities.Sum(c => c.Industries.Values.Sum()).ToString() + " 级", $"{cities.Length} 座收录城市");
        var population = ProvinceBlock(parent, 8); var row = Row(population, 9);
        row.AddChild(new TextureRect { Texture = UiIcon("population"), CustomMinimumSize = new Vector2(44, 44),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var popText = VBox(row, 2); popText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        popText.AddChild(ProvinceLabel("人口", 15)); popText.AddChild(ProvinceLabel(Compact(region.Population), 25, Gold, true));
        var rural = VBox(row, 2); rural.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; rural.CustomMinimumSize = new Vector2(173, 0);
        rural.AddChild(ProvinceLabel("农村人口  " + Compact(region.RuralPopulation), 13));
        rural.AddChild(ProvinceLabel("城市人口  " + Compact(cities.Sum(c => c.Population)), 13));
        var metrics = ProvinceGrid(parent, 2);
        ProvinceMetric(metrics, "population", "城市可用劳工", Compact(cities.Sum(c => c.Unemployed)), "尚未就业的城市劳动力");
        ProvinceMetric(metrics, "construction", "进行中的建设", country.Construction.Count(p => cities.Any(c => c.Id == p.CityId)).ToString(), "已进入建造队列");
        ProvinceMetric(metrics, "markets", "市场接入", $"{region.MarketAccess:0.0%}", $"铁路 {region.RailwayLevels} 级");
        ProvinceMetric(metrics, "terrain", "基础设施 / 已用", $"{region.Infrastructure:0.#} / {region.InfrastructureUsage:0.#}");
        parent.AddChild(SmallButton("本地市场与铁路 →", () => { _provincePage = "prices"; _rightScroll.ScrollVertical = 0; RefreshPanels(); }));
        if (cities.Length == 0) parent.AddChild(Para("已接入农村人口；城邑与地方产业尚待补充，当前没有可建设城市。", 13, Muted));
        if (country.Id == _engine.Player.Id && cities.Length > 0)
        {
            var build = B("查看本省建筑  →", () => { _provincePage = "buildings"; _rightScroll.ScrollVertical = 0; RefreshPanels(); });
            parent.AddChild(build);
        }
        ProvinceCityRows(parent, cities);
    }

    private void ProvinceBuildings(Control parent, CountryState country, RegionState region, CityState[] cities)
    {
        var summary = Row(parent, 6);
        var total = ProvinceLabel($"{cities.Sum(c => c.Industries.Values.Sum())} 级建筑", 15, Cream, true);
        total.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; summary.AddChild(total);
        var available = ProvinceLabel($"可用劳工 {Compact(cities.Sum(c => c.Unemployed))}", 13, Gold);
        available.CustomMinimumSize = new Vector2(185, 0); available.HorizontalAlignment = HorizontalAlignment.Right; summary.AddChild(available);
        foreach (var (category, title) in new[] { ("industry", "工业建筑"), ("resources", "资源产业"), ("agriculture", "农业建筑") })
        {
            ProvinceSection(parent, title); var grid = ProvinceGrid(parent, 4);
            foreach (var definition in Catalog.Industries.Where(i => ConstructionClass(i.Id) == category))
            {
                int levels = cities.Sum(c => c.Industries.GetValueOrDefault(definition.Id));
                int pending = country.Construction.Count(p => p.IndustryId == definition.Id && cities.Any(c => c.Id == p.CityId));
                var panel = ProvinceBlock(grid, 2); panel.CustomMinimumSize = new Vector2(112, 0);
                var column = VBox(panel, 2);
                var name = ProvinceLabel(definition.Name, 13, Cream, true); name.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(name);
                var picture = new TextureRect { Texture = ConstructionPicture(definition.Id), CustomMinimumSize = new Vector2(0, 104),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = Control.MouseFilterEnum.Ignore }; column.AddChild(picture);
                var actions = Row(column, 2); var level = ProvinceLabel($"{levels}" + (pending > 0 ? $" +{pending}" : ""), 19, Gold, true);
                level.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; level.VerticalAlignment = VerticalAlignment.Center; level.HorizontalAlignment = HorizontalAlignment.Center;
                actions.AddChild(level);
                string type = definition.Id, targetRegion = region.Id;
                var add = ConstructionAddButton(() => ChooseConstructionLocations(type, targetRegion));
                add.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; add.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                add.CustomMinimumSize = new Vector2(36, 36); add.AddThemeFontSizeOverride("font_size", 18);
                add.AddThemeStyleboxOverride("disabled", Style(new Color("4d504d"), new Color("77786a"), 18, 2));
                add.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
                add.Disabled = country.Id != _engine.Player.Id || cities.Length == 0 || _engine.State.Finished;
                add.TooltipText = country.Id != _engine.Player.Id ? "外国地区，由其政府管理建设" : "选择本省城市，扩建一级";
                actions.AddChild(add);
                panel.TooltipText = $"{Localization.Tr(definition.Name)}\n已建 {levels} 级 · 待建 {pending} 级\n实际就业 {Compact(ConstructionIndustryEmployment(cities, definition.Id))}\n{ConstructionCatalog.Costs[definition.Id]:0} 建造点 / 级";
            }
            // Keep the same four-column rhythm even when this scenario has fewer building types.
            int remainder = Catalog.Industries.Count(i => ConstructionClass(i.Id) == category) % 4;
            if (remainder > 0) for (int i = remainder; i < 4; i++) grid.AddChild(new Control { CustomMinimumSize = new Vector2(112, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        }
        if (cities.Length == 0) parent.AddChild(Para("该省尚未收录可建设城市，建筑数据待补充。", 13, Muted));
        if (country.Id != _engine.Player.Id) parent.AddChild(Para("外国地区可查看产业情况；建设由该国政府管理。", 13, Muted));
        else if (cities.Length > 0)
        {
            var actions = Row(parent, 5);
            var detailed = SmallButton("管理本省建筑", () => OpenRegionConstruction(region.Id)); detailed.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; actions.AddChild(detailed);
            var queue = SmallButton("建造队列", OpenConstructionQueue); queue.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; actions.AddChild(queue);
        }
    }

    private void ProvincePopulation(Control parent, CountryState country, RegionState region, CityState[] cities)
    {
        var pops = country.Pops.Where(p => p.RegionId == region.Id).ToArray();
        long urban = cities.Sum(c => c.Population), workforce = pops.Sum(p => p.Workforce);
        ProvinceSection(parent, "地区人口");
        var people = ProvinceGrid(parent, 2);
        ProvinceMetric(people, "population", "总人口", Compact(region.Population));
        ProvinceMetric(people, "atlas", "农村人口", Compact(region.RuralPopulation));
        ProvinceMetric(people, "population", "城市人口", Compact(urban));
        ProvinceMetric(people, "industry", "地区劳动力", Compact(workforce));
        ProvinceSection(parent, "人口阶层");
        parent.AddChild(Para("阶层包含家属；财富和识字率按人口加权，收入为税后收入。", 12, Muted));
        PopRows(parent, pops);
        ProvinceCityRows(parent, cities);
    }

    private void ProvincePrices(Control parent, CountryState country, RegionState region)
    {
        RegionMarketAccess(parent, country, region);
        LocalMarketRows(parent, country, region);
    }

    private void ProvinceInformation(Control parent, CountryState country, RegionState region, CityState[] cities)
    {
        var province = QingProvinceCatalog.Provinces.FirstOrDefault(p => p.Id == region.Id);
        ProvinceSection(parent, "地区资料");
        if (province != null)
        {
            var info = ProvinceGrid(parent, 2);
            ProvinceMetric(info, "politics", "行政建制", province.Administration);
            ProvinceMetric(info, "atlas", "行政中心", province.Capital);
            parent.AddChild(Para(province.Note, 14, Cream));
            parent.AddChild(Para(QingProvinceCatalog.BoundaryNotice, 12, Muted));
        }
        else parent.AddChild(Para("地区划分用于本场景经济模拟；地图国家疆域参考1815年资料，未逐地校订至1836年。", 13, Muted));
        parent.AddChild(Para($"已收录 {cities.Length} 座城市。起始人口与产业为场景估算；农村人口已单独计入地区总人口。", 13, Muted));
        parent.AddChild(SmallButton("← 返回全国地区", () => ExploreCountry(country.Id)));
        ProvinceCityRows(parent, cities);
    }

    private void ProvinceCityRows(Control parent, CityState[] cities)
    {
        if (cities.Length == 0) return;
        ProvinceSection(parent, "城市");
        foreach (var city in cities)
        {
            var block = ProvinceBlock(parent, 6); var row = Row(block, 6);
            var detail = VBox(row, 1); detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            detail.AddChild(ProvinceLabel(city.Name, 18, Cream, true));
            detail.AddChild(ProvinceLabel($"人口 {Compact(city.Population)}  ·  {city.Industries.Values.Sum()} 级产业", 12, Muted));
            var inspect = SmallButton("经济", () => SelectCity(city.Id)); inspect.TooltipText = "查看城市经济"; row.AddChild(inspect);
            var enter = SmallButton("进入", () => OpenCityDetail(city.Id)); enter.TooltipText = "进入三维城市"; row.AddChild(enter);
        }
    }
}
