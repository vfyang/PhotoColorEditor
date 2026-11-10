using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace PhotoColorEditor;

public sealed class PhotoPreviewSurface : Grid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new SurfacePeer(this);
}

public sealed class CurveEditorSurface : Canvas
{
    protected override AutomationPeer OnCreateAutomationPeer() => new SurfacePeer(this);
}

internal sealed class SurfacePeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => Owner.GetType().Name;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
    protected override bool IsControlElementCore() => true;
    protected override bool IsContentElementCore() => true;
}
