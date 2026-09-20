using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign.Simulation;

public sealed partial class SimulationEngine
{
    private void CheckEvents()
    {
        if (State.PendingEvent != null) return;
        if (State.Date >= new DateTime(1836, 3, 1) && !State.CompletedEvents.Contains("industrial_petition"))
            State.PendingEvent = CreateEvent("industrial_petition", "The petitions on your desk",
                "Teachers ask for grants. Town councils request relief for workers. The first months of your administration have made its priorities visible.",
                "An original simulated policy dilemma, not a claim about a specific historical petition. Economic effects are authored for play.", "",
                ("schools", "Fund local classrooms", "Pay 900. Literacy +3 points. Unrest +2 points from opponents."),
                ("relief", "Support household relief", "Pay 600. Unrest −7 points."),
                ("defer", "Defer the petitions", "No cost. Unrest +4 points."));
        else if (State.Date >= new DateTime(1837, 6, 1) && !State.CompletedEvents.Contains("harvest_pressure"))
            State.PendingEvent = CreateEvent("harvest_pressure", "A fragile harvest",
                "歉收使粮食市场承压。地方官员请求拨款采购粮食并赈济百姓。",
                "An original simulated harvest shock. It is not a reconstruction of any particular historical famine. The chosen response changes game stocks and public unrest.", "",
                ("release", "采购赈济粮", "支付800及100单位粮食的当前市价；骚乱−10，生活水平+1。"),
                ("cash", "Fund local purchases", "Pay 1,200. Unrest −6 points; living standard +0.5."),
                ("wait", "Leave relief to local authorities", "No cost. Unrest +8 points; living standard −1."));
        else if (Player.Id == "GBR" && State.Date >= new DateTime(1838, 5, 8) && !State.CompletedEvents.Contains("chartists"))
            State.PendingEvent = CreateEvent("chartists", "The People's Charter",
                "A movement for parliamentary reform is gathering support. Its demands include a wider male franchise, a secret ballot and equal electoral districts. How will your government respond?",
                "The People's Charter appeared in 1838; Parliament rejected the first mass petition in 1839. This event is placed in May 1838. The options and their numerical effects are alternative-history game design, not historical predictions.",
                "https://www.parliament.uk/about/living-heritage/transformingsociety/electionsvoting/chartists/overview/chartistmovement/",
                ("recognize", "Begin a franchise reform process", "Pay 2,400. Unrest −12 points; literacy +2; prestige +5. An alternative-history response."),
                ("commission", "Appoint a reform commission", "Pay 1,000. Unrest −5 points."),
                ("reject", "Reject the demands", "No cost. Unrest +10 points; prestige +2."));
        else if (Player.Id == "PRU" && State.Date >= new DateTime(1840, 6, 7) && !State.CompletedEvents.Contains("accession"))
            State.PendingEvent = CreateEvent("accession", "A new king in Prussia",
                "Frederick William IV succeeds his father. A new reign raises expectations of political change, investment and reconciliation. Set the direction of the new administration.",
                "Frederick William III died on 7 June 1840, and his son succeeded him. The Deutsches Historisches Museum records the succession. Policy choices here are authored alternatives, not an exact account of the reign.",
                "https://www.dhm.de/lemo/jahreschronik/1840",
                ("hearing", "Hear petitions for political reform", "Pay 1,800. Unrest −10 points; literacy +2; prestige +3."),
                ("scholarship", "Sponsor education and scholarship", "Pay 2,000. Literacy +4 points; prestige +2."),
                ("authority", "Affirm royal authority", "No cost. Unrest +5 points; prestige +4."));
        else if (Player.Id == "JAP" && State.Date >= new DateTime(1841, 1, 1) && !State.CompletedEvents.Contains("tenpo"))
            State.PendingEvent = CreateEvent("tenpo", "The Tenpō reform debate",
                "Officials debate how to respond to social and economic strain. Choose whether relief, learning or austerity will guide your administration.",
                "Japan's Tenpō reforms took place in 1841–1843. The National Diet Library authority record establishes this period. This event uses 1 January as a year marker, not the exact date of an announcement. Choices and numerical effects are fictional policy abstractions.",
                "https://id.ndl.go.jp/auth/ndlsh/00661577",
                ("rural", "Fund rural relief", "Pay 1,600. Unrest −10 points; living standard +1."),
                ("learning", "Invest in learning", "Pay 2,000. Literacy +4 points; unrest +3 points."),
                ("austerity", "Collect an extraordinary levy", "Gain 800 treasury from a levy. Unrest +8 points; living standard −1."));
        if (State.PendingEvent != null) AddLog($"Decision required: {State.PendingEvent.Title}.");
    }

