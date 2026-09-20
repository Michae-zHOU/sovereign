using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

/// <summary>Procedural, explorable city study. The streets are an artistic industrial-era composite, not a historical city reconstruction.</summary>
public partial class CityView : Node3D
{
    public bool IsOpen { get; private set; }
    private Camera3D _camera = null!;
    private Node3D _district = null!, _train = null!, _boat = null!;
    private string _country = "", _cityId = "";
    private long _population;
    private int _factories = 3, _seed = 1836;
    private bool _port = true;
    private SettlementSceneDefinition? _sceneDefinition;
    private bool EastAsian => _country is "JAP" or "QNG";
    private int _industry = -1, _rail = -1;
    private float _yaw = -.43f, _pitch = .70f, _distance = 42f, _smoothDistance = 42f;
    private Vector3 _focus = new(0, 0, -2f);
    private bool _orbiting;
    private double _elapsed;
    private readonly Dictionary<string, Batch> _batches = new();
    private readonly Dictionary<string, Material> _materials = new();
    private readonly List<CpuParticles3D> _smoke = new();
    private readonly BoxMesh _cube = new() { Size = Vector3.One };
    private readonly SphereMesh _sphere = new() { Radius = .5f, Height = 1f, RadialSegments = 10, Rings = 5 };
    private readonly CylinderMesh _cylinder = new() { TopRadius = .5f, BottomRadius = .5f, Height = 1f, RadialSegments = 12 };
    private Mesh _roof = null!, _japanRoof = null!;
    private Random _random = new(1836);

    private sealed class Batch
    {
        public Mesh Mesh = null!;
        public Material Material = null!;
        public readonly List<Transform3D> Transforms = new();
    }

