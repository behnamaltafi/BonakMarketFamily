using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using BonakMarket.Core.Identity;

namespace BonakMarket.StorefrontApi.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MergeGuestCartAttribute : Attribute { }
