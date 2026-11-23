using System.ComponentModel.DataAnnotations;
using BonakMarket.Web.Models.Common;

namespace BonakMarket.Admin.Models.Customers;

public class CustomerAddressModel : ModelBase
{
    public int CustomerId { get; set; }
    public string Username { get; set; }

    [UIHint("Address")]
    public AddressModel Address { get; set; } = new();
}