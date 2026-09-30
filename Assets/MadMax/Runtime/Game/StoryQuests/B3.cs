using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>B3 LIGHTS WORTH COMING BACK TO: reads the garage's own grid (a powered lamp, a powered essential
    /// service), then runs Nell's test: her compressor on a lead through a load breaker (story props, cabled into the
    /// service's line) draws more than the supply can make; a generator stalls, the service goes dark, and the priority
    /// load has to come back while the surge stays plugged in (her breaker on LOW + a restart, or storage carrying it).
    /// Then the refuge: recovery point at the garage bed, a lantern under the sign.</summary>
    public partial class WastelandGame
    {
        static readonly string[] B3Services = { "fridge", "freezer", "pump", "heater", "aircon", "oven", "kitchen_range", "desalinator" };
        float b3TestAt = -1f, b3PoweredSince = -1f;
        UtilityNode b3Surge;

        static bool B3Near(Placeable p, float r)
        {
            if (!p || !StoryAnchors.Has("garage")) return false;
            var d = p.transform.position - StoryAnchors.Get("garage"); d.y = 0f;
            return d.sqrMagnitude < r * r;
        }

        /// <summary>A lamp the player built at the garage that has power (lit at night).</summary>
        bool B3LampPowered()
        {
            foreach (var l in PoweredLight.All)
            {
                if (!l || !l.needsPower || !l.TryGetComponent<UtilityNode>(out var n) || !n.Powered) continue;
                var p = l.GetComponent<Placeable>();
                if (p && !IsStoryProp(p) && B3Near(p, 25f)) return true;
            }
            return false;
        }

        /// <summary>The essential service at the garage: a built appliance on the grid (powered ones first).</summary>
        UtilityNode B3Service(bool poweredOnly)
        {
            UtilityNode any = null;
            foreach (var n in UtilityNode.All)
            {
                if (!n || (n.kinds & UtilityKind.Power) == 0) continue;
                var p = n.Piece;
                if (!p || IsStoryProp(p) || System.Array.IndexOf(B3Services, p.id) < 0 || !B3Near(p, 25f)) continue;
                if (n.Powered) return n;
                any = n;
            }
            return poweredOnly ? null : any;
        }

        partial void Tick_B3()
        {
            var q = StoryLibrary.Get("B3");
            if (q == null) return;
            if (StoryAnchors.Has("garage_yard")) Q7Pose("nell_garage", Q7At("garage_yard", 1.5f, -0.5f), StoryAnchors.Yaw("garage_yard"));
            bool testing = Story.Story.StepDone("B3", "test") && !Story.Story.StepDone("B3", "restore");
            if (testing && (Time.frameCount % 10 == 0 || !b3Surge)) B3Surge();
            if (Time.frameCount % 15 != 0) return;
            q.payoff = StoryLibrary.B3_Payoff(Story.Story.Route("B3", "supply"), Story.Story.Route("B3", "restore"));
            var cur = Story.Story.Current(q);
            string step = cur != null ? cur.id : null;
            if (step == "lamp" && B3LampPowered()) Story.Story.Note("b3:lamp");
            if (step == "service" && B3Service(true)) Story.Story.Note("b3:service");
            if (testing) B3Test();
            if (step == "refuge" && !Story.Story.Flag("b3:refuge_set")) B3Refuge();
        }

        /// <summary>Keep the surge on the compressor's node (not saved: re-applied after a reload).</summary>
        void B3Surge()
        {
            if (!b3Surge)
            {
                var c = Q7Prop("garage", "air_compressor", new Vector3(7.6f, 0f, 4.6f), 3f);
                b3Surge = c ? c.GetComponent<UtilityNode>() : null;
            }
            if (!b3Surge) return;
            float supply = 0f;
            foreach (var n in UtilityNode.All) if (n && n != b3Surge && n.produce > 0f && n.Piece && B3Near(n.Piece, 60f)) supply += n.produce;
            b3Surge.auxDemand = Mathf.Max(800f, supply + 900f - 600f);                        // the compressor draws 600 W itself
        }

        void B3Test()
        {
            if (!Story.Story.Flag("b3:test_on"))
            {
                // Nell's lead: a load breaker (NORMAL) cabled to your service's line, her compressor behind it
                var service = B3Service(false);
                var brk = PutAt("garage", "breaker", new Vector3(6.2f, 0f, 3.8f), 0f);
                var comp = PutAt("garage", "air_compressor", new Vector3(7.6f, 0f, 4.6f), 0f);
                if (!brk || !comp) return;
                var bn = brk.GetComponent<UtilityNode>(); var cn = comp.GetComponent<UtilityNode>();
                if (bn && cn) { cn.Link(bn, UtilityKind.Power); if (service) bn.Link(service, UtilityKind.Power); }
                if (brk.TryGetComponent<PowerBreaker>(out var pb)) pb.SetPriority(1);
                b3Surge = cn;
                Story.Story.SetFlag("b3:test_on");
                b3TestAt = Time.time; b3PoweredSince = -1f;
                Toast("NELL PLUGS HER COMPRESSOR INTO YOUR LINE: A SURGE BIGGER THAN YOUR SUPPLY");
                Journal.Add("POWER", "NELL'S TEST: HER COMPRESSOR IS ON YOUR LINE THROUGH A LOAD BREAKER. KEEP THE ESSENTIAL SERVICE LIT.");
                return;
            }
            if (b3TestAt < 0f) b3TestAt = Time.time;                                             // after a reload the test goes on
            float t = Time.time - b3TestAt;
            var svc = B3Service(true);
            bool stalled = false;
            foreach (var p in Placeable.All)
                if (p && B3Near(p, 40f) && p.TryGetComponent<Generator>(out var gen) && gen.stalled && !gen.on) stalled = true;
            if (t > 1.5f && (!svc || stalled) && !Story.Story.Flag("b3:outage"))
            {
                Story.Story.SetFlag("b3:outage");
                Toast("OUTAGE: THE SURGE KNOCKED THE GARAGE DARK. GET THE ESSENTIAL SERVICE BACK FIRST");
            }
            if (!svc || stalled) { b3PoweredSince = -1f; return; }
            if (b3PoweredSince < 0f) b3PoweredSince = Time.time;
            if (Time.time - b3PoweredSince < 4f) return;
            if (Story.Story.Flag("b3:outage")) Story.Story.Note("b3:restored");
            else if (t > 8f) Story.Story.Note("b3:carried");
        }

        /// <summary>The garage becomes the refuge: Nell takes her kit home, you come to at the garage bed, a lantern goes
        /// up under the sign.</summary>
        void B3Refuge()
        {
            Story.Story.SetFlag("b3:refuge_set");
            Q7Drop(Q7Prop("garage", "air_compressor", new Vector3(7.6f, 0f, 4.6f), 3f));
            Q7Drop(Q7Prop("garage", "breaker", new Vector3(6.2f, 0f, 3.8f), 3f));
            b3Surge = null;
            Placeable bed = null; float bd = 16f * 16f;
            foreach (var p in Placeable.All)
            {
                if (!p || IsStoryProp(p) || (p.id != "bed" && p.id != "clinic_bed")) continue;
                var d = p.transform.position - StoryAnchors.Get("garage"); d.y = 0f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; bed = p; }
            }
            SetSpawn(bed ? bed.transform.position + bed.transform.forward * 1.2f + Vector3.up * 0.2f : Q7At("garage_yard", 0f, 0f) + Vector3.up * 0.3f);
            PutAt("garage", "porch_lights", new Vector3(-4.8f, 0f, 5.6f), 0f);
            Story.Story.SetFlag("refuge");
            Journal.Add("PLACE", "THE GARAGE AT THE BEND IS YOUR REFUGE: YOU COME TO HERE, AND A LANTERN HANGS UNDER THE SIGN");
            Story.Story.Note("b3:refuge");
        }
    }
}
