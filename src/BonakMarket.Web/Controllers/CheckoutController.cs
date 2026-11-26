using BonakMarket.Core.Identity;
﻿using BonakMarket.ComponentModel;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Localization.Routing;
using BonakMarket.Core.Seo.Routing;
using BonakMarket.Core.Stores;
using BonakMarket.Web.Models.Checkout;
using BonakMarket.Web.Models.Common;

namespace BonakMarket.Web.Controllers;

public class CheckoutController : PublicController
{
    const string ErrorMessageKey = "CheckoutErrorMessage";

    private readonly SmartDbContext _db;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;
    private readonly ICheckoutWorkflow _checkoutWorkflow;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly ICheckoutStateAccessor _checkoutStateAccessor;
    private readonly OrderSettings _orderSettings;
    private readonly ShoppingCartSettings _shoppingCartSettings;

    public CheckoutController(
        SmartDbContext db,
        IStoreContext storeContext,
        IWorkContext workContext,
        ICheckoutWorkflow checkoutWorkflow,
        IShoppingCartService shoppingCartService,
        ICheckoutStateAccessor checkoutStateAccessor,
        OrderSettings orderSettings,
        ShoppingCartSettings shoppingCartSettings)
    {
        _db = db;
        _storeContext = storeContext;
        _workContext = workContext;
        _checkoutWorkflow = checkoutWorkflow;
        _shoppingCartService = shoppingCartService;
        _checkoutStateAccessor = checkoutStateAccessor;
        _orderSettings = orderSettings;
        _shoppingCartSettings = shoppingCartSettings;
    }

    [DisallowRobot]
    [LocalizedRoute("/checkout", Name = "Checkout")]
    public async Task<IActionResult> Index()
    {
        var result = await _checkoutWorkflow.StartAsync(await CreateCheckoutContext());

        return result.ActionResult ?? RedirectToRoute("ShoppingCart");
    }

    public async Task<IActionResult> BillingAddress()
    {
        var context = await CreateCheckoutContext();
        var result = await _checkoutWorkflow.ProcessAsync(context);
        if (result.ActionResult != null)
        {
            return result.ActionResult;
        }

        var model = await context.MapAddressesAsync(false);

        return View(result.ViewPath, model);
    }

    [HttpPost]
    public async Task<IActionResult> SelectBillingAddress(int addressId)
    {
        var result = await _checkoutWorkflow.AdvanceAsync(await CreateCheckoutContext(addressId));

        return result.ActionResult ?? RedirectToAction(nameof(BillingAddress));
    }

    [HttpPost, ActionName(CheckoutActionNames.BillingAddress)]
    [FormValueRequired("nextstep")]
    public async Task<IActionResult> NewBillingAddress(CheckoutAddressModel model)
    {
        var context = await CreateCheckoutContext();
        var result = await AddAddress(model, context, false);

        if (result?.ActionResult != null)
        {
            return result.ActionResult;
        }

        model = await context.MapAddressesAsync(false);

        return View(model);
    }

    [HttpPost, ActionName(CheckoutActionNames.ShippingAddress)]
    [FormValueRequired("nextstep")]
    public async Task<IActionResult> NewShippingAddress(CheckoutAddressModel model)
    {
        var context = await CreateCheckoutContext();
        var result = await AddAddress(model, context, true);

        if (result?.ActionResult != null)
        {
            return result.ActionResult;
        }

        model = await context.MapAddressesAsync(true);

        return View(model);
    }

