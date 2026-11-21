using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Domain;

public class ZzLinksHammerTests
{
    private static int linksSeen; private static readonly Dictionary<string, int> heads = [];
    private static readonly string[] Kinds =
    [
        BuiltInMapObjects.TwoWayMonolith, BuiltInMapObjects.TwoWayMonolith, BuiltInMapObjects.OneWayMonolithEntrance, BuiltInMapObjects.OneWayMonolithEntrance,
        BuiltInMapObjects.OneWayMonolithExit, BuiltInMapObjects.SeerHut, BuiltInMapObjects.QuestGuard, BuiltInMapObjects.QuestGate,
        BuiltInMapObjects.PandorasBox, BuiltInMapObjects.PandorasBox, BuiltInMapObjects.TreasureChest, "town", "monster", "monster", "hero",
    ];

    [Fact]
    public void Hammer()
    {
        var problems = new Dictionary<string, string>();
        var counts = new Dictionary<string, int>();
        var checks = 0;
        linksSeen = 0;
        void Note(string kind, string detail)
        {
            counts[kind] = counts.GetValueOrDefault(kind) + 1;
            problems.TryAdd(kind, detail);
        }

        for (var seed = 1; seed <= 400; seed++)
        {
            var random = new Random(seed);
            var editor = new MapEditorSession(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);
            var everId = new List<int> { 9999, 0, -1 };
            var everHero = new List<int> { 777 };
            var names = new List<string>();
            MapPosition Slot() => new(2 + (4 * random.Next(0, 9)), 2 + (4 * random.Next(0, 6)));
            int AnyId() => everId[random.Next(everId.Count)];
            EventCondition Condition(int depth)
            {
                var roll = random.Next(depth > 2 ? 3 : 7);
                return roll switch
                {
                    0 => new MonsterDefeatedCondition(AnyId()),
                    1 => new TownOwnerCondition(AnyId(), null),
                    2 => new HeroDefeatedCondition(everHero[random.Next(everHero.Count)]),
                    3 => new AllCondition([.. Enumerable.Range(0, random.Next(0, 4)).Select(_ => Condition(depth + 1))]),
                    4 => new AnyCondition([.. Enumerable.Range(0, random.Next(0, 4)).Select(_ => Condition(depth + 1))]),
                    5 => new NotCondition(Condition(depth + 1)),
                    _ => new PlayerIsHumanCondition(),
                };
            }

            for (var step = 0; step < 70; step++)
            {
                var op = random.Next(16);
                var objects = editor.Scenario.Objects.ToList();
                var what = "?";
                try
                {
                    switch (op)
                    {
                        case 0 or 1 or 2 or 3:
                            var kind = Kinds[random.Next(Kinds.Length)];
                            what = "place " + kind;
                            if (kind == "monster")
                            {
                                if (editor.Monsters.Place(Slot(), BuiltInCreatures.Wolf, 10, GuardDisposition.Aggressive, false, out var m) == EditResult.Ok)
                                {
                                    everId.Add(m.Value);
                                }
                            }
                            else if (kind == "hero")
                            {
                                if (editor.Heroes.Place(new PlayerId(1), Slot(), "H" + step, out var h) == EditResult.Ok)
                                {
                                    everHero.Add(h.Value);
                                }
                            }
                            else
                            {
                                var definition = kind == "town" ? BuiltInMapObjects.TownId(BuiltInMapObjects.PlayableTowns.First()) : kind;
                                if (editor.Objects.Place(definition, Slot(), out var id) == EditResult.Ok)
                                {
                                    everId.Add(id.Value);
                                }
                            }

                            break;
                        case 4 or 5:
                            what = "link";
                            if (objects.Count > 1)
                            {
                                var (a, b) = (objects[random.Next(objects.Count)], objects[random.Next(objects.Count)]);
                                editor.Objects.LinkGates(a.Id, b.Id);
                                editor.Objects.SetGateExit(a.Id, random.Next(4) == 0 ? null : b.Id);
                            }

                            break;
                        case 6 or 7:
                            what = "event";
                            var name = "e" + names.Count;
                            var questKind = random.Next(2) == 0;
                            var definitionE = questKind
                                ? new MapEventDefinition(name, MapEventKind.Quest, [])
                                {
                                    Stages = [.. Enumerable.Range(0, random.Next(1, 4)).Select(_ => new QuestStage(Condition(0), "p", random.Next(2) == 0 ? [] : [new IfAction(Condition(1), [new IfAction(Condition(2), [])])]))],
                                }
                                : new MapEventDefinition(name, MapEventKind.Hero, [new IfAction(Condition(0), [new WhileAction(Condition(1), [])]), new IfAction(Condition(0), [])]);
                            if (editor.MapEvents.Add(definitionE) == EditResult.Ok)
                            {
                                names.Add(name);
                            }

                            break;
                        case 8 or 9:
                            what = "bind";
                            if (names.Count > 0 && objects.Count > 0)
                            {
                                var target = objects[random.Next(objects.Count)];
                                var name2 = random.Next(6) == 0 ? null : names[random.Next(names.Count)];
                                editor.Contents.SetQuestSiteQuest(target.Id, name2);
                                editor.Contents.SetPandorasBoxEvent(target.Id, name2);
                                if (name2 is not null)
                                {
                                    editor.MapEvents.SetCell(new MapPosition(random.Next(40), random.Next(30)), name2);
                                }
                            }

                            break;
                        case 10:
                            what = "remove object";
                            if (objects.Count > 0)
                            {
                                editor.Objects.Remove(objects[random.Next(objects.Count)].Id);
                            }

                            break;
                        case 11:
                            what = "remove hero/event/move";
                            var heroes = editor.Scenario.Heroes.ToList();
                            var r = random.Next(4);
                            if (r == 0 && heroes.Count > 0)
                            {
                                editor.Heroes.Remove(heroes[random.Next(heroes.Count)].Id);
                            }
                            else if (r == 1 && names.Count > 0)
                            {
                                editor.MapEvents.Remove(names[random.Next(names.Count)]);
                            }
                            else if (r == 2 && objects.Count > 0)
                            {
                                editor.Objects.Move(objects[random.Next(objects.Count)].Id, Slot());
                            }
                            else if (heroes.Count > 0)
                            {
                                editor.Heroes.Move(heroes[random.Next(heroes.Count)].Id, Slot());
                            }

                            break;
                        case 12 or 13:
                            what = "undo";
                            for (var i = random.Next(1, 4); i > 0; i--)
                            {
                                editor.Undo();
                            }

                            break;
                        case 14:
                            what = "redo";
                            for (var i = random.Next(1, 4); i > 0; i--)
                            {
                                editor.Redo();
                            }

                            break;
                        default:
                            what = "resize/shift";
                            if (random.Next(2) == 0)
                            {
                                editor.Shift(random.Next(-3, 4), random.Next(-3, 4));
                            }
                            else
                            {
                                editor.Resize(random.Next(24, 44), random.Next(20, 34));
                            }

                            break;
                    }
                }
                catch (Exception e)
                {
                    Note("EDITOR THREW in " + what, $"seed {seed} step {step}: {e.GetType().Name} {e.Message}");
                }

                checks++;
                Check(editor, $"seed {seed} step {step} after {what}", Note);
            }
        }

        Assert.True(problems.Count == 0, $"checks {checks}, links {linksSeen}\n" + string.Join("\n", problems.Select(p => $"[{counts[p.Key]}x] {p.Key}: {p.Value}")));
        Assert.True(linksSeen > 10000, $"only {linksSeen} links seen in {checks} checks");
    }

