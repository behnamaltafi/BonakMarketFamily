using BonakMarket.Core.Common.Configuration;
using BonakMarket.Scheduling;

namespace BonakMarket.Core.Identity.Tasks;

/// <summary>
/// A task that periodically deletes guest customers.
/// </summary>
public partial class DeleteGuestsTask : ITask
{
    private readonly ICustomerService _customerService;
    private readonly CommonSettings _commonSettings;

    public DeleteGuestsTask(ICustomerService customerService, CommonSettings commonSettings)
    {
        _customerService = customerService;
        _commonSettings = commonSettings;
    }

    public async Task Run(TaskExecutionContext ctx, CancellationToken cancelToken = default)
    {
        Guard.NotNegative(_commonSettings.MaxGuestsRegistrationAgeInMinutes);

        var registrationTo = DateTime.UtcNow.AddMinutes(-_commonSettings.MaxGuestsRegistrationAgeInMinutes);

        await _customerService.DeleteGuestCustomersAsync(
            registrationFrom: null,
            registrationTo,
            onlyWithoutShoppingCart: true,
            cancelToken);
    }
}