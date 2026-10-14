using MadMax.Building;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A set of points with its lever stand: [E] at the stand throws them (the blades swing across, the target
    /// vane turns), or a driver sets them from the cab. Trains already past the points keep their road; a train heading
    /// for them re-reads the setting (<see cref="Thrown"/>).</summary>
    public class RailSwitch : MonoBehaviour, IInteractable
    {
        /// <summary>Any points thrown (free trains drop the path they had planned beyond them).</summary>
        public static event System.Action<RailNode> Thrown;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Thrown = null;

        public RailNetwork net;
        public RailNode node;
        Transform lever, target, bladeA, bladeB;
        Vector3 bladeAHome, bladeBHome, bladeASide, bladeBSide;
        float shown = -1f;

        public static RailSwitch Create(RailNetwork net, RailNode n, Material mat, Transform parent)
        {
            var toe = net.lines[n.ends[n.toe].x].Outward(n.ends[n.toe].y);
            var into = -toe;                                                                       // from the toe into the heels
            var side = Vector3.Cross(Vector3.up, into).normalized * (n.lever == 0 ? 1 : n.lever);
            var go = new GameObject("Points", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(n.pos + toe * 1.5f + side * 2.6f, Quaternion.LookRotation(side, Vector3.up));
            go.GetComponent<MeshFilter>().sharedMesh = RailProps.Stand;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var sw = go.AddComponent<RailSwitch>();
            sw.net = net; sw.node = n;
            var box = go.AddComponent<BoxCollider>(); box.center = new Vector3(0f, 0.6f, 0f); box.size = new Vector3(0.7f, 1.2f, 0.7f);
            var lv = new GameObject("Lever", typeof(MeshFilter), typeof(MeshRenderer));
            lv.transform.SetParent(go.transform, false); lv.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            lv.GetComponent<MeshFilter>().sharedMesh = RailProps.StandLever; lv.GetComponent<MeshRenderer>().sharedMaterial = mat;
            sw.lever = lv.transform;
            var tg = new GameObject("Target", typeof(MeshFilter), typeof(MeshRenderer));
            tg.transform.SetParent(go.transform, false); tg.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            tg.GetComponent<MeshFilter>().sharedMesh = RailProps.StandTarget; tg.GetComponent<MeshRenderer>().sharedMaterial = mat;
            sw.target = tg.transform;
            // blades: the first 5 m of each road's inner rail beside the node
            Transform Blade(int heel, out Vector3 home, out Vector3 toward)
            {
                var e = n.ends[heel];
                var o = net.lines[e.x].Outward(e.y);
                var other = net.lines[n.ends[heel == n.heelA ? n.heelB : n.heelA].x].Outward(n.ends[heel == n.heelA ? n.heelB : n.heelA].y);
                var lat = Vector3.Cross(Vector3.up, o).normalized;
                if (Vector3.Dot(lat, other) < 0f) lat = -lat;                                       // the rail on the side of the other road
                var b = new GameObject("Blade", typeof(MeshFilter), typeof(MeshRenderer));
                b.transform.SetParent(go.transform, true);
                home = n.pos + lat * (RailNetwork.Gauge * 0.5f) + Vector3.up * (RailNetwork.RailTop - 0.15f);
                b.transform.SetPositionAndRotation(home, Quaternion.LookRotation(o, Vector3.up));
                b.GetComponent<MeshFilter>().sharedMesh = RailProps.Blade; b.GetComponent<MeshRenderer>().sharedMaterial = mat;
                toward = -lat * 0.12f;
                return b.transform;
            }
            sw.bladeA = Blade(n.heelA, out sw.bladeAHome, out sw.bladeASide);
            sw.bladeB = Blade(n.heelB, out sw.bladeBHome, out sw.bladeBSide);
            return sw;
        }

        void OnEnable() => PartFunctions.Interactables.Add(this);
        void OnDisable() => PartFunctions.Interactables.Remove(this);

        public string Side(int state)
        {
            int div = net.DivergeSide(node);
            if (state == 0) return "STRAIGHT ON";
            return div < 0 ? "TO THE LEFT" : "TO THE RIGHT";
        }

        public string Prompt(MadMax.Game.WastelandGame g) => node == null || !node.Switch ? null : "[E] THROW THE POINTS (SET " + Side(node.state) + ")";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary || node == null || !node.Switch) return;
            Throw(node, 1 - node.state);
            g.Toast("POINTS SET " + Side(node.state));
        }

        /// <summary>Set points (lever or cab) and tell the trains.</summary>
        public static void Throw(RailNode n, int state)
        {
            if (n == null || !n.Switch || n.state == state) return;
            n.state = state;
            MadMax.Audio.Sfx.Play("ratchet", n.pos, 0.8f, 0.7f, 40f);
            Thrown?.Invoke(n);
        }

        void Update()
        {
            if (node == null) return;
            float want = node.state;
            if (shown < 0f) shown = want;
            shown = Mathf.MoveTowards(shown, want, Time.deltaTime * 1.6f);
            lever.localRotation = Quaternion.Euler(Mathf.Lerp(-35f, 35f, shown), 0f, 0f);
            target.localRotation = Quaternion.Euler(0f, shown * 90f, 0f);
            // the open road's blade stands off its stock rail
            bladeA.position = bladeAHome + bladeASide * shown;
            bladeB.position = bladeBHome + bladeBSide * (1f - shown);
        }
    }
}