    private async Task<CheckoutResult> AddAddress(CheckoutAddressModel model, CheckoutContext context, bool isShippingAddress)
    {
        var cart = context.Cart;
        var customer = cart.Customer;
        var ga = customer.GenericAttributes;

        if (!cart.HasItems)
        {
            return new(RedirectToRoute("ShoppingCart"));
        }

        if (!customer.IsRegistered())
        {
            return new(ChallengeOrForbid());
        }
        if (await CustomerGuildPolicy.RequiresGuildAsync(_db, customer))
            return new(RedirectToAction("Profile", "SmsLogin", new { returnUrl = Url.RouteUrl("Checkout") }));

        var ordinary = !CustomerGuildPolicy.IsExempt(customer);
        var firstAddress = !customer.Addresses.Any();
        if (ordinary)
        {
            var iran = await _db.Countries.Where(x => x.Published && x.TwoLetterIsoCode == "IR")
                .ApplyStandardFilter(false, _storeContext.CurrentStore.Id).SingleOrDefaultAsync();
            if (iran == null || (isShippingAddress ? !iran.AllowsShipping : !iran.AllowsBilling))
                ModelState.AddModelError("NewAddress.CountryId", "Iran is not configured for checkout.");
            else
            {
                if (model.NewAddress.CountryId.HasValue && model.NewAddress.CountryId != iran.Id)
                    ModelState.AddModelError("NewAddress.CountryId", "Only Iranian addresses are accepted.");
                model.NewAddress.CountryId = iran.Id;
                if (model.NewAddress.StateProvinceId > 0 && !await _db.StateProvinces.AnyAsync(x =>
                    x.Id == model.NewAddress.StateProvinceId && x.CountryId == iran.Id && x.Published))
                    ModelState.AddModelError("NewAddress.StateProvinceId", "Invalid province.");
            }
            model.NewAddress.Email = customer.Email;
        }

        if (ModelState.IsValid)
        {
            var address = await MapperFactory.MapAsync<AddressModel, Address>(model.NewAddress);
            customer.Addresses.Add(address);

            // Save to avoid duplicate addresses.
            await _db.SaveChangesAsync();
            if (ordinary && firstAddress && address.Country?.AllowsBilling == true && address.Country.AllowsShipping)
            {
                ga.DefaultBillingAddressId = address.Id;
                ga.DefaultShippingAddressId = address.Id;
            }

            if (isShippingAddress)
            {
                customer.ShippingAddress = address;
                if (_shoppingCartSettings.QuickCheckoutEnabled)
                {
                    ga.DefaultShippingAddressId = customer.ShippingAddress.Id;
                }
            }
            else
            {
                customer.BillingAddress = address;
                customer.ShippingAddress = model.ShippingAddressDiffers || !cart.IsShippingRequired ? null : address;

                var state = _checkoutStateAccessor.CheckoutState;
                state.CustomProperties["SkipShippingAddress"] = !model.ShippingAddressDiffers;
                state.CustomProperties["ShippingAddressDiffers"] = model.ShippingAddressDiffers;

                if (_shoppingCartSettings.QuickCheckoutEnabled)
                {
                    ga.DefaultBillingAddressId = customer.BillingAddress.Id;
                    if (customer.ShippingAddress != null)
                    {
                        ga.DefaultShippingAddressId = customer.ShippingAddress.Id;
                    }
                }
            }

            await _db.SaveChangesAsync();

            var result = await _checkoutWorkflow.AdvanceAsync(context);
            result.ActionResult ??= RedirectToAction(isShippingAddress ? nameof(ShippingMethod) : nameof(ShippingAddress));

            return result;
        }

        return null;
    }

    public async Task<IActionResult> ShippingAddress()
    {
        var context = await CreateCheckoutContext();
        var result = await _checkoutWorkflow.ProcessAsync(context);
        if (result.ActionResult != null)
        {
            return result.ActionResult;
        }

        var model = await context.MapAddressesAsync(true);

        return View(result.ViewPath, model);
    }

    [HttpPost]
    public async Task<IActionResult> SelectShippingAddress(int addressId)
    {
        var result = await _checkoutWorkflow.AdvanceAsync(await CreateCheckoutContext(addressId));

        return result.ActionResult ?? RedirectToAction(nameof(ShippingAddress));
    }

