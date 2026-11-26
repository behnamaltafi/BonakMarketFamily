using System.Security.Cryptography;
using System.Text.Json;

namespace BonakMarket.StorefrontApi.Services;

/// <summary>Deterministic change detector, not a lock or a reservation.</summary>
public static class CartSnapshotRevision
{
    public static string Create<T>(T state) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state))).ToLowerInvariant();
}
