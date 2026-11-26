using BonakMarket.Core.Catalog.Brands;
using BonakMarket.Core.Catalog.Categories;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.Core.Seo;
using BonakMarket.Core.Stores;
using BonakMarket.Domain;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Cart;

namespace BonakMarket.StorefrontApi.Security;

internal static class StorefrontScope
{
    private static IQueryable<T> Visible<T>(IQueryable<T> query, SmartDbContext db, Customer customer, int store)
        where T : BaseEntity, IAclRestricted, IStoreRestricted, new()
    {
        var roles = customer.CustomerRoleMappings.Where(x => x.CustomerRole != null && x.CustomerRole.Active)
            .Select(x => x.CustomerRoleId).ToArray();
        var name = NamedEntity.GetEntityName<T>();
        var acl = db.AclRecords.Where(x => x.EntityName == name && roles.Contains(x.CustomerRoleId)).Select(x => x.EntityId);
        var stores = db.StoreMappings.Where(x => x.EntityName == name && x.StoreId == store).Select(x => x.EntityId);
        return query.Where(x => (!x.SubjectToAcl || acl.Contains(x.Id)) && (!x.LimitedToStores || stores.Contains(x.Id)));
    }

    public static IQueryable<Product> Products(SmartDbContext db, Customer customer, int store, bool forSearch = false, bool forDetail = false)
    {
        var now = DateTime.UtcNow;
        return Visible(db.Set<Product>(), db, customer, store).ApplyStandardFilter()
            .Where(x => !x.Deleted && (x.Visibility == ProductVisibility.Full || ((forSearch || forDetail) && x.Visibility == ProductVisibility.SearchResults) || (forDetail && x.Visibility == ProductVisibility.ProductPage)) &&
                (x.AvailableStartDateTimeUtc == null || x.AvailableStartDateTimeUtc <= now) &&
                (x.AvailableEndDateTimeUtc == null || x.AvailableEndDateTimeUtc >= now));
    }
    public static IQueryable<Category> Categories(SmartDbContext db, Customer customer, int store) =>
        Visible(db.Set<Category>(), db, customer, store).Where(x => x.Published && !x.Deleted);
    public static IQueryable<Manufacturer> Brands(SmartDbContext db, Customer customer, int store) =>
        Visible(db.Set<Manufacturer>(), db, customer, store).Where(x => x.Published && !x.Deleted);
    public static IQueryable<BonakMarket.Core.Content.Topics.Topic> Topics(SmartDbContext db, Customer customer, int store) =>
        Visible(db.Topics, db, customer, store).Where(x => x.IsPublished && !x.IsPasswordProtected && !x.RenderAsWidget);

    public static IQueryable<Order> Orders(SmartDbContext db, Customer customer, int store) =>
        db.Set<Order>().Where(x => !x.Deleted).ApplyStandardFilter(customer.Id, store);
    public static IQueryable<ShoppingCartItem> Cart(SmartDbContext db, Customer customer, int store, ShoppingCartType type = ShoppingCartType.ShoppingCart) =>
        db.Set<ShoppingCartItem>().Where(x => x.CustomerId == customer.Id && x.StoreId == store &&
            x.ShoppingCartTypeId == (int)type);
}