    public async Task<IActionResult> ShippingMethod()
    {
        var context = await CreateCheckoutContext();
        var result = await _checkoutWorkflow.ProcessAsync(context);
        if (result.ActionResult != null)
        {
            return result.ActionResult;
        }

        var model = await MapperFactory.MapAsync<CheckoutContext, CheckoutShippingMethodModel>(context);

        result.Errors.Each(x => model.Warnings.Add(x.ErrorMessage));

        return View(result.ViewPath, model);
    }

    [HttpPost, ActionName(CheckoutActionNames.ShippingMethod)]
    [FormValueRequired("nextstep")]
    public async Task<IActionResult> SelectShippingMethod(string shippingOption)
    {
        var result = await _checkoutWorkflow.AdvanceAsync(await CreateCheckoutContext(shippingOption));

        result.Errors.Take(3).Each(x => NotifyError(x.ErrorMessage));

        return result.ActionResult ?? RedirectToAction(nameof(ShippingMethod));
    }

    public async Task<IActionResult> PaymentMethod()
    {
        var context = await CreateCheckoutContext();
        var result = await _checkoutWorkflow.ProcessAsync(context);
        if (result.ActionResult != null)
        {
            return result.ActionResult;
        }

        var model = await MapperFactory.MapAsync<CheckoutContext, CheckoutPaymentMethodModel>(context);

        if (TempData.TryGetValueAs<string>(ErrorMessageKey, out var msg) && msg.HasValue())
        {
            NotifyError(msg);
        }

        return View(result.ViewPath, model);
    }

    [HttpPost, ActionName(CheckoutActionNames.PaymentMethod)]
    [FormValueRequired("nextstep")]
    public async Task<IActionResult> SelectPaymentMethod(string paymentMethod)
    {
        var result = await _checkoutWorkflow.AdvanceAsync(await CreateCheckoutContext(paymentMethod));

        result.Errors.Each(x => ModelState.AddModelError(x.PropertyName, x.ErrorMessage));

        if (!ModelState.IsValid)
        {
            return await PaymentMethod();
        }

        return result.ActionResult ?? RedirectToAction(nameof(PaymentMethod));
    }

