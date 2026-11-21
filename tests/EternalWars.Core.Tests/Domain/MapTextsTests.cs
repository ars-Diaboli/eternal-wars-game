using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EternalWars.Core.Data;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The map's texts (EDIT-11, ADR-258): every string the map file writes is either a text, listed by <see cref="MapTexts"/> with a working
/// setter, or named here as not one (an id, a name things refer to, a ground row).
/// </summary>
public class MapTextsTests
{
    private const string Marked = "Marked text";
    private const string Changed = "Changed text";

    /// <summary>The strings of the map file that are not texts: ids, the names events and variables are found by, and the ground's rows.</summary>
    private static readonly HashSet<string> NotTexts =
    [
        "MapDocument.terrain", "MapDocument.roads", "MapDocument.rivers", "MapDocument.heights", "MapDocument.startZones",
        "MapObjectDocument.definition",
        "BansDocument.heroes", "BansDocument.artifacts", "BansDocument.spells", "BansDocument.skills",
        "WeekEventsDocument.weeklyFactions", "WeekEventsDocument.monthlyFactions",
        "PlayerSlotDocument.factions",
        "HeroPlacementDocument.type", "HeroPlacementDocument.portrait", "HeroPlacementDocument.skills",
        "PandorasBoxDocument.event", "PandorasBoxDocument.skills",
        "PrisonDocument.hero", "QuestSiteDocument.quest", "WitchHutDocument.skills",
        "MapEventSet.variables", "MapEventSet.keptVariables", "MapEventSet.takenVariables",
        "MapEventDefinition.name", "EventCell.event", "RunEventAction.event", "EnableEventAction.event",
        "SetVariableAction.variable", "VariableExpr.name", "QuantityExpr.id",
        "HeroIsCondition.hero", "HeroClassCondition.town", "FactionCondition.town",
        "GiveHeroAction.hero", "SkillAction.skill", "BuildAction.building",
    ];

    /// <summary>The texts of the map file, each with how to put <c>text</c> there on a blank map with the editor.</summary>
    private static readonly Dictionary<string, Action<MapEditorSession, string>> Writers = new()
    {
        ["MapDocument.name"] = (e, text) => e.Map.SetProperties(text, ""),
        ["MapDocument.description"] = (e, text) => e.Map.SetProperties("Map", text),
        ["PlayerSlotDocument.name"] = (e, text) => e.Players.Update(new PlayerId(0), text, true),
        ["MapObjectDocument.name"] = (e, text) => e.Objects.SetName(Place(e, BuiltInMapObjects.Sign), text),
        ["HeroPlacementDocument.name"] = (e, text) => e.Heroes.Place(new PlayerId(0), new MapPosition(3, 3), text, out _),
        ["HeroPlacementDocument.biography"] = (e, text) =>
        {
            e.Heroes.Place(new PlayerId(0), new MapPosition(3, 3), "Orrin", out var hero);
            e.Heroes.SetBiography(hero, text);
        },
        ["SignDocument.message"] = (e, text) => e.Contents.SetSignMessage(Place(e, BuiltInMapObjects.Sign), text),
        ["PandorasBoxDocument.message"] = (e, text) => e.Contents.SetPandorasBox(Place(e, BuiltInMapObjects.PandorasBox), BoxContents.Empty, [], text),
        ["SphinxDocument.question"] = (e, text) => e.Contents.SetSphinxRiddle(Place(e, BuiltInMapObjects.Sphinx), new Riddle(text, "a", "b", "c")),
        ["SphinxDocument.answer"] = (e, text) => e.Contents.SetSphinxRiddle(Place(e, BuiltInMapObjects.Sphinx), new Riddle("Why?", text, "b", "c")),
        ["SphinxDocument.wrong1"] = (e, text) => e.Contents.SetSphinxRiddle(Place(e, BuiltInMapObjects.Sphinx), new Riddle("Why?", "a", text, "c")),
        ["SphinxDocument.wrong2"] = (e, text) => e.Contents.SetSphinxRiddle(Place(e, BuiltInMapObjects.Sphinx), new Riddle("Why?", "a", "b", text)),
        ["ScenarioRulesDocument.rumours"] = (e, text) => e.Map.SetRules(e.Scenario.Rules with { Rumours = ["A rumour", text] }),
        ["MessageAction.text"] = (e, text) => AddEvent(e, new IfAction(new PlayerIsHumanCondition(), [new MessageAction(text)])),
        ["QuestionAction.text"] = (e, text) => AddEvent(e, new QuestionAction(text, [new EventAnswer("Yes", []), new EventAnswer("No", [])])),
        ["EventAnswer.text"] = (e, text) => AddEvent(e, new QuestionAction("Well?", [new EventAnswer("Yes", []), new EventAnswer(text, [])])),
        ["DialogueLine.speaker"] = (e, text) => AddEvent(e, new DialogueAction([new DialogueLine(text, "Hello.")])),
        ["DialogueLine.text"] = (e, text) => AddEvent(e, new DialogueAction([new DialogueLine("", "Hello."), new DialogueLine("", text)])),
        ["EndGameAction.text"] = (e, text) => AddEvent(e, new IfAction(new PlayerIsHumanCondition(), []) { Else = [new EndGameAction(true) { Text = text }] }),
        ["QuestStage.proposal"] = (e, text) => AddQuest(e, new QuestStage(new PlayerIsHumanCondition(), text, [])),
        ["QuestStage.progress"] = (e, text) => AddQuest(e, new QuestStage(new PlayerIsHumanCondition(), "Bring me gold.", []) { Progress = text }),
        ["QuestStage.completion"] = (e, text) => AddQuest(e, new QuestStage(new PlayerIsHumanCondition(), "Bring me gold.", []) { Completion = text }),
    };

