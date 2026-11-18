using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// What the end of a battle does to a guard (ADR-216): the guard describes it (<see cref="IBattleGuard"/>), <see cref="BattleGuardService"/>
/// applies it. A guard that won (or whose attacker fled or surrendered) keeps what lived; a beaten one leaves, falls to the winner or pays out.
/// </summary>
internal abstract record GuardOutcome;

/// <summary>The guard stays as it was (a creature bank's, a Pandora's Box's); views redraw it.</summary>
internal sealed record GuardUnchanged(MapObject Guard) : GuardOutcome;

/// <summary>The monster keeps the creatures that lived.</summary>
internal sealed record MonsterSurvives(MonsterGuard Monster) : GuardOutcome;

/// <summary>A mine's or a dwelling's guard stack keeps the creatures that lived.</summary>
internal sealed record SiteGuardSurvives(MapObject Site) : GuardOutcome;

/// <summary>The troops of a town or a map garrison (<paramref name="Holder"/>) keep what lived, slot by slot.</summary>
internal sealed record TroopsSurvive(MapObject Holder, Army Troops) : GuardOutcome;

/// <summary>The beaten monster leaves the map.</summary>
internal sealed record MonsterBeaten(MonsterGuard Monster) : GuardOutcome;

/// <summary>The beaten guard leaves the mine (an abandoned one becomes a normal mine), and the mine is the winner's.</summary>
internal sealed record MineGuardBeaten(Mine Mine) : GuardOutcome;

/// <summary>The beaten guard leaves the dwelling, and the dwelling is the winner's.</summary>
internal sealed record DwellingGuardBeaten(CreatureDwelling Dwelling) : GuardOutcome;

/// <summary>The bank's reward goes to the winner and the bank is empty until it refills (ADR-068).</summary>
internal sealed record BankPlundered(CreatureBank Bank) : GuardOutcome;

/// <summary>Winning opens the box for the hero (ADR-100).</summary>
internal sealed record BoxWon(PandorasBox Box) : GuardOutcome;

/// <summary>Beating the garrison of a town without a hero captures the town.</summary>
internal sealed record TownWon(Town Town) : GuardOutcome;

/// <summary>The beaten troops of a map garrison are gone, and the garrison is flagged for the winner (ADR-070).</summary>
internal sealed record GarrisonWon(MapGarrison Garrison) : GuardOutcome;
