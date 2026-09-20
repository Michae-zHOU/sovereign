using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class WorldMap
{
    public event Action<string>? ProvinceSelected;
    private readonly Dictionary<string, (MeshInstance3D Border, Label3D Label)> _provinceVisuals = new();
    private string _selectedProvince = "";
    private string _provinceVisualState = "";
    private Node3D _provinceLayer = null!;
    private StandardMaterial3D _provinceBorder = null!, _provinceSelectedBorder = null!;

    private void BuildProvinces()
    {
        _provinceLayer = new Node3D { Visible = false }; AddChild(_provinceLayer);
        _terrainMaterial.SetShaderParameter("province_ids", GD.Load<Texture2D>("res://Assets/provinces/qing-province-ids.png"));
        var allowed = Image.CreateEmpty(512, 1, false, Image.Format.Rgb8); allowed.Fill(Colors.Black);
        foreach (var country in HistoricalWorldAtlas.Countries.Where(c => c.ScenarioCountryId == "QNG")) allowed.SetPixel(country.ColorIndex, 0, Colors.White);
        _terrainMaterial.SetShaderParameter("qing_territory_mask", ImageTexture.CreateFromImage(allowed)); allowed.Dispose();
        _provinceBorder = Material(new Color("89764f"), true);
        _provinceSelectedBorder = Material(new Color("f9dd88"), true);
        foreach (var province in QingProvinceCatalog.Provinces)
        {
            var points = new List<Vector3>();
            foreach (var polygon in province.BoundaryPolygons)
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(a.Longitude - b.Longitude), Math.Abs(a.Latitude - b.Latitude)) / .2));
                Vector3 Project(double t) { float x = (float)(a.Longitude + (b.Longitude - a.Longitude) * t); float y = (float)(a.Latitude + (b.Latitude - a.Latitude) * t); return Geo(x, y, TerrainHeight(x, y) + .015f); }
                for (int j = 0; j < steps; j++) {
                    var middle = Project((j + .5) / steps);
                    if (!ProvinceLand(middle)) continue;
                    points.Add(Project((double)j / steps)); points.Add(Project((double)(j + 1) / steps));
                }
            }
            var border = CreateLines(points, _provinceBorder); _provinceLayer.AddChild(border);
            float lon = (float)province.Longitude, lat = (float)province.Latitude;
            var label = MapLabel(province.Name, Geo(lon, lat, TerrainHeight(lon, lat) + .035f), 48, new Color("392d26"), .0022f);
            label.OutlineModulate = new Color("ecdbb5"); label.OutlineSize = 1;
            label.Reparent(_provinceLayer); _provinceVisuals[province.Id] = (border, label);
        }
    }

    private void UpdateProvinces()
    {
        if (_provinceLayer == null) return;
        string state = _mode + ":" + _selectedProvince;
        if (_provinceVisualState == state) return;
        _provinceVisualState = state;
        _provinceLayer.Visible = _mode == "Provinces";
        foreach (var item in _provinceVisuals)
        {
            item.Value.Border.MaterialOverride = item.Key == _selectedProvince ? _provinceSelectedBorder : _provinceBorder;
            item.Value.Label.Modulate = item.Key == _selectedProvince ? new Color("5c351b") : new Color("42352b");
            item.Value.Label.Visible = false;
        }
    }

    private void UpdateProvinceLabels(Vector2 viewport)
    {
        if (_provinceLayer == null) return;
        bool show = _mode == "Provinces" && _currentZoom < 9;
        if (_provinceVisuals.TryGetValue(_selectedProvince, out var selected))
            PlaceMapLabel(selected.Label, 29, show, viewport);
        foreach (var item in _provinceVisuals)
        {
            if (item.Key == _selectedProvince) continue;
            PlaceMapLabel(item.Value.Label, _currentZoom < 4.8f ? 23 : 19, show, viewport);
        }
    }

    public Vector2 ProvinceScreenPosition(string id)
    {
        var p = QingProvinceCatalog.Province(id)!;
        return _camera.UnprojectPosition(Geo((float)p.Longitude, (float)p.Latitude, TerrainHeight((float)p.Longitude, (float)p.Latitude)));
    }
    public Vector2 GeographyScreenPosition(float longitude, float latitude) => _camera.UnprojectPosition(Geo(longitude, latitude, TerrainHeight(longitude, latitude)));

    private bool ProvinceLand(Vector3 ground) => IsLand(ground) && HistoricalWorldAtlas.At(ground.X / MapScale, -ground.Z / MapScale)?.ScenarioCountryId == "QNG";

    public bool PickProvince(Vector2 screen)
    {
        if (_mode != "Provinces") return false;
        var origin = _camera.ProjectRayOrigin(screen); var direction = _camera.ProjectRayNormal(screen);
        if (direction.Y >= -.001f) return false;
        var ground = origin + direction * ((.13f - origin.Y) / direction.Y);
        for (int i = 0; i < 8; i++) ground = origin + direction * ((TerrainHeight(ground.X / MapScale, -ground.Z / MapScale) - origin.Y) / direction.Y);
        if (!ProvinceLand(ground)) return false;
        var province = QingProvinceCatalog.ProvinceAt(ground.X / MapScale, -ground.Z / MapScale);
        if (province == null) return false;
        FocusRegion(province.Id); ProvinceSelected?.Invoke(province.Id); return true;
    }
}
