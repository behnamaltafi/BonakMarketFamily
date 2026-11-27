namespace BonakMarket.StorefrontApi.Services;

public sealed record CartChange(int Status, string Code = null, List<string> Warnings = null);
