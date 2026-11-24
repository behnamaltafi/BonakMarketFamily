using BonakMarket.Core.AI.Metadata;
using BonakMarket.Json.Converters;

namespace BonakMarket.Core.AI.JsonConverters;

public sealed class AIResponseToolConverter : ArrayEnumFlagConverter<AIResponseTool>
{
    protected override IReadOnlyDictionary<string, AIResponseTool> GetMapping()
    {
        return new Dictionary<string, AIResponseTool>(StringComparer.OrdinalIgnoreCase)
        {
            ["WebSearch"] = AIResponseTool.WebSearch,
            ["ImageGeneration"] = AIResponseTool.ImageGeneration,
            ["CodeAnalysis"] = AIResponseTool.CodeAnalysis,
            ["FileSearch"] = AIResponseTool.FileSearch
        };
    }
}