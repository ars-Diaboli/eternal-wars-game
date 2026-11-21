using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

/// <summary>What a <see cref="MapText"/> is (EDIT-11, ADR-258).</summary>
public enum MapTextKind
{
    MapName,
    MapDescription,
    PlayerName,
    ObjectName,
    HeroName,
    HeroBiography,
    SignMessage,
    BoxMessage,
    RiddleQuestion,
    RiddleAnswer,
    RiddleWrongAnswer,
    Rumour,
    EventMessage,
    EventQuestion,
    EventAnswer,
    DialogueSpeaker,
    DialogueLine,
    EndGameText,
    QuestProposal,
    QuestProgress,
    QuestCompletion,
}

/// <summary>
/// One text of a map (EDIT-11, ADR-258): <see cref="Id"/> names its place (a rumour's number, an event action's path), which an edit that adds,
/// removes or moves things may give another text; <see cref="Group"/> and
/// <see cref="Where"/> say where it is, <see cref="Text"/> is what it says. <see cref="Multiline"/> texts may have line breaks. It is set through
/// <see cref="MapTextEditor.Set"/>, which makes the edit of the panel the text belongs to.
/// </summary>
public sealed record MapText(string Id, MapTextKind Kind, string Group, string Where, string Text, bool Multiline)
{
    /// <summary>The edit that sets the text, through the session's editors.</summary>
    internal Func<MapEditorSession, string, EditResult> Setter { get; init; } = (_, _) => EditResult.InvalidTarget;

    /// <summary>The text as the map would keep it (trimmed where the edit trims), so the same text records nothing.</summary>
    internal Func<string, string> Kept { get; init; } = text => text;
}

/// <summary>
/// Every text of a map in one list (EDIT-11, ADR-258), so it can be read, edited and later translated in one place: the map's name and
/// description, the players' names, the names authors gave objects (and every town's), the heroes' names and biographies, sign and
/// Pandora's Box messages, an author's sphinx riddles, the rumours and the map events' texts (messages, questions and their answers,
/// dialogue lines and speakers who are not roster heroes, end-of-game texts, quest stages). The order is the groups', then the map's own.
/// </summary>
public static class MapTexts
{
    public const string MapGroup = "Map";
    public const string PlayersGroup = "Players";
    public const string HeroesGroup = "Heroes";
    public const string ObjectsGroup = "Objects";
    public const string RumoursGroup = "Rumours";
    public const string EventsGroup = "Events";

    public static IReadOnlyList<MapText> List(GameState scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var texts = new List<MapText>();
        Map(scenario, texts);
        Players(scenario, texts);
        Heroes(scenario, texts);
        Objects(scenario, texts);
        Rumours(scenario, texts);
        Events(scenario, texts);
        return texts;
    }

    private static void Map(GameState scenario, List<MapText> texts)
    {
        var map = scenario.Map;
        texts.Add(new MapText("map.name", MapTextKind.MapName, MapGroup, "Map name", map.Name, false)
        {
            Setter = (e, v) => string.IsNullOrWhiteSpace(v) ? EditResult.InvalidValue : Done(() => e.Map.SetProperties(v.Trim(), e.Scenario.Map.Description)),
            Kept = Trimmed,
        });
        texts.Add(new MapText("map.description", MapTextKind.MapDescription, MapGroup, "Map description", map.Description, true)
        {
            Setter = (e, v) => Done(() => e.Map.SetProperties(e.Scenario.Map.Name, v)),
        });
    }

    private static void Players(GameState scenario, List<MapText> texts)
    {
        foreach (var player in scenario.Players)
        {
            var id = player.Id;
            texts.Add(new MapText($"player.{id.Value}.name", MapTextKind.PlayerName, PlayersGroup, $"Player {id.Value + 1}", player.Name, false)
            {
                Setter = (e, v) => string.IsNullOrWhiteSpace(v) ? EditResult.InvalidValue : e.Players.Update(id, v.Trim(), e.Scenario.GetPlayer(id).IsHuman),
                Kept = Trimmed,
            });
        }
    }

    private static void Heroes(GameState scenario, List<MapText> texts)
    {
        foreach (var hero in scenario.Heroes)
        {
            var id = hero.Id;
            var where = $"Hero #{id.Value} at {hero.Position.X}, {hero.Position.Y}";
            texts.Add(new MapText($"hero.{id.Value}.name", MapTextKind.HeroName, HeroesGroup, $"{where}: name", hero.Name, false)
            {
                Setter = (e, v) => e.Heroes.SetName(id, v),
                Kept = Trimmed,
            });
            texts.Add(new MapText($"hero.{id.Value}.biography", MapTextKind.HeroBiography, HeroesGroup, $"{where}: biography", hero.Biography ?? "", true)
            {
                Setter = (e, v) => e.Heroes.SetBiography(id, v),
                Kept = Trimmed,
            });
        }
    }

