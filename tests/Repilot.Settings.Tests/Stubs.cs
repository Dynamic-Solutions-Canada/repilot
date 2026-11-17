// Stand-ins for the kit's theme and window types the settings class uses. Same-named types in the same
// namespaces let the production files compile here unchanged, which is why the namespaces below don't
// match this folder (IDE0130 is off for this file).
#pragma warning disable IDE0130

namespace WinUIAppKit.Theming
{
    /// <summary>The kit's theme choice, with the kit's numbers (they are what the file stores).</summary>
    public enum AppThemeChoice
    {
        System = 0,
        Light = 1,
        Dark = 2,
    }
}

namespace WinUIAppKit.Windowing
{
    /// <summary>The kit's saved window position, with the kit's property names.</summary>
    public sealed class WindowPlacement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}

#pragma warning restore IDE0130
