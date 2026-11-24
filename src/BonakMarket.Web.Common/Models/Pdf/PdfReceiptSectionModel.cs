using BonakMarket.Core.Common.Configuration;
using BonakMarket.Web.Modelling;

namespace BonakMarket.Web.Models.Pdf;

public partial class PdfReceiptSectionModel : ModelBase
{
    public int StoreId { get; set; }
    public string StoreName { get; set; }
    public string StoreUrl { get; set; }
    public int LogoId { get; set; }

    public CompanyInformationSettings MerchantCompanyInfo { get; set; }
    public BankConnectionSettings MerchantBankAccount { get; set; }
    public ContactDataSettings MerchantContactData { get; set; }

    public string MerchantFormattedAddress { get; set; }
}