using Sovereign.Simulation;

internal static class DiplomaticConversationChecks
{
    public static void OfferConsistency()
    {
        var situations = new (string Name, Action<SimulationEngine> Setup)[]
        {
            ("opening relations", _ => { }),
            ("friendly", g => SetRelations(g, "GBR", 40)),
            ("at relation ceiling", g => SetRelations(g, "GBR", 95)),
            ("at relation floor", g => SetRelations(g, "GBR", -95)),
            ("empty treasury", g => g.Player.Treasury = 0m),
            ("exact mission cost", g => g.Player.Treasury = 400m),
            ("active cooldown", g => g.Player.DiplomaticCooldowns["GBR"] = 1),
            ("existing pact", g => { SetRelations(g, "GBR", 40); g.Player.TradePacts.Add("GBR"); g.State.Countries.Single(c => c.Id == "GBR").TradePacts.Add("QNG"); }),
            ("mobilized against target", g => { g.Player.MobilizedAgainst = "GBR"; g.Player.DiplomaticCooldowns["GBR"] = 90; }),
            ("friendly but under military pressure", g => { SetRelations(g, "GBR", 60); g.Player.MobilizedAgainst = "GBR"; }),
            ("mobilized against another country", g => g.Player.MobilizedAgainst = "FRA"),
            ("pending event", g => g.AdvanceDays(60)),
            ("finished campaign", g => { g.State.Date = Catalog.EndDate; g.State.DayNumber = (Catalog.EndDate - Catalog.StartDate).Days; g.State.Finished = true; })
        };
        int checkedOffers = 0;
        foreach (var situation in situations)
        {
            var source = SimulationEngine.NewGame("QNG"); situation.Setup(source);
            foreach (string target in new[] { "GBR", "QNG", "not-a-country", "" })
            foreach (var offer in source.DiplomaticOffers(target))
            {
                var game = SimulationEngine.LoadJson(source.SaveJson());
                decimal treasury = game.Player.Treasury;
                int previousRelation = game.Player.Relations.GetValueOrDefault(target);
                string before = game.CanonicalDigest();
                var result = game.Diplomacy(target, offer.Id);
                Check(result.Success == offer.Available, situation.Name + ": displayed availability must match execution for " + target + "/" + offer.Id);
                if (!offer.Available)
                {
                    Check(result.Message == offer.BlockedReason, "The displayed reason explains the rejected command");
                    Check(game.CanonicalDigest() == before, "Rejected offer changes neither money, relations, log nor any other state");
                }
                else
                {
                    Check(game.Player.Treasury == treasury - offer.Cost, "Exactly the quoted proposal cost is deducted");
                    Check(game.Player.Relations[target] == Math.Clamp(previousRelation + offer.RelationChange, -100, 100), "Relationship result matches offer, including limits");
                    Check(game.State.Countries.Single(c => c.Id == target).Relations["QNG"] == game.Player.Relations[target], "Bilateral relationship stays symmetric");
                    Check(game.Player.DiplomaticCooldowns[target] == 90, "Every successful proposal sets the documented cooldown");
                    SimulationEngine.LoadJson(game.SaveJson());
                }
                checkedOffers++;
            }
            string digest = source.CanonicalDigest();
            Check(!source.Diplomacy("GBR", "invented-action").Success && digest == source.CanonicalDigest(), "Unknown action rejects atomically");
        }
        Check(checkedOffers == 208, "Every proposal/target/situation combination checked");
    }

