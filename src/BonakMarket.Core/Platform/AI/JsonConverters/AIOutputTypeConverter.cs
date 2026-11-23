using System.Text.Json;
using System.Text.Json.Serialization;
using BonakMarket.Core.AI.Metadata;

namespace BonakMarket.Core.AI.JsonConverters;

public sealed class AIOutputTypeConverter : JsonStringEnumConverter<AIOutputType>
{
    public AIOutputTypeConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    {
    }
}
