using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using Smartstore.Core.Messaging;
using Smartstore.MultiSms.Configuration;
using Smartstore.MultiSms.Services;

namespace Smartstore.Core.Tests.Platform.Messaging;

[TestFixture]
public sealed class SmsMockAndHistoryTests
{
    [Test]
    public async Task Development_mock_stores_one_short_lived_message_without_real_transport()
    {
        var env = Environment("Development");
        var inbox = new DevelopmentSmsInbox(env);
        var provider = new MockSmsProvider(env, inbox);
        var config = new SmsGatewaySettings { Enabled = true, MockEnabled = true };

        var receipt = await provider.SendAsync("09123456789", "OTP code 123456", config, CancellationToken.None);
        var messages = inbox.Snapshot();

        Assert.Multiple(() =>
        {
            Assert.That(receipt, Is.Not.Empty);
            Assert.That(provider.IsAvailable(config), Is.True);
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Phone, Is.EqualTo("09123456789"));
            Assert.That(messages[0].Message, Does.Contain("123456"));
        });
    }

    [Test]
    public void Production_mock_is_unavailable_even_if_setting_is_enabled()
    {
        var env = Environment("Production");
        var inbox = new DevelopmentSmsInbox(env);
        var provider = new MockSmsProvider(env, inbox);
        var config = new SmsGatewaySettings { Enabled = true, MockEnabled = true };

        Assert.Multiple(() =>
        {
            Assert.That(provider.IsAvailable(config), Is.False);
            Assert.That(inbox.Snapshot(), Is.Empty);
            Assert.Throws<InvalidOperationException>(() => inbox.Receive("09123456789", "123456"));
        });
        Assert.ThrowsAsync<SmsProviderException>(async () =>
            await provider.SendAsync("09123456789", "123456", config, CancellationToken.None));
    }

    [Test]
    public void Mock_requires_explicit_enable_even_in_development()
    {
        var env = Environment("Development");
        var provider = new MockSmsProvider(env, new DevelopmentSmsInbox(env));
        Assert.That(provider.IsAvailable(new SmsGatewaySettings { Enabled = true }), Is.False);
    }

    [Test]
    public void History_error_categories_do_not_copy_provider_exception_text()
    {
        var error = new SmsProviderException("Secret=private-token", safeToFailover: true);
        Assert.That(SmsHistoryFormatter.SafeCategory(error), Is.EqualTo("ProviderRejected"));
        Assert.That(SmsHistoryFormatter.FailedStatus(error), Is.EqualTo("Rejected"));

        Assert.That(SmsHistoryFormatter.SafeCategory(new HttpRequestException("Secret=private-token")),
            Is.EqualTo("Transport"));
        Assert.That(SmsHistoryFormatter.FailedStatus(new HttpRequestException()),
            Is.EqualTo("Unknown"));
    }

    [Test]
    public void History_masks_recipient_and_otp_is_explicit_message_kind()
    {
        Assert.That(SmsHistoryFormatter.MaskPhone("+989121234567"), Is.Not.EqualTo("+989121234567"));
        var request = new SmsSendRequest("09123456789", "OTP 123456", SmsMessageKind.Otp);
        Assert.That(request.Kind, Is.EqualTo(SmsMessageKind.Otp));
    }

    private static IHostEnvironment Environment(string name)
    {
        var mock = new Mock<IHostEnvironment>();
        mock.SetupGet(x => x.EnvironmentName).Returns(name);
        return mock.Object;
    }
}
