using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private string _foreignCountry = "", _foreignPage = "overview";
    private readonly List<(string Speaker, string Text)> _conversation = new();

    private void InspectCountry(string id)
    {
        if (id == _engine.Player.Id) { ExploreCountry(id); return; }
        _foreignCountry = id; _foreignPage = "overview"; _exploreCountry = id;
        _selectedRegion = ""; _selectedCity = "";
        if (_city.IsOpen || _leader.IsOpen) CloseCity();
        SetLens("Political"); _tab = "ForeignCountry"; _drawerOpen = true;
        _rightScroll.ScrollVertical = 0; _map.FocusCountry(id); RefreshPanels();
    }

    private void ForeignCountryPanel()
    {
        var country = _engine.State.Countries.First(c => c.Id == _foreignCountry);
        var leader = HistoricalLeaders.Get(country.Id, _engine.State.Date);
        IllustratedHeader(_right, "diplomacy", "使节与交涉", Localization.Tr(_engine.Player.Name) + " · " + Localization.Tr(country.Name), 120);
        CountryIdentity(_right, country.Id, country.Name);
        _right.AddChild(Para(leader.Title + " · " + leader.Name, 16));
        var tabs = Row(_right, 2);
        foreach (var (id, caption) in new[] { ("overview", "概览"), ("diplomacy", "外交"), ("leader", "领袖") })
        { string page = id; var b = SmallButton(caption, () => { _foreignPage = page; RefreshPanels(); }, page == _foreignPage); b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; tabs.AddChild(b); }
        Pair(_right, "两国关系", _engine.Player.Relations[country.Id].ToString("+0;-0;0"));
        Pair(_right, "贸易协定", _engine.Player.TradePacts.Contains(country.Id) ? "已生效" : "未签订");
        _right.AddChild(B("觐见领袖 · 开始三维会谈", () => MeetLeader(country.Id), true));
        Rule(_right);
        if (_foreignPage == "overview")
        {
            var identity = Row(_right, 14); identity.AddChild(LeaderPortrait(country.Id));
            var statistics = VBox(identity, 7); statistics.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            foreach (var (caption, value) in new[] { ("人口", Compact(country.Population)), ("年化产出", Money(country.Gdp)), ("识字率", country.Literacy.ToString("0.0") + "%"), ("生活水平", country.LivingStandard.ToString("0.0")) })
            { statistics.AddChild(L(caption, 12, Gold)); statistics.AddChild(Para(value, 21, Cream)); }
            _right.AddChild(B("查看各地区与城市 →", () => ExploreCountry(country.Id)));
        }
        else if (_foreignPage == "diplomacy") AddDiplomaticActions(country.Id, false);
        else
        {
            var portrait = Row(_right, 12); portrait.AddChild(LeaderPortrait(country.Id));
            var identity = VBox(portrait, 8); identity.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            identity.AddChild(Para(leader.Name, 23, Gold)); identity.AddChild(Para(leader.Title, 15));
            identity.AddChild(Para("当前日期\n" + Localization.Date(_engine.State.Date), 13));
            _right.AddChild(Para(leader.Description, 15));
            if (!string.IsNullOrWhiteSpace(leader.SecondaryOffice)) _right.AddChild(Para(leader.SecondaryOffice, 14, Gold));
            _right.AddChild(B("领袖史料 ↗", () => OS.ShellOpen(leader.SourceUrl)));
        }
    }

    private void MeetLeader(string countryId)
    {
        if (!_started || _menuOpen || countryId == _engine.Player.Id) return;
        if (_engine.State.PendingEvent != null) { CheckEvent(); return; }
        SetSpeed(0); _foreignCountry = countryId; _conversation.Clear();
        OpenLeader(countryId); _tab = "Meeting";
        _conversation.Add((Localization.Tr(HistoricalLeaders.Get(countryId, _engine.State.Date).Name), _engine.LeaderReply(countryId, "greeting")));
        RefreshPanels(); _leader.TriggerGesture("greeting");
    }

    private void MeetingPanel()
    {
        var country = _engine.State.Countries.First(c => c.Id == _foreignCountry);
        var leader = HistoricalLeaders.Get(country.Id, _engine.State.Date);
        CountryIdentity(_right, country.Id, country.Name + " · 外交会谈", 21);
        _right.AddChild(Para(leader.Name + " / " + leader.Title, 16));
        if (!string.IsNullOrWhiteSpace(leader.SecondaryOffice)) _right.AddChild(Para(leader.SecondaryOffice, 13, Gold));
        _right.AddChild(Para("游戏对白 · 根据当前局势回应，并非历史原话。拖动人物可转动视角，滚轮调整远近。", 12, Muted));
        Pair(_right, "两国关系", _engine.Player.Relations[country.Id].ToString("+0;-0;0"));
        var dialogue = Panel(_right, new Color("25242b"), new Color("827258"), 12); var transcript = VBox(dialogue, 8);
        foreach (var line in _conversation.TakeLast(4)) { transcript.AddChild(L(line.Speaker, 13, Gold, true)); transcript.AddChild(Para(line.Text, 15)); }
        var topics = Row(_right, 4);
        topics.AddChild(SmallButton("谈论两国关系", () => Discuss("relations", "我们如何改善两国关系？")));
        topics.AddChild(SmallButton("讨论通商", () => Discuss("economy", "我希望商议两国通商。")));
        Section(_right, "正式提案 · 费用与条件");
        AddDiplomaticActions(country.Id, true);
        _right.AddChild(B("结束会谈 · 返回国家详情", () => InspectCountry(country.Id)));
    }

    private void Discuss(string topic, string text)
    {
        _conversation.Add(("我方使者", text));
        _conversation.Add((Localization.Tr(HistoricalLeaders.Get(_foreignCountry, _engine.State.Date).Name), _engine.LeaderReply(_foreignCountry, topic)));
        if (_conversation.Count > 20) _conversation.RemoveRange(0, _conversation.Count - 20);
        _leader.TriggerGesture("talk"); RefreshPanels();
    }

    private void AddDiplomaticActions(string id, bool meeting)
    {
        foreach (var offer in _engine.DiplomaticOffers(id))
        {
            var b = B(offer.Name + "   " + (offer.Cost > 0 ? Money(offer.Cost) : "无费用"), () => {
                var result = _engine.Diplomacy(id, offer.Id);
                Toast(result.Message);
                if (result.Success && meeting) { _conversation.Add(("我方使者", offer.Name)); _conversation.Add((Localization.Tr(HistoricalLeaders.Get(id, _engine.State.Date).Name), _engine.LeaderReply(id, offer.Id))); _leader.TriggerGesture(offer.Id == "mobilize" ? "refuse" : "agree"); }
                RefreshAll();
            });
            b.Disabled = !offer.Available; b.TooltipText = offer.Effect + (offer.Available ? "" : "\n" + offer.BlockedReason);
            _right.AddChild(b);
            _right.AddChild(Para(offer.Available ? offer.Effect : offer.BlockedReason, 12, offer.Available ? Muted : Red));
        }
    }
}