    private static void Check(MapEditorSession editor, string where, Action<string, string> note)
    {
        var s = editor.Scenario;
        var set = s.MapEvents.Set;
        var all = new Dictionary<MapObjectId, IReadOnlyList<MapLink>>();
        var ofHero = new Dictionary<HeroId, IReadOnlyList<MapLink>>();
        foreach (var o in s.Objects)
        {
            try
            {
                all[o.Id] = MapLinks.Of(s, o);
            }
            catch (Exception e)
            {
                note("THREW object", $"{where}: {o} {e}");
                all[o.Id] = [];
            }
        }

        foreach (var h in s.Heroes)
        {
            try
            {
                ofHero[h.Id] = MapLinks.Of(s, h);
            }
            catch (Exception e)
            {
                note("THREW hero", $"{where}: {e}");
                ofHero[h.Id] = [];
            }
        }

        void Basic(string owner, MapObjectId? self, IReadOnlyList<MapLink> links)
        {
            linksSeen += links.Count; foreach (var l0 in links) { var k0 = l0.Text.Split(new[] { " \"", ":" }, StringSplitOptions.None)[0].Split(" #")[0]; k0 = k0.Length > 12 ? k0[..12] : k0; heads[k0] = heads.GetValueOrDefault(k0) + 1; }
            if (links.Distinct().Count() != links.Count)
            {
                note("duplicate link", $"{where}: {owner}: {string.Join(" | ", links.Select(l => l.Text))}");
            }

            if (links.Select(l => (l.Object, l.Hero, l.Position, Head: l.Text.Split(' ')[0])).Distinct().Count() != links.Count)
            {
                note("same target twice under one heading", $"{where}: {owner}: {string.Join(" | ", links.Select(l => l.Text))}");
            }

            foreach (var l in links)
            {
                if (!s.Map.Contains(l.Position))
                {
                    note("position off map", $"{where}: {owner}: {l}");
                }

                if (l.Object is { } id)
                {
                    if (s.FindObject(id) is not { } target)
                    {
                        note("object missing", $"{where}: {owner}: {l}");
                    }
                    else if (target.VisitPosition != l.Position)
                    {
                        note("object position wrong", $"{where}: {owner}: {l}");
                    }

                    if (id == self)
                    {
                        note("links to itself", $"{where}: {owner}: {l.Text}");
                    }

                    if (l.Hero is not null)
                    {
                        note("both object and hero", $"{where}: {l}");
                    }
                }
                else if (l.Hero is { } hid)
                {
                    if (s.Heroes.FirstOrDefault(h => h.Id == hid) is not { } hero)
                    {
                        note("hero missing", $"{where}: {owner}: {l}");
                    }
                    else if (hero.Position != l.Position)
                    {
                        note("hero position wrong", $"{where}: {owner}: {l}");
                    }
                }
                else if (!set.Cells.Any(c => c.Position == l.Position))
                {
                    note("cell missing", $"{where}: {owner}: {l}");
                }
            }
        }

        foreach (var o in s.Objects)
        {
            var links = all[o.Id];
            Basic(o.ToString()!, o.Id, links);

            // Gates: the neighbours are the target (when on the map) and whoever targets me.
            var expected = s.Objects.Where(x => x.TeleportTarget == o.Id && x.Id != o.Id).Select(x => x.Id).ToHashSet();
            if (o.TeleportTarget is { } t && s.FindObject(t) is not null && t != o.Id)
            {
                expected.Add(t);
            }

            var gateLinks = links.Where(l => l.Text.StartsWith("Leads to", StringComparison.Ordinal) || l.Text.StartsWith("Entered from", StringComparison.Ordinal)).ToList();
            if (!gateLinks.Select(l => l.Object!.Value).ToHashSet().SetEquals(expected) || gateLinks.Count != expected.Count)
            {
                note("gate links differ from oracle", $"{where}: {o}: got [{string.Join(" | ", gateLinks.Select(l => l.Text))}] expected [{string.Join(",", expected)}]");
            }

            // Quest site forward links: exactly the existing things its quest's conditions name.
            if (o is QuestSite { Quest: { } quest } && set.Find(quest) is { } definition)
            {
                var named = Targets(s, definition);
                var got = links.Where(l => l.Text.StartsWith("Quest target", StringComparison.Ordinal)).Select(l => (l.Object, l.Hero)).ToHashSet();
                if (!got.SetEquals(named))
                {
                    note("quest targets differ from oracle", $"{where}: {o}: got {string.Join(",", got)} expected {string.Join(",", named)}");
                }
            }
            else if (links.Any(l => l.Text.StartsWith("Quest target", StringComparison.Ordinal)))
            {
                note("quest target without quest", $"{where}: {o}");
            }

            if (o is PandorasBox { Event: { } run })
            {
                var cells = set.Cells.Where(c => c.Event == run).Select(c => c.Position).ToHashSet();
                var got = links.Where(l => l.Text.StartsWith("Also run", StringComparison.Ordinal)).Select(l => l.Position).ToHashSet();
                if (!got.SetEquals(cells))
                {
                    note("box cells differ", $"{where}: {o}");
                }
            }
        }

        foreach (var h in s.Heroes)
        {
            Basic("hero " + h.Name, null, ofHero[h.Id]);
        }

        // Reverse: everything an event names links to every runner of the event; and nothing else is "Named by".
        var expectedBack = new Dictionary<(MapObjectId? Object, HeroId? Hero), HashSet<(string Event, MapObjectId? Runner, MapPosition? Cell)>>();
        foreach (var definition in set.Events)
        {
            foreach (var target in Targets(s, definition))
            {
                if (!expectedBack.TryGetValue(target, out var back))
                {
                    expectedBack[target] = back = [];
                }

                foreach (var runner in s.Objects.Where(x => (x is QuestSite q && q.Quest == definition.Name) || (x is PandorasBox b && b.Event == definition.Name)))
                {
                    if (runner.Id != target.Object)
                    {
                        back.Add((definition.Name, runner.Id, null));
                    }
                }

                foreach (var cell in set.Cells.Where(c => c.Event == definition.Name))
                {
                    back.Add((definition.Name, null, cell.Position));
                }
            }
        }

        void Back((MapObjectId? Object, HeroId? Hero) key, IReadOnlyList<MapLink> links, string owner)
        {
            var got = links.Where(l => l.Text.StartsWith("Named by", StringComparison.Ordinal))
                .Select(l => (Event: l.Text.Split('"')[1], Runner: l.Object, Cell: l.Object is null ? l.Position : (MapPosition?)null)).ToHashSet();
            var expected = expectedBack.GetValueOrDefault(key) ?? [];
            if (!got.SetEquals(expected))
            {
                note("named-by differs from oracle", $"{where}: {owner}: got [{string.Join(" | ", links.Select(l => l.Text))}] expected [{string.Join(",", expected)}]");
            }
        }

        foreach (var o in s.Objects)
        {
            Back((o.Id, null), all[o.Id], o.ToString()!);
        }

        foreach (var h in s.Heroes)
        {
            Back((null, h.Id), ofHero[h.Id], "hero " + h.Name);
        }
    }