    /// <summary>
    /// AJAX. Refreshes a part of the current checkout page.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Refresh(CheckoutRefreshModel model)
    {
        var success = false;
        var contents = new Dictionary<int, string>();

        try
        {
            var cart = await _shoppingCartService.GetCartAsync(storeId: _storeContext.CurrentStore.Id);
            var context = new CheckoutContext(cart, HttpContext, Url)
            {
                Model = GetContextModel(),
                Parts = model.Parts,
                RouteValues = new(new
                {
                    action = model.ActionName,
                    controller = "Checkout",
                    area = string.Empty
                })
            };

            var result = await _checkoutWorkflow.RefreshAsync(context);
            
            success = result.Success;
            result.Errors.Take(3).Each(x => NotifyError(x.ErrorMessage));

            if (result.ActionResult != null)
            {
                return result.ActionResult;
            }

            foreach (var widget in result.Widgets)
            {
                var widgetContent = await widget.Value.InvokeAsync(new WidgetContext(ControllerContext));
                contents[(int)widget.Key] = widgetContent.ToHtmlString().ToString();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
            NotifyError(ex.Message);
        }

        return Json(new 
        {
            success,
            contents
        });

        string GetContextModel()
        {
            if (model.ActionName.EqualsNoCase(CheckoutActionNames.PaymentMethod))
            {
                return model.PaymentMethodSystemName;
            }

            if (model.ActionName.EqualsNoCase(CheckoutActionNames.ShippingMethod))
            {
                return model.ShippingOption;
            }

            return null;
        }
    }

    /// <summary>
    /// AJAX. Starts the payment confirmation process. The URL of the third-party payment page is obtained 
    /// from the payment provider and is used to confirm and fulfil the payment. Redirection is performed on the client side.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ConfirmPayment()
    {
        var result = await _checkoutWorkflow.ConfirmPaymentAsync(await CreateCheckoutContext());
        var redirectUrl = GetUrl(result.ActionResult);
        var errors = result.Errors.Select(e => e.ErrorMessage).ToList();

        if (redirectUrl != null && errors.Count > 0)
        {
            // INFO: Display error after client-side redirection.
            TempData[ErrorMessageKey] = string.Join(' ', errors);
        }

        // Success: If redirectUrl is provided, redirect to a third-party payment page to confirm the payment. Otherwise, place the order.
        // Failure: Stay on confirmation page and display error messages, except a redirectUrl is provided.
        // Exception: Redirect to payment selection page and display notification (default case of a payment error).
        return Json(new
        {
            success = result.Success,
            redirectUrl,
            messages = errors
        });
    }

    /// <summary>
    /// After completing the payment, the payment provider redirects the customer to this action method.
    /// </summary>
    /// <remarks>
    /// We have been redirected to third-party payment page via browser (JavaScript "window.location").
    /// Cookies are thereby preserved. The customer and the checkout state object are the same as before the redirection.
    /// Without cookies we would get a new guest customer and an empty checkout state object here. In this case, CheckoutState could not be used.
    /// We would have to either cache state obejct for x minutes or store it in the database.
    /// </remarks>
    [SaveChanges<SmartDbContext>(false)]
    public async Task<IActionResult> PaymentCompleted()
    {
        var result = await _checkoutWorkflow.CompletePaymentAsync(await CreateCheckoutContext());

        return result.ActionResult ?? RedirectToAction(nameof(Completed));
    }

    public async Task<IActionResult> Confirm()
    {
        var context = await CreateCheckoutContext();
        var result = await _checkoutWorkflow.ProcessAsync(context);
        if (result.ActionResult != null)
        {
            return result.ActionResult;
        }

        var model = await MapperFactory.MapAsync<CheckoutContext, CheckoutConfirmModel>(context);

        return View(result.ViewPath, model);
    }

    [HttpPost, ActionName(CheckoutActionNames.Confirm)]
    [SaveChanges<SmartDbContext>(false)]
    public async Task<IActionResult> ConfirmOrder()
    {
        var result = await _checkoutWorkflow.CompleteAsync(await CreateCheckoutContext());

        if (result.Errors.Length > 0)
        {
            var context = await CreateCheckoutContext();
            var model = await MapperFactory.MapAsync<CheckoutContext, CheckoutConfirmModel>(context);

            result.Errors.Each(x => model.Warnings.Add(x.ErrorMessage));

            return View(model);
        }

        return result.ActionResult ?? RedirectToAction(nameof(Confirm));
    }

    public async Task<IActionResult> Completed()
    {
        var store = _storeContext.CurrentStore;
        var customer = _workContext.CurrentCustomer;

        if (!_orderSettings.AnonymousCheckoutAllowed && !_workContext.CurrentCustomer.IsRegistered())
        {
            return ChallengeOrForbid();
        }

        var order = await _db.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .ApplyStandardFilter(customer.Id, store.Id)
            .FirstOrDefaultAsync();

        if (order == null || customer.Id != order.CustomerId)
        {
            return NotFound();
        }

        if (_orderSettings.DisableOrderCompletedPage)
        {
            return RedirectToAction(nameof(OrderController.Details), "Order", new { id = order.Id });
        }

        return View(new CheckoutCompletedModel
        {
            OrderId = order.Id,
            OrderNumber = order.GetOrderNumber(),
            Order = order
        });
    }

    private async Task<CheckoutContext> CreateCheckoutContext(object model = null)
    {
        var cart = await _shoppingCartService.GetCartAsync(storeId: _storeContext.CurrentStore.Id);

        return new CheckoutContext(cart, HttpContext, Url)
        {
            Model = model
        };
    }

    private string GetUrl(IActionResult result)
    {
        var url = result switch
        {
            RedirectToRouteResult route => Url.RouteUrl(route.RouteName, route.RouteValues),
            RedirectToActionResult action => Url.Action(action.ActionName, action.ControllerName, action.RouteValues),
            RedirectResult direct => direct.Url,
            _ => null
        };

        return url.NullEmpty();
    }
}
