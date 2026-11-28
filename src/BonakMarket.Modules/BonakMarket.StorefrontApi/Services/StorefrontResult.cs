#nullable enable
namespace BonakMarket.StorefrontApi.Services;

/// <summary>Application operation outcome. Controllers translate failures to their existing HTTP contract.</summary>
public sealed record StorefrontResult<T>(T? Value, StorefrontFailure? Failure = null)
{
    public static implicit operator StorefrontResult<T>(StorefrontFailure failure) => new(default, failure);
}
