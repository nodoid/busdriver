using BusDriver.Game.Core;
using Foundation;
using UIKit;

namespace BusDriver.iOS;

/// <summary>
/// Transparent view that sits over the MonoGame surface and reports touches
/// itself.  MonoGame's iOS TouchPanel hands the game positions that do not
/// match the drawn frame on a scene-based launch, so the raw UITouch locations
/// are normalised across this view and pushed to the shared game instead.
/// </summary>
public sealed class TouchOverlay : UIView
{
    public TouchOverlay(CoreGraphics.CGRect frame) : base(frame)
    {
        BackgroundColor = UIColor.Clear;
        UserInteractionEnabled = true;
        MultipleTouchEnabled = true;
    }

    public override void TouchesBegan(NSSet touches, UIEvent? evt) =>
        Publish(touches, true);

    public override void TouchesMoved(NSSet touches, UIEvent? evt) =>
        Publish(touches, true);

    public override void TouchesEnded(NSSet touches, UIEvent? evt) =>
        Publish(touches, false);

    public override void TouchesCancelled(NSSet touches, UIEvent? evt) =>
        Publish(touches, false);

    private void Publish(NSSet touches, bool down)
    {
        var width = (float)Bounds.Width;
        var height = (float)Bounds.Height;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        foreach (var item in touches)
        {
            if (item is not UITouch touch)
            {
                continue;
            }

            var point = touch.LocationInView(this);
            var id = touch.Handle.GetHashCode();
            PlatformPointer.Push(
                id,
                (float)point.X / width,
                (float)point.Y / height,
                down);
        }
    }
}
