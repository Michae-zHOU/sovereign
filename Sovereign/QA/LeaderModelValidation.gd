extends SceneTree
## Run after asset import, independently of the C# build:
## godot --headless --path outputs/Sovereign --script res://QA/LeaderModelValidation.gd -- --report=absolute-path.json
## Omit --headless for actual renderer memory counters.

const IDENTITIES = [
    "william_iv", "victoria", "frederick_william_iii", "frederick_william_iv",
    "tokugawa_ienari", "tokugawa_ieyoshi", "louis_philippe", "ferdinand_i",
    "nicholas_i", "andrew_jackson", "martin_van_buren", "william_henry_harrison",
    "john_tyler", "james_polk", "daoguang", "mahmud_ii", "abdulmejid_i",
    "isabella_ii", "maria_ii", "leopold_i", "isabella_ii_adolescent"
]
const REQUIRED_BONES = ["Root", "Pelvis", "Chest", "Neck", "Head", "LeftUpperArm", "LeftForearm", "RightUpperArm", "RightForearm"]
var failures: Array[String] = []
var results: Array[Dictionary] = []
var report_path = "user://leader-model-validation.json"
var camera: Camera3D
var render_metrics_available = false
var baseline_nodes = 0

func _initialize():
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--report="):
            report_path = arg.trim_prefix("--report=")
    run.call_deferred()

func require(condition: bool, message: String) -> bool:
    if not condition:
        failures.append(message)
        push_error("LEADER_QA_FAIL " + message)
    return condition

func counters() -> Dictionary:
    return {
        "nodes": int(Performance.get_monitor(Performance.OBJECT_NODE_COUNT)),
        "orphan_nodes": int(Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT)),
        "resources": int(Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT)),
        "static_memory_bytes": int(Performance.get_monitor(Performance.MEMORY_STATIC)),
        "video_memory_bytes": int(Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)) if render_metrics_available else null,
        "texture_memory_bytes": int(Performance.get_monitor(Performance.RENDER_TEXTURE_MEM_USED)) if render_metrics_available else null,
        "buffer_memory_bytes": int(Performance.get_monitor(Performance.RENDER_BUFFER_MEM_USED)) if render_metrics_available else null,
        "draw_calls": int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)) if render_metrics_available else null
    }

func vector_data(v: Vector3) -> Array:
    return [v.x, v.y, v.z]

