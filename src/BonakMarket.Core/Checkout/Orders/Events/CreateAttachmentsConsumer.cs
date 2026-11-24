using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Common.Configuration;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Messaging;
using BonakMarket.Core.Messaging.Events;
using BonakMarket.Events;
using BonakMarket.Http;
using BonakMarket.Pdf;

namespace BonakMarket.Core.Checkout.Orders.Events;

internal class CreateAttachmentsConsumer : IConsumer
{
    public ILogger Logger { get; set; } = NullLogger.Instance;
    public Localizer T { get; set; } = NullLocalizer.Instance;

    public void HandleEvent(MessageQueuingEvent message,
        Lazy<IUrlHelper> urlHelper,
        PdfSettings pdfSettings)
    {
        var messageName = message.MessageContext.MessageTemplate.Name;

        bool attachPdf =
            (pdfSettings.AttachOrderPdfToOrderPlacedEmail
                && messageName.EqualsNoCase(MessageTemplateNames.OrderPlacedCustomer))
            || (pdfSettings.AttachOrderPdfToOrderCompletedEmail
                && messageName.EqualsNoCase(MessageTemplateNames.OrderCompletedCustomer));

        if (attachPdf
            && message.MessageModel.Get("Order") is IDictionary<string, object> order
            && order.Get("ID") is int orderId)
        {
            try
            {
                // Resolve the MVC helper only when a PDF is needed; unrelated messages may be
                // queued outside an MVC request.
                var helper = urlHelper.Value;
                var request = helper.ActionContext.HttpContext.Request;
                var path = helper.Action("Print", "Order", new { id = orderId, pdf = true, area = string.Empty });
                var url = WebHelper.GetAbsoluteUrl(path, request);
                var requestSnapshot = request.CreateSnapshot();

                var work = AttachmentWork.Create<PdfHttpClient, (string Url, HttpRequestSnapshot Snapshot)>(
                    (url, requestSnapshot),
                    static async (client, state, attachment, cancelToken) =>
                    {
                        var result = await client.GetPdfAsync(state.Url, state.Snapshot, cancelToken);
                        attachment.Name = result.FileName;
                        attachment.MediaStorage = new MediaStorage { Data = result.Buffer };
                    });

                message.AddDeferredAttachment(new QueuedEmailAttachment
                {
                    StorageLocation = EmailAttachmentStorageLocation.Blob,
                    MimeType = MediaTypeNames.Application.Pdf,
                    Name = $"order-{orderId}.pdf"
                }, work);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, T("Admin.System.QueuedEmails.ErrorCreatingAttachment"));
            }
        }
    }
}