using MadMax.Building;

namespace MadMax.Game
{
    /// <summary>Depth stage I game hooks: the defence tallies (mines gone off, people they took, tripwires pulled) are
    /// saved in <c>SaveData.blockDefence</c> as "key=value" lines, with a journal note the first time a mine takes
    /// someone. The pieces run themselves (<see cref="Landmine"/>, <see cref="Tripwire"/>, <see cref="MotorGate"/>,
    /// <see cref="Watchtower"/>, <see cref="GunNest"/>).</summary>
    public partial class WastelandGame
    {
        int defenceKillsNoted;

        partial void DefenceUpdate()
        {
            if (DefenceWorks.Kills > defenceKillsNoted)
            {
                if (defenceKillsNoted == 0) Journal.Add("BASE", "ONE OF YOUR MINES TOOK SOMEONE DOWN");
                defenceKillsNoted = DefenceWorks.Kills;
            }
        }

        partial void DefenceSave(SaveData d)
        {
            d.blockDefence.Clear();
            d.blockDefence.Add("blasts=" + DefenceWorks.Blasts);
            d.blockDefence.Add("kills=" + DefenceWorks.Kills);
            d.blockDefence.Add("trips=" + DefenceWorks.Trips);
        }

        partial void DefenceLoad(SaveData d)
        {
            DefenceWorks.Blasts = DefenceWorks.Kills = DefenceWorks.Trips = 0;
            if (d.blockDefence != null)
                foreach (var line in d.blockDefence)
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0 || !int.TryParse(line.Substring(eq + 1), out int n)) continue;
                    switch (line.Substring(0, eq))
                    {
                        case "blasts": DefenceWorks.Blasts = n; break;
                        case "kills": DefenceWorks.Kills = n; break;
                        case "trips": DefenceWorks.Trips = n; break;
                    }
                }
            defenceKillsNoted = DefenceWorks.Kills;
        }

        partial void DefenceNewGame()
        {
            DefenceWorks.Blasts = DefenceWorks.Kills = DefenceWorks.Trips = 0;
            defenceKillsNoted = 0;
        }
    }
}
