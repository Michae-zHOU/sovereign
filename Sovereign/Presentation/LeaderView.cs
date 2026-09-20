using Godot;
using System;
using System.Collections.Generic;

namespace Sovereign.Presentation;

/// <summary>
/// An animated 3D leader gallery. Authored CC0-derived anatomical assets include skeleton and facial
/// morph animation; procedural studies remain a compatibility fallback for missing assets.
/// Historical identity is researched independently; visual reconstructions are not scanned likenesses.
/// </summary>
public partial class LeaderView : Node3D
{
    public bool IsOpen { get; private set; }
    // Only the explicit offline capture command sets this; PNG encoding must not speed up the recorded animation.
    public double AnimationCaptureStepSeconds { get; set; }
    public bool UsesAuthoredModel { get; private set; }
    /// <summary>Centers the same animated model for an embedded, independently rendered portrait viewport.</summary>
    public bool CompactPortrait { get; set; }
    public bool PreferRealisticPortrait { get; set; } = false;
    public bool IsPortraitMode { get; private set; }
    public bool HasRealisticPortrait => _appearance == "daoguang";
    private Control _portraitCanvas = null!;
    private TextureRect _realisticPortrait = null!;
    private Camera3D _camera = null!;
    private Node3D _portrait = null!;
    private Node3D _cabinet = null!;
    private readonly Dictionary<string, StandardMaterial3D> _materials = new();
    private string _appearance = "", _country = "";
    private int _age = -1;
    private float _yaw = -.12f, _pitch = .035f, _distance = 2.95f, _smoothDistance = 2.95f;
    private bool _orbiting;
    private double _elapsed;
    private Vector3 _focus = new(0, 1.78f, 0), _targetFocus = new(0, 1.78f, 0);
    private float _modelBottom = .83f, _modelTop = 2.5f;
    public bool HasFullFigure => _modelTop - _modelBottom > 1.9f;
    public string Framing { get; private set; } = "three_quarter";
    // Restore our additive pose before sampling idle. Models need not key every bone on every track.
    private readonly Dictionary<int, Quaternion> _gestureBasePose = new();
    private readonly List<MeshInstance3D> _eyelids = new();
    private AnimationPlayer? _authoredAnimation;
    private Skeleton3D? _skeleton;
    private MeshInstance3D? _animatedFace;
    private Node3D? _fallbackHead;
    private int _headBone = -1, _speechShape = -1, _leftForearm = -1, _rightForearm = -1;
    private string _gesture = "";
    private double _gestureTime, _gestureDuration;
    public bool HasSkeletalAnimation => UsesAuthoredModel && _skeleton != null;
    public bool HasFacialAnimation => _animatedFace != null && _speechShape >= 0;

    /// <summary>Dialogue feedback animates real head/neck geometry and facial morphs, not a portrait card.</summary>
    public void TriggerGesture(string gesture)
    {
        _gesture = gesture is "greeting" or "talk" or "agree" or "refuse" ? gesture : "talk";
        _gestureTime = 0;
        _gestureDuration = _gesture == "talk" ? 3.6 : _gesture == "greeting" ? 2.4 : 1.8;
    }

    private void UpdateGesture(double delta)
    {
        if (_skeleton != null)
            foreach (var pose in _gestureBasePose) _skeleton.SetBonePoseRotation(pose.Key, pose.Value);
        _gestureBasePose.Clear();
        _authoredAnimation?.Advance(delta);
        _gestureTime += delta;
        float phase = (float)Math.Clamp(_gestureTime / Math.Max(.01, _gestureDuration), 0, 1);
        float envelope = Mathf.Sin(phase * Mathf.Pi);
        float nod = 0, turn = 0, speech = 0;
        if (phase < 1)
        {
            nod = _gesture switch
            {
                "greeting" => -.095f * envelope,
                "agree" => Mathf.Sin(phase * Mathf.Tau * 2) * envelope * .065f,
                "talk" => Mathf.Sin((float)_gestureTime * 2.7f) * envelope * .018f,
                _ => 0
            };
            if (_gesture == "refuse") turn = Mathf.Sin(phase * Mathf.Tau * 1.7f) * envelope * .11f;
            if (_gesture == "talk") speech = (.15f + .65f * Math.Abs(Mathf.Sin((float)_gestureTime * 9.3f))) * envelope;
        }
        if (_skeleton != null && _headBone >= 0)
        {
            var idle = _skeleton.GetBonePoseRotation(_headBone);
            _gestureBasePose[_headBone] = idle;
            _skeleton.SetBonePoseRotation(_headBone, idle * Quaternion.FromEuler(new Vector3(nod, turn, 0)));
        }
        if (_skeleton != null)
        {
            float hand = phase < 1 && _gesture is "talk" or "greeting" ? envelope * .085f : 0;
            ApplyArmGesture(_leftForearm, hand);
            ApplyArmGesture(_rightForearm, hand * .6f);
        }
        else if (_fallbackHead != null)
            _fallbackHead.Rotation = new Vector3(nod, turn + Mathf.Sin((float)_elapsed * .43f) * .018f, 0);
        if (_animatedFace != null && _speechShape >= 0) _animatedFace.SetBlendShapeValue(_speechShape, speech);
    }

    private void ApplyArmGesture(int bone, float angle)
    {
        if (_skeleton == null || bone < 0) return;
        var idle = _skeleton.GetBonePoseRotation(bone);
        _gestureBasePose[bone] = idle;
        _skeleton.SetBonePoseRotation(bone, idle * Quaternion.FromEuler(new Vector3(angle, 0, 0)));
    }

    /// <summary>Frame the authored silhouette without stretching the model or hiding its lower body.</summary>
    public void SetFraming(string framing, bool immediate = false)
    {
        Framing = framing is "full" or "face" ? framing : "three_quarter";
        float height = _modelTop - _modelBottom;
        float bottom = Framing switch
        {
            "full" => _modelBottom - .04f,
            "face" => _modelTop - Math.Min(.78f, height * .54f),
            _ => HasFullFigure ? _modelBottom + height * .28f : _modelBottom
        };
        float top = _modelTop + .045f;
        _targetFocus = new Vector3(0, (bottom + top) * .5f, .025f);
        _distance = (top - bottom) / (2 * Mathf.Tan(Mathf.DegToRad(_camera.Fov * .5f))) * (CompactPortrait ? 1.06f : 1.18f);
        if (immediate) { _focus = _targetFocus; _smoothDistance = _distance; }
    }

    public void ResetView()
    {
        _yaw = -.10f; _pitch = 0;
        SetFraming(Framing);
    }

    public void SetViewAngle(float yaw)
    {
        _yaw = Mathf.Clamp(yaw, -Mathf.Pi, Mathf.Pi); _pitch = 0;
    }