func morph_geometry(path: String) -> Dictionary:
    # The headless dummy renderer cannot read back blend-shape arrays. Inspect the source GLB
    # POSITION accessors directly in both render modes, then separately test Godot's morph controls.
    var empty = {"vertices": 0, "affected_vertices": 0, "maximum_delta_m": 0.0}
    var result = {"Blink": empty.duplicate(), "Speech": empty.duplicate()}
    var bytes = FileAccess.get_file_as_bytes(path)
    if not require(bytes.size() >= 28 and bytes.decode_u32(0) == 0x46546c67, path + ": no readable GLB source for morph geometry"):
        return result
    var json_length = bytes.decode_u32(12)
    var document = JSON.parse_string(bytes.slice(20, 20 + json_length).get_string_from_utf8()) as Dictionary
    if not require(document != null, path + ": invalid GLB JSON"):
        return result
    var binary_start = 20 + json_length + 8
    for mesh in document.get("meshes", []):
        var names = mesh.get("extras", {}).get("targetNames", [])
        for shape in ["Blink", "Speech"]:
            var index = names.find(shape)
            if index < 0:
                continue
            for primitive in mesh.get("primitives", []):
                var targets = primitive.get("targets", [])
                if index >= targets.size() or not targets[index].has("POSITION"):
                    continue
                var accessor = document["accessors"][int(targets[index]["POSITION"])]
                if not require(accessor.get("componentType") == 5126 and accessor.get("type") == "VEC3",
                    path + ": unsupported morph POSITION accessor " + shape):
                    continue
                var count = int(accessor["count"])
                var deltas = PackedVector3Array()
                deltas.resize(count)
                if accessor.has("bufferView"):
                    var view = document["bufferViews"][int(accessor["bufferView"])]
                    var offset = binary_start + int(view.get("byteOffset", 0)) + int(accessor.get("byteOffset", 0))
                    var stride = int(view.get("byteStride", 12))
                    if not require(offset >= binary_start and offset + maxi(0, count - 1) * stride + 12 <= bytes.size(),
                        path + ": morph buffer exceeds GLB " + shape):
                        continue
                    for vertex in range(count):
                        var cursor = offset + vertex * stride
                        deltas[vertex] = Vector3(bytes.decode_float(cursor), bytes.decode_float(cursor + 4), bytes.decode_float(cursor + 8))
                if accessor.has("sparse"):
                    var sparse = accessor["sparse"]
                    var indices = sparse["indices"]
                    var values = sparse["values"]
                    var iv = document["bufferViews"][int(indices["bufferView"])]
                    var vv = document["bufferViews"][int(values["bufferView"])]
                    var index_offset = binary_start + int(iv.get("byteOffset", 0)) + int(indices.get("byteOffset", 0))
                    var value_offset = binary_start + int(vv.get("byteOffset", 0)) + int(values.get("byteOffset", 0))
                    var component = int(indices["componentType"])
                    var index_size = 1 if component == 5121 else (2 if component == 5123 else (4 if component == 5125 else 0))
                    var sparse_count = int(sparse["count"])
                    if not require(index_size > 0 and index_offset + sparse_count * index_size <= bytes.size()
                        and value_offset + sparse_count * 12 <= bytes.size(), path + ": invalid sparse morph data " + shape):
                        continue
                    var last_index = -1
                    for sparse_item in range(sparse_count):
                        var position = index_offset + sparse_item * index_size
                        var vertex = bytes.decode_u8(position) if index_size == 1 else (bytes.decode_u16(position) if index_size == 2 else bytes.decode_u32(position))
                        if not require(vertex > last_index and vertex < count, path + ": invalid sparse morph vertex index " + shape):
                            break
                        last_index = vertex
                        var cursor = value_offset + sparse_item * 12
                        deltas[vertex] = Vector3(bytes.decode_float(cursor), bytes.decode_float(cursor + 4), bytes.decode_float(cursor + 8))
                result[shape]["vertices"] += count
                for delta in deltas:
                    if not require(delta.is_finite(), path + ": non-finite morph vertex " + shape):
                        break
                    var length = delta.length()
                    if length > .000001:
                        result[shape]["affected_vertices"] += 1
                        result[shape]["maximum_delta_m"] = maxf(result[shape]["maximum_delta_m"], length)
    return result

