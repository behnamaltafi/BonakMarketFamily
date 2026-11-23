using BonakMarket.Core.Theming;
using BonakMarket.Web.Models.Common;

namespace BonakMarket.Web.Components;

public class StoreThemeSelectorViewComponent : SmartViewComponent
{
    private readonly IThemeRegistry _themeRegistry;
    private readonly ThemeSettings _themeSettings;
    private readonly IThemeContext _themeContext;

    public StoreThemeSelectorViewComponent(IThemeRegistry themeRegistry, ThemeSettings themeSettings, IThemeContext themeContext)
    {
        _themeRegistry = themeRegistry;
        _themeSettings = themeSettings;
        _themeContext = themeContext;
    }

    public IViewComponentResult Invoke()
    {
        if (!_themeSettings.AllowCustomerToSelectTheme)
            return Empty();

        var currentTheme = _themeRegistry.GetThemeDescriptor(_themeContext.WorkingThemeName);

        ViewBag.CurrentStoreTheme = new StoreThemeModel
        {
            Name = currentTheme.Name,
            Title = currentTheme.FriendlyName
        };

        ViewBag.AvailableStoreThemes = _themeRegistry
            .GetThemeDescriptors()
            .Select(x =>
            {
                return new StoreThemeModel
                {
                    Name = x.Name,
                    Title = x.FriendlyName
                };
            })
            .ToList();

        return View();
    }
}