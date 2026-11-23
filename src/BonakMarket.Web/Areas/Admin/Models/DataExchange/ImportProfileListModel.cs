using BonakMarket.Core.DataExchange;

namespace BonakMarket.Admin.Models.Import;

public partial class ImportProfileListModel : EntityModelBase
{
    [LocalizedDisplay("Admin.Common.Entity")]
    public ImportEntityType EntityType { get; set; }

    public List<ImportProfileModel> Profiles { get; set; } = new();
}