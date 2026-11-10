using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// The visit rules of the objects that give something (<c>MapObjectInteractionService.VisitRules</c>): piles, chests, loot, banks, buffs,
/// training, shrines, schools and the objects that offer a decision for a price or a riddle.
/// </summary>
internal sealed class MapObjectRewardVisits(
    ObjectInteractionContext context,
    BattleService battles,
    ProgressionService progression,
    ArtifactService artifacts)
{
    private Game.GameState State => context.State;

    private Core.Events.IEventPublisher Events => context.Events;

    internal void CollectPile(Hero hero, ResourcePile pile)
    {
        context.RemoveFromMap(pile);
        var chance = SkillEffects.ProspectingPileChancePercent(hero.SkillLevelOf(BuiltInSkills.Prospecting));
        var inPile = pile.Amount ?? ResourcePile.Roll(pile.Resource, State.Random);
        var amount = chance > 0 && State.Random.Next(100) < chance ? inPile + (inPile + 1) / 2 : inPile;
        context.Economy.Grant(hero.Owner, ResourceSet.Of(pile.Resource, amount));
        Events.Publish(new ResourcePileCollected(hero.Id, pile.Id, pile.Resource, amount));
    }

    internal void OpenChest(Hero hero, TreasureChest chest)
    {
        var values = chest.FixedValues ?? WeightedRoll.Pick(TreasureChest.Odds, State.Random);
        if (values is null)
        {
            // The artifact outcome (ADR-068): no choice, the chest is emptied; a full backpack loses it.
            context.RemoveFromMap(chest);
            var found = artifacts.Draw(ArtifactTier.Minor) is { } minor && artifacts.Grant(hero, minor, ArtifactSource.Found) ? minor : null;
            Events.Publish(new LootFound(hero.Id, chest.Id, ResourceSet.Empty, found));
            return;
        }

        context.Ask(new TreasureChestDecision(hero.Id, chest.Id, values.Gold, values.Experience));
    }

    internal void PickUp(Hero hero, ArtifactPickup pickup)
    {
        if (pickup.Artifact is { } found && artifacts.Grant(hero, found, ArtifactSource.Pickup))
        {
            context.RemoveFromMap(pickup);
        }
    }

    internal void OpenBox(Hero hero, PandorasBox box)
    {
        if (box.IsGuarded && hero.Army.IsEmpty)
        {
            Events.Publish(new MapObjectRefused(hero.Id, box.Id, $"{hero.Name} has no army to face what may guard the box."));
        }
        else
        {
            context.Ask(new PandorasBoxDecision(hero.Id, box.Id));
        }
    }

    /// <summary>
    /// A creature bank (ADR-068): the Ruins show their surroundings on every visit; a full bank means a fight; a plundered one gives nothing, and the
    /// Churchyard's costs the next battle's morale.
    /// </summary>
    internal void EnterBank(Hero hero, CreatureBank bank)
    {
        if (bank.Rule == BankRule.Crypt)
        {
            Events.Publish(new SurroundingsRevealed(hero.Id, bank.Id, hero.Owner, bank.VisitPosition, CreatureBankRules.CryptRevealRadius));
        }

        if (bank.IsGuarded)
        {
            battles.BeginBankBattle(hero, bank);
            return;
        }

        Events.Publish(new MapObjectFoundEmpty(hero.Id, bank.Id));
        if (bank.Rule == BankRule.Churchyard)
        {
            progression.GrantStatus(hero, new HeroStatus(bank.Definition.Id, HeroStatusKind.Morale, CreatureBankRules.ChurchyardMorale, StatusExpiry.NextBattle));
        }
    }

    /// <summary>
    /// A buff site (ADR-068): statuses for the next battle or the week, movement points for today, the Magic Well's mana. The Stables also upgrade
    /// Lancers on every visit, whether the hero may take the movement again or not.
    /// </summary>
    internal void Bless(Hero hero, BuffSite site)
    {
        if (site.Rule == BuffRule.Stables)
        {
            UpgradeAtStables(hero);
        }

        if (!context.TryCollect(hero, site))
        {
            Events.Publish(new MapObjectFoundEmpty(hero.Id, site.Id));
            return;
        }

        foreach (var status in BuffRules.Statuses(site.Definition, State.Date))
        {
            progression.GrantStatus(hero, status);
        }

        if (BuffRules.MovementNow(site.Rule) is > 0 and var points)
        {
            hero.MovementPoints += points;
            Events.Publish(new HeroMovementGained(hero.Id, points, hero.MovementPoints));
        }

        if (BuffRules.ManaAfter(site.Rule, hero.Mana, hero.MaxMana) is var mana && mana > hero.Mana)
        {
            hero.Mana = mana;
            Events.Publish(new HeroManaRestored(hero.Id, hero.Mana, hero.MaxMana));
        }
    }

    private void UpgradeAtStables(Hero hero)
    {
        if (!State.Creatures.TryGet(BuffRules.StablesCreature, out var lancer) || lancer.UpgradesTo is not { } upgrade)
        {
            return;
        }

        var stacks = hero.Army.Stacks.Where(s => s.Stack.Creature == lancer.Id).ToList();
        foreach (var (slot, stack) in stacks)
        {
            hero.Army.Set(slot, new CreatureStack(upgrade, stack.Count));
        }

        if (stacks.Count > 0)
        {
            Events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        }
    }

    /// <summary>
    /// A training site (ADR-068): once per hero. The Learning Stone and the Library give at once (the Library only from level 10); the others ask
    /// through a <see cref="TrainingDecision"/>, and a paid one turns the hero away when the player cannot pay any price.
    /// </summary>
    internal void Train(Hero hero, TrainingSite site)
    {
        if (!site.CanCollect(hero.Id, State.Date))
        {
            Events.Publish(new MapObjectFoundEmpty(hero.Id, site.Id));
            return;
        }

        switch (site.Rule)
        {
            case TrainingRule.LearningStone:
                context.TryCollect(hero, site);
                progression.GrantExperience(hero, TrainingRules.LearningStoneExperience);
                return;
            case TrainingRule.LibraryOfEnlightenment when hero.Level < TrainingRules.LibraryLevel:
                Events.Publish(new MapObjectRefused(hero.Id, site.Id, $"Only heroes of level {TrainingRules.LibraryLevel} or more may study here."));
                return;
            case TrainingRule.LibraryOfEnlightenment:
                context.TryCollect(hero, site);
                foreach (var stat in Enum.GetValues<PrimaryStat>())
                {
                    progression.RaiseStat(hero, stat, TrainingRules.LibraryStats);
                }

                return;
            case TrainingRule.DreamTeacher:
                var others = State.ObjectsOf<TrainingSite>().Count(t => t != site && t.Rule == TrainingRule.DreamTeacher && t.Visits.Heroes.Contains(hero.Id));
                context.TryCollect(hero, site);
                progression.GrantExperience(hero, TrainingRules.DreamTeacher(others));
                return;
            case var rule when TrainingRules.SingleStat(rule) is { } single:
                context.TryCollect(hero, site);
                progression.RaiseStat(hero, single, 1);
                return;
        }

        var options = TrainingRules.Options(site.Rule, State.GetPlayer(hero.Owner).Resources);
        if (options.Count == 0)
        {
            Events.Publish(new MapObjectRefused(hero.Id, site.Id, "Not enough resources to pay."));
            return;
        }

        context.Ask(new TrainingDecision(hero.Id, site.Id, options, TrainingRules.CanDecline(site.Rule)));
    }

    /// <summary>
    /// A loot site (ADR-068): the first visitor finds what it rolls, vanishing sites go; later visitors find nothing. The Warrior's Tomb costs
    /// every visitor morale for the next battle, the first one too.
    /// </summary>
    internal void Search(Hero hero, LootSite site)
    {
        if (context.TryCollect(hero, site))
        {
            var roll = LootRules.Roll(site.Rule, State.Random);
            if (site.Vanishes)
            {
                context.RemoveFromMap(site);
            }

            context.Economy.Grant(hero.Owner, roll.Resources);
            var found = roll.Artifact is { } tier && artifacts.Draw(tier) is { } artifact && artifacts.Grant(hero, artifact, ArtifactSource.Found) ? artifact : null;
            Events.Publish(new LootFound(hero.Id, site.Id, roll.Resources, found));
        }
        else
        {
            Events.Publish(new MapObjectFoundEmpty(hero.Id, site.Id));
        }

        if (site.Rule == LootRule.WarriorsTomb)
        {
            progression.GrantStatus(hero, new HeroStatus(site.Definition.Id, HeroStatusKind.Morale, LootRules.TombMorale, StatusExpiry.NextBattle));
        }
    }

    /// <summary>A deposit offers itself for its price; a player who cannot pay is told the price (ADR-070, MAP-21).</summary>
    internal void OfferDeposit(Hero hero, Deposit deposit)
    {
        if (!State.GetPlayer(hero.Owner).Resources.CanAfford(deposit.Price))
        {
            Events.Publish(new MapObjectRefused(hero.Id, deposit.Id, $"Opening the {deposit.Definition.Name.ToLowerInvariant()} costs {ResourceProviderRules.Describe(deposit.Price)}."));
            return;
        }

        context.Ask(new DepositDecision(hero.Id, deposit.Id, deposit.Price));
    }

    internal void OfferMap(Hero hero, Cartographer cartographer)
    {
        if (cartographer.Level >= State.Map.Levels)
        {
            Events.Publish(new MapObjectRefused(hero.Id, cartographer.Id, "This map has no underground to chart."));
        }
        else if (cartographer.Buyers.Contains(hero.Owner))
        {
            Events.Publish(new MapObjectFoundEmpty(hero.Id, cartographer.Id));
        }
        else if (!State.GetPlayer(hero.Owner).Resources.CanAfford(new ResourceSet(Gold: Cartographer.Price)))
        {
            Events.Publish(new MapObjectRefused(hero.Id, cartographer.Id, $"The cartographer's map costs {Cartographer.Price} gold."));
        }
        else
        {
            context.Ask(new CartographerDecision(hero.Id, cartographer.Id, cartographer.Level, Cartographer.Price));
        }
    }

    /// <summary>Each hero gets one riddle per sphinx: the author's, or one from the table; the right answer's place among the three is rolled.</summary>
    internal void AskRiddle(Hero hero, Sphinx sphinx)
    {
        if (!sphinx.CanCollect(hero.Id, State.Date))
        {
            Events.Publish(new MapObjectFoundEmpty(hero.Id, sphinx.Id));
            return;
        }

        var riddle = sphinx.Riddle ?? RiddleTable.Default[State.Random.Next(RiddleTable.Default.Count)];
        var correct = State.Random.Next(3);
        var options = new List<string> { riddle.Wrong1, riddle.Wrong2 };
        options.Insert(correct, riddle.Answer);
        context.Ask(new SphinxDecision(hero.Id, sphinx.Id, riddle.Question, options, correct));
    }

    /// <summary>A shrine teaches its spell to a hero who can learn it and does not know it yet (ADR-120); otherwise it says why not.</summary>
    internal void Worship(Hero hero, Shrine shrine)
    {
        if (shrine.Spell is not { } id || !State.Spells.TryGet(id, out var spell))
        {
            return;
        }

        if (hero.Spells.ContainsKey(id))
        {
            Events.Publish(new MapObjectFoundEmpty(hero.Id, shrine.Id));
            return;
        }

        if (!Magic.MagicRules.CanLearn(hero, spell))
        {
            var why = hero.HasSpellBook ? $"{hero.Name} needs the {BuiltInSkills.Get(Magic.MagicRules.MagicSkill(spell.School)).Name} skill at level {spell.Level} to learn {spell.Name}."
                : $"{hero.Name} needs a spell book to learn {spell.Name}.";
            Events.Publish(new MapObjectRefused(hero.Id, shrine.Id, why));
            return;
        }

        hero.Learn(id);
        Events.Publish(new SpellsLearned(hero.Id, shrine.Id, [id]));
    }

    internal void OfferSkills(Hero hero, WitchHut hut)
    {
        var options = WitchHutRules.Options(hero, hut, State.Rules.Bans);
        if (hero.WitchHutLevel == hero.Level)
        {
            Events.Publish(new MapObjectRefused(hero.Id, hut.Id, $"{hero.Name} learned at a witch hut at this level already."));
        }
        else if (options.Count == 0)
        {
            Events.Publish(new MapObjectRefused(hero.Id, hut.Id, $"The witch has nothing to teach {hero.Name}."));
        }
        else
        {
            context.Ask(new WitchHutDecision(hero.Id, hut.Id, options));
        }
    }

    /// <summary>
    /// A hero's first visit to a University or War Academy draws its lessons, kept for that hero at that school (ADR-139); later visits show the
    /// same ones. A hero with nothing to learn there is told so.
    /// </summary>
    internal void DrawLessons(Hero hero, SkillSchool school)
    {
        if (!school.Lessons.TryGetValue(hero.Id, out var lessons))
        {
            lessons = SkillSchoolRules.Draw(hero, ProgressionService.TownOf(State, hero), school.TeachesMagic, State.Random, State.Rules.Bans);
            school.Lessons[hero.Id] = lessons;
            Events.Publish(new LessonsDrawn(school.Id, hero.Id, lessons));
        }

        if (lessons.Count == 0)
        {
            Events.Publish(new MapObjectRefused(hero.Id, school.Id, $"The {school.Definition.Name.ToLowerInvariant()} has nothing to teach {hero.Name}."));
        }
    }
}
