using Microsoft.AspNetCore.Razor.TagHelpers;
using BonakMarket.Web.TagHelpers.Shared;

namespace BonakMarket.Web.TagHelpers;

public static class TagHelperContextExtensions
{
    public static bool ShouldSuppressChildContent(this TagHelperContext context)
        => context.Items.TryGetValue(IfTagHelper.SuppressChildContentKey, out var value)
            && value is true;
}
