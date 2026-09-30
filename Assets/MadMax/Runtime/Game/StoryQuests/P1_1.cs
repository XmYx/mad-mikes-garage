using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    // P1 Nell's unfinished car, stage 1: the coupe behind Nell's stop (tagged "nell_car", not in the player's fleet) and
    // her parts pile; the wheels and radiator are noticed when they sit in their sockets.
    public partial class WastelandGame
    {
        float p1Check, p1Ensure;

        /// <summary>Nell's coupe, wherever it is (null when it isn't in the world).</summary>
        public VehicleDriver P1Car() { var t = StoryTag.Find("nell_car"); return t ? t.GetComponent<VehicleDriver>() : null; }

        partial void Scene_P1_1()
        {
            if (!Build || !Build.Structures || !StoryAnchors.Has("p1_pile")) return;
            SpawnNellCar();
            PutAt("p1_pile", "tyres", new Vector3(-1.5f, 0f, 0.2f), 20f);
            PutAt("p1_pile", "crate", new Vector3(1.4f, 0f, 0.4f), 0f);
            P1Loose("wheel_street", "p1_pile", new Vector3(-0.2f, 0.4f, 1.2f), 0.35f);             // half the tread gone: it'll do
            P1Loose("coupe_hood", "p1_pile", new Vector3(0.3f, 0.3f, -1.3f), 0.1f);
            Journal.Add("PLACE", "NELL'S COUPE: BEHIND HER STOP, WITH A PILE OF PARTS");
        }

        partial void Tick_P1_1()
        {
            if (Time.time < p1Check) return;
            p1Check = Time.time + 0.5f;
            var car = P1Car();
            if (!car) { EnsureNellCar(); return; }
            var ch = car.GetComponent<VehicleChassis>();
            if (!Story.Story.StepDone("P1.1", "wheels") && P1Mounted(ch, "wheel_rear_R") && P1Mounted(ch, "wheel_rear_L")) Story.Story.Note("p1_1:wheels");
            if (!Story.Story.StepDone("P1.1", "radiator") && P1Mounted(ch, "radiator")) Story.Story.Note("p1_1:radiator");
        }

        static bool P1Mounted(VehicleChassis ch, string socket) { var s = ch ? ch.FindSocket(socket) : null; return s && s.Current; }

        /// <summary>The coupe as far as the chain has got: a shell (no rear wheels, radiator, hood or engine), then on four
        /// wheels with a radiator (after P1.1), then with its engine (after P1.2: Tom's straight six if bought back, else a
        /// four). It is Nell's: registered to nobody's fleet.</summary>
        void SpawnNellCar()
        {
            if (!StoryAnchors.Has("p1_car") || P1Car()) return;
            var pf = PrefabFor("Coupe") ?? PrefabFor("Sedan");
            if (!pf) return;
            var p = StoryAnchors.Get("p1_car"); p.y = terrain.HeightNoLoad(p.x, p.z) + 0.7f;
            var v = Instantiate(pf, p, Quaternion.Euler(0f, StoryAnchors.Yaw("p1_car"), 0f)).GetComponent<VehicleDriver>();
            v.name = "Nell's Coupe";
            Register(v, null);
            StoryTag.Set(v.gameObject, "nell_car");
            bool fitted = Story.Story.StateOf("P1.1") == Story.Story.State.Done, engined = Story.Story.StateOf("P1.2") == Story.Story.State.Done;
            var ch = v.GetComponent<VehicleChassis>();
            foreach (var s in ch.Sockets)
            {
                bool strip = s.name == "hood" || (s.name == "engine" && (!engined || StoryLibrary.P1Bought(Story.Story.Route("P1.2", "deal"))))
                             || (!fitted && (s.name == "wheel_rear_R" || s.name == "wheel_rear_L" || s.name == "radiator"));
                if (strip && s.Current) { var part = s.Detach(false); if (part) Destroy(part.gameObject); }
            }
            if (engined && StoryLibrary.P1Bought(Story.Story.Route("P1.2", "deal")))
            {
                var es = ch.FindSocket("engine"); var e = es ? SpawnPart(StoryLibrary.P1Original, es.transform.position, es.transform.rotation) : null;
                if (e && !es.Attach(e)) Destroy(e.gameObject);
            }
            if (v.TryGetComponent<VehicleSystems>(out var sys))
            {
                bool ready = Story.Story.StepDone("P1.3", "ready") || Story.Story.StateOf("P1.3") == Story.Story.State.Done;
                sys.fuel = ready ? 20f : 0f; sys.oil = ready ? sys.oilCapacity : 0f; sys.coolant = ready ? sys.coolantCapacity : 0f;
            }
        }

        /// <summary>The coupe went missing (a save made while Nell was driving it keeps no AI car): put it back at her stop.</summary>
        void EnsureNellCar()
        {
            if (Time.time < p1Ensure) return;
            p1Ensure = Time.time + 3f;
            if (Story.Story.Campaign && Story.Story.Flag("hook:P1.1") && !P1Car()) SpawnNellCar();
        }

        /// <summary>A loose part lying at a story place (physical: take it with [E] and mount it with the wrench).</summary>
        VehiclePart P1Loose(string id, string anchor, Vector3 local, float damage)
        {
            if (!StoryAnchors.Has(anchor)) return null;
            var r = Quaternion.Euler(0f, StoryAnchors.Yaw(anchor), 0f);
            var p = StoryAnchors.Get(anchor) + r * new Vector3(local.x, 0f, local.z);
            p.y = terrain.HeightNoLoad(p.x, p.z) + 0.3f + local.y;
            var part = SpawnPart(id, p, r);
            if (!part) return null;
            part.damage = damage;
            if (!part.GetComponent<Rigidbody>()) part.gameObject.AddComponent<Rigidbody>().mass = part.mass;
            MadMax.Net.NetSession.Instance?.SendLooseSpawn(part);
            return part;
        }
    }
}
