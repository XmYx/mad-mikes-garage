using System.Collections.Generic;
using MadMax.Game;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>The people of a generated city (roadmap 28 R3, authority only): a shopkeeper behind every ground-floor
    /// counter (each shop its own trade), the housing clerk by the gangway, and residents who live in the occupied
    /// flats — by day they come down the stair core and linger on the deck, in the evening they walk home, at night
    /// they stay in. Everyone is parented to the city (carried with it) and steered along the plan's routes
    /// (<see cref="TrackedCity.RouteIn"/>) in generator space, converted to world space every frame. While the player is
    /// within <see cref="Far"/> (+ the host's <see cref="CityHost.FolkRadius"/>) of the city, each person is spawned once
    /// the player is within <see cref="CityHost.PersonReach"/> of their post or flat and folded away 60 m further out.</summary>
    public class CityFolk : MonoBehaviour
    {
        public const float Far = 240f;
        static readonly string[] Kinds = { "food", "parts", "salvage", "build", "fuel", "pack", "scrap" };

        CityHost city;
        WastelandGame game;
        List<Vector3> deck;

        class Person
        {
            public string id;
            public NpcProfile profile;
            public Npc npc;
            public NpcRole role;
            public TrackedCity.Room home;                 // residents' flat / keepers' shop
            public Vector3 post, face;                    // keepers / clerk (generator space)
            public Vector3 anchor;                        // where they are found (generator space): post or flat
            public List<Vector3> route = new List<Vector3>();
            public int step;
            public float lingerUntil;
            public int phase;                             // residents: 0 home, 1 going out, 2 on deck, 3 going home
        }
        readonly List<Person> people = new List<Person>();
        float scanAt;
        bool spawned;

        public static CityFolk Attach(WastelandGame game, CityHost city, int index)
        {
            var f = city.gameObject.AddComponent<CityFolk>();
            f.game = game; f.city = city;
            var plan = city.CityPlan;
            f.deck = city.Spots();
            int shop = 0;
            foreach (var r in plan.rooms)
            {
                if (!r.shop) continue;
                var post = TrackedCity.KeeperSpot(plan, r, out var face);
                string id = "city" + index + ":shop" + r.index;
                f.people.Add(new Person { id = id, role = NpcRole.Shopkeeper, home = r, post = post, face = face, anchor = post, profile = Profile(game, id, NpcRole.Shopkeeper, Kinds[shop++ % Kinds.Length]) });
            }
            string cid = "city" + index + ":clerk";
            var clerkAt = city.ClerkSpot(out var clerkFace);
            f.people.Add(new Person { id = cid, role = NpcRole.Resident, post = clerkAt, face = clerkFace, anchor = clerkAt, profile = Profile(game, cid, NpcRole.Resident, null) });
            int residents = 0;
            foreach (var r in plan.rooms)
            {
                if (r.shop || !WastelandGame.Occupied(plan, r) || residents >= 18) continue;
                if (MadMax.Voxel.Pal.Hash(r.index, plan.p.seed, 77) > 0.35f) continue;               // not everyone is about
                string id = "city" + index + ":home" + r.index;
                f.people.Add(new Person { id = id, role = NpcRole.Resident, home = r, anchor = new Vector3((r.x0 + r.x1) / 2, (r.y0 + r.y1) / 2, r.z0), profile = Profile(game, id, NpcRole.Resident, null) });
                residents++;
            }
            return f;
        }

        static NpcProfile Profile(WastelandGame game, string id, NpcRole role, string kind)
        {
            unchecked { int h = 17; foreach (char c in id) h = h * 31 + c; return NpcProfile.Make(id, role, h ^ game.World.seed, kind); }
        }

        Vector3 World(Vector3 gen) => city.PlanPoint(gen);

        void Update()
        {
            if (!game || !city || !city.IsReady || game.Player == null) return;
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.IsClient) return;
            var me = game.Player.transform.position;
            if (Time.time >= scanAt)
            {
                scanAt = Time.time + 1f;
                float d = Vector3.Distance(me, city.transform.position);
                spawned = d < Far + city.FolkRadius;
                foreach (var p in people)
                    if (p.npc && (!spawned || Vector3.Distance(p.npc.transform.position, me) > city.PersonReach + 60f) && game.Menus.TalkingTo != p.npc) { Destroy(p.npc.gameObject); p.npc = null; }
            }
            if (!spawned) return;
            foreach (var p in people)                                                            // one body per frame, warmed on workers
            {
                if (p.npc || NpcRegistry.IsDead(p.id) || Vector3.Distance(World(p.anchor), me) > city.PersonReach) continue;
                if (!HumanRig.Prewarm(p.profile.look, p.profile.outfit, p.profile.Condition)) break;
                Spawn(p);
                break;
            }
            float h = DayNight.Hours;
            bool day = h >= 7.5f && h < 18.5f;
            foreach (var p in people) if (p.npc && p.npc.Alive) Steer(p, day);
        }

        float Yaw(Vector3 genDir)
        {
            var w = city.transform.TransformDirection(new Vector3(-genDir.y, 0f, genDir.x));
            return Mathf.Atan2(w.x, w.z) * Mathf.Rad2Deg;
        }

        void Spawn(Person p)
        {
            Vector3 at, face;
            if (p.role == NpcRole.Shopkeeper || p.home == null) { at = p.post; face = p.face; }
            else
            {
                p.route = TrackedCity.RouteIn(city.CityPlan, p.home);
                bool day = DayNight.Hours >= 7.5f && DayNight.Hours < 18.5f;
                if (day && deck.Count > 0) { at = p.post = Spot(p, p.id.GetHashCode() & 0x7fffffff); p.phase = 2; p.lingerUntil = Time.time + Random.Range(10f, 40f); }
                else { at = p.route[p.route.Count - 1]; p.phase = 0; }
                face = Vector3.right;
            }
            var n = Npc.Spawn(p.profile, World(at) + Vector3.up * 0.05f, Yaw(face), city.transform, game.propMaterial);
            n.aboard = city;
            n.clerk = p.id.EndsWith(":clerk");
            n.mode = Npc.Mode.Stand;
            n.homeYaw = Yaw(face);
            p.npc = n;
        }

        void Steer(Person p, bool day)
        {
            var n = p.npc;
            if (n.Hostile || n.mode == Npc.Mode.Flee || n.mode == Npc.Mode.Fight || n.companion) return;
            if (p.role == NpcRole.Shopkeeper || p.home == null)
            {
                n.home = World(p.post);
                n.homeYaw = Yaw(p.face);
                return;
            }
            switch (p.phase)
            {
                case 0:                                                                          // at home
                    n.home = World(p.route[p.route.Count - 1]);
                    if (day && Random.value < 0.002f) { p.phase = 1; p.step = p.route.Count - 1; }
                    break;
                case 1:                                                                          // down the stairs and out
                    if (Follow(p, p.route[p.step]) && --p.step < 0) { p.phase = 2; p.lingerUntil = 0f; }
                    break;
                case 2:                                                                          // about the deck
                    if (!day) { p.phase = 3; p.step = 0; break; }
                    if (Time.time > p.lingerUntil && deck.Count > 0) { p.post = Spot(p, Random.Range(0, 1 << 20)); p.lingerUntil = Time.time + Random.Range(25f, 90f); }
                    Follow(p, p.post);
                    break;
                case 3:                                                                          // home for the night
                    if (Follow(p, p.route[p.step]) && ++p.step >= p.route.Count) p.phase = 0;
                    break;
            }
        }

        /// <summary>An open spot on the person's own deck (a megacity has decks at many heights; routes never leave one).</summary>
        Vector3 Spot(Person p, int pick)
        {
            float z = city.CityPlan.blocks[p.home.block].z0;
            int n = 0;
            foreach (var s in deck) if (Mathf.Abs(s.z - z) < 1f) n++;
            if (n == 0) return p.route[0];
            pick %= n;
            foreach (var s in deck) if (Mathf.Abs(s.z - z) < 1f && pick-- == 0) return s;
            return p.route[0];
        }

        /// <summary>Walk the person towards a generator-space point; true once there.</summary>
        bool Follow(Person p, Vector3 gen)
        {
            var w = World(gen);
            p.npc.home = w;
            var me = p.npc.transform.position;
            return new Vector2(me.x - w.x, me.z - w.z).sqrMagnitude < 0.55f * 0.55f && Mathf.Abs(me.y - w.y) < 1.6f;
        }

        /// <summary>People aboard right now (tests).</summary>
        public int Alive { get { int n = 0; foreach (var p in people) if (p.npc && p.npc.Alive) n++; return n; } }
        public int Keepers { get { int n = 0; foreach (var p in people) if (p.npc && p.role == NpcRole.Shopkeeper) n++; return n; } }
        public Npc Clerk { get { foreach (var p in people) if (p.npc && p.npc.clerk) return p.npc; return null; } }
    }
}
