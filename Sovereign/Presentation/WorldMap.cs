using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

/// <summary>A geographic, freely navigable 3D atlas. All meshes are generated locally.</summary>
public partial class WorldMap : Node3D
{
    public event Action<string>? CountrySelected;
    public event Action<string>? CitySelected;
    public event Action<string>? HistoricalCountrySelected;
    private readonly Dictionary<string, (Vector3 Position, Label3D Label, Node3D Pin)> _cityMarkers = new();
    private readonly Dictionary<string, CityDefinition> _cityDefinitions = GeographyCatalog.Cities.ToDictionary(c => c.Id);
    private readonly Dictionary<string, CountryDefinition> _countryDefinitions = GeographyCatalog.Countries.ToDictionary(c => c.Id);
    private readonly Dictionary<string, (long Population, decimal Gdp)> _cityEconomy = new();
    private readonly Dictionary<string, (long Population, decimal Gdp)> _countryEconomy = new();
    private readonly Dictionary<string, Label3D> _historicalLabels = new();
    private readonly List<Rect2> _labelBounds = new();
    private readonly List<(string Id, double Area)> _historicalLabelOrder = new();
    private string? _historicalSelection;
    private double _nextLabelUpdate;
    private Font _mapSerif = null!;
    private Font _mapSans = null!;
    private StandardMaterial3D _selectedRingMaterial = null!;
    private StandardMaterial3D _otherRingMaterial = null!;
    private StandardMaterial3D _riverMaterial = null!;
    private Node3D _rivers = null!;

    private const float MapScale = 0.12f;
    private readonly Dictionary<string, CountryVisual> _countries = new();
    private readonly List<Vector2[]> _land = new();
    private ShaderMaterial _terrainMaterial = null!, _oceanMaterial = null!;
    private MultiMeshInstance3D _forest = null!;
    private byte[] _heightData = Array.Empty<byte>();
    private Texture2D _physicalTexture = null!;
    private readonly List<Label3D> _regionalLabels = new();
    private readonly List<Ship> _ships = new();
    private Camera3D _camera = null!;
    private Node3D _terrain = null!;
    private Vector3 _focus = Geo(18, 40);
    private Vector3 _currentFocus = Geo(18, 40);
    private float _zoom = 7.2f;
    private float _currentZoom = 7.2f;
    private bool _dragging;
    private string _selected = "GBR";
    private string _mode = "Political";
    private double _time;
    private readonly Color _gold = new("d4b06e");
    private readonly Color _cream = new("e6debf");
    private readonly Color _sage = new("7e9275");

    private sealed class CountryVisual
    {
        public string Id = "";
        public Vector3 Capital;
        public Node3D Root = null!;
        public Node3D City = null!;
        public Node3D Marker = null!;
        public MeshInstance3D Ring = null!;
        public Label3D Name = null!;
        public Label3D CapitalLabel = null!;
        public Node3D Train = null!;
        public int Industry = -1;
        public int Rail = -1;
    }

    private sealed class Ship
    {
        public Node3D Node = null!;
        public Vector3[] Route = Array.Empty<Vector3>();
        public float Phase;
        public float Speed;
    }

    public override void _Ready()
    {
        // Separate font resources prevent map MSDF rendering settings changing the interface fonts.
        _mapSerif = (Font)GD.Load<Font>("res://Assets/fonts/SourceHanSerifSC-Regular.otf").Duplicate();
        _mapSans = (Font)GD.Load<Font>("res://Assets/fonts/SourceHanSansSC-Regular.otf").Duplicate();
        foreach (var font in new[] { _mapSerif, _mapSans }.OfType<FontFile>())
        {
            font.MultichannelSignedDistanceField = true;
            font.MsdfPixelRange = 8;
            font.MsdfSize = 64;
        }
        _selectedRingMaterial = Material(_gold, true);
        _otherRingMaterial = Material(new Color("acb2a0"), true);
        BuildEnvironment();
        ReadGeography();
        BuildOcean();
        BuildLand();
        BuildRivers();
        BuildRelief();
        BuildGeographicLabels();
        foreach (var country in GeographyCatalog.Countries)
        {
            var capital = _cityDefinitions[country.CapitalCityId];
            AddCountry(country.Id, country.Name.ToUpperInvariant(), capital.Name, (float)capital.Longitude, (float)capital.Latitude, new Color(country.ColorHex), new Vector3(.30f, 0, -.22f));
            SetDevelopment(country.Id, 2, country.Id == "GBR" ? 1 : 0);
        }
        foreach (var city in GeographyCatalog.Cities) AddCityMarker(city);
        BuildHistoricalLabels();
        BuildProvinces();
        BuildMaritimeRoutes();
        UpdateSelection();
    }

    private static Vector3 Geo(float longitude, float latitude, float height = 0.16f) => new(longitude * MapScale, height, -latitude * MapScale);