    private static void Objects(GameState scenario, List<MapText> texts)
    {
        foreach (var mapObject in scenario.Objects)
        {
            var id = mapObject.Id;
            var key = $"object.{id.Value}";
            var where = $"{mapObject.Definition.Name} at {mapObject.VisitPosition.X}, {mapObject.VisitPosition.Y}";
            if (mapObject is Town || mapObject.GivenName is not null)
            {
                texts.Add(new MapText($"{key}.name", MapTextKind.ObjectName, ObjectsGroup, $"{where}: name", mapObject.Name ?? "", false)
                {
                    Setter = (e, v) => e.Objects.SetName(id, v),
                    Kept = Trimmed,
                });
            }

            switch (mapObject)
            {
                case SignPost sign:
                    texts.Add(new MapText($"{key}.message", MapTextKind.SignMessage, ObjectsGroup, $"{where}: message", sign.Message, true)
                    {
                        Setter = (e, v) => e.Contents.SetSignMessage(id, v),
                    });
                    break;
                case PandorasBox box:
                    texts.Add(new MapText($"{key}.message", MapTextKind.BoxMessage, ObjectsGroup, $"{where}: message", box.Message ?? "", true)
                    {
                        Setter = (e, v) => e.Scenario.FindObject(id) is PandorasBox now
                            ? e.Contents.SetPandorasBox(id, now.Contents, now.Guard, v)
                            : EditResult.InvalidTarget,
                    });
                    break;
                case Sphinx { Riddle: { } riddle }:
                    Riddle(texts, id, key, where, MapTextKind.RiddleQuestion, "question", riddle.Question, (r, v) => r with { Question = v });
                    Riddle(texts, id, key, where, MapTextKind.RiddleAnswer, "answer", riddle.Answer, (r, v) => r with { Answer = v });
                    Riddle(texts, id, key, where, MapTextKind.RiddleWrongAnswer, "wrong answer 1", riddle.Wrong1, (r, v) => r with { Wrong1 = v });
                    Riddle(texts, id, key, where, MapTextKind.RiddleWrongAnswer, "wrong answer 2", riddle.Wrong2, (r, v) => r with { Wrong2 = v });
                    break;
            }
        }
    }

    private static void Riddle(
        List<MapText> texts, Common.MapObjectId id, string key, string where, MapTextKind kind, string part, string text, Func<Riddle, string, Riddle> with) =>
        texts.Add(new MapText($"{key}.riddle.{part.Replace(' ', '-')}", kind, ObjectsGroup, $"{where}: riddle {part}", text, false)
        {
            Setter = (e, v) => e.Scenario.FindObject(id) is Sphinx { Riddle: { } now }
                ? e.Contents.SetSphinxRiddle(id, with(now, v))
                : EditResult.InvalidTarget,
            Kept = Trimmed,
        });

    private static void Rumours(GameState scenario, List<MapText> texts)
    {
        var rumours = scenario.Rules.Rumours;
        for (var i = 0; i < rumours.Count; i++)
        {
            var index = i;
            texts.Add(new MapText($"rumour.{i}", MapTextKind.Rumour, RumoursGroup, $"Rumour {i + 1}", rumours[i], false)
            {
                Setter = (e, v) =>
                {
                    var now = e.Scenario.Rules.Rumours;
                    return index < now.Count
                        ? e.Map.SetRules(e.Scenario.Rules with { Rumours = [.. now.Select((r, n) => n == index ? v.Trim() : r)] })
                        : EditResult.InvalidTarget;
                },
                Kept = Trimmed,
            });
        }
    }

    private static void Events(GameState scenario, List<MapText> texts)
    {
        foreach (var definition in scenario.MapEvents.Set.Events)
        {
            var name = definition.Name;
            var key = $"event.{name}";
            for (var s = 0; s < definition.Stages.Count; s++)
            {
                var stage = definition.Stages[s];
                var where = $"Quest \"{name}\", stage {s + 1}";
                Stage(texts, name, s, $"{key}.stage.{s}.proposal", MapTextKind.QuestProposal, $"{where}: proposal", stage.Proposal, true,
                    (st, v) => st with { Proposal = v });
                Stage(texts, name, s, $"{key}.stage.{s}.progress", MapTextKind.QuestProgress, $"{where}: progress", stage.Progress, false,
                    (st, v) => st with { Progress = v });
                Stage(texts, name, s, $"{key}.stage.{s}.completion", MapTextKind.QuestCompletion, $"{where}: completion", stage.Completion, false,
                    (st, v) => st with { Completion = v });
            }

            Walk(scenario, texts, definition, MapEventTreeEdit.Lists(definition), MapEventTreeEdit.ListNames(definition), [], $"Event \"{name}\"");
        }
    }