    private static HashSet<(MapObjectId? Object, HeroId? Hero)> Targets(EternalWars.Core.Domain.Game.GameState s, MapEventDefinition definition)
    {
        var result = new HashSet<(MapObjectId? Object, HeroId? Hero)>();
        foreach (var c in MapEventTree.Conditions(definition))
        {
            switch (c)
            {
                case MonsterDefeatedCondition m when s.FindObject(new MapObjectId(m.Monster)) is { } o:
                    result.Add((o.Id, null));
                    break;
                case TownOwnerCondition t when s.FindObject(new MapObjectId(t.Town)) is { } o:
                    result.Add((o.Id, null));
                    break;
                case HeroDefeatedCondition h when s.Heroes.FirstOrDefault(x => x.Id.Value == h.Hero) is { } hero:
                    result.Add((null, hero.Id));
                    break;
            }
        }

        return result;
    }
}

public class ZzPicturesTests
{
    [Fact]
    public void Distinct()
    {
        var browser = new EternalWars.Core.Domain.Editing.BrowserCatalog(EternalWars.Core.Domain.Objects.MapObjectCatalog.Default, EternalWars.Core.Domain.Creatures.CreatureCatalog.Default, EternalWars.Core.Domain.Artifacts.ArtifactCatalog.Default);
        var rendered = browser.Entries.Where(e => e.Definition is { } d
            && e.Placement != EternalWars.Core.Domain.Editing.BrowserPlacement.Monster && e.Placement != EternalWars.Core.Domain.Editing.BrowserPlacement.Hero && e.Artifact is null
            && !(d.Kind == EternalWars.Core.Domain.Objects.MapObjectKind.Town && d.Town is not null)
            && !(d.Kind == EternalWars.Core.Domain.Objects.MapObjectKind.ResourcePile && d.Resource is not null)).ToList();
        string Key(EternalWars.Core.Domain.Objects.MapObjectDefinition d) =>
            (d.Colour?.ToString() ?? d.Kind.ToString()) + ":" + string.Join(";", d.Footprint.OccupiedCells.Select(c => $"{c.Dx},{c.Dy},{c.Cell}"));
        var groups = rendered.GroupBy(e => Key(e.Definition!)).OrderByDescending(g => g.Count()).ToList();
        Assert.Fail($"entries {browser.Entries.Count}, rendered {rendered.Count}, distinct pictures at most {groups.Count}, alone {groups.Count(g => g.Count() == 1)}\n"
            + string.Join("\n", groups.Take(8).Select(g => $"{g.Count()}x {g.Key}: {string.Join(", ", g.Take(6).Select(e => e.Name))}")));
    }
}
