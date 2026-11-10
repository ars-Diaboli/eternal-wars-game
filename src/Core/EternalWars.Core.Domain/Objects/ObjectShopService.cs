using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// What the hero on a map object's entrance does there from its panel: recruiting, buying, upgrading, hiring, sacrificing, taking lessons and
/// moving garrison troops.
/// </summary>
public sealed class ObjectShopService(ObjectInteractionContext context, ProgressionService progression, ArtifactService artifacts)
{
    private Game.GameState State => context.State;

    /// <summary>A hero on a map dwelling's entrance buys creatures into its army, discounted by its own Stewardship (ADR-054).</summary>
    internal void RecruitAtDwelling(PlayerId player, MapObjectId dwellingId, int count)
    {
        var dwelling = State.FindObject(dwellingId) as CreatureDwelling ?? throw new CommandRefusedException($"{dwellingId} is not a dwelling.");
        if (State.PendingDecision is not null)
        {
            throw new CommandRefusedException("Resolve the pending decision first.");
        }

        var hero = State.HeroAt(dwelling.VisitPosition);
        if (hero is null || hero.Owner != player)
        {
            throw new CommandRefusedException($"No own hero stands at the {dwelling.Definition.Name}.");
        }

        var available = dwelling.Available ?? 0;
        if (count < 1 || count > available)
        {
            throw new CommandRefusedException($"Only {available} creatures wait here.");
        }

        var creature = State.Creatures.Get(dwelling.Creature);
        if (!hero.Army.CanAccept(creature.Id))
        {
            throw new CommandRefusedException($"{hero.Name} has no free slot for {creature.Name}.");
        }

        var price = StewardshipRules.DiscountCreatures(creature.Cost, hero.SkillLevelOf(BuiltInSkills.Stewardship)) * count;
        if (!context.Economy.TrySpend(player, price))
        {
            throw new CommandRefusedException("Not enough resources.");
        }

        dwelling.Available = available - count;
        hero.Army.TryAdd(new CreatureStack(creature.Id, count));
        context.Events.Publish(new DwellingRecruited(dwelling.Id, hero.Id, creature.Id, count, dwelling.Available.Value));
        context.Events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    /// <summary>The hero on a hill fort's entrance upgrades one of its stacks of tiers 1–3 for 150 % of the price difference (ADR-068, MAP-11).</summary>
    internal void UpgradeAtHillFort(PlayerId player, MapObjectId fortId, int slot)
    {
        var hero = context.HeroAt<HillFort>(player, fortId, out var fort);
        var stack = (Army.IsValidSlot(slot) ? hero.Army[slot] : null) ?? throw new CommandRefusedException("The slot is empty.");
        var upgraded = HillFortRules.UpgradeOf(State.Creatures, stack.Creature)
                       ?? throw new CommandRefusedException("Only stacks of tiers 1–3 that have an upgrade are upgraded here.");
        if (!context.Economy.TrySpend(player, HillFortRules.Cost(State.Creatures, stack)))
        {
            throw new CommandRefusedException("Not enough resources.");
        }

        hero.Army.Set(slot, new CreatureStack(upgraded.Id, stack.Count));
        context.Events.Publish(new HillFortUpgraded(fort.Id, hero.Id, slot, stack.Creature, upgraded.Id, stack.Count));
        context.Events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    /// <summary>The hero on a War Machine Factory's entrance buys a machine it lacks at the blacksmith's price (ADR-068, MAP-13).</summary>
    internal void BuyAtFactory(PlayerId player, MapObjectId factoryId, Battles.WarMachineSlot slot)
    {
        var hero = context.HeroAt<WarMachineFactory>(player, factoryId, out var factory);
        if (hero.WarMachines.Contains(slot))
        {
            throw new CommandRefusedException($"{hero.Name} already has that machine.");
        }

        if (!context.Economy.TrySpend(player, Battles.WarMachines.Cost(slot)))
        {
            throw new CommandRefusedException("Not enough gold.");
        }

        hero.AddWarMachine(slot);
        context.Events.Publish(new WarMachineBought(factory.Id, hero.Id, slot));
    }

    /// <summary>
    /// The owner's hero on a garrison's entrance moves a whole stack between its army and the garrison (ADR-070); the hero keeps at least one
    /// stack, and the stack joins one of the same creature or a free slot.
    /// </summary>
    internal void MoveGarrisonStack(PlayerId player, MapObjectId garrisonId, int slot, bool toGarrison)
    {
        var hero = context.HeroAt<MapGarrison>(player, garrisonId, out var garrison);
        if (garrison.Owner != player)
        {
            throw new CommandRefusedException("Only the garrison's owner moves troops in or out.");
        }

        var (from, to) = toGarrison ? (hero.Army, garrison.Troops) : (garrison.Troops, hero.Army);
        var stack = (Army.IsValidSlot(slot) ? from[slot] : null) ?? throw new CommandRefusedException("The slot is empty.");
        if (toGarrison && hero.Army.Stacks.Count() == 1)
        {
            throw new CommandRefusedException($"{hero.Name} must keep at least one stack.");
        }

        if (!to.CanAccept(stack.Creature))
        {
            throw new CommandRefusedException("There is no free slot for that stack.");
        }

        from.Set(slot, null);
        to.TryAdd(stack);
        context.Events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        context.Events.Publish(new GarrisonChanged(garrison.Id, garrison.Troops.Slots.ToList()));
    }

    /// <summary>
    /// The hero on a map tavern's entrance hires one of its offers for its player (ADR-070, ADR-071): the hero limit applies, and the new hero
    /// needs a free cell next to the entrance; it brings a starter stack and moves from the player's next turn on.
    /// </summary>
    internal void HireAtMapTavern(PlayerId player, MapObjectId tavernId, string heroId)
    {
        context.HeroAt<MapTavern>(player, tavernId, out var tavern);
        if (MapTavernRules.Offers(State, tavern).FirstOrDefault(h => h.Id == heroId) is not { } definition)
        {
            throw new CommandRefusedException($"{heroId} is not offered here this week.");
        }

        if (State.HeroLimitReached(player))
        {
            throw new CommandRefusedException($"You may have at most {State.HeroLimit(player)} heroes.");
        }

        var arrival = MapTavernRules.Arrival(State, tavern) ?? throw new CommandRefusedException("There is no free cell next to the tavern.");
        if (!context.Economy.TrySpend(player, TavernRules.HeroCost))
        {
            throw new CommandRefusedException($"A hero costs {TavernRules.HeroCost}.");
        }

        var hero = Hero.Create(State.NextUnusedHeroId(), player, arrival, null, definition);
        if (TavernRules.StarterStack(State, definition) is { } starter)
        {
            hero.Army.TryAdd(starter);
        }

        hero.MaxMovementPoints = MovementAllowance.Daily(State, hero);
        hero.MovementPoints = hero.MaxMovementPoints; // like a hero hired in a town
        State.AddHero(hero);
        context.Events.Publish(new HeroHired(tavern.Id, hero.Id, definition.Id));
        context.Events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    /// <summary>The hero on a University's or War Academy's entrance takes one of its lessons for <see cref="SkillSchoolRules.Price"/> (ADR-139).</summary>
    internal void TakeLesson(PlayerId player, MapObjectId schoolId, string skill)
    {
        var hero = context.HeroAt<SkillSchool>(player, schoolId, out var school);
        var lesson = SkillSchoolRules.LessonsOf(school, hero.Id).FirstOrDefault(l => l.Skill == skill)
                     ?? throw new CommandRefusedException($"The {school.Definition.Name.ToLowerInvariant()} does not offer {hero.Name} that skill.");
        switch (SkillSchoolRules.StateOf(hero, lesson))
        {
            case LessonState.Learned:
                throw new CommandRefusedException($"{hero.Name} knows {BuiltInSkills.Get(skill).Name} at that level already.");
            case LessonState.NoRoom:
                throw new CommandRefusedException($"{hero.Name} learns at most {HeroSkills.MaxGroups} skill groups.");
        }

        if (!context.Economy.TrySpend(player, SkillSchoolRules.Price))
        {
            throw new CommandRefusedException("Not enough resources.");
        }

        hero.Skills.Raise(skill);
        context.Events.Publish(new LessonTaken(school.Id, hero.Id, skill, hero.Skills.Level(skill), SkillSchoolRules.Price));
    }

    /// <summary>The hero on an artifact merchant's entrance buys the artifact of a slot for twice its value (ADR-079, MAP-33).</summary>
    internal void BuyArtifact(PlayerId player, MapObjectId merchantId, int slot)
    {
        var hero = context.HeroAt<ArtifactMerchant>(player, merchantId, out var merchant);
        var artifact = (slot >= 0 && slot < merchant.Stock.Count ? merchant.Stock[slot] : null)
                       ?? throw new CommandRefusedException("Nothing is for sale in that slot.");
        if (hero.Artifacts.BackpackFull)
        {
            throw new CommandRefusedException($"{hero.Name}'s backpack is full.");
        }

        var price = ArtifactMerchantRules.Price(State.Artifacts.Get(artifact));
        if (!context.Economy.TrySpend(player, price))
        {
            throw new CommandRefusedException("Not enough gold.");
        }

        merchant.Stock[slot] = null;
        artifacts.Grant(hero, artifact, ArtifactSource.Bought);
        context.Events.Publish(new ArtifactBought(merchant.Id, hero.Id, slot, artifact, price));
    }

    /// <summary>The hero on a refugee camp's entrance buys its creatures at the normal price (ADR-079, MAP-30; no Stewardship discount).</summary>
    internal void RecruitAtRefugeeCamp(PlayerId player, MapObjectId campId, int count)
    {
        var hero = context.HeroAt<RefugeeCamp>(player, campId, out var camp);
        if (camp.Creature is not { } id || count < 1 || count > camp.Available)
        {
            throw new CommandRefusedException($"Only {camp.Available} creatures wait here.");
        }

        var creature = State.Creatures.Get(id);
        if (!hero.Army.CanAccept(id))
        {
            throw new CommandRefusedException($"{hero.Name} has no free slot for {creature.Name}.");
        }

        if (!context.Economy.TrySpend(player, creature.Cost * count))
        {
            throw new CommandRefusedException("Not enough resources.");
        }

        camp.Available -= count;
        hero.Army.TryAdd(new CreatureStack(id, count));
        context.Events.Publish(new DwellingRecruited(camp.Id, hero.Id, id, count, camp.Available));
        context.Events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    /// <summary>
    /// Creatures of an army slot go on the altar for twice their hit points in experience (ADR-079, MAP-29): Darkness and Wild heroes only, and the
    /// hero keeps at least one creature *(choice)*.
    /// </summary>
    internal void SacrificeCreatures(PlayerId player, MapObjectId altarId, int slot, int count)
    {
        var hero = context.HeroAt<SacrificialAltar>(player, altarId, out var altar);
        if (!SacrificeRules.For(ProgressionService.TownOf(State, hero)).HasFlag(SacrificeKind.Creatures))
        {
            throw new CommandRefusedException($"{hero.Name} cannot sacrifice creatures.");
        }

        var stack = (Army.IsValidSlot(slot) ? hero.Army[slot] : null) ?? throw new CommandRefusedException("The slot is empty.");
        if (count < 1 || count > stack.Count)
        {
            throw new CommandRefusedException($"The stack has {stack.Count} creatures.");
        }

        if (count == hero.Army.Stacks.Sum(s => s.Stack.Count))
        {
            throw new CommandRefusedException($"{hero.Name} must keep at least one creature.");
        }

        hero.Army.Set(slot, count == stack.Count ? null : new CreatureStack(stack.Creature, stack.Count - count));
        var experience = SacrificeRules.Experience(State.Creatures.Get(stack.Creature), count);
        context.Events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        context.Events.Publish(new SacrificeMade(altar.Id, hero.Id, experience, stack.Creature, count));
        progression.GrantExperience(hero, experience);
    }

    /// <summary>An artifact the hero wears or carries goes on the altar for 2000 / 5000 / 10 000 experience by tier (Light and Wild heroes only).</summary>
    internal void SacrificeArtifact(PlayerId player, MapObjectId altarId, string artifact)
    {
        var hero = context.HeroAt<SacrificialAltar>(player, altarId, out var altar);
        if (!SacrificeRules.For(ProgressionService.TownOf(State, hero)).HasFlag(SacrificeKind.Artifacts))
        {
            throw new CommandRefusedException($"{hero.Name} cannot sacrifice artifacts.");
        }

        if (!hero.Artifacts.Has(artifact))
        {
            throw new CommandRefusedException($"{hero.Name} has no '{artifact}'.");
        }

        if (State.Artifacts.Get(artifact).IsGrail)
        {
            throw new CommandRefusedException("The Grail is not given up on an altar.");
        }

        if (State.Artifacts.Get(artifact).IsAssembled)
        {
            // The altar takes pieces only (ADR-108, ADR-121).
            throw new CommandRefusedException($"Take {State.Artifacts.Get(artifact).Name} apart first: the altar takes its pieces only.");
        }

        if (!hero.Artifacts.RemoveFromBackpack(artifact))
        {
            hero.Artifacts.RemoveWorn(artifact);
            State.RefreshArtifacts(hero);
        }

        var experience = SacrificeRules.Experience(State.Artifacts.Get(artifact));
        context.Events.Publish(new HeroArtifactsChanged(hero.Id));
        context.Events.Publish(new SacrificeMade(altar.Id, hero.Id, experience, Artifact: artifact));
        progression.GrantExperience(hero, experience);
    }
}
