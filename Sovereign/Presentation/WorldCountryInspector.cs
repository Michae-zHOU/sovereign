using Godot;
using System;
using System.Linq;

namespace Sovereign.Presentation;

public partial class Main
{
    private string _historicalCountryId = "", _worldSearch = "";

    private async void RunWorldSmoke()
    {
        try
        {
            ShowTab("WorldIndex"); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); AuditChinese(_ui);
            int count = 0;
            foreach (var country in HistoricalWorldAtlas.Countries)
            {
                InspectHistoricalCountry(country.Id);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!_drawerOpen || _tab != "WorldCountry" || _historicalCountryId != country.Id) throw new Exception("Historical country selection failed: " + country.Id);
                AuditChinese(_ui); count++;
            }
            foreach (string mode in new[] { "Political", "Economy", "Population", "Terrain" }) SetLens(mode);
            ShowTab("Overview"); ToggleDrawer(); SelectCity("QNG_suzhou");
            if (!_drawerOpen || _selectedCity != "QNG_suzhou") throw new Exception("City selection did not reopen the drawer");
            GD.Print($"SOVEREIGN_WORLD_UI_PASS records={count} modes=4 drawer=True"); RequestQuit(0);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); RequestQuit(1); }
    }

    private void InspectHistoricalCountry(string id)
    {
        if (!_started || _menuOpen) return;
        if (_city.IsOpen || _leader.IsOpen) CloseCity();
        _historicalCountryId = id; _selectedCity = ""; _selectedRegion = "";
        _drawerOpen = true; _tab = "WorldCountry"; _rightScroll.ScrollVertical = 0;
        _map.FocusHistoricalCountry(id); RefreshPanels();
    }

    private void WorldCountryPanel()
    {
        var country = HistoricalWorldAtlas.Country(_historicalCountryId);
        if (country == null) { WorldCountryIndex(); return; }
        _right.AddChild(L(country.Name, 27, Cream, true));
        Section(_right, "历史地图档案");
        Pair(_right, "疆域资料年份", country.SourceYear + "年");
        _right.AddChild(Para("地图中的疆域依据1815年历史底图，不能视为当前战役日期的精确国界。", 13, Gold));
        _right.AddChild(Para(country.Description, 13, Cream));
        Rule(_right);
        if (country.ScenarioCountryId is string id && _engine.State.Countries.Any(c => c.Id == id))
        {
            _right.AddChild(Para("该国家已有经济、人口与城市场景，可进入国家详情查看本局模拟。", 14));
            _right.AddChild(B("查看国家与外交 →", () => InspectCountry(id), true));
        }
        else
        {
            Section(_right, "本版覆盖情况");
            _right.AddChild(Para("已支持疆域选择和地图定位。此国家尚未接入人口、经济、城市与领袖模拟，不能在当前版本开始战役。", 14));
            var locked = B("国家模拟尚未开放", () => { }); locked.Disabled = true; _right.AddChild(locked);
        }
        Rule(_right);
        _right.AddChild(B("定位所选疆域", () => _map.FocusHistoricalCountry(country.Id)));
        _right.AddChild(B("全球国家索引 →", () => ShowTab("WorldIndex")));
        _right.AddChild(B("历史底图来源 ↗", () => OS.ShellOpen("https://github.com/aourednik/historical-basemaps")));
    }

    private void WorldCountryIndex()
    {
        _right.AddChild(Para($"全球地图收录 {HistoricalWorldAtlas.Countries.Count} 项国家、政权与历史地区记录。点击地图疆域或下方名称即可查看。", 14));
        _right.AddChild(Para("参考年份：1815年。地图可选范围与本版12国模拟范围分别列示。", 12, Gold));
        var search = new LineEdit { Text = _worldSearch, PlaceholderText = "搜索中文国名", CustomMinimumSize = new Vector2(0, 36), ClearButtonEnabled = true };
        search.AddThemeStyleboxOverride("normal", Style(new Color("182c29"), new Color("85734c"), 0, 8));
        search.AddThemeColorOverride("font_color", Cream); _right.AddChild(search);
        var list = VBox(_right, 5);
        void Fill()
        {
            Clear(list);
            var matches = HistoricalWorldAtlas.Countries.Where(c => string.IsNullOrEmpty(_worldSearch) || c.Name.Contains(_worldSearch, StringComparison.OrdinalIgnoreCase) || c.SourceName.Contains(_worldSearch, StringComparison.OrdinalIgnoreCase));
            foreach (var country in matches.OrderBy(c => c.Name, StringComparer.Create(Localization.Chinese, false)))
            {
                string id = country.Id; var b = SmallButton(country.Name + (country.ScenarioCountryId != null ? "  · 已有模拟" : ""), () => InspectHistoricalCountry(id));
                b.Alignment = HorizontalAlignment.Left; b.ClipText = true; list.AddChild(b);
            }
        }
        search.TextChanged += value => { _worldSearch = value; Fill(); };
        Fill();
    }
}
