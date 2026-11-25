using System;
using NUnit.Framework;
using BonakMarket.MultiSms.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class SmsDiagnosticsTests
{
    [Test]
    public void Explicit_rejection_keeps_numeric_diagnostics_without_exception_text()
    {
        var exception = new SmsProviderException("secret OTP 123456", true, 200, 0, -1);
        Assert.That(SmsHistoryFormatter.SafeCategory(exception), Is.EqualTo("ProviderRejected"));
        Assert.That(SmsHistoryFormatter.FailedStatus(exception), Is.EqualTo("Rejected"));
        Assert.That(SmsHistoryFormatter.SafeDiagnostic(exception), Is.EqualTo("ProviderRejected; HTTP=200; Status=0; Code=-1"));
    }

    [Test]
    public void Ambiguous_transport_error_does_not_expose_message()
    {
        Assert.That(SmsHistoryFormatter.SafeDiagnostic(new InvalidOperationException("secret")), Is.EqualTo("Configuration"));
        Assert.That(SmsHistoryFormatter.FailedStatus(new TimeoutException()), Is.EqualTo("Unknown"));
    }

    [Test]
    public void Diagnostic_fits_existing_database_column_and_excludes_large_value()
    {
        var exception = new SmsProviderException("secret", true, 200, int.MinValue, 9999, SmsFailureReason.CredentialsDecryptionFailed);
        Assert.That(SmsHistoryFormatter.SafeDiagnostic(exception).Length, Is.LessThanOrEqualTo(60));
        Assert.That(SmsHistoryFormatter.SafeDiagnostic(new SmsProviderException("secret", true, 200, 0, 123456)), Does.Not.Contain("123456"));
    }
}
