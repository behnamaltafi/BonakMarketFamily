using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace BonakMarket.PayPal.Models;

[LocalizedDisplay("Account.Fields.")]
public class PublicInvoiceModel : ModelBase
{
    [LocalizedDisplay("*DateOfBirth")]
    public DateTime DateOfBirth { get; set; }

    [LocalizedDisplay("*Phone")]
    [DataType(DataType.PhoneNumber)]
    public string PhoneNumber { get; set; }
    public string DiallingCode { get; set; }
}

public partial class PayPalInvoiceValidator : SmartValidator<PublicInvoiceModel>
{
    public PayPalInvoiceValidator(Localizer T)
    {
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^[0-9]{1,14}?$")
            .WithMessage(T("Plugins.BonakMarket.PayPal.PhoneNumber.Invalid"));
    }
}