    public static TheoryData<string> TextFields => [.. Writers.Keys.Order(StringComparer.Ordinal)];

    private static MapEditorSession Editor() => new(BlankScenario.Create(30, 20));

    private static MapObjectId Place(MapEditorSession editor, string definition)
    {
        Assert.Equal(EditResult.Ok, editor.Objects.Place(definition, new MapPosition(10, 10), out var id));
        return id;
    }

    private static void AddEvent(MapEditorSession editor, EventAction action) =>
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("talk", MapEventKind.Player, [new MessageAction("Hi."), action])));

    private static void AddQuest(MapEditorSession editor, QuestStage stage) =>
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("quest", MapEventKind.Quest, []) { Stages = [stage] }));

    private static string Json(MapEditorSession editor) => MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));

    /// <summary>A string field added to the map file fails here until it is a text (listed, with a writer above) or named as not one.</summary>
    [Fact]
    public void EveryStringOfTheMapFile_IsATextOrNamedAsNotOne()
    {
        var fields = StringFields(JsonDefaults.Options, typeof(MapDocument));

        var unknown = fields.Where(f => !Writers.ContainsKey(f) && !NotTexts.Contains(f)).Order(StringComparer.Ordinal).ToList();
        var stale = Writers.Keys.Concat(NotTexts).Where(f => !fields.Contains(f)).Order(StringComparer.Ordinal).ToList();

        Assert.True(unknown.Count == 0, $"Strings of the map file that are neither a text (MapTexts and a writer here) nor named as not one: {string.Join(", ", unknown)}");
        Assert.True(stale.Count == 0, $"Names here the map file no longer has: {string.Join(", ", stale)}");
    }

    [Theory]
    [MemberData(nameof(TextFields))]
    public void EveryText_IsListed_AndItsSetterChangesTheFile_AsOneUndoStep(string field)
    {
        var editor = Editor();
        Writers[field](editor, Marked);
        Assert.Contains(Marked, Json(editor), StringComparison.Ordinal);

        var text = Assert.Single(editor.Texts.List(), t => t.Text == Marked);
        Assert.Equal(EditResult.Ok, editor.Texts.Set(text.Id, Changed));

        var json = Json(editor);
        Assert.DoesNotContain(Marked, json, StringComparison.Ordinal);
        Assert.Contains(Changed, json, StringComparison.Ordinal);
        Assert.Equal(Changed, Assert.Single(editor.Texts.List(), t => t.Id == text.Id).Text);

        editor.Undo();
        Assert.Contains(Marked, Json(editor), StringComparison.Ordinal);
        Assert.DoesNotContain(Changed, Json(editor), StringComparison.Ordinal);
    }

    [Fact]
    public void ABlankMap_ListsItsNameDescriptionPlayersAndHeroes_InGroups()
    {
        var editor = Editor();
        var texts = editor.Texts.List();
        var hero = editor.Scenario.Heroes[0].Id.Value;

        Assert.Equal(["map.name", "map.description", "player.0.name", "player.1.name", $"hero.{hero}.name", $"hero.{hero}.biography"], texts.Take(6).Select(t => t.Id));
        Assert.Equal([MapTexts.MapGroup, MapTexts.MapGroup, MapTexts.PlayersGroup, MapTexts.PlayersGroup, MapTexts.HeroesGroup], texts.Take(5).Select(t => t.Group));
        Assert.Equal(2 + editor.Scenario.Players.Count + (2 * editor.Scenario.Heroes.Count), texts.Count);
        Assert.True(texts[1].Multiline);
        Assert.False(texts[0].Multiline);
    }

    [Fact]
    public void ATown_IsListedWithItsName_OtherObjectsOnlyWhenNamed_AndRosterSpeakersAreNot()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(18, 6), out var town));
        Place(editor, BuiltInMapObjects.Sign);
        AddEvent(editor, new DialogueAction([new DialogueLine(editor.Scenario.HeroRoster.All.First().Id, "I am known.")]));

        var texts = editor.Texts.List();
        Assert.Contains(texts, t => t.Id == $"object.{town.Value}.name" && t.Text == editor.Scenario.FindObject(town)!.Name);
        Assert.DoesNotContain(texts, t => t.Kind == MapTextKind.ObjectName && t.Id != $"object.{town.Value}.name");
        Assert.DoesNotContain(texts, t => t.Kind == MapTextKind.DialogueSpeaker);
        Assert.Contains(texts, t => t.Kind == MapTextKind.DialogueLine && t.Text == "I am known.");
    }

    [Fact]
    public void Setting_FollowsThePanelsRules_AndRefusesBlankRequiredTexts()
    {
        var editor = Editor();
        AddEvent(editor, new QuestionAction("Well?", [new EventAnswer("Yes", []), new EventAnswer("No", [])]));
        Assert.Equal(EditResult.Ok, editor.Map.SetRules(editor.Scenario.Rules with { Rumours = ["A rumour"] }));
        var steps = editor.History.Count;

        Assert.Equal(EditResult.InvalidTarget, editor.Texts.Set("nothing.here", "x"));
        Assert.Equal(EditResult.InvalidValue, editor.Texts.Set("map.name", "  "));
        Assert.Equal(EditResult.InvalidValue, editor.Texts.Set("player.0.name", ""));
        Assert.Equal(EditResult.InvalidValue, editor.Texts.Set("rumour.0", "two\nlines"));
        var question = Assert.Single(editor.Texts.List(), t => t.Kind == MapTextKind.EventQuestion);
        Assert.Equal(EditResult.InvalidValue, editor.Texts.Set(question.Id, " "));
        Assert.Equal(EditResult.Ok, editor.Texts.Set("map.name", editor.Scenario.Map.Name)); // the same text: nothing to record
        Assert.Equal(steps, editor.History.Count);

        Assert.Equal(EditResult.Ok, editor.Texts.Set("map.name", "  The Pass  "));
        Assert.Equal("The Pass", editor.Scenario.Map.Name);
    }

    [Fact]
    public void ATextTheMapWouldKeepAsItIs_RecordsNothing_TrimmedToo()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Map.SetRules(editor.Scenario.Rules with { Rumours = ["A rumour"] }));
        AddEvent(editor, new DialogueAction([new DialogueLine("Bob", "Hello.")]));
        var speaker = Assert.Single(editor.Texts.List(), t => t.Kind == MapTextKind.DialogueSpeaker);
        var steps = editor.History.Count;

        Assert.Equal(EditResult.Ok, editor.Texts.Set("rumour.0", "A rumour "));
        Assert.Equal(EditResult.Ok, editor.Texts.Set(speaker.Id, " Bob"));
        Assert.Equal(EditResult.Ok, editor.Texts.Set("map.name", editor.Scenario.Map.Name + "  "));
        Assert.Equal(steps, editor.History.Count);
    }

    [Fact]
    public void AnIdIsAPlace_SoTheExpectedTextGuardsIt()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("talk", MapEventKind.Player, [new MessageAction("First."), new MessageAction("Second.")])));
        var second = Assert.Single(editor.Texts.List(), t => t.Text == "Second.");
        var talk = editor.Scenario.MapEvents.Set.Find("talk")!;
        Assert.Equal(EditResult.Ok, editor.MapEvents.Replace("talk", MapEventTreeEdit.Insert(talk, [], 0, 0, new MessageAction("Zeroth."))));

        Assert.Equal(EditResult.InvalidTarget, editor.Texts.Set(second.Id, "Changed.", expected: "Second."));
        Assert.Equal(["Zeroth.", "First.", "Second."], editor.Scenario.MapEvents.Set.Find("talk")!.Actions.Cast<MessageAction>().Select(m => m.Text));
    }

    /// <summary>"Type.jsonName" of every property the map's JSON writes a string in (alone, in a list or as a dictionary key); content ids are ids.</summary>
    private static HashSet<string> StringFields(JsonSerializerOptions options, Type root)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<Type>();
        var pending = new Queue<Type>([root]);
        while (pending.TryDequeue(out var type))
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (!seen.Add(type) || type.IsPrimitive || type.IsEnum || type == typeof(string) || IsContentId(type))
            {
                continue;
            }

            var info = options.GetTypeInfo(type);
            if (info.Kind != JsonTypeInfoKind.Object)
            {
                continue;
            }

            foreach (var derived in info.PolymorphismOptions?.DerivedTypes ?? [])
            {
                pending.Enqueue(derived.DerivedType);
            }

            foreach (var property in info.Properties)
            {
                var declaring = (property.AttributeProvider as MemberInfo)?.DeclaringType ?? type;
                if (HoldsString(options, property.PropertyType, pending))
                {
                    fields.Add($"{declaring.Name}.{property.Name}");
                }
            }
        }

        return fields;
    }

    /// <summary>Whether a property of the type writes strings; the object types it holds are queued.</summary>
    private static bool HoldsString(JsonSerializerOptions options, Type type, Queue<Type> pending)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string))
        {
            return true;
        }

        if (type.IsPrimitive || type.IsEnum || IsContentId(type))
        {
            return false;
        }

        var info = options.GetTypeInfo(type);
        switch (info.Kind)
        {
            case JsonTypeInfoKind.Enumerable:
                return HoldsString(options, info.ElementType!, pending);
            case JsonTypeInfoKind.Dictionary:
                var value = HoldsString(options, info.ElementType!, pending);
                return HoldsString(options, info.KeyType!, pending) || value;
            case JsonTypeInfoKind.Object:
                pending.Enqueue(type);
                return false;
            default:
                return false;
        }
    }

    private static bool IsContentId(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IContentId<>));
}
