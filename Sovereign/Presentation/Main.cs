using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main : Node3D
{
    private readonly Color Ink = new("182d35"), Cream = new("efe8d8"), Gold = new("d6b879"), Muted = new("9faeaa"), Green = new("9bc5a0"), Red = new("d99583");
    private WorldMap _map = null!;
    private CityView _city = null!;
    private PanelContainer _leftPanel = null!, _rightPanel = null!;
    private Control _atlasHeading = null!, _cityHeading = null!;
    private Control _ui = null!, _modal = null!;
    private VBoxContainer _left = null!, _right = null!;
    private ScrollContainer _leftScroll = null!, _rightScroll = null!;
    private HBoxContainer _metrics = null!, _goods = null!;
    private Label _date = null!, _status = null!, _toast = null!;
    private SimulationEngine _engine = null!;
    private readonly Dictionary<string, Button> _nav = new();
    private readonly List<Button> _speedButtons = new();
    private string _tab = "Overview", _choice = "QNG", _inspect = "", _shownEvent = "";
    private bool _started, _rebuild, _menuOpen;
    private int _speed;
    private double _clock, _refreshClock, _toastTime, _sinceInput = 10;
    private Font _body = null!, _serif = null!;
    private MiniChart _chart = null!;
    private readonly Dictionary<string, string> _nationNotes = new()
    {
        ["GBR"] = "An industrial head start. Feed your growing cities, keep workshops supplied, and decide who shares in prosperity.",
        ["PRU"] = "An economy at a crossroads. Connect coal, iron and skilled labor to build an industrial state.",
        ["JAP"] = "A society poised for change. Develop domestic industry and education while managing the cost of reform."
    };

    public override void _Ready()
    {
        Localization.Initialize(); GetWindow().Title = "万国纪元 · 大清篇 0.14";
        _engine = SimulationEngine.NewGame(_choice);
        _map = new WorldMap(); AddChild(_map);
        _city = new CityView(); AddChild(_city);
        _leader = new LeaderView(); AddChild(_leader);
        _map.CountrySelected += id => { if (_started && !_menuOpen) InspectCountry(id); else { _choice = id; ShowCountryMenu(); } };
        _map.HistoricalCountrySelected += InspectHistoricalCountry;
        _map.ProvinceSelected += id => { if (_started && !_menuOpen) SelectRegion(id); };
        _map.CitySelected += id => { if (_started && !_menuOpen) SelectCity(id); };
        _body = GD.Load<FontFile>("res://Assets/fonts/SourceHanSansSC-Regular.otf");
        _serif = GD.Load<FontFile>("res://Assets/fonts/SourceHanSerifSC-SemiBold.otf");
        var layer = new CanvasLayer { Layer = 5 }; AddChild(layer);
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; layer.AddChild(_ui); _ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ConfigureInterfaceTheme();
        BuildShell(); ShowCountryMenu();
        GetTree().AutoAcceptQuit = false;
        if (OS.GetCmdlineUserArgs().Contains("--preview"))
        {
            StartGame("QNG");
            if (OS.GetCmdlineUserArgs().Contains("--leader")) OpenLeader("QNG");
        }
        if (OS.GetCmdlineUserArgs().Contains("--mechanics-smoke")) RunMechanicsSmoke();
        if (OS.GetCmdlineUserArgs().Contains("--smoke")) { StartGame("PRU"); RunSmoke(); }
        if (OS.GetCmdlineUserArgs().Contains("--content-smoke")) { StartGame("PRU"); RunContentSmoke(); }
        if (OS.GetCmdlineUserArgs().Contains("--profile") || OS.GetCmdlineUserArgs().Contains("--leader-profile")) RunPerformanceSample();
        if (OS.GetCmdlineUserArgs().Contains("--world-smoke")) { StartGame("QNG"); RunWorldSmoke(); }
        if (OS.GetCmdlineUserArgs().Contains("--construction-smoke")) { StartGame("QNG"); RunConstructionSmoke(); }
        if (OS.GetCmdlineUserArgs().Contains("--province-diplomacy-smoke")) { StartGame("QNG"); RunProvinceDiplomacySmoke(); }
        var captureArgs = OS.GetCmdlineUserArgs(); int captureIndex = Array.IndexOf(captureArgs, "--capture");
        if (captureIndex >= 0 && captureIndex + 1 < captureArgs.Length) CapturePreview(captureArgs[captureIndex + 1]);
        int clipIndex = Array.IndexOf(captureArgs, "--leader-clip");
        if (clipIndex >= 0 && clipIndex + 1 < captureArgs.Length) CaptureLeaderClip(captureArgs[clipIndex + 1]);
        int galleryIndex = Array.IndexOf(captureArgs, "--leader-gallery");
        if (galleryIndex >= 0 && galleryIndex + 1 < captureArgs.Length) CaptureLeaderGallery(captureArgs[galleryIndex + 1]);
    }

    private StyleBoxFlat Style(Color bg, Color? border = null, int radius = 3, int pad = 12)
    {
        return new StyleBoxFlat { BgColor = bg, BorderColor = border ?? bg, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = pad, ContentMarginRight = pad, ContentMarginTop = pad, ContentMarginBottom = pad };
    }
    private static float Brightness(Color color) => color.R * .2126f + color.G * .7152f + color.B * .0722f;
    private Label L(string text, int size = 16, Color? color = null, bool serif = false)
    {
        var l = new Label { Text = Localization.Tr(text), MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", Math.Max(12, size)); l.AddThemeColorOverride("font_color", color.HasValue ? (color.Value == Ink ? Cream : Brightness(color.Value) < .55f ? color.Value.Lerp(Cream, .42f) : color.Value) : Cream);
        if (serif) l.AddThemeFontOverride("font", _serif);
        else if (_numbers != null && System.Text.RegularExpressions.Regex.IsMatch(text, @"^[£+−\d][\d.,£+−% /万亿天]*$")) l.AddThemeFontOverride("font", _numbers);
        return l;
    }
    private Label Para(string text, int size = 15, Color? color = null)
    {
        var l = L(text, size, color); l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; return l;
    }
    private Button B(string text, Action action, bool primary = false, bool light = false)
    {
        var b = new Button { Text = Localization.Tr(text), CustomMinimumSize = new Vector2(0, 36), MouseDefaultCursorShape = Control.CursorShape.PointingHand, FocusMode = Control.FocusModeEnum.All };
        b.AddThemeFontSizeOverride("font_size", 15);
        b.AddThemeFontOverride("font", _serif);
        b.AddThemeStyleboxOverride("normal", CabinetSurface(primary ? "button-selected" : "cabinet-row", 7));
        b.AddThemeStyleboxOverride("hover", CabinetSurface("button-hover", 7));
        b.AddThemeStyleboxOverride("pressed", CabinetSurface("button-pressed", 7));
        b.AddThemeStyleboxOverride("focus", Style(new Color(0, 0, 0, 0), Gold, 3, 9));
        b.AddThemeStyleboxOverride("disabled", Style(new Color("302b31"), new Color("4e5141"), 1, 7));
        b.AddThemeColorOverride("font_color", Cream);
        b.AddThemeColorOverride("font_hover_color", Cream);
        b.AddThemeColorOverride("font_pressed_color", Gold); b.AddThemeColorOverride("font_disabled_color", new Color("92968d"));
        b.Pressed += () => action(); return b;
    }
    private PanelContainer Panel(Control parent, Color bg, Color? border = null, int pad = 18)
    {
        if (Brightness(bg) > .65f) { bg = new Color("30343d"); border = new Color("625b4b"); }
        var p = new PanelContainer(); p.AddThemeStyleboxOverride("panel", Style(bg, border, 1, pad)); parent.AddChild(p); return p;
    }
    private VBoxContainer VBox(Node parent, int spacing = 10)
    {
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", spacing); parent.AddChild(v); return v;
    }
    private HBoxContainer Row(Node parent, int spacing = 10)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", spacing); parent.AddChild(h); return h;
    }
    private void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }
    private void Rule(Node n, bool light = false)
    {
        var line = new ColorRect { Color = new Color("66705a"), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore }; n.AddChild(line);
    }
    private void Section(Node n, string text, bool light = false) { n.AddChild(L(text.ToUpperInvariant(), 11, light ? new Color("706b5d") : Gold)); }
    private string Money(decimal value) => (value < 0 ? "−" : "") + "£" + Compact(Math.Abs(value));
    private string Compact(decimal n) => Localization.Number(n);
    private string Title(string s) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Replace('_', ' '));
    private void Pair(Node n, string title, string value, bool light = false, Color? valueColor = null)
    {
        var r = Row(n); var a = L(title, 14, light ? new Color("6c6d61") : Muted); a.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; r.AddChild(a); r.AddChild(L(value, 16, valueColor ?? (light ? Ink : Cream)));
    }
    private void Meter(Node parent, double value, Color color, bool light = false)
    {
        var p = new ProgressBar { Value = Math.Clamp(value, 0, 100), ShowPercentage = false, CustomMinimumSize = new Vector2(0, 5), MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("background", Style(new Color("182b28"), null, 2, 0));
        p.AddThemeStyleboxOverride("fill", Style(color, null, 2, 0)); parent.AddChild(p);
    }

    private void BuildShell() => BuildCabinetShell();

    private void RefreshAll() { RefreshHeader(); RefreshPanels(); UpdateDevelopment(); }
    private void RefreshHeader() => RefreshCabinetHeader();
    private void RefreshPanels()
    {
        if (_rebuild || _quitting) return; _rebuild = true;
        int leftScroll = _leftScroll.ScrollVertical, rightScroll = _rightScroll.ScrollVertical;
        var focusButton = GetViewport().GuiGetFocusOwner() as Button;
        string focused = focusButton?.Text ?? "";
        string focusKey = focusButton?.GetMeta("focus_key", "").AsString() ?? "";
        try
        {
        SuspendEmbeddedPortrait(); _atlasSelector = null; Clear(_right); var p = _engine.Player;
        UpdateCabinetVisibility(); RefreshOutliner();
        switch (_tab)
        {
            case "ForeignCountry": ForeignCountryPanel(); break;
            case "Meeting": MeetingPanel(); break;
            case "WorldCountry": WorldCountryPanel(); break;
            case "WorldIndex": WorldCountryIndex(); break;
            case "Overview": Overview(); break;
            case "Atlas": AtlasPanel(); break;
            case "Population": PopulationPanel(); break;
            case "Leadership": LeadershipPanel(); break;
            case "City": CityPanel(); break;
            case "Industry": Industry(); break;
            case "Markets": Markets(); break;
            case "Politics": Politics(); break;
            case "Research": Research(); break;
            case "Diplomacy": Diplomacy(); break;
            case "Chronicle": Chronicle(); break;
        }
        }
        finally { _rebuild = false; }
        Callable.From(() =>
        {
            if (_quitting || !IsInstanceValid(_leftScroll) || !IsInstanceValid(_rightScroll)) return;
            if (!string.IsNullOrEmpty(focused))
            {
                var match = ButtonsBelow(_left).Concat(ButtonsBelow(_right)).FirstOrDefault(b =>
                    (focusKey.Length > 0 ? b.GetMeta("focus_key", "").AsString() == focusKey : b.Text == focused)
                    && !b.Disabled && b.IsVisibleInTree());
                match?.GrabFocus();
            }
            _leftScroll.ScrollVertical = leftScroll; _rightScroll.ScrollVertical = rightScroll;
        }).CallDeferred();
    }
    private IEnumerable<Button> ButtonsBelow(Node parent)
    {
        foreach (var child in parent.GetChildren()) { if (child is Button button) yield return button; foreach (var descendant in ButtonsBelow(child)) yield return descendant; }
    }
    private void Overview()
    {
        if (_engine.Player.Id == "QNG") { QingOverview(); return; }
        var p = _engine.Player;
        _right.AddChild(Para(GeographyCatalog.Countries.First(c => c.Id == p.Id).Description, 15, Ink)); Section(_right, "THE NATIONAL ACCOUNTS", true);
        Pair(_right, "Annualized output", Money(p.Gdp), true); Pair(_right, "Taxes / day", Money(p.TaxRevenue), true); Pair(_right, "Spending / day", Money(p.Spending), true); Pair(_right, "Public debt", Money(p.Debt), true);
        _chart = new MiniChart { CustomMinimumSize = new Vector2(0, 100), LineColor = new Color("647e68"), FillColor = new Color("354941") };
        _chart.Values = p.History.Select(h => (float)h.Gdp).TakeLast(90).ToArray(); _right.AddChild(_chart); _right.AddChild(L("OUTPUT TREND  /  RECENT OBSERVATIONS", 10, new Color("747669")));
        Rule(_right, true); Section(_right, "A FIRST STEP", true);
        _right.AddChild(Para("Build a farm or workshop, advance time, and watch prices and household wellbeing respond. More production is useful only when you can supply its workers and inputs.", 14, Ink));
        _right.AddChild(B("Explore regions and cities  →", () => ExploreCountry(p.Id), true));
        _right.AddChild(B("Meet the historical leader  →", () => OpenLeader(p.Id), false, true));
        _right.AddChild(B("Develop your industry  →", () => { _tab = "Industry"; RefreshPanels(); }, true));
        ShowQueue();
    }
    private void ShowQueue()
    {
        Section(_right, "全国建造队列");
        ConstructionQueue(_right, _engine.GetConstructionStatus(), false, 3);
        _right.AddChild(SmallButton("管理建造队列 →", OpenConstructionQueue));
    }
    private void Industry() => ConstructionPanel();
    private void Markets() => OrderMarketPanel();
    private void Politics() => ReferencePolitics();
    private void Research()
    {
        _right.AddChild(Para("Knowledge opens new ways to produce. Education makes the long work of discovery faster.", 14, Ink));
        foreach (var d in Catalog.Technologies)
        {
            _right.AddChild(L(d.Name, 21, Ink, true)); _right.AddChild(Para(d.Description, 13, new Color("667063")));
            bool has = _engine.Player.Technologies.Contains(d.Id), active = _engine.Player.ResearchId == d.Id;
            if (active) { Pair(_right, "Research effort", $"{_engine.Player.ResearchProgress:0} / {d.Days} days", true); Meter(_right, (double)_engine.Player.ResearchProgress / Math.Max(1, d.Days) * 100, new Color("8d976b"), true); }
            var b = B(has ? "Discovered ✓" : active ? "Researching…" : $"Research · {d.Days} base days", () => Act(() => _engine.SetResearch(d.Id)), !has && !active, true); b.Disabled = has || active; _right.AddChild(b); Rule(_right, true);
        }
    }
    private void Diplomacy()
    {
        IllustratedHeader(_right, "diplomacy", "外交与列国", "使节往来 · 贸易协定 · 国际关系");
        _right.AddChild(Para("选择国家查看交涉条件，或觐见在位领袖。正式提案会改变关系、国库与贸易。", 13, Muted));
        foreach (var c in _engine.State.Countries.Where(c => c.Id != _engine.Player.Id))
        {
            var card = CabinetPanel(_right, "cabinet-row", 10); var body = VBox(card, 7);
            var identity = Row(body, 12); identity.AddChild(CountryFlag(c.Id, 76, 48));
            var name = VBox(identity, 3); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.AddChild(L(c.Name, 21, Gold, true));
            name.AddChild(Para(Localization.Tr(HistoricalLeaders.Get(c.Id, _engine.State.Date).Name), 13, Cream));
            int relation = _engine.Player.Relations.GetValueOrDefault(c.Id);
            var relationBox = VBox(identity, 2); relationBox.AddChild(L("关系", 11, Muted));
            relationBox.AddChild(L(relation.ToString("+0;-0;0"), 23, relation >= 0 ? Green : Red));
            body.AddChild(Para(_engine.Player.TradePacts.Contains(c.Id) ? "贸易协定已生效" : "尚未签订贸易协定", 12, Muted));
            var row = Row(body, 5);
            var detail = SmallButton("国家与外交", () => InspectCountry(c.Id)); detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(detail);
            detail.SetMeta("focus_key", "diplomacy:" + c.Id + ":country");
            var meeting = SmallButton("觐见领袖", () => MeetLeader(c.Id)); meeting.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(meeting);
            meeting.SetMeta("focus_key", "diplomacy:" + c.Id + ":meeting");
        }
        _right.AddChild(Para("This slice models diplomatic pressure and its costs. Territorial conquest and full military campaigns belong to later milestones.", 12, new Color("717566")));
    }
    private void Chronicle()
    {
        _right.AddChild(Para("Your nation's unfolding story. These records describe this campaign; historical notes in event cards describe the real past.", 14, Ink));
        foreach (var message in _engine.State.Log.TakeLast(25).Reverse()) { _right.AddChild(Para(message, 13, Ink)); Rule(_right, true); }
        if (_engine.State.Log.Count == 0) _right.AddChild(Para("The first page is yours to write.", 17, Ink));
    }

    private void SetSpeed(int speed)
    {
        if (_tab == "Meeting" && speed > 0) { Toast("请先结束会谈，再推进日期。"); return; }
        if (!_started || _menuOpen || _engine.State.PendingEvent != null || _engine.State.Finished) { _speed = 0; return; }
        _speed = speed; _clock = 0; RefreshHeader();
    }
    private void AdvanceOne()
    {
        if (_tab == "Meeting") { Toast("请先结束会谈，再推进日期。"); return; }
        if (!_started || _menuOpen || _engine.State.PendingEvent != null || _engine.State.Finished) return;
        _engine.AdvanceDays(1); RefreshAll(); CheckEvent();
    }
    private void Act(Func<CommandResult> action)
    {
        if (!_started || _menuOpen || _engine.State.PendingEvent != null) return;
        try { var r = action(); Toast(r.Message); RefreshAll(); CheckEvent(); } catch (Exception ex) { Toast("Action could not complete: " + ex.Message); GD.PushError(ex.ToString()); }
    }
    public override void _Process(double delta)
    {
        if (_quitting) return;
        UpdateNavigationHover();
        _sinceInput += delta;
        _toastTime -= delta; if (_toastTime <= 0 && _toast != null) _toast.Text = "";
        if (!_started || _menuOpen) return;
        _refreshClock += delta;
        if (_speed > 0 && _engine.State.PendingEvent == null && !_engine.State.Finished)
        {
            _clock += delta * _speed; int steps = Math.Min(24, (int)_clock);
            if (steps > 0) { _clock -= steps; _engine.AdvanceDays(steps); RefreshHeader(); UpdateDevelopment(); CheckEvent(); }
        }
        if (_engine.State.Finished && _speed != 0) { _speed = 0; Toast("The 1846 milestone is reached. Explore your final accounts or begin a new campaign."); RefreshAll(); }
        // Refresh every screen, preserving its position and keyboard focus, after a short input idle period.
        bool selectorOpen = GodotObject.IsInstanceValid(_atlasSelector) && _atlasSelector!.GetPopup().Visible;
        bool editingText = GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit;
        if (_refreshClock >= 2 && _sinceInput >= 1.5 && _speed > 0 && !selectorOpen && !editingText && _tab is not ("WorldIndex" or "WorldCountry")) { _refreshClock = 0; RefreshPanels(); }
        else if (_refreshClock >= 2 && _sinceInput >= 1.5 && _speed > 0) { _refreshClock = 0; RefreshOutliner(); }
    }
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton || @event is InputEventKey) _sinceInput = 0;
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return;
        if (key.Keycode == Key.Space) { SetSpeed(_speed == 0 ? 1 : 0); GetViewport().SetInputAsHandled(); }
        if (key.Keycode == Key.F5) { Save(); GetViewport().SetInputAsHandled(); }
        if (key.Keycode == Key.F9) { Load(); GetViewport().SetInputAsHandled(); }
        if (key.Keycode == Key.Escape && _modal != null && _engine.State.PendingEvent == null && _started) { CloseModal(); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.Escape && _started && _drawerOpen) { ToggleDrawer(); GetViewport().SetInputAsHandled(); }
        if (_modal == null && _started && key.Keycode == Key.F3) { ShowTab("Industry"); GetViewport().SetInputAsHandled(); }
        if (_modal == null && _started && key.Keycode == Key.F1) { ShowTab("Politics"); GetViewport().SetInputAsHandled(); }
    }
    private void UpdateDevelopment()
    {
        _map.UpdateEconomy(_engine.State.Countries);
        foreach (var c in _engine.State.Countries) _map.SetDevelopment(c.Id, c.Industries.Values.Sum(), c.Technologies.Contains("railways") ? 1 : 0);
        if (_city.IsOpen && !string.IsNullOrEmpty(_selectedCity)) UpdateCityScene();
        if (_leader.IsOpen) UpdateLeaderScene();
    }
    private void OpenCity()
    {
        if (!_started) return;
        if (string.IsNullOrEmpty(_selectedCity)) _selectedCity = GeographyCatalog.Countries.First(c => c.Id == _engine.Player.Id).CapitalCityId;
        OpenCityDetail(_selectedCity);
    }
    private void CloseCity()
    {
        _city.HideCity(); _leader.HideLeader(); _map.Visible = true; _leftPanel.Visible = true; _rightPanel.Visible = true; _atlasHeading.Visible = true; _cityHeading.Visible = false;
        if (_tab is "City" or "Leadership" or "Meeting") _tab = "Atlas";
        if (!string.IsNullOrEmpty(_selectedCity)) _map.FocusCity(_selectedCity);
        else _map.FocusCountry(string.IsNullOrEmpty(_exploreCountry) ? _engine.Player.Id : _exploreCountry);
        RefreshPanels();
    }
    private void Toast(string message) { _toast.Text = Localization.Tr(message); _toastTime = 7; }
    private string SavePath => ProjectSettings.GlobalizePath("user://campaign.json");
    private void Save()
    {
        if (!_started) { Toast("Start a campaign before saving."); return; }
        try
        {
            var path = SavePath; Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path + ".tmp", _engine.SaveJson());
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
            Toast("Campaign saved · F9 to resume this save");
        }
        catch (Exception ex) { Toast("Save failed: " + ex.Message); }
    }
    private void Load()
    {
        try
        {
            if (!File.Exists(SavePath)) { Toast("No saved campaign yet. Use Save or F5 during play."); return; }
            ApplyLoadedGame(SimulationEngine.LoadJson(File.ReadAllText(SavePath)));
            Toast("Campaign loaded · paused");
        }
        catch (Exception ex) { Toast("Could not load save: " + ex.Message); }
    }
    private void ApplyLoadedGame(SimulationEngine loaded)
    {
        _engine = loaded; _started = true; _speed = 0; _clock = 0; _shownEvent = "";
        _tab = "Overview"; _drawerOpen = true; _foreignCountry = ""; _historicalCountryId = ""; _conversation.Clear();
        _constructionRegion = ""; _constructionType = ""; _constructionPage = "buildings";
        _constructionQueuePage = 0; _foreignPage = "overview"; _leaderCountry = "";
        _selectedCity = ""; _selectedRegion = ""; _exploreCountry = _engine.Player.Id;
        CloseModal(); CloseCity(); SetLens(_engine.Player.Id == "QNG" ? "Provinces" : "Political");
        _map.FocusCountry(_engine.Player.Id); RefreshAll(); CheckEvent();
    }
    private VBoxContainer Modal(int width, int height)
    {
        CloseModal(); _speed = 0; _menuOpen = true; RefreshHeader();
        _modal = new Control(); _ui.AddChild(_modal); _modal.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0.035f, 0.07f, 0.085f, 0.86f) }; _modal.AddChild(dim); dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = Panel(_modal, new Color("132a32"), new Color("8b805f"), 32); panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center); panel.OffsetLeft = -width / 2f; panel.OffsetRight = width / 2f; panel.OffsetTop = -height / 2f; panel.OffsetBottom = height / 2f;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; panel.AddChild(scroll); var v = VBox(scroll, 14); v.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; return v;
    }
    private void CloseModal()
    {
        if (_modal != null) { _ui.RemoveChild(_modal); _modal.QueueFree(); _modal = null!; }
        _menuOpen = false;
    }
    private void ShowCountryMenu()
    {
        var v = Modal(1180, 750);
        if (ResourceLoader.Exists("res://Assets/art/industrial-era.png"))
        {
            var art = new TextureRect { Texture = GD.Load<Texture2D>("res://Assets/art/industrial-era.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore };
            _modal.AddChild(art); _modal.MoveChild(art, 0); art.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            foreach (var dim in _modal.GetChildren().OfType<ColorRect>()) dim.Color = new Color(0.025f, 0.045f, 0.065f, 0.25f);
            foreach (var panel in _modal.GetChildren().OfType<PanelContainer>()) panel.AddThemeStyleboxOverride("panel", Style(new Color(0.055f, 0.11f, 0.14f, 0.89f), Gold, 4, 32));
        }
        var title = L("SOVEREIGN", 66, Cream, true); title.HorizontalAlignment = HorizontalAlignment.Center; v.AddChild(title);
        var subtitle = L("工业世纪 / 大清篇 0.14", 14, Gold); subtitle.HorizontalAlignment = HorizontalAlignment.Center; v.AddChild(subtitle);
        var intro = L("Choose a nation. Shape a generation.", 30, Cream, true); intro.HorizontalAlignment = HorizontalAlignment.Center; v.AddChild(intro);
        var selected = GeographyCatalog.Countries.First(c => c.Id == _choice);
        var chooser = new GridContainer { Columns = 4 }; chooser.AddThemeConstantOverride("h_separation", 10); chooser.AddThemeConstantOverride("v_separation", 8); v.AddChild(chooser);
        foreach (var country in GeographyCatalog.Countries)
        {
            string id = country.Id;
            var choose = B(country.Name + (_choice == id ? "  ✓" : ""), () => { _choice = id; _map.FocusCountry(id); ShowCountryMenu(); }, _choice == id);
            choose.Icon = CountryFlagTexture(id); choose.ExpandIcon = true; choose.AddThemeConstantOverride("icon_max_width", 54); choose.AddThemeConstantOverride("h_separation", 12);
            choose.CustomMinimumSize = new Vector2(265, 48); chooser.AddChild(choose);
        }
        Rule(v); v.AddChild(L(selected.Name, 30, Gold, true)); v.AddChild(Para(selected.Description, 17));
        var historical = HistoricalLeaders.Get(_choice, Catalog.StartDate);
        v.AddChild(Para($"1836 leadership: {historical.Name} · {historical.Title}", 16, Cream));
        var lower = Row(v, 16); var note = Para($"{GeographyCatalog.Countries.Count}个可玩国家 · {GeographyCatalog.Regions.Count}个地区 · {GeographyCatalog.Cities.Count}座城市 · 1836—1846\n大清篇：40座城邑的发展与改革。地图疆域参考1815年。", 13, Muted); note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; lower.AddChild(note);
        var begin = B("Begin your campaign  →", () => StartGame(_choice), true); begin.CustomMinimumSize = new Vector2(272, 50); lower.AddChild(begin);
        var r = Row(v, 12); if (_started) r.AddChild(B("Return to current campaign", CloseModal)); if (File.Exists(SavePath)) r.AddChild(B("Continue saved campaign", Load));
        r.AddChild(B("How to play", ShowHelp));
    }
    private void StartGame(string id)
    {
        ApplyLoadedGame(SimulationEngine.NewGame(id));
        Toast("Your campaign begins paused. Build an industry, then press Space to advance time.");
    }
    private void CheckEvent()
    {
        var ev = _engine.State.PendingEvent;
        if (ev == null || ev.Id == _shownEvent) return;
        _shownEvent = ev.Id; _speed = 0; var v = Modal(740, 680);
        Section(v, "THE NATIONAL CHRONICLE  /  " + Localization.Date(_engine.State.Date)); v.AddChild(L(ev.Title, 32, Cream, true)); v.AddChild(Para(ev.Description, 17));
        Rule(v); Section(v, "HISTORICAL CONTEXT"); v.AddChild(Para(ev.HistoricalContext, 14, Muted));
        if (!string.IsNullOrWhiteSpace(ev.SourceUrl)) { var source = B("Read the historical source  ↗", () => { if (ev.SourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) OS.ShellOpen(ev.SourceUrl); }); v.AddChild(source); }
        Rule(v);
        var feedback = Para("", 14, Red); v.AddChild(feedback);
        foreach (var option in ev.Options)
        {
            v.AddChild(Para(option.Description, 13, Muted)); var b = B(option.Label, () =>
            {
                var result = _engine.ResolveEvent(option.Id); if (result.Success) { _shownEvent = ""; CloseModal(); RefreshAll(); Toast(result.Message + " · Press Space to continue."); CheckEvent(); } else feedback.Text = Localization.Tr(result.Message);
            }, true); v.AddChild(b);
        }
    }
    private void ShowHelp()
    {
        var v = Modal(820, 650); Section(v, "FIELD GUIDE"); v.AddChild(L("The work of a government", 34, Cream, true));
        foreach (var (heading, body) in new[] {
            ("01  建立经济", "打开建设，选择建筑类型与建造地点。所有项目共享全国建造力，施工持续消耗材料和资金。队列可暂停、调整优先级或取消；营建部门提供额外建造力，闲置时仍支付工资。"),
            ("02  Follow the consequences", "Start time with Space or the speed controls. Watch goods, public finances and living standards. Use Markets to import shortages or export a surplus."),
            ("03  治理与改革", "在政治页调整税率、组织政府并提出法案。法律须通过阶段审议，再以官僚预算维持机构。研究解锁新的生产方式，需要到建筑页面选择启用。"),
            ("04  Drill into your country", "Open Atlas to choose a country, region, and city. Enter any city in 3D, inspect population and jobs, and construct local industries. Housing, Industry, Civic, and Waterfront buttons move to districts. Click map city markers to inspect them."),
            ("05  历史人物", "人物档案依日期显示在任领袖与资料出处。当前12国的20位历史领袖均有全身骨骼模型、眨眼与交谈动作；伊莎贝拉二世分儿童和少女阶段。可切换全身、面容视角，并进行外交会谈。造型仍为历史资料基础上的美术重建。"),
            ("05  Keep your progress", "Save with F5, load with F9. Saves live in the game's Windows application-data folder. This milestone ends on 1 January 1846.") })
        { v.AddChild(L(heading, 21, Gold, true)); v.AddChild(Para(body, 15)); }
        v.AddChild(Para("当前为原创简化开发版：12国、128城、6类商品，战役至1846年。已接入职业阶层、利益集团、法律和私人投资；完整文化宗教、世界贸易、战争与全球历史内容仍待开发。", 13, Muted));
        v.AddChild(B(_started ? "Return to the atlas" : "Choose your nation", () => { if (_started) CloseModal(); else ShowCountryMenu(); }, true));
    }
    private async void RunSmoke()
    {
        try
        {
            var result = _engine.Build("farm"); _engine.AdvanceDays(30);
            if (_engine.State.PendingEvent != null) _engine.ResolveEvent(_engine.State.PendingEvent.Options[0].Id);
            var copy = SimulationEngine.LoadJson(_engine.SaveJson());
            if (copy.State.Date != _engine.State.Date) throw new Exception("Save round trip date mismatch");
            foreach (string tab in new[] { "Atlas", "Population", "Leadership", "Industry", "Markets", "Politics", "Research", "Diplomacy", "Chronicle", "Overview" }) { _tab = tab; RefreshAll(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); AuditChinese(_ui); }
            foreach (var nation in GeographyCatalog.Countries) { _selectedCity = nation.CapitalCityId; OpenCity(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); AuditChinese(_ui); CloseCity(); OpenLeader(nation.Id); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); AuditChinese(_ui); CloseCity(); }
            _engine.AdvanceDays(40); CheckEvent();
            if (_engine.State.PendingEvent == null) throw new Exception("Expected the first historical event for the Chinese interface check");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); AuditChinese(_ui);
            CloseModal(); ShowHelp(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); AuditChinese(_ui);
            ShowCountryMenu(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); AuditChinese(_ui);
            GD.Print("SOVEREIGN_SMOKE_PASS date=" + _engine.State.Date.ToString("yyyy-MM-dd") + " build=" + result.Success);
            RequestQuit(0);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); RequestQuit(1); }
    }
    private async void CapturePreview(string path)
    {
        bool cityCapture = OS.GetCmdlineUserArgs().Contains("--city");
        var args = OS.GetCmdlineUserArgs(); int nationArg = Array.IndexOf(args, "--nation"); StartGame(nationArg >= 0 && nationArg + 1 < args.Length ? args[nationArg + 1] : "QNG"); _engine.Build("farm"); _engine.Build("textile"); _engine.SetResearch(cityCapture ? "railways" : "mechanical_tools");
        for (int i = 0; i < (cityCapture ? 600 : 90); i++) { if (_engine.State.PendingEvent != null) _engine.ResolveEvent(_engine.State.PendingEvent.Options.Last().Id); _engine.AdvanceDays(1); }
        if (_engine.State.PendingEvent != null) _engine.ResolveEvent(_engine.State.PendingEvent.Options.Last().Id);
        RefreshAll(); _toast.Text = ""; _toastTime = 0;
        if (args.Contains("--city")) OpenCity();
        int settlementArg = Array.IndexOf(args, "--settlement");
        if (settlementArg >= 0 && settlementArg + 1 < args.Length) OpenCityDetail(args[settlementArg + 1]);
        if (args.Contains("--leader")) OpenLeader(_engine.Player.Id);
        if (args.Contains("--leader-full")) { OpenLeader(_engine.Player.Id); _leader.SetFraming("full", true); }
        if (args.Contains("--leader-face")) { OpenLeader(_engine.Player.Id); _leader.SetFraming("face", true); }
        if (args.Contains("--leader-side")) { OpenLeader(_engine.Player.Id); _leader.SetFraming("full", true); _leader.SetViewAngle(.95f); }
        if (args.Contains("--leader-back")) { OpenLeader(_engine.Player.Id); _leader.SetFraming("full", true); _leader.SetViewAngle(Mathf.Pi); }
        if (args.Contains("--leader-panel")) ShowTab("Leadership");
        int foreignArg = Array.IndexOf(args, "--foreign");
        if (foreignArg >= 0 && foreignArg + 1 < args.Length) InspectCountry(args[foreignArg + 1]);
        int meetingArg = Array.IndexOf(args, "--meeting");
        if (meetingArg >= 0 && meetingArg + 1 < args.Length) MeetLeader(args[meetingArg + 1]);
        int provinceArg = Array.IndexOf(args, "--province");
        if (provinceArg >= 0 && provinceArg + 1 < args.Length) SelectRegion(args[provinceArg + 1]);
        int pageArg = Array.IndexOf(args, "--province-page");
        if (pageArg >= 0 && pageArg + 1 < args.Length) { _provincePage = args[pageArg + 1]; RefreshPanels(); }
        if (args.Contains("--politics")) ShowTab("Politics");
        int politicalPageArg = Array.IndexOf(args, "--political-page");
        if (politicalPageArg >= 0 && politicalPageArg + 1 < args.Length) { _politicalView = args[politicalPageArg + 1]; ShowTab("Politics"); }
        if (args.Contains("--markets")) ShowTab("Markets");
        if (args.Contains("--atlas")) ExploreCountry(_engine.Player.Id);
        if (args.Contains("--closed")) { _drawerOpen = false; UpdateCabinetVisibility(); }
        if (args.Contains("--population")) { _tab = "Population"; RefreshPanels(); }
        if (args.Contains("--population-overview")) { _popView = "overview"; ShowTab("Population"); }
        if (args.Contains("--diplomacy")) ShowTab("Diplomacy");
        if (args.Contains("--world-index")) ShowTab("WorldIndex");
        if (args.Contains("--construction"))
        {
            if (_engine.Player.Id == "QNG")
            {
                _engine.BuildInCity("QNG_suzhou", "textile"); _engine.BuildInCity("QNG_canton", "lumber");
                _engine.BuildConstructionSector("QNG_tianjin"); _engine.BuildInCity("QNG_chengdu", "farm");
            }
            _constructionPage = args.Contains("--queue") ? "queue" : args.Contains("--sectors") ? "sectors" : "buildings";
            _constructionType = args.Contains("--build-locations") ? "textile" : "";
            ShowTab("Industry");
        }
        int historicalArg = Array.IndexOf(args, "--historical");
        if (historicalArg >= 0 && historicalArg + 1 < args.Length) InspectHistoricalCountry(args[historicalArg + 1]);
        int lensArg = Array.IndexOf(args, "--lens");
        if (lensArg >= 0 && lensArg + 1 < args.Length) SetLens(args[lensArg + 1]);
        if (args.Contains("--event")) { StartGame(_engine.Player.Id); _engine.AdvanceDays(70); RefreshAll(); CheckEvent(); }
        int regionalBuild = Array.IndexOf(args, "--region-construction");
        if (regionalBuild >= 0 && regionalBuild + 1 < args.Length) OpenRegionConstruction(args[regionalBuild + 1]);
        if (args.Contains("--production-methods"))
        {
            _constructionCategory = "industry"; _constructionExpanded.Add("textile");
            _engine.Player.Technologies.Add("mechanical_tools");
            _engine.SetProductionMethod("QNG_suzhou", "textile", "mechanized");
            ShowTab("Industry");
        }
        if (args.Contains("--menu")) ShowCountryMenu();
        if (args.Contains("--help")) ShowHelp();
        _toast.Text = ""; _toastTime = 0;
        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var result = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print("SOVEREIGN_CAPTURE " + result + " fps=" + Engine.GetFramesPerSecond()); RequestQuit(result == Error.Ok ? 0 : 1);
    }
}

public partial class MiniChart : Control
{
    public float[] Values = Array.Empty<float>();
    public Color LineColor = new("9bc5a0"), FillColor = new("32494e");
    public override void _Draw()
    {
        float w = Size.X, h = Size.Y - 12;
        for (int i = 1; i <= 3; i++) DrawLine(new Vector2(0, h * i / 4), new Vector2(w, h * i / 4), new Color("c9c5b7"), 1);
        if (Values.Length < 2) { DrawLine(new Vector2(0, h * 0.55f), new Vector2(w, h * 0.55f), LineColor, 2); return; }
        float min = Values.Min(), max = Values.Max(); float spread = Math.Max(1, max - min); min -= spread * 0.12f; max += spread * 0.12f;
        var pts = new Vector2[Values.Length]; for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(w * i / (pts.Length - 1), h - (Values[i] - min) / (max - min) * h);
        var fill = pts.Concat(new[] { new Vector2(w, h), new Vector2(0, h) }).ToArray(); DrawColoredPolygon(fill, FillColor); DrawPolyline(pts, LineColor, 2, true);
    }
}
