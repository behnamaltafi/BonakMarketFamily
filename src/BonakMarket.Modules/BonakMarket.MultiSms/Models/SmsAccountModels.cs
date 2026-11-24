namespace BonakMarket.MultiSms.Models;

public sealed record SmsAccountProfileDto(int Id, string Username, string FirstName, string LastName, string Email, string Phone, bool ProfileRequired);
public sealed record SmsAccountAddressDto(int Id, string FirstName, string LastName, int? CountryId, int? StateProvinceId, string City, string Address1, string Address2, string ZipPostalCode, string PhoneNumber);
