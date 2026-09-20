using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private readonly Dictionary<string, Texture2D> _constructionPictures = new();
    private string _constructionCategory = "all", _constructionLocationSort = "labor", _constructionSearch = "";
    private string _constructionRegistrySearch = "", _constructionRegistryRegion = "";
    private string _constructionBuildingSort = "name";
    private readonly HashSet<string> _constructionExpanded = new(StringComparer.Ordinal);
    private SimulationEngine? _constructionSession;

    private void EnsureConstructionSession()
    {
        if (ReferenceEquals(_constructionSession, _engine)) return;
        _constructionSession = _engine;
        _constructionSearch = ""; _constructionRegistrySearch = ""; _constructionRegistryRegion = "";
        _constructionCategory = "all"; _constructionLocationSort = "labor"; _constructionBuildingSort = "name";
        _constructionExpanded.Clear();
    }

    // Atlas contract: two columns, three rows; farm, lumber, coal, iron, tools, textile.
    private Texture2D ConstructionPicture(string buildingId)
    {
        if (_constructionPictures.TryGetValue(buildingId, out var cached)) return cached;
        const string atlasPath = "res://Assets/illustrations/buildings.png";
        const string sectorPath = "res://Assets/illustrations/construction.png";
        Texture2D texture;
        if (buildingId == ConstructionCatalog.SectorId && ResourceLoader.Exists(sectorPath)) texture = GD.Load<Texture2D>(sectorPath);
        else if (buildingId != ConstructionCatalog.SectorId && ResourceLoader.Exists(atlasPath))
        {
            var atlas = GD.Load<Texture2D>(atlasPath);
            int index = buildingId switch { "farm" => 0, "lumber" => 1, "coal_mine" => 2, "iron_mine" => 3, "toolworks" => 4, "textile" => 5, _ => -1 };
            if (index < 0) return UiIcon("industry");
            var cell = new Vector2(atlas.GetWidth() / 2f, atlas.GetHeight() / 3f);
            texture = new AtlasTexture { Atlas = atlas, Region = new Rect2(new Vector2(index % 2, index / 2) * cell, cell), FilterClip = true };
        }
        else texture = UiIcon("industry");
        _constructionPictures[buildingId] = texture;
        return texture;
    }

    // Backdrop atlas: two columns, four rows, in the same order as these IDs.
    // Dedicated images take priority; unknown types use the existing site scene.
    private Texture2D ConstructionBackdrop(string id)
    {
        int index = id switch
        {
            "farm" => 0, "lumber" => 1, "coal_mine" => 2, "iron_mine" => 3,
            "toolworks" => 4, "textile" => 5, ConstructionCatalog.SectorId => 6,
            ConstructionCatalog.RailwayId => 7, _ => -1
        };
        if (index < 0) return ConstructionPicture(ConstructionCatalog.SectorId);
        string key = "backdrop:" + id;
        if (_constructionPictures.TryGetValue(key, out var cached)) return cached;
        string path = $"res://Assets/illustrations/panels/building-{id}.png";
        Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        const string atlasPath = "res://Assets/illustrations/panels/building-scenes.png";
        if (texture == null && ResourceLoader.Exists(atlasPath))
        {
            var atlas = GD.Load<Texture2D>(atlasPath);
            if (atlas != null && atlas.GetWidth() >= 2 && atlas.GetHeight() >= 4)
            {
                var cell = new Vector2(atlas.GetWidth() / 2f, atlas.GetHeight() / 4f);
                texture = new AtlasTexture { Atlas = atlas, FilterClip = true,
                    Region = new Rect2(new Vector2(index % 2, index / 2) * cell, cell) };
            }
        }
        texture ??= ConstructionPicture(id == ConstructionCatalog.RailwayId ? ConstructionCatalog.SectorId : id);
        _constructionPictures[key] = texture;
        return texture;
    }

    private Control ConstructionScene(string id, int height = 88, string? badge = null)
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", Style(new Color("171d20"), new Color("837151"), 0, 2));
        var viewport = new Control { CustomMinimumSize = new Vector2(0, height - 4), ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddChild(viewport);
        var picture = new TextureRect { Texture = ConstructionBackdrop(id), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore };
        viewport.AddChild(picture); picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        if (!string.IsNullOrEmpty(badge))
        {
            var plaque = new PanelContainer { Position = new Vector2(8, 8), MouseFilter = Control.MouseFilterEnum.Ignore };
            plaque.AddThemeStyleboxOverride("panel", Style(new Color("222a30"), new Color("a99366"), 1, 4));
            plaque.AddChild(ConstructionText(badge, 12, Cream)); viewport.AddChild(plaque);
        }
        return frame;
    }

    private Control ConstructionThumbnail(string buildingId, int width = 84, int height = 84)
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(width, height), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", Style(new Color("171d20"), new Color("837151"), 0, 2));
        frame.ClipContents = true;
        frame.AddChild(new TextureRect { Texture = ConstructionBackdrop(buildingId), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, CustomMinimumSize = new Vector2(width - 4, height - 4),
            MouseFilter = Control.MouseFilterEnum.Ignore });
        return frame;
    }

    private Label ConstructionText(string text, int size = 13, Color? color = null, bool serif = false, bool expand = false)
    {
        var label = L(text, size, color ?? Cream, serif);
        label.AddThemeFontSizeOverride("font_size", size);
        if (expand)
        {
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            label.ClipText = true;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        return label;
    }

    private Button ConstructionButton(string text, Action action, bool selected = false)
    {
        var button = SmallButton(text, action, selected);
        button.CustomMinimumSize = new Vector2(0, 29);
        button.AddThemeFontSizeOverride("font_size", 12);
        return button;
    }

    private Button ConstructionAddButton(Action action)
    {
        var button = ConstructionButton("＋", action);
        button.CustomMinimumSize = new Vector2(38, 38);
        button.AddThemeFontSizeOverride("font_size", 25);
        button.AddThemeStyleboxOverride("normal", Style(new Color("b49453"), new Color("e6cd8c"), 19, 2));
        button.AddThemeStyleboxOverride("hover", Style(new Color("d4b578"), Cream, 19, 2));
        button.AddThemeStyleboxOverride("pressed", Style(new Color("80663a"), Gold, 19, 2));
        button.AddThemeColorOverride("font_color", new Color("292620"));
        button.AddThemeColorOverride("font_hover_color", new Color("292620"));
        button.AddThemeColorOverride("font_pressed_color", Cream);
        return button;
    }

    private void ConstructionNumber(Node parent, string caption, string value, Color? color = null)
    {
        var column = VBox(parent, 1); column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        column.AddChild(ConstructionText(caption, 11, Muted));
        column.AddChild(ConstructionText(value, 17, color ?? Cream));
    }

    private Control ConstructionGood(string goodId, string value, string? tooltip = null, Color? color = null)
    {
        var row = new HBoxContainer { TooltipText = tooltip ?? Localization.Tr(Title(goodId)), MouseFilter = Control.MouseFilterEnum.Stop };
        row.AddThemeConstantOverride("separation", 4);
        const string goodsPath = "res://Assets/illustrations/goods.png";
        if (Catalog.Goods.Contains(goodId) && ResourceLoader.Exists(goodsPath))
        {
            string key = "good:" + goodId;
            if (!_constructionPictures.TryGetValue(key, out var icon))
            {
                var atlas = GD.Load<Texture2D>(goodsPath);
                int index = goodId switch { "grain" => 0, "timber" => 1, "coal" => 2, "iron" => 3, "tools" => 4, "clothes" => 5, _ => throw new ArgumentOutOfRangeException(nameof(goodId)) };
                var cell = new Vector2(atlas.GetWidth() / 3f, atlas.GetHeight() / 2f);
                icon = new AtlasTexture { Atlas = atlas, Region = new Rect2(new Vector2(index % 3, index / 3) * cell, cell), FilterClip = true };
                _constructionPictures[key] = icon;
            }
            row.AddChild(new TextureRect { Texture = icon, CustomMinimumSize = new Vector2(25, 25), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore });
            row.AddChild(ConstructionText(value, 12, color ?? Cream)); return row;
        }
        var badge = new PanelContainer { CustomMinimumSize = new Vector2(20, 20) };
        badge.AddThemeStyleboxOverride("panel", Style(new Color("302c25"), new Color("736245"), 2, 1));
        var mark = ConstructionText(goodId switch { "grain" => "粮", "timber" => "木", "coal" => "煤", "iron" => "铁", "tools" => "具", "clothes" => "衣", _ => "工" }, 12, Gold);
        mark.HorizontalAlignment = HorizontalAlignment.Center; badge.AddChild(mark); row.AddChild(badge);
        row.AddChild(ConstructionText(value, 12, color ?? Cream));
        return row;
    }

    private static string ConstructionClass(string industryId) => industryId switch
    {
        "farm" => "agriculture", "lumber" or "coal_mine" or "iron_mine" => "resources", _ => "industry"
    };

    private CityState[] ConstructionScopeCities(bool registry = false)
    {
        string region = registry ? _constructionRegistryRegion : _constructionRegion;
        return _engine.Player.Cities.Where(city => region.Length == 0 || city.RegionId == region).ToArray();
    }

    private static long ConstructionIndustryEmployment(IEnumerable<CityState> cities, string industryId) => cities.Sum(city =>
        city.Buildings.TryGetValue(industryId, out var account) ? account.Workers : 0L);

    private Control ConstructionCommodityChip(string goodId, string tooltip, bool output = false)
    {
        var chip = new PanelContainer { CustomMinimumSize = new Vector2(36, 36), TooltipText = tooltip,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        chip.AddThemeStyleboxOverride("panel", Style(new Color("222a32"), output ? new Color("748c69") : new Color("686b6e"), 1, 2));
        string key = "good:" + goodId;
        if (!_constructionPictures.TryGetValue(key, out var texture))
        {
            const string path = "res://Assets/illustrations/goods.png";
            if (ResourceLoader.Exists(path))
            {
                var atlas = GD.Load<Texture2D>(path);
                int index = goodId switch { "grain" => 0, "timber" => 1, "coal" => 2, "iron" => 3, "tools" => 4, "clothes" => 5, _ => -1 };
                if (index >= 0)
                {
                    var cell = new Vector2(atlas.GetWidth() / 3f, atlas.GetHeight() / 2f);
                    texture = new AtlasTexture { Atlas = atlas, Region = new Rect2(new Vector2(index % 3, index / 3) * cell, cell), FilterClip = true };
                    _constructionPictures[key] = texture;
                }
            }
            texture ??= UiIcon("markets");
        }
        chip.AddChild(new TextureRect { Texture = texture, CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore });
        return chip;
    }

    private Control AddConstructionIndustryRow(Control parent, CountryState country, CityState[] cities, IndustryDefinition definition, bool cityOnly = false)
    {
        var group = VBox(parent, 3); group.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var card = CabinetPanel(group, "cabinet-row", 5);
        int levels = cities.Sum(city => city.Industries[definition.Id]);
        var cityIds = cities.Select(city => city.Id).ToHashSet();
        int pending = country.Construction.Count(project => project.IndustryId == definition.Id && cityIds.Contains(project.CityId));
        decimal production = cities.Sum(city => city.Production.GetValueOrDefault(definition.Produces));
        decimal profit = cities.Sum(city => city.Buildings.TryGetValue(definition.Id, out var account) ? account.DailyProfit : 0m);
        decimal wages = cities.Sum(city => city.Buildings.TryGetValue(definition.Id, out var account) ? account.DailyWages : 0m);
        var activeMethods = cities.Where(city => city.Industries[definition.Id] > 0)
            .Select(city => ProductionCatalog.Get(definition.Id, city.Buildings[definition.Id].MethodId)).DistinctBy(method => method.Id).ToArray();
        if (activeMethods.Length == 0) activeMethods = new[] { ProductionCatalog.Get(definition.Id, ProductionCatalog.DefaultMethodId) };
        string recipeSummary = (activeMethods.Length > 1 ? "混合生产方式：" : "生产方式：") + string.Join("、", activeMethods.Select(method => method.Name));
        long employment = ConstructionIndustryEmployment(cities, definition.Id);
        Control bodyParent;
        if (cityOnly)
        {
            var layout = VBox(card, 6);
            layout.AddChild(ConstructionScene(definition.Id, 80,
                $"{levels} 级" + (pending > 0 ? $"  ·  +{pending} 在建" : "")));
            bodyParent = layout;
        }
        else
        {
            card.CustomMinimumSize = new Vector2(0, 118);
            var row = Row(card, 8);
            var picture = ConstructionScene(definition.Id, 104, $"{levels} 级");
            picture.CustomMinimumSize = new Vector2(94, 104);
            picture.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            picture.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(picture);
            bodyParent = row;
        }
        var body = VBox(bodyParent, cityOnly ? 4 : 3); body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var heading = Row(body, 6);
        string type = definition.Id, region = _constructionRegion;
        bool expanded = !cityOnly && _constructionExpanded.Contains(type);
        var title = new Button { Text = (cityOnly ? "" : expanded ? "▾ " : "▸ ") + Localization.Tr(definition.Name),
            Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 25), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        title.AddThemeFontSizeOverride("font_size", 17); title.AddThemeFontOverride("font", _serif);
        title.AddThemeColorOverride("font_color", Cream); title.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        title.AddThemeStyleboxOverride("hover", Style(new Color("39434c"), null, 0, 0)); title.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        title.TooltipText = cityOnly ? "在地图上定位此城市" : "展开各城市的生产方式、工资、利润和所有权"; heading.AddChild(title);
        var value = ConstructionText(Money(profit), 16, profit > 0m ? Green : profit < 0m ? Red : Muted);
        value.TooltipText = "最近一日建筑利润，已扣投入成本与实付工资"; heading.AddChild(value);
        body.AddChild(ConstructionText($"就业 {Compact(employment)}  ·  工资 {Money(wages)}/日" +
            (!cityOnly && pending > 0 ? $"  ·  +{pending} 在建" : ""), 11, Muted, expand: true));
        if (!cityOnly)
        {
            var methodRow = Row(body, 4);
            var methodLabel = ConstructionText(recipeSummary, 11, Muted, expand: true);
            methodLabel.TooltipText = recipeSummary; methodRow.AddChild(methodLabel);
            methodRow.AddChild(ConstructionText($"{ConstructionCatalog.Costs[type]:0} 点/级", 11, Gold));
        }
        var chips = Row(body, 5);
        chips.AddChild(ConstructionCommodityChip(definition.Produces, $"产出：{Localization.Tr(Title(definition.Produces))} {Compact(production)}/日\n最近一日各城市实物产出合计\n{recipeSummary}", true));
        var inputGoods = activeMethods.SelectMany(method => method.Inputs.Keys).Distinct().ToArray();
        if (inputGoods.Length > 0)
        {
            chips.AddChild(ConstructionText("←", 12, Muted));
            foreach (string input in inputGoods)
                chips.AddChild(ConstructionCommodityChip(input, $"投入商品：{Localization.Tr(Title(input))}\n{recipeSummary}\n不同城市用量随配方和就业变化，每级配方见展开详情。"));
        }
        chips.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        if (cityOnly) chips.AddChild(ConstructionText($"{ConstructionCatalog.Costs[type]:0} 点/级", 11, Gold));
        var add = ConstructionAddButton(() => {
            if (cityOnly) Act(() => _engine.BuildInCity(cities[0].Id, type));
            else ChooseConstructionLocations(type, region);
        });
        add.Disabled = country.Id != _engine.Player.Id || _engine.State.Finished || cityOnly && levels + pending >= 100;
        add.TooltipText = $"扩建一级 · {ConstructionCatalog.Costs[type]:0} 建造点" + (cityOnly ? "，加入全国队列" : "，选择建设城市"); chips.AddChild(add);
        card.TooltipText = $"{Localization.Tr(definition.Name)} · 已建{levels}级，队列{pending}级\n实际就业 {Compact(employment)} · 实付工资 {Money(wages)}/日\n右上金额为最近一日利润（扣投入、工资）。\n{recipeSummary}";
        if (cityOnly)
        {
            title.Pressed += () => _map.FocusCity(cities[0].Id);
            BuildingMechanicsRows(group, country, cities[0], definition.Id);
        }
        else
        {
            var details = VBox(group, 1); details.Visible = expanded; bool populated = false;
            void Populate()
            {
                if (populated) return; populated = true;
                foreach (var city in cities.Where(city => city.Industries[type] > 0 || country.Construction.Any(project => project.CityId == city.Id && project.IndustryId == type)).OrderByDescending(city => city.Industries[type]).ThenBy(city => city.Id))
                    AddConstructionCityDetail(details, country, city, definition);
                if (details.GetChildCount() == 0) details.AddChild(ConstructionText("尚无该类建筑，点击＋选择城市扩建。", 12, Muted));
            }
            if (expanded) Populate();
            title.Pressed += () => {
                expanded = !expanded; details.Visible = expanded;
                if (expanded) { _constructionExpanded.Add(type); Populate(); } else _constructionExpanded.Remove(type);
                title.Text = (expanded ? "▾ " : "▸ ") + Localization.Tr(definition.Name);
            };
        }
        return group;
    }

    private void AddConstructionCityDetail(Control parent, CountryState country, CityState city, IndustryDefinition definition)
    {
        var card = CabinetPanel(parent, "cabinet-row", 5); var detail = VBox(card, 6);
        detail.AddChild(ConstructionScene(definition.Id, 64));
        var row = Row(detail, 6);
        var name = ConstructionButton(city.Name, () => _map.FocusCity(city.Id)); name.CustomMinimumSize = new Vector2(0, 26); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.Alignment = HorizontalAlignment.Left; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; row.AddChild(name);
        row.AddChild(ConstructionText($"{city.Industries[definition.Id]}级", 12, Gold));
        row.AddChild(ConstructionText($"产 {Compact(city.Production.GetValueOrDefault(definition.Produces))}", 11, Cream));
        row.AddChild(ConstructionText($"工 {Compact(ConstructionIndustryEmployment(new[] { city }, definition.Id))}", 11, Muted));
        var add = ConstructionButton("＋", () => Act(() => _engine.BuildInCity(city.Id, definition.Id))); add.CustomMinimumSize = new Vector2(28, 26);
        add.Disabled = _engine.State.Finished || country.Id != _engine.Player.Id || city.Industries[definition.Id] + country.Construction.Count(project => project.CityId == city.Id && project.IndustryId == definition.Id) >= 100;
        add.TooltipText = $"在{Localization.Tr(city.Name)}扩建一级，加入全国队列"; row.AddChild(add);
        card.TooltipText = "产：最近一日实物产出；工：该建筑实际就业。展开信息显示生产方式和建筑账户。";
        BuildingMechanicsRows(detail, country, city, definition.Id);
    }

    private void ConstructionScopeSelector()
    {
        var country = _engine.Player;
        bool registry = _constructionPage == "registry";
        string selectedRegion = registry ? _constructionRegistryRegion : _constructionRegion;
        var region = new OptionButton { CustomMinimumSize = new Vector2(0, 32), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        region.AddThemeFontSizeOverride("font_size", 13);
        region.AddThemeStyleboxOverride("normal", CabinetSurface("cabinet-row", 6));
        region.AddItem("全国建筑 · 全部地区");
        foreach (var entry in country.Regions) region.AddItem(Localization.Tr(entry.Name));
        region.Selected = country.Regions.FindIndex(entry => entry.Id == selectedRegion) + 1;
        region.ItemSelected += index =>
        {
            if (country.Id != _engine.Player.Id || index < 0 || index > country.Regions.Count) return;
            string selected = index == 0 ? "" : country.Regions[(int)index - 1].Id;
            if (registry) _constructionRegistryRegion = selected;
            else _constructionRegion = selected;
            _rightScroll.ScrollVertical = 0; RefreshPanels();
        };
        _atlasSelector = region; _right.AddChild(region);
    }

    private void OpenRegionConstruction(string regionId)
    {
        if (!_engine.Player.Regions.Any(region => region.Id == regionId)) return;
        EnsureConstructionSession();
        _constructionRegion = regionId; _constructionType = ""; _constructionPage = "buildings"; _constructionCategory = "all"; _constructionSearch = "";
        ShowTab("Industry");
    }

    private void AddRegionalConstructionPreview(Control parent, CountryState country, RegionState region)
    {
        var cities = country.Cities.Where(city => city.RegionId == region.Id).ToArray();
        var panel = CabinetPanel(parent, "cabinet-row", 7); var row = Row(panel, 9);
        row.AddChild(ConstructionThumbnail(ConstructionCatalog.SectorId, 96, 68));
        var body = VBox(row, 4); body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(ConstructionText(Localization.Tr(region.Name) + " · 地方建设", 16, Gold, true, true));
        body.AddChild(ConstructionText($"{cities.Sum(city => city.Industries.Values.Sum())} 级产业  ·  劳工 {Compact(cities.Sum(city => city.Unemployed))}", 12, Muted, expand: true));
        var enter = ConstructionButton("查看本省建筑  →", () => OpenRegionConstruction(region.Id));
        enter.Disabled = country.Id != _engine.Player.Id; body.AddChild(enter);
    }

    private void AddCityConstructionCards(Control parent, CountryState country, CityState city)
    {
        foreach (var definition in Catalog.Industries)
            AddConstructionIndustryRow(parent, country, new[] { city }, definition, cityOnly: true);
    }

    private void OpenConstructionRegistry()
    {
        EnsureConstructionSession();
        _constructionPage = "registry"; _constructionType = "";
        ShowTab("Industry");
    }

    private void ConstructionRegistry()
    {
        var country = _engine.Player;
        var search = new LineEdit { Text = _constructionRegistrySearch, PlaceholderText = "搜索城市或省份", ClearButtonEnabled = true,
            CustomMinimumSize = new Vector2(0, 35), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        search.AddThemeFontSizeOverride("font_size", 14); _right.AddChild(search);
        ConstructionScopeSelector(); ConstructionTabs();
        var cities = ConstructionScopeCities(registry: true).OrderByDescending(city => city.Industries.Values.Sum()).ThenBy(city => city.Id).ToArray();
        var title = Row(_right, 6); title.AddChild(ConstructionText("建筑注册表", 17, Cream, true, true)); title.AddChild(ConstructionText($"{cities.Length} 座城市", 12, Muted));
        var columns = Row(_right, 3); columns.AddChild(ConstructionText("城市", 12, Muted, expand: true));
        foreach (var definition in Catalog.Industries) columns.AddChild(ConstructionCommodityChip(definition.Produces, Localization.Tr(definition.Name)));
        var rows = new List<(Control Card, string Name)>();
        foreach (var city in cities)
        {
            var card = CabinetPanel(_right, "cabinet-row", 3); var row = Row(card, 3);
            var name = ConstructionButton(city.Name, () => SelectCity(city.Id)); name.CustomMinimumSize = new Vector2(0, 26);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; name.Alignment = HorizontalAlignment.Left; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; row.AddChild(name);
            foreach (var definition in Catalog.Industries)
            {
                var level = ConstructionText(city.Industries[definition.Id].ToString(), 13, city.Industries[definition.Id] > 0 ? Cream : Muted);
                level.CustomMinimumSize = new Vector2(36, 0); level.HorizontalAlignment = HorizontalAlignment.Center; row.AddChild(level);
            }
            rows.Add((card, Localization.Tr(city.Name) + " " + Localization.Tr(country.Regions.First(region => region.Id == city.RegionId).Name)));
            card.TooltipText = $"{Localization.Tr(city.Name)} · 人口{Compact(city.Population)}，可用劳工{Compact(city.Unemployed)}。列值为已建等级，点击城市查看详细建设。";
        }
        var noMatches = ConstructionText(cities.Length == 0 ? "该地区尚未收录城市。" : "未找到匹配城市。", 12, Muted); _right.AddChild(noMatches);
        void Filter(string text)
        {
            _constructionRegistrySearch = text; string query = text.Trim();
            foreach (var row in rows) row.Card.Visible = query.Length == 0 || row.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
            noMatches.Visible = rows.All(row => !row.Card.Visible);
        }
        search.TextChanged += Filter; Filter(_constructionRegistrySearch);
    }
}