    private StandardMaterial3D Material(Color color, bool unshaded = false, float metallic = 0)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = 0.88f,
            ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
    }

    private void BuildEnvironment()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("102631"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("d0d1bd"),
            AmbientLightEnergy = 0.50f,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            FogEnabled = true,
            FogLightColor = new Color("bdba9b"),
            FogDensity = 0.0014f,
            FogHeight = 2.5f,
            FogHeightDensity = 0.018f,
            FogDepthBegin = 21,
            FogDepthEnd = 85
        };
        AddChild(new WorldEnvironment { Environment = environment });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-44, -48, 0),
            LightColor = new Color("f6f2e6"),
            LightEnergy = 0.86f,
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowMaxDistance = 45,
            ShadowBias = 0.08f,
            ShadowNormalBias = 0.75f
        });
        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Perspective,
            Fov = 40,
            Near = 0.03f,
            Far = 140,
            Current = true,
            Environment = environment
        };
        AddChild(_camera);
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        UpdateCamera();
    }

    private void BuildOcean()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/map/ocean.gdshader") }; _oceanMaterial = material;
        material.SetShaderParameter("physical", _physicalTexture);
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(85, 52), SubdivideWidth = 1, SubdivideDepth = 1 },
            Position = new Vector3(0, 0.018f, 0),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    private void ReadGeography()
    {
        string raw = Godot.FileAccess.GetFileAsString("res://Assets/map/ne_50m_land.geojson");
        _heightData = Godot.FileAccess.GetFileAsBytes("res://Assets/map/earth_height.bin");
        _physicalTexture = GD.Load<Texture2D>("res://Assets/map/earth_physical.png");
        if (string.IsNullOrEmpty(raw))
        {
            GD.PushError("The Natural Earth land dataset is missing.");
            return;
        }
        using var document = JsonDocument.Parse(raw);
        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var geometry = feature.GetProperty("geometry");
            var coordinates = geometry.GetProperty("coordinates");
            if (geometry.GetProperty("type").GetString() == "Polygon") ReadRing(coordinates[0]);
            else if (geometry.GetProperty("type").GetString() == "MultiPolygon")
                foreach (var polygon in coordinates.EnumerateArray()) ReadRing(polygon[0]);
        }
    }

    private void ReadRing(JsonElement points)
    {
        var ring = new List<Vector2>();
        foreach (var point in points.EnumerateArray())
        {
            var value = new Vector2(point[0].GetSingle() * MapScale, -point[1].GetSingle() * MapScale);
            if (ring.Count == 0 || ring[^1].DistanceSquaredTo(value) > 0.0000001f) ring.Add(value);
        }
        if (ring.Count > 2 && ring[0].DistanceSquaredTo(ring[^1]) < 0.0000001f) ring.RemoveAt(ring.Count - 1);
        if (ring.Count >= 3) _land.Add(ring.ToArray());
    }

    private void BuildLand()
    {
        _terrainMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/map/historical_terrain.gdshader") };
        _terrainMaterial.SetShaderParameter("landcover", GD.Load<Texture2D>("res://Assets/map/earth_landcover.jpg"));
        _terrainMaterial.SetShaderParameter("physical", _physicalTexture);
        _terrainMaterial.SetShaderParameter("territory_ids", GD.Load<Texture2D>("res://Assets/map/historical/territory_ids.png"));
        var palette = Image.CreateEmpty(512, 1, false, Image.Format.Rgba8);
        palette.Fill(Colors.Transparent);
        foreach (var country in HistoricalWorldAtlas.Countries) palette.SetPixel(country.ColorIndex, 0, new Color(country.ColorHex));
        _terrainMaterial.SetShaderParameter("territory_palette", ImageTexture.CreateFromImage(palette));
        // Denser geographic tiles follow the existing elevation samples across Europe and East Asia.
        // Elevation comes from GEBCO, not hand-placed symbolic mountains.
        for (int lon = -180; lon < 180; lon += 20)
        for (int lat = 90; lat > -90; lat -= 20)
        {
            float south = Math.Max(-90, lat - 20);
            bool detailed = (lon >= -20 && lon < 60 && lat >= 30 && lat <= 70)
                || (lon >= 80 && lon < 160 && lat >= 10 && lat <= 50);
            BuildTerrainTile(lon, lat, 20, lat - south, detailed ? 192 : 64);
        }
        var shore = new List<Vector3>();
        foreach (var ring in _land)
        for (int j = 0; j < ring.Length; j++)
        {
            var a = ring[j]; var b = ring[(j + 1) % ring.Length];
            if (a.DistanceSquaredTo(b) > 4) continue;
            shore.Add(new Vector3(a.X, 0.057f, a.Y));
            shore.Add(new Vector3(b.X, 0.057f, b.Y));
        }
        Lines(shore, Material(new Color("6f7864"), true));
    }

    private float SampleChannel(float longitude, float latitude, int channel)
    {
        float x = Mathf.Clamp((longitude + 180) / 360 * 4095, 0, 4094.99f);
        float y = Mathf.Clamp((90 - latitude) / 180 * 2047, 0, 2046.99f);
        int ix = (int)x; int iy = (int)y;
        float fx = x - ix; float fy = y - iy;
        int a = (iy * 4096 + ix) * 2 + channel;
        float top = Mathf.Lerp(_heightData[a], _heightData[a + 2], fx);
        float bottom = Mathf.Lerp(_heightData[a + 8192], _heightData[a + 8194], fx);
        return Mathf.Lerp(top, bottom, fy) / 255f;
    }

    private float TerrainHeight(float longitude, float latitude)
    {
        float elevation = SampleChannel(longitude, latitude, 0);
        float mask = SampleChannel(longitude, latitude, 1);
        return 0.047f + Mathf.Pow(elevation, 0.84f) * 0.58f * mask;
    }

    private void BuildTerrainTile(float west, float north, float width, float height, int divisions)
    {
        bool anyLand = false;
        for (int r = 0; r <= 12 && !anyLand; r++)
        for (int c = 0; c <= 12; c++)
            if (SampleChannel(west + width * c / 12, north - height * r / 12, 1) > 0.3f) { anyLand = true; break; }
        if (!anyLand) return;
        int side = divisions + 1;
        var positions = new Vector3[side * side];
        var normals = new Vector3[side * side];
        var uvs = new Vector2[side * side];
        var tangents = new float[side * side * 4];
        float dx = width / divisions; float dy = height / divisions;
        for (int row = 0; row < side; row++)
        for (int col = 0; col < side; col++)
        {
            int index = row * side + col;
            float lon = west + col * dx;
            float lat = north - row * dy;
            positions[index] = Geo(lon, lat, TerrainHeight(lon, lat));
            float slopeX = TerrainHeight(lon - dx, lat) - TerrainHeight(lon + dx, lat);
            float slopeZ = TerrainHeight(lon, lat + dy) - TerrainHeight(lon, lat - dy);
            normals[index] = new Vector3(slopeX / (2 * dx * MapScale), 1, slopeZ / (2 * dy * MapScale)).Normalized();
            uvs[index] = new Vector2((lon + 180) / 360, (90 - lat) / 180);
            tangents[index * 4] = 1; tangents[index * 4 + 3] = -1;
        }
        var indices = new int[divisions * divisions * 6];
        int k = 0;
        for (int row = 0; row < divisions; row++)
        for (int col = 0; col < divisions; col++)
        {
            int a = row * side + col;
            indices[k++] = a; indices[k++] = a + 1; indices[k++] = a + side;
            indices[k++] = a + 1; indices[k++] = a + side + 1; indices[k++] = a + side;
        }
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Tangent] = tangents;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = _terrainMaterial });
    }

    private void BuildGrid()
    {
        var grid = new List<Vector3>();
        for (int longitude = -180; longitude <= 180; longitude += 15)
        {
            grid.Add(Geo(longitude, -75, 0.017f));
            grid.Add(Geo(longitude, 85, 0.017f));
        }
        for (int latitude = -75; latitude <= 85; latitude += 15)
        {
            grid.Add(Geo(-180, latitude, 0.017f));
            grid.Add(Geo(180, latitude, 0.017f));
        }
        Lines(grid, Material(new Color("30505a"), true));
        var equator = new List<Vector3>();
        for (int longitude = -180; longitude < 180; longitude += 3)
        {
            equator.Add(Geo(longitude, 0, 0.022f));
            equator.Add(Geo(longitude + 1, 0, 0.022f));
        }
        Lines(equator, Material(new Color("496069"), true));
    }

    private bool IsLand(Vector3 point) => SampleChannel(point.X / MapScale, -point.Z / MapScale, 1) > 0.65f;

    private void BuildRivers()
    {
        _rivers = new Node3D { Name = "NaturalEarthRivers" }; AddChild(_rivers);
        _riverMaterial = Material(new Color("55777b"), true);
        string raw = Godot.FileAccess.GetFileAsString("res://Assets/map/ne_50m_rivers_lake_centerlines.geojson");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        int vertices = 0;
        using var document = JsonDocument.Parse(raw);
        // A single batched ribbon mesh follows the same CPU elevation as picking and cities.
        // This is modern physical geography, not a reconstruction of nineteenth-century river courses.
        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var geometry = feature.GetProperty("geometry");
            var coordinates = geometry.GetProperty("coordinates");
            int rank = feature.GetProperty("properties").GetProperty("scalerank").GetInt32();
            void AddRiver(JsonElement line)
            {
                Vector2? previous = null;
                foreach (var point in line.EnumerateArray())
                {
                    var current = new Vector2(point[0].GetSingle(), point[1].GetSingle());
                    if (previous is { } start && start.DistanceSquaredTo(current) < 64)
                    {
                        int steps = Math.Max(1, (int)MathF.Ceiling(start.DistanceTo(current) / .12f));
                        for (int i = 0; i < steps; i++)
                        {
                            var a = start.Lerp(current, i / (float)steps);
                            var b = start.Lerp(current, (i + 1) / (float)steps);
                            var center = a.Lerp(b, .5f);
                            if (SampleChannel(center.X, center.Y, 1) < .65f) continue;
                            var va = Geo(a.X, a.Y, TerrainHeight(a.X, a.Y) + .006f);
                            var vb = Geo(b.X, b.Y, TerrainHeight(b.X, b.Y) + .006f);
                            var tangent = new Vector3(vb.X - va.X, 0, vb.Z - va.Z);
                            if (tangent.LengthSquared() < .000000001f) continue;
                            var side = new Vector3(-tangent.Z, 0, tangent.X).Normalized() * (rank < 3 ? .0026f : .00155f);
                            foreach (var vertex in new[] { va - side, vb + side, vb - side, va - side, va + side, vb + side })
                            {
                                surface.AddVertex(vertex); vertices++;
                            }
                        }
                    }
                    previous = current;
                }
            }
            if (geometry.GetProperty("type").GetString() == "LineString") AddRiver(coordinates);
            else foreach (var line in coordinates.EnumerateArray()) AddRiver(line);
        }
        if (vertices == 0) return;
        _rivers.AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = _riverMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private void BuildRelief()
    {
        _terrain = new Node3D(); AddChild(_terrain);
        var locations = new List<(Vector3 P, float S, Color C)>();
        var random = new Random(619);
        void Forest(float longitude, float latitude, float spanX, float spanY, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float lon = longitude + ((float)random.NextDouble() - 0.5f) * spanX;
                float lat = latitude + ((float)random.NextDouble() - 0.5f) * spanY;
                if (SampleChannel(lon, lat, 1) < 0.9f) continue;
                float h = TerrainHeight(lon, lat);
                if (h > 0.53f || h < 0.055f) continue;
                float density = MathF.Sin(lon * 2.17f) * MathF.Cos(lat * 2.31f);
                if (density < -0.25f) continue;
                float size = 0.7f + (float)random.NextDouble() * 0.85f;
                float shade = 0.83f + (float)random.NextDouble() * 0.28f;
                locations.Add((Geo(lon, lat, h + 0.015f * size), size, new Color(0.21f * shade, 0.29f * shade, 0.20f * shade)));
            }
        }
        Forest(13, 50, 26, 12, 2600); Forest(16, 62, 14, 11, 2500);
        Forest(-4, 55, 8, 8, 900); Forest(52, 57, 48, 12, 3000);
        Forest(-99, 54, 42, 15, 2500); Forest(-70, -8, 34, 20, 1500);
        Forest(135, 37, 16, 13, 1500); Forest(27, 0, 20, 14, 900);
        Forest(109, 9, 28, 21, 900);
        var crown = new SphereMesh { Radius = 0.0085f, Height = 0.032f, RadialSegments = 7, Rings = 4 };
        var material = Material(Colors.White); material.VertexColorUseAsAlbedo = true;
        crown.Material = material;
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = crown, InstanceCount = locations.Count };
        for (int i = 0; i < locations.Count; i++)
        {
            var tree = locations[i];
            multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(tree.S, tree.S, tree.S)), tree.P));
            multimesh.SetInstanceColor(i, tree.C);
        }
        _forest = new MultiMeshInstance3D { Multimesh = multimesh }; _terrain.AddChild(_forest);
    }

    private void BuildGeographicLabels()
    {
        void Place(string name, float lon, float lat, int size, Color color)
        {
            var label = MapLabel(name, Geo(lon, lat, TerrainHeight(lon, lat) + 0.035f), size, color, 0.0048f);
            _regionalLabels.Add(label);
        }
        var oceanInk = new Color("77745e");
        Place("北 大 西 洋", -30, 35, 48, oceanInk);
        Place("地 中 海", 19, 34, 42, oceanInk);
        Place("印 度 洋", 74, -17, 48, oceanInk);
        Place("北 太 平 洋", 157, 24, 48, oceanInk);
        Place("南 中 国 海", 115, 14, 42, oceanInk);
        Place("东 海", 127, 27, 40, oceanInk);
        Place("黄 海", 123, 36, 40, oceanInk);
        Place("日 本 海", 135, 42, 40, oceanInk);
    }

    private Label3D MapLabel(string text, Vector3 position, int size, Color color, float pixelSize = 0.010f)
    {
        var label = new Label3D
        {
            Text = Localization.Tr(text),
            Position = position,
            FontSize = size,
            Font = _mapSerif,
            PixelSize = pixelSize,
            Modulate = color,
            OutlineModulate = new Color("e3d5ac"),
            OutlineSize = 1,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            RenderPriority = 2,
            OutlineRenderPriority = 1
        };
        AddChild(label);
        return label;
    }

    private void AddCountry(string id, string title, string capitalName, float longitude, float latitude, Color color, Vector3 labelOffset)
    {
        var p = Geo(longitude, latitude, TerrainHeight(longitude, latitude) + 0.008f);
        var root = new Node3D { Position = p };
        AddChild(root);
        var city = new Node3D { Scale = Vector3.One * .52f };
        root.AddChild(city);
        var marker = new Node3D(); root.AddChild(marker);
        var ring = Circle(0.25f, color, 96);
        root.AddChild(ring);
        var pin = new MeshInstance3D
        {
            Position = new Vector3(0, 0.035f, 0),
            Mesh = new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.031f, Height = 0.07f, RadialSegments = 8 },
            MaterialOverride = Material(_cream)
        };
        marker.AddChild(pin);
        marker.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, 0.093f, 0),
            Mesh = new SphereMesh { Radius = 0.031f, Height = 0.062f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = Material(color, true)
        });
        var name = MapLabel(title, p + labelOffset + new Vector3(0, 0.14f, 0), 56, new Color("3b3026"), 0.0034f);
        if (id == "QNG") name.Position = Geo(111, 35, TerrainHeight(111, 35) + .25f);
        var capitalLabel = MapLabel(capitalName, p + new Vector3(0.02f, 0.06f, 0.13f), 16, _cream, 0.0048f);
        var train = new Node3D { Visible = false };
        root.AddChild(train);
        Box(train, new Vector3(0, 0.09f, 0), new Vector3(0.13f, 0.09f, 0.07f), new Color("bda571"));
        Box(train, new Vector3(-0.14f, 0.07f, 0), new Vector3(0.10f, 0.07f, 0.065f), new Color("77694d"));
        Box(train, new Vector3(-0.27f, 0.07f, 0), new Vector3(0.10f, 0.07f, 0.065f), new Color("77694d"));
        _countries[id] = new CountryVisual { Id = id, Capital = p, Root = root, City = city, Marker = marker, Ring = ring, Name = name, CapitalLabel = capitalLabel, Train = train };
    }

    public void FocusCountry(string id)
    {
        if (!_countries.TryGetValue(id, out var country)) return;
        _selected = id;
        _historicalSelection = HistoricalWorldAtlas.Countries.FirstOrDefault(c => c.ScenarioCountryId == id && c.SourceName != "Hong Kong")?.Id;
        _focus = country.Capital + new Vector3(id == "JAP" ? -0.75f : 0.6f, 0, 0.65f);
        if (id == "QNG") _focus = Geo(113, 33);
        if (IsVisibleInTree()) _camera.Current = true;
        _zoom = id == "QNG" ? 3.7f : id == "JAP" ? 2.65f : 2.85f;
        UpdateSelection();
    }

    public void SetDevelopment(string id, int industryLevels, int railLevel)
    {
        if (!_countries.TryGetValue(id, out var country)) return;
        int industry = Mathf.Clamp(industryLevels, 0, 14);
        int rail = Mathf.Clamp(railLevel, 0, 5);
        if (industry == country.Industry && rail == country.Rail) return;
        country.Industry = industry; country.Rail = rail;
        foreach (var child in country.City.GetChildren()) child.QueueFree();
        var random = new Random(id == "GBR" ? 1836 : id == "PRU" ? 1840 : 1868);
        var roads = Material(new Color("b2a285"), true);
        var roadLines = new List<Vector3>();
        for (int row = 0; row < 6; row++)
        {
            roadLines.Add(new Vector3(-0.27f, 0.004f, -0.10f + row * 0.065f));
            roadLines.Add(new Vector3(0.23f, 0.004f, -0.10f + row * 0.065f));
        }
        for (int col = 0; col < 7; col++)
        {
            roadLines.Add(new Vector3(-0.24f + col * 0.07f, 0.004f, -0.12f));
            roadLines.Add(new Vector3(-0.24f + col * 0.07f, 0.004f, 0.26f));
        }
        country.City.AddChild(CreateLines(roadLines, roads));
        int buildings = 24 + industry * 4;
        for (int i = 0; i < buildings; i++)
        {
            float x = (i % 9 - 4) * 0.048f + ((float)random.NextDouble() - 0.5f) * 0.009f;
            float z = -0.105f + i / 9 * 0.048f;
            float h = 0.035f + (float)random.NextDouble() * 0.065f;
            float width = 0.027f + (float)random.NextDouble() * 0.012f;
            var wall = i % 4 == 0 ? new Color("a89572") : i % 4 == 1 ? new Color("b6aa8e") : new Color("b9ae97");
            Box(country.City, new Vector3(x, h / 2, z), new Vector3(width, h, 0.035f), wall);
            GableRoof(country.City, new Vector3(x, h, z), width + 0.004f, 0.04f, 0.016f, id == "JAP" ? new Color("4d5653") : new Color("63594e"));
            if (i % 4 == 0)
                Box(country.City, new Vector3(x + 0.007f, h + 0.026f, z), new Vector3(0.006f, 0.03f, 0.006f), new Color("8a7563"));
        }
        // A civic landmark makes each dense city readable without a giant pin.
        Box(country.City, new Vector3(-0.045f, 0.07f, -0.14f), new Vector3(0.10f, 0.14f, 0.045f), new Color("c2b59a"));
        GableRoof(country.City, new Vector3(-0.045f, 0.14f, -0.14f), 0.11f, 0.05f, 0.022f, new Color("4c5750"));
        for (int i = 0; i < 2; i++)
            Box(country.City, new Vector3(-0.09f + i * 0.09f, 0.10f, -0.14f), new Vector3(0.025f, 0.20f, 0.035f), new Color("bbae90"));
        for (int i = 0; i < Math.Min(6, industry); i++)
        {
            float x = -0.21f + i * 0.075f;
            Box(country.City, new Vector3(x, 0.035f, 0.28f), new Vector3(0.065f, 0.07f, 0.09f), new Color("8a6c55"));
            GableRoof(country.City, new Vector3(x, 0.07f, 0.28f), 0.071f, 0.097f, 0.026f, new Color("50595b"));
            var chimney = new MeshInstance3D { Position = new Vector3(x + 0.022f, 0.092f, 0.25f), Mesh = new CylinderMesh { TopRadius = 0.007f, BottomRadius = 0.011f, Height = 0.18f, RadialSegments = 8 }, MaterialOverride = Material(new Color("83664d")) };
            country.City.AddChild(chimney);
        }
        country.Train.Visible = rail > 0;
        country.Train.Scale = Vector3.One * 0.16f;
        if (rail > 0)
        {
            var track = new List<Vector3>();
            track.Add(new Vector3(-0.31f, 0.008f, 0.34f)); track.Add(new Vector3(0.31f, 0.008f, 0.34f));
            track.Add(new Vector3(-0.31f, 0.008f, 0.36f)); track.Add(new Vector3(0.31f, 0.008f, 0.36f));
            country.City.AddChild(CreateLines(track, Material(new Color("4b463c"), true)));
            for (int i = 0; i < 23; i++)
                Box(country.City, new Vector3(-0.3f + i * 0.027f, 0.004f, 0.35f), new Vector3(0.009f, 0.007f, 0.034f), new Color("8a7b5d"));
        }
        UpdateSelection();
    }

    private void GableRoof(Node3D parent, Vector3 position, float width, float depth, float height, Color color)
    {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        var a = new Vector3(-width/2,0,-depth/2); var b = new Vector3(width/2,0,-depth/2);
        var c = new Vector3(-width/2,0,depth/2); var d = new Vector3(width/2,0,depth/2);
        var e = new Vector3(0,height,-depth/2); var f = new Vector3(0,height,depth/2);
        foreach(var v in new[] {a,e,c,c,e,f,e,b,f,f,b,d,a,b,e,c,f,d}) surface.AddVertex(v);
        surface.GenerateNormals();
        parent.AddChild(new MeshInstance3D { Mesh = surface.Commit(), Position = position, MaterialOverride = Material(color) });
    }

    public void SetMapMode(string mode)
    {
        _mode = mode is "Economy" or "Population" or "Terrain" or "Provinces" ? mode : "Political";
        _oceanMaterial.SetShaderParameter("paper_amount", _mode is "Political" or "Provinces" ? 1f : 0f);
        _terrainMaterial.SetShaderParameter("province_mode", _mode == "Provinces" ? 1f : 0f);
        _forest.Visible = _currentZoom < 3.1f;
        _terrainMaterial.SetShaderParameter("map_mode", _mode == "Economy" ? 1.0f : _mode == "Terrain" ? 2.0f : _mode == "Population" ? 3.0f : 0.0f);
        foreach (var label in _regionalLabels) label.Visible = _mode != "Terrain";
        UpdateEconomicMarkers();
        UpdateSelection();
    }

    /// <summary>Only simulated data is used; reference atlas regions receive no invented statistics.</summary>
    public void UpdateEconomy(IEnumerable<CountryState> countries)
    {
        _cityEconomy.Clear(); _countryEconomy.Clear();
        foreach (var country in countries)
        {
            _countryEconomy[country.Id] = (country.Population, country.Gdp);
            foreach (var city in country.Cities) _cityEconomy[city.Id] = (city.Population, city.Gdp);
        }
        UpdateEconomicMarkers(); UpdateSelection();
    }

    private void UpdateEconomicMarkers()
    {
        foreach (var marker in _cityMarkers)
        {
            var definition = _cityDefinitions[marker.Key];
            float scale = 1;
            string label = Localization.Tr(definition.Name);
            if (_cityEconomy.TryGetValue(marker.Key, out var data))
            {
                if (_mode == "Population")
                {
                    scale = Mathf.Clamp(MathF.Sqrt(data.Population / 100000f), .7f, 8);
                    label += " · " + Localization.Number(data.Population) + "人";
                }
                else if (_mode == "Economy")
                {
                    scale = Mathf.Clamp(MathF.Sqrt((float)data.Gdp / 100000f), .7f, 8);
                    label += " · 产值 £" + Localization.Number(data.Gdp);
                }
            }
            marker.Value.Pin.Scale = new Vector3(scale, MathF.Sqrt(scale), scale);
            marker.Value.Label.Text = label;
        }
        _nextLabelUpdate = 0;
    }

    private void UpdateSelection()
    {
        _terrainMaterial.SetShaderParameter("selected_province", QingProvinceCatalog.Provinces.ToList().FindIndex(p => p.Id == _selectedProvince) + 1);
        _terrainMaterial.SetShaderParameter("selected_territory", _historicalSelection == null ? 0 : HistoricalWorldAtlas.Country(_historicalSelection)?.ColorIndex ?? 0);
        if (_countries.TryGetValue(_selected, out var focusCountry))
            _terrainMaterial.SetShaderParameter("selected_capital", new Vector2((focusCountry.Capital.X / MapScale + 180) / 360, (90 + focusCountry.Capital.Z / MapScale) / 180));
        foreach (var pair in _countries)
        {
            var country = pair.Value;
            bool selected = pair.Key == _selected;
            country.Ring.MaterialOverride = selected ? _selectedRingMaterial : _otherRingMaterial;
            float scale = selected ? 1.15f : 0.8f;
            if (_countryEconomy.TryGetValue(country.Id, out var data))
            {
                if (_mode == "Economy") scale = Mathf.Clamp(MathF.Sqrt((float)data.Gdp / 100000000f), .6f, 2.3f);
                if (_mode == "Population") scale = Mathf.Clamp(MathF.Sqrt(data.Population / 30000000f), .6f, 2.3f);
            }
            country.Ring.Scale = new Vector3(scale, 1, scale);
            country.Name.Modulate = _mode is "Political" or "Provinces" ? new Color("3d3025") : _cream;
            country.Name.OutlineModulate = _mode is "Political" or "Provinces" ? new Color("e7d9b3") : new Color("192e2c");
            string countryName = Localization.Tr(_countryDefinitions[country.Id].Name);
            if (_countryEconomy.TryGetValue(country.Id, out var values))
            {
                if (_mode == "Economy") countryName += " · 产值 £" + Localization.Number(values.Gdp);
                if (_mode == "Population") countryName += " · " + Localization.Number(values.Population) + "人";
            }
            country.Name.Text = countryName;
        }
        _nextLabelUpdate = 0;
    }

    private void BuildHistoricalLabels()
    {
        foreach (var country in HistoricalWorldAtlas.Countries)
        {
            if (country.ScenarioCountryId != null || country.Id == "H1815_UNASSIGNED" || country.Area < .3 || country.Name.StartsWith("历史地区（")) continue;
            var label = MapLabel(country.Name, Geo(country.Longitude, country.Latitude, TerrainHeight(country.Longitude, country.Latitude) + .09f), 48, new Color("423a2e"), .0030f);
            label.Visible = false; _historicalLabels[country.Id] = label;
            _historicalLabelOrder.Add((country.Id, country.Area));
        }
        _historicalLabelOrder.Sort((a, b) => b.Area.CompareTo(a.Area));
    }

    public void FocusHistoricalCountry(string id)
    {
        var country = HistoricalWorldAtlas.Country(id); if (country == null) return;
        _historicalSelection = id; _selected = country.ScenarioCountryId ?? "";
        _focus = Geo(country.Longitude, country.Latitude);
        _zoom = Mathf.Clamp(MathF.Sqrt((float)country.Area) * .18f, 1.9f, 7.5f);
        if (country.Id == "H1815_UNASSIGNED") _zoom = 8;
        if (IsVisibleInTree()) _camera.Current = true;
        UpdateSelection();
    }

    private void BuildMaritimeRoutes()
    {
        // Period-plausible shipping corridors. No Suez or Panama shortcuts in 1836.
        AddRoute(new[] { Geo(-5, 50), Geo(-15, 42), Geo(-29, 34), Geo(-47, 33), Geo(-65, 37), Geo(-73, 40) }, 0.1f, 0.036f);
        AddRoute(new[] { Geo(-5, 50), Geo(-16, 36), Geo(-22, 13), Geo(-5, -6), Geo(9, -26), Geo(20, -37), Geo(49, -26), Geo(73, -8), Geo(79, 6) }, 0.5f, 0.019f);
        AddRoute(new[] { Geo(130, 32), Geo(126, 29), Geo(127, 23), Geo(135, 23), Geo(141, 30), Geo(141, 35) }, 0.3f, 0.036f);
    }

    private void AddRoute(Vector3[] route, float phase, float speed)
    {
        var curve = new List<Vector3>();
        for (int i = 0; i < route.Length - 1; i++)
        {
            var a = route[Math.Max(0, i - 1)]; var b = route[i];
            var c = route[i + 1]; var d = route[Math.Min(route.Length - 1, i + 2)];
            for (int j = 0; j < 12; j++)
            {
                float t = j / 12f; float t2 = t * t; float t3 = t2 * t;
                curve.Add(0.5f * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3));
            }
        }
        curve.Add(route[^1]); route = curve.ToArray();
        var dashes = new List<Vector3>();
        for (int segment = 0; segment < route.Length - 1; segment++)
        {
            float distance = route[segment].DistanceTo(route[segment + 1]);
            int count = Math.Max(1, (int)(distance / 0.075f));
            for (int i = 0; i < count; i += 2)
            {
                var a = route[segment].Lerp(route[segment + 1], i / (float)count);
                var b = route[segment].Lerp(route[segment + 1], Math.Min(i + 1, count) / (float)count);
                a.Y = b.Y = 0.035f;
                dashes.Add(a); dashes.Add(b);
            }
        }
        Lines(dashes, Material(new Color("4d676b"), true));
        var ship = new Node3D { Scale = new Vector3(0.55f, 0.55f, 0.55f) };
        AddChild(ship);
        Box(ship, new Vector3(0, 0.043f, 0), new Vector3(0.10f, 0.07f, 0.23f), new Color("d1bd8e"));
        Box(ship, new Vector3(0, 0.17f, -0.005f), new Vector3(0.017f, 0.25f, 0.017f), new Color("b7a57c"));
        Box(ship, new Vector3(0.01f, 0.20f, -0.025f), new Vector3(0.15f, 0.13f, 0.017f), new Color("e1d6b1"));
        _ships.Add(new Ship { Node = ship, Route = route, Phase = phase, Speed = speed });
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        if (!Input.IsMouseButtonPressed(MouseButton.Right)) _dragging = false;
        _time += delta;
        float dt = (float)delta;
        float moveSpeed = _currentZoom * 0.48f * dt;
        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up)) _focus.Z -= moveSpeed;
        if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down)) _focus.Z += moveSpeed;
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left)) _focus.X -= moveSpeed;
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right)) _focus.X += moveSpeed;
        _focus.X = Mathf.Clamp(_focus.X, -23, 23);
        _focus.Z = Mathf.Clamp(_focus.Z, -11, 9);
        float blend = 1 - Mathf.Exp(-7 * dt);
        _currentFocus = _currentFocus.Lerp(_focus, blend);
        _currentZoom = Mathf.Lerp(_currentZoom, _zoom, blend);
        UpdateCamera();
        foreach (var ship in _ships)
        {
            float progress = (float)(_time * ship.Speed + ship.Phase) % 1;
            float segmentT = progress * (ship.Route.Length - 1);
            int segment = Math.Min((int)segmentT, ship.Route.Length - 2);
            var start = ship.Route[segment];
            var end = ship.Route[segment + 1];
            ship.Node.Position = start.Lerp(end, segmentT - segment);
            ship.Node.Position = new Vector3(ship.Node.Position.X, 0.025f, ship.Node.Position.Z);
            ship.Node.Rotation = new Vector3(0, Mathf.Atan2(end.X - start.X, end.Z - start.Z), 0);
        }
        foreach (var country in _countries.Values)
        {
            if (!country.Train.Visible) continue;
            country.Train.Position = new Vector3(-0.23f + (float)(_time * 0.14 % 1) * 0.46f, 0, 0.35f);
        }
    }

    private void UpdateCamera()
    {
        UpdateProvinces();
        _camera.Size = _currentZoom;
        _camera.Position = _currentFocus + new Vector3(0, _currentZoom * 1.47f, _currentZoom * 1.13f);
        _camera.LookAt(_currentFocus, Vector3.Up);
        if (_terrainMaterial != null) _terrainMaterial.SetShaderParameter("map_zoom", _currentZoom);
        if (_oceanMaterial != null) _oceanMaterial.SetShaderParameter("map_zoom", _currentZoom);
        // Declutter labels at 12.5 Hz rather than allocating/querying every rendered frame.
        if (_time < _nextLabelUpdate) return;
        _nextLabelUpdate = _time + .08;
        _labelBounds.Clear();
        var viewport = GetViewport().GetVisibleRect().Size;
        if (_forest != null) _forest.Visible = _currentZoom < 3.1f;
        if (_rivers != null)
        {
            _rivers.Visible = _currentZoom < 7.5f;
            _riverMaterial.AlbedoColor = _mode is "Political" or "Provinces" ? new Color("66818a") : new Color("668e9c");
        }
        // Reserve the highest level first: selected country/province, other countries, then settlements.
        // A measured rectangle includes the entire label, including long economic statistics.
        UpdateProvinceLabels(viewport);
        if (_countries.TryGetValue(_selected, out var selected))
            PlaceMapLabel(selected.Name, _mode is "Economy" or "Population" ? 25 : 38, _mode is not ("Terrain" or "Provinces") && _currentZoom > 3.15f, viewport);
        foreach (var country in _countries.Values)
        {
            country.City.Visible = _currentZoom < 2.6f;
            country.Marker.Visible = _currentZoom < 6.5f && _mode != "Provinces";
            country.Ring.Visible = _mode != "Provinces" && country.Id == _selected && _currentZoom < 3.2f;
            if (country.Id != _selected)
                PlaceMapLabel(country.Name, _mode is "Economy" or "Population" ? 21 : 29, _mode is not ("Terrain" or "Provinces") && _currentZoom > 3.8f, viewport);
            country.CapitalLabel.Visible = false; // Capitals already have one settlement label.
        }
        foreach (var entry in _historicalLabelOrder)
        {
            var label = _historicalLabels[entry.Id];
            bool largeEnough = entry.Area > _currentZoom * _currentZoom * .30;
            label.Modulate = new Color("494133"); label.OutlineModulate = new Color("e5d7b3");
            PlaceMapLabel(label, entry.Area > 120 ? 27 : entry.Area > 12 ? 21 : 17, _mode == "Political" && largeEnough, viewport);
        }
        foreach (var item in _cityMarkers)
        {
            var definition = _cityDefinitions[item.Key];
            bool economic = _mode is "Economy" or "Population";
            bool show = _currentZoom < 4.3f && (definition.CountryId == _selected || economic || _currentZoom < 2.5f);
            if (_mode == "Provinces") show &= _currentZoom < 3.0f;
            var screen = _camera.UnprojectPosition(item.Value.Position);
            show &= !_camera.IsPositionBehind(item.Value.Position) && screen.X > 0 && screen.X < viewport.X && screen.Y > 0 && screen.Y < viewport.Y;
            item.Value.Pin.Visible = show;
            item.Value.Label.Modulate = _mode is "Political" or "Provinces" ? new Color("3d372b") : _cream;
            item.Value.Label.OutlineModulate = _mode is "Political" or "Provinces" ? new Color("ecdfba") : new Color("223a32");
            PlaceMapLabel(item.Value.Label, economic ? 17 : 18, show && _mode != "Terrain", viewport);
        }
        foreach (var label in _regionalLabels)
            PlaceMapLabel(label, _currentZoom > 6 ? 22 : 18, _mode is "Political" or "Provinces" && _currentZoom > 2.6f, viewport);
    }

    private bool PlaceMapLabel(Label3D label, float pixels, bool visible, Vector2 viewport)
    {
        label.Visible = false;
        if (!visible || _camera.IsPositionBehind(label.GlobalPosition) || viewport.Y <= 0) return false;
        var screen = _camera.UnprojectPosition(label.GlobalPosition);
        // Avoid shaping text and touching glyph atlases for the rest of the world off screen.
        if (screen.X < -400 || screen.X > viewport.X + 400 || screen.Y < 40 || screen.Y > viewport.Y - 35) return false;
        float depth = (label.GlobalPosition - _camera.GlobalPosition).Dot(-_camera.GlobalTransform.Basis.Z);
        label.PixelSize = pixels * 2 * depth * Mathf.Tan(Mathf.DegToRad(_camera.Fov * .5f)) / (viewport.Y * label.FontSize);
        var measured = label.Font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, label.FontSize);
        float width = measured.X * pixels / label.FontSize;
        var bounds = new Rect2(screen - new Vector2(width * .5f + 7, pixels * .62f + 5), new Vector2(width + 14, pixels * 1.24f + 10));
        if (bounds.End.X < 15 || bounds.Position.X > viewport.X - 15 || bounds.End.Y < 80 || bounds.Position.Y > viewport.Y - 75) return false;
        foreach (var other in _labelBounds) if (bounds.Intersects(other)) return false;
        label.Visible = true; _labelBounds.Add(bounds); return true;
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!IsVisibleInTree()) return;
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed) _zoom = Mathf.Max(1.5f, _zoom * 0.89f);
            if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed) _zoom = Mathf.Min(32, _zoom * 1.12f);
            if (mouse.ButtonIndex == MouseButton.Right) _dragging = mouse.Pressed;
            if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed) { if (!PickProvince(mouse.Position) && !PickCity(mouse.Position)) PickCountry(mouse.Position); }
        }
        if (input is InputEventMouseMotion motion && _dragging)
        {
            float scale = _currentZoom / GetViewport().GetVisibleRect().Size.Y;
            _focus += new Vector3(-motion.Relative.X * scale, 0, -motion.Relative.Y * scale * 1.25f);
        }
    }


    private void AddCityMarker(CityDefinition city)
    {
        var pos = Geo((float)city.Longitude, (float)city.Latitude, TerrainHeight((float)city.Longitude, (float)city.Latitude) + .028f);
        var pin = new Node3D { Position = pos }; AddChild(pin);
        pin.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .016f, BottomRadius = .016f, Height = .007f, RadialSegments = 24 }, MaterialOverride = Material(new Color("534833"), true) });
        pin.AddChild(new MeshInstance3D { Position = new Vector3(0, .0045f, 0), Mesh = new CylinderMesh { TopRadius = .010f, BottomRadius = .010f, Height = .003f, RadialSegments = 24 }, MaterialOverride = Material(new Color("ead5a2"), true) });
        var label = MapLabel(city.Name, pos + new Vector3(.055f, .022f, .04f), 40, _cream, .0018f);
        label.Font = _mapSans;
        _cityMarkers[city.Id] = (pos, label, pin);
    }
    public void FocusCity(string cityId)
    {
        if (!_cityMarkers.TryGetValue(cityId, out var city)) return;
        _selected = _cityDefinitions[cityId].CountryId;
        _historicalSelection = HistoricalWorldAtlas.Countries.FirstOrDefault(c => c.ScenarioCountryId == _selected && c.SourceName != "Hong Kong")?.Id;
        _focus = city.Position; _zoom = 1.55f; if (IsVisibleInTree()) _camera.Current = true; UpdateSelection();
    }
    public void FocusRegion(string regionId)
    {
        var province = QingProvinceCatalog.Provinces.FirstOrDefault(p => p.Id == regionId);
        if (province != null) {
            _selected = "QNG"; _selectedProvince = regionId;
            _historicalSelection = HistoricalWorldAtlas.Countries.FirstOrDefault(c => c.ScenarioCountryId == "QNG")?.Id;
            _focus = Geo((float)province.Longitude, (float)province.Latitude); _zoom = 2.5f;
            if (IsVisibleInTree()) _camera.Current = true; UpdateSelection(); UpdateProvinces(); return;
        }
        var cities = GeographyCatalog.Cities.Where(c => c.RegionId == regionId).ToArray(); if (cities.Length == 0) return;
        _selected = cities[0].CountryId;
        _historicalSelection = HistoricalWorldAtlas.Countries.FirstOrDefault(c => c.ScenarioCountryId == _selected && c.SourceName != "Hong Kong")?.Id;
        _focus = Geo((float)cities.Average(c => c.Longitude), (float)cities.Average(c => c.Latitude)); _zoom = 2.2f;
        if (IsVisibleInTree()) _camera.Current = true; UpdateSelection();
    }
    private bool PickCity(Vector2 screen)
    {
        string? closest = null; float distance = 25 * 25;
        foreach (var city in _cityMarkers)
        {
            if (!city.Value.Pin.Visible || _camera.IsPositionBehind(city.Value.Position)) continue;
            float d = _camera.UnprojectPosition(city.Value.Position).DistanceSquaredTo(screen);
            if (d < distance) { distance = d; closest = city.Key; }
        }
        if (closest == null) return false;
        FocusCity(closest); CitySelected?.Invoke(closest); return true;
    }

    private void PickCountry(Vector2 screen)
    {
        // Explicit capital icons remain usable when a scenario differs from the 1815 atlas
        // (notably independent Belgium). They have a small visible hit target, not a Voronoi territory.
        foreach (var country in _countries.Values)
        {
            if (!country.Marker.Visible || _camera.IsPositionBehind(country.Capital)) continue;
            float d = _camera.UnprojectPosition(country.Capital + new Vector3(0, 0.12f, 0)).DistanceSquaredTo(screen);
            if (d > 12 * 12) continue;
            FocusCountry(country.Id); CountrySelected?.Invoke(country.Id); return;
        }
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);
        if (direction.Y >= -.001f) return;
        var ground = origin + direction * ((.13f - origin.Y) / direction.Y);
        for (int i = 0; i < 5; i++)
        {
            float elevation = TerrainHeight(ground.X / MapScale, -ground.Z / MapScale);
            ground = origin + direction * ((elevation - origin.Y) / direction.Y);
        }
        float longitude = ground.X / MapScale, latitude = -ground.Z / MapScale;
        if (longitude < -180 || longitude > 180 || latitude < -90 || latitude > 90 || !IsLand(ground)) return;
        var territory = HistoricalWorldAtlas.At(longitude, latitude);
        if (territory == null) return;
        _historicalSelection = territory.Id;
        if (territory.ScenarioCountryId is { } simulated)
        {
            FocusCountry(simulated); CountrySelected?.Invoke(simulated);
        }
        else
        {
            _selected = ""; UpdateSelection();
            HistoricalCountrySelected?.Invoke(territory.Id);
        }
    }

    private MeshInstance3D Circle(float radius, Color color, int segments)
    {
        var points = new List<Vector3>();
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.Tau / segments;
            float b = (i + 1) * Mathf.Tau / segments;
            points.Add(new Vector3(Mathf.Cos(a) * radius, 0.015f, Mathf.Sin(a) * radius));
            points.Add(new Vector3(Mathf.Cos(b) * radius, 0.015f, Mathf.Sin(b) * radius));
        }
        return CreateLines(points, Material(color, true));
    }

    private MeshInstance3D Box(Node3D parent, Vector3 position, Vector3 size, Color color)
    {
        var mesh = new MeshInstance3D { Position = position, Mesh = new BoxMesh { Size = size }, MaterialOverride = Material(color) };
        parent.AddChild(mesh);
        return mesh;
    }

    private void Line(Vector3[] points, StandardMaterial3D material)
    {
        var lines = new List<Vector3>();
        for (int i = 0; i < points.Length - 1; i++) { lines.Add(points[i]); lines.Add(points[i + 1]); }
        Lines(lines, material);
    }

    private void Lines(List<Vector3> points, StandardMaterial3D material) => AddChild(CreateLines(points, material));

    private MeshInstance3D CreateLines(List<Vector3> points, StandardMaterial3D material)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        foreach (var point in points) mesh.SurfaceAddVertex(point);
        mesh.SurfaceEnd();
        return new MeshInstance3D { Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }
}






