using Godot;
using System;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private string _politicsPage = "laws";

    private void BuildReferenceClock()
    {
        var clock = CabinetPanel(_ui, "hud-top", 3); clock.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight); Place(clock, -378, 0, -2, 72);
        var surface = new Control(); clock.AddChild(surface);
        _date = L("", 18, Cream); surface.AddChild(_date); Place(_date, 10, 1, 203, 27);
        _clockStatus = L("已暂停", 13, Muted); surface.AddChild(_clockStatus); Place(_clockStatus, 10, 32, 123, 62);
        var step = SmallButton("+1天", AdvanceOne); surface.AddChild(step); Place(step, 128, 34, 188, 62);
        var dial = new TextureRect { Texture = UiIcon("clock-face"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        surface.AddChild(dial); Place(dial, 209, -2, 369, 70);
        var pause = SmallButton("Ⅱ", () => SetSpeed(_speed == 0 ? 1 : 0)); pause.TooltipText = "暂停 / 继续 · 空格键"; surface.AddChild(pause); Place(pause, 191, 39, 219, 64); _speedButtons.Add(pause);
        var points = new[] { new Vector2(223, 35), new Vector2(243, 12), new Vector2(272, 0), new Vector2(301, 12), new Vector2(323, 35) };
        int[] speeds = { 1, 2, 4, 8, 12 }; string[] captions = { "Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ", "Ⅴ" };
        for (int i = 0; i < speeds.Length; i++)
        {
            int speed = speeds[i]; var b = SmallButton(captions[i], () => SetSpeed(speed)); b.TooltipText = $"每秒推进{speed}天";
            b.CustomMinimumSize = new Vector2(26, 26); b.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); b.AddThemeStyleboxOverride("hover", CabinetSurface("button-hover", 0));
            surface.AddChild(b); Place(b, points[i].X, points[i].Y, points[i].X + 27, points[i].Y + 27); _speedButtons.Add(b);
        }
    }
    private void BuildReferenceNavigation()
    {
        var pause = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; _ui.AddChild(pause); _pauseBadge = pause;
        pause.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop); Place(pause, -33, 164, 33, 220);
        var b = Medallion("close", "继续时间 · 空格键", () => SetSpeed(1), 30); b.Icon = null; b.Text = "Ⅱ"; b.AddThemeColorOverride("font_color", Gold); pause.AddChild(b);
        var caption = L("已暂停", 14, Cream); caption.HorizontalAlignment = HorizontalAlignment.Center; pause.AddChild(caption);
        var reveal = CabinetPanel(_ui, "cabinet-body", 1); _navReveal = reveal; Place(reveal, 46, 174, 152, 637); reveal.Visible = false;
        var rows = VBox(reveal, 3);
        foreach (var (tab, label) in new[] { ("Overview", "朝政"), ("Politics", "政治"), ("Industry", "建筑"), ("Markets", "市场"),
            ("Population", "人口"), ("Research", "科技"), ("Diplomacy", "外交"), ("Chronicle", "日志"), ("Atlas", "地区"), ("Leadership", "领袖") })
        {
            string target = tab; var row = PlainOutlineRow(label, () => { _navReveal.Visible = false; ShowTab(target); });
            row.CustomMinimumSize = new Vector2(103, 43); row.AddThemeFontSizeOverride("font_size", 17); rows.AddChild(row);
        }
    }
    private void UpdateNavigationHover()
    {
        if (_navReveal != null && _navReveal.Visible && !new Rect2(0, 174, 156, 467).HasPoint(_ui.GetLocalMousePosition())) _navReveal.Visible = false;
    }
    private Button PlainOutlineRow(string caption, Action action, string icon = "")
    {
        var b = SmallButton(caption, action); b.Alignment = HorizontalAlignment.Left; b.ClipText = true; b.CustomMinimumSize = new Vector2(0, 28);
        b.AddThemeStyleboxOverride("normal", Style(new Color("29293099"), new Color("49463e"), 0, 4));
        if (icon.Length > 0) { b.Icon = UiIcon(icon); b.ExpandIcon = true; b.AddThemeConstantOverride("icon_max_width", 23); }
        return b;
    }
    private VBoxContainer OutlineGroup(string key, string title, int count, bool initiallyOpen = true)
    {
        if (!_outlineExpanded.ContainsKey(key)) _outlineExpanded[key] = initiallyOpen;
        bool expanded = _outlineExpanded[key];
        var heading = SmallButton((expanded ? "⌄ " : "› ") + title + "   " + count, () => { _outlineExpanded[key] = !_outlineExpanded[key]; RefreshOutliner(); });
        heading.Alignment = HorizontalAlignment.Left; heading.AddThemeStyleboxOverride("normal", CabinetSurface("outliner-heading", 4)); _outliner.AddChild(heading);
        var body = VBox(_outliner, 1); body.Visible = expanded; return body;
    }
    private void RefreshReferenceOutliner()
    {
        var p = _engine.Player; var construction = _engine.GetConstructionStatus(); Clear(_outliner);
        _constructionReadout.Text = $"{construction.WeeklyProgress:0.#} / {construction.WeeklyCapacity:0.#} 建造力 / 周\n{p.Construction.Count}座建筑处于建造中";
        var title = PlainOutlineRow("★  国家事务", () => _map.FocusCountry(p.Id)); title.AddThemeColorOverride("font_color", Gold); _outliner.AddChild(title);
        var journal = OutlineGroup("journal", "日志条目", _engine.State.Log.Count);
        foreach (string message in _engine.State.Log.TakeLast(2).Reverse())
        { var b = PlainOutlineRow(Localization.Tr(message), () => ShowTab("Chronicle"), "chronicle"); b.TooltipText = Localization.Tr(message); journal.AddChild(b); }
        if (_engine.State.Log.Count == 0) journal.AddChild(PlainOutlineRow("开启国家纪事 →", () => ShowTab("Chronicle"), "chronicle"));
        var researchBody = OutlineGroup("research", "科技", p.ResearchId.Length > 0 ? 1 : 0);
        var research = Catalog.Technologies.FirstOrDefault(t => t.Id == p.ResearchId);
        researchBody.AddChild(PlainOutlineRow(research == null ? "选择研究方向" : Localization.Tr(research.Name), () => ShowTab("Research"), "research"));
        if (research != null) Meter(researchBody, (double)(100 * p.ResearchProgress / Math.Max(1, research.Days)), Green);
        var builds = OutlineGroup("build", "建造队列", p.Construction.Count);
        foreach (var job in p.Construction.Take(4))
        {
            var city = p.Cities.First(c => c.Id == job.CityId); var forecast = construction.Projects.FirstOrDefault(f => f.Id == job.Id);
            string estimate = forecast?.Paused == true ? "暂停" : forecast?.EstimatedDays.HasValue == true ? forecast.EstimatedDays.Value + "天" : "等待";
            builds.AddChild(PlainOutlineRow(Localization.Tr(city.Name) + " · " + Localization.Tr(job.Name) + "  " + estimate, OpenConstructionQueue, "construction"));
            Meter(builds, (double)(100m * job.ProgressPoints / Math.Max(1m, job.RequiredPoints)), Gold);
        }
        if (p.Construction.Count == 0) builds.AddChild(PlainOutlineRow("规划地方建设 →", () => ShowTab("Industry"), "construction"));
        var market = OutlineGroup("market", "市场", 1); market.AddChild(PlainOutlineRow(Localization.Tr(p.Name) + "市场", () => ShowTab("Markets"), "markets"));
        var goods = Row(market, 3); foreach (string good in Catalog.Goods) goods.AddChild(ConstructionGood(good, ""));
        var regions = OutlineGroup("regions", "地区", p.Regions.Count, false);
        foreach (var region in p.Regions.OrderByDescending(r => r.Population).Take(5))
        { string id = region.Id; regions.AddChild(PlainOutlineRow(Localization.Tr(region.Name) + "   " + Compact(region.Population), () => SelectRegion(id), "atlas")); }
        regions.AddChild(PlainOutlineRow("查看全部地区 →", () => ExploreCountry(p.Id)));
        var diplomacy = OutlineGroup("diplomacy", "外交", p.TradePacts.Count, false); diplomacy.AddChild(PlainOutlineRow("各国关系与领袖 →", () => ShowTab("Diplomacy"), "diplomacy"));
        diplomacy.AddChild(PlainOutlineRow("全球国家索引 →", () => ShowTab("WorldIndex"), "atlas"));
        var society = OutlineGroup("society", "社会", p.Reforms.Count, false); society.AddChild(PlainOutlineRow($"社会矛盾   {p.Unrest:0.0}%", () => ShowTab("Politics"), "population"));
    }

    private void ReferencePolitics() => PoliticalMechanicsPanel(_right);
}
