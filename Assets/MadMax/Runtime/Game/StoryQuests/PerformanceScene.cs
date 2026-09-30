using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Plays a <see cref="Performance"/> in the running game (story system "performance"): the performers line
    /// up across the stage facing out, the cast audience stands at the front, townsfolk walk in and fill the rows
    /// behind, then the beats run in order (a speech bubble and a toast for each line, the crowd calling out, sounds at
    /// the stage) and "&lt;key&gt;:done" is noted after the last. It pauses, saying why, while the player is away, a
    /// performer isn't there, a required radio is off or the quest holds it; nothing runs out. Afterwards the guests
    /// drift home. One at a time; not saved (a quest restarts it from the top after a reload).</summary>
    public class PerformanceScene : MonoBehaviour
    {
        public static PerformanceScene Current { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Current = null;

        public const float Away = 45f;
        public Performance Show { get; private set; }
        /// <summary>The beat playing (-1 while everyone gathers).</summary>
        public int Beat { get; private set; } = -1;
        public bool Finished { get; private set; }
        /// <summary>Why it is paused right now (null = playing).</summary>
        public string Waiting { get; private set; }
        /// <summary>The generated guests watching.</summary>
        public IReadOnlyList<MadMax.Npc.Npc> Guests => guests;

        readonly List<MadMax.Npc.Npc> guests = new List<MadMax.Npc.Npc>();
        float beatLeft = 3f, arrangeT, linger;
        string lastWait;
        bool released;

        /// <summary>Start (or keep) the show; a different one running is ended first.</summary>
        public static PerformanceScene Begin(WastelandGame g, Performance p)
        {
            if (Current && Current.Show != null && Current.Show.key == p.key && !Current.Finished) return Current;
            if (Current) Current.End();
            var s = new GameObject("Performance " + p.key).AddComponent<PerformanceScene>();
            s.Show = p;
            Current = s;
            if (g) g.Toast(p.title + ": EVERYONE GATHERS");
            Journal.Add("STORY", p.title + " BEGINS.");
            return s;
        }

        /// <summary>What keeps a performance from starting now (null = ready): performers there, the music on.</summary>
        public static string Missing(WastelandGame g, Performance p)
        {
            foreach (var k in p.performers) if (!g.CastBody(k)) return "WAITING FOR " + NameOf(k);
            if (p.radio && !RadioPlaying(StoryAnchors.Get(p.stage))) return "THE RADIO IS OFF: SWITCH IT ON ([E])";
            return p.hold?.Invoke();
        }

        /// <summary>A placed radio (any receiver) switched on near the stage.</summary>
        public static bool RadioPlaying(Vector3 stage)
        {
            foreach (var r in MadMax.Audio.RadioReceiver.All)
                if (r && r.on && Flat(r.transform.position, stage) <= Performance.RadioReach) return true;
            return false;
        }

        static string NameOf(string key) { var m = StoryCast.Find(key); return m != null ? m.Value.name : key.ToUpperInvariant(); }
        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        Vector3 Spot(Vector3 stage, float syaw, float across, float ahead)
        {
            var p = stage + Quaternion.Euler(0f, syaw, 0f) * new Vector3(across, 0f, ahead);
            var t = MadMax.World.DeformableTerrain.Instance;
            if (t) p.y = t.HeightNoLoad(p.x, p.z) + 0.05f;
            return p;
        }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || !g.Player || Show == null) return;
            var stage = StoryAnchors.Get(Show.stage);
            float syaw = StoryAnchors.Yaw(Show.stage);
            if (Finished) { Linger(g, stage); return; }
            Gather(g, stage, syaw);
            if ((arrangeT -= Time.deltaTime) <= 0f) { arrangeT = 0.5f; Arrange(g, stage, syaw); }
            string why = Flat(g.Current ? g.Current.transform.position : g.Player.transform.position, stage) > Away ? "WAITS FOR YOU TO COME BACK" : Missing(g, Show);
            if (why != null)
            {
                if (why != lastWait) { g.Toast(Show.title + ": " + why); lastWait = why; }
                Waiting = why;
                return;
            }
            Waiting = null; lastWait = null;
            if ((beatLeft -= Time.deltaTime) > 0f) return;
            Beat++;
            if (Beat >= Show.beats.Count) { Finish(g); return; }
            Play(g, Show.beats[Beat], stage);
        }

        /// <summary>Townsfolk walk in from behind the rows, one a frame.</summary>
        void Gather(WastelandGame g, Vector3 stage, float syaw)
        {
            if (guests.Count >= Show.extras) return;
            int i = guests.Count;
            int row = 1 + i / 5, col = i % 5;
            var spot = Spot(stage, syaw, (col - 2) * 1.25f + (row % 2) * 0.5f, Show.crowdAt + row * 1.3f);
            var from = Spot(stage, syaw, (col - 2) * 2f, Show.crowdAt + 14f);
            int seed = 0x5eed; foreach (char ch in Show.key) seed = unchecked(seed * 31 + ch);
            var p = NpcProfile.Make("perf:" + Show.key + ":" + i, NpcRole.Shopkeeper, unchecked(seed * 7 + i * 131 + g.World.seed));
            p.tool = null; p.title = "GUEST";
            var n = MadMax.Npc.Npc.Spawn(p, from, syaw + 180f, null, g.propMaterial);
            n.mode = MadMax.Npc.Npc.Mode.Stand;
            n.home = spot; n.homeYaw = syaw + 180f; n.homeRadius = 0.3f;
            guests.Add(n);
        }

        /// <summary>Performers across the stage, the cast audience at the front, everyone facing the right way.</summary>
        void Arrange(WastelandGame g, Vector3 stage, float syaw)
        {
            int n = Show.performers.Count;
            for (int i = 0; i < n; i++) Place(g.CastBody(Show.performers[i]), Spot(stage, syaw, (i - (n - 1) * 0.5f) * 1.4f, 0f), syaw);
            int a = Show.audience.Count;
            for (int i = 0; i < a; i++) Place(g.CastBody(Show.audience[i]), Spot(stage, syaw, (i - (a - 1) * 0.5f) * 1.3f, Show.crowdAt), syaw + 180f);
        }

        static void Place(MadMax.Npc.Npc body, Vector3 spot, float yawDeg)
        {
            if (!body || !body.Alive || body.Riding || body.Driving) return;
            if (body.Profile.role == NpcRole.Resident) body.Profile.role = NpcRole.Shopkeeper;   // stands where put, awake
            body.home = spot; body.homeYaw = yawDeg; body.homeRadius = 0.3f;
            if (Flat(body.transform.position, spot) > 7f)
            {
                var cc = body.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                body.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, yawDeg, 0f));
                if (cc) cc.enabled = true;
            }
        }

        void Play(WastelandGame g, Performance.Beat b, Vector3 stage)
        {
            beatLeft = Mathf.Max(0.5f, b.seconds);
            if (b.sfx != null) MadMax.Audio.Sfx.Play(b.sfx, stage + Vector3.up, 0.8f, 1f, 60f);
            if (b.who == Performance.Crowd)
            {
                int said = 0;
                for (int i = 0; i < guests.Count && said < 3; i += 2) if (guests[i]) { Say(guests[i], b.line, b.seconds); said++; }
                g.Toast("THE CROWD: " + b.line);
            }
            else if (b.who != null)
            {
                var body = g.CastBody(b.who);
                Say(body, b.line, b.seconds);
                g.Toast(NameOf(b.who) + ": " + b.line);
            }
            else g.Toast(b.line);
            Show.onBeat?.Invoke(Beat);
        }

        static void Say(MadMax.Npc.Npc n, string text, float seconds)
        {
            if (!n || !n.Head || !GameSettings.Current.voiceCaptions) return;
            NpcVoice.Captions.Add(new NpcVoice.Caption { at = n.Head, text = text, until = Time.unscaledTime + seconds });
        }

        void Finish(WastelandGame g)
        {
            Finished = true;
            linger = 25f;
            MadMax.Audio.Sfx.Play("crowd_cheer", StoryAnchors.Get(Show.stage) + Vector3.up, 0.8f, 1f, 60f);
            Show.onDone?.Invoke();
            Journal.Add("STORY", Show.title + " IS OVER.");
            MadMax.Story.Story.Note(Show.DoneEvent);
        }

        /// <summary>After the show the guests mill about a while, then go home once the player isn't looking.</summary>
        void Linger(WastelandGame g, Vector3 stage)
        {
            linger -= Time.deltaTime;
            if (linger > 0f && Flat(g.Current ? g.Current.transform.position : g.Player.transform.position, stage) < 80f) return;
            End();
        }

        /// <summary>Send the guests home and close the scene.</summary>
        public void End()
        {
            Release();
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        void Release()
        {
            if (released) return;
            released = true;
            foreach (var n in guests) if (n) n.LetGo();
            guests.Clear();
        }

        void OnDestroy()
        {
            if (!released) foreach (var n in guests) if (n) Destroy(n.gameObject);
            if (Current == this) Current = null;
        }
    }
}
