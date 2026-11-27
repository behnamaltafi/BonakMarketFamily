using Microsoft.Extensions.Configuration;
using BonakMarket.ERP.Services;

namespace BonakMarket.ERP.Infrastructure;

// Explicit deployment allowlist, not an admin-editable list. Private ERP hosts
// are supported when deliberately allowed; network egress ACLs remain necessary.
public sealed class ErpEndpointPolicy(IConfiguration configuration)
{
    public Uri ValidateBase(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ErpIntegrationException("invalid_https_base_url");
        var allowed = configuration.GetSection("BonakMarket:ERP:AllowedAuthorities").GetChildren()
            .Any(x => string.Equals(x.Value, uri.Authority, StringComparison.OrdinalIgnoreCase));
        if (!allowed) throw new ErpIntegrationException("erp_destination_not_allowed");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public Uri Relative(Uri root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\\') || path.Contains('?') || path.Contains('#') || path.Contains("..", StringComparison.Ordinal) ||
            path.Contains('%') || path.Any(char.IsControl) || Uri.TryCreate(path.TrimStart('/'), UriKind.Absolute, out _))
            throw new ErpIntegrationException("invalid_relative_path");
        return SameOrigin(root, new Uri(root, path.TrimStart('/')));
    }

    public Uri SameOrigin(Uri root, Uri uri)
    {
        if (uri.Scheme != root.Scheme || uri.IdnHost != root.IdnHost || uri.Port != root.Port ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            !uri.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal))
            throw new ErpIntegrationException("unexpected_destination");
        return uri;
    }
}
