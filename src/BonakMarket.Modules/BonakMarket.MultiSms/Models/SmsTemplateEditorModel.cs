namespace BonakMarket.MultiSms.Models;

public sealed class SmsTemplateEditorModel
{
    public int Id { get; set; }
    public string Key { get; set; }
    public string Title { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsOtp { get; set; }
    public string Parameters { get; set; }
    public List<SmsTemplateMappingEditor> Mappings { get; set; } = [];
}

public sealed class SmsTemplateMappingEditor
{
    public string Provider { get; set; }
    public string ExternalTemplateId { get; set; }
    /// <summary>Comma-separated provider slots, in the order of the logical parameters.</summary>
    public string Slots { get; set; }
}
