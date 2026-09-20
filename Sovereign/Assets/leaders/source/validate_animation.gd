extends SceneTree

func _initialize():
    run.call_deferred()

func run():
    var count = 0
    for identity in ["daoguang", "william_iv", "victoria", "tokugawa_ienari", "tokugawa_ieyoshi"]:
        var packed = load("res://Assets/leaders/" + identity + ".glb")
        assert(packed != null)
        var model = packed.instantiate()
        root.add_child(model)
        await process_frame
        var skeleton = null
        var anim = null
        var face = null
        var nodes = [model]
        while nodes.size() > 0:
            var node = nodes.pop_back()
            if node is Skeleton3D: skeleton = node
            if node is AnimationPlayer: anim = node
            if node is MeshInstance3D and node.find_blend_shape_by_name("Speech") >= 0: face = node
            nodes.append_array(node.get_children())
        assert(skeleton != null and skeleton.get_bone_count() >= 6)
        assert(face != null and anim != null)
        var selected = ""
        for n in anim.get_animation_list():
            if "idle" in n.to_lower(): selected = n
        assert(selected != "")
        anim.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
        anim.play(selected)
        anim.advance(0)
        var head = skeleton.find_bone("Head")
        var pose_a = skeleton.get_bone_pose_rotation(head)
        var blink = face.find_blend_shape_by_name("Blink")
        var minimum = 1.0
        var maximum = 0.0
        var head_motion = 0.0
        for i in range(240):
            anim.advance(1.0 / 30.0)
            minimum = minf(minimum, face.get_blend_shape_value(blink))
            maximum = maxf(maximum, face.get_blend_shape_value(blink))
            head_motion = maxf(head_motion, pose_a.angle_to(skeleton.get_bone_pose_rotation(head)))
        assert(maximum > 0.5 and minimum < 0.1)
        assert(head_motion > 0.01)
        var speech = face.find_blend_shape_by_name("Speech")
        face.set_blend_shape_value(speech, 0.65)
        assert(absf(face.get_blend_shape_value(speech)-0.65)<0.001)
        print("SOVEREIGN_LEADER_ANIMATION_PASS identity=",identity," bones=",skeleton.get_bone_count()," blink=",minimum,"..",maximum," head_motion_rad=",head_motion," speech=",face.get_blend_shape_value(speech))
        root.remove_child(model)
        model.queue_free()
        await process_frame
        count += 1
    print("SOVEREIGN_AUTHORED_MODELS_PASS count=",count)
    quit()
