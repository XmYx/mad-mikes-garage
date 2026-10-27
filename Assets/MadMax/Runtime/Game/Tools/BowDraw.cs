using MadMax.Building;
using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The live string of a bow, crossbow or slingshot (added by <see cref="ToolLibrary.Create"/>): two cord halves
    /// from the limb / prod / fork tips to the nock, posed every frame. Bow and slingshot draw back while the player aims
    /// with a round loaded and slap forward on the shot; the crossbow string sits forward after a shot and is spanned back
    /// to the latch in ratchet steps over the reload. The arrow / bolt / stone shows while one is loaded and rides the nock.
    /// Built in the look in use: voxels on the stringless held mesh, or <see cref="HDShapes"/> standing in for the imported
    /// HD model in the hand (that one has its string baked in). Held by NPCs (tool disabled) or remote players: a resting
    /// string, nothing loaded.</summary>
    public class BowDraw : MonoBehaviour
    {
        public enum Kind { Bow, Crossbow, Slingshot }

        public RangedTool tool;
        public Kind kind;
        /// <summary>0 = string at rest (crossbow: forward, unspanned) .. 1 = full draw / spanned to the latch.</summary>
        public float DrawAmount { get; private set; }
        /// <summary>An arrow / bolt / stone sits on the string (the player's loaded weapon only).</summary>
        public bool Nocked { get; private set; }
        /// <summary>The nock (string centre / pouch), tool space.</summary>
        public Vector3 NockLocal => nock;
        /// <summary>Metres the nock stands back from its rest point (tool space; tests).</summary>
        public float NockOffset => Vector3.Dot(nock - rest, back);
        /// <summary>Shots released by this weapon.</summary>
        public int Releases { get; private set; }
        /// <summary>The loaded arrow / bolt / stone is drawn.</summary>
        public bool LoadShown => loadParts != null && loadParts.Length > 0 && loadParts[0] && loadParts[0].enabled;
        public bool Smooth => hd;

        public static bool Handles(string id) => id == "tool_bow" || id == "tool_crossbow" || id == "tool_slingshot";

        const float S = VoxelMesher.DefaultSize;
        const float CordLength = 9f * S;                     // the cord mesh, scaled to each half

        Transform cordA, cordB, load, pouch;
        Vector3 tipA, tipB, rest, full, back, nock, upHint, loadOffset;
        float drawSeconds, overshoot, firedAt = -10f;
        int ratchetStep;
        bool hd, built, wasChambering;
        Renderer host;
        Renderer[] parts, loadParts;

        /// <summary>Lay out and build the string for <paramref name="id"/> (called once, right after the tool is made).</summary>
        public void Setup(RangedTool t, string id)
        {
            tool = t;
            kind = id == "tool_crossbow" ? Kind.Crossbow : id == "tool_slingshot" ? Kind.Slingshot : Kind.Bow;
            host = GetComponent<Renderer>();
            hd = PieceBits.HD(gameObject);
            var mat = host ? host.sharedMaterial : null;
            if (hd && TryGetComponent<HDVisual>(out var v) && v.root)
            {
                v.root.gameObject.SetActive(false);                 // the imported model's string can't move: stand in for it
                PieceBits.Shape(transform, "BodyHD", Vector3.zero, "bowbody_" + kind, true, null,
                    kind == Kind.Bow ? BowBodyHD : kind == Kind.Crossbow ? (System.Action<HDShapes>)CrossbowBodyHD : SlingshotBodyHD, mat);
            }
            Layout();
            bool band = kind == Kind.Slingshot;
            float cz = hd ? 0f : S * 0.5f;
            cordA = Pivot("CordA", tipA); PieceBits.Shape(cordA, "Cord", new Vector3(0f, 0f, cz), band ? "slingband" : "bowstring", hd, band ? (System.Action<VoxelGrid>)BandVoxel : CordVoxel, band ? (System.Action<HDShapes>)BandHD : CordHD, mat);
            cordB = Pivot("CordB", tipB); PieceBits.Shape(cordB, "Cord", new Vector3(0f, 0f, cz), band ? "slingband" : "bowstring", hd, band ? (System.Action<VoxelGrid>)BandVoxel : CordVoxel, band ? (System.Action<HDShapes>)BandHD : CordHD, mat);
            load = Pivot("Load", rest + loadOffset);
            if (kind == Kind.Bow) PieceBits.Shape(load, "Arrow", Vector3.zero, "nocked_arrow", hd, ArrowVoxel, ArrowHD, mat);
            else if (kind == Kind.Crossbow) PieceBits.Shape(load, "Bolt", Vector3.zero, "nocked_bolt", hd, BoltVoxel, BoltHD, mat);
            else
            {
                PieceBits.Shape(load, "Stone", Vector3.zero, "pouch_stone", hd, StoneVoxel, StoneHD, mat);
                pouch = Pivot("Pouch", rest);
                PieceBits.Shape(pouch, "Leather", Vector3.zero, "sling_pouch", hd, PouchVoxel, PouchHD, mat);
            }
            loadParts = load.GetComponentsInChildren<Renderer>(true);
            var a = cordA.GetComponentsInChildren<Renderer>(true); var b = cordB.GetComponentsInChildren<Renderer>(true);
            var p = pouch ? pouch.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            parts = new Renderer[a.Length + b.Length + p.Length];
            a.CopyTo(parts, 0); b.CopyTo(parts, a.Length); p.CopyTo(parts, a.Length + b.Length);
            nock = rest;
            built = true;
            if (tool) tool.Fired += OnFired;
            Pose();
        }

        /// <summary>Tips, rest and full-draw nock per weapon (tool space, metres; -Y = forward, the way it shoots).</summary>
        void Layout()
        {
            switch (kind)
            {
                case Kind.Bow:
                {
                    float y = hd ? 0f : S * 0.5f;
                    tipA = new Vector3(0f, y, -0.72f); tipB = new Vector3(0f, y, 0.72f);
                    rest = new Vector3(0f, y, 0f); full = rest + new Vector3(0f, 0.3f, 0f);
                    upHint = Vector3.right; loadOffset = new Vector3(hd ? 0.03f : S, 0f, 0f);
                    drawSeconds = 0.7f; overshoot = 0.05f;
                    break;
                }
                case Kind.Crossbow:
                {
                    float z = hd ? 0.05f : S;
                    tipA = new Vector3(-0.48f, -1.04f, 0f); tipB = new Vector3(0.48f, -1.04f, 0f);
                    rest = new Vector3(0f, -0.99f, z); full = new Vector3(0f, -0.64f, z);
                    upHint = Vector3.forward; loadOffset = new Vector3(0f, 0f, hd ? 0.01f : 0f);
                    drawSeconds = 0f; overshoot = 0.035f;
                    break;
                }
                default:
                {
                    float x = hd ? 0.15f : 2f * S, y = hd ? -0.62f : -8f * S, z = hd ? 0.04f : S;
                    tipA = new Vector3(-x, y, 0f); tipB = new Vector3(x, y, 0f);
                    rest = new Vector3(0f, y, z); full = rest + new Vector3(0f, 0.3f, hd ? 0.03f : S * 0.5f);
                    upHint = Vector3.forward; loadOffset = new Vector3(0f, hd ? -0.02f : -S, 0f);
                    drawSeconds = 0.45f; overshoot = 0.06f;
                    break;
                }
            }
            back = (full - rest).normalized;
        }

        Transform Pivot(string n, Vector3 at)
        {
            var t = new GameObject(n).transform;
            t.gameObject.layer = gameObject.layer;
            t.SetParent(transform, false);
            t.localPosition = at;
            return t;
        }

        void OnFired()
        {
            firedAt = Time.time;
            Releases++;
            DrawAmount = 0f;
            ratchetStep = 0;
        }

        void OnDestroy() { if (tool) tool.Fired -= OnFired; }

        void LateUpdate()
        {
            if (!built) return;
            var g = WastelandGame.Instance;
            bool mine = tool && tool.enabled && g && g.Player && g.Player.Tool == tool;
            bool loaded = mine && g.Rounds(tool.id) > 0 && !tool.Reloading;
            float dt = Time.deltaTime;
            if (kind == Kind.Crossbow)
            {
                // spanned in eight ratchet steps over the reload, each a click of the windlass
                bool ch = mine && tool.Chambering;
                int step = ch ? Mathf.FloorToInt(tool.ReloadProgress * 8f) : 0;
                if (ch && step > ratchetStep) MadMax.Audio.Sfx.Play("ratchet", transform.position, 0.3f, 1.9f, 15f);
                ratchetStep = ch ? step : 0;
                float want = !mine ? 0f : ch ? step / 8f : loaded ? 1f : 0f;
                DrawAmount = Mathf.MoveTowards(DrawAmount, want, dt * 4f);
                if (wasChambering && !ch && loaded) MadMax.Audio.Sfx.Play("click", transform.position, 0.45f, 1.4f, 15f);   // the latch catches
                wasChambering = ch;
            }
            else
            {
                float want = loaded && g.Aiming ? 1f : 0f;
                if (want > 0f && DrawAmount <= 0f && kind == Kind.Bow) MadMax.Audio.Sfx.Play("creak", transform.position, 0.2f, 1.8f, 12f);
                DrawAmount = Mathf.MoveTowards(DrawAmount, want, dt / (want > DrawAmount ? drawSeconds : 0.2f));
            }
            Nocked = loaded;
            Pose();
        }

        void Pose()
        {
            float d = DrawAmount;
            float e = kind == Kind.Crossbow ? d : d * (2f - d);          // heavier toward full draw
            nock = rest + (full - rest) * e;
            float t = Time.time - firedAt;
            if (t < 0.6f) nock -= back * (overshoot * Mathf.Exp(-t * 9f) * Mathf.Cos(t * 55f));   // slaps past rest and settles
            Aim(cordA, tipA); Aim(cordB, tipB);
            load.localPosition = nock + loadOffset;
            if (pouch) pouch.localPosition = nock;
            bool show = !host || host.enabled;
            for (int i = 0; i < parts.Length; i++) if (parts[i] && parts[i].enabled != show) parts[i].enabled = show;
            bool ls = show && Nocked;
            for (int i = 0; i < loadParts.Length; i++) if (loadParts[i] && loadParts[i].enabled != ls) loadParts[i].enabled = ls;
        }

        void Aim(Transform cord, Vector3 tip)
        {
            var dir = nock - tip;
            float len = dir.magnitude;
            if (len < 1e-4f) return;
            cord.localRotation = Quaternion.LookRotation(dir / len, upHint);
            cord.localScale = new Vector3(1f, 1f, len / CordLength);
        }

        // ---------------------------------------------------------------- meshes (pivot = origin; cords run along +Z)
        static void CordVoxel(VoxelGrid g) => g.Box(0, 0, 0, 0, 0, 8, Pal.Solid(Pal.Cream[3]));
        static void BandVoxel(VoxelGrid g) => g.Box(0, 0, 0, 0, 0, 8, Pal.Ramp(Pal.Crimson, 3, 1331));
        static void CordHD(HDShapes s) => s.Tube(Vector3.zero, new Vector3(0f, 0f, CordLength), 0.004f, 0.004f, Pal.Cream[3], 5);
        static void BandHD(HDShapes s) => s.Tube(Vector3.zero, new Vector3(0f, 0f, CordLength), 0.007f, 0.007f, Pal.Crimson[3], 6);

        static void ArrowVoxel(VoxelGrid g)
        {
            g.Box(0, -8, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 3, 1332));                                          // shaft
            g.Set(0, -9, 0, Pal.Solid(Pal.Chrome[2]));                                                      // head
            g.Set(0, -1, 1, Pal.Solid(Pal.Crimson[3])); g.Set(0, -1, -1, Pal.Solid(Pal.Crimson[3]));        // fletching
        }

        static void ArrowHD(HDShapes s)
        {
            s.Tube(Vector3.zero, new Vector3(0f, -0.7f, 0f), 0.005f, 0.005f, Pal.Wood[3], 6);
            s.Tube(new Vector3(0f, -0.7f, 0f), new Vector3(0f, -0.77f, 0f), 0.012f, 0.001f, Pal.Chrome[2], 6, true);
            for (int i = 0; i < 3; i++)
            {
                var q = Quaternion.Euler(0f, i * 120f, 0f);
                s.Box(q * new Vector3(0f, -0.07f, 0.012f), new Vector3(0.002f, 0.08f, 0.018f), Pal.Crimson[3], 0.001f, q);
            }
        }

        static void BoltVoxel(VoxelGrid g)
        {
            g.Box(0, -4, 0, 0, 0, 0, Pal.Ramp(Pal.Wood, 2, 1333));
            g.Set(0, -5, 0, Pal.Solid(Pal.Chrome[3]));
            g.Set(1, 0, 0, Pal.Solid(Pal.Cream[2])); g.Set(-1, 0, 0, Pal.Solid(Pal.Cream[2]));            // vanes
        }

        static void BoltHD(HDShapes s)
        {
            s.Tube(Vector3.zero, new Vector3(0f, -0.36f, 0f), 0.007f, 0.007f, Pal.Wood[2], 6);
            s.Tube(new Vector3(0f, -0.36f, 0f), new Vector3(0f, -0.42f, 0f), 0.013f, 0.001f, Pal.Chrome[3], 6, true);
            s.Box(new Vector3(0f, -0.04f, 0f), new Vector3(0.04f, 0.06f, 0.002f), Pal.Cream[2], 0.001f);
        }

        static void StoneVoxel(VoxelGrid g) => g.Set(0, 0, 0, Pal.Ramp(Pal.Fur, 1, 1334));
        static void StoneHD(HDShapes s) => s.Ellipsoid(Vector3.zero, new Vector3(0.02f, 0.018f, 0.017f), Pal.Fur[2], 7, 4);
        static void PouchVoxel(VoxelGrid g) => g.Set(0, 0, 0, Pal.Solid(Pal.Wood[0]));
        static void PouchHD(HDShapes s) => s.Box(Vector3.zero, new Vector3(0.05f, 0.03f, 0.012f), Pal.Wood[1], 0.004f);

        // the HD bodies follow tools/blender/hd/items/tools.py (bow, crossbow, slingshot) without their strings
        static void BowBodyHD(HDShapes s)
        {
            Vector3 prev = Vector3.zero; float pr = 0f;
            for (int k = 0; k <= 16; k++)
            {
                float u = -1f + k / 8f;
                var p = new Vector3(0f, -(1f - u * u) * 0.24f, u * 0.72f);
                float r = 0.01f + 0.012f * (1f - Mathf.Abs(u));
                if (k > 0) s.Tube(prev, p, pr, r, Pal.Wood[2], 8, k == 1 || k == 16);
                prev = p; pr = r;
            }
            s.Tube(new Vector3(0f, -0.24f, -0.06f), new Vector3(0f, -0.24f, 0.06f), 0.026f, 0.026f, Pal.Black[2], 10, true);   // grip
        }

        static void CrossbowBodyHD(HDShapes s)
        {
            s.Box(new Vector3(0f, -0.5f, 0f), new Vector3(0.06f, 1.16f, 0.08f), Pal.Wood[2], 0.012f);       // stock
            s.Box(new Vector3(0f, 0.02f, 0f), new Vector3(0.08f, 0.2f, 0.12f), Pal.Wood[2], 0.02f);         // butt
            Vector3 prev = Vector3.zero;
            for (int k = 0; k <= 12; k++)                                                                   // prod
            {
                float u = -1f + k / 6f;
                var p = new Vector3(u * 0.48f, -1.04f, (1f - u * u) * 0.08f);
                if (k > 0) s.Tube(prev, p, 0.014f, 0.014f, Pal.Chrome[1], 8, k == 1 || k == 12);
                prev = p;
            }
            s.Box(new Vector3(0f, -0.12f, -0.06f), new Vector3(0.02f, 0.08f, 0.06f), Pal.Metal[1], 0.004f); // trigger
            s.Box(new Vector3(0f, -0.7f, 0.045f), new Vector3(0.02f, 0.6f, 0.012f), Pal.Metal[0], 0.002f);  // bolt rail
        }

        static void SlingshotBodyHD(HDShapes s)
        {
            s.Tube(Vector3.zero, new Vector3(0f, -0.36f, 0f), 0.022f, 0.022f, Pal.Wood[2], 10, true);
            for (int k = -1; k <= 1; k += 2)
            {
                var m = new Vector3(k * 0.08f, -0.44f, 0f);
                s.Tube(new Vector3(0f, -0.36f, 0f), m, 0.022f, 0.018f, Pal.Wood[2], 10);
                s.Tube(m, new Vector3(k * 0.15f, -0.62f, 0f), 0.018f, 0.016f, Pal.Wood[2], 10, true);
            }
        }
    }
}
