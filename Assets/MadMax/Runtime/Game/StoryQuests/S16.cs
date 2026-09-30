using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S16 RUST IN PEACE in the world: Mo's scrap cart at the edge of town, the Vargas' school bus on the verge
    /// (loose battery lead, dry tank, a tired engine), their camp behind it and Hanne's shelter off the road. While it
    /// runs the bus itself is watched: cut with the salvage cutter (25+ units), two parts off it, scrapped or driven away
    /// = stripped/taken; its engine catching = running. Taking it without a deal, or breaking the promise to repair it,
    /// costs standing with the town (once) and the family moves on.</summary>
    public partial class WastelandGame
    {
        bool s16Seen, s16NotedStrip, s16NotedRun, s16NotedSold, s16NotedSpares, s16Set, s16Trust;
        int s16PrefabParts = -1;
        string s16Deal, s16Work;

        partial void Scene_S16()
        {
            if (!Build || !Build.Structures) return;
            // Mo's cart: a table of scrap, tyres, a barrel and his sign
            Q2Put("mo", "table", new Vector3(0f, 0f, -1.4f), 0f);
            Q2Put("mo", "tyres", new Vector3(1.9f, 0f, -1.8f), 20f);
            Q2Put("mo", "barrel", new Vector3(-1.8f, 0f, -1.6f), 0f);
            Q2Put("mo", "crate", new Vector3(-0.6f, 0f, -2.6f), 12f);
            Q2Put("mo", "sign", new Vector3(2.6f, 0f, 0.2f), 0f);
            // behind the bus: bedding, last night's ashes, a child's drawing, a lamp
            Q2Put("s16_camp", "bed", new Vector3(-1.4f, 0f, -0.6f), 90f);
            Q2Put("s16_camp", "campfire", new Vector3(1.3f, 0f, 0.3f), 0f);
            Q2Put("s16_camp", "rug", new Vector3(-1.4f, 0f, 1.2f), 0f);
            Q2Put("s16_camp", "painting", new Vector3(0.2f, 0.02f, -1.6f), 20f);
            Q2Put("s16_camp", "lamp", new Vector3(-2.8f, 0f, 0.4f), 0f);
            Q2Put("s16_camp", "barrel", new Vector3(2.8f, 0f, -1.2f), 0f);
            // Hanne's shelter off the road
            Q2Put("s16_hanne", "porch_awning", new Vector3(0f, 0f, -2f), 0f);
            Q2Put("s16_hanne", "bench", new Vector3(-1.4f, 0f, -1.8f), 0f);
            Q2Put("s16_hanne", "barrel", new Vector3(1.8f, 0f, -2.2f), 0f);
            Q2Put("s16_hanne", "crate", new Vector3(1.2f, 0f, -3f), 30f);
            // the bus, parked along the verge
            var pf = PrefabFor("Bus");
            if (pf && StoryAnchors.Has("s16_bus"))
            {
                var p = StoryAnchors.Get("s16_bus"); p.y = terrain.HeightNoLoad(p.x, p.z) + 1f;
                var v = Instantiate(pf, p, Quaternion.Euler(0f, StoryAnchors.Yaw("s16_bus"), 0f)).GetComponent<VehicleDriver>();
                v.name = "Varga Bus";
                Register(v, null);
                StoryTag.Set(v.gameObject, "s16_bus");
                var paint = v.GetComponent<VehiclePaint>() ?? v.gameObject.AddComponent<VehiclePaint>();
                paint.colour = 4; paint.Apply();
                if (v.Engine && v.Engine.TryGetComponent<VehiclePart>(out var ep)) ep.damage = 0.8f;             // tired, not dead
                if (v.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = 0f; sys.disconnected = true; }
                if (v.TryGetComponent<VehicleDamage>(out var dmg)) dmg.AddFrameDamage(0.08f, 1f);
            }
        }

        partial void Tick_S16()
        {
            var tag = StoryTag.Find("s16_bus");
            var bus = tag ? tag.GetComponent<VehicleDriver>() : null;
            if (bus) s16Seen = true;
            if (Time.frameCount % 15 == 0 && StoryAnchors.Has("s16_bus"))
            {
                bool bought = StoryLibrary.S16Bought, promised = StoryLibrary.S16Promised;
                bool moved = bus && Q2Flat(bus.transform.position, StoryAnchors.Get("s16_bus")) > 150f;
                bool stripped = s16Seen && !bus;
                if (bus && !stripped)
                {
                    if (bus.TryGetComponent<VehicleDamage>(out var dmg) && bus.TryGetComponent<VehicleChassis>(out var ch) && ch.bodyMass / 25f - dmg.SalvageLeft >= 25f) stripped = true;
                    if (s16PrefabParts < 0) { var pf = PrefabFor("Bus"); s16PrefabParts = pf ? pf.GetComponentsInChildren<VehiclePart>(true).Length : 0; }
                    if (s16PrefabParts > 0 && s16PrefabParts - bus.GetComponentsInChildren<VehiclePart>(true).Length >= 2) stripped = true;
                }
                // stripping it, or driving it off without having bought it, is taking it (a deal already made stays made)
                if ((stripped || (moved && !bought)) && !s16NotedStrip) { s16NotedStrip = true; Story.Story.Note("s16:stripped"); Story.Story.Note("s16:taken"); }
                if (!s16NotedRun && bus && !moved && bus.TryGetComponent<VehicleSystems>(out var sys) && sys.Started) { s16NotedRun = true; Story.Story.Note("s16:running"); }
                if (!s16NotedSold && bought) { s16NotedSold = true; Story.Story.Note("s16:sold"); }
                if (!s16NotedSpares && promised && Story.Story.Route("S16", "work") == "GOT IT RUNNING") { s16NotedSpares = true; Story.Story.Note("s16:spares"); }
                // taking the family's home, or promising to fix it and then stripping it or driving it off, gets round town
                string deal = Story.Story.Route("S16", "deal");
                if (!s16Trust && !Story.Story.Flag("s16_trust") && ((deal == "TOOK IT ANYWAY" && s16NotedStrip) || (promised && (stripped || moved))))
                {
                    s16Trust = true;
                    Story.Story.SetFlag("s16_trust");
                    MadMax.World.Settlement near = null; float best = float.MaxValue;
                    var mo = StoryAnchors.Get("mo");
                    foreach (var st in World.settlements) { float d = Vector2.Distance(st.pos, new Vector2(mo.x, mo.z)); if (d < best) { best = d; near = st; } }
                    Factions.Shift(near != null ? Factions.OfSettlement(near) : Faction.Settlers, -6);
                    Journal.Add("STORY", "WORD GETS AROUND TOWN: THE VARGAS' BUS WENT FOR SCRAP. THE FAMILY HAS MOVED ON.");
                }
            }
            string dl = Story.Story.Route("S16", "deal"), wk = Story.Story.Route("S16", "work");
            bool trust = Story.Story.Flag("s16_trust");
            if (!s16Set || !ReferenceEquals(dl, s16Deal) || !ReferenceEquals(wk, s16Work) || trust != s16Trust)
            {
                s16Set = true; s16Deal = dl; s16Work = wk; s16Trust = trust;
                var q = StoryLibrary.Get("S16"); if (q != null) q.payoff = StoryLibrary.S16Payoff();
            }
        }
    }
}
