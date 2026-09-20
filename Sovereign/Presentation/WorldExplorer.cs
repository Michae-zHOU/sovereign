using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private LeaderView _leader = null!;
    private Label _placeTitle = null!, _placeSubtitle = null!;
    private HBoxContainer _districtButtons = null!;
    private string _exploreCountry = "", _selectedRegion = "", _selectedCity = "", _leaderCountry = "";
    private OptionButton? _atlasSelector;

    private CountryState ExploredCountry => _engine.State.Countries.FirstOrDefault(c => c.Id == _exploreCountry) ?? _engine.Player;
    private CountryState OwnerOf(string cityId) => _engine.State.Countries.First(c => c.Cities.Any(x => x.Id == cityId));
    private CityState SelectedCity => OwnerOf(_selectedCity).Cities.First(c => c.Id == _selectedCity);

    private void ExploreCountry(string countryId)
    {
        _exploreCountry = countryId; _selectedRegion = ""; _selectedCity = "";
        if (_city.IsOpen || _leader.IsOpen) CloseCity();
        SetLens(countryId == "QNG" ? "Provinces" : "Political");
        _drawerOpen = true; _tab = "Atlas"; _rightScroll.ScrollVertical = 0; _map.FocusCountry(countryId); RefreshPanels();
    }

    private void SelectRegion(string regionId)
    {
        if (_city.IsOpen || _leader.IsOpen) CloseCity();
        if (_selectedRegion != regionId) _provincePage = "overview";
        _exploreCountry = GeographyCatalog.Regions.First(r => r.Id == regionId).CountryId;
        _selectedRegion = regionId; _selectedCity = ""; _drawerOpen = true; _tab = "Atlas";
        SetLens(QingProvinceCatalog.Provinces.Any(p => p.Id == regionId) ? "Provinces" : "Political");
        _rightScroll.ScrollVertical = 0; _map.FocusRegion(regionId); RefreshPanels();
    }

    private void SelectCity(string cityId)
    {
        if (_activeMapMode == "Provinces") SetLens("Political");
        var definition = GeographyCatalog.Cities.First(c => c.Id == cityId);
        _exploreCountry = definition.CountryId; _selectedRegion = definition.RegionId; _selectedCity = cityId;
        _drawerOpen = true; _tab = "Atlas"; _rightScroll.ScrollVertical = 0; _map.FocusCity(cityId); RefreshPanels();
    }

    private void AtlasPanel()
    {
        var country = ExploredCountry;
        if (DrawerProvince is RegionState selectedProvince)
        {
            ProvincePanel(country, selectedProvince);
            return;
        }
        var selector = new OptionButton { CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _atlasSelector = selector;
        selector.AddThemeStyleboxOverride("normal", Style(new Color("514049"), new Color("71664a"), 3, 9));
        selector.AddThemeStyleboxOverride("hover", Style(new Color("6a5257"), Gold, 3, 9));
        selector.AddThemeColorOverride("font_color", Cream);
        foreach (var definition in GeographyCatalog.Countries)
        {
            selector.AddIconItem(CountryFlagTexture(definition.Id), Localization.Tr(definition.Name));
            selector.GetPopup().SetItemIconMaxWidth(selector.ItemCount - 1, 40);
        }
        selector.AddThemeConstantOverride("icon_max_width", 40);
        selector.Selected = GeographyCatalog.Countries.ToList().FindIndex(c => c.Id == country.Id);
        selector.ItemSelected += index => ExploreCountry(GeographyCatalog.Countries[(int)index].Id);
        _right.AddChild(selector);
        _right.AddChild(L($"{country.Regions.Count} regions · {country.Cities.Count} cities", 13, Ink));
        _right.AddChild(B(country.Id == _engine.Player.Id ? "查看三维领袖" : "觐见领袖 · 三维会谈", () => { if (country.Id == _engine.Player.Id) OpenLeader(country.Id); else MeetLeader(country.Id); }, false, true));
        if (country.Id == "QNG") _right.AddChild(B("大清省份地图 · 点击省域选择", () => { SetLens("Provinces"); _map.FocusCountry("QNG"); }));
        if (!string.IsNullOrEmpty(_selectedCity))
        {
            _right.AddChild(B("← Cities in this region", () => SelectRegion(_selectedRegion), false, true));
            CitySummary(_right, SelectedCity, country);
            _right.AddChild(B("Enter " + SelectedCity.Name + " in 3D  ↗", () => OpenCityDetail(_selectedCity), true));
            _right.AddChild(Para("Select Housing, Industry, Civic or Waterfront in the city view to move into a district.", 13, Ink));
            return;
        }
        Pair(_right, "Population", Compact(country.Population), true);
        Pair(_right, "Urban residents", Compact(country.Cities.Sum(c => c.Population)), true);
        foreach (var region in country.Regions)
        {
            var card = Panel(_right, new Color("e3dccb"), new Color("cfc2a8"), 10); var box = VBox(card, 5);
            box.AddChild(WrappedHeading(region.Name));
            box.AddChild(L($"{Compact(region.Population)} people · {country.Cities.Count(c => c.RegionId == region.Id)} cities", 12, new Color("677267")));
            box.AddChild(B("Explore region  →", () => SelectRegion(region.Id), false, true));
        }
        _right.AddChild(Para($"本版经济模拟覆盖{GeographyCatalog.Countries.Count}国、{GeographyCatalog.Regions.Count}个场景地区及{GeographyCatalog.Cities.Count}座城市。国家疆域图采用1815年参考数据，尚未逐国校订至1836年。", 12, new Color("747465")));
    }

    private void CityLink(CityState city)
    {
        var card = Panel(_right, new Color("e3dccb"), new Color("cfc2a8"), 10); var box = VBox(card, 5);
        box.AddChild(L(city.Name, 22, Ink, true));
        box.AddChild(L($"{Compact(city.Population)} residents · {city.Industries.Values.Sum()} industry", 12, new Color("677267")));
        box.AddChild(B("Inspect economy  →", () => SelectCity(city.Id), false, true));
        box.AddChild(B("Explore in 3D  ↗", () => OpenCityDetail(city.Id), true));
    }

    private void CitySummary(Node box, CityState city, CountryState owner)
    {
        box.AddChild(L(city.Name, 28, Ink, true));
        box.AddChild(Para(owner.Name + " / " + owner.Regions.First(r => r.Id == city.RegionId).Name, 12, new Color("6c766a")));
        if (city.Id == "OTT_damascus" && _engine.State.Date.Year <= 1840)
            box.AddChild(Para("Historical administration: Egyptian government controlled Damascus until 1840 under nominal Ottoman sovereignty. This prototype aggregates it into the Ottoman economy; separate administration is not simulated.", 12, new Color("79533f")));
        if (city.Id is "JAP_edo" or "JAP_kyoto")
            box.AddChild(Para("Edo is the shogunal seat; Kyōto is the imperial capital. Government and ceremonial sovereignty are distinct.", 12, new Color("79533f")));
        Pair(box, "Population", Compact(city.Population), true);
        Pair(box, "Workforce", Compact(city.Workforce), true);
        Pair(box, "Employed", Compact(city.Employed), true);
        Pair(box, "Unemployed", Compact(city.Unemployed), true);
        if (city.ConstructionWorkers > 0) Pair(box, "其中：营建工人", Compact(city.ConstructionWorkers), true);
        Pair(box, "Living standard", city.LivingStandard.ToString("0.0"), true);
        Pair(box, "Literacy", $"{city.Literacy:0.0}%", true);
        Pair(box, "Annualized output", Money(city.Gdp), true);
        Pair(box, "Migration last month", city.MigrationLastMonth.ToString("+0;-0;0"), true);
    }

    private void OpenCityDetail(string cityId)
    {
        var definition = GeographyCatalog.Cities.First(c => c.Id == cityId);
        _exploreCountry = definition.CountryId; _selectedRegion = definition.RegionId; _selectedCity = cityId;
        _leader.HideLeader(); _map.Visible = false; _leftPanel.Visible = true; _drawerOpen = true; _rightPanel.Visible = true;
        _atlasHeading.Visible = false; _cityHeading.Visible = true; _districtButtons.Visible = true;
        _placeTitle.Text = Localization.Tr(definition.Name); _placeSubtitle.Text = Localization.Tr(ExploredCountry.Name + " / " + ExploredCountry.Regions.First(r => r.Id == definition.RegionId).Name);
        _tab = "City"; _rightScroll.ScrollVertical = 0; UpdateCityScene(); RefreshPanels();
    }

    private void UpdateCityScene()
    {
        var country = OwnerOf(_selectedCity); var city = SelectedCity;
        _city.ShowSettlement(country.Id, city.Id, city.Population, city.Industries, country.Technologies.Contains("railways") ? 1 : 0);
    }

    private void CityPanel()
    {
        if (string.IsNullOrEmpty(_selectedCity)) { AtlasPanel(); return; }
        var city = SelectedCity; var country = OwnerOf(city.Id);
        _right.AddChild(ProvinceLabel(city.Name, 25, Cream, true));
        _right.AddChild(ProvinceLabel(Localization.Tr(country.Name) + " / " + Localization.Tr(country.Regions.First(r => r.Id == city.RegionId).Name), 13, Muted));
        var figures = ProvinceGrid(_right, 2);
        ProvinceMetric(figures, "population", "人口", Compact(city.Population));
        ProvinceMetric(figures, "markets", "年化产出", Money(city.Gdp));
        ProvinceMetric(figures, "industry", "就业人口", Compact(city.Employed), $"可用劳工 {Compact(city.Unemployed)}");
        ProvinceMetric(figures, "research", "识字率", $"{city.Literacy:0.0}%", $"生活水平 {city.LivingStandard:0.0}");
        ProvinceSection(_right, "本地产出 / 日");
        var production = ProvinceGrid(_right, 2);
        foreach (var good in Catalog.Goods)
        {
            var block = ProvinceBlock(production, 6);
            block.AddChild(ConstructionGood(good, $"{Localization.Tr(Title(good))}  {city.Production.GetValueOrDefault(good):0.0}"));
        }
        ProvinceSection(_right, "劳动力构成");
        var labor = ProvinceGrid(_right, 2);
        foreach (var (label, count) in new[] { ("产业与农业", city.IndustrialWorkers), ("营建部门", city.ConstructionWorkers), ("工匠与服务业", city.Artisans), ("受抚养人口", city.Dependents) })
        {
            var block = ProvinceBlock(labor, 6); var row = Row(block, 5);
            var name = ProvinceLabel(label, 13, Muted); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(name);
            var countLabel = ProvinceLabel(Compact(count), 16, Gold); countLabel.CustomMinimumSize = new Vector2(82, 0);
            countLabel.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(countLabel);
        }
        if (country.Id == _engine.Player.Id)
        {
            Section(_right, "INVEST IN " + city.Name, true);
            AddCityConstructionCards(_right, country, city);
            foreach (var project in country.Construction.Where(c => c.CityId == city.Id)) Pair(_right, project.Name, $"{project.ProgressPoints:0.#} / {project.RequiredPoints:0} 点", true);
            _right.AddChild(B("管理全国建造队列 →", OpenConstructionQueue));
        }
        else _right.AddChild(Para("Foreign city: economic inspection is available. Construction is controlled by its own government.", 13, Ink));
        Rule(_right, true); _right.AddChild(Para("A generated district reflecting local population and industry. Streets and landmarks are a regional interpretation, not an exact historical reconstruction.", 12, new Color("727663")));
    }

    private void PopulationPanel() => PopMechanicsPanel();

    private void OpenLeader(string countryId)
    {
        if (_exploreCountry != countryId) { _selectedCity = ""; _selectedRegion = ""; }
        _leaderCountry = countryId; _exploreCountry = countryId;
        _city.HideCity(); _map.Visible = false; _leftPanel.Visible = true; _drawerOpen = true; _rightPanel.Visible = true;
        _atlasHeading.Visible = false; _cityHeading.Visible = true; _districtButtons.Visible = false;
        _tab = "Leadership"; _rightScroll.ScrollVertical = 0; UpdateLeaderScene(); RefreshPanels();
    }

    private Label WrappedHeading(string text)
    {
        var heading = Para(text, 21, Ink); heading.AddThemeFontOverride("font", _serif); return heading;
    }

    private void UpdateLeaderScene()
    {
        var definition = HistoricalLeaders.Get(_leaderCountry, _engine.State.Date);
        int age = definition.AgeOn(_engine.State.Date) ?? -1;
        _leader.ShowLeader(definition.AppearanceKey, _leaderCountry, age);
        _placeTitle.Text = Localization.Tr(definition.Name); _placeSubtitle.Text = Localization.Tr(definition.Title) + " · " + _engine.State.Date.ToString("yyyy") + "年" + " · 可动三维人物";
    }

    private void LeadershipPanel()
    {
        var id = _leader.IsOpen ? _leaderCountry : _engine.Player.Id;
        var definition = HistoricalLeaders.Get(id, _engine.State.Date);
        if (!_leader.IsOpen)
        {
            var row = Row(_right, 14); row.AddChild(LeaderPortrait(id));
            var information = VBox(row, 9); information.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            information.AddChild(Para(definition.Name, 23, Gold)); information.AddChild(Para(definition.Title, 16));
            if (definition.AgeOn(_engine.State.Date) is int age) information.AddChild(Para($"年龄 · {age}岁", 14));
            information.AddChild(Para(Localization.Date(_engine.State.Date), 13, Muted));
        }
        _right.AddChild(L(definition.Name, 26, Ink, true));
        _right.AddChild(Para(definition.Title, 17, Ink));
        _right.AddChild(Para(definition.Description, 14, Ink));
        if (!string.IsNullOrWhiteSpace(definition.SecondaryOffice)) { Rule(_right, true); Section(_right, "GOVERNMENT & REGENCY", true); _right.AddChild(Para(definition.SecondaryOffice, 14, Ink)); }
        _right.AddChild(B("Historical source  ↗", () => { if (definition.SourceUrl.StartsWith("https://")) OS.ShellOpen(definition.SourceUrl); }, false, true));
        if (!_leader.IsOpen) _right.AddChild(B("查看可动三维人物 ↗", () => OpenLeader(id), true));
        if (id != _engine.Player.Id) _right.AddChild(B("开始外交会谈", () => MeetLeader(id), true));
        if (_leader.IsOpen)
        {
            var views = Row(_right, 6);
            if (_leader.HasFullFigure) views.AddChild(SmallButton("全身", () => _leader.SetFraming("full")));
            views.AddChild(SmallButton("常规", () => _leader.SetFraming("three_quarter")));
            views.AddChild(SmallButton("面容", () => _leader.SetFraming("face")));
            views.AddChild(SmallButton("复位", () => _leader.ResetView()));
            var gestures = Row(_right, 6);
            gestures.AddChild(SmallButton("致意", () => _leader.TriggerGesture("greeting")));
            gestures.AddChild(SmallButton("交谈", () => _leader.TriggerGesture("talk")));
            _right.AddChild(Para("按住鼠标右键旋转 · 滚轮拉近或拉远", 12, Muted));
        }
        Rule(_right, true); Section(_right, "SUCCESSION IN THIS CAMPAIGN", true);
        foreach (var leader in HistoricalLeaders.All.Where(l => l.CountryId == id).OrderBy(l => l.StartDate))
        {
            _right.AddChild(L(leader.Name, 18, Ink, true));
            _right.AddChild(L(leader.StartDate == Catalog.StartDate ? "Incumbent at campaign start" : Localization.Date(leader.StartDate), 12, new Color("677267")));
        }
        Rule(_right, true); _right.AddChild(Para("人物为可转动与播放动画的三维模型。服饰、面貌为历史题材美术诠释，并非经考证的本人容貌复原。", 12, Muted));
    }

    private async void RunContentSmoke()
    {
        try
        {
            int cities = 0, leaders = 0;
            foreach (var city in GeographyCatalog.Cities)
            {
                OpenCityDetail(city.Id);
                foreach (var district in new[] { "City", "Housing", "Industry", "Civic", "Waterfront" }) _city.FocusDistrict(district);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!_city.IsOpen || _selectedCity != city.Id) throw new Exception("City scene failed: " + city.Id);
                AuditChinese(_ui);
                cities++;
            }
            CloseCity();
            foreach (var leader in HistoricalLeaders.All.GroupBy(x => x.Id).Select(x => x.First()))
            {
                _map.Visible = false;
                _leader.ShowLeader(leader.AppearanceKey, leader.CountryId, leader.AgeOn(leader.StartDate) ?? -1);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!_leader.IsOpen) throw new Exception("Leader scene failed: " + leader.Id);
                leaders++;
            }
            CloseCity();
            GD.Print($"SOVEREIGN_CONTENT_PASS cities={cities} leaders={leaders} districts={cities * 5}");
            RequestQuit(0);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); RequestQuit(1); }
    }

    private void AuditChinese(Node node)
    {
        if (node is Control control && control.IsVisibleInTree())
        {
            string value = control is Label label ? label.Text : control is Button button ? button.Text : "";
            if (System.Text.RegularExpressions.Regex.IsMatch(value, "[A-Za-z]{3,}")) GD.Print("ZH_REVIEW " + value.Replace("\n", " / "));
        }
        foreach (var child in node.GetChildren()) AuditChinese(child);
    }
}