func test_model(identity: String) -> void:
    var row: Dictionary = {"identity": identity, "failures_before": failures.size()}
    var path = "res://Assets/leaders/" + identity + ".glb"
    var start = Time.get_ticks_msec()
    if not require(ResourceLoader.exists(path), identity + ": GLB is missing or not imported"):
        row["status"] = "missing"
        results.append(row)
        return
    var packed = ResourceLoader.load(path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE) as PackedScene
    if not require(packed != null, identity + ": resource is not a PackedScene"):
        row["status"] = "load_failed"
        results.append(row)
        return
    var model = packed.instantiate() as Node3D
    if not require(model != null, identity + ": root is not Node3D"):
        row["status"] = "invalid_root"
        results.append(row)
        return
    root.add_child(model)
    await process_frame
    var skeleton: Skeleton3D
    var animation: AnimationPlayer
    var face: MeshInstance3D
    var mesh_nodes: Array[MeshInstance3D] = []
    var pending: Array[Node] = [model]
    var node_count = 0
    var triangles = 0
    var skinned_meshes = 0
    var minimum = Vector3(INF, INF, INF)
    var maximum = Vector3(-INF, -INF, -INF)
    while not pending.is_empty():
        var node = pending.pop_back()
        node_count += 1
        if node is Skeleton3D:
            skeleton = node
        if node is AnimationPlayer:
            animation = node
        if node is MeshInstance3D and node.mesh != null:
            mesh_nodes.append(node)
            if node.skin != null and node.skin.get_bind_count() > 0:
                skinned_meshes += 1
            if node.find_blend_shape_by_name("Blink") >= 0 and node.find_blend_shape_by_name("Speech") >= 0:
                face = node
            var bounds = node.get_aabb()
            for corner in range(8):
                var p: Vector3 = model.to_local(node.to_global(bounds.get_endpoint(corner)))
                minimum = minimum.min(p)
                maximum = maximum.max(p)
            for surface in range(node.mesh.get_surface_count()):
                var arrays = node.mesh.surface_get_arrays(surface)
                var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
                triangles += (indices.size() if indices.size() > 0 else arrays[Mesh.ARRAY_VERTEX].size()) / 3
        pending.append_array(node.get_children())
    var size = maximum - minimum
    row.merge({"nodes": node_count, "mesh_nodes": mesh_nodes.size(), "skinned_meshes": skinned_meshes,
        "triangles": triangles, "bounds_min": vector_data(minimum), "bounds_max": vector_data(maximum), "height_m": size.y,
        "load_ms": Time.get_ticks_msec() - start})
    require(minimum.is_finite() and maximum.is_finite() and size.x > 0.15 and size.z > 0.05 and size.y > 0.70 and size.y < 3.5,
        identity + ": invalid or implausible figure bounds " + str(size))
    require(minimum.y > -0.25 and minimum.y < 0.25, identity + ": full figure feet must be near floor Y=0, got " + str(minimum.y))
    require(skinned_meshes > 0, identity + ": no skin-bound meshes")
    require(face != null, identity + ": no mesh has both Blink and Speech blend shapes")
    require(skeleton != null, identity + ": no Skeleton3D")
    require(animation != null, identity + ": no AnimationPlayer")
    if face != null:
        var geometry = morph_geometry(path)
        row["blink_geometry"] = geometry["Blink"]
        row["speech_geometry"] = geometry["Speech"]
        require(row["blink_geometry"]["affected_vertices"] > 8, identity + ": Blink has no meaningful vertex deformation")
        require(row["speech_geometry"]["affected_vertices"] > 8, identity + ": Speech has no meaningful vertex deformation")
        var speech = face.find_blend_shape_by_name("Speech")
        face.set_blend_shape_value(speech, .65)
        require(absf(face.get_blend_shape_value(speech) - .65) < .0001, identity + ": Speech is not writable")
        face.set_blend_shape_value(speech, 0)
    if skeleton != null and animation != null and face != null:
        if identity == "daoguang":
            var skin_material = face.get_active_material(0) as BaseMaterial3D
            row["face_normal_connected"] = skin_material != null and skin_material.normal_enabled and skin_material.normal_texture != null
            require(row["face_normal_connected"], identity + ": skin detail must reach the rendered face material")
        var bones_valid = true
        for bone in REQUIRED_BONES:
            bones_valid = require(skeleton.find_bone(bone) >= 0, identity + ": missing bone " + bone) and bones_valid
        row["bones"] = skeleton.get_bone_count()
        var idle_name = ""
        for name in animation.get_animation_list():
            if "idle" in name.to_lower():
                idle_name = name
                break
        if require(idle_name != "", identity + ": no idle animation") and bones_valid:
            var idle = animation.get_animation(idle_name)
            require(idle.length > 1.0 and idle.get_track_count() >= 3, identity + ": empty idle animation")
            animation.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
            animation.play(idle_name)
            animation.advance(0)
            var head = skeleton.find_bone("Head")
            var neck = skeleton.find_bone("Neck")
            var initial_head = skeleton.get_bone_pose_rotation(head)
            var initial_neck = skeleton.get_bone_pose_rotation(neck)
            var head_motion = 0.0
            var neck_motion = 0.0
            var blink_min = 1.0
            var blink_max = 0.0
            var eye_initial: Dictionary = {}
            var eye_motion: Dictionary = {}
            if identity == "daoguang":
                for eye_name in ["LeftEye", "RightEye"]:
                    var eye_bone = skeleton.find_bone(eye_name)
                    if require(eye_bone >= 0, identity + ": missing independent " + eye_name):
                        eye_initial[eye_bone] = skeleton.get_bone_pose_rotation(eye_bone)
                        eye_motion[eye_bone] = 0.0
            var blink = face.find_blend_shape_by_name("Blink")
            for sample in range(int(ceil(maxf(idle.length, 8.0) * 30))):
                animation.advance(1.0 / 30.0)
                head_motion = maxf(head_motion, initial_head.angle_to(skeleton.get_bone_pose_rotation(head)))
                neck_motion = maxf(neck_motion, initial_neck.angle_to(skeleton.get_bone_pose_rotation(neck)))
                for eye_bone in eye_initial:
                    eye_motion[eye_bone] = maxf(eye_motion[eye_bone], eye_initial[eye_bone].angle_to(skeleton.get_bone_pose_rotation(eye_bone)))
                blink_min = minf(blink_min, face.get_blend_shape_value(blink))
                blink_max = maxf(blink_max, face.get_blend_shape_value(blink))
            row.merge({"idle": idle_name, "idle_seconds": idle.length, "head_motion_rad": head_motion,
                "neck_motion_rad": neck_motion, "blink_min": blink_min, "blink_max": blink_max})
            require(head_motion > .001, identity + ": idle never moves the Head bone")
            require(head_motion < .25 and neck_motion < .25, identity + ": excessive idle head/neck travel")
            require(blink_min < .1 and blink_max > .5, identity + ": idle never completes a blink")
            if identity == "daoguang":
                row["eye_motion_rad"] = {}
                for eye_bone in eye_motion:
                    row["eye_motion_rad"][skeleton.get_bone_name(eye_bone)] = eye_motion[eye_bone]
                    require(eye_motion[eye_bone] > .002 and eye_motion[eye_bone] < .06,
                        identity + ": eye gaze must move subtly and independently of the head")
    camera.position = Vector3(0, (minimum.y + maximum.y) * .5, maxf(2.0, size.y * 1.9))
    camera.look_at(Vector3(0, (minimum.y + maximum.y) * .5, 0))
    await process_frame
    await process_frame
    row["loaded_counters"] = counters()
    mesh_nodes.clear()
    pending.clear()
    skeleton = null
    animation = null
    face = null
    model.free()
    packed = null
    await process_frame
    await process_frame
    row["released_counters"] = counters()
    require(row["released_counters"]["nodes"] == baseline_nodes, identity + ": model nodes remain after release")
    require(row["released_counters"]["orphan_nodes"] == 0, identity + ": orphan nodes remain after release")
    row["status"] = "pass" if failures.size() == row["failures_before"] else "fail"
    row.erase("failures_before")
    results.append(row)
    print("LEADER_QA_", row["status"].to_upper(), " ", identity, " meshes=", row["mesh_nodes"], " triangles=", triangles, " height=", snappedf(size.y, .001))

