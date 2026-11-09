using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>An artifact being moved on the hero or exchange screen: whose, which, and whether it is the worn copy.</summary>
public sealed record ArtifactInHand(HeroId Hero, string Id, bool Worn);

/// <summary>Where an artifact is dropped: a hero's paper doll (put on) or its backpack (carry).</summary>
public sealed record ArtifactPlace(HeroId Hero, bool Doll);

/// <summary>
/// Drag and drop of artifacts (ADR-162) with Godot's control drag: a worn or carried artifact's button is pressed and dragged, a label with its
/// name follows the pointer, and letting go over a paper-doll slot or a backpack button moves it there. What a drop does is the screen's: the
/// same commands as its clicks. A drop the screen would ignore (back where it came from) shows the "no drop" pointer.
/// </summary>
public static class ArtifactDrag
{
    /// <summary>Whether moving <paramref name="item"/> to <paramref name="place"/> does something: to the other hero always, else on or off.</summary>
    public static bool Accepts(ArtifactInHand item, ArtifactPlace place) => item.Hero != place.Hero || item.Worn != place.Doll;

    /// <summary>
    /// The command that moves an artifact: onto another hero's paper doll (it puts it on) or into its backpack (<see cref="GiveArtifactCommand"/>);
    /// onto the own doll (<see cref="EquipArtifactCommand"/>) or into the own backpack (<see cref="UnequipArtifactCommand"/>); none when it would
    /// stay where it is.
    /// </summary>
    public static GameCommand? MoveCommand(PlayerId player, ArtifactInHand item, ArtifactPlace place) =>
        item.Hero != place.Hero ? new GiveArtifactCommand(player, item.Hero, item.Id, place.Hero, item.Worn, Wear: place.Doll)
        : place.Doll && !item.Worn ? new EquipArtifactCommand(player, place.Hero, item.Id)
        : !place.Doll && item.Worn ? new UnequipArtifactCommand(player, place.Hero, item.Id)
        : null;

    /// <summary>Lets <paramref name="control"/> be dragged when it shows an artifact (<paramref name="item"/>, named <paramref name="name"/>), and
    /// be a drop target for <paramref name="place"/> (the "no drop" pointer where <see cref="Accepts"/> says no).</summary>
    /// <param name="dropped">Runs the move.</param>
    public static T WithArtifactDrag<T>(this T control, ArtifactInHand? item, string? name, ArtifactPlace place,
        Action<ArtifactInHand, ArtifactPlace> dropped) where T : Control
    {
        control.SetDragForwarding(
            Callable.From<Vector2, Variant>(_ =>
            {
                if (item is null)
                {
                    return default;
                }

                control.SetDragPreview(Preview(name ?? item.Id));
                return Variant.From(new ArtifactDragPayload { Item = item });
            }),
            Callable.From<Vector2, Variant, bool>((_, data) => data.Obj is ArtifactDragPayload { Item: { } held } && Accepts(held, place)),
            Callable.From<Vector2, Variant>((_, data) =>
            {
                if (data.Obj is ArtifactDragPayload { Item: { } held })
                {
                    dropped(held, place);
                }
            }));
        return control;
    }

    private static Control Preview(string name)
    {
        var panel = new PanelContainer { Modulate = new Color(1, 1, 1, 0.9f), Position = new Vector2(12, 12) }; // beside the pointer, not under it
        panel.UseOpaqueBackground();
        panel.AddChild(new Label { Text = name });
        return panel;
    }
}

/// <summary>What Godot carries during an artifact drag: the artifact in hand.</summary>
internal sealed partial class ArtifactDragPayload : RefCounted
{
    public ArtifactInHand? Item { get; init; }
}