    private void MeasureAuthoredFigure(Node3D model)
    {
        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        var pending = new Stack<Node>(); pending.Push(model);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                var bounds = mesh.GetAabb();
                for (int i = 0; i < 8; i++)
                {
                    float y = _portrait.ToLocal(mesh.ToGlobal(bounds.GetEndpoint(i))).Y;
                    bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                }
            }
            foreach (Node child in node.GetChildren()) pending.Push(child);
        }
        if (float.IsFinite(bottom) && float.IsFinite(top) && top - bottom > .3f)
        { _modelBottom = bottom; _modelTop = top; }
    }

    private void BindAuthoredAnimation(Node3D model)
    {
        var pending = new Stack<Node>(); pending.Push(model);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is AnimationPlayer player) _authoredAnimation = player;
            if (node is Skeleton3D skeleton) _skeleton = skeleton;
            if (node is MeshInstance3D mesh && mesh.FindBlendShapeByName("Speech") >= 0)
            { _animatedFace = mesh; _speechShape = mesh.FindBlendShapeByName("Speech"); }
            foreach (Node child in node.GetChildren()) pending.Push(child);
        }
        _headBone = _skeleton?.FindBone("Head") ?? -1;
        _leftForearm = _skeleton?.FindBone("LeftForearm") ?? -1; _rightForearm = _skeleton?.FindBone("RightForearm") ?? -1;
        if (_authoredAnimation == null) return;
        _authoredAnimation.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        foreach (string name in _authoredAnimation.GetAnimationList())
        {
            if (!name.Contains("idle", StringComparison.OrdinalIgnoreCase)) continue;
            var idle = _authoredAnimation.GetAnimation(name); idle.LoopMode = Animation.LoopModeEnum.Linear;
            _authoredAnimation.Play(name); _authoredAnimation.Advance(0); break;
        }
    }

    private sealed record Portrait(string Wardrobe, string Hair, string Beard, string Coat, string Sash,
        float FaceWidth = 1, float FaceLength = 1, float Nose = 1, float Chin = 1, float EyeGap = 1,
        bool Female = false, string Skin = "c9977d", string HairColor = "514337");

    // Color and silhouette studies, not claims about an exact surviving uniform or regalia inventory.
    private static readonly Dictionary<string, Portrait> Portraits = new()
    {
        ["william_iv"] = new("naval", "receding", "sideburns", "202a3d", "7196a8", 1.11f, 1.03f, 1.13f, 1.08f, .96f, HairColor:"bdbaaa"),
        ["victoria"] = new("gown", "center_part", "none", "e8daca", "3f7392", .99f, .95f, .90f, .94f, 1.04f, true, "d1a28b", "57412f"),
        ["frederick_william_iii"] = new("prussian", "receding", "sideburns", "24313c", "9e5344", .98f, 1.05f, 1.06f, 1.04f, .94f, HairColor:"837c6b"),
        ["frederick_william_iv"] = new("prussian", "swept", "sideburns", "24313c", "b26345", 1.16f, .99f, .94f, 1.08f, .98f, HairColor:"6f5540"),
        ["tokugawa_ienari"] = new("shogun", "shogun", "none", "344b50", "b5a777", 1.04f, 1.00f, .82f, .94f, 1.08f, Skin:"c49975", HairColor:"302d29"),
        ["tokugawa_ieyoshi"] = new("shogun", "shogun", "none", "58614c", "bbae87", .99f, 1.05f, .90f, 1.03f, 1.03f, Skin:"c69d7c", HairColor:"393630"),
        ["louis_philippe"] = new("royal", "swept", "sideburns", "243d50", "b8433c", 1.13f, 1.03f, 1.08f, 1.03f, .98f, HairColor:"726551"),
        ["ferdinand_i"] = new("austrian", "swept", "none", "e1d9c4", "95423c", .94f, 1.12f, 1.11f, .94f, .97f, HairColor:"78624a"),
        ["nicholas_i"] = new("russian", "receding", "moustache", "33483e", "5598af", .96f, 1.06f, 1.10f, 1.08f, 1.00f, HairColor:"776449"),
        ["andrew_jackson"] = new("statesman", "high_swept", "none", "282e31", "d1c6b1", .91f, 1.12f, 1.16f, 1.09f, .94f, HairColor:"c6c1af"),
        ["martin_van_buren"] = new("statesman", "receding", "large_sideburns", "343331", "ab9980", 1.07f, 1.03f, 1.04f, .99f, .97f, HairColor:"c1ad82"),
        ["william_henry_harrison"] = new("statesman", "receding", "none", "292e33", "c5baa3", .91f, 1.10f, 1.13f, 1.02f, .95f, HairColor:"bbb4a2"),
        ["john_tyler"] = new("statesman", "swept", "none", "2d3236", "d9cbae", .92f, 1.12f, 1.12f, 1.05f, .94f, HairColor:"867c67"),
        ["james_polk"] = new("statesman", "long_swept", "none", "272f35", "bdb7a7", .95f, 1.06f, 1.05f, 1.00f, .98f, HairColor:"a6a38f"),
        ["daoguang"] = new("qing", "qing", "qing", "bd9744", "5d6f69", .98f, 1.09f, .94f, .92f, 1.07f, Skin:"c89f79", HairColor:"555147"),
        ["mahmud_ii"] = new("ottoman", "fez", "full", "314654", "b3a070", 1.05f, 1.05f, 1.09f, 1.05f, .99f, Skin:"bf9171", HairColor:"433b32"),
        ["abdulmejid_i"] = new("ottoman", "fez", "short", "293e4f", "b6a574", .91f, 1.06f, 1.05f, .95f, 1.00f, Skin:"c59779", HairColor:"47382d"),
        ["isabella_ii"] = new("gown", "center_part", "none", "b36365", "6695aa", 1.10f, .91f, .80f, .92f, 1.05f, true, "d1a58e", "584130"),
        ["maria_ii"] = new("gown", "curls", "none", "e1c594", "89a6b2", 1.06f, .96f, .93f, .98f, 1.02f, true, "d1a38a", "634631"),
        ["leopold_i"] = new("royal", "swept", "sideburns", "29443e", "a05454", .94f, 1.09f, 1.09f, 1.03f, .98f, HairColor:"68533f")
    };

    // Visual age is only a modeling parameter. These defaults must never be presented as researched ages.
    private static readonly Dictionary<string, int> VisualAges = new()
    {
        ["william_iv"]=70,["victoria"]=18,["frederick_william_iii"]=65,["frederick_william_iv"]=45,
        ["tokugawa_ienari"]=62,["tokugawa_ieyoshi"]=44,["louis_philippe"]=62,["ferdinand_i"]=42,
        ["nicholas_i"]=39,["andrew_jackson"]=68,["martin_van_buren"]=54,["william_henry_harrison"]=68,
        ["john_tyler"]=51,["james_polk"]=49,["daoguang"]=53,["mahmud_ii"]=50,["abdulmejid_i"]=16,
        ["isabella_ii"]=5,["maria_ii"]=16,["leopold_i"]=45
    };

    public override void _Ready()
    {
        Materials();
        BuildCabinet();
        _camera = new Camera3D { Fov = 32, HOffset = -.46f, Near = .05f, Far = 60, Current = false };
        _camera.Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("141b1e"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("d1d8d9"), AmbientLightEnergy = .21f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            SsaoEnabled = true, SsaoIntensity = .8f, SsaoRadius = .12f, SsaoDetail = .65f,
            GlowEnabled = false,
            AdjustmentEnabled = true, AdjustmentContrast = 1.04f, AdjustmentSaturation = .92f
        };
        AddChild(_camera);
        AddPortraitLight(new Vector3(-2.8f, 3.8f, 3.7f), new Color("fff0dc"), 2.7f, 65, true);
        AddPortraitLight(new Vector3(2.2f, 2.8f, 2.8f), new Color("c7d7e8"), .95f, 70, false);
        AddPortraitLight(new Vector3(1.5f, 3.5f, -1.1f), new Color("efce9e"), 2.2f, 65, true);
        // This is explicitly a 2D illustration gallery. The independent 3D viewport remains available.
        var illustrationLayer = new CanvasLayer { Layer = 2 }; AddChild(illustrationLayer);
        _portraitCanvas = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        illustrationLayer.AddChild(_portraitCanvas); _portraitCanvas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("131b20"), MouseFilter = Control.MouseFilterEnum.Ignore };
        _portraitCanvas.AddChild(background); background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _realisticPortrait = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        _portraitCanvas.AddChild(_realisticPortrait);
        _realisticPortrait.AnchorLeft = .31f; _realisticPortrait.AnchorRight = 1; _realisticPortrait.AnchorBottom = 1;
        _realisticPortrait.OffsetTop = 87; _realisticPortrait.OffsetBottom = -54; _realisticPortrait.OffsetRight = -20;
        Visible = false;
    }

    public void ShowLeader(string appearanceKey, string countryId, int age)
    {
        if (age < 0) age = VisualAges.TryGetValue(appearanceKey, out int visualAge) ? visualAge : 45;
        bool usePortrait = appearanceKey == "daoguang" && PreferRealisticPortrait;
        bool changed = _appearance != appearanceKey || _country != countryId || _age != age || IsPortraitMode != usePortrait;
        IsPortraitMode = usePortrait;
        if (changed)
        {
            _appearance = appearanceKey; _country = countryId; _age = Math.Clamp(age, 0, 110);
            if (!usePortrait) BuildPortrait();
            else { UsesAuthoredModel = false; _realisticPortrait.Texture = GD.Load<Texture2D>("res://Assets/portraits/daoguang-realistic-v1.png"); }
            _yaw = -.10f; _pitch = 0; SetFraming("three_quarter", true);
        }
        IsOpen = true; Visible = true; _camera.Current = true;
        _portraitCanvas.Visible = usePortrait;
        if (_portrait != null) _portrait.Visible = !usePortrait;
        UpdateCamera();
    }

    public void HideLeader()
    {
        IsOpen = false; Visible = false; _orbiting = false;
        _portraitCanvas.Visible = false;
        _camera.ClearCurrent(true);
    }

    public override void _Process(double delta)
    {
        if (!IsOpen || IsPortraitMode) return;
        if (AnimationCaptureStepSeconds > 0) delta = Math.Min(AnimationCaptureStepSeconds, .1);
        _elapsed += delta;
        _smoothDistance = Mathf.Lerp(_smoothDistance, _distance, 1 - Mathf.Exp(-(float)delta * 8));
        _focus = _focus.Lerp(_targetFocus, 1 - Mathf.Exp(-(float)delta * 8));
        // A restrained breathing idle keeps the portrait from reading as a museum mannequin.
        _portrait.Rotation = new Vector3(0, Mathf.Sin((float)_elapsed * .26f) * .009f, Mathf.Sin((float)_elapsed * .39f) * .0015f);
        double blinkTime = _elapsed % 5.2;
        float blink = blinkTime > 4.92 ? (float)Math.Sin((blinkTime - 4.92) / .28 * Math.PI) : 0;
        foreach (var lid in _eyelids) { lid.Visible = blink > .6f; }
        UpdateGesture(delta);
        UpdateCamera();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!IsOpen || !Visible || IsPortraitMode) return;
        if (input is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Right) _orbiting = button.Pressed;
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp) _distance = Mathf.Max(1.2f, _distance - .20f);
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown) _distance = Mathf.Min(6.5f, _distance + .20f);
        }
        if (_orbiting && !Input.IsMouseButtonPressed(MouseButton.Right)) _orbiting = false;
        if (input is InputEventMouseMotion motion && _orbiting)
        {
            _yaw -= motion.Relative.X * .004f;
            _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * .0028f, -.15f, .48f);
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateCamera()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        _camera.HOffset = CompactPortrait ? 0 : -_smoothDistance * Mathf.Tan(Mathf.DegToRad(_camera.Fov * .5f))
            * Math.Min(550, viewportSize.X * .45f) / Math.Max(1, viewportSize.Y);
        _camera.Position = _focus + new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(_pitch), Mathf.Sin(_pitch), Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * _smoothDistance;
        _camera.LookAt(GlobalPosition + _focus, Vector3.Up);
        // Keep the studio behind the figure during a full turn; an orbit must never enter a back wall.
        _cabinet.Rotation = new Vector3(0, _yaw, 0);
    }

    private void AddPortraitLight(Vector3 position, Color color, float energy, float angle, bool shadow)
    {
        var light = new SpotLight3D { Position = position, LightColor = color, LightEnergy = energy,
            SpotRange = 10, SpotAngle = angle, SpotAttenuation = 1.05f, ShadowEnabled = shadow, ShadowBias = .02f,
            ShadowNormalBias = .035f, LightSize = .65f };
        _cabinet.AddChild(light); light.LookAt(new Vector3(0, 1.4f, 0), Vector3.Up);
    }

    private void Materials()
    {
        Material("gold", "ba9b58", .48f, .62f); Material("antique_gold", "807044", .47f, .6f);
        Material("brass_light", "cfbb7d", .28f, .62f); Material("wood", "382b27", .65f);
        Material("wood_light", "5b4634", .59f); Material("wall", "263e3b", .89f);
        Material("black", "161b1c", .75f); Material("ivory", "e5dac3", .68f);
        Material("marble", "4c5352", .4f); Material("marble_pale", "777971", .45f);
        Material("velvet", "642d30", .94f); Material("red", "9a3d38", .78f);
        Material("paper", "b4ad8c", .90f); Material("map_ink", "747d65", .90f);
        Material("eye", "c1bdb0", .30f); Material("iris", "596158", .29f); Material("pupil", "1b2527", .20f);
        Material("pearl", "e4dac2", .24f, .15f); Material("jewel", "447479", .16f, .38f);
        Fabric(_materials["velvet"], true); Fabric(_materials["wall"], false);
    }

    private void BuildCabinet()
    {
        _cabinet = new Node3D(); AddChild(_cabinet);
        Box(_cabinet, new Vector3(0, -.10f, -.2f), new Vector3(10, .18f, 10), "wood");
        Box(_cabinet, new Vector3(0, -.01f, 0), new Vector3(8, .015f, 8), "wood_light");
        Box(_cabinet, new Vector3(0, 2.4f, -2.05f), new Vector3(8, 5, .15f), "wall");
        Box(_cabinet, new Vector3(0, 3.58f, -1.8f), new Vector3(8, .10f, .20f), "gold");
        // Continuous draped cloth gives the silhouette a quiet background, with actual fold normals.
        var curtainVertices = new List<Vector3>(); var curtainNormals = new List<Vector3>();
        var curtainUvs = new List<Vector2>(); var curtainIndices = new List<int>();
        const int panels = 128;
        for (int row = 0; row < 2; row++) for (int i = 0; i <= panels; i++)
        {
            float x = -2.5f + 5f * i / panels;
            float wave = x * 15f;
            curtainVertices.Add(new Vector3(x, row == 0 ? .09f : 3.57f, -1.72f + .10f * Mathf.Sin(wave)));
            curtainNormals.Add(new Vector3(-1.5f * Mathf.Cos(wave), 0, 1).Normalized());
            curtainUvs.Add(new Vector2(i / 16f, row * 5f));
        }
        for (int i = 0; i < panels; i++)
        {
            int a = i, b = i + 1, c = i + panels + 1, d = c + 1;
            curtainIndices.AddRange(new[] { a, c, b, b, c, d });
        }
        var curtainArrays = new Godot.Collections.Array(); curtainArrays.Resize((int)Mesh.ArrayType.Max);
        curtainArrays[(int)Mesh.ArrayType.Vertex] = curtainVertices.ToArray();
        curtainArrays[(int)Mesh.ArrayType.Normal] = curtainNormals.ToArray();
        curtainArrays[(int)Mesh.ArrayType.TexUV] = curtainUvs.ToArray();
        curtainArrays[(int)Mesh.ArrayType.Index] = curtainIndices.ToArray();
        var curtainMesh = new ArrayMesh(); curtainMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, curtainArrays);
        _cabinet.AddChild(new MeshInstance3D { Mesh = curtainMesh, MaterialOverride = _materials["velvet"] });
        // Velvet pleats and a restrained cornice frame the silhouette from any frontal orbit.
        foreach (int side in new[] { -1, 1 })
        {
            for (int i = 0; i < 9; i++)
            {
                float x = side * (1.45f + i * .083f);
                Ellipsoid(_cabinet, new Vector3(x, 1.95f, -1.52f + Mathf.Sin(i * 1.8f) * .035f), new Vector3(.074f, 1.48f, .075f), "velvet", 20, 12);
            }
            Cylinder(_cabinet, new Vector3(side * 2.34f, 1.66f, -1.40f), .115f, .13f, 2.85f, "marble_pale");
            for (float y = .20f; y < 3.2f; y += 2.88f)
            {
                Cylinder(_cabinet, new Vector3(side * 2.34f, y, -1.4f), .18f, .18f, .12f, "gold");
                Box(_cabinet, new Vector3(side * 2.34f, y + .06f, -1.4f), new Vector3(.39f, .075f, .39f), "marble_pale");
            }
            Curve(_cabinet, new[] { new Vector3(side * 1.37f, 1.68f, -1.38f), new Vector3(side * 1.70f, 1.49f, -1.35f), new Vector3(side * 2.02f, 1.61f, -1.39f) }, .022f, "gold");
        }
        // The full figure stands on the room floor; camera presets also expose the face and garment details.
    }

    private void BuildPortrait()
    {
        _eyelids.Clear();
        _gestureBasePose.Clear(); _modelBottom = .83f; _modelTop = 2.5f;
        _authoredAnimation = null; _skeleton = null; _animatedFace = null; _fallbackHead = null;
        _headBone = _speechShape = _leftForearm = _rightForearm = -1; _gesture = ""; _gestureTime = _gestureDuration = 0;
        if (_portrait != null) { RemoveChild(_portrait); _portrait.QueueFree(); }
        _portrait = new Node3D(); AddChild(_portrait);
        UsesAuthoredModel = false;
        // Authoring contract: meters, Y up, face toward +Z, origin at the gallery floor.
        // The gallery measures authored bounds and supplies separate full-figure and close-up framing.
        // Materials, rig and idle animation remain in the GLB.
        string authoredPath = $"res://Assets/leaders/{_appearance}.glb";
        if (ResourceLoader.Exists(authoredPath))
        {
            var packed = ResourceLoader.Load<PackedScene>(authoredPath);
            if (packed != null)
            {
                var authored = packed.Instantiate();
                if (authored is Node3D model)
                {
                    _portrait.AddChild(model); UsesAuthoredModel = true;
                    BindAuthoredAnimation(model);
                    MeasureAuthoredFigure(model);
                    return;
                }
                authored.Free();
            }
        }
        var p = Portraits.TryGetValue(_appearance, out var found) ? found : new Portrait("royal", "swept", "none", "334650", "a38752");
        bool child = _age < 13;
        if (_age < 20 && p.Beard is "short" or "full") p = p with { Beard = "moustache" };
        Color skin = new(p.Skin);
        Material("skin", skin.ToHtml(false), .75f);
        _materials["skin"].SubsurfScatterEnabled = true;
        _materials["skin"].SubsurfScatterSkinMode = true;
        _materials["skin"].SubsurfScatterStrength = .13f;
        _materials["skin"].VertexColorUseAsAlbedo = true;
        Material("skin_shadow", skin.Darkened(.18f).ToHtml(false), .82f);
        Material("lip", skin.Lerp(new Color("844e48"), .40f).ToHtml(false), .72f);
        Material("hair", p.HairColor, .91f); Material("hair_light", new Color(p.HairColor).Lightened(.13f).ToHtml(false), .86f);
        Material("coat", p.Coat, .88f); Material("sash", p.Sash, .75f);
        Material("cloth_trim", new Color(p.Coat).Lightened(.16f).ToHtml(false), .82f);
        Fabric(_materials["coat"], false); Fabric(_materials["sash"], true);

        float shoulder = child ? .33f : p.Female ? .42f : .51f;
        float headY = child ? 1.94f : 2.075f;
        float headH = child ? .252f : .278f;
        float faceWidth = (child ? .196f : .202f) * p.FaceWidth;
        var body = new Node3D(); _portrait.AddChild(body);
        // These rings form a tailored bust with a flat lower cut, broad shoulders and a neck opening.
        RingSurface(body, new[] { new Vector3(0,.83f,0), new Vector3(0,1.02f,0), new Vector3(0,1.38f,0), new Vector3(0,1.61f,0), new Vector3(0,1.70f,0), new Vector3(0,1.76f,0) },
            new[] { new Vector2(.32f,.22f), new Vector2(shoulder*.82f,.26f), new Vector2(shoulder,.28f), new Vector2(shoulder,.23f), new Vector2(shoulder*.76f,.18f), new Vector2(.14f,.13f) }, "coat");
        foreach (int side in new[] { -1, 1 })
        {
            var arm = Ellipsoid(body, new Vector3(side * shoulder * .94f, 1.28f, -.015f), new Vector3(.14f, .40f, .16f), "coat");
            arm.Rotation = new Vector3(0, 0, side * .10f);
            Cylinder(body, new Vector3(side * shoulder * 1.02f, .98f, .015f), .139f, .136f, .065f, "cloth_trim");
        }
        if (p.Female) BuildGown(body, p, shoulder, child);
        else if (p.Wardrobe == "shogun") BuildShogun(body, shoulder);
        else if (p.Wardrobe == "qing") BuildQing(body, shoulder);
        else BuildUniform(body, p, shoulder);

        RingSurface(body, new[]{new Vector3(0,1.69f,.008f),new Vector3(0,1.79f,.008f),new Vector3(0,1.93f,.008f)},new[]{new Vector2(.165f,.14f),new Vector2(.128f,.113f),new Vector2(.106f,.105f)},"skin");
        var head = new Node3D { Position = new Vector3(0, headY, .012f) }; _portrait.AddChild(head); _fallbackHead = head;
        var headScale = new Vector3(faceWidth, headH * p.FaceLength, .196f);
        BuildHead(head, p, headScale, child);
        BuildHair(head, p, headScale, child);
        if (p.Female) Jewelry(head, body, headScale, child);
        if (_age >= 48) Wrinkles(head, headScale, p);
        // Child ruler representation has a shorter bust and rounder facial proportions; the historical
        // regency belongs in the UI's dated office metadata, not in this decorative presentation layer.
        if (child) body.Scale = new Vector3(.94f, .94f, .94f);
    }

    private void BuildHead(Node3D root, Portrait p, Vector3 scale, bool child)
    {
        const int rings = 96, segments = 128;
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(int r, int s)
        {
            float phi = Mathf.Pi * r / rings, theta = Mathf.Tau * s / segments;
            float yy = Mathf.Cos(phi), circle = Mathf.Sin(phi), front = Math.Max(0, Mathf.Cos(theta));
            float width = 1;
            if (yy < -.25f) width *= Mathf.Lerp(p.Chin * .86f, 1, Mathf.Clamp((yy + .9f) / .65f, 0, 1));
            float x = Mathf.Sin(theta) * circle * scale.X * width;
            float y = yy * scale.Y;
            float z = Mathf.Cos(theta) * circle * scale.Z;
            // Forehead, cheek planes and the muzzle are sculpted into a continuous head surface.
            z += front * (.022f * MathF.Exp(-MathF.Pow((yy + .22f) / .22f, 2)) + .012f * MathF.Exp(-MathF.Pow((yy - .38f) / .25f, 2)));
            z += front * .029f * MathF.Exp(-MathF.Pow((yy+.61f)/.18f,2)) * MathF.Exp(-MathF.Pow(x/.13f,2));
            float cheek = .014f * MathF.Exp(-MathF.Pow((Math.Abs(x)-.112f)/.062f,2)-MathF.Pow((y+.04f)/.07f,2));
            float sockets = -.009f * MathF.Exp(-MathF.Pow((Math.Abs(x)-.077f)/.044f,2)-MathF.Pow((y-.035f)/.021f,2));
            float brow = .010f * MathF.Exp(-MathF.Pow((Math.Abs(x)-.077f)/.052f,2)-MathF.Pow((y-.078f)/.025f,2));
            float bridge = .045f*p.Nose*MathF.Exp(-MathF.Pow(x/.021f,2)-MathF.Pow((y-.005f)/.081f,2));
            float noseTip = .030f*p.Nose*MathF.Exp(-MathF.Pow(x/.031f,2)-MathF.Pow((y+.052f)/.020f,2));
            z += front*(cheek+sockets+brow+bridge+noseTip);
            if (child) z += front * .006f;
            return new Vector3(x, y, z);
        }
        void Vertex(int r, int s)
        {
            var v = Point(r, s);
            var down=Point(Math.Min(r+1,rings),s)-Point(Math.Max(r-1,0),s);
            var across=Point(r,s+1)-Point(r,s-1);
            var normal=down.Cross(across).Normalized();
            if(normal.LengthSquared()<.5f)normal=r==0?Vector3.Up:Vector3.Down;
            surface.SetNormal(normal);
            float cheek = MathF.Exp(-MathF.Pow((Math.Abs(v.X)-.115f)/.055f,2)-MathF.Pow((v.Y+.034f)/.060f,2))*Mathf.Clamp(v.Z/.12f,0,1);
            surface.SetColor(new Color(1,1-cheek*.045f,1-cheek*.065f));
            surface.SetUV(new Vector2((float)s / segments, (float)r / rings)); surface.AddVertex(v);
        }
        for (int r = 0; r < rings; r++) for (int s = 0; s < segments; s++)
        {
            Vertex(r, s); Vertex(r, s + 1); Vertex(r + 1, s);
            Vertex(r, s + 1); Vertex(r + 1, s + 1); Vertex(r + 1, s);
        }
        AddMesh(root, surface.Commit(), "skin");
        foreach (int side in new[] { -1, 1 })
        {
            Ellipsoid(root, new Vector3(side * scale.X * .95f, -.016f, 0), new Vector3(.037f, .064f, .036f), "skin");
            Ellipsoid(root, new Vector3(side * scale.X * 1.045f, -.012f, .023f), new Vector3(.009f, .036f, .010f), "skin_shadow");
            float x = side * .077f * p.EyeGap;
            float y = .035f;
            float z = .190f;
            // Shallow, almond-like exposed eyes avoid the spherical, protruding toy-eye silhouette.
            Ellipsoid(root, new Vector3(x, y, z), new Vector3(.043f, .017f, .011f), "skin_shadow");
            Ellipsoid(root, new Vector3(x, y, z + .006f), new Vector3(.038f, .010f, .009f), "eye");
            Ellipsoid(root, new Vector3(x - side * .003f, y, z + .014f), new Vector3(.0145f, .010f, .0038f), "iris");
            Ellipsoid(root, new Vector3(x - side * .003f, y, z + .017f), new Vector3(.0075f, .0065f, .0015f), "pupil");
            Ellipsoid(root, new Vector3(x -.003f, y + .0045f, z + .0185f), new Vector3(.002f, .002f, .001f), "ivory", 12, 8);
            Curve(root, new[] { new Vector3(x-.042f,y+.002f,z), new Vector3(x-.019f,y+.015f,z+.008f), new Vector3(x+.017f,y+.014f,z+.008f), new Vector3(x+.041f,y+.001f,z) }, .0055f, "skin");
            var lid = Ellipsoid(root, new Vector3(x, y + .002f, z + .017f), new Vector3(.043f, .018f, .009f), "skin");
            lid.Visible = false; _eyelids.Add(lid);
            Curve(root, new[] { new Vector3(x-.039f,.080f,.173f), new Vector3(x-.01f,.092f,.184f), new Vector3(x+.026f,.086f,.173f), new Vector3(x+.046f,.072f,.159f) }, p.Female || child ? .006f : .009f, "hair");
        }
        // A tapered bridge, tip, alae and recessed nostrils give a distinct profile at every orbit angle.
        foreach (int side in new[] { -1, 1 })
        {
            Ellipsoid(root, new Vector3(side*.027f,-.055f,.234f), new Vector3(.019f,.012f,.015f), "skin");
            Ellipsoid(root, new Vector3(side*.021f,-.064f,.244f), new Vector3(.006f,.003f,.005f), "skin_shadow");
        }
        Curve(root, new[] { new Vector3(-.047f,-.111f,.188f), new Vector3(-.020f,-.107f,.211f), new Vector3(0,-.112f,.217f), new Vector3(.020f,-.107f,.211f), new Vector3(.047f,-.111f,.188f) }, .0058f, "lip");
        Ellipsoid(root, new Vector3(0,-.123f,.206f), new Vector3(.033f,.008f,.008f), "lip");
        if (p.Beard != "none") FacialHair(root, p, scale);
    }

    private void BuildHair(Node3D root, Portrait p, Vector3 s, bool child)
    {
        if (p.Hair is "fez" or "qing" or "shogun")
        {
            // A short hairline around the skull remains visible below period headwear.
            HairCap(root, s, 1.0f, true);
            if (p.Hair == "fez")
            {
                Cylinder(root, new Vector3(0, s.Y + .055f, -.025f), .144f, .179f, .18f, "red");
                Curve(root, new[] { new Vector3(.012f,s.Y+.153f,-.017f), new Vector3(.13f,s.Y+.12f,-.020f), new Vector3(.18f,s.Y-.06f,-.025f) }, .010f, "black");
                Ellipsoid(root, new Vector3(.18f,s.Y-.095f,-.025f), new Vector3(.020f,.043f,.020f), "black");
            }
            else if (p.Hair == "qing")
            {
                Cylinder(root, new Vector3(0, s.Y-.014f, -.02f), .21f, .215f, .08f, "black");
                Cylinder(root, new Vector3(0, s.Y+.065f, -.02f), .035f, .202f, .11f, "red");
                Ellipsoid(root, new Vector3(0,s.Y+.138f,-.02f), new Vector3(.028f,.032f,.028f), "gold");
                Curve(root, new[] { new Vector3(.0f,.13f,-.19f),new Vector3(.025f,-.04f,-.21f),new Vector3(.035f,-.32f,-.21f),new Vector3(.025f,-.57f,-.21f) }, .018f, "hair");
            }
            else
            {
                Ellipsoid(root, new Vector3(0, s.Y-.018f, -.012f), new Vector3(.183f,.086f,.169f), "black");
                var hat = Ellipsoid(root, new Vector3(0, s.Y+.127f, -.05f), new Vector3(.080f,.19f,.082f), "black");
                hat.Rotation = new Vector3(-.21f, 0, 0);
                Curve(root, new[] { new Vector3(-.159f,.16f,.046f), new Vector3(-.129f,-.13f,.132f),new Vector3(0,-.26f,.121f),new Vector3(.129f,-.13f,.132f),new Vector3(.159f,.16f,.046f) }, .004f, "black");
            }
            return;
        }
        HairCap(root, s, p.Hair == "receding" ? .66f : .93f, p.Hair == "receding");
        if(p.Hair=="receding")
            foreach(int side in new[]{-1,1})for(int i=0;i<8;i++)
            {
                float t=i/7f;
                Curve(root,new[]{new Vector3(side*s.X*.72f,.19f,-.015f-t*.065f),new Vector3(side*(s.X+.002f),.105f,.01f-t*.075f),new Vector3(side*(s.X+.010f),.013f,.020f-t*.073f),new Vector3(side*s.X*.97f,-.040f,.025f-t*.071f)},.009f,i%3==0?"hair_light":"hair");
            }
        if (p.Female)
        {
            Ellipsoid(root, new Vector3(0, -.007f, -.204f), new Vector3(.142f,.128f,.084f), "hair");
            foreach (int side in new[] { -1, 1 })
            {
                for (int i = 0; i < 8; i++)
                {
                    float t = i / 7f;
                    Curve(root, new[] { new Vector3(side*.012f,s.Y+.008f,-.025f), new Vector3(side*(.08f+t*.055f),s.Y*.84f,.105f-t*.10f),new Vector3(side*(s.X*.94f),.09f,-.01f-t*.07f),new Vector3(side*.13f,-.10f,-.18f) }, .018f, i % 3 == 0 ? "hair_light" : "hair");
                }
                if (p.Hair == "curls" || child)
                    for (int i=0;i<5;i++) Ellipsoid(root,new Vector3(side*(s.X+.005f),.085f-i*.035f,.037f),new Vector3(.028f,.042f,.025f),i%2==0?"hair":"hair_light");
            }
        }
        else
        {
            int locks = p.Hair == "receding" ? 8 : 17;
            for (int i = 0; i < locks; i++)
            {
                float t = i / (float)(locks-1), x = -.17f + t * .34f;
                float arc = Mathf.Sqrt(Mathf.Max(.05f,1-Mathf.Pow(x/(s.X+.012f),2)));
                float lift = p.Hair == "high_swept" ? .042f : .014f;
                Vector3 Scalp(float a,float drift) => new(x+drift,(s.Y+lift)*arc*Mathf.Cos(a)+.007f,(s.Z+.014f)*arc*Mathf.Sin(a)-.009f);
                if(p.Hair=="receding")
                    Curve(root,new[]{Scalp(-.65f,0),Scalp(-.9f,.003f),Scalp(-1.2f,.004f),Scalp(-1.52f,0)},.009f,i%4==0?"hair_light":"hair");
                else
                    Curve(root,new[]{Scalp(.82f,0),Scalp(.34f,-.008f),Scalp(-.36f,.007f),Scalp(-1.1f,0)},.012f,i%4==0?"hair_light":"hair");
            }
            if (p.Hair == "long_swept")
                foreach(int side in new[]{-1,1}) Ellipsoid(root,new Vector3(side*.164f,-.02f,-.132f),new Vector3(.043f,.145f,.070f),"hair");
        }
    }

    private void HairCap(Node3D root, Vector3 s, float frontExtent, bool receding)
    {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        const int rows = 22, columns = 64;
        Vector3 Point(int row, int col)
        {
            float t = Mathf.Tau * col / columns;
            float front = Math.Max(0, Mathf.Cos(t));
            float extent = Mathf.Lerp(1.9f, frontExtent, front * front);
            if (receding) extent -= .14f * MathF.Exp(-MathF.Pow(Mathf.Sin(t)/.6f,2)) * front;
            float a = receding ? Mathf.Lerp(.65f,extent,row/(float)rows) : row/(float)rows*extent;
            return new Vector3(Mathf.Sin(t)*Mathf.Sin(a)*(s.X+.012f),Mathf.Cos(a)*(s.Y+.01f),Mathf.Cos(t)*Mathf.Sin(a)*(s.Z+.01f)-.012f);
        }
        void V(int a,int b){var p=Point(a,b);surface.SetNormal(new Vector3(p.X/(s.X*s.X),p.Y/(s.Y*s.Y),p.Z/(s.Z*s.Z)).Normalized());surface.AddVertex(p);}
        for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)
        {
            if(receding&&Mathf.Cos(Mathf.Tau*(c+.5f)/columns)>.42f)continue;
            V(r,c);V(r,c+1);V(r+1,c);V(r,c+1);V(r+1,c+1);V(r+1,c);
        }
        AddMesh(root,surface.Commit(),"hair");
    }

    private void FacialHair(Node3D root, Portrait p, Vector3 s)
    {
        if(p.Beard is "sideburns" or "large_sideburns")
        {
            foreach(int side in new[]{-1,1})
            {
                float size=p.Beard=="large_sideburns"?1.42f:1;
                Ellipsoid(root,new Vector3(side*s.X*.89f,-.028f,.057f),new Vector3(.021f*size,.078f*size,.030f),"hair");
                for(int j=0;j<5;j++) Curve(root,new[]{new Vector3(side*s.X*.95f,.04f-j*.010f,.027f),new Vector3(side*s.X*.91f,-.032f-j*.011f,.065f),new Vector3(side*s.X*.80f,-.070f-j*.009f,.078f)},.004f,j%2==0?"hair":"hair_light");
            }
            return;
        }
        if(p.Beard is "moustache" or "full" or "short" or "qing")
            foreach(int side in new[]{-1,1})
                Curve(root,new[]{new Vector3(0,-.082f,.225f),new Vector3(side*.023f,-.086f,.229f),new Vector3(side*.051f,-.101f,.206f),new Vector3(side*.064f,-.116f,.179f)},p.Beard=="qing"?.007f:.012f,"hair");
        if(p.Beard is "full" or "short")
        {
            float length=p.Beard=="full"?.11f:.048f;
            Ellipsoid(root,new Vector3(0,-.206f-length*.3f,.095f),new Vector3(.128f,.090f+length,.093f),"hair");
            foreach(int side in new[]{-1,1}) Ellipsoid(root,new Vector3(side*.113f,-.13f,.085f),new Vector3(.048f,.086f,.048f),"hair");
            for(int i=-5;i<=5;i++) Curve(root,new[]{new Vector3(i*.017f,-.151f,.172f),new Vector3(i*.016f,-.221f,.192f),new Vector3(i*.009f,-.263f-length,.102f)},.005f,i%3==0?"hair_light":"hair");
        }
        if(p.Beard=="qing")
            for(int i=-3;i<=3;i++) Curve(root,new[]{new Vector3(i*.007f,-.148f,.174f),new Vector3(i*.009f,-.24f,.174f),new Vector3(i*.006f,-.33f,.113f)},.0036f,"hair");
    }

    private void Wrinkles(Node3D root, Vector3 s, Portrait p)
    {
        for(int i=0;i<(_age>63?3:2);i++)
        {
            float y=.138f+i*.026f;
            var points=new Vector3[13];
            for(int j=0;j<points.Length;j++)
            {
                float x=-.082f+j*.164f/(points.Length-1),yy=y/s.Y;
                float circle=Mathf.Sqrt(1-yy*yy),front=Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(x/(circle*s.X),2)));
                float z=front*circle*s.Z+front*(.022f*MathF.Exp(-MathF.Pow((yy+.22f)/.22f,2))+.012f*MathF.Exp(-MathF.Pow((yy-.38f)/.25f,2)));
                points[j]=new Vector3(x,y,z+.0005f);
            }
            Curve(root,points,.0007f,"skin_shadow");
        }
        foreach(int side in new[]{-1,1})
        {
            Curve(root,new[]{new Vector3(side*.044f,-.061f,.199f),new Vector3(side*.068f,-.094f,.176f),new Vector3(side*.070f,-.13f,.159f)},.0017f,"skin_shadow");
            Curve(root,new[]{new Vector3(side*.119f,.024f,.151f),new Vector3(side*.146f,.006f,.126f)},.0014f,"skin_shadow");
        }
    }

    private void BuildUniform(Node3D root, Portrait p, float shoulder)
    {
        bool civilian=p.Wardrobe=="statesman";
        // Ivory waistcoat and the two folded lapels are actual layered surfaces, not painted lines.
        Panel(root,new[]{new Vector3(-.13f,1.74f,.17f),new Vector3(.13f,1.74f,.17f),new Vector3(.105f,1.12f,.27f),new Vector3(-.10f,1.12f,.27f)},civilian?"ivory":"coat");
        foreach(int side in new[]{-1,1})
        {
            Panel(root,new[]{new Vector3(side*.12f,1.74f,.175f),new Vector3(side*.30f,1.61f,.20f),new Vector3(side*.135f,1.22f,.285f),new Vector3(side*.055f,1.53f,.288f)},"cloth_trim");
            Panel(root,new[]{new Vector3(side*.105f,1.76f,.14f),new Vector3(side*.016f,1.76f,.198f),new Vector3(side*.065f,1.64f,.215f)},"ivory");
        }
        if(civilian)
        {
            Ellipsoid(root,new Vector3(0,1.67f,.227f),new Vector3(.085f,.035f,.024f),"black");
            Panel(root,new[]{new Vector3(-.029f,1.68f,.24f),new Vector3(.036f,1.68f,.24f),new Vector3(.063f,1.40f,.286f),new Vector3(-.022f,1.39f,.283f)},"black");
            for(int i=0;i<4;i++) Ellipsoid(root,new Vector3(.014f,1.39f-i*.075f,.283f),new Vector3(.008f,.008f,.007f),"gold",12,8);
            Curve(root,new[]{new Vector3(.012f,1.19f,.282f),new Vector3(.10f,1.11f,.29f),new Vector3(.205f,1.20f,.246f)},.004f,"gold");
        }
        else
        {
            Sash(root,shoulder,.105f);
            for(int i=0;i<5;i++)foreach(int side in new[]{-1,1}) Ellipsoid(root,new Vector3(side*.085f,1.56f-i*.095f,.28f),new Vector3(.010f,.010f,.009f),"gold",16,8);
            foreach(int side in new[]{-1,1})
            {
                Ellipsoid(root,new Vector3(side*shoulder*.80f,1.65f,.013f),new Vector3(.18f,.052f,.15f),"gold");
                for(int i=0;i<10;i++) Cylinder(root,new Vector3(side*(shoulder*.63f+i*.015f),1.59f,.095f),.006f,.008f,.085f,"brass_light",10);
            }
            Medal(root,new Vector3(.205f,1.40f,.272f),.055f,8);
            Medal(root,new Vector3(.28f,1.53f,.226f),.029f,6);
            for(int i=0;i<3;i++)
            {
                Box(root,new Vector3(.155f+i*.035f,1.59f,.246f),new Vector3(.021f,.046f,.008f),i%2==0?"red":"sash");
                Ellipsoid(root,new Vector3(.155f+i*.035f,1.556f,.253f),new Vector3(.014f,.015f,.005f),"gold",16,8);
            }
            Curve(root,new[]{new Vector3(-.33f,1.60f,.195f),new Vector3(-.32f,1.37f,.265f),new Vector3(-.10f,1.49f,.294f)},.010f,"gold");
            Curve(root,new[]{new Vector3(-.35f,1.60f,.190f),new Vector3(-.35f,1.32f,.255f),new Vector3(-.09f,1.46f,.294f)},.007f,"brass_light");
        }
    }

    private void BuildGown(Node3D root, Portrait p, float shoulder, bool child)
    {
        Panel(root,new[]{new Vector3(-.12f,1.74f,.165f),new Vector3(.12f,1.74f,.165f),new Vector3(.18f,1.54f,.267f),new Vector3(-.18f,1.54f,.267f)},"ivory");
        Curve(root,new[]{new Vector3(-shoulder*.86f,1.63f,.13f),new Vector3(-.21f,1.56f,.24f),new Vector3(0,1.53f,.278f),new Vector3(.21f,1.56f,.24f),new Vector3(shoulder*.86f,1.63f,.13f)},.026f,"ivory");
        for(int i=-7;i<=7;i++)
        {
            float x=i*.025f;
            Curve(root,new[]{new Vector3(x*.48f,1.54f,.27f),new Vector3(x*.67f,1.35f,.284f),new Vector3(x,1.06f,.269f)},.007f,"cloth_trim");
        }
        foreach(int side in new[]{-1,1})
        {
            Ellipsoid(root,new Vector3(side*shoulder*.90f,1.49f,.00f),new Vector3(.15f,.18f,.18f),"coat");
            for(int i=0;i<5;i++)Curve(root,new[]{new Vector3(side*(shoulder*.64f+i*.017f),1.66f,.045f),new Vector3(side*(shoulder*.83f+i*.018f),1.49f,.166f),new Vector3(side*(shoulder*.78f+i*.016f),1.35f,.10f)},.010f,"cloth_trim");
        }
        if(!child) Sash(root,shoulder,.075f);
        Medal(root,new Vector3(.18f,1.36f,.289f),.035f,8);
    }

    private void BuildShogun(Node3D root,float shoulder)
    {
        foreach(int side in new[]{-1,1})
        {
            Panel(root,new[]{new Vector3(side*.10f,1.76f,.12f),new Vector3(side*.68f,1.65f,-.01f),new Vector3(side*.49f,1.36f,.18f),new Vector3(side*.16f,1.34f,.285f)},"cloth_trim");
            Curve(root,new[]{new Vector3(side*.09f,1.75f,.17f),new Vector3(side*.10f,1.58f,.235f),new Vector3(-side*.13f,1.34f,.294f)},.024f,"ivory");
            Ellipsoid(root,new Vector3(side*.37f,1.57f,.121f),new Vector3(.038f,.038f,.007f),"ivory");
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.Tau/3;
                Ellipsoid(root,new Vector3(side*.37f+Mathf.Sin(a)*.018f,1.57f+Mathf.Cos(a)*.018f,.129f),new Vector3(.013f,.010f,.004f),"coat",16,8);
            }
        }
        Box(root,new Vector3(0,1.21f,.278f),new Vector3(.58f,.15f,.026f),"sash");
        Box(root,new Vector3(0,1.215f,.297f),new Vector3(.14f,.14f,.035f),"cloth_trim");
        for(int i=-6;i<=6;i++) Curve(root,new[]{new Vector3(i*.027f,1.11f,.274f),new Vector3(i*.045f,.84f,.231f)},.004f,"cloth_trim");
    }

    private void BuildQing(Node3D root,float shoulder)
    {
        Curve(root,new[]{new Vector3(-.12f,1.77f,.13f),new Vector3(.08f,1.65f,.227f),new Vector3(.23f,1.52f,.258f),new Vector3(.22f,1.03f,.268f)},.028f,"sash");
        Ellipsoid(root,new Vector3(0,1.39f,.282f),new Vector3(.165f,.165f,.008f),"gold");
        for(int i=0;i<16;i++)
        {
            float a=Mathf.Tau*i/16;
            Ellipsoid(root,new Vector3(Mathf.Sin(a)*.137f,1.39f+Mathf.Cos(a)*.137f,.295f),new Vector3(.012f,.013f,.006f),"sash",12,8);
        }
        Curve(root,new[]{new Vector3(-.035f,1.48f,.304f),new Vector3(.073f,1.46f,.304f),new Vector3(.08f,1.36f,.304f),new Vector3(-.056f,1.31f,.304f),new Vector3(-.084f,1.39f,.304f),new Vector3(.016f,1.39f,.304f)},.014f,"sash");
        for(int i=0;i<27;i++)
        {
            float a=Mathf.Pi*.14f+Mathf.Pi*.72f*i/26;
            Ellipsoid(root,new Vector3(Mathf.Cos(a)*.30f,1.61f-Mathf.Sin(a)*.40f,.302f),new Vector3(.013f,.014f,.013f),"pearl",12,8);
        }
        foreach(int side in new[]{-1,1}) for(int i=0;i<7;i++)
            Curve(root,new[]{new Vector3(side*(.32f+i*.022f),1.40f,.19f),new Vector3(side*(.35f+i*.018f),1.19f,.18f),new Vector3(side*(.36f+i*.014f),1.07f,.13f)},.0035f,"gold");
    }

    private void Jewelry(Node3D head,Node3D body,Vector3 s,bool child)
    {
        if(!child)
        {
            for(int i=0;i<17;i++)
            {
                float a=Mathf.Pi*.12f+Mathf.Pi*.76f*i/16;
                Ellipsoid(body,new Vector3(Mathf.Cos(a)*.15f,1.76f-Mathf.Sin(a)*.18f,.213f+Mathf.Sin(a)*.023f),new Vector3(.009f,.010f,.008f),"pearl",16,8);
            }
            foreach(int side in new[]{-1,1})
            {
                Ellipsoid(head,new Vector3(side*(s.X+.012f),-.085f,.02f),new Vector3(.009f,.019f,.009f),"pearl",16,8);
                Ellipsoid(head,new Vector3(side*(s.X+.013f),-.107f,.02f),new Vector3(.013f,.022f,.013f),"pearl",16,8);
            }
        }
        for(int i=-5;i<=5;i++)
        {
            float x=i*.024f,z=.13f-Math.Abs(i)*.014f,y=s.Y*.81f+(5-Math.Abs(i))*.004f;
            Ellipsoid(head,new Vector3(x,y,z),new Vector3(.009f,.014f,.009f),"pearl",16,8);
            if(!child) Ellipsoid(head,new Vector3(x,y+.025f*(1-Math.Abs(i)/6f),z),new Vector3(.008f,.012f,.007f),i%2==0?"jewel":"gold",16,8);
        }
    }

    private void Medal(Node3D parent,Vector3 center,float radius,int points)
    {
        for(int i=0;i<points;i++)
        {
            float a=Mathf.Tau*i/points;
            Panel(parent,new[]{center+new Vector3(Mathf.Sin(a)*radius,Mathf.Cos(a)*radius,.008f),center+new Vector3(Mathf.Sin(a-.3f)*radius*.30f,Mathf.Cos(a-.3f)*radius*.30f,0),center+new Vector3(Mathf.Sin(a+.3f)*radius*.30f,Mathf.Cos(a+.3f)*radius*.30f,0)},"brass_light");
        }
        Ellipsoid(parent,center+new Vector3(0,0,.010f),new Vector3(radius*.32f,radius*.32f,.010f),"red",20,10);
        Ellipsoid(parent,center+new Vector3(0,0,.019f),new Vector3(radius*.13f,radius*.13f,.006f),"gold",16,8);
    }

    private void Sash(Node3D parent,float shoulder,float width)
    {
        Vector3 P(float t,float edge)
        {
            float x=Mathf.Lerp(-shoulder*.71f,shoulder*.64f,t)+edge*width*.5f;
            float y=Mathf.Lerp(1.69f,1.03f,t)+edge*width*.37f;
            float z=Mathf.Sqrt(Mathf.Max(.08f,1-Mathf.Pow(x/(shoulder*1.05f),2)))*.275f+.040f;
            return new Vector3(x,y,z);
        }
        for(int i=0;i<18;i++)
        {
            float a=i/18f,b=(i+1)/18f;
            Panel(parent,new[]{P(a,-1),P(a,1),P(b,1),P(b,-1)},"sash");
        }
    }

    private StandardMaterial3D Material(string id,string hex,float roughness=.8f,float metallic=0)
    {
        var m=new StandardMaterial3D{AlbedoColor=new Color(hex),Roughness=roughness,Metallic=metallic,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
        _materials[id]=m; return m;
    }

    private void Fabric(StandardMaterial3D material,bool silk)
    {
        var img=Image.CreateEmpty(128,128,false,Image.Format.Rgba8);
        var random=new Random(1836);
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            float thread=(x%3==0||y%3==0)?.88f:.99f;
            float n=thread+(float)random.NextDouble()*.035f;
            img.SetPixel(x,y,new Color(n,n,n));
        }
        var normal=Image.CreateFromData(128,128,false,Image.Format.Rgba8,img.GetData());
        normal.BumpMapToNormalMap(.35f); normal.GenerateMipmaps(); img.GenerateMipmaps();
        material.AlbedoTexture=ImageTexture.CreateFromImage(img); material.NormalEnabled=true;
        material.NormalTexture=ImageTexture.CreateFromImage(normal);material.NormalScale=.17f;
        material.Uv1Scale=new Vector3(5,5,1);material.TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
        material.RimEnabled=true;material.Rim=silk?.13f:.22f;material.RimTint=.55f;
    }

    private MeshInstance3D AddMesh(Node3D parent,Mesh mesh,string material)
    {
        var instance=new MeshInstance3D{Mesh=mesh,MaterialOverride=_materials[material]};parent.AddChild(instance);return instance;
    }
    private MeshInstance3D Box(Node3D parent,Vector3 position,Vector3 size,string material)
    {
        var n=AddMesh(parent,new BoxMesh{Size=size},material);n.Position=position;return n;
    }
    private MeshInstance3D Ellipsoid(Node3D parent,Vector3 position,Vector3 radii,string material,int segments=32,int rings=20)
    {
        var n=AddMesh(parent,new SphereMesh{Radius=1,Height=2,RadialSegments=segments,Rings=rings},material);n.Position=position;n.Scale=radii;return n;
    }
    private MeshInstance3D Cylinder(Node3D parent,Vector3 position,float top,float bottom,float height,string material,int segments=40)
    {
        var n=AddMesh(parent,new CylinderMesh{TopRadius=top,BottomRadius=bottom,Height=height,RadialSegments=segments},material);n.Position=position;return n;
    }
    private void Curve(Node3D parent,Vector3[] points,float radius,string material)
    {
        var smooth=new List<Vector3>();
        for(int i=0;i<points.Length-1;i++)
        {
            var a=points[Math.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Math.Min(points.Length-1,i+2)];
            for(int j=0;j<5;j++)
            {
                float t=j/5f,t2=t*t,t3=t2*t;
                smooth.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));
            }
        }
        smooth.Add(points[^1]);
        for(int i=0;i<smooth.Count-1;i++)
        {
            var delta=smooth[i+1]-smooth[i];
            if(delta.Length()<.00001f)continue;
            var n=Cylinder(parent,(smooth[i]+smooth[i+1])*.5f,radius,radius,delta.Length(),material,10);
            n.Quaternion=new Quaternion(Vector3.Up,delta.Normalized());
            if(i>0)Ellipsoid(parent,smooth[i],Vector3.One*radius,material,10,6);
        }
    }
    private void Panel(Node3D parent,Vector3[] polygon,string material)
    {
        var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
        for(int i=1;i<polygon.Length-1;i++)foreach(int index in new[]{0,i,i+1})
        {
            st.SetNormal(Vector3.Back);st.SetUV(new Vector2(polygon[index].X,polygon[index].Y));st.AddVertex(polygon[index]);
        }
        AddMesh(parent,st.Commit(),material);
    }
    private void RingSurface(Node3D parent,Vector3[] centers,Vector2[] radii,string material,int segments=48)
    {
        var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
        void V(int r,int s)
        {
            float a=Mathf.Tau*s/segments;
            st.SetNormal(new Vector3(Mathf.Sin(a),.10f,Mathf.Cos(a)).Normalized());
            st.SetUV(new Vector2((float)s/segments,(float)r/(centers.Length-1)));
            st.AddVertex(centers[r]+new Vector3(Mathf.Sin(a)*radii[r].X,0,Mathf.Cos(a)*radii[r].Y));
        }
        for(int r=0;r<centers.Length-1;r++)for(int s=0;s<segments;s++)
        {
            if(centers[r+1].Y>centers[r].Y)
            {V(r,s);V(r+1,s);V(r,s+1);V(r,s+1);V(r+1,s);V(r+1,s+1);}
            else
            {V(r,s);V(r,s+1);V(r+1,s);V(r,s+1);V(r+1,s+1);V(r+1,s);}
        }
        AddMesh(parent,st.Commit(),material);
    }
}

