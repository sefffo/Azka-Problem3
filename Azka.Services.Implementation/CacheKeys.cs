namespace Azka.Services.Implementation;

/// <summary>
/// Central registry of all IMemoryCache keys used across services.
/// </summary>
public static class CacheKeys
{
    // ── Dashboard ────────────────────────────────────────────────────────────
    public const string Dashboard = "dashboard_summary";

    // ── Engineers ────────────────────────────────────────────────────────────
    /// <summary>Prefix for paged engineer list entries. Full key = prefix + query fingerprint.</summary>
    public const string EngineerListPrefix = "engineers_list_";

    // ── Assets ───────────────────────────────────────────────────────────────
    /// <summary>Prefix for paged asset list entries. Full key = prefix + query fingerprint.</summary>
    public const string AssetListPrefix = "assets_list_";
}
