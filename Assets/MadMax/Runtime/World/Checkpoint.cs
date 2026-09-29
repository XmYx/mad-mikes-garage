using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A Remnant checkpoint on a highway (on the guard hut): a striped boom across the road. Honk or [E] at the hut
    /// to pay the toll (free for friends of the Remnants) and the boom lifts for half a minute; crawl into it and it
    /// holds, hit it at speed and it snaps — the Remnants remember that.</summary>
    public class Checkpoint : MonoBehaviour, MadMax.Building.IInteractable
    {
        public const int Toll = 10;
        public static readonly List<Checkpoint> All = new List<Checkpoint>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); boomMesh = null; }
        static Mesh boomMesh;

        Transform boom;
        BoxCollider boomCol;
        Quaternion down;
        float openUntil, angle, nagAt, length;
        bool broken;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        static bool Friend => Factions.Friendly(Faction.Remnants);

        /// <summary>Lay the boom across the road from the hut's side (<paramref name="centre"/> = road centre, <paramref name="yaw"/> = road heading).</summary>
        public void Setup(Vector2 centre, float yaw, Material mat)
        {
            var hut = new Vector2(transform.position.x, transform.position.z);
            var across = (centre - hut).normalized;
            float toCentre = Vector2.Distance(centre, hut);
            length = (toCentre - 2.5f) * 2f;
            var pivot = hut + across * 2.5f;
            var t = DeformableTerrain.Instance;
            var go = new GameObject("Boom", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, true);
            go.transform.position = new Vector3(pivot.x, (t ? t.HeightNoLoad(pivot.x, pivot.y) : transform.position.y) + 1.05f, pivot.y);
            down = Quaternion.LookRotation(new Vector3(across.x, 0f, across.y));
            go.transform.rotation = down;
            go.GetComponent<MeshFilter>().sharedMesh = BoomMesh();
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.localScale = new Vector3(1f, 1f, length / 8f);                               // the mesh is 8 m long
            boom = go.transform;
            boomCol = go.AddComponent<BoxCollider>();
            boomCol.center = new Vector3(0f, 0f, 4f); boomCol.size = new Vector3(0.2f, 0.2f, 8f);
        }

        static Mesh BoomMesh()
        {
            if (boomMesh) return boomMesh;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Wood);
            g.Box(-1, -1, 0, 1, 1, 99, Pal.Stripe(Pal.Solid(Pal.Crimson[3]), Pal.Solid(Pal.Cream[3]), 2, 12, 6));
            g.Mat((byte)MadMax.Items.ResourceType.Scrap); g.Box(-2, -13, -2, 2, 2, 2, Pal.Ramp(Pal.Metal, 2));   // post and counterweight
            g.Bevel();
            return boomMesh = VoxelMesher.Build(g, "CheckpointBoom");
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (broken) return "CHECKPOINT: THE BOOM IS SMASHED";
            return Time.time < openUntil ? "CHECKPOINT: BOOM UP" : "[E] PAY THE TOLL (" + (Friend ? "FREE FOR FRIENDS" : Toll + " SCRAP") + ")";
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary) TryOpen(g); }

        /// <summary>A horn blast near a checkpoint: the toll is paid from the pack and the boom lifts.</summary>
        public static void Horn(Vector3 at, MadMax.Vehicles.VehicleDriver car)
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g) return;
            foreach (var c in All) if (c && (c.transform.position - at).sqrMagnitude < 40f * 40f) { c.TryOpen(g); return; }
        }

        void TryOpen(MadMax.Game.WastelandGame g)
        {
            if (broken || Time.time < openUntil) return;
            if (!Friend && !g.Inventory.TrySpend(MadMax.Items.ResourceType.Scrap, Toll)) { g.Toast("NO SCRAP FOR THE TOLL: THE BOOM STAYS DOWN"); return; }
            openUntil = Time.time + 30f;
            MadMax.Audio.Sfx.Play("creak", boom ? boom.position : transform.position, 0.6f, 1.3f, 40f);
            g.Toast(Friend ? "THE REMNANTS WAVE YOU THROUGH" : "TOLL PAID (" + Toll + " SCRAP): THE BOOM LIFTS");
        }

        void Update()
        {
            if (!boom || broken) return;
            float want = Time.time < openUntil ? 80f : 0f;
            angle = Mathf.MoveTowards(angle, want, 60f * Time.deltaTime);
            boom.rotation = down * Quaternion.Euler(-angle, 0f, 0f);
            boomCol.enabled = angle < 30f;
            var g = MadMax.Game.WastelandGame.Instance;
            var car = g ? g.Current : null;
            if (!car || angle > 30f) return;
            // along the boom: how close is the car to it, and how fast
            var p = car.transform.position;
            var local = boom.InverseTransformPoint(p);
            float d = Mathf.Abs(local.x) + Mathf.Max(0f, Mathf.Abs(local.y) - 1.5f);
            bool alongside = local.z > -2f && local.z < 8f;
            if (!alongside) return;
            float speed = car.Body ? car.Body.linearVelocity.magnitude : 0f;
            if (d < 3.5f && speed > 7f) { Snap(g); return; }
            if (d < 25f && Time.time > nagAt) { nagAt = Time.time + 25f; g.Toast("CHECKPOINT: HONK TO PAY THE TOLL (" + (Friend ? "FREE" : Toll + " SCRAP") + ")"); }
        }

        void Snap(MadMax.Game.WastelandGame g)
        {
            broken = true;
            if (DebrisSystem.Instance) DebrisSystem.Instance.EmitPuff(boom.position + boom.forward * length * 0.5f, new Color32(200, 60, 50, 255), 0.08f, Vector3.up, 1f);
            MadMax.Audio.Sfx.Play("hit_wood", boom.position, 1f, 0.8f, 60f);
            Destroy(boom.gameObject);
            Factions.Shift(Faction.Remnants, -6);
            g.Toast("YOU SMASHED THE REMNANT BOOM - THEY WON'T FORGET");
            NpcDirector.Instance?.Noise(transform.position, 40f);
        }
    }
}
