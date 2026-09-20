using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private async void RunMechanicsSmoke()
    {
        int exitCode = 0;
        var priorSaves = new Dictionary<string, byte[]?>();
        try
        {
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
            Button Find(Node parent, Func<Button, bool> predicate) => ButtonsBelow(parent).First(button => button.IsVisibleInTree() && !button.Disabled && predicate(button));
            void Press(string text, Node? parent = null) => Find(parent ?? _right, button => button.Text == text).EmitSignal(BaseButton.SignalName.Pressed);
            void Navigate(string tab)
            {
                _nav[tab].EmitSignal(BaseButton.SignalName.Pressed);
                Check(_tab == tab && _drawerOpen, "Navigation button did not open " + tab);
            }
            IEnumerable<Label> Labels(Node node)
            {
                foreach (var child in node.GetChildren())
                {
                    if (child is Label label && label.IsVisibleInTree()) yield return label;
                    foreach (var descendant in Labels(child)) yield return descendant;
                }
            }
            StartGame("QNG");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Navigate("Markets");
            Check(Labels(_right).Any(label => label.Text.Contains("买单")), "Market buy order controls missing");
            Check(_engine.Player.ImportOrders["grain"] == 0m, "Fresh campaign unexpectedly imports grain");
            Press("进口");
            Check(_engine.Player.Trade["grain"] == 1, "Import button did not change grain trade direction");
            var beforeDate = _engine.State.Date;
            Press("+1天", _ui);
            Check(_engine.State.Date == beforeDate.AddDays(1) && _engine.Player.ImportOrders["grain"] > 0m && _engine.Player.SellOrders["grain"] >= _engine.Player.ImportOrders["grain"], "Import button failed to create daily market orders");
            AuditChinese(_ui);

            Navigate("Population");
            Check(PopulationCatalog.Professions.Values.All(name => Labels(_right).Any(label => label.Text == name)), "Population profession cards missing");
            Check(_engine.Player.Pops.Sum(pop => pop.Population) == _engine.Player.Population, "Population UI state does not reconcile");
            AuditChinese(_ui);

            Navigate("Industry");
            string farmTitle = Localization.Tr(Catalog.Industries.Single(industry => industry.Id == "farm").Name);
            var farmCity = _engine.Player.Cities.Where(city => city.Industries["farm"] > 0).OrderByDescending(city => city.Industries["farm"]).ThenBy(city => city.Id).First();
            Press("▸ " + farmTitle);
            string advancedMethod = ProductionCatalog.Get("farm", "mechanized").Name;
            Check(ButtonsBelow(_right).Any(button => button.Text == advancedMethod && button.Disabled), "Locked production method should be visible and disabled");
            _engine.Player.Technologies.Add("mechanical_tools"); RefreshAll();
            Press(advancedMethod);
            Check(farmCity.Buildings["farm"].MethodId == "mechanized", "Production method button did not change its displayed city's recipe");
            Check(Labels(_right).Any(label => label.Text.Contains("所有权")) && Labels(_right).Any(label => label.Text.Contains("现金储备")), "Building account and ownership controls missing");
            AuditChinese(_ui);

            Navigate("Politics"); Press("组阁与集团");
            var desiredGovernment = new HashSet<string> { "landowners", "armed_forces", "devout", "rural_folk" };
            foreach (var group in PoliticalCatalog.InterestGroups)
            {
                var choice = ButtonsBelow(_right).OfType<CheckBox>().Single(button => button.Text == group.Name);
                choice.ButtonPressed = desiredGovernment.Contains(group.Id);
            }
            Press("提交政府改组");
            Check(_engine.Player.GovernmentGroups.ToHashSet().SetEquals(desiredGovernment), "Government checkbox choices were not committed by the button");
            Press("法律");
            var candidate = PoliticalCatalog.Laws.First(law => _engine.GetLawEnactmentBlockReason(law.Id).Length == 0);
            string priorLaw = _engine.Player.Laws[candidate.GroupId];
            Press(PoliticalCatalog.LawGroups.Single(group => group.Id == candidate.GroupId).Name);
            Press("提出法案");
            Check(_engine.Player.LawEnactment?.LawId == candidate.Id && _engine.Player.Laws[candidate.GroupId] == priorLaw, "Law button must start deliberation rather than enact instantly");
            Check(ButtonsBelow(_right).Any(button => button.Text == "撤回法案" && !button.Disabled), "Active law deliberation controls missing");
            Press("机构");
            int institutionLevels = _engine.Player.InstitutionLevels.Values.Sum(), bureaucracy = _engine.Player.BureaucracyUsed;
            Press("＋ 一级");
            Check(_engine.Player.InstitutionLevels.Values.Sum() == institutionLevels + 1 && _engine.Player.BureaucracyUsed == bureaucracy + 20, "Institution budget button did not reserve real bureaucracy");
            Press("财政"); Press("35%");
            Check(_engine.Player.TaxRate == 35 && Labels(_right).Any(label => label.Text.Contains("投资池余额")), "Fiscal tax control or private investment accounts missing");
            AuditChinese(_ui);

            Navigate("Industry");
            Find(_right, button => button.Text.StartsWith("建造队列", StringComparison.Ordinal)).EmitSignal(BaseButton.SignalName.Pressed);
            Press("暂停私人投资"); Check(!_engine.Player.PrivateConstructionEnabled, "Private investment pause button failed");
            Press("恢复私人投资"); Check(_engine.Player.PrivateConstructionEnabled, "Private investment resume button failed");
            AuditChinese(_ui);

            _engine.Player.Technologies.Add("railways");
            Navigate("Markets"); Press("地区价格与接入");
            string regionId = _marketRegion;
            int railways = _engine.Player.Regions.Single(region => region.Id == regionId).RailwayLevels;
            int projects = _engine.Player.Construction.Count; decimal treasury = _engine.Player.Treasury;
            Find(_right, button => button.Text.StartsWith("加入铁路建设", StringComparison.Ordinal)).EmitSignal(BaseButton.SignalName.Pressed);
            Check(_engine.Player.Construction.Count == projects + 1 && _engine.Player.Construction[^1].IndustryId == ConstructionCatalog.RailwayId &&
                _engine.GetCity(_engine.Player.Construction[^1].CityId).RegionId == regionId && _engine.Player.Treasury == treasury &&
                _engine.Player.Regions.Single(region => region.Id == regionId).RailwayLevels == railways, "Railway UI must queue the selected province's project without immediate construction or charging");
            Press("+1天", _ui);
            Check(_engine.Player.Construction.Any(project => project.IndustryId == ConstructionCatalog.RailwayId && project.ProgressPoints > 0m), "Queued railway did not receive construction points");
            AuditChinese(_ui);

            // Exercise the real save/load buttons while preserving any campaign already on this machine.
            foreach (string path in new[] { SavePath, SavePath + ".bak", SavePath + ".tmp" }) priorSaves[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            string digest = _engine.CanonicalDigest();
            Press("保存", _ui); Press("读取", _ui);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(_engine.CanonicalDigest() == digest && _engine.Player.LawEnactment?.LawId == candidate.Id &&
                _engine.GetCity(farmCity.Id).Buildings["farm"].MethodId == "mechanized" && _engine.Player.TaxRate == 35, "UI save/load lost law progress, production method or fiscal state");
            AuditChinese(_ui);
            GD.Print("SOVEREIGN_MECHANICS_UI_PASS market-import=True population=True production-method=True government=True law-deliberation=True institution-budget=True fiscal-tax=True private-toggle=True railway-queue=True chinese-audit=True save-load=True");
        }
        catch (Exception ex) { exitCode = 1; GD.PushError(ex.ToString()); }
        finally
        {
            try
            {
                foreach (var saved in priorSaves)
                {
                    if (saved.Value == null) { if (File.Exists(saved.Key)) File.Delete(saved.Key); }
                    else File.WriteAllBytes(saved.Key, saved.Value);
                }
            }
            catch (Exception ex) { exitCode = 1; GD.PushError("Mechanics smoke could not restore pre-existing save files: " + ex); }
        }
        RequestQuit(exitCode);
    }
}
