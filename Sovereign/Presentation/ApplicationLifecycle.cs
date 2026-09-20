using Godot;
using System;

namespace Sovereign.Presentation;

public partial class Main
{
    private bool _quitting;

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) RequestQuit(0);
    }

    // Close the scene while the rendering server is still alive. A last-frame panel
    // rebuild can otherwise leave queued controls and .NET resource finalizers racing
    // Godot's renderer teardown. Shared loaded resources are never manually disposed.
    private async void RequestQuit(int exitCode)
    {
        if (_quitting) return;
        _quitting = true;
        var tree = GetTree();
        try
        {
            _speed = 0; ProcessMode = ProcessModeEnum.Disabled;
            SuspendEmbeddedPortrait();
            // First detach viewport-texture consumers in the CanvasLayer. Render
            // targets and their cameras remain alive until those controls are gone.
            foreach (var child in GetChildren()) if (child is CanvasLayer) child.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            foreach (var child in GetChildren()) child.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Release owners only after the tree has freed their nodes/viewports.
            _map = null!; _city = null!; _leader = null!;
            _portraitPreview = null; _portraitViewport = null;
            _ui = _modal = null!; _leftPanel = _rightPanel = null!;
            _outlinerPanel = _marketStrip = null!; _drawerHeader = _mapSource = null!;
            _left = _right = _outliner = null!; _leftScroll = _rightScroll = null!;
            _atlasHeading = _cityHeading = null!; _metrics = _goods = _districtButtons = null!;
            _date = _status = _toast = _placeTitle = _placeSubtitle = null!;
            _drawerTitle = _drawerSubtitle = _clockStatus = _constructionReadout = null!; _countrySeal = _drawerFlag = null!;
            _navReveal = _pauseBadge = _constructionStrip = _drawerFooter = null!;
            _atlasSelector = null; _chart = null!; _body = _serif = _numbers = null!;
            _nav.Clear(); _speedButtons.Clear(); _lenses.Clear(); _hudValues.Clear();
            _marketValues.Clear(); _hudColumns.Clear();
            _interfaceTextures.Clear(); _constructionPictures.Clear();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            tree.Quit(exitCode);
        }
        catch (Exception error)
        {
            GD.PushError("Scene shutdown failed: " + error);
            tree.Quit(1);
        }
    }
}
