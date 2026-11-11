using System.Collections;
using System.Reflection;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Tests.Battles;

/// <summary>
/// <see cref="BattleState.Copy"/> (the AI's and the auto-combat preview's what-if battles) copies everything (ADR-174): a busy siege battle and its
/// copy agree field by field, the copy shares nothing the battle changes in place, and a field added to a battle type fails here until it is listed
/// — after checking that <c>Copy</c> takes it along.
/// </summary>
public class BattleCopyTests
{
    /// <summary>Every instance field of the copied types; a new one must be copied by <c>Copy()</c> and then added here.</summary>
    private static readonly Dictionary<Type, string> Fields = new()
    {
        [typeof(BattleState)] = "<ActiveUnit>k__BackingField, <Attacker>k__BackingField, <AutoResolve>k__BackingField, <CastRounds>k__BackingField, <CliffsAllowed>k__BackingField, <Creatures>k__BackingField, <Defender>k__BackingField, <Deployer>k__BackingField, <DeploymentAllowed>k__BackingField, <DeploymentColumns>k__BackingField, <EndReason>k__BackingField, <IsAmbush>k__BackingField, <Location>k__BackingField, <ManaLeft>k__BackingField, <NativeTowns>k__BackingField, <PendingStacks>k__BackingField, <Phase>k__BackingField, <Raising>k__BackingField, <Random>k__BackingField, <Risen>k__BackingField, <Round>k__BackingField, <SappedPower>k__BackingField, <SideEffects>k__BackingField, <Siege>k__BackingField, <Spells>k__BackingField, <Terrain>k__BackingField, <TieRolls>k__BackingField, <UsedWarcries>k__BackingField, <WaitedThisRound>k__BackingField, <WarcryRounds>k__BackingField, <Width>k__BackingField, <WidthChooser>k__BackingField, <WidthOptions>k__BackingField, <Winner>k__BackingField, <ZonesMade>k__BackingField, _obstacles, _queue, _units, _waiting, _zones",
        [typeof(BattleUnit)] = "<CopyHits>k__BackingField, <CopySpell>k__BackingField, <CopyUntilRound>k__BackingField, <Count>k__BackingField, <Creature>k__BackingField, <DamageDealt>k__BackingField, <HasRetaliated>k__BackingField, <Height>k__BackingField, <HitsThisRound>k__BackingField, <Id>k__BackingField, <InKeep>k__BackingField, <InitialCount>k__BackingField, <IsCopy>k__BackingField, <IsDefending>k__BackingField, <IsRaised>k__BackingField, <IsRooted>k__BackingField, <IsSummoned>k__BackingField, <LosesNextAction>k__BackingField, <Machine>k__BackingField, <MutableCastsLeft>k__BackingField, <MutableSpells>k__BackingField, <Owner>k__BackingField, <Position>k__BackingField, <RageAttack>k__BackingField, <Revived>k__BackingField, <RoundMorale>k__BackingField, <ShotsLeft>k__BackingField, <SourceSlot>k__BackingField, <Stays>k__BackingField, <StaysPut>k__BackingField, <TookSecondTurn>k__BackingField, <TopHealth>k__BackingField, <Width>k__BackingField",
        [typeof(SiegeState)] = "<Moat>k__BackingField, <Spec>k__BackingField, _structures",
        [typeof(SiegeStructure)] = "<Health>k__BackingField, <Id>k__BackingField, <Kind>k__BackingField, <MaxHealth>k__BackingField, <Rect>k__BackingField",
    };

    private static BattleState BusyBattle()
    {
        var battle = TestBattle.Create(siege: new SiegeSpec(1, 5));
        var archers = battle.Add(BattleSide.Attacker, BuiltInCreatures.Archer, 12, 2, 3);
        var footmen = battle.Add(BattleSide.Attacker, BuiltInCreatures.Footman, 20, 2, 5);
        var imps = battle.Add(BattleSide.Defender, "inferno.imp", 30, 30, 4);
        battle.StartWith(archers);

        var state = battle.State;
        archers.MutableSpells.Add(new ActiveSpell("haste", BattleSide.Attacker, new SpellModifiers(Speed: 3), 4));
        imps.MutableSpells.Add(new ActiveSpell("slow", BattleSide.Attacker, new SpellModifiers(Speed: -2), 3, TurnsLeft: 2));
        imps.Count = 27;
        imps.TopHealth = 2;
        imps.HasRetaliated = true;
        footmen.IsDefending = true;
        archers.ShotsLeft = 9;
        state.ManaLeft[BattleSide.Attacker] = 12;
        state.CastRounds[BattleSide.Attacker] = 1;
        state.Raising[BattleSide.Defender] = new RaiseDead("animate-dead", 50, default);
        state.Risen.Add(imps.Id);
        state.SappedPower[BattleSide.Defender] = 1;
        imps.Stays = true;
        state.WaitedThisRound.Add(footmen.Id);
        state.MutableWaiting.Add(footmen.Id);
        state.SideEffects.Add(new BattleSideEffect(BattleSide.Defender, -1, 90, 3));
        state.UsedWarcries.Add((BattleSide.Attacker, Enum.GetValues<Warcry>()[0]));
        state.WarcryRounds[BattleSide.Attacker] = 1;
        state.Siege!.Structures[0].Health = 1;
        return state;
    }

