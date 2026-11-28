#nullable enable
namespace BonakMarket.StorefrontApi.Services;

/// <summary>Application failure without MVC response dependencies.</summary>
public sealed record StorefrontFailure(int Status, string Code, IReadOnlyDictionary<string, object?>? Extensions = null);
