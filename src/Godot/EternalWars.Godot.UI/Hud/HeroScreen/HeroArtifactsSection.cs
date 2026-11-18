using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.HeroScreen;

/// <summary>
/// The paper doll and the backpack: click a worn artifact to take it off, a carried one to put it on, or drag it there (ADR-162); assemble
/// relics whose pieces are worn, or take one apart (ADR-121).
/// </summary>
internal sealed class HeroArtifactsSection(HeroPanel panel) : HeroSection(panel)
{
    public GridContainer Doll { get; } = new() { Columns = 4 };

    public HFlowContainer Backpack { get; } = new();

    protected override Node Anchor => Doll;

    public override void Show(Hero hero)
    {
        var state = State;

        // Drag and drop (ADR-162): a worn artifact into the backpack takes it off, a carried one onto any doll slot puts it on.
        var onDoll = new ArtifactPlace(hero.Id, Doll: true);
        var inBackpack = new ArtifactPlace(hero.Id, Doll: false);
        Doll.WithArtifactDrag(null, null, onDoll, Drop);
        Backpack.WithArtifactDrag(null, null, inBackpack, Drop);
        Doll.ClearChildren();
        foreach (var slot in ArtifactSlots.All)
        {
            var worn = hero.Artifacts.Worn(slot);
            var blocked = hero.Artifacts.AllBlocked.Where(b => b.Slot == slot).Select(b => state.Artifacts.Get(b.Relic).Name).ToList();
            for (var i = 0; i < ArtifactSlots.Capacity(slot); i++)
            {
                var artifact = i < worn.Count ? state.Artifacts.Get(worn[i]) : null;
                var blocker = artifact is null && i - worn.Count < blocked.Count ? blocked[i - worn.Count] : null;
                var button = new Button
                {
                    Text = $"{slot}: {artifact?.Name ?? (blocker is null ? "—" : $"(held by {blocker})")}",
                    TooltipText = artifact is null ? "" : ArtifactText(artifact),
                    Disabled = artifact is null,
                    CustomMinimumSize = new Vector2(UiTheme.DollSlotWidth, 0),
                    ClipText = true,
                };
                var id = artifact?.Id;
                button.WithArtifactDrag(id is null ? null : new ArtifactInHand(hero.Id, id, Worn: true), artifact?.Name, onDoll, Drop);
                if (id is not null)
                {
                    button.Pressed += () => Run(new UnequipArtifactCommand(State.CurrentPlayer.Id, hero.Id, id));
                    button.WithInfo(detailed => InfoSheets.Artifact(State, id, detailed));
                }

                Doll.AddChild(button);
            }
        }

        // Assembled relics (ADR-121): join the worn pieces, or take a relic apart, worn or carried.
        var player = state.CurrentPlayer.Id;
        foreach (var relic in ArtifactService.Assemblable(state.Artifacts, hero))
        {
            var id = relic.Id;
            var join = new Button { Text = $"Assemble {relic.Name}", TooltipText = ArtifactText(relic) };
            join.Pressed += () => Run(new AssembleRelicCommand(player, hero.Id, id));
            Doll.AddChild(join);
        }

        foreach (var id in hero.Artifacts.AllWorn.Select(w => w.Id).Concat(hero.Artifacts.Backpack).Distinct().Where(a => state.Artifacts.Get(a).IsAssembled))
        {
            var relicId = id;
            var apart = new Button { Text = $"Take apart {state.Artifacts.Get(id).Name}" };
            apart.Pressed += () => Run(new TakeRelicApartCommand(player, hero.Id, relicId));
            Doll.AddChild(apart);
        }

        Backpack.ClearChildren();
        if (hero.Artifacts.Backpack.Count == 0)
        {
            Backpack.AddChild(new Label { Text = "    Empty." });
        }

        foreach (var id in hero.Artifacts.Backpack)
        {
            var artifact = state.Artifacts.Get(id);
            var button = new Button { Text = $"{artifact.Name} ({artifact.Slot})", TooltipText = ArtifactText(artifact) };
            var artifactId = artifact.Id;
            button.Pressed += () => Run(new EquipArtifactCommand(State.CurrentPlayer.Id, hero.Id, artifactId));
            button.WithInfo(detailed => InfoSheets.Artifact(State, artifactId, detailed))
                .WithArtifactDrag(new ArtifactInHand(hero.Id, artifactId, Worn: false), artifact.Name, inBackpack, Drop);
            Backpack.AddChild(button);
        }
    }

    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) => OnArtifacts(refresh, events);

    private string ArtifactText(ArtifactDefinition artifact)
    {
        var artifacts = State.Artifacts;
        return $"{artifact.Name} ({(artifact.IsAssembled ? "assembled relic" : artifact.Tier.ToString())}, {artifact.Slot})\n{ArtifactBonuses.Of(artifacts.EffectsOf(artifact)).Describe(State.Spells)}"
               + (artifact.IsAssembled ? $"\nPieces: {string.Join(", ", artifact.Pieces.Select(p => artifacts.Get(p).Name))}" : "")
               + (artifacts.RelicOf(artifact.Id) is { } relic ? $"\nA piece of {artifacts.Get(relic).Name}" : "")
               + (artifact.Set is { } set ? $"\nSet: {artifacts.GetSet(set).Name}" : "")
               + (artifact.Notes.Length > 0 ? $"\n{artifact.Notes}" : "");
    }

    /// <summary>An artifact dragged and let go on the doll or in the backpack: put on or taken off (<see cref="ArtifactDrag.MoveCommand"/>).</summary>
    private void Drop(ArtifactInHand item, ArtifactPlace place)
    {
        if (ArtifactDrag.MoveCommand(State.CurrentPlayer.Id, item, place) is { } command)
        {
            Run(command);
        }
    }
}
