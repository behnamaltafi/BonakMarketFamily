using System.Text.Json.Serialization;

namespace BonakMarket.Web.Models.DataGrid;

public class GridSelection
{
    [JsonPropertyName("selectedKeys")]
    public string[] SelectedKeys { get; set; }

    public IEnumerable<int> GetEntityIds()
    {
        return SelectedKeys
            .Select(x => x.ToInt())
            .Where(x => x > 0)
            .Distinct();
    }
}