func run():
    render_metrics_available = DisplayServer.get_name() != "headless"
    camera = Camera3D.new()
    camera.current = true
    camera.fov = 35
    root.add_child(camera)
    var light = DirectionalLight3D.new()
    light.rotation_degrees = Vector3(-35, -25, 0)
    light.light_energy = 1.1
    root.add_child(light)
    var baseline = counters()
    baseline_nodes = baseline["nodes"]
    for identity in IDENTITIES:
        await test_model(identity)
    var report = {"schema": 1, "expected_models": IDENTITIES.size(), "tested_models": results.size(),
        "passed": failures.is_empty(), "renderer": RenderingServer.get_current_rendering_method(),
        "render_memory_available": render_metrics_available,
        "memory_note": "Native renderer counters describe this process; headless counters are unavailable, not zero. They are not an isolated VRAM allocation benchmark.",
        "baseline": baseline, "final": counters(), "models": results, "failures": failures}
    var output = FileAccess.open(report_path, FileAccess.WRITE)
    if output == null:
        require(false, "Cannot write report: " + report_path)
    else:
        output.store_string(JSON.stringify(report, "  "))
        output.close()
    print("LEADER_QA_COMPLETE expected=", IDENTITIES.size(), " failures=", failures.size(), " report=", report_path)
    quit(0 if failures.is_empty() else 1)
