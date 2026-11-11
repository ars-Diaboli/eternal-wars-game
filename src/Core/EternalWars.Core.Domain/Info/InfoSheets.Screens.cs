using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Info;

public static partial class InfoSheets
{
    // Kingdom overview --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A row of the kingdom overview (ADR-077): what it pays (or, for a provider, what a week brings and what waits there), then the town's, the
    /// mine's, the provider's or the hero's own sheet.
    /// </summary>
    public static InfoSheet Income(GameState state, PlayerId viewer, IncomeRow row, bool detailed)
    {
        var pays = row.Kind == IncomeSourceKind.Provider
            ? $"A week brings {row.Detail.Replace(" a week", "", StringComparison.Ordinal)}; waiting there: {Cost(row.Stored)}."
            : $"Pays {Cost(row.Daily)} a day" + (row.Detail.Length > 0 ? $" ({row.Detail})." : ".");
        var source = row switch
        {
            { Hero: { } hero } => Hero(state, state.GetHero(hero), viewer, detailed),
            { Object: { } id } when state.FindObject(id) is { } mapObject => MapObject(state, viewer, mapObject, detailed),
            _ => new InfoSheet(row.Name, Title(row.Kind.ToString()), []),
        };
        return source with { Lines = [pays, .. source.Lines] };
    }

    /// <summary>The overview's totals: what the towns, the mines and the heroes pay a day, and what waits at the providers.</summary>
    public static InfoSheet KingdomTotal(KingdomOverview overview)
    {
        var lines = new List<string>();
        foreach (var (kind, label) in new[] { (IncomeSourceKind.Town, "Towns"), (IncomeSourceKind.Mine, "Mines"), (IncomeSourceKind.Hero, "Heroes") })
        {
            var rows = overview.Rows.Where(r => r.Kind == kind).ToList();
            if (rows.Count > 0)
            {
                lines.Add($"{label} ({N(rows.Count)}): {Cost(rows.Aggregate(ResourceSet.Empty, (total, r) => total + r.Daily))} a day");
            }
        }

        lines.Add($"Every day: {Cost(overview.DailyTotal)}");
        if (overview.StoredTotal != ResourceSet.Empty)
        {
            lines.Add($"Waiting at providers: {Cost(overview.StoredTotal)} (fetch it with a hero or a caravan)");
        }

        return new InfoSheet("Every day", "Kingdom income", lines);
    }

    // Tavern ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A hero the town's tavern offers (ADR-060, ADR-081): a roster hero's class, specialty, stats, starting skills and what each does, its preferred
    /// creature and the troops it brings, the price and why it is refused; a hero of the player's own coming back, as its hero sheet.
    /// </summary>
    public static InfoSheet TavernHero(GameState state, PlayerId viewer, Town town, HeroDefinition offer, bool detailed)
    {
        var hire = TavernRules.CheckHire(state, town, offer.Id) is { } refusal ? $"Cannot be hired: {refusal}" : $"Hire for {Cost(TavernRules.HeroCost)}.";
        if (TavernRules.Returning(state, viewer, offer.Id) is { } back)
        {
            var sheet = Hero(state, back, viewer, detailed);
            return sheet with { Kind = $"{sheet.Kind}, waiting in your taverns", Lines = ["Comes back with its level, skills, spells, artifacts and army.", .. sheet.Lines, hire] };
        }

        var lines = new List<string>
        {
            $"Attack {N(offer.Attack)}   Defense {N(offer.Defense)}   Spell Power {N(offer.SpellPower)}   Knowledge {N(offer.Knowledge)}",
            $"Specialty: {offer.Specialty.Describe(state.Spells)}",
        };
        foreach (var (skill, level) in offer.StartingSkills)
        {
            lines.Add(BuiltInSkills.Contains(skill) ? $"{BuiltInSkills.Get(skill).Name} {level}: {BuiltInSkills.Get(skill).Description}" : $"{skill} {level}");
        }

        var preferred = state.Creatures.TryGet(offer.PreferredCreature, out var creature) ? creature.Name : offer.PreferredCreature;
        lines.Add($"Prefers {preferred}: they get +{N(SkillEffects.PreferredAttack)} Attack, +{N(SkillEffects.PreferredDefense)} Defense and "
                  + $"+{N(SkillEffects.PreferredSpeed)} speed in its army.");
        lines.Add(TavernRules.StarterStack(state, offer) is { } stack ? $"Brings {N(stack.Count)} {state.Creatures.Get(stack.Creature).Name}." : "Comes without an army.");
        lines.Add(hire);
        if (detailed)
        {
            lines.Add($"Id {offer.Id}   Rank {N(TavernRules.RankOf(offer.Town, offer) + 1)} of {N(TavernRules.RankCount(offer.Town))} in its faction's pool");
        }

        return new InfoSheet(offer.Name, $"{offer.Class}, {Title(offer.Town)}{(offer.Town == town.TownType ? " (native)" : "")}, for hire", lines);
    }

