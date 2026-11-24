using BonakMarket.ComponentModel;
using BonakMarket.Core.Common.Configuration;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Stores;

namespace BonakMarket.Web.Models.Pdf;

internal class PdfReceiptMapper : Mapper<Store, PdfReceiptSectionModel>
{
    private readonly ISettingFactory _settingFactory;
    private readonly IAddressService _addressService;

    public PdfReceiptMapper(ISettingFactory settingFactory, IAddressService addressService)
    {
        _settingFactory = settingFactory;
        _addressService = addressService;
    }

    protected override void Map(Store from, PdfReceiptSectionModel to, dynamic parameters = null)
        => throw new NotImplementedException();

    public override async Task MapAsync(Store from, PdfReceiptSectionModel to, dynamic parameters = null)
    {
        Guard.NotNull(from, nameof(from));
        Guard.NotNull(to, nameof(to));

        var companyInfoSettings = await _settingFactory.LoadSettingsAsync<CompanyInformationSettings>(from.Id);
        var bankSettings = await _settingFactory.LoadSettingsAsync<BankConnectionSettings>(from.Id);
        var contactSettings = await _settingFactory.LoadSettingsAsync<ContactDataSettings>(from.Id);
        var pdfSettings = await _settingFactory.LoadSettingsAsync<PdfSettings>(from.Id);

        to.StoreId = from.Id;
        to.StoreName = from.Name;
        to.StoreUrl = from.GetBaseUrl();
        to.LogoId = pdfSettings.LogoPictureId != 0 ? pdfSettings.LogoPictureId : from.LogoMediaFileId;

        to.MerchantCompanyInfo = companyInfoSettings;
        to.MerchantBankAccount = bankSettings;
        to.MerchantContactData = contactSettings;
        to.MerchantFormattedAddress = await _addressService.FormatAddressAsync(companyInfoSettings, true);
    }
}