    private static void Stage(
        List<MapText> texts, string name, int index, string id, MapTextKind kind, string where, string text, bool required,
        Func<QuestStage, string, QuestStage> with) =>
        texts.Add(new MapText(id, kind, EventsGroup, where, text, true)
        {
            Setter = (e, v) =>
            {
                if (required && string.IsNullOrWhiteSpace(v))
                {
                    return EditResult.InvalidValue;
                }

                return e.Scenario.MapEvents.Set.Find(name) is { } now && index < now.Stages.Count
                    ? e.MapEvents.Replace(name, MapEventTreeEdit.SetStage(now, index, with(now.Stages[index], v)))
                    : EditResult.InvalidTarget;
            },
        });

    private static void Walk(
        GameState scenario, List<MapText> texts, MapEventDefinition definition, IReadOnlyList<IReadOnlyList<EventAction>> lists, IReadOnlyList<string> listNames,
        IReadOnlyList<EventStep> parent, string where)
    {
        for (var l = 0; l < lists.Count; l++)
        {
            for (var i = 0; i < lists[l].Count; i++)
            {
                var action = lists[l][i];
                IReadOnlyList<EventStep> path = [.. parent, new EventStep(l, i)];
                var here = $"{where} › {listNames[l]} {i + 1}";
                var key = $"event.{definition.Name}.{string.Join('/', path.Select(p => $"{p.List}:{p.Index}"))}";
                switch (action)
                {
                    case MessageAction message:
                        Action<MessageAction>(texts, definition.Name, path, $"{key}.text", MapTextKind.EventMessage, $"{here}: message", message.Text, true, true,
                            (a, v) => a with { Text = v });
                        break;
                    case QuestionAction question:
                        Action<QuestionAction>(texts, definition.Name, path, $"{key}.text", MapTextKind.EventQuestion, $"{here}: question", question.Text, true, true,
                            (a, v) => a with { Text = v });
                        for (var k = 0; k < question.Answers.Count; k++)
                        {
                            var answer = k;
                            Action<QuestionAction>(texts, definition.Name, path, $"{key}.answer.{k}", MapTextKind.EventAnswer, $"{here}: answer {k + 1}",
                                question.Answers[k].Text, false, true,
                                (a, v) => answer < a.Answers.Count ? a with { Answers = [.. a.Answers.Select((x, n) => n == answer ? x with { Text = v } : x)] } : null);
                        }

                        break;
                    case DialogueAction dialogue:
                        for (var k = 0; k < dialogue.Lines.Count; k++)
                        {
                            var line = k;
                            var said = dialogue.Lines[k];
                            if (!string.IsNullOrWhiteSpace(said.Speaker) && !scenario.HeroRoster.Contains(said.Speaker)) // a roster hero's id shows its name; none is a narrator
                            {
                                Action<DialogueAction>(texts, definition.Name, path, $"{key}.line.{k}.speaker", MapTextKind.DialogueSpeaker,
                                    $"{here}: line {k + 1} speaker", said.Speaker, false, false,
                                    (a, v) => line < a.Lines.Count ? a with { Lines = [.. a.Lines.Select((x, n) => n == line ? x with { Speaker = v.Trim() } : x)] } : null,
                                    trim: true);
                            }

                            Action<DialogueAction>(texts, definition.Name, path, $"{key}.line.{k}.text", MapTextKind.DialogueLine, $"{here}: line {k + 1}",
                                said.Text, true, true,
                                (a, v) => line < a.Lines.Count ? a with { Lines = [.. a.Lines.Select((x, n) => n == line ? x with { Text = v } : x)] } : null);
                        }

                        break;
                    case EndGameAction end:
                        Action<EndGameAction>(texts, definition.Name, path, $"{key}.text", MapTextKind.EndGameText, $"{here}: end of the game", end.Text, true, false,
                            (a, v) => a with { Text = v });
                        break;
                }

                Walk(scenario, texts, definition, MapEventTreeEdit.Lists(action), MapEventTreeEdit.ListNames(action), path, here);
            }
        }
    }

    private static void Action<T>(
        List<MapText> texts, string name, IReadOnlyList<EventStep> path, string id, MapTextKind kind, string where, string text, bool multiline,
        bool required, Func<T, string, T?> with, bool trim = false)
        where T : EventAction =>
        texts.Add(new MapText(id, kind, EventsGroup, where, text, multiline)
        {
            Setter = (e, v) =>
            {
                if (required && string.IsNullOrWhiteSpace(v))
                {
                    return EditResult.InvalidValue;
                }

                return e.Scenario.MapEvents.Set.Find(name) is { } now && MapEventTreeEdit.Get(now, path) is T action && with(action, v) is { } changed
                    ? e.MapEvents.Replace(name, MapEventTreeEdit.Replace(now, path, changed))
                    : EditResult.InvalidTarget;
            },
            Kept = trim ? Trimmed : text => text,
        });

    private static string Trimmed(string text) => text.Trim();

    /// <summary>The setter's result for an edit that cannot be refused.</summary>
    private static EditResult Done(Action edit)
    {
        edit();
        return EditResult.Ok;
    }
}