    // Decision prompts --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// What a decision prompt is about (ADR-154): the object the hero visits, the stack that offers to join or flees, the hero who levels up or whose
    /// army is full. Null for a prompt with nothing more to show (a map event's message, the battle, the exchange).
    /// </summary>
    public static InfoSheet? Decision(GameState state, PlayerId viewer, PendingDecision decision, bool detailed)
    {
        switch (decision)
        {
            case GuardEncounterDecision guard when state.Creatures.Contains(guard.Creature):
                var sheet = Creature(state, guard.Creature, detailed, new ShownStack(guard.Creature, guard.Count, guard.Count, guard.Count));
                return sheet with { Kind = guard.Offer == GuardOffer.Join ? "Neutral stack, offering to join" : "Neutral stack, trying to flee" };
            case LevelUpDecision or MapEventArmyDecision:
                return Hero(state, state.GetHero(decision.Hero), viewer, detailed);
        }

        var hero = state.GetHero(decision.Hero).Name;
        (MapObjectId Object, string Offer)? visit = decision switch
        {
            TreasureChestDecision d => (d.Chest, $"{N(d.Gold)} gold, or {N(d.Experience)} experience for {hero}."),
            TrainingDecision d => (d.Site, $"{hero} may train here once{(d.CanDecline ? "" : " (it is paid for)")}."),
            DepositDecision d => (d.Deposit, $"Opening it as a mine costs {Cost(d.Price)}."),
            CartographerDecision d => (d.Cartographer, $"Sells the map of the {(d.Level == 0 ? "surface" : "underground")} for {N(d.Price)} gold."),
            SphinxDecision d => (d.Sphinx, $"Asks: {d.Question}"),
            PandorasBoxDecision d => (d.Box, "Nobody knows what waits inside, or what guards it."),
            WitchHutDecision d => (d.Hut, $"Teaches {hero} one of: {string.Join(", ", d.Options.Select(s => BuiltInSkills.Get(s).Name))}."),
            TownGateDecision d => (d.Gate, $"Leads to {string.Join(", ", d.Towns.Select(t => t.Available ? t.Name : $"{t.Name} (entrance blocked)"))}."),
            _ => null,
        };
        if (visit is not { } v || state.FindObject(v.Object) is not { } mapObject)
        {
            return null;
        }

        // The hero is visiting it: what it offers now stands for "visit it to see"; an arena box shows what it holds instead (ARN-2 c).
        var shown = MapObject(state, viewer, mapObject, detailed);
        var lines = shown.Lines.Where(l => l != VisitToSee).ToList();
        if (decision is not PandorasBoxDecision || AdventureIntel.ArenaContents(state, mapObject).Count == 0)
        {
            lines.Insert(0, v.Offer);
        }

        return shown with { Lines = lines };
    }

    /// <summary>A skill a hero is offered (a level-up or a witch hut): the level it would learn, what it does, the hero's specialty in it.</summary>
    public static InfoSheet SkillChoice(Hero hero, string skill, bool detailed)
    {
        var current = hero.Skills.Level(skill);
        var next = current + 1;
        var sheet = Skill(skill, next, detailed, hero);
        var max = BuiltInSkills.Get(skill).MaxLevel;
        var first = $"{hero.Name} would learn it to {next} of {max}" + (current > SkillLevel.None ? $" (now {current})." : ".");
        return sheet with { Lines = [first, .. sheet.Lines.Skip(1)] };
    }
}
