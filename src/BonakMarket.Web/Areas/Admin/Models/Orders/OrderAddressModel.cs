using System.ComponentModel.DataAnnotations;
using BonakMarket.Web.Models.Common;

namespace BonakMarket.Admin.Models.Orders;

public class OrderAddressModel : ModelBase
{
    public int OrderId { get; set; }
    public string Username { get; set; }

    [UIHint("Address")]
    public AddressModel Address { get; set; } = new();
}