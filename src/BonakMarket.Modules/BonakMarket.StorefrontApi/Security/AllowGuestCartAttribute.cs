using System.Security.Cryptography;
usingMicrosoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using BonakMarket.Core.Identity;

namespace BonakMarket.StorefrontApi.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowGuestCartAttribute : Attribute { }