    [Fact]
    public void TheCopiedTypes_HaveTheListedFields()
    {
        foreach (var (type, listed) in Fields)
        {
            var actual = string.Join(", ", InstanceFields(type).Select(f => f.Name).Order(StringComparer.Ordinal));
            Assert.True(
                listed == actual,
                $"{type.Name}'s fields changed. Check that Copy() copies each new one, then list them here:\n{actual}");
        }
    }

    [Fact]
    public void TheCopy_AgreesFieldByField_AndSharesNothingTheBattleChangesInPlace()
    {
        var battle = BusyBattle();

        var copy = battle.Copy();

        Compare(battle, copy, nameof(BattleState), new HashSet<(object, object)>(ReferencePairComparer.Instance));
    }

    private static IEnumerable<FieldInfo> InstanceFields(Type type)
    {
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
        {
            foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    /// <summary>The battle's own mutable parts: the copied battle types, its random stream and any list, set or dictionary it changes in place.</summary>
    private static bool IsMutable(Type declared) =>
        Fields.ContainsKey(declared) || declared == typeof(GameRandom) || declared.IsSubclassOf(typeof(System.Collections.ObjectModel.Collection<ActiveSpell>))
        || (declared.IsGenericType && declared.GetGenericTypeDefinition() is var g && (g == typeof(List<>) || g == typeof(Dictionary<,>) || g == typeof(HashSet<>)));

    private static void Compare(object? original, object? copy, string path, HashSet<(object, object)> seen)
    {
        if (original is null || copy is null)
        {
            Assert.True(original is null && copy is null, $"{path}: {original ?? "null"} was copied as {copy ?? "null"}.");
            return;
        }

        var type = original.GetType();
        Assert.Equal(type, copy.GetType());
        if (type.IsPrimitive || type.IsEnum || original is string or decimal || ReferenceEquals(original, copy) || !seen.Add((original, copy)))
        {
            Assert.True(Equals(original, copy) || ReferenceEquals(original, copy) || !type.IsValueType, $"{path}: {original} was copied as {copy}.");
            return;
        }

        if (original is IEnumerable items && original is not string)
        {
            var left = items.Cast<object?>().ToList();
            var right = ((IEnumerable)copy).Cast<object?>().ToList();
            Assert.True(left.Count == right.Count, $"{path}: {left.Count} items were copied as {right.Count}.");
            for (var i = 0; i < left.Count; i++)
            {
                Compare(left[i], right[i], $"{path}[{i}]", seen);
            }

            return;
        }

        // Records and other values that define their own equality are compared as wholes; anything else field by field.
        if (type.GetMethod("Equals", [type]) is { DeclaringType: var declaring } && declaring == type)
        {
            Assert.True(original.Equals(copy), $"{path}: {original} was copied as {copy}.");
            return;
        }

        foreach (var field in InstanceFields(type))
        {
            var (a, b) = (field.GetValue(original), field.GetValue(copy));
            if (a is not null && IsMutable(field.FieldType))
            {
                Assert.False(ReferenceEquals(a, b), $"{path}.{field.Name} is shared by the battle and its copy, so playing the copy would change the battle.");
            }

            Compare(a, b, $"{path}.{field.Name}", seen);
        }
    }

    private sealed class ReferencePairComparer : IEqualityComparer<(object, object)>
    {
        public static readonly ReferencePairComparer Instance = new();

        public bool Equals((object, object) x, (object, object) y) => ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);

        public int GetHashCode((object, object) pair) => HashCode.Combine(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Item1), System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Item2));
    }
}
