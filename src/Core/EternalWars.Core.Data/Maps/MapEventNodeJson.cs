using System.Text.Json;
using EternalWars.Core.Domain.MapEvents;

namespace EternalWars.Core.Data.Maps;

/// <summary>
/// One node of an event tree as the map file writes it (<see cref="MapEventJson"/>): the editor's "Events" tab shows a chosen action or quest stage
/// as this text and reads it back, so every action, condition and expression can be edited without a form of its own (EVT-6).
/// </summary>
public sealed class MapEventNodeJson : IMapEventNodeText
{
    public static readonly MapEventNodeJson Instance = new();

    private MapEventNodeJson()
    {
    }

    public string Write(EventAction action) => JsonSerializer.Serialize(action, JsonDefaults.Options);

    /// <summary>A quest stage without its actions (they are edited in the tree).</summary>
    public string Write(QuestStage stage) => JsonSerializer.Serialize(stage with { Actions = [] }, JsonDefaults.Options);

    public string Write(EventCondition condition) => JsonSerializer.Serialize(condition, JsonDefaults.Options);

    /// <summary>The action the text describes; <see cref="FormatException"/> with the reason when it describes none.</summary>
    public EventAction ReadAction(string text) => Read<EventAction>(text);

    /// <summary>The stage the text describes, with <paramref name="actions"/> as its actions.</summary>
    public QuestStage ReadStage(string text, IReadOnlyList<EventAction> actions)
    {
        var stage = Read<QuestStage>(text);
        if (stage.Requirement is null || stage.Proposal is null)
        {
            throw new FormatException("A stage needs a requirement and a proposal.");
        }

        return stage with { Actions = actions };
    }

    public EventCondition ReadCondition(string text) => Read<EventCondition>(text);

    private static T Read<T>(string text)
        where T : class
    {
        try
        {
            var node = JsonSerializer.Deserialize<T>(text, JsonDefaults.Options) ?? throw new FormatException("Nothing to read.");
            Complete(node, typeof(T).Name);
            return node;
        }
        catch (JsonException e)
        {
            throw new FormatException(e.Message, e);
        }
        catch (NotSupportedException e)
        {
            throw new FormatException(e.Message, e);
        }
    }

    /// <summary>Refuses a node with a value left out that may not be missing (a message without its text, an "if" without its condition), at any depth.</summary>
    private static void Complete(object node, string where)
    {
        var type = node.GetType();
        if (type.Namespace != typeof(EventAction).Namespace && node is not Domain.Armies.CreatureStack)
        {
            return;
        }

        var nullability = new System.Reflection.NullabilityInfoContext();
        foreach (var property in type.GetProperties().Where(p => p.GetIndexParameters().Length == 0 && p.CanRead && p.DeclaringType != typeof(object)))
        {
            if (property.PropertyType.IsValueType)
            {
                continue;
            }

            var value = property.GetValue(node);
            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (value is null)
            {
                if (nullability.Create(property).ReadState == System.Reflection.NullabilityState.NotNull)
                {
                    throw new FormatException($"'{name}' is missing in {where}.");
                }

                continue;
            }

            if (value is string)
            {
                continue;
            }

            if (value is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    Complete(item ?? throw new FormatException($"'{name}' has an empty entry in {where}."), name);
                }
            }
            else
            {
                Complete(value, name);
            }
        }
    }
}
