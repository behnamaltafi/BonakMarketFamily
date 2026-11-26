using System.Threading.Tasks;
namespace BonakMarket.Core.Messaging;
/// <summary>Reports transport availability independently of customer login policy.</summary>
public interface ISmsDeliveryAvailability
{
    Task<bool> IsEnabledAsync();
}