    public CommandResult ResolveEvent(string optionId)
    {
        var ev = State.PendingEvent;
        if (ev == null) return Fail("There is no pending event.");
        if (!ev.Options.Any(o => o.Id == optionId)) return Fail("Unknown event option.");
        decimal cost = 0m, unrest = 0m, literacy = 0m, prestige = 0m, living = 0m, grain = 0m;
        switch (ev.Id + ":" + optionId)
        {
            case "industrial_petition:schools": cost = 900m; literacy = 3m; unrest = 2m; break;
            case "industrial_petition:relief": cost = 600m; unrest = -7m; break;
            case "industrial_petition:defer": unrest = 4m; break;
            case "harvest_pressure:release": cost = 800m; grain = 100m; unrest = -10m; living = 1m; break;
            case "harvest_pressure:cash": cost = 1200m; unrest = -6m; living = .5m; break;
            case "harvest_pressure:wait": unrest = 8m; living = -1m; break;
            case "chartists:recognize": cost = 2400m; unrest = -12m; literacy = 2m; prestige = 5m; break;
            case "chartists:commission": cost = 1000m; unrest = -5m; break;
            case "chartists:reject": unrest = 10m; prestige = 2m; break;
            case "accession:hearing": cost = 1800m; unrest = -10m; literacy = 2m; prestige = 3m; break;
            case "accession:scholarship": cost = 2000m; literacy = 4m; prestige = 2m; break;
            case "accession:authority": unrest = 5m; prestige = 4m; break;
            case "tenpo:rural": cost = 1600m; unrest = -10m; living = 1m; break;
            case "tenpo:learning": cost = 2000m; literacy = 4m; unrest = 3m; break;
            case "tenpo:austerity": cost = -800m; unrest = 8m; living = -1m; break;
            default: return Fail("Unsupported event option.");
        }
        cost += grain * Player.Prices["grain"];
        if (Player.Treasury < cost) return Fail($"This option requires {cost:N0} treasury.");
        Player.Treasury -= cost;
        Player.Unrest = Clamp(Player.Unrest + unrest, 0m, 100m);
        Player.Literacy = Clamp(Player.Literacy + literacy, 0m, 99m);
        Player.LivingStandard = Clamp(Player.LivingStandard + living, 0m, 30m);
        Player.Prestige = Math.Max(0m, Player.Prestige + prestige);
        foreach (var pop in Player.Pops)
        {
            pop.Literacy = Clamp(pop.Literacy + literacy, 0m, 99m);
            pop.Wealth = Clamp(pop.Wealth + living, 1m, 30m);
            pop.Radicals = Clamp(pop.Radicals + unrest / 100m, 0m, 1m);
            pop.Loyalists = Math.Min(pop.Loyalists, 1m - pop.Radicals);
        }
        State.CompletedEvents.Add(ev.Id); State.PendingEvent = null;
        AddLog($"{ev.Title}: {ev.Options.Single(o => o.Id == optionId).Label}.");
        return Ok("Decision enacted. The simulation can resume.");
    }

    private static GameEvent CreateEvent(string id, string title, string description, string context, string source,
        params (string Id, string Label, string Description)[] options) => new()
    {
        Id = id, Title = title, Description = description, HistoricalContext = context, SourceUrl = source,
        Options = options.Select(o => new EventOption { Id = o.Id, Label = o.Label, Description = o.Description }).ToList()
    };
}
