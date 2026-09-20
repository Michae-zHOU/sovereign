using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private PanelContainer _outlinerPanel = null!, _marketStrip = null!;
    private VBoxContainer _outliner = null!;
    private Label _drawerTitle = null!, _drawerSubtitle = null!, _clockStatus = null!, _constructionReadout = null!;
    private TextureRect _countrySeal = null!, _drawerFlag = null!;
    private Control _drawerHeader = null!, _mapSource = null!, _navReveal = null!, _pauseBadge = null!, _constructionStrip = null!, _drawerFooter = null!;
    private readonly Dictionary<string, bool> _outlineExpanded = new();
    private bool _drawerOpen = true;
    private string _activeMapMode = "Political";
    private readonly Dictionary<string, Button> _lenses = new();
    private readonly Dictionary<string, Label> _hudValues = new();
    private readonly Dictionary<string, (Label Price, Label Stock)> _marketValues = new();
    private readonly List<VBoxContainer> _hudColumns = new();


    private StyleBoxTexture CabinetSurface(string name, int pad = 10)
    {
        int edge = name.StartsWith("medallion", StringComparison.Ordinal) || name is "lens-tray" or "clock-face" ? 0 : 18;
        var skin = new StyleBoxTexture { Texture = UiIcon(name), TextureMarginLeft = edge, TextureMarginTop = edge,
            TextureMarginRight = edge, TextureMarginBottom = edge, ContentMarginLeft = pad, ContentMarginTop = pad,
            ContentMarginRight = pad, ContentMarginBottom = pad };
        if (name is "cabinet-body" or "cabinet-header" or "outliner-heading" or "hud-top")
        { skin.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile; skin.AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile; }
        return skin;
    }

    private PanelContainer CabinetPanel(Control parent, string surface = "cabinet-body", int pad = 10)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", CabinetSurface(surface, pad)); parent.AddChild(panel); return panel;
    }

    private Button Medallion(string icon, string tooltip, Action action, int size = 50)
    {
        var button = new Button { CustomMinimumSize = new Vector2(size, size), TooltipText = tooltip, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand, Icon = UiIcon(icon), ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center };
        button.AddThemeConstantOverride("icon_max_width", size * 4 / 5);
        button.AddThemeStyleboxOverride("normal", CabinetSurface("medallion", size / 6));
        button.AddThemeStyleboxOverride("hover", CabinetSurface("medallion-hover", size / 6));
        button.AddThemeStyleboxOverride("pressed", CabinetSurface("medallion-active", size / 6));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += action; return button;
    }

    private void Place(Control node, float left, float top, float right, float bottom)
    {
        node.OffsetLeft = left; node.OffsetTop = top; node.OffsetRight = right; node.OffsetBottom = bottom;
    }

    private Button SmallButton(string text, Action action, bool selected = false)
    {
        var b = B(text, action, selected); b.CustomMinimumSize = new Vector2(0, 30);
        b.AddThemeFontSizeOverride("font_size", 13);
        b.AddThemeStyleboxOverride("normal", CabinetSurface(selected ? "button-selected" : "cabinet-row", 5));
        b.AddThemeStyleboxOverride("hover", CabinetSurface("button-hover", 5));
        b.AddThemeStyleboxOverride("pressed", CabinetSurface("button-pressed", 5));
        b.AddThemeColorOverride("font_color", Cream); b.AddThemeColorOverride("font_hover_color", Cream); b.AddThemeColorOverride("font_pressed_color", Cream);
        return b;
    }

    private void BuildCabinetShell()
    {
        // Live-reference measurements normalized to a 1000-unit UI height.
        var top = CabinetPanel(_ui, "hud-top", 3); Place(top, 0, 0, 638, 72);
        var topRow = Row(top, 4);
        var identity = new Button { CustomMinimumSize = new Vector2(96, 64), TooltipText = "国家概览", MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        identity.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); identity.AddThemeStyleboxOverride("hover", new StyleBoxEmpty()); identity.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        identity.Pressed += () => ShowTab("Overview"); topRow.AddChild(identity);
        _countrySeal = CountryFlag("QNG", 92, 62); _countrySeal.MouseFilter = Control.MouseFilterEnum.Ignore;
        identity.AddChild(_countrySeal); _countrySeal.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _metrics = Row(topRow, 4); _metrics.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        for (int i = 0; i < 3; i++) { var column = VBox(_metrics, 0); column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; _hudColumns.Add(column); }
        BuildReferenceClock();
        _leftPanel = CabinetPanel(_ui, "nav-rail", 1);
        Place(_leftPanel, 0, 174, 47, 637);
        _leftScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _leftPanel.AddChild(_leftScroll); _left = VBox(_leftScroll, 3); _left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foreach (var (tab, label, icon) in new[] {
            ("Overview", "朝政", "overview"), ("Politics", "政治", "politics"), ("Industry", "建筑", "industry"),
            ("Markets", "市场", "markets"), ("Population", "人口", "population"), ("Research", "科技", "research"),
            ("Diplomacy", "外交", "diplomacy"), ("Chronicle", "日志", "chronicle"), ("Atlas", "地区", "atlas"), ("Leadership", "领袖", "leadership") })
        {
            string target = tab;
            var b = Medallion(icon, label + " · " + Localization.Tr(tab), () => { if (_drawerOpen && _tab == target && !_city.IsOpen && !_leader.IsOpen) ToggleDrawer(); else ShowTab(target); }, 43);
            b.MouseEntered += () => _navReveal.Visible = true;
            _left.AddChild(b); _nav[tab] = b;
        }

        _rightPanel = CabinetPanel(_ui, "cabinet-body", 6);
        _rightPanel.AnchorBottom = 1; Place(_rightPanel, 48, 164, 550, -22);
        _rightScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _rightPanel.AddChild(_rightScroll); _right = VBox(_rightScroll, 4); _right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var drawerHeader = CabinetPanel(_ui, "cabinet-header", 9); _drawerHeader = drawerHeader;
        Place(drawerHeader, 48, 78, 550, 164);
        var headerRow = Row(drawerHeader, 8);
        var back = Medallion("close", "返回国家概览", () => ShowTab("Overview"), 40); back.Icon = null; back.Text = "↶";
        back.AddThemeFontSizeOverride("font_size", 24); back.AddThemeColorOverride("font_color", Gold); headerRow.AddChild(back);
        var titleBox = VBox(headerRow, 0); titleBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; titleBox.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _drawerTitle = L("大清朝政", 25, Cream, true); _drawerTitle.HorizontalAlignment = HorizontalAlignment.Center; titleBox.AddChild(_drawerTitle);
        var subtitleRow = Row(titleBox, 4); subtitleRow.Alignment = BoxContainer.AlignmentMode.Center;
        _drawerFlag = CountryFlag("QNG", 25, 16); subtitleRow.AddChild(_drawerFlag);
        _drawerSubtitle = L("", 15, Gold); subtitleRow.AddChild(_drawerSubtitle);
        var closeDrawer = Medallion("close", "关闭详情面板 · Esc", ToggleDrawer, 40); closeDrawer.AddThemeConstantOverride("icon_max_width", 18); headerRow.AddChild(closeDrawer);
        var footer = CabinetPanel(_ui, "cabinet-body", 5); _drawerFooter = footer; footer.AnchorTop = footer.AnchorBottom = 1; Place(footer, 48, -70, 550, -22);
        footer.AddChild(B("建筑注册表", OpenConstructionRegistry));
        var strip = CabinetPanel(_ui, "cabinet-top", 7); _constructionStrip = strip; strip.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight); Place(strip, -378, 75, -2, 132);
        var stripRow = Row(strip, 8); stripRow.AddChild(Medallion("construction", "查看全国建造队列", OpenConstructionQueue, 36));
        _constructionReadout = L("", 15, Cream); _constructionReadout.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; stripRow.AddChild(_constructionReadout);
        stripRow.AddChild(SmallButton("→", OpenConstructionQueue));
        _outlinerPanel = CabinetPanel(_ui, "cabinet-body", 4);
        _outlinerPanel.AnchorLeft = 1; _outlinerPanel.AnchorRight = 1; _outlinerPanel.AnchorBottom = 1;
        Place(_outlinerPanel, -318, 142, -3, -134);
        var outlineScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _outlinerPanel.AddChild(outlineScroll);
        _outliner = VBox(outlineScroll, 2); _outliner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var mapSource = L("疆域参考：1815年 · 非1836年精确边界", 10, Cream); _ui.AddChild(mapSource); _mapSource = mapSource;
        mapSource.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight); Place(mapSource, -716, 86, -388, 108);
        var lensTray = CabinetPanel(_ui, "lens-tray", 0); lensTray.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom); Place(lensTray, -201, -80, 201, -1);
        var lenses = new HBoxContainer(); lensTray.AddChild(lenses); _atlasHeading = lensTray;
        lenses.AddThemeConstantOverride("separation", 3); lenses.Alignment = BoxContainer.AlignmentMode.Center; 
        foreach (var (mode, label, icon) in new[] { ("Political", "国家", "diplomacy"), ("Provinces", "省份", "atlas"), ("Economy", "经济", "markets"), ("Population", "人口", "population"), ("Terrain", "地形", "terrain") })
        {
            var b = Medallion(icon, label + "地图", () => SetLens(mode), 66);
            _lenses[mode] = b; lenses.AddChild(b);
        }

        var cityHeading = new VBoxContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore }; _ui.AddChild(cityHeading); _cityHeading = cityHeading;
        cityHeading.Position = new Vector2(568, 98); cityHeading.AddChild(SmallButton("← 返回世界地图", CloseCity));
        _placeTitle = L("", 30, Cream, true); cityHeading.AddChild(_placeTitle); _placeSubtitle = L("", 12, Cream); cityHeading.AddChild(_placeSubtitle);
        _districtButtons = Row(cityHeading, 4);
        foreach (var name in new[] { "City", "Housing", "Industry", "Civic", "Waterfront" }) { var district = name; _districtButtons.AddChild(SmallButton(name, () => _city.FocusDistrict(district))); }

        _marketStrip = CabinetPanel(_ui, "cabinet-body", 9);
        _marketStrip.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide); Place(_marketStrip, 558, -160, -326, -86); _goods = Row(_marketStrip, 7);
        _status = L("", 10, Muted); _ui.AddChild(_status); _status.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft); Place(_status, 54, -21, 500, -1);
        var utility = new HBoxContainer(); _ui.AddChild(utility); utility.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight); Place(utility, -252, -49, -12, -12); utility.AddThemeConstantOverride("separation", 3);
        foreach (var (caption, action) in new (string, Action)[] { ("保存", Save), ("读取", Load), ("帮助", ShowHelp), ("战役", ShowCountryMenu) })
        { var b = SmallButton(caption, action); b.CustomMinimumSize = new Vector2(56, 32); utility.AddChild(b); }
        _toast = L("", 13, Cream); _ui.AddChild(_toast); _toast.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom); Place(_toast, -310, -133, 310, -97); _toast.HorizontalAlignment = HorizontalAlignment.Center; _toast.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        BuildReferenceNavigation();
        RefreshAll();
    }

    private void ToggleDrawer() { _drawerOpen = !_drawerOpen; _navReveal.Visible = false; UpdateCabinetVisibility(); }
    private void ShowTab(string tab)
    {
        if (_city.IsOpen || _leader.IsOpen) CloseCity();
        _drawerOpen = true; _navReveal.Visible = false; _tab = tab; _rightScroll.ScrollVertical = 0; RefreshPanels();
    }
    private void SetLens(string mode)
    {
        _activeMapMode = mode; _map.SetMapMode(mode);
        foreach (var pair in _lenses) pair.Value.AddThemeStyleboxOverride("normal", CabinetSurface(pair.Key == mode ? "medallion-active" : "medallion", 11));
        Toast(mode switch { "Provinces" => "大清省份 · 点击省域查看人口、行政建制与城市；边界为地理示意", "Economy" => "经济视图 · 城市标记大小表示当地年化产出", "Population" => "人口视图 · 城市标记大小表示当地人口", "Terrain" => "地形视图 · 滚轮缩放，右键拖动地图", _ => "国家视图 · 点击疆域打开国家外交；省份视图可选择大清各省" });
    }
    private void UpdateCabinetVisibility()
    {
        bool full = _tab == "Politics" && _drawerOpen;
        _rightPanel.AnchorRight = _drawerHeader.AnchorRight = full ? 1 : 0;
        _rightPanel.OffsetRight = _drawerHeader.OffsetRight = full ? -4 : 550;
        _rightPanel.OffsetBottom = _tab == "Industry" ? -69 : -22;
        _rightPanel.Visible = _drawerOpen; _drawerHeader.Visible = _drawerOpen;
        _drawerFooter.Visible = _tab == "Industry" && _drawerOpen;
        _constructionStrip.Visible = !full && !_city.IsOpen && !_leader.IsOpen;
        _pauseBadge.Visible = !full && _speed == 0 && !_city.IsOpen && !_leader.IsOpen;
        _atlasHeading.Visible = !full && !_city.IsOpen && !_leader.IsOpen;
        _leftPanel.Visible = true;
        _outlinerPanel.Visible = !full && !_city.IsOpen && !_leader.IsOpen;
        _mapSource.Visible = !full && !_city.IsOpen && !_leader.IsOpen;
        _marketStrip.Visible = _tab == "Markets" && !_city.IsOpen && !_leader.IsOpen;
        _drawerTitle.Text = _tab == "Meeting" ? "外交会谈" : _tab == "ForeignCountry" ? "国家外交" : _tab == "WorldCountry" ? "国家档案" : _tab == "WorldIndex" ? "全球国家索引" : _tab == "Industry" ? "建筑" : _tab == "Politics" ? "政治" : _tab == "Overview" && _engine.Player.Id == "QNG" ? "大清朝政" : Localization.Tr(_tab);
        string provinceTitle = GetProvinceDrawerTitle(); if (provinceTitle.Length > 0) _drawerTitle.Text = provinceTitle;
        string country = GetProvinceDrawerCountryId();
        if (country.Length == 0) country = _tab switch {
            "ForeignCountry" or "Meeting" when _foreignCountry.Length > 0 => _foreignCountry,
            "Leadership" when _leader.IsOpen && _leaderCountry.Length > 0 => _leaderCountry,
            "Atlas" or "City" => ExploredCountry.Id, _ => _engine.Player.Id };
        _drawerFlag.Texture = CountryFlagTexture(country); _drawerFlag.Visible = _tab is not ("Industry" or "WorldCountry" or "WorldIndex");
        _drawerSubtitle.Text = provinceTitle.Length > 0 ? GetProvinceDrawerSubtitle() : _tab == "Industry" ? $"{_engine.Player.Construction.Count}座建筑处于建造中 →" :
            _tab is "WorldCountry" or "WorldIndex" ? "1815年参考疆域" : Localization.Tr(_engine.State.Countries.First(c => c.Id == country).Name);
        foreach (var pair in _nav) pair.Value.AddThemeStyleboxOverride("normal", CabinetSurface(pair.Key == _tab && _drawerOpen ? "medallion-active" : "medallion", 8));
        foreach (var pair in _lenses) pair.Value.AddThemeStyleboxOverride("normal", CabinetSurface(pair.Key == _activeMapMode ? "medallion-active" : "medallion", 11));
    }

    private void RefreshCabinetHeader()
    {
        var p = _engine.Player;
        _countrySeal.Texture = CountryFlagTexture(p.Id); _countrySeal.TooltipText = CountryFlagHint(p.Id);
        foreach (var (key, value, tab, hint, colour) in new[] {
            ("国库", Money(p.Treasury), "Overview", "可用于建设与改革的公共储备", Cream),
            ("每日结余", (p.DailyBalance >= 0 ? "+" : "") + Money(p.DailyBalance), "Politics", "今日税收减去公共支出等费用", p.DailyBalance >= 0 ? Green : Red),
            ("年化产出", Money(p.Gdp), "Industry", "当前生产速度折算的年产出；剧本估值", Cream),
            ("人口", Compact(p.Population), "Population", "农村与已收录城市人口总计；剧本估值", Cream),
            ("识字率", $"{p.Literacy:0.0}%", "Research", "教育影响研究速度", Cream),
            ("生活水平", $"{p.LivingStandard:0.0}", "Population", "居民获取生活必需品的综合水平", Cream) })
        {
            if (_hudValues.TryGetValue(key, out var existing))
            {
                existing.Text = value; existing.AddThemeColorOverride("font_color", colour); continue;
            }
            var cell = new Button { TooltipText = key + "\n" + hint, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(166, 30), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
            cell.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            cell.AddThemeStyleboxOverride("hover", Style(new Color("3b514b"), new Color("817759"), 0, 0));
            cell.AddThemeStyleboxOverride("pressed", Style(new Color("192b2c"), Gold, 0, 0));
            cell.Pressed += () => ShowTab(tab); _hudColumns[_hudValues.Count / 2].AddChild(cell);
            var box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; cell.AddChild(box); box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); box.OffsetLeft = 3; box.OffsetRight = -5; box.AddThemeConstantOverride("separation", 5);
            string icon = key switch { "国库" or "每日结余" => "markets", "年化产出" => "industry", "识字率" => "research", _ => "population" };
            box.AddChild(new TextureRect { Texture = UiIcon(icon), CustomMinimumSize = new Vector2(26, 26), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore });
            var number = L(value, 19, colour); box.AddChild(number); _hudValues[key] = number;
        }
        _date.Text = Localization.Date(_engine.State.Date);
        _clockStatus.Text = _speed == 0 ? "已暂停" : $"每秒{_speed}天";
        _pauseBadge.Visible = _speed == 0 && !_city.IsOpen && !_leader.IsOpen && !(_tab == "Politics" && _drawerOpen);
        var construction = _engine.GetConstructionStatus();
        _constructionReadout.Text = $"{construction.WeeklyProgress:0.#} / {construction.WeeklyCapacity:0.#} 建造力 / 周\n{p.Construction.Count}座建筑处于建造中";
        _status.Text = (_speed == 0 ? "已暂停" : $"每秒{_speed}天") + "  ·  万国纪元 0.10  ·  " + (p.Id == "QNG" ? "大清篇" : Localization.Tr(p.Name));
        for (int i = 0; i < _speedButtons.Count; i++) _speedButtons[i].Modulate = new[] { 0, 1, 2, 4, 8, 12 }[i] == _speed ? Gold : Colors.White;
        foreach (string good in Catalog.Goods)
        {
            if (_marketValues.TryGetValue(good, out var existing))
            { existing.Price.Text = $"£{p.Prices[good]:0.00}"; existing.Stock.Text = "买 " + Compact(p.BuyOrders[good]); continue; }
            var box = VBox(_goods, 0); box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            box.AddChild(L(Title(good), 10, Gold)); var price = L($"£{p.Prices[good]:0.00}", 13, Cream); box.AddChild(price);
            var stock = L("买 " + Compact(p.BuyOrders[good]), 10, Muted); box.AddChild(stock); _marketValues[good] = (price, stock);
        }
    }

    private void RefreshOutliner() => RefreshReferenceOutliner();
}
