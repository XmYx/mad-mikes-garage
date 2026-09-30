using System.Collections.Generic;
using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // Shared scene helpers of the S01, S08, S12, S14, S15 and S23 hooks (the Q5 prefix keeps them apart from other
    // quests' helpers).
    public partial class WastelandGame
    {
        readonly HashSet<string> q5Noted = new HashSet<string>();

        /// <summary>Runtime dressing of the Q5 scenes, kept on their saved props in any quest state (Q5Dressing calls it).</summary>
        internal void Q5Dress() => S15Dress();

        /// <summary>Note a quest event once per session (a condition noted stays met; re-noting every frame would only
        /// make the story re-evaluate every quest every frame).</summary>
        void Q5Note(string key) { if (q5Noted.Add(key)) MadMax.Story.Story.Note(key); }

        /// <summary>A world position at an anchor's local offset (its yaw), on the ground.</summary>
        Vector3 Q5At(string anchor, Vector3 local)
        {
            var p = StoryAnchors.Get(anchor) + Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f) * local;
            p.y = terrain ? terrain.HeightNoLoad(p.x, p.z) : p.y;
            return p;
        }

        /// <summary>A campaign prop at an anchor's local offset, turned by <paramref name="turn"/>, raised by
        /// <paramref name="up"/>, and tipped over by <paramref name="roll"/> degrees (appliances lying on their side).</summary>
        Placeable Q5Put(string anchor, string id, Vector3 local, float turn, float up = 0f, float roll = 0f)
        {
            if (!Build || !Build.Structures || !StoryAnchors.Has(anchor)) return null;
            var p = Q5At(anchor, local); p.y += up;
            var rot = Quaternion.Euler(0f, StoryAnchors.Yaw(anchor) + turn, 0f) * Quaternion.Euler(0f, 0f, roll);
            var pl = FurnitureLibrary.Spawn(id, Build.Structures, p, rot, propMaterial);
            if (pl) storyProps.Add(pl.Id);
            return pl;
        }

        /// <summary>The campaign prop <paramref name="id"/> nearest <paramref name="near"/> within <paramref name="r"/> m.</summary>
        Placeable Q5Prop(string id, Vector3 near, float r)
        {
            Placeable best = null; float bd = r * r;
            foreach (var p in Placeable.All)
            {
                if (!p || p.id != id || !IsStoryProp(p)) continue;
                var d = p.transform.position - near; d.y = 0f;
                if (d.sqrMagnitude <= bd) { bd = d.sqrMagnitude; best = p; }
            }
            return best;
        }

        /// <summary>Every campaign prop <paramref name="id"/> within <paramref name="r"/> m.</summary>
        int Q5Count(string id, Vector3 near, float r)
        {
            int n = 0;
            foreach (var p in Placeable.All)
            {
                if (!p || p.id != id || !IsStoryProp(p)) continue;
                var d = p.transform.position - near; d.y = 0f;
                if (d.sqrMagnitude <= r * r) n++;
            }
            return n;
        }

        static float Q5Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>The player on foot (or their vehicle) within <paramref name="r"/> m of <paramref name="p"/>.</summary>
        bool Q5Near(Vector3 p, float r) => Player && Q5Flat(FocusPos, p) <= r;

        static bool Q5Route(string quest, string step, string prefix) { var r = MadMax.Story.Story.Route(quest, step); return r != null && r.StartsWith(prefix); }

        /// <summary>A cast member stays put and awake while their quest runs (a stand-in shopkeeper's routine: no
        /// wandering off, no bed at night), facing <paramref name="yawDeg"/> at <paramref name="home"/>.</summary>
        static void Q5Keep(MadMax.Npc.Npc n, Vector3 home, float yawDeg, float radius = 0.4f)
        {
            if (!n || !n.Alive) return;
            if (n.Profile.role == MadMax.Npc.NpcRole.Resident) n.Profile.role = MadMax.Npc.NpcRole.Shopkeeper;
            n.home = home; n.homeYaw = yawDeg; n.homeRadius = radius;
        }

        /// <summary>Put a person somewhere at once (their character controller would undo a plain move).</summary>
        static void Q5Teleport(MadMax.Npc.Npc n, Vector3 p, float yawDeg)
        {
            if (!n) return;
            var cc = n.GetComponent<CharacterController>();
            bool was = cc && cc.enabled;
            if (cc) cc.enabled = false;
            n.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yawDeg, 0f));
            if (cc) cc.enabled = was;
        }

        /// <summary>The authored closing lines, kept before the first rewrite (the catalogue outlives a world).</summary>
        static readonly Dictionary<string, string> q5Payoffs = new Dictionary<string, string>();

        /// <summary>The quest's closing journal line, written from the choices made (set before the last step lands).</summary>
        static void Q5Payoff(string quest, string text)
        {
            var q = StoryLibrary.Get(quest);
            if (q == null) return;
            if (!q5Payoffs.ContainsKey(quest)) q5Payoffs[quest] = q.payoff;
            q.payoff = text;
        }

        /// <summary>A new world's quest starts from the authored closing line (called from its scene hook).</summary>
        static void Q5ResetPayoff(string quest) { var q = StoryLibrary.Get(quest); if (q != null && q5Payoffs.TryGetValue(quest, out var t)) q.payoff = t; }

        /// <summary>A cast member's full name (the key when unknown).</summary>
        static string Q5Name(string key) { var m = StoryCast.Find(key); return m != null ? m.Value.name : key.ToUpperInvariant(); }

        /// <summary>A speech bubble over someone (the HUD's caption layer), for scripted lines without a voice.</summary>
        static void Q5Say(MadMax.Npc.Npc n, string text, float seconds = 4f)
        {
            if (!n || !n.Head || !GameSettings.Current.voiceCaptions) return;
            MadMax.Npc.NpcVoice.Captions.Add(new MadMax.Npc.NpcVoice.Caption { at = n.Head, text = text, until = Time.unscaledTime + seconds });
        }
    }
}
