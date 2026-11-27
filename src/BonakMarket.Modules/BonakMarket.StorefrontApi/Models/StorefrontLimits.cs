namespace BonakMarket.StorefrontApi.Models;

// Stable API limits shared by DTO validation, query policies and defensive service checks.
internal static class StorefrontLimits
{
    internal const int MaxQuantity = 1000;
    internal const int MaxVariantChoices = 30;
    internal const int MaxSearchLength = 200;
    internal const int MaxSuggestions = 16;
    internal const int MaxPage = 10000;
    internal const int MaxPageSize = 100;
}
