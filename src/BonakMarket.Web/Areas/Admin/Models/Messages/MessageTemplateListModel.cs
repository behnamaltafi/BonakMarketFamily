using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Admin.Models.Messages;

public class MessageTemplateListModel : ModelBase
{
    [UIHint("Stores")]
    [LocalizedDisplay("Admin.Common.Store.SearchFor")]
    public int SearchStoreId { get; set; }

    [LocalizedDisplay("Admin.ContentManagement.MessageTemplates.Fields.Name")]
    public string SearchName { get; set; }

    [LocalizedDisplay("Admin.ContentManagement.MessageTemplates.Fields.Subject")]
    public string SearchSubject { get; set; }
}