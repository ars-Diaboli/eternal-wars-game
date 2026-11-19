using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Game;

/// <summary>A map event ended the game for <paramref name="Player"/> (ADR-242): won or lost, with the author's reason (empty: a plain one).</summary>
internal sealed record ScriptedEnd(PlayerId Player, bool Won, string Reason);
