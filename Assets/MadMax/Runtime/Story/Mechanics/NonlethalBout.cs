using UnityEngine;

namespace MadMax.Story
{
    /// <summary>A supervised bout (story system "nonlethal_bout"): explicit rules, a score instead of wounds, and real
    /// stop states. Each side has WIND (100): a landed blow costs the one who took it some of it; at zero they are down
    /// (a knockdown: the referee counts, no blows land, they get up with their wind back). The third knockdown ends it.
    /// So does stepping out of the ring (a yield), a foul (a blade, a gun, a vehicle, anyone else joining in) and the
    /// referee stopping it when someone is genuinely hurt. Scoring never touches anyone's health, and nobody dies: the
    /// game side (<see cref="MadMax.Game.BoutRing"/>) takes the fighter's blows off the damage path entirely.</summary>
    public class NonlethalBout
    {
        public enum Result { Running, Won, Lost, Yielded, Stopped, Foul }

        public const float Wind = 100f, DownSeconds = 4f, BlowOnPlayer = 17f, BlowOnFighter = 26f;
        public const int Knockdowns = 3;
        /// <summary>The player's real health (fraction of max) below which the referee stops it (and won't start it).</summary>
        public const float HurtStop = 0.45f;
        public const string Rules = "RULES: PADDED BLOWS ONLY, NO BLADES, NO GUNS, NOBODY ELSE JOINS IN. THREE KNOCKDOWNS WINS. STEP OUT OF THE RING TO YIELD. THE REFEREE STOPS IT IF ANYONE IS HURT.";

        /// <summary>Event prefix: "&lt;key&gt;:won", ":lost", ":yielded", ":stopped", ":foul" when it ends.</summary>
        public string key;
        /// <summary>Cast key of the opponent; the ring's anchor and radius (m).</summary>
        public string fighter, ring;
        public float ringRadius = 3.8f;
        /// <summary>Blows on the fighter count this much (a seasoned one takes more).</summary>
        public float fighterGuard = 1f;

        public float playerWind = Wind, fighterWind = Wind;
        /// <summary>Knockdowns each side has taken.</summary>
        public int playerDowns, fighterDowns;
        public bool playerDown, fighterDown;
        public float downFor;
        public int blowsLanded, blowsTaken;
        public Result result;
        public string reason;

        public bool Running => result == Result.Running;
        public bool Down => playerDown || fighterDown;

        /// <summary>A blow the player landed with a tool of strike <paramref name="power"/>; false while someone is down.</summary>
        public bool PlayerLands(float power)
        {
            if (!Running || Down) return false;
            blowsLanded++;
            fighterWind -= Mathf.Clamp(power, 0.2f, 1.6f) * BlowOnFighter * fighterGuard;
            if (fighterWind > 0f) return true;
            fighterWind = 0f; fighterDowns++; fighterDown = true; downFor = DownSeconds;
            if (fighterDowns >= Knockdowns) Stop(Result.Won, "THIRD KNOCKDOWN");
            return true;
        }

        /// <summary>A blow the fighter landed on the player.</summary>
        public bool FighterLands()
        {
            if (!Running || Down) return false;
            blowsTaken++;
            playerWind -= BlowOnPlayer;
            if (playerWind > 0f) return true;
            playerWind = 0f; playerDowns++; playerDown = true; downFor = DownSeconds;
            if (playerDowns >= Knockdowns) Stop(Result.Lost, "THIRD KNOCKDOWN");
            return true;
        }

        /// <summary>Counts a knockdown out and lets the one down get up with their wind back; the player gets their
        /// breath back slowly between blows.</summary>
        public void Tick(float dt)
        {
            if (!Running) return;
            if (downFor > 0f)
            {
                downFor -= dt;
                if (downFor > 0f) return;
                if (fighterDown) { fighterDown = false; fighterWind = Wind; }
                if (playerDown) { playerDown = false; playerWind = Wind; }
                return;
            }
            playerWind = Mathf.Min(Wind, playerWind + 1.5f * dt);
        }

        public void Stop(Result r, string why)
        {
            if (!Running) return;
            result = r; reason = why;
        }

        /// <summary>One HUD line: both sides' wind and knockdowns.</summary>
        public string Score(string fighterName) =>
            "BOUT  YOU " + Mathf.CeilToInt(playerWind) + (playerDowns > 0 ? " (DOWN " + playerDowns + ")" : "") + "   " + fighterName + " " + Mathf.CeilToInt(fighterWind) + (fighterDowns > 0 ? " (DOWN " + fighterDowns + ")" : "");

        public string Event => key + ":" + result.ToString().ToLowerInvariant();
    }
}
