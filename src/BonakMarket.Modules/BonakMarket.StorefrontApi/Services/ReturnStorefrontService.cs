using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Data;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Messaging;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;



public sealed class ReturnStorefrontService(SmartDbContext db, StorefrontRequestContext request, IOrderProcessingService processing,
    OrderSettings settings, LocalizationSettings localization, IMessageFactory messages)
{
    private IQueryable<ReturnCase> Owned() => db.ReturnCases.Where(x => x.CustomerId == request.Customer.Id && x.StoreId == request.StoreId);
    private IQueryable<Order> Orders() => StorefrontScope.Orders(db, request.Customer, request.StoreId);
    private static IQueryable<ReturnCaseDto> Project(IQueryable<ReturnCase> query) => query.Select(x =>
        new ReturnCaseDto(x.Id, x.OrderItemId, x.Quantity, x.ReasonForReturn, x.RequestedAction,
            x.CustomerComments, x.ReturnCaseStatusId, x.CreatedOnUtc));

    public async Task<PageDto<ReturnCaseDto>> ListAsync(PageRequest page, CancellationToken ct) =>
        await Project(Owned().AsNoTracking().ApplyStandardFilter()).PageAsync(page, ct);

    public async Task<StorefrontResult<ReturnCaseDto>> GetAsync(int id, CancellationToken ct)
    {
        var result = await Project(Owned().AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(ct);
        return result == null ? new StorefrontResult<ReturnCaseDto>(null, new StorefrontFailure(404, "return_not_found")) : new(result);
    }

    private (string[] Reasons, string[] Actions) Choices(Order order)
    {
        string reasons = settings.GetLocalizedSetting(x => x.ReturnRequestReasons, order.CustomerLanguageId, order.StoreId, true, false);
        string actions = settings.GetLocalizedSetting(x => x.ReturnRequestActions, order.CustomerLanguageId, order.StoreId, true, false);
        return (string.IsNullOrWhiteSpace(reasons) ? [] : JsonSerializer.Deserialize<string[]>(reasons) ?? [],
            string.IsNullOrWhiteSpace(actions) ? [] : JsonSerializer.Deserialize<string[]>(actions) ?? []);
    }

    private async Task<List<ReturnableItemDto>> Quantities(Order order, CancellationToken ct)
    {
        var ids = order.OrderItems.Select(x => x.Id).ToArray();
        var existing = await db.ReturnCases.AsNoTracking().Where(x => ids.Contains(x.OrderItemId))
            .GroupBy(x => x.OrderItemId).Select(x => new { Id = x.Key, Quantity = x.Sum(y => y.Quantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        return order.OrderItems.Select(x => new ReturnableItemDto(x.Id, x.ProductId,
            Math.Max(0, x.Quantity - existing.GetValueOrDefault(x.Id)))).ToList();
    }

    public async Task<StorefrontResult<ReturnOptionsDto>> OptionsAsync(int id, CancellationToken ct)
    {
        var order = await Orders().Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (order == null) return new StorefrontFailure(404, "order_not_found");
        var choices = Choices(order);
        var allowed = processing.CanReturnItems(order);
        return new(new ReturnOptionsDto(allowed, choices.Reasons, choices.Actions,
            allowed ? await Quantities(order, ct) : []));
    }

    public async Task<StorefrontResult<List<int>>> CreateAsync(int id, ReturnRequest input, CancellationToken ct)
    {
        if (input.Items == null || input.Items.Count == 0 || input.Items.Any(x => x == null || x.Quantity < 1) ||
            input.Items.Select(x => x.OrderItemId).Distinct().Count() != input.Items.Count)
            return new StorefrontFailure(400, "invalid_return_items");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var order = await Orders().Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (order == null) return new StorefrontFailure(404, "order_not_found");
        if (!processing.CanReturnItems(order)) return new StorefrontFailure(403, "returns_not_allowed");
        var choices = Choices(order);
        if (!choices.Reasons.Contains(input.Reason) || !choices.Actions.Contains(input.Action))
            return new StorefrontFailure(422, "invalid_return_reason_or_action");
        var quantities = (await Quantities(order, ct)).ToDictionary(x => x.OrderItemId, x => x.MaxQuantity);
        if (input.Items.Any(x => !quantities.TryGetValue(x.OrderItemId, out var max) || x.Quantity > max))
            return new StorefrontFailure(422, "return_quantity_exceeded");
        var cases = input.Items.Select(x => new ReturnCase
        {
            Kind = ReturnCaseKind.Return, StoreId = order.StoreId, CustomerId = request.Customer.Id,
            OrderItemId = x.OrderItemId, Quantity = x.Quantity, ReasonForReturn = input.Reason,
            RequestedAction = input.Action, CustomerComments = input.Comments, StaffNotes = string.Empty,
            ReturnCaseStatus = ReturnCaseStatus.Pending, CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow
        }).ToList();
        db.ReturnCases.AddRange(cases);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        // Notification follows the committed native return-case records.
        foreach (var item in cases)
            await messages.SendNewReturnCaseStoreOwnerNotificationAsync(item,
                order.OrderItems.First(x => x.Id == item.OrderItemId), localization.DefaultAdminLanguageId);
        return new(cases.Select(x => x.Id).ToList());
    }
}
