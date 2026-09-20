using Godot;
using System;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private async void RunProvinceDiplomacySmoke()
    {
        try
        {
            SetLens("Provinces"); int count = 0;
            foreach (var province in QingProvinceCatalog.Provinces)
            {
                SelectRegion(province.Id);
                await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
                if (!_map.PickProvince(_map.ProvinceScreenPosition(province.Id)) || _selectedRegion != province.Id)
                    throw new Exception("Province terrain ray selection failed: " + province.Id);
                if (_tab != "Atlas" || !_drawerOpen || ExploredCountry.Id != "QNG") throw new Exception("Province UI failed: " + province.Id);
                count++;
            }
            foreach (var coordinate in new[] { new Vector2(120, 38), new Vector2(85, 28.2f), new Vector2(140, 50) })
                if (_map.PickProvince(_map.GeographyScreenPosition(coordinate.X, coordinate.Y))) throw new Exception("Province captured sea or foreign territory: " + coordinate);
            void Press(string text) { ButtonsBelow(_right).First(b => b.Text == text && !b.Disabled).EmitSignal(BaseButton.SignalName.Pressed); }
            foreach (var region in GeographyCatalog.Regions.Where(r => r.CountryId != "QNG"))
            {
                SelectRegion(region.Id);
                if (_selectedRegion != region.Id || _rebuild) throw new Exception("Foreign region selection failed: " + region.Id);
            }
            SelectRegion("QNG_hubei"); MeetLeader("GBR"); CloseCity();
            if (_selectedRegion.Length != 0 || _tab != "Atlas") throw new Exception("Foreign leader retained Qing region");
            InspectCountry("GBR");
            ApplyLoadedGame(SimulationEngine.LoadJson(SimulationEngine.NewGame("GBR").SaveJson()));
            if (_tab != "Overview" || _foreignCountry.Length > 0 || _rebuild) throw new Exception("Cross-country save UI failed");
            ChooseConstructionLocations("farm", "QNG_hubei"); StartGame("GBR");
            if (_constructionRegion.Length > 0 || _constructionType.Length > 0) throw new Exception("New campaign retained old construction filter");
            StartGame("QNG"); InspectCountry("GBR");
            if (_tab != "ForeignCountry") throw new Exception("Country detail failed");
            if (!_map.Visible || _portraitPreview == null || !_portraitPreview.IsOpen || !_portraitViewport!.OwnWorld3D)
                throw new Exception("Embedded 3D portrait must preserve world map");
            Press("觐见领袖 · 开始三维会谈");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_tab != "Meeting" || !_leader.IsOpen || _leader.IsPortraitMode) throw new Exception("Animated meeting failed");
            var meetingDate = _engine.State.Date; SetSpeed(4); AdvanceOne();
            if (_speed != 0 || _engine.State.Date != meetingDate) throw new Exception("Meeting must keep historical interlocutor date stable");
            Press("讨论通商");
            if (_conversation.Count != 3) throw new Exception("Dialogue topic failed");
            int relation = _engine.Player.Relations["GBR"]; decimal treasury = _engine.Player.Treasury;
            var improve = ButtonsBelow(_right).First(b => b.Text.StartsWith("派遣友好使团") && !b.Disabled); improve.EmitSignal(BaseButton.SignalName.Pressed);
            if (_engine.Player.Relations["GBR"] != relation + 15 || _engine.Player.Treasury != treasury - 400) throw new Exception("Conversation action did not affect simulation");
            if (ButtonsBelow(_right).Any(b => b.Text.StartsWith("派遣友好使团") && !b.Disabled)) throw new Exception("Cooldown unavailable state failed");
            Press("结束会谈 · 返回国家详情");
            if (_leader.IsOpen || _tab != "ForeignCountry") throw new Exception("Meeting exit failed");
            OpenLeader("QNG");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_leader.UsesAuthoredModel || !_leader.HasSkeletalAnimation || !_leader.HasFacialAnimation || _leader.IsPortraitMode)
                throw new Exception("Qing requires authored animated skeletal/facial model");
            _leader.TriggerGesture("talk");
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            CloseCity();
            GD.Print($"SOVEREIGN_PROVINCE_DIPLOMACY_PASS provinces={count} country-detail=True conversation=True action=True cooldown=True qing-skeletal=True qing-facial=True");
            RequestQuit(0);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); RequestQuit(1); }
    }
}
