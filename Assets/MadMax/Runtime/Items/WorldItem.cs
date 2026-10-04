using System.Collections.Generic;
using MadMax.Building;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Items
{
    /// <summary>An inventory item or a resource stack out in the world as its own object: dropped (pack page, the tool in
    /// hand), placed (pack page PLACE: a preview on any flat surface) or restored from a save. [E] takes the whole stack.
    /// A light rigidbody that settles and sleeps (frozen far from the player like loose parts, never enough mass to shove
    /// a vehicle), or part of a vehicle's compound when placed on one. Saved in <c>SaveData.blockItems</c>. Online the
    /// host owns its existence: spawns, stack counts and settled poses replicate by <see cref="netId"/>, a client's [E]
    /// asks the host, which hands the stack to one picker only (<c>Net/NetSession.Items</c>).</summary>
    public class WorldItem : MonoBehaviour, IInteractable
    {
        public static readonly List<WorldItem> All = new List<WorldItem>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        /// <summary>Item id, or "res:N" for a resource.</summary>
        public string key;
        public int count = 1;
        /// <summary>Make of a tool / weapon / garment (0 crude .. 2 fine); -1 = none.</summary>
        public float quality = -1f;
        /// <summary>Put down with care (PLACE): it stays where it was set until something knocks it.</summary>
        public bool placed;
        /// <summary>Id shared by all peers online (0 = not announced yet).</summary>
        [System.NonSerialized] public uint netId;

        public Rigidbody Body { get; private set; }
        public BoxCollider Box { get; private set; }
        public ResourceType Resource { get; private set; }
        public bool IsResource => Resource != ResourceType.None;
        /// <summary>Riding on a vehicle: parented, no rigidbody of its own (part of the vehicle's compound).</summary>
        public bool OnVehicle => !Body;
        /// <summary>"3 SCRAP", "12L PETROL", "WRENCH (FINE)".</summary>
        public string Label { get; private set; }
        string prompt;
        bool listed;

        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); SetNear(false); }

        /// <summary>A new object for <paramref name="count"/> of <paramref name="key"/> at <paramref name="pos"/> (its lowest
        /// point). With a <paramref name="vehicle"/> it is parented there without a rigidbody.
        /// <paramref name="visual"/> replaces the catalogue model (loose debris: a scrap sheet, a plank) and is drawn with
        /// <paramref name="mat"/> as is.</summary>
        public static WorldItem Create(string key, int count, float quality, Material mat, Vector3 pos, Quaternion rot, Transform vehicle = null, Mesh visual = null)
        {
            var go = new GameObject("Item " + key);
            go.transform.SetPositionAndRotation(pos, rot);
            var w = go.AddComponent<WorldItem>();
            w.key = key; w.count = Mathf.Max(1, count); w.quality = quality;
            w.Resource = key.StartsWith("res:") && int.TryParse(key.Substring(4), out int r) ? (ResourceType)r : ResourceType.None;
            Bounds b;
            if (visual)
            {
                var vis = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                vis.SetParent(go.transform, false);
                vis.GetComponent<MeshFilter>().sharedMesh = visual;
                vis.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var mb = visual.bounds;
                vis.localPosition = new Vector3(-mb.center.x, -mb.min.y, -mb.center.z);
                b = new Bounds(new Vector3(0f, mb.size.y * 0.5f, 0f), mb.size);
            }
            else WorldItemModels.AddVisual(go.transform, key, mat, out b);
            w.Box = go.AddComponent<BoxCollider>();
            w.Box.center = b.center;
            w.Box.size = Vector3.Max(b.size, Vector3.one * 0.06f);
            if (vehicle) go.transform.SetParent(vehicle, true);
            else
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.linearDamping = 0.3f; rb.angularDamping = 0.8f;
                rb.maxDepenetrationVelocity = 1.5f;                                                 // a pile eases apart, it doesn't burst
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // thin books and plates on mesh ground
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                w.Body = rb;
            }
            w.Refresh();
            return w;
        }

        /// <summary>After the count changed: label, prompt, mass.</summary>
        public void Refresh()
        {
            string name = IsResource ? ResourceInfo.Name(Resource) : ItemCatalog.Name(key);
            string make = quality >= 0f && !IsResource ? " (" + WastelandGame.QualityNames[Mathf.Clamp(Mathf.RoundToInt(quality), 0, 2)] + ")" : "";
            Label = IsResource ? count + (ResourceInfo.IsFluid(Resource) ? "L " : " ") + name : (count > 1 ? count + " " : "") + name + make;
            prompt = "[E] PICK UP " + Label + " (HOLD: ALL NEAR)" + (!IsResource && MadMax.Game.BagLibrary.IsBag(key) ? "  [" + Controls.Name(Controls.Act.Second) + "] LOOK INSIDE" : "");   // a bag on the ground can be looted
            if (Body) Body.mass = Mathf.Clamp(Weight, 0.2f, 8f);                                     // light: a car never notices it
            gameObject.name = "Item " + Label;
        }

        /// <summary>Real weight of the stack (kg).</summary>
        public float Weight => (IsResource ? ItemCatalog.ResourceWeight(Resource) : ItemCatalog.Weight(key)) * count;

        /// <summary>Offered to [E] only while the player is close (the game's item tick keeps that list short).</summary>
        public void SetNear(bool near)
        {
            if (near == listed) return;
            listed = near;
            if (near) MadMax.Vehicles.PartFunctions.Interactables.Add(this);
            else MadMax.Vehicles.PartFunctions.Interactables.Remove(this);
        }

        public string Prompt(WastelandGame g) => prompt;

        public void Use(WastelandGame g, bool secondary)
        {
            if (!secondary) { g.PickUpItem(this); g.ArmLootAll(); }
            else if (!IsResource && MadMax.Game.BagLibrary.IsBag(key)) g.OpenGroundBag(this);
        }
    }
}
