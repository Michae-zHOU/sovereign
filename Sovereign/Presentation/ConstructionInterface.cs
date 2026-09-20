using Godot;
using System;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private string _constructionPage = "buildings", _constructionType = "", _constructionRegion = "";
    private int _constructionQueuePage;

    private void ConstructionPanel()
    {
        EnsureConstructionSession();
        var country = _engine.Player;
        if (_constructionRegion.Length > 0 && country.Regions.All(region => region.Id != _constructionRegion)) _constructionRegion = "";
        if (_constructionPage == "buildings" && _constructionType.Length == 0) { ConstructionBuildings(); return; }
        if (_constructionPage == "registry") { ConstructionRegistry(); return; }
        ConstructionTabs();
        if (_constructionPage == "buildings") { ConstructionLocations(); return; }
        var status = _engine.GetConstructionStatus();
        var account = Row(_right, 8);
        account.AddChild(ConstructionText($"建造力 {status.WeeklyProgress:0.#}/{status.WeeklyCapacity:0.#} 每周", 12, Gold, expand: true));
        account.AddChild(ConstructionText($"政府 {Money(status.WeeklyGovernmentCost)}/周", 12, Red));
        account.TooltipText = $"全国共享建造力。材料 {Money(status.WeeklyMaterialCost)}/周，工资 {Money(status.WeeklyWages)}/周。";
        if (country.ConstructionPaused) _right.AddChild(ConstructionText("政府建造已暂停", 12, Gold));
        else if (status.ShortageFactor < .999m) _right.AddChild(ConstructionText($"供料不足 · 营建产能利用 {status.ShortageFactor * 100:0}%", 12, Red));
        if (_constructionPage == "queue") ConstructionQueue(_right, status, true);
        else ConstructionSectorsPanel(status);
    }

    private void ConstructionTabs()
    {
        var tabs = Row(_right, 2);
        foreach (var (id, name) in new[] { ("buildings", "建筑"), ("queue", $"建造队列 {_engine.Player.Construction.Count}"), ("sectors", "营建部门") })
        {
            var button = ConstructionButton(name, () => { _constructionPage = id; _rightScroll.ScrollVertical = 0; RefreshPanels(); }, _constructionPage == id);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; button.CustomMinimumSize = new Vector2(0, 30); tabs.AddChild(button);
        }
    }

    private void ConstructionBuildings()
    {
        var country = _engine.Player; var cities = ConstructionScopeCities();
        var search = new LineEdit { Text = _constructionSearch, PlaceholderText = "搜索建筑", ClearButtonEnabled = true,
            CustomMinimumSize = new Vector2(0, 35), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        search.AddThemeFontSizeOverride("font_size", 14); _right.AddChild(search);
        ConstructionScopeSelector();
        ConstructionTabs();
        var categories = Row(_right, 2);
        foreach (var (id, name) in new[] { ("all", "全部"), ("agriculture", "农业"), ("resources", "资源"), ("industry", "工业") })
        {
            var button = ConstructionButton(name, () => { _constructionCategory = id; RefreshPanels(); }, _constructionCategory == id);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; button.CustomMinimumSize = new Vector2(0, 30); categories.AddChild(button);
        }
        var sorting = Row(_right, 5); sorting.CustomMinimumSize = new Vector2(0, 24);
        sorting.AddChild(ConstructionText(_constructionRegion.Length == 0 ? "全国建筑" : country.Regions.First(region => region.Id == _constructionRegion).Name, 12, Muted, expand: true));
        foreach (var (id, caption) in new[] { ("name", "默认"), ("levels", "等级 ↓"), ("value", "产值 ↓") })
        {
            var button = ConstructionButton(caption, () => { _constructionBuildingSort = id; RefreshPanels(); }, _constructionBuildingSort == id);
            button.CustomMinimumSize = new Vector2(0, 24); sorting.AddChild(button);
        }
        if (cities.Length == 0) { _right.AddChild(ConstructionText("该地区尚无可建设城市。", 13, Muted)); return; }
        var rows = new System.Collections.Generic.List<(Control Card, string Name)>();
        var definitions = Catalog.Industries.Where(definition => _constructionCategory == "all" || ConstructionClass(definition.Id) == _constructionCategory);
        definitions = _constructionBuildingSort == "levels" ? definitions.OrderByDescending(definition => cities.Sum(city => city.Industries[definition.Id])) :
            _constructionBuildingSort == "value" ? definitions.OrderByDescending(definition => cities.Sum(city => city.Production.GetValueOrDefault(definition.Produces)) * country.Prices[definition.Produces]) : definitions;
        foreach (var definition in definitions)
        {
            var card = AddConstructionIndustryRow(_right, country, cities, definition);
            rows.Add((card, Localization.Tr(definition.Name) + " " + definition.Name));
        }
        var noMatches = ConstructionText("未找到匹配建筑。", 12, Muted); _right.AddChild(noMatches);
        void FilterRows(string text)
        {
            _constructionSearch = text; string query = text.Trim();
            foreach (var item in rows) item.Card.Visible = query.Length == 0 || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
            noMatches.Visible = rows.Count > 0 && rows.All(item => !item.Card.Visible);
        }
        search.TextChanged += FilterRows; FilterRows(_constructionSearch);
    }

    private void ChooseConstructionLocations(string type, string? region = null)
    {
        if (!ConstructionCatalog.Costs.ContainsKey(type)) { Toast("未知建筑类型。"); return; }
        if (!string.IsNullOrEmpty(region) && !_engine.Player.Regions.Any(entry => entry.Id == region))
        {
            Toast("只能在本国地区安排建设。"); return;
        }
        _constructionPage = "buildings"; _constructionType = type; _constructionRegion = region ?? "";
        if (_city.IsOpen || _leader.IsOpen) CloseCity();
        _drawerOpen = true; _tab = "Industry"; _rightScroll.ScrollVertical = 0; RefreshPanels();
    }

    private void ConstructionLocations()
    {
        var country = _engine.Player;
        _right.AddChild(ConstructionButton("← 返回建筑列表", () => { _constructionType = ""; _rightScroll.ScrollVertical = 0; RefreshPanels(); }));
        var header = CabinetPanel(_right, "cabinet-row", 6); var hero = Row(header, 10);
        hero.AddChild(ConstructionThumbnail(_constructionType));
        var detail = VBox(hero, 5); detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        detail.AddChild(ConstructionText(ConstructionCatalog.Name(_constructionType), 17, Cream, true, true));
        detail.AddChild(ConstructionText($"{ConstructionCatalog.Costs[_constructionType]:0} 建造点 / 级", 14, Gold));
        detail.AddChild(ConstructionText(_constructionType == ConstructionCatalog.SectorId ? "每级 1,000 个营建岗位" : "基础方式每级 2,500 岗位；生产方式会改变用工", 12, Muted));
        ConstructionScopeSelector();
        var sorting = Row(_right, 4); sorting.AddChild(ConstructionText("比较建设地点", 13, Gold, expand: true));
        foreach (var (id, name) in new[] { ("labor", "劳工 ↓"), ("population", "人口 ↓") })
            sorting.AddChild(ConstructionButton(name, () => { _constructionLocationSort = id; RefreshPanels(); }, _constructionLocationSort == id));
        var columns = Row(_right, 4); columns.AddChild(ConstructionText("城市 / 地区", 11, Muted, expand: true));
        foreach (var (name, width) in new[] { ("人口", 58), ("可用劳工", 65), ("等级", 46), ("建造", 32) })
        {
            var caption = ConstructionText(name, 11, Muted); caption.CustomMinimumSize = new Vector2(width, 0); caption.HorizontalAlignment = HorizontalAlignment.Center; columns.AddChild(caption);
        }
        var cities = ConstructionScopeCities();
        var ordered = _constructionLocationSort == "population" ? cities.OrderByDescending(city => city.Population).ThenBy(city => city.Id) : cities.OrderByDescending(city => city.Unemployed).ThenBy(city => city.Id);
        foreach (var city in ordered)
        {
            int levels = _constructionType == ConstructionCatalog.SectorId ? city.ConstructionSectors : city.Industries[_constructionType];
            int pending = country.Construction.Count(p => p.CityId == city.Id && p.IndustryId == _constructionType);
            var card = CabinetPanel(_right, "cabinet-row", 5); var top = Row(card, 4);
            var label = VBox(top, 1); label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var name = ConstructionButton(Localization.Tr(city.Name), () => _map.FocusCity(city.Id));
            name.Alignment = HorizontalAlignment.Left; name.TooltipText = "在地图上定位城市"; name.CustomMinimumSize = new Vector2(0, 24);
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; label.AddChild(name);
            label.AddChild(ConstructionText(country.Regions.First(region => region.Id == city.RegionId).Name, 10, Muted, expand: true));
            foreach (var (value, width, color) in new[] { (Compact(city.Population), 58, Cream), (Compact(city.Unemployed), 65, city.Unemployed > 2500 ? Green : Gold), ($"{levels}" + (pending > 0 ? $" +{pending}" : ""), 46, Gold) })
            {
                var number = ConstructionText(value, 12, color); number.CustomMinimumSize = new Vector2(width, 0); number.HorizontalAlignment = HorizontalAlignment.Center;
                number.TooltipText = width == 46 ? $"已建 {levels} 级，队列中 {pending} 级" : width == 65 ? "当前城市可用劳工" : "当前城市人口"; top.AddChild(number);
            }
            string targetCity = city.Id, type = _constructionType;
            var add = ConstructionButton("＋", () => Act(() => type == ConstructionCatalog.SectorId ? _engine.BuildConstructionSector(targetCity) : _engine.BuildInCity(targetCity, type)));
            add.CustomMinimumSize = new Vector2(32, 32); add.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; add.Disabled = _engine.State.Finished || levels + pending >= 100;
            add.TooltipText = $"在{Localization.Tr(city.Name)}扩建一级{Localization.Tr(ConstructionCatalog.Name(type))}，加入全国队列"; top.AddChild(add);
        }
        if (cities.Length == 0) _right.AddChild(Para("该地区尚无收录城市，请选择其他地区。", 12, Muted));
    }

    private void ConstructionQueue(Node parent, ConstructionStatus status, bool controls, int limit = int.MaxValue)
    {
        var country = _engine.Player;
        if (controls)
        {
            var controlRow = Row(parent, 5);
            var toggle = ConstructionButton(country.ConstructionPaused ? "▶ 恢复全国施工" : "Ⅱ 暂停全国施工", () => Act(() => _engine.SetConstructionPaused(!country.ConstructionPaused)));
            toggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; toggle.Disabled = _engine.State.Finished; controlRow.AddChild(toggle);
            controlRow.AddChild(ConstructionButton("＋ 添加建筑", () => { _constructionPage = "buildings"; _constructionType = ""; RefreshPanels(); }));
        }
        if (controls)
        {
            var pool = Row(parent, 5);
            pool.AddChild(ConstructionText($"投资池 {Money(country.InvestmentPool)} · 今日投入 {Money(country.DailyPrivateConstructionSpending)}", 12, Gold, expand: true));
            pool.AddChild(ConstructionButton(country.PrivateConstructionEnabled ? "暂停私人投资" : "恢复私人投资", () => Act(() => _engine.SetPrivateConstructionEnabled(!country.PrivateConstructionEnabled))));
            parent.AddChild(ConstructionText($"私人支出 {Money(status.WeeklyPrivateCost)}/周 · 政府与私人共享空闲建造力", 11, Muted));
        }
        if (country.Construction.Count == 0)
        {
            var empty = CabinetPanel((Control)parent, "cabinet-row", 8); var row = Row(empty, 10);
            row.AddChild(ConstructionThumbnail(ConstructionCatalog.SectorId, 96, 66));
            var body = VBox(row, 5); body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            body.AddChild(ConstructionText("尚无施工计划", 17, Cream, true));
            body.AddChild(ConstructionText("选择建筑与地点，安排全国建设。", 12, Muted, expand: true)); return;
        }
        const int pageSize = 8;
        int pageCount = Math.Max(1, (country.Construction.Count + pageSize - 1) / pageSize);
        _constructionQueuePage = Math.Clamp(_constructionQueuePage, 0, pageCount - 1);
        int start = controls ? _constructionQueuePage * pageSize : 0;
        int end = Math.Min(country.Construction.Count, controls ? start + pageSize : limit);
        if (controls && pageCount > 1)
        {
            var pages = Row(parent, 5);
            var previous = SmallButton("←", () => { _constructionQueuePage--; _rightScroll.ScrollVertical = 0; RefreshPanels(); }); previous.Disabled = _constructionQueuePage == 0; pages.AddChild(previous);
            var caption = L($"第 {_constructionQueuePage + 1} / {pageCount} 页 · 每页 8 项", 12, Muted); caption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; caption.HorizontalAlignment = HorizontalAlignment.Center; pages.AddChild(caption);
            var next = SmallButton("→", () => { _constructionQueuePage++; _rightScroll.ScrollVertical = 0; RefreshPanels(); }); next.Disabled = _constructionQueuePage + 1 >= pageCount; pages.AddChild(next);
        }
        var forecasts = status.Projects.ToDictionary(p => p.Id);
        for (int index = start; index < end; index++)
        {
            var project = country.Construction[index]; var forecast = forecasts[project.Id];
            var city = country.Cities.First(c => c.Id == project.CityId);
            var card = CabinetPanel((Control)parent, "cabinet-row", 3); var box = VBox(card, 3); var row = Row(box, 7);
            row.AddChild(ConstructionThumbnail(project.IndustryId, controls ? 70 : 54, controls ? 54 : 46));
            var detail = VBox(row, 2); detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var title = Row(detail, 4); title.AddChild(ConstructionText($"{index + 1}  {Localization.Tr(project.Name)}", 15, Cream, true, true)); title.AddChild(ConstructionText("＋1级", 11, Gold));
            string state = forecast.Paused ? "已暂停" : forecast.WeeklyProgress == 0 ? "等待建造力" : $"{forecast.WeeklyProgress:0.#} 点/周";
            var stateRow = Row(detail, 5); stateRow.AddChild(ConstructionText(city.Name, 12, Muted, expand: true)); stateRow.AddChild(ConstructionText(state, 12, forecast.Paused ? Muted : Gold));
            Meter(detail, project.RequiredPoints > 0 ? (double)(100m * project.ProgressPoints / project.RequiredPoints) : 0, Gold);
            card.TooltipText = $"{project.ProgressPoints:0.#} / {project.RequiredPoints:0} 建造点 · " +
                (forecast.EstimatedDays.HasValue ? $"按当前速度约{forecast.EstimatedDays}天" : "尚未获得可用建造力") +
                (project.LegacyPrepaid ? "\n旧存档已预付，不重复收取材料费。" : "\n工期随队列和供料变化。");
            if (!controls) continue;
            if (project.PrivateInvestment) { box.AddChild(ConstructionText("私人投资 · 由投资池筹资，投资者自主排程", 11, Gold)); continue; }
            var actions = Row(box, 4); string id = project.Id; int position = index;
            var up = ConstructionButton("↑", () => Act(() => _engine.MoveConstructionProject(id, position - 1))); up.Disabled = index == 0 || _engine.State.Finished; up.TooltipText = "提高施工优先级"; actions.AddChild(up);
            var down = ConstructionButton("↓", () => Act(() => _engine.MoveConstructionProject(id, position + 1))); down.Disabled = index == country.Construction.Count - 1 || _engine.State.Finished; down.TooltipText = "降低施工优先级"; actions.AddChild(down);
            var location = ConstructionButton(Localization.Tr(city.Name) + " ↗", () => _map.FocusCity(city.Id)); location.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; location.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; actions.AddChild(location);
            var pause = ConstructionButton(project.Paused ? "恢复" : "暂停", () => Act(() => _engine.SetProjectPaused(id, !project.Paused))); pause.Disabled = _engine.State.Finished; actions.AddChild(pause);
            var cancel = ConstructionButton("取消", () => Act(() => _engine.CancelConstruction(id))); cancel.Disabled = _engine.State.Finished; cancel.TooltipText = "移除项目；已消耗的材料和工程进度不返还"; actions.AddChild(cancel);
            foreach (var button in actions.GetChildren().OfType<Button>()) button.CustomMinimumSize = new Vector2(0, 25);
        }
        if (controls) parent.AddChild(ConstructionText($"全国共享建造力 · 单项目上限 {ConstructionCatalog.MaxProjectWeeklyPoints:0} 点/周", 11, Muted));
    }

    private void ConstructionSectorsPanel(ConstructionStatus status)
    {
        var country = _engine.Player;
        var overview = CabinetPanel(_right, "cabinet-row", 6); var top = Row(overview, 10);
        top.AddChild(ConstructionThumbnail(ConstructionCatalog.SectorId));
        var totals = VBox(top, 4); totals.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        totals.AddChild(ConstructionText($"营建部门  ·  {country.Cities.Sum(city => city.ConstructionSectors)} 级", 18, Cream, true, true));
        totals.AddChild(ConstructionText($"在岗工人 {Compact(country.Cities.Sum(city => city.ConstructionWorkers))}", 12, Muted));
        totals.AddChild(ConstructionButton("＋ 选择地点扩建营建部门", () => ChooseConstructionLocations(ConstructionCatalog.SectorId)));
        var supplyTitle = Row(_right, 5); supplyTitle.AddChild(ConstructionText("施工材料", 15, Gold, true, true)); supplyTitle.AddChild(ConstructionText("实耗 / 需求 · 每周", 11, Muted));
        foreach (var input in status.Inputs)
        {
            var supply = CabinetPanel(_right, "cabinet-row", 5); var row = Row(supply, 8);
            row.AddChild(ConstructionGood(input.GoodId, Localization.Tr(Title(input.GoodId))));
            var amount = ConstructionText($"{input.WeeklyUsed:0.#} / {input.WeeklyDemand:0.#}", 13, input.WeeklyUsed < input.WeeklyDemand ? Red : Cream, expand: true); amount.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(amount);
            var price = ConstructionText($"£{input.UnitPrice:0.00}", 12, Gold); price.CustomMinimumSize = new Vector2(61, 0); price.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(price);
            supply.TooltipText = $"市场卖单 {Compact(input.Stock)}；短缺降低营建产出，投入以买单结算";
        }
        _right.AddChild(ConstructionText("地方部门与生产方式", 15, Gold, true));
        foreach (var city in country.Cities.Where(c => c.ConstructionSectors > 0))
        {
            var method = ConstructionCatalog.Method(city.ConstructionMethodId);
            decimal staffed = city.ConstructionWorkers / (decimal)ConstructionCatalog.WorkersPerSector;
            var card = CabinetPanel(_right, "cabinet-row", 3); var row = Row(card, 7);
            row.AddChild(ConstructionThumbnail(ConstructionCatalog.SectorId));
            var body = VBox(row, 2); body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var heading = Row(body, 5);
            heading.AddChild(ConstructionText($"{Localization.Tr(city.Name)} · {city.ConstructionSectors}级", 15, Cream, true, true));
            heading.AddChild(ConstructionText($"{staffed * method.WeeklyPoints:0.#} 点/周", 13, Gold));
            var methods = Row(body, 3);
            foreach (var productionMethod in ConstructionCatalog.Methods)
            {
                bool selected = city.ConstructionMethodId == productionMethod.Id;
                var button = ConstructionButton(productionMethod.Name, () => Act(() => _engine.SetConstructionMethod(city.Id, productionMethod.Id)), selected);
                button.CustomMinimumSize = new Vector2(0, 24); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                button.AddThemeStyleboxOverride("normal", Style(new Color(selected ? "675842" : "29323a"), selected ? Gold : new Color("59616a"), 0, 3));
                button.AddThemeStyleboxOverride("hover", Style(new Color("54606a"), Gold, 0, 3));
                button.AddThemeStyleboxOverride("pressed", Style(new Color("38434c"), Gold, 0, 3));
                button.Disabled = _engine.State.Finished || productionMethod.Id == "iron" && !country.Technologies.Contains("mechanical_tools");
                button.TooltipText = $"每满员级 {productionMethod.WeeklyPoints:0} 点/周，工资 {Money(productionMethod.WeeklyWages)}/周" + (productionMethod.Id == "iron" ? "；需要机械工具科技" : ""); methods.AddChild(button);
            }
            var inputs = Row(body, 5);
            foreach (var input in method.WeeklyInputs) inputs.AddChild(ConstructionCommodityChip(input.Key, $"满负荷每级消耗{Localization.Tr(Title(input.Key))} {input.Value:0.#}/周"));
            inputs.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            inputs.AddChild(ConstructionText(Money(staffed * method.WeeklyWages) + "/周", 11, Red));
            card.TooltipText = $"就业 {Compact(city.ConstructionWorkers)} / {Compact(city.ConstructionSectors * ConstructionCatalog.WorkersPerSector)}\n营建工资 {Money(staffed * method.WeeklyWages)}/周；闲置不耗材料，但仍付工资。";
        }
    }

    private void OpenConstructionQueue()
    {
        _constructionPage = "queue"; ShowTab("Industry");
    }

    private async void RunConstructionSmoke()
    {
        try
        {
            void Press(string text) { var button = ButtonsBelow(_right).First(b => b.Text == text && !b.Disabled); button.EmitSignal(BaseButton.SignalName.Pressed); }
            ShowTab("Industry");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Press("＋");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            decimal treasury = _engine.Player.Treasury;
            Press("＋");
            if (_engine.Player.Construction.Count != 1 || _engine.Player.Treasury != treasury) throw new Exception("UI order must enqueue without upfront payment");
            OpenConstructionQueue();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Press("暂停");
            if (!_engine.Player.Construction[0].Paused) throw new Exception("Project pause UI failed");
            Press("恢复");
            Press("Ⅱ 暂停全国施工");
            if (!_engine.Player.ConstructionPaused) throw new Exception("Global construction pause UI failed");
            Press("▶ 恢复全国施工");
            _engine.BuildConstructionSector("QNG_suzhou"); RefreshPanels();
            Press("↓");
            if (_engine.Player.Construction[0].IndustryId != ConstructionCatalog.SectorId) throw new Exception("Priority UI failed");
            Press("取消");
            if (_engine.Player.Construction.Count != 1) throw new Exception("Cancel UI failed");
            _constructionPage = "sectors"; RefreshPanels();
            AuditChinese(_ui);
            var copy = SimulationEngine.LoadJson(_engine.SaveJson());
            if (copy.Player.Construction[0].Id != _engine.Player.Construction[0].Id) throw new Exception("Construction UI save roundtrip failed");
            OpenLeader("QNG");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_leader.IsPortraitMode || !_leader.IsOpen) throw new Exception("Default leader must be animated 3D");
            _leader.TriggerGesture("greeting");
            CloseCity();
            if (_leader.IsOpen) throw new Exception("Leader return to map failed");
            GD.Print("SOVEREIGN_CONSTRUCTION_UI_PASS enqueue pause resume priority cancel sectors save animated-3d");
            RequestQuit(0);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); RequestQuit(1); }
    }
}
