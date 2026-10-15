using EW.CoreLib.Interfaces;
using EW.CoreLib.Types;

namespace EW.CoreLib;

public partial class GameScenarioController
{
    partial void UpdateAi(double timeDelta);

    public void ProcessAnimations(double timeDelta)
    {
        foreach (var mapObject in Map.PermanentObjects.OfType<IAnimatable>())
            mapObject.ProcessAnimation(timeDelta);

        foreach (var removeable in Map.TemporalObjects.OfType<IAnimatable>())
            removeable.ProcessAnimation(timeDelta);

        foreach (var actor in Map.MapHeroes.OfType<IAnimatable>())
            actor.ProcessAnimation(timeDelta);

        UpdateAi(timeDelta);
    }
}
