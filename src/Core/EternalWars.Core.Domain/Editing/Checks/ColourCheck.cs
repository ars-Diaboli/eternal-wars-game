using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The coloured objects, colour by colour: Eyes of the Magi (ADR-070) at most four a colour, and a window needs an eye to show; border posts need a
/// Keymaster's Tent of their colour (ADR-071).
/// </summary>
internal sealed class ColourCheck : IMapCheck
{
    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        foreach (var colour in Enum.GetValues<ObjectColour>())
        {
            var eyes = scenario.ObjectsOf<MagiEye>().Count(e => e.Colour == colour);
            if (eyes > MagiWindow.MaxEyes)
            {
                yield return new(IssueSeverity.Error, $"{eyes} {colour.ToString().ToLowerInvariant()} Eyes of the Magi; a colour has at most {MagiWindow.MaxEyes}.");
            }

            foreach (var window in scenario.ObjectsOf<MagiWindow>().Where(w => w.Colour == colour && eyes == 0))
            {
                yield return new(IssueSeverity.Warning, $"{window.Definition.Name} has no Eye of its colour to look through.", window.Position);
            }

            if (!scenario.ObjectsOf<KeymasterTent>().Any(t => t.Colour == colour))
            {
                foreach (var post in scenario.ObjectsOf<BorderPost>().Where(p => p.Colour == colour))
                {
                    yield return new(IssueSeverity.Warning, $"{post.Definition.Name} has no Keymaster's Tent of its colour: nobody can pass.", post.Position);
                }
            }
        }
    }
}
