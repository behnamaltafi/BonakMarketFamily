#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed record InstantSearchDto(string Search, bool Enabled, int MinimumTermLength, int Limit,
    bool HasMore, IReadOnlyList<InstantSearchProductDto> Items);
