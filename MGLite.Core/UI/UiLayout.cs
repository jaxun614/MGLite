using System;
using Microsoft.Xna.Framework;
using MonoGameGum;

namespace MGLite.UI;

/// <summary>
/// Keeps Gum's scale and canvas in step with the window.
///
/// The menus are authored in a 270x180 "design space" (the old 1080x720 / 4). On resize we pick
/// the largest whole-number zoom that still fits that design space, set the Gum canvas to
/// window / zoom, and expose Offset, the top-left of the design space once it is centered in the
/// canvas. Whole-number zoom keeps pixel art crisp; the leftover space becomes margin.
/// </summary>
public static class UiLayout
{
    public const float DesignWidth = 270f;
    public const float DesignHeight = 180f;

    public static bool PixelPerfect { get; set; } = false;


    public static int Zoom { get; private set; } = 4;
    public static float CanvasWidth { get; private set; } = DesignWidth;
    public static float CanvasHeight { get; private set; } = DesignHeight;

    /// <summary>Top-left of the centered design area, in Gum canvas units.</summary>
    public static Vector2 Offset => new(
        MathF.Floor((CanvasWidth - DesignWidth) / 2f),
        MathF.Floor((CanvasHeight - DesignHeight) / 2f));

    /// <summary>Raised after the zoom/canvas change so screens can re-position their controls.</summary>
    public static event Action Changed;

    public static void Apply(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return;

        Zoom = Math.Max(1, (int)MathF.Floor(MathF.Min(pixelWidth / DesignWidth, pixelHeight / DesignHeight)));
        CanvasWidth = pixelWidth / (float)Zoom;
        CanvasHeight = pixelHeight / (float)Zoom;

        GumService.Default.Renderer.Camera.Zoom = Zoom;
        GumService.Default.CanvasWidth = CanvasWidth;
        GumService.Default.CanvasHeight = CanvasHeight;

        Changed?.Invoke();
    }
}
