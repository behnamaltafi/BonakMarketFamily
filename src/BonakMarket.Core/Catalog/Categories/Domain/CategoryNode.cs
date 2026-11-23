using System.Text.Json.Serialization;
using BonakMarket.Collections;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Security;
using BonakMarket.Core.Seo;
using BonakMarket.Core.Stores;
using BonakMarket.Json;

namespace BonakMarket.Core.Catalog.Categories;

[DefaultImplementation(typeof(CategoryNode))]
public interface ICategoryNode : ILocalizedEntity, ISlugSupported, IAclRestricted, IStoreRestricted
{
    new int Id { get; }
    int? ParentId { get; }
    string Name { get; }

    /// <summary>
    /// Gets the icon expression associated with the category.
    /// </summary>
    string Icon { get; }
    string ExternalLink { get; }
    string Alias { get; }
    int? MediaFileId { get; }
    bool Published { get; }
    int DisplayOrder { get; }
    DateTime UpdatedOnUtc { get; }
    string BadgeText { get; }
    string BadgeStyle { get; }
}

public class CategoryNode : ICategoryNode, IKeyedNode
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public string ExternalLink { get; set; }
    public string Alias { get; set; }
    public int? MediaFileId { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
    public string BadgeText { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Published { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DisplayOrder { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string BadgeStyle { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SubjectToAcl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool LimitedToStores { get; set; }

    /// <inheritdoc/>
    object IKeyedNode.GetNodeKey()
    {
        return Id;
    }

    /// <inheritdoc/>
    public string GetDisplayName()
    {
        return Name;
    }

    /// <inheritdoc/>
    public string[] GetDisplayNameMemberNames()
    {
        return new[] { nameof(Name) };
    }

    /// <inheritdoc/>
    public string GetEntityName()
    {
        return nameof(Category);
    }
}