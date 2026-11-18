using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The map's bans (ADR-126): each must name something that exists; what the author placed by hand that a ban names stays in the game, so it is
/// pointed out (a hero of a banned type or with banned artifacts, spells or skills, a prisoner, an artifact on the map, a shrine's spell, a guild's
/// fixed spell, a witch hut's skill).
/// </summary>
internal sealed class BansCheck : IMapCheck
{
    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        var bans = scenario.Rules.Bans;
        if (bans.IsEmpty)
        {
            yield break;
        }

        foreach (var (kind, ids, known) in new (string, IReadOnlyList<string>, Func<string, bool>)[]
                 {
                     ("hero", bans.Heroes, scenario.HeroRoster.Contains), ("artifact", bans.Artifacts, scenario.Artifacts.Contains),
                     ("spell", bans.Spells, id => scenario.Spells.TryGet(id, out _)), ("skill", bans.Skills, Heroes.BuiltInSkills.Contains),
                 })
        {
            foreach (var id in ids.Where(i => !known(i)))
            {
                yield return new(IssueSeverity.Error, $"The bans name the unknown {kind} '{id}'.");
            }
        }

        const string Kept = "(placed by hand, so it stays)";
        foreach (var hero in scenario.Heroes)
        {
            if (!hero.IsPlaceholder && hero.Definition is { } type && bans.BansHero(type))
            {
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} is of a banned type {Kept}.", hero.Position);
            }

            foreach (var artifact in hero.Artifacts.AllWorn.Select(w => w.Id).Concat(hero.Artifacts.Backpack).Where(bans.BansArtifact).Distinct())
            {
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} carries the banned artifact '{artifact}' {Kept}.", hero.Position);
            }

            foreach (var spell in hero.IsPlaceholder ? [] : hero.Spells.Keys.Where(bans.BansSpell))
            {
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} knows the banned spell '{spell}' {Kept}.", hero.Position);
            }

            foreach (var skill in hero.IsPlaceholder ? [] : hero.Skills.Levels.Keys.Where(bans.BansSkill))
            {
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} has the banned skill '{skill}': it keeps it, but it is never offered.", hero.Position);
            }
        }

        foreach (var mapObject in scenario.Objects)
        {
            var name = mapObject.Definition.Name;
            var banned = mapObject switch
            {
                Prison { Hero: { } prisoner } when bans.BansHero(prisoner) => $"holds the banned hero '{prisoner}'",
                ArtifactPickup { Artifact: { } artifact } when bans.BansArtifact(artifact) => $"is the banned artifact '{artifact}'",
                Shrine { Spell: { } spell } when bans.BansSpell(spell) => $"teaches the banned spell '{spell}'",
                WitchHut hut when hut.Skills.FirstOrDefault(bans.BansSkill) is { } skill => $"teaches the banned skill '{skill}'",
                Town town when town.FixedGuildSpells.FirstOrDefault(bans.BansSpell) is { } spell => $"has the banned spell '{spell}' fixed in its guild",
                PandorasBox box when box.Contents.Artifacts.FirstOrDefault(bans.BansArtifact) is { } artifact => $"holds the banned artifact '{artifact}'",
                _ => null,
            };
            if (banned is not null)
            {
                yield return new(IssueSeverity.Warning, $"{(mapObject is Town t ? t.Name : name)} {banned} {Kept}.", mapObject.Position);
            }
        }
    }
}
