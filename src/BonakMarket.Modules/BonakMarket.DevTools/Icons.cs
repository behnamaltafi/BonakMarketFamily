using BonakMarket.IconKit;

namespace BonakMarket.DevTools;

/// <summary>
/// Contributes icon concepts used by this module.
/// </summary>
internal sealed class Icons : IIconProvider
{
    /// <inheritdoc />
    public void Configure(IconKitBuilder builder)
    {
        builder.Kit("admin")
            .Add("devtools-server", "server")
            .Add("terminal", "terminal");
    }
}
