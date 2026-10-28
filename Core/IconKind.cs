namespace WebToAppStudio.Core;

public enum IconKind
{
    /// <summary>Square PNG with theme-coloured background (Android legacy launcher icon).</summary>
    Legacy,
    /// <summary>Transparent canvas, artwork in the safe zone (Android adaptive foreground).</summary>
    AdaptiveForeground,
    /// <summary>No alpha channel at all - required by the App Store for the 1024px icon.</summary>
    Opaque
}
