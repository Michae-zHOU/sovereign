using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign.Simulation;

public sealed record DiplomaticOffer(string Id, string Name, decimal Cost, int RelationChange, string Effect, string BlockedReason)
{
    public bool Available => BlockedReason.Length == 0;
}

public partial class SimulationEngine
{
    public IReadOnlyList<DiplomaticOffer> DiplomaticOffers(string countryId) => new[] {
        Offer(countryId, "improve", "派遣友好使团", 400m, 15, "双方关系 +15；该国外交冷却90天。"),
        Offer(countryId, "trade_agreement", "签署贸易协定", 1200m, 8, "建立双边贸易协定；双方关系 +8；外交冷却90天。"),
        Offer(countryId, "mobilize", "施加军事压力", 1000m, -30, "双方关系 −30；取消贸易协定；每日军费增加45；外交冷却90天。尚无战斗与割地。"),
        Offer(countryId, "deescalate", "撤回军事施压", 0m, 15, "撤回对该国的动员，双方关系 +15；威望最多减少4；外交冷却90天。")
    };

    private DiplomaticOffer Offer(string id, string action, string name, decimal cost, int change, string effect)
    {
        var other = State.Countries.FirstOrDefault(c => c.Id == id);
        string reason = State.Finished ? "战役已经结束" : other == null || other.Id == Player.Id ? "需要选择另一个已接入模拟的国家"
            : State.PendingEvent != null ? "请先处理待决历史事件"
            : action != "deescalate" && Player.DiplomaticCooldowns.GetValueOrDefault(id) > 0 ? $"使团尚未归来：还需{Player.DiplomaticCooldowns[id]}天"
            : Player.Treasury < cost ? $"国库不足，需要£{cost:0}"
            : action == "trade_agreement" && Player.TradePacts.Contains(id) ? "两国已有贸易协定"
            : action == "trade_agreement" && Player.MobilizedAgainst == id ? "请先撤回对该国的军事施压，再讨论贸易协定"
            : action == "trade_agreement" && Player.Relations.GetValueOrDefault(id) < 20 ? $"对方拒绝：关系需达到+20，当前{Player.Relations.GetValueOrDefault(id):+0;-0;0}"
            : action == "mobilize" && Player.MobilizedAgainst.Length > 0 ? "已有动员，请先撤回军事施压"
            : action == "deescalate" && Player.MobilizedAgainst != id ? "未对该国动员" : "";
        return new(action, name, cost, change, effect, reason);
    }

    // These responses are authored game dialogue, not quotations or a historical claim.
    public string LeaderReply(string id, string topic)
    {
        var other = State.Countries.FirstOrDefault(c => c.Id == id);
        if (other == null || id == Player.Id) return "此国家尚未开放外交会谈。";
        int relation = Player.Relations.GetValueOrDefault(id);
        return topic switch {
            "economy" => Player.MobilizedAgainst == id
                ? "贵国仍在对我方施加军事压力。请先撤回动员，再讨论贸易协定。"
                : Player.TradePacts.Contains(id)
                ? "两国已经签订贸易协定。协定的成效仍取决于商路运力、货源与各自的财政。"
                : relation >= 20 ? "两国交往已有基础。如贵国愿意承担缔约费用，我方愿意签署贸易协定。"
                : "通商需要互信。请先改善两国关系，再讨论正式贸易协定。",
            "relations" => Player.MobilizedAgainst == id ? "贵国的军事动员使局势紧张。请先撤回施压，再谈长远合作。"
                : relation < 0 ? "两国之间仍有嫌隙。我们会根据贵国采取的实际行动判断诚意。"
                : relation >= 40 ? "两国关系友善。我们愿继续维护现有的合作。" : "我们愿意保持往来，但仍需建立更多信任。",
            "improve" => "我们已接待贵国使团。愿此次往来成为两国关系改善的开始。",
            "trade_agreement" => "贸易协定已正式生效。两国商贸将依照新协定办理。",
            "mobilize" => "我们已经注意到贵国的军事行动。此举损害了两国关系。",
            "deescalate" => "撤回动员有助于缓和紧张。我们愿意恢复外交接触。",
            _ => relation < 0 ? "使者，双方分歧仍在。请说明此次来意。" : "欢迎贵国使者。今日希望商议何事？"
        };
    }
}
