#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record TopicDetailDto(int Id, string? SystemName, string? Title, string? Intro, string? BodyHtml,
    string? MetaTitle, string? MetaDescription);