    public static void ConversationAndPersistence()
    {
        var game = SimulationEngine.NewGame("QNG");
        string cautious = game.LeaderReply("GBR", "economy");
        decimal before = game.Player.Treasury;
        Check(game.Diplomacy("GBR", "improve").Success && game.Player.Treasury == before - 400m, "Mission is a real funded command");
        string willing = game.LeaderReply("GBR", "economy");
        Check(willing != cautious, "Trade dialogue responds to improved relations");
        var restored = SimulationEngine.LoadJson(game.SaveJson());
        Check(restored.CanonicalDigest() == game.CanonicalDigest() && restored.LeaderReply("GBR", "economy") == willing,
            "Relationship, cost, cooldown and dialogue state survive save/load");
        Advance(game, 89);
        Check(game.Player.DiplomaticCooldowns["GBR"] == 1 && !game.DiplomaticOffers("GBR").Single(o => o.Id == "trade_agreement").Available,
            "Negotiation remains unavailable before the final cooldown day");
        Advance(game, 1);
        Check(game.Player.DiplomaticCooldowns["GBR"] == 0 && game.Diplomacy("GBR", "trade_agreement").Success,
            "Trade agreement becomes executable at cooldown expiry");
        Check(game.Player.TradePacts.Contains("GBR") && game.State.Countries.Single(c => c.Id == "GBR").TradePacts.Contains("QNG"), "Agreement is bilateral");
        Check(game.LeaderReply("GBR", "economy") != willing, "Economic dialogue acknowledges the signed agreement");
        Advance(game, 90);
        var duplicate = game.DiplomaticOffers("GBR").Single(o => o.Id == "trade_agreement");
        Check(!duplicate.Available && duplicate.BlockedReason.Contains("已有贸易协定"), "Existing treaty cannot be bought twice after cooldown");
        string digest = game.CanonicalDigest();
        Check(!game.Diplomacy("GBR", "trade_agreement").Success && digest == game.CanonicalDigest(), "Duplicate agreement neither charges nor modifies state");
        Check(game.Diplomacy("GBR", "mobilize").Success, "Military pressure is available after normal cooldown");
        Check(!game.Player.TradePacts.Contains("GBR") && !game.State.Countries.Single(c => c.Id == "GBR").TradePacts.Contains("QNG"), "Pressure cancels both treaty records");
        string threatened = game.LeaderReply("GBR", "relations");
        restored = SimulationEngine.LoadJson(game.SaveJson());
        Check(restored.Player.MobilizedAgainst == "GBR" && restored.LeaderReply("GBR", "relations") == threatened, "Military pressure and corresponding response persist");
        before = game.Player.Treasury;
        Check(game.Diplomacy("GBR", "deescalate").Success && game.Player.Treasury == before && game.Player.MobilizedAgainst.Length == 0,
            "Withdrawing pressure is free and remains available during the cooldown");
        Check(game.LeaderReply("GBR", "relations") != threatened, "Relations dialogue changes after withdrawal");
        digest = game.CanonicalDigest();
        Check(!game.Diplomacy("GBR", "deescalate").Success && digest == game.CanonicalDigest(), "Repeated withdrawal cannot farm relations");
        SetRelations(game, "GBR", -10);
        string hostile = game.LeaderReply("GBR", "greeting");
        SetRelations(game, "GBR", 50);
        Check(hostile != game.LeaderReply("GBR", "greeting"), "Leader greeting follows current diplomatic sentiment");
        Check(game.LeaderReply("QNG", "economy") == game.LeaderReply("unknown", "economy"), "Own and unsupported countries do not pretend to hold diplomatic talks");
        var prolongedPressure = SimulationEngine.NewGame("QNG");
        SetRelations(prolongedPressure, "GBR", 80);
        Check(prolongedPressure.Diplomacy("GBR", "mobilize").Success, "Pressure can start despite previously friendly relations");
        Advance(prolongedPressure, 90);
        var pressuredOffer = prolongedPressure.DiplomaticOffers("GBR").Single(o => o.Id == "trade_agreement");
        Check(prolongedPressure.Player.Relations["GBR"] >= 20 && !pressuredOffer.Available && pressuredOffer.BlockedReason.Contains("军事施压"),
            "Positive relations do not permit signing a trade agreement under continuing military pressure");
        Check(prolongedPressure.LeaderReply("GBR", "economy").Contains("撤回动员"), "Economic dialogue reflects the actual mobilization gate");
    }

    private static void SetRelations(SimulationEngine game, string target, int value)
    {
        game.Player.Relations[target] = value;
        game.State.Countries.Single(c => c.Id == target).Relations[game.Player.Id] = value;
    }
    private static void Advance(SimulationEngine game, int days)
    {
        var target = game.State.Date.AddDays(days);
        while (game.State.Date < target)
        {
            if (game.State.PendingEvent is { } ev) Check(game.ResolveEvent(ev.Options[^1].Id).Success, "Resolve intervening event");
            game.AdvanceDays(1);
        }
    }
    private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
}
