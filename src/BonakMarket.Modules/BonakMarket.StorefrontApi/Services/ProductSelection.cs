using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Services;

/// <summary>Shared JSON selection boundary for the cart and read-only product quote.</summary>
internal static class ProductSelection
{
    internal static bool SupportsControl(AttributeControlType type) => type is AttributeControlType.DropdownList
        or AttributeControlType.RadioList or AttributeControlType.Boxes or AttributeControlType.Checkboxes;

    internal static bool SupportsProduct(Product product) =>
        !product.CustomerEntersPrice && !product.IsGiftCard && !product.CallForPrice && !product.DisableBuyButton &&
        product.ProductType == ProductType.SimpleProduct;

    internal static string UnsupportedCode(Product product)
    {
        if (!SupportsProduct(product))
            return "product_requires_configuration";
        if (product.ProductVariantAttributes.Any(x => x.IsRequired && !SupportsControl(x.AttributeControlType)))
            return "unsupported_required_attribute";
        return null;
    }

    internal static async Task<ProductSelectionResult> CreateAsync(Product product, List<VariantChoice> choices,
        IProductAttributeMaterializer attributes)
    {
        var unsupported = UnsupportedCode(product);
        if (unsupported != null) return new(409, unsupported);
        choices ??= [];
        if (choices.Count > StorefrontLimits.MaxVariantChoices || choices.Any(x => x == null) ||
            choices.Select(x => (x.VariantAttributeId, x.ValueId)).Distinct().Count() != choices.Count)
            return new(400, "invalid_variants");

        var query = new ProductVariantQuery();
        foreach (var group in choices.GroupBy(x => x.VariantAttributeId))
        {
            var attribute = product.ProductVariantAttributes.FirstOrDefault(x => x.Id == group.Key);
            if (attribute == null || !SupportsControl(attribute.AttributeControlType))
                return new(400, "invalid_variant_attribute");
            if (attribute.AttributeControlType != AttributeControlType.Checkboxes && group.Count() > 1)
                return new(400, "multiple_variant_values");
            foreach (var choice in group)
            {
                if (!attribute.ProductVariantAttributeValues.Any(x => x.Id == choice.ValueId))
                    return new(400, "invalid_variant_value");
                query.AddVariant(new ProductVariantQueryItem
                {
                    ProductId = product.Id, AttributeId = attribute.ProductAttributeId,
                    VariantAttributeId = attribute.Id,
                    Value = choice.ValueId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }
        }
        // Only numeric list values from this product are accepted. Never accept raw attributes or file GUIDs.
        var (selection, warnings) = await attributes.CreateAttributeSelectionAsync(query,
            product.ProductVariantAttributes, product.Id, 0, getFilesFromRequest: false);
        return warnings.Count > 0 ? new(422, "invalid_variants", Warnings: warnings.ToList()) : new(200, Selection: selection);
    }
}
