using WinUIAppKit.Updates;

namespace Repilot.Classes;

/// <summary>
/// The one place the repository is named (BRAND-5): the About page's source link and the update
/// check are both built from these values.
/// </summary>
internal static class AboutContent
{
    /// <summary>The repository's current owner (BRAND-10).</summary>
    public const string RepoOwner = "Dynamic-Solutions-Canada";

    /// <summary>The repository name.</summary>
    public const string RepoName = "repilot";

    /// <summary>The repository's GitHub page, shown as the About page's source link.</summary>
    public static Uri Repository { get; } = new($"https://github.com/{RepoOwner}/{RepoName}");

    /// <summary>
    /// "Check for updates" (DIST-6): the Store's updates page when packaged, this repo's GitHub Releases
    /// otherwise. It only checks when the user asks.
    /// </summary>
    public static UpdateService Updates { get; } = new(RepoOwner, RepoName);
}
