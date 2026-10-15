namespace EW.CoreLib;

public class UiHost
{
    public GamePlayer HostPlayer { get; set; }
    public Hero? SelectedHero { get; private set; }
    public Town? SelectedTown { get; private set; }

    public void SelectHero(Hero hero)
    {
        SelectedHero = hero;
        SelectedTown = null;
    }
    
    public void SelectTown(Town town)
    {
        SelectedTown = town;
        SelectedHero = null;
    }
}