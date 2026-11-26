using BonakMarket.Core.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
﻿using System.Dynamic;
using BonakMarket.ComponentModel;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Stores;
using BonakMarket.Web.Models.Common;
using BonakMarket.Web.Rendering;

namespace BonakMarket.Web.Models.Checkout;

public static partial class CheckoutAddressMappingExtensions
{
    public static async Task<CheckoutAddressModel> MapAddressesAsync(this CheckoutContext context,
        bool shipping,
        int? selectedCountryId = null)
    {
        var model = new CheckoutAddressModel();
        await context.MapAddressesAsync(model, shipping, selectedCountryId);

        return model;
    }

    public static async Task MapAddressesAsync(this CheckoutContext context,
        CheckoutAddressModel model,
        bool shipping,
        int? selectedCountryId)
    {
        dynamic parameters = new ExpandoObject();
        parameters.SelectedCountryId = selectedCountryId;
        parameters.Shipping = shipping;

        await MapperFactory.MapAsync(context, model, parameters);
    }
}

public class CheckoutAddressMapper : Mapper<CheckoutContext, CheckoutAddressModel>
{
    private readonly SmartDbContext _db;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly ICheckoutFactory _checkoutFactory;

    public CheckoutAddressMapper(
        SmartDbContext db,
        IStoreContext storeContext,
        IWorkContext workContext,
        IShoppingCartService shoppingCartService,
        ICheckoutFactory checkoutFactory)
    {
        _db = db;
        _storeContext = storeContext;
        _workContext = workContext;
        _shoppingCartService = shoppingCartService;
        _checkoutFactory = checkoutFactory;
    }

    public Localizer T { get; set; } = NullLocalizer.Instance;

    protected override void Map(CheckoutContext from, CheckoutAddressModel to, dynamic parameters = null)
        => throw new NotImplementedException();

    public override async Task MapAsync(CheckoutContext from, CheckoutAddressModel to, dynamic parameters = null)
    {
        Guard.NotNull(to);

        var shipping = parameters?.Shipping == true;
        var selectedCountryId = parameters?.SelectedCountryId as int?;
        var cart = from.Cart;
        var ordinary = cart.Customer.IsRegistered() && !CustomerGuildPolicy.IsExempt(cart.Customer);
        var addresses = cart.Customer.Addresses.Where(x => x.Country == null || (shipping ? x.Country.AllowsShipping : x.Country.AllowsBilling));
        if (ordinary) addresses = addresses.Where(x => x.Country?.TwoLetterIsoCode == "IR" && x.Country.Published);

        foreach (var address in addresses)
        {
            to.ExistingAddresses.Add(await address.MapAsync(cart.Customer));
        }

        to.ActionName = shipping ? CheckoutActionNames.ShippingAddress : CheckoutActionNames.BillingAddress;
        to.PreviousStepUrl = _checkoutFactory.GetNextCheckoutStepUrl(from, false);
        to.IsShippingRequired = cart.IsShippingRequired;

        // New address.
        await new Address().MapAsync(to.NewAddress, cart.Customer);

        to.NewAddress.CountryId = selectedCountryId;
        to.NewAddress.Email = _workContext.CurrentCustomer.Email;

        if (ordinary)
        {
            var iran = await _db.Countries.AsNoTracking().Where(x => x.Published && x.TwoLetterIsoCode == "IR")
                .ApplyStandardFilter(false, _storeContext.CurrentStore.Id).SingleOrDefaultAsync();
            to.NewAddress.CountryFixed = true;
            to.NewAddress.CountryId = iran?.Id;
            to.NewAddress.CountryName = iran?.Name ?? "Iran";
            if (iran != null)
            {
                var provinces = await _db.StateProvinces.AsNoTracking().Where(x => x.CountryId == iran.Id && x.Published)
                    .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync();
                to.NewAddress.AvailableStates = provinces.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
                if (!to.NewAddress.StateProvinceRequired)
                    to.NewAddress.AvailableStates.Insert(0, new SelectListItem(T("Common.PleaseSelect").Value, "0"));
            }
            to.NewAddress.FirstName ??= cart.Customer.FirstName;
            to.NewAddress.LastName ??= cart.Customer.LastName;
            to.NewAddress.PhoneNumber ??= cart.Customer.GenericAttributes.Phone;
        }
        else if (to.NewAddress.CountryEnabled)
        {
            var countriesQuery = _db.Countries.AsNoTracking();

            countriesQuery = shipping
                ? countriesQuery.Where(x => x.AllowsShipping)
                : countriesQuery.Where(x => x.AllowsBilling);

            var countries = await countriesQuery
                .ApplyStandardFilter(false, _storeContext.CurrentStore.Id)
                .ToListAsync();

            to.NewAddress.AvailableCountries = countries.ToSelectListItems(selectedCountryId ?? 0);
        }
    }
}