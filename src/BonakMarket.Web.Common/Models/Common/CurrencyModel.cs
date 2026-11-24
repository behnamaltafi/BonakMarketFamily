using BonakMarket.Web.Modelling;

namespace BonakMarket.Web.Models.Common;

public partial class CurrencyModel : EntityModelBase
{
    public string Name { get; set; }
    public string ISOCode { get; set; }
    public string Symbol { get; set; }
}