    public override void _Ready()
    {
        _camera = new Camera3D { Fov = 43, Near = .1f, Far = 200, Current = false };
        _camera.Environment = BuildEnvironment();
        AddChild(_camera);
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-43, -38, 0), LightColor = new Color("ffe3b7"), LightEnergy = .90f,
            ShadowEnabled = true, DirectionalShadowMaxDistance = 90, ShadowBias = .035f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowBlendSplits = true
        };
        AddChild(sun);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-32, 130, 0), LightColor = new Color("bfd5de"), LightEnergy = .12f });
        _roof = RoofMesh(false); _japanRoof = RoofMesh(true);
        CreateMaterials();
        Visible = false;
    }

    public void ShowCity(string countryId, int industryLevel, int railLevel)
    {
        ShowSettlement(countryId, GeographyCatalog.Countries.First(c => c.Id == countryId).CapitalCityId, 500000,
            new Dictionary<string,int> { ["toolworks"] = industryLevel }, railLevel);
    }
    public void ShowSettlement(string countryId, string cityId, long population, IReadOnlyDictionary<string,int> industries, int railLevel)
    {
        int industryLevel = industries.Values.Sum();
        int factories = Math.Min(6, industries.Where(x => x.Key is not "farm" and not "lumber").Sum(x => x.Value));
        bool changedCity = cityId != _cityId;
        if (changedCity || _industry != industryLevel || _rail != railLevel || _factories != factories)
        {
            if (changedCity) _sceneDefinition = SettlementScenes.Load(cityId);
            _cityId = cityId; _country = countryId; _population = population; _industry = industryLevel; _rail = railLevel; _factories = factories;
            _seed = _sceneDefinition!.Seed;
            _port = _sceneDefinition.IsPort;
            BuildCity();
            if (changedCity) { _yaw = -.43f; _pitch = .70f; _distance = _smoothDistance = 42f; _focus = new Vector3(0, 0, -2f); }
        }
        IsOpen = true; Visible = true; _camera.Current = true;
        foreach (var smoke in _smoke) smoke.Emitting = true;
        UpdateCamera();
    }
    public void FocusDistrict(string district)
    {
        (_focus, _distance, _pitch) = district switch
        {
            "Housing" => (new Vector3(-10, .8f, -5), 17f, .55f),
            "Industry" => (new Vector3(8, .8f, -5), 20f, .59f),
            "Civic" => (new Vector3(-.3f, 1.7f, -6.2f), 17f, .47f),
            "Waterfront" => (new Vector3(4, .2f, 5), 24f, .50f),
            _ => (new Vector3(0, 0, -2f), 42f, .70f)
        };
    }

    public void HideCity()
    {
        IsOpen = false; Visible = false; _orbiting = false;
        foreach (var smoke in _smoke) smoke.Emitting = false;
        _camera.ClearCurrent(true);
    }

    public override void _Process(double delta)
    {
        if (!IsOpen) return;
        _elapsed += delta;
        _smoothDistance = Mathf.Lerp(_smoothDistance, _distance, 1f - Mathf.Exp(-(float)delta * 7));
        UpdateCamera();
        if (_train != null) _train.Position = new Vector3((float)((_elapsed * 1.25) % 45) - 22.5f, .49f, -12.2f);
        if (_boat != null)
        {
            _boat.Position = new Vector3(8f - (float)((_elapsed * .20) % 24), -.05f, 6.9f);
            _boat.Rotation = new Vector3(0, -.04f, Mathf.Sin((float)_elapsed * .7f) * .009f);
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!IsOpen) return;
        if (input is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Right) _orbiting = button.Pressed;
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp) _distance = Mathf.Max(7f, _distance - 2f);
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown) _distance = Mathf.Min(62f, _distance + 2f);
        }
        if (_orbiting && !Input.IsMouseButtonPressed(MouseButton.Right)) _orbiting = false;
        if (input is InputEventMouseMotion motion && _orbiting)
        {
            _yaw -= motion.Relative.X * .004f;
            _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * .003f, .26f, 1.22f);
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateCamera()
    {
        _camera.Position = _focus + new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(_pitch), Mathf.Sin(_pitch), Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * _smoothDistance;
        _camera.LookAt(GlobalPosition + _focus, Vector3.Up);
    }

    private Godot.Environment BuildEnvironment()
    {
        var skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("7699ad"), SkyHorizonColor = new Color("d9d5bb"),
            GroundHorizonColor = new Color("cec9ad"), GroundBottomColor = new Color("878d79"),
            SkyCurve = .22f, SunAngleMax = 12, SunCurve = .13f
        };
        return new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky, Sky = new Sky { SkyMaterial = skyMaterial },
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("bbc7c8"), AmbientLightEnergy = .32f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            FogEnabled = true, FogMode = Godot.Environment.FogModeEnum.Depth,
            FogLightColor = new Color("c2c8b9"), FogLightEnergy = .85f, FogDepthBegin = 48, FogDepthEnd = 145, FogDepthCurve = 1.7f,
            SsaoEnabled = true, SsaoIntensity = 1.3f, SsaoRadius = 1.0f, SsaoDetail = .65f,
            GlowEnabled = true, GlowIntensity = .24f, GlowBloom = .045f, GlowHdrThreshold = 1.45f,
            AdjustmentEnabled = true, AdjustmentSaturation = .86f, AdjustmentContrast = 1.06f
        };
    }

    private void CreateMaterials()
    {
        Mat("stone", "b6ad98"); Mat("stone_light", "d6c8ad"); Mat("stone_dark", "807d70");
        Mat("brick", "9f735b", true); Mat("brick_dark", "805846", true); Mat("brick_pale", "b58d71", true);
        Mat("plaster", "d4c9ad"); Mat("timber", "62513c"); Mat("wood", "84704e"); Mat("roof", "58636b"); Mat("roof_red", "946c55");
        Mat("copper", "688f84", false, .45f); Mat("iron", "343d3c", false, .5f); Mat("rail", "7a8381", false, .75f);
        Mat("window", "334b50", false, .3f); Mat("window_lit", "c3af76"); Mat("road", "7d8178"); Mat("gravel", "9e9c88");
        Mat("grass", "7c8a5d"); Mat("leaf", "596c47"); Mat("leaf_light", "748253"); Mat("leaf_dark", "435d40");
        Mat("white", "dbd5c2"); Mat("red", "84483d"); Mat("gold", "c2a56b", false, .55f);
        ((StandardMaterial3D)_materials["window_lit"]).EmissionEnabled = true;
        ((StandardMaterial3D)_materials["window_lit"]).Emission = new Color("aa833e");
        ((StandardMaterial3D)_materials["window_lit"]).EmissionEnergyMultiplier = .3f;
        SurfaceTexture("roof", false); SurfaceTexture("roof_red", false);
        SurfaceTexture("road", true); SurfaceTexture("gravel", true);
    }

    private void SurfaceTexture(string id, bool paving)
    {
        var image = Image.CreateEmpty(192, 192, false, Image.Format.Rgba8);
        var random = new Random(paving ? 729 : 412);
        var tones = new float[16, 16];
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) tones[x, y] = .80f + (float)random.NextDouble() * .20f;
        for (int y = 0; y < 192; y++) for (int x = 0; x < 192; x++)
        {
            int shifted = (x + (y / 12 % 2) * 6) % 192;
            bool seam = y % 12 == 0 || shifted % 12 == 0;
            float tone = seam ? (paving ? .70f : .63f) : tones[shifted / 12, y / 12] + (float)random.NextDouble() * .045f;
            image.SetPixel(x, y, new Color(tone, tone, tone, 1));
        }
        var material = (StandardMaterial3D)_materials[id];
        var normal = Image.CreateFromData(192, 192, false, Image.Format.Rgba8, image.GetData());
        normal.BumpMapToNormalMap(paving ? 1.2f : 2.4f); normal.GenerateMipmaps();
        material.NormalEnabled = true; material.NormalScale = .55f; material.NormalTexture = ImageTexture.CreateFromImage(normal);
        image.GenerateMipmaps();
        material.AlbedoTexture = ImageTexture.CreateFromImage(image);
        material.Uv1Scale = paving ? new Vector3(8, 5, 1) : new Vector3(1.5f, 1.8f, 1);
        material.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
    }

    private void Mat(string id, string hex, bool brick = false, float metallic = 0)
    {
        var m = new StandardMaterial3D { AlbedoColor = new Color(hex), Roughness = metallic > 0 ? .4f : .88f, Metallic = metallic };
        if (brick)
        {
            var image = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);
            var random = new Random(512);
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                bool mortar = y % 16 < 2 || (x + (y / 16 % 2) * 16) % 32 < 2;
                float shade = mortar ? .78f : .93f + (float)random.NextDouble() * .13f;
                image.SetPixel(x, y, new Color(shade, shade, shade, 1));
            }
            var normal = Image.CreateFromData(128, 128, false, Image.Format.Rgba8, image.GetData());
            normal.BumpMapToNormalMap(2.6f); normal.GenerateMipmaps();
            m.NormalEnabled = true; m.NormalScale = .60f; m.NormalTexture = ImageTexture.CreateFromImage(normal);
            image.GenerateMipmaps(); m.AlbedoTexture = ImageTexture.CreateFromImage(image);
            m.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
            m.Uv1Scale = new Vector3(2, 3, 1);
        }
        _materials[id] = m;
    }

    private void BuildCity()
    {
        if (_district != null) { RemoveChild(_district); _district.QueueFree(); }
        _district = new Node3D { Name = "IndustrialCity" }; AddChild(_district);
        _train = null!; _boat = null!;
        _batches.Clear(); _smoke.Clear(); _random = new Random(_seed);
        if (!string.IsNullOrEmpty(_sceneDefinition?.AuthoredScenePath))
        {
            var authored = ResourceLoader.Load<PackedScene>(_sceneDefinition.AuthoredScenePath)?.Instantiate();
            if (authored is Node3D model) { _district.AddChild(model); return; }
            authored?.Free();
            throw new System.IO.InvalidDataException("The authored settlement scene requires a Node3D root.");
        }
        BuildLandscape(); BuildStreets(); BuildHousing(); BuildCivicQuarter(); BuildIndustry(); BuildWaterfront(); BuildRailway(); BuildDetails();
        CommitBatches();
    }

    private void BuildLandscape()
    {
        // Continuous ground joins the urban patch to rolling countryside; no floating plinth.
        var mesh = new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
        const int n = 60; const float step = 2.6f;
        float Height(float x, float z)
        {
            if (z > 3.2f && z < 10.5f) return -.45f;
            float edge = Mathf.Clamp((Mathf.Abs(x) - 20) / 25, 0, 1) + Mathf.Clamp((-z - 15) / 28, 0, 1);
            return .02f + edge * (2.6f + 1.9f * Mathf.Sin(x * .072f) * Mathf.Cos(z * .12f));
        }
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
        {
            var a = new Vector3((x - 30) * step, 0, (z - 30) * step);
            var b = a + new Vector3(step, 0, 0); var c = a + new Vector3(step, 0, step); var d = a + new Vector3(0, 0, step);
            a.Y = Height(a.X, a.Z); b.Y = Height(b.X, b.Z); c.Y = Height(c.X, c.Z); d.Y = Height(d.X, d.Z);
            Tri(mesh, a, c, b); Tri(mesh, a, d, c);
        }
        mesh.GenerateNormals();
        var groundShader = new Shader { Code = "shader_type spatial; varying vec3 wp; void vertex(){wp=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;} void fragment(){float n=sin(wp.x*.27)*sin(wp.z*.21)+sin(wp.x*1.9+wp.z*.8)*.12; ALBEDO=mix(vec3(.27,.33,.20),vec3(.48,.51,.33),.5+n*.2);ROUGHNESS=.98;}" };
        _district.AddChild(new MeshInstance3D { Mesh = mesh.Commit(), MaterialOverride = new ShaderMaterial { Shader = groundShader } });
        Box("gravel", new Vector3(0, .055f, -5.5f), new Vector3(36, .09f, 16.8f));
        Box("grass", new Vector3(0, .065f, 13.9f), new Vector3(40, .08f, 6));
        var waterShader = new Shader { Code = @"shader_type spatial;
            varying vec3 world;
            void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;VERTEX.y+=sin(world.x*1.7+TIME*.7)*.018+sin(world.z*2.4+TIME)*.012;}
            void fragment(){float wave=sin(world.x*5.0+world.z*2.0+TIME*1.2)*sin(world.z*8.0-TIME*.8);float shimmer=pow(max(0.,sin(world.x*13.0+world.z*11.0+TIME*1.1)),24.0)*.015;
            ALBEDO=mix(vec3(.09,.18,.19),vec3(.20,.29,.27),wave*.13+.4)+shimmer;METALLIC=.12;ROUGHNESS=.48;SPECULAR=.25;NORMAL_MAP=vec3(.5+sin(world.x*3.0+TIME)*.018,.5+sin(world.z*5.0-TIME*.7)*.012,1.0);}" };
        _district.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(155, 7.3f), SubdivideWidth = 100, SubdivideDepth = 14 }, Position = new Vector3(0, -.08f, 6.85f), MaterialOverride = new ShaderMaterial { Shader = waterShader } });
        for (int i = 0; i < 120; i++)
        {
            float x = Range(-52, 52), z = Range(-48, -16);
            Tree(new Vector3(x, Height(x, z) + .04f, z), Range(.75f, 1.45f));
        }
        // Hedged field strips and a dirt approach let the town settle into a broader landscape.
        for (int field = 0; field < 6; field++)
        {
            float x = -24f - field * 2.4f;
            Box(field % 2 == 0 ? "grass" : "gravel", new Vector3(x, Height(x, -6) + .02f, -6), new Vector3(2.1f, .025f, 12f));
            for (int hedge = 0; hedge < 9; hedge++)
                Sphere("leaf_dark", new Vector3(x - 1.1f, Height(x, -11 + hedge * 1.25f) + .24f, -11 + hedge * 1.25f), new Vector3(.45f, .45f, .74f));
        }
        Box("road", new Vector3(-24, .105f, 1.2f), new Vector3(15, .035f, .8f));
    }

    private void BuildStreets()
    {
        foreach (float z in new[] { -11f, -7f, -3f, 1.2f })
        {
            Box("road", new Vector3(0, .12f, z), new Vector3(36, .025f, .8f));
            Box("stone", new Vector3(0, .16f, z - .49f), new Vector3(36, .08f, .16f));
            Box("stone", new Vector3(0, .16f, z + .49f), new Vector3(36, .08f, .16f));
        }
        foreach (float x in new[] { -15.4f, -9.4f, -3.4f, 3.2f, 9.4f, 15.4f })
        {
            Box("road", new Vector3(x, .125f, -5.3f), new Vector3(.8f, .03f, 17.4f));
            Box("stone", new Vector3(x - .48f, .16f, -5.3f), new Vector3(.13f, .07f, 17.4f));
            Box("stone", new Vector3(x + .48f, .16f, -5.3f), new Vector3(.13f, .07f, 17.4f));
        }
        // Cobblestone rhythm and gutters give the roads detail at inspection distance.
        for (float x = -17; x < 17; x += .28f)
            for (int row = 0; row < 3; row++) Box("stone_dark", new Vector3(x + row * .07f, .145f, 1.03f + row * .19f), new Vector3(.19f, .009f, .07f));
    }

    private void BuildHousing()
    {
        bool japanese = EastAsian;
        for (int blockX = 0; blockX < 3; blockX++) for (int blockZ = 0; blockZ < 3; blockZ++)
        {
            float startX = -14.7f + blockX * 6f, startZ = -10.35f + blockZ * 4f;
            for (int row = 0; row < 2; row++) for (int house = 0; house < (_population < 80000 ? 3 : _population < 250000 ? 4 : 5); house++)
            {
                float x = startX + house * .94f, z = startZ + row * 1.5f;
                if (blockX == 2 && blockZ is 0 or 1) continue; // A generous civic square, not an indiscriminate grid.
                float height = japanese ? Range(.72f, 1.1f) : Range(1.25f, 2.0f);
                Townhouse(new Vector3(x, .18f, z), .84f, 1.15f, height, row == 0 ? Mathf.Pi : 0, japanese);
            }
        }
        // A long river-facing merchant terrace.
        for (int i = 0; i < 16; i++) Townhouse(new Vector3(-14.6f + i * 1.03f, .2f, 2.3f), .94f, .95f, japanese ? 1.0f : Range(1.4f, 2.05f), 0, japanese);
        // Secondary streets beyond the rail line build visual depth.
        for (int i = 0; i < 30; i++)
            Townhouse(new Vector3(-15 + (i % 15) * 1.65f, .18f, -15.2f - (i / 15) * 2.1f), 1.05f, 1.15f, japanese ? .9f : Range(1.2f, 1.8f), 0, japanese);
    }

    private void Townhouse(Vector3 p, float width, float depth, float height, float yaw, bool japanese)
    {
        string facade = japanese ? "plaster" : _country == "GBR" ? (_random.Next(3) == 0 ? "brick_pale" : "brick") : (_random.Next(3) == 0 ? "brick_pale" : "plaster");
        string roof = japanese ? "roof" : _country is "PRU" or "SPA" or "POR" or "OTT" ? "roof_red" : "roof";
        LocalBox("stone_dark", p, new Vector3(0, .09f, 0), new Vector3(width + .04f, .18f, depth + .04f), yaw);
        LocalBox(facade, p, new Vector3(0, height / 2, 0), new Vector3(width, height, depth), yaw);
        LocalBox("stone_light", p, new Vector3(0, height - .025f, 0), new Vector3(width + .10f, .07f, depth + .09f), yaw);
        MeshBatch(japanese ? "jroof" : "roof", japanese ? _japanRoof : _roof, roof, new Transform3D(new Basis(Vector3.Up, yaw).Scaled(new Vector3(width + .18f, japanese ? .5f : .55f, depth + .2f)), p + new Vector3(0, height, 0)));
        int floors = japanese ? 2 : height > 1.65f ? 3 : 2;
        for (int f = 0; f < floors; f++)
        {
            float y = .30f + f * (height - .40f) / floors;
            for (int col = 0; col < 3; col++)
            {
                float x = (col - 1) * width * .28f;
                string glass = _random.Next(12) == 0 ? "window_lit" : "window";
                LocalBox("stone_light", p, new Vector3(x, y + .05f, depth / 2 + .008f), new Vector3(.18f, .25f, .026f), yaw);
                LocalBox(glass, p, new Vector3(x, y + .06f, depth / 2 + .025f), new Vector3(.12f, .18f, .025f), yaw);
                LocalBox(japanese ? "timber" : "stone_light", p, new Vector3(x, y + .055f, depth / 2 + .045f), new Vector3(.012f, .18f, .008f), yaw);
            }
            LocalBox(japanese ? "timber" : "stone", p, new Vector3(0, y + .26f, 0), new Vector3(width + .018f, .038f, depth + .018f), yaw);
            for (int side = -1; side <= 1; side += 2)
                LocalBox("window", p, new Vector3(side * (width / 2 + .005f), y + .06f, .12f), new Vector3(.015f, .17f, .14f), yaw);
        }
        LocalBox("timber", p, new Vector3(0, .20f, depth / 2 + .04f), new Vector3(.17f, .39f, .045f), yaw);
        LocalBox("stone_light", p, new Vector3(0, .04f, depth / 2 + .14f), new Vector3(.26f, .08f, .22f), yaw);
        if (japanese)
        {
            foreach (float x in new[] { -width / 2 + .05f, width / 2 - .05f })
                LocalBox("timber", p, new Vector3(x, height / 2, depth / 2 + .02f), new Vector3(.045f, height, .05f), yaw);
            LocalBox("wood", p, new Vector3(0, height * .53f, depth / 2 + .12f), new Vector3(width + .1f, .07f, .38f), yaw, -.16f);
        }
        else
        {
            LocalBox("brick_dark", p, new Vector3(width * .28f, height + .33f, -.15f), new Vector3(.13f, .62f, .20f), yaw);
            LocalBox("stone_light", p, new Vector3(width * .28f, height + .65f, -.15f), new Vector3(.19f, .07f, .25f), yaw);
        }
    }

    private void BuildCivicQuarter()
    {
        Box("stone", new Vector3(-.45f, .2f, -6.1f), new Vector3(5.0f, .1f, 7.5f));
        foreach (float z in new[] { -9.2f, -3.5f }) for (int i = 0; i < 5; i++) Tree(new Vector3(-2.3f + i * .95f, .3f, z), .62f);
        var p = new Vector3(-.3f, .28f, -6.2f);
        if (EastAsian)
        {
            Box("stone_light", p + new Vector3(0, .12f, 0), new Vector3(3.7f, .24f, 3.1f));
            for (int level = 0; level < 4; level++)
            {
                float w = 2.6f - level * .39f, y = .3f + level * .85f;
                Box("timber", p + new Vector3(0, y + .34f, 0), new Vector3(w, .66f, w * .8f));
                for (int j = -2; j <= 2; j++) Box("white", p + new Vector3(j * w / 6, y + .34f, w * .4f + .012f), new Vector3(w / 9, .39f, .018f));
                MeshBatch("jroof", _japanRoof, "roof", new Transform3D(Basis.Identity.Scaled(new Vector3(w + .9f, .62f, w * .8f + .75f)), p + new Vector3(0, y + .64f, 0)));
            }
            Cylinder("gold", p + new Vector3(0, 4.3f, 0), new Vector3(.07f, 1.05f, .07f));
            // A Japanese gate is specific to the Japanese kit.
            if (_country == "JAP") {
            foreach (float x in new[] { -1f, 1f }) Cylinder("red", new Vector3(x, 1.3f, -2.8f), new Vector3(.14f, 2.2f, .14f));
            Box("red", new Vector3(0, 2.32f, -2.8f), new Vector3(2.8f, .15f, .25f));
            Box("roof", new Vector3(0, 2.48f, -2.8f), new Vector3(3.1f, .13f, .34f));
            }
        }
        else if (_country is "OTT" or "RUS")
        {
            Box("stone_light", p + new Vector3(0, 1.4f, 0), new Vector3(3.5f, 2.8f, 3.1f));
            Sphere(_country == "OTT" ? "roof" : "gold", p + new Vector3(0, 3.2f, 0), new Vector3(3.4f, 2.2f, 3.0f));
            for (int side = -1; side <= 1; side += 2) {
                Cylinder("stone_light", p + new Vector3(side * 2.3f, 2.4f, -.3f), new Vector3(.38f, 4.7f, .38f));
                Sphere(_country == "OTT" ? "copper" : "gold", p + new Vector3(side * 2.3f, 4.85f, -.3f), new Vector3(.68f, .9f, .68f));
                Cylinder("gold", p + new Vector3(side * 2.3f, 5.6f, -.3f), new Vector3(.05f, .7f, .05f));
            }
            for(int x=-3;x<=3;x++) Box("window",p+new Vector3(x*.44f,1.5f,1.57f),new Vector3(.24f,.8f,.02f));
        }
        else
        {
            Box("stone_light", p + new Vector3(0, 1.12f, 0), new Vector3(3.3f, 2.25f, 2.3f));
            Box("stone_dark", p + new Vector3(0, .1f, 0), new Vector3(3.65f, .20f, 2.65f));
            MeshBatch("roof", _roof, "roof", new Transform3D(Basis.Identity.Scaled(new Vector3(3.65f, .9f, 2.6f)), p + new Vector3(0, 2.25f, 0)));
            for (int x = -3; x <= 3; x++)
            {
                Cylinder("stone_light", p + new Vector3(x * .43f, 1.1f, 1.39f), new Vector3(.13f, 1.8f, .13f));
                Box("stone", p + new Vector3(x * .43f, .25f, 1.39f), new Vector3(.23f, .15f, .23f));
                Box("window", p + new Vector3(x * .43f, 1.35f, 1.155f), new Vector3(.18f, .7f, .02f));
            }
            Box("stone_light", p + new Vector3(0, 2.08f, 1.40f), new Vector3(3.65f, .23f, .40f));
            Box("stone", p + new Vector3(0, .10f, 1.95f), new Vector3(3.8f, .18f, 1.0f));
            Box("stone_light", p + new Vector3(0, 3.5f, -.20f), new Vector3(.91f, 2.2f, .85f));
            Box("stone", p + new Vector3(0, 4.62f, -.20f), new Vector3(1.05f, .16f, 1.0f));
            if (_country == "GBR")
            {
                Sphere("copper", p + new Vector3(0, 5.03f, -.20f), new Vector3(.88f, .88f, .88f));
                Cylinder("gold", p + new Vector3(0, 5.65f, -.20f), new Vector3(.04f, .66f, .04f));
            }
            else
            {
                var spire = new CylinderMesh { TopRadius = .015f, BottomRadius = .6f, Height = 2.1f, RadialSegments = 4 };
                MeshBatch("spire", spire, "copper", new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 4), p + new Vector3(0, 5.72f, -.20f)));
            }
            for (int side = -1; side <= 1; side += 2) Box("window", p + new Vector3(side * .46f, 3.8f, -.2f), new Vector3(.02f, .6f, .33f));
            // Fountain basin and central stone monument.
            Cylinder("stone_light", new Vector3(-.3f, .35f, -2.0f), new Vector3(1.1f, .24f, 1.1f));
            Cylinder("copper", new Vector3(-.3f, .49f, -2.0f), new Vector3(.9f, .035f, .9f));
            Cylinder("stone_light", new Vector3(-.3f, .8f, -2.0f), new Vector3(.18f, .6f, .18f));
            Sphere("stone_light", new Vector3(-.3f, 1.15f, -2.0f), new Vector3(.3f, .4f, .3f));
        }
    }

    private void BuildIndustry()
    {
        int factories = _factories;
        for (int i = 0; i < factories; i++)
        {
            float x = 5.2f + (i % 2) * 6.1f, z = -8.8f + (i / 2) * 4;
            float h = 1.7f + (i % 3) * .25f;
            Box("brick_dark", new Vector3(x, .2f + h / 2, z), new Vector3(3.8f, h, 2.3f));
            Box("stone", new Vector3(x, .3f, z), new Vector3(3.98f, .20f, 2.45f));
            for (int w = 0; w < 7; w++) for (int floor = 0; floor < 2; floor++)
            {
                var p = new Vector3(x - 1.6f + w * .53f, .62f + floor * .72f, z + 1.17f);
                Box("stone", p, new Vector3(.35f, .56f, .06f));
                Box("window", p + new Vector3(0, 0, .04f), new Vector3(.27f, .46f, .03f));
                Box("stone_light", p + new Vector3(0, 0, .06f), new Vector3(.025f, .46f, .016f));
                Box("stone_light", p + new Vector3(0, -.035f, .06f), new Vector3(.27f, .025f, .016f));
            }
            for (int tooth = 0; tooth < 4; tooth++)
                MeshBatch("roof", _roof, "roof", new Transform3D(Basis.Identity.Scaled(new Vector3(.98f, .60f, 2.45f)), new Vector3(x - 1.46f + tooth * .98f, h + .20f, z)));
            float chimneyHeight = 4.2f + i * .39f;
            var chimney = new CylinderMesh { TopRadius = .19f, BottomRadius = .34f, Height = chimneyHeight, RadialSegments = 14 };
            var chimneyPos = new Vector3(x + 2.25f, .2f + chimneyHeight / 2, z - .62f);
            MeshBatch("stack" + i, chimney, "brick_dark", new Transform3D(Basis.Identity, chimneyPos));
            for (int ring = 0; ring < 4; ring++) Cylinder("stone_dark", new Vector3(chimneyPos.X, chimneyHeight - ring * .13f, chimneyPos.Z), new Vector3(.47f, .06f, .47f));
            AddSmoke(_district, new Vector3(chimneyPos.X, chimneyHeight + .2f, chimneyPos.Z), false);
            Box("timber", new Vector3(x - .85f, .5f, z + 1.3f), new Vector3(.7f, .7f, .09f));
            for (int crate = 0; crate < 8; crate++)
                Box("wood", new Vector3(x - 1.4f + crate % 4 * .45f, .37f + crate / 4 * .32f, z + 1.65f), new Vector3(.36f, .32f, .35f));
        }
        // Gasometer: open iron cage around a cylindrical reservoir.
        Cylinder("iron", new Vector3(16.5f, .95f, -6.4f), new Vector3(2.0f, 1.55f, 2.0f));
        Cylinder("stone", new Vector3(16.5f, .27f, -6.4f), new Vector3(2.25f, .22f, 2.25f));
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10;
            Cylinder("iron", new Vector3(16.5f + Mathf.Cos(a) * 1.1f, 1.4f, -6.4f + Mathf.Sin(a) * 1.1f), new Vector3(.055f, 2.35f, .055f));
        }
    }

    private void BuildWaterfront()
    {
        // Block masonry quay, coping, mooring posts and merchant warehouses.
        Box("stone_dark", new Vector3(0, -.05f, 3.23f), new Vector3(41, .75f, .40f));
        Box("stone_light", new Vector3(0, .36f, 3.16f), new Vector3(41, .13f, .59f));
        Box("stone_dark", new Vector3(0, -.09f, 10.55f), new Vector3(42, .67f, .36f));
        for (float x = -19; x < 20; x += .58f)
        {
            Box("stone", new Vector3(x, .015f, 3.44f), new Vector3(.53f, .19f, .025f));
            Box("stone", new Vector3(x + .22f, -.20f, 3.44f), new Vector3(.53f, .18f, .025f));
        }
        for (float x = -18; x < 20; x += 2.0f) Cylinder("iron", new Vector3(x, .55f, 3.17f), new Vector3(.11f, .29f, .11f));
        for (int dock = 0; dock < (_port ? 3 : 1); dock++)
        {
            float x = 7.5f + dock * 3.4f;
            Box("wood", new Vector3(x, .12f, 4.35f), new Vector3(1.1f, .16f, 2.8f));
            for (int i = 0; i < 12; i++) Box("timber", new Vector3(x, .21f, 3.1f + i * .23f), new Vector3(1.1f, .025f, .018f));
            for (int side = -1; side <= 1; side += 2)
                Cylinder("timber", new Vector3(x + side * .49f, -.12f, 5.55f), new Vector3(.12f, 1.35f, .12f));
            Box("iron", new Vector3(x + .2f, 1.6f, 3.65f), new Vector3(.10f, 2.7f, .10f));
            Box("iron", new Vector3(x + .2f, 2.84f, 4.3f), new Vector3(.12f, .12f, 1.45f), -.27f);
            Cylinder("iron", new Vector3(x + .53f, 2.0f, 4.95f), new Vector3(.025f, 1.6f, .025f));
        }
        // Elegant five-span masonry bridge, with pierced arches rather than solid blocks.
        float bridgeX = -7.8f;
        Box("stone", new Vector3(bridgeX, .70f, 6.85f), new Vector3(1.75f, .17f, 8.2f));
        Box("road", new Vector3(bridgeX, .80f, 6.85f), new Vector3(1.33f, .045f, 8.2f));
        for (int i = 0; i <= 5; i++)
        {
            float z = 3.13f + i * 1.48f;
            Box("stone_light", new Vector3(bridgeX, .15f, z), new Vector3(1.8f, 1.07f, .21f));
            if (i < 5) for (int segment = 0; segment < 12; segment++)
            {
                float a = segment * Mathf.Pi / 12;
                var pos = new Vector3(bridgeX, -.03f + Mathf.Sin(a) * .61f, z + .74f - Mathf.Cos(a) * .65f);
                for (int side = -1; side <= 1; side += 2)
                    Box("stone_light", pos + new Vector3(side * .79f, 0, 0), new Vector3(.22f, .23f, .20f), 0, -a);
            }
        }
        foreach (int side in new[] { -1, 1 })
        {
            Box("stone_light", new Vector3(bridgeX + side * .79f, 1.07f, 6.85f), new Vector3(.12f, .15f, 8.3f));
            for (float z = 2.9f; z < 11f; z += .43f) Box("stone", new Vector3(bridgeX + side * .79f, .95f, z), new Vector3(.1f, .35f, .09f));
        }
        for (int i = 0; i < 6; i++) Townhouse(new Vector3(-15 + i * 2.5f, .17f, 12.6f), 1.4f, 1.8f, 1.6f, Mathf.Pi, EastAsian);
        _boat = new Node3D(); _district.AddChild(_boat);
        DirectBox(_boat, "timber", new Vector3(0, .12f, 0), new Vector3(2.55f, .34f, .60f));
        DirectBox(_boat, "wood", new Vector3(0, .33f, 0), new Vector3(2.1f, .12f, .49f));
        DirectBox(_boat, "white", new Vector3(.64f, .58f, 0), new Vector3(.63f, .44f, .48f));
        DirectBox(_boat, "roof", new Vector3(.64f, .83f, 0), new Vector3(.77f, .08f, .58f));
        DirectCylinder(_boat, "iron", new Vector3(.12f, .82f, 0), new Vector3(.12f, .91f, .12f));
        for (int i = 0; i < 4; i++) DirectBox(_boat, "wood", new Vector3(-.82f + i * .26f, .48f, 0), new Vector3(.21f, .22f, .37f));
    }

    private void BuildRailway()
    {
        if (_rail <= 0 && _country != "GBR") { Box("road", new Vector3(0, .16f, -12.2f), new Vector3(48, .04f, 1.3f)); return; }
        Box("stone_dark", new Vector3(0, .21f, -12.2f), new Vector3(48, .25f, 1.5f));
        for (float x = -24; x < 24; x += .25f) Box("timber", new Vector3(x, .36f, -12.2f), new Vector3(.12f, .06f, 1.2f));
        foreach (float z in new[] { -12.57f, -11.83f }) Box("rail", new Vector3(0, .415f, z), new Vector3(48, .07f, .055f));
        // Covered station platform with a glazed, pitched iron canopy.
        Box("stone_light", new Vector3(-9, .42f, -13.75f), new Vector3(8f, .47f, 1.55f));
        for (float x = -12.5f; x < -5; x += 1.25f)
            Box("iron", new Vector3(x, 1.37f, -13.25f), new Vector3(.07f, 1.8f, .07f));
        MeshBatch("roof", _roof, "copper", new Transform3D(Basis.Identity.Scaled(new Vector3(8.5f, .65f, 2.1f)), new Vector3(-9, 2.2f, -13.75f)));
        Box("brick", new Vector3(-9, .95f, -15.0f), new Vector3(5.5f, 1.65f, 1.45f));
        for (int i = 0; i < 10; i++) Box("window", new Vector3(-11.3f + i * .5f, 1.03f, -14.26f), new Vector3(.24f, .66f, .025f));
        if (_rail <= 0 && _country != "GBR")
        {
            // Without the technology, this is a proposed transport corridor under construction.
            for (int i = 0; i < 8; i++) Box("wood", new Vector3(-4 + i * .34f, .59f, -12.9f), new Vector3(.25f, .4f, .5f));
            return;
        }
        _train = new Node3D(); _district.AddChild(_train);
        DirectBox(_train, "iron", new Vector3(0, .23f, 0), new Vector3(1.85f, .20f, .71f));
        var boiler = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .24f, BottomRadius = .24f, Height = 1.24f, RadialSegments = 18 }, MaterialOverride = _materials["iron"], Position = new Vector3(.19f, .54f, 0), RotationDegrees = new Vector3(0, 0, 90) };
        _train.AddChild(boiler);
        DirectBox(_train, _country == "PRU" ? "red" : "leaf_dark", new Vector3(-.61f, .61f, 0), new Vector3(.50f, .66f, .64f));
        DirectBox(_train, "roof", new Vector3(-.61f, .99f, 0), new Vector3(.66f, .10f, .85f));
        DirectBox(_train, "window_lit", new Vector3(-.61f, .76f, .326f), new Vector3(.24f, .23f, .018f));
        DirectCylinder(_train, "iron", new Vector3(.64f, .99f, 0), new Vector3(.14f, .67f, .14f));
        DirectCylinder(_train, "gold", new Vector3(.13f, .89f, 0), new Vector3(.17f, .21f, .17f));
        int carriages = _rail > 0 ? 4 : 2;
        for (int carriage = 0; carriage < carriages; carriage++)
        {
            float x = -2.13f - carriage * 1.8f;
            DirectBox(_train, "timber", new Vector3(x, .56f, 0), new Vector3(1.55f, .66f, .72f));
            DirectBox(_train, "roof", new Vector3(x, .94f, 0), new Vector3(1.65f, .10f, .83f));
            for (int w = 0; w < 5; w++) DirectBox(_train, "window_lit", new Vector3(x - .58f + w * .28f, .67f, .37f), new Vector3(.17f, .26f, .015f));
            foreach (float wheel in new[] { -.52f, .52f }) TrainWheels(_train, x + wheel, .18f);
        }
        foreach (float x in new[] { -.66f, -.11f, .44f }) TrainWheels(_train, x, .19f);
        AddSmoke(_train, new Vector3(.64f, 1.34f, 0), true);
    }

    private void BuildDetails()
    {
        for (float x = -17; x < 18; x += 2.1f)
        {
            Lamp(new Vector3(x, .21f, 1.8f));
            if (x < 2 || x > 16) Tree(new Vector3(x, .18f, -.07f), Range(.6f, .85f));
        }
        for (float x = -19; x < 21; x += 2.5f) Tree(new Vector3(x, .13f, 14.6f), Range(.8f, 1.25f));
        // Market square: striped canvas awnings, crates and tiny passersby.
        for (int i = 0; i < 6; i++)
        {
            float x = -2.25f + (i % 3) * 1.32f, z = -.65f + (i / 3) * 1.1f;
            Box("wood", new Vector3(x, .55f, z), new Vector3(.9f, .58f, .5f));
            for (int side = -1; side <= 1; side += 2) Box("timber", new Vector3(x + side * .46f, .93f, z), new Vector3(.045f, 1.3f, .045f));
            for (int stripe = 0; stripe < 6; stripe++) Box(stripe % 2 == 0 ? "white" : "red", new Vector3(x - .43f + stripe * .17f, 1.56f, z), new Vector3(.17f, .045f, .81f), 0, -.09f);
        }
        for (int i = 0; i < 90; i++)
        {
            float x = Range(-16, 16), z = i % 3 == 0 ? 1.4f : i % 3 == 1 ? -3.0f : -7.0f;
            Cylinder(i % 4 == 0 ? "red" : "iron", new Vector3(x, .34f, z + Range(-.27f, .27f)), new Vector3(.075f, .26f, .075f));
            Sphere("stone_light", new Vector3(x, .52f, z), new Vector3(.085f, .09f, .085f));
        }
        for (int i = 0; i < 8; i++)
        {
            float x = -13 + i * 3.9f, z = i % 2 == 0 ? -3 : 1.1f;
            Box("timber", new Vector3(x, .52f, z), new Vector3(.62f, .45f, .40f));
            Box("roof", new Vector3(x, .80f, z), new Vector3(.69f, .09f, .5f));
            for (int side = -1; side <= 1; side += 2) Sphere("iron", new Vector3(x - .2f, .32f, z + side * .24f), new Vector3(.21f, .21f, .055f));
        }
    }

    private void Tree(Vector3 p, float scale)
    {
        Cylinder("timber", p + new Vector3(0, .59f * scale, 0), new Vector3(.10f, 1.18f, .10f) * scale);
        for (int i = 0; i < 7; i++)
        {
            float a = i * 2.399f;
            var offset = new Vector3(Mathf.Cos(a) * Range(.17f, .37f), 1.02f + (i % 3) * .20f + Range(-.1f, .1f), Mathf.Sin(a) * Range(.17f, .37f)) * scale;
            Sphere(i % 3 == 0 ? "leaf_light" : i % 3 == 1 ? "leaf" : "leaf_dark", p + offset, new Vector3(Range(.58f, .80f), Range(.65f, .97f), Range(.60f, .84f)) * scale);
        }
    }

    private void Lamp(Vector3 p)
    {
        Cylinder("iron", p + new Vector3(0, .63f, 0), new Vector3(.035f, 1.25f, .035f));
        Cylinder("iron", p + new Vector3(0, .09f, 0), new Vector3(.095f, .18f, .095f));
        Box("gold", p + new Vector3(0, 1.31f, 0), new Vector3(.13f, .20f, .13f));
        Box("iron", p + new Vector3(0, 1.44f, 0), new Vector3(.19f, .06f, .19f));
    }

    private void AddSmoke(Node3D parent, Vector3 position, bool engine)
    {
        var gradient = new Gradient { Colors = new[] { new Color(.52f, .52f, .47f, .20f), new Color(.65f, .64f, .57f, .17f), new Color(.74f, .73f, .65f, 0) }, Offsets = new[] { 0f, .38f, 1f } };
        var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float d = new Vector2(x - 15.5f, y - 15.5f).Length() / 15.5f;
            image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Max(0, 1 - d), 1.5f)));
        }
        var material = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(image), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = false, CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        var curve = new Curve(); curve.AddPoint(new Vector2(0, .25f)); curve.AddPoint(new Vector2(1, 2.0f));
        var particles = new CpuParticles3D
        {
            Position = position, Amount = engine ? 20 : 34, Lifetime = engine ? 3.8f : 7.2f,
            Preprocess = engine ? 2 : 6, Direction = new Vector3(.45f, 1, .10f), Spread = 12,
            Gravity = new Vector3(.12f, .035f, .025f), InitialVelocityMin = .32f, InitialVelocityMax = .52f,
            ScaleAmountMin = engine ? .20f : .45f, ScaleAmountMax = engine ? .40f : .72f,
            ScaleAmountCurve = curve, ColorRamp = gradient,
            Mesh = new QuadMesh { Size = Vector2.One, Material = material },
            LocalCoords = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(particles); _smoke.Add(particles);
    }

    private void TrainWheels(Node3D parent, float x, float y)
    {
        foreach (int side in new[] { -1, 1 })
        {
            var wheel = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .19f, BottomRadius = .19f, Height = .07f, RadialSegments = 14 }, MaterialOverride = _materials["iron"], Position = new Vector3(x, y, side * .38f), RotationDegrees = new Vector3(90, 0, 0) };
            parent.AddChild(wheel);
        }
    }

    private void DirectBox(Node3D parent, string material, Vector3 position, Vector3 size) => parent.AddChild(new MeshInstance3D { Mesh = _cube, MaterialOverride = _materials[material], Position = position, Scale = size });
    private void DirectCylinder(Node3D parent, string material, Vector3 position, Vector3 size) => parent.AddChild(new MeshInstance3D { Mesh = _cylinder, MaterialOverride = _materials[material], Position = position, Scale = size });

    private void LocalBox(string material, Vector3 origin, Vector3 offset, Vector3 size, float yaw, float pitch = 0)
    {
        var basis = new Basis(Vector3.Up, yaw);
        var transform = new Transform3D(basis * new Basis(Vector3.Right, pitch).Scaled(size), origin + basis * offset);
        MeshBatch("box", _cube, material, transform);
    }
    private void Box(string material, Vector3 p, Vector3 size, float yaw = 0, float pitch = 0) => MeshBatch("box", _cube, material, new Transform3D(new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, pitch).Scaled(size), p));
    private void Sphere(string material, Vector3 p, Vector3 size) => MeshBatch("sphere", _sphere, material, new Transform3D(Basis.Identity.Scaled(size), p));
    private void Cylinder(string material, Vector3 p, Vector3 size) => MeshBatch("cylinder", _cylinder, material, new Transform3D(Basis.Identity.Scaled(size), p));
    private void MeshBatch(string key, Mesh mesh, string material, Transform3D transform)
    {
        string id = key + ":" + material;
        if (!_batches.TryGetValue(id, out var batch)) { batch = new Batch { Mesh = mesh, Material = _materials[material] }; _batches[id] = batch; }
        batch.Transforms.Add(transform);
    }
    private void CommitBatches()
    {
        foreach (var b in _batches.Values)
        {
            var mesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = b.Mesh, InstanceCount = b.Transforms.Count };
            for (int i = 0; i < b.Transforms.Count; i++) mesh.SetInstanceTransform(i, b.Transforms[i]);
            _district.AddChild(new MultiMeshInstance3D { Multimesh = mesh, MaterialOverride = b.Material });
        }
    }
    private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    private static Mesh RoofMesh(bool japanese)
    {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        int segments = japanese ? 6 : 2;
        float Profile(float z) => japanese ? .48f * Mathf.Pow(1 - Mathf.Abs(z) * 2, .60f) + .10f * Mathf.Pow(Mathf.Abs(z) * 2, 8) : .5f - Mathf.Abs(z);
        for (int i = 0; i < segments; i++)
        {
            float z0 = -.5f + (float)i / segments, z1 = -.5f + (float)(i + 1) / segments;
            var a = new Vector3(-.5f, Profile(z0), z0); var b = new Vector3(.5f, Profile(z0), z0);
            var c = new Vector3(.5f, Profile(z1), z1); var d = new Vector3(-.5f, Profile(z1), z1);
            Tri(s, a, c, b); Tri(s, a, d, c);
            Tri(s, a, new Vector3(-.5f, 0, z1), d); Tri(s, a, new Vector3(-.5f, 0, z0), new Vector3(-.5f, 0, z1));
            Tri(s, b, c, new Vector3(.5f, 0, z1)); Tri(s, b, new Vector3(.5f, 0, z1), new Vector3(.5f, 0, z0));
        }
        s.GenerateNormals(); s.GenerateTangents(); return s.Commit();
    }
    private static void Tri(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c)
    {
        // Godot's front-face convention is clockwise.
        s.SetUV(new Vector2(a.X, a.Z)); s.AddVertex(a); s.SetUV(new Vector2(c.X, c.Z)); s.AddVertex(c); s.SetUV(new Vector2(b.X, b.Z)); s.AddVertex(b);
    }
}
