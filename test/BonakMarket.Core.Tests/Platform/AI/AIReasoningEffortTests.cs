using System.Text.Json;
using NUnit.Framework;
using BonakMarket.Core.AI;
using BonakMarket.Core.AI.Metadata;
using BonakMarket.Json;
using BonakMarket.Test.Common;

namespace BonakMarket.Core.Tests.Platform.AI;

[TestFixture]
public class AIReasoningEffortTests
{
    [Test]
    public void Can_convert_from_and_to_string()
    {
        AIReasoningEffort? effort = "high";

        effort.ShouldEqual(AIReasoningEffort.High);
        ((string)effort.Value).ShouldEqual("high");
    }

    [Test]
    public void Can_deserialize_think_options()
    {
        var json = """
            {
              "levels": [ "low", "medium", "xhigh" ]
            }
            """;

        var options = JsonSerializer.Deserialize<AIModelThinkOptions>(json, SmartJsonOptions.CamelCased);

        options.ShouldNotBeNull();
        options.Default.ShouldEqual(AIReasoningEffort.Auto);
        options.Levels.ShouldEqual(new AIReasoningEffort[] { AIReasoningEffort.Low, AIReasoningEffort.Medium, AIReasoningEffort.XHigh });
        options.ContainsLevel(AIReasoningEffort.Medium).ShouldBeTrue();
        options.ContainsLevel(AIReasoningEffort.High).ShouldBeFalse();
    }

    [Test]
    public void Can_roundtrip_think_options()
    {
        var options = new AIModelThinkOptions
        {
            Default = AIReasoningEffort.High,
            Levels = [AIReasoningEffort.Low, AIReasoningEffort.High, AIReasoningEffort.Max]
        };

        var json = JsonSerializer.Serialize(options, SmartJsonOptions.CamelCased);
        var roundtripped = JsonSerializer.Deserialize<AIModelThinkOptions>(json, SmartJsonOptions.CamelCased);

        roundtripped.ShouldNotBeNull();
        roundtripped.Default.ShouldEqual(AIReasoningEffort.High);
        roundtripped.Levels.ShouldEqual(new AIReasoningEffort[] { AIReasoningEffort.Low, AIReasoningEffort.High, AIReasoningEffort.Max });
    }

    [Test]
    public void Unknown_effort_cannot_be_deserialized()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AIModelThinkOptions>(
                """{ "levels": [ "unknown" ] }""",
                SmartJsonOptions.CamelCased));
    }
}
