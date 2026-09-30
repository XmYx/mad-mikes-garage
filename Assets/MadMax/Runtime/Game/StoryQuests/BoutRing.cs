using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Runs a <see cref="NonlethalBout"/> in the game (story system "nonlethal_bout"): the opponent fights the
    /// player for real (melee AI), but every blow either way goes through the bout's score instead of the damage path
    /// (<see cref="MadMax.Npc.Npc.boutHit"/> / <see cref="MadMax.Npc.Npc.boutStrike"/>), so a knockdown ends a round and the
    /// third ends the bout, never a life. Stop states: the player steps out of the ring or gets into a vehicle (yield),
    /// a blade or gun in hand, a vehicle or anyone else hitting the opponent (foul), the player already hurt (the
    /// referee stops it). The opponent is healed and made peaceable again whatever happens, and "&lt;key&gt;:&lt;result&gt;"
    /// is noted. Not saved: a reload ends it (the quest clears any leftover hostility).</summary>
    public class BoutRing : MonoBehaviour
    {
        public static BoutRing Current { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Current = null; Finished = null; }

        public NonlethalBout Bout { get; private set; }
        public MadMax.Npc.Npc Fighter { get; private set; }
        string fighterName, shown;
        float scoreT;
        bool hostileNow;

        /// <summary>Why a bout can't start right now (null = it can).</summary>
        public static string Refuse(WastelandGame g, NonlethalBout b, MadMax.Npc.Npc fighter)
        {
            if (!fighter || !fighter.Alive) return "YOUR OPPONENT ISN'T HERE";
            if (g.Current) return "OUT OF THE CAR FIRST";
            if (g.Stats.health < g.Stats.MaxHealth * NonlethalBout.HurtStop) return "YOU'RE IN NO STATE TO FIGHT: GET PATCHED UP FIRST";
            if (Foul(g) != null) return Foul(g) + ": PUT IT AWAY";
            return null;
        }

        /// <summary>A banned weapon in the player's hand (null = fair).</summary>
        static string Foul(WastelandGame g)
        {
            var t = g.Player ? g.Player.Tool : null;
            if (t is RangedTool) return "NO GUNS IN THE RING";
            if (t is MeleeTool m && m.bleeds > 0f) return "NO BLADES IN THE RING";
            return null;
        }

        public static BoutRing Begin(WastelandGame g, NonlethalBout b, MadMax.Npc.Npc fighter)
        {
            if (Current) Current.End();
            var r = new GameObject("Bout " + b.key).AddComponent<BoutRing>();
            r.Bout = b; r.Fighter = fighter;
            var m = StoryCast.Find(b.fighter);
            r.fighterName = m != null ? m.Value.name.Split(' ')[0] : "HIM";
            fighter.boutHit = r.OnHit;
            fighter.boutStrike = r.OnStrike;
            fighter.Heal();
            r.SetHostile(true);
            Current = r;
            g.Toast("THE BOUT IS ON. " + NonlethalBout.Rules);
            Journal.Add("STORY", "BOUT WITH " + (m != null ? m.Value.name : "THE FIGHTER") + ". " + NonlethalBout.Rules);
            MadMax.Audio.Sfx.Play("bell", StoryAnchors.Get(b.ring) + Vector3.up, 0.9f, 1f, 50f);
            return r;
        }

        void SetHostile(bool on)
        {
            hostileNow = on;
            if (Fighter) Fighter.State.Set(MadMax.Npc.NpcSave.Hostile, on);
        }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || !g.Player || Bout == null) { End(); return; }
            if (!Fighter || !Fighter.Alive) { Bout.Stop(NonlethalBout.Result.Stopped, "THE FIGHT IS OFF"); End(); return; }
            Bout.Tick(Time.deltaTime);
            var ring = StoryAnchors.Get(Bout.ring);
            var me = g.Current ? g.Current.transform.position : g.Player.transform.position;
            if (g.Current) Bout.Stop(NonlethalBout.Result.Foul, "A VEHICLE IN THE RING");
            else if (new Vector2(me.x - ring.x, me.z - ring.z).magnitude > Bout.ringRadius + 0.6f) Bout.Stop(NonlethalBout.Result.Yielded, "YOU STEPPED OUT OF THE RING");
            else if (g.Stats.health < g.Stats.MaxHealth * NonlethalBout.HurtStop) Bout.Stop(NonlethalBout.Result.Stopped, "THE REFEREE STEPS IN: YOU'RE HURT");
            else if (Foul(g) != null) Bout.Stop(NonlethalBout.Result.Foul, Foul(g));
            if (!Bout.Running) { End(); return; }
            // a knockdown: the one standing goes to their corner while the referee counts
            bool fight = !Bout.Down;
            if (fight != hostileNow) SetHostile(fight);
            if (!fight) Fighter.Chat(me, 0.6f);
            if ((scoreT -= Time.deltaTime) <= 0f)
            {
                scoreT = 1.2f;
                string s = Bout.fighterDown ? fighterName + " IS DOWN: THE REFEREE COUNTS" : Bout.playerDown ? "YOU'RE DOWN: GET YOUR BREATH, THE REFEREE COUNTS" : Bout.Score(fighterName);
                if (s != shown) { g.Toast(s); shown = s; }
            }
        }

        /// <summary>Any blow on the fighter while the bout runs: the player's fair ones score, anything else is a foul
        /// (and does no harm, except a vehicle's, which the ring can't stop).</summary>
        bool OnHit(MadMax.Npc.Npc n, Vector3 point, Vector3 dir, float power, GameObject source)
        {
            var g = WastelandGame.Instance;
            if (!g || Bout == null || !Bout.Running) return false;
            if (source && source.GetComponentInParent<MadMax.Vehicles.VehicleDriver>()) { Bout.Stop(NonlethalBout.Result.Foul, "A VEHICLE IN THE RING"); return false; }
            bool byPlayer = source && g.Player && source.transform.IsChildOf(g.Player.transform);
            if (!byPlayer) { Bout.Stop(NonlethalBout.Result.Foul, "SOMEONE ELSE JOINED IN"); return true; }
            if (Foul(g) != null) { Bout.Stop(NonlethalBout.Result.Foul, Foul(g)); return true; }
            if (Bout.PlayerLands(power))
            {
                MadMax.Audio.Sfx.Play("punch", point, 0.8f, Random.Range(0.85f, 1.05f));
                g.Stats.Practice(MadMax.RPG.Skill.Melee, 0.6f);
                if (Bout.fighterDown) { MadMax.Audio.Sfx.Play("crowd_cheer", point, 0.6f); g.Toast(fighterName + " IS DOWN! (" + Bout.fighterDowns + " OF " + NonlethalBout.Knockdowns + ")"); shown = null; }
            }
            return true;
        }

        /// <summary>The fighter's blow landed on the player: wind and breath, never a wound.</summary>
        void OnStrike(MadMax.Npc.Npc n)
        {
            var g = WastelandGame.Instance;
            if (!g || Bout == null || !Bout.FighterLands()) return;
            if (g.cameraRig) g.cameraRig.Shake(1.5f);
            g.Vitals.Spend(Bout.playerDown ? 999f : 8f);
            if (Bout.playerDown) { MadMax.Audio.Sfx.Play("crowd_aww", g.Player.transform.position + Vector3.up, 0.6f); g.Toast("YOU'RE DOWN! (" + Bout.playerDowns + " OF " + NonlethalBout.Knockdowns + ")"); shown = null; }
        }

        /// <summary>Unhook, make peace, note the result.</summary>
        public void End()
        {
            if (Current == this) Current = null;
            if (Fighter)
            {
                Fighter.boutHit = null; Fighter.boutStrike = null;
                SetHostile(false);
                Fighter.Heal();
            }
            var g = WastelandGame.Instance;
            if (Bout != null)
            {
                if (Bout.Running) Bout.Stop(NonlethalBout.Result.Stopped, "THE FIGHT IS OFF");
                string what = Bout.result == NonlethalBout.Result.Won ? "YOU WIN" : Bout.result == NonlethalBout.Result.Lost ? "YOU LOSE, FAIR AND SQUARE"
                    : Bout.result == NonlethalBout.Result.Yielded ? "YOU YIELD" : Bout.result == NonlethalBout.Result.Foul ? "FOUL: NO RESULT" : "STOPPED: NO RESULT";
                if (g) g.Toast("BOUT OVER: " + what + " (" + Bout.reason + ")");
                Journal.Add("STORY", "BOUT OVER: " + what + " (" + Bout.reason + "). LANDED " + Bout.blowsLanded + ", TOOK " + Bout.blowsTaken + ".");
                MadMax.Audio.Sfx.Play("bell", StoryAnchors.Get(Bout.ring) + Vector3.up, 0.9f, 0.9f, 50f);
                MadMax.Story.Story.Note(Bout.Event);
                var b = Bout; Bout = null;
                Finished = b;
            }
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (Fighter && Fighter.boutStrike != null) { Fighter.boutHit = null; Fighter.boutStrike = null; SetHostile(false); }
        }

        /// <summary>The last bout that ended (tests and quests read its result).</summary>
        public static NonlethalBout Finished { get; private set; }
    }
}
