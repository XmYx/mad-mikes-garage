using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Scarecrow: keeps crows off the garden plots and the sprinkler puddles within 12 m. Crows have a memory
    /// (2026-10-23): it frightens them for <see cref="FreshDays"/> days after it is put up, moved or dressed, then they
    /// grow used to it (<see cref="Fresh"/> false; they come back to the beds). [E] with a garment in the pack dresses it
    /// in that garment (the coat hangs on it in its colour) and frightens them anew; moving it does too.</summary>
    public class Scarecrow : MonoBehaviour, IInteractable, IPlaceState
    {
        public const float Reach = 12f, FreshDays = 4f;
        static readonly List<Scarecrow> all = new List<Scarecrow>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => all.Clear();
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// <summary>World days when it was last put up, moved or dressed (-1: not yet, set on Start).</summary>
        public float since = -1f;
        /// <summary>The garment it wears (item id; null: its own patched coat).</summary>
        public string dressed;
        Vector3 placedAt;
        Transform coat;
        bool warned;

        /// <summary>Still frightening crows (they haven't got used to it).</summary>
        public bool Fresh => since >= 0f && DayNight.TotalDays - since < FreshDays;

        void Start()
        {
            if (since < 0f) since = DayNight.TotalDays;
            placedAt = transform.position;
            ShowCoat();
        }

        void Update()
        {
            if ((transform.position - placedAt).sqrMagnitude > 0.25f)                                    // moved: a new thing to fear
            {
                placedAt = transform.position; since = DayNight.TotalDays; warned = false;
                GetComponent<Placeable>()?.Dirty();
            }
        }

        public static bool Near(Vector3 p)
        {
            foreach (var s in all) if (s && s.Fresh && (s.transform.position - p).sqrMagnitude < Reach * Reach) return true;
            return false;
        }

        /// <summary>A scarecrow the crows have got used to within reach of <paramref name="p"/> (for the hint).</summary>
        public static Scarecrow Stale(Vector3 p)
        {
            foreach (var s in all) if (s && !s.Fresh && (s.transform.position - p).sqrMagnitude < Reach * Reach) return s;
            return null;
        }

        /// <summary>Once per staleness: tell the player the crows aren't fooled any more.</summary>
        public static void Notice(Vector3 p, MadMax.Game.WastelandGame g)
        {
            var s = Stale(p);
            if (!s || s.warned || !g) return;
            s.warned = true;
            g.Toast("THE CROWS HAVE GOT USED TO THE SCARECROW: MOVE IT OR DRESS IT IN SOMETHING NEW");
        }

        public string Prompt(MadMax.Game.WastelandGame g) => (Fresh ? "SCARECROW (THE CROWS KEEP OFF)" : "SCARECROW (THE CROWS ARE USED TO IT)")
            + (FirstGarment(g) != null ? "  [E] DRESS IT" : "");

        static string FirstGarment(MadMax.Game.WastelandGame g)
        {
            if (g == null || g.Inventory == null) return null;
            foreach (var kv in g.Inventory.Items)
                if (kv.Value > 0 && kv.Key.StartsWith("cloth_") && kv.Key.IndexOf(MadMax.Game.BagLibrary.Sep) < 0 && MadMax.Game.ClothingLibrary.Get(kv.Key) != null) return kv.Key;
            return null;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            var id = FirstGarment(g);
            if (id == null) { g.Toast(Fresh ? "THE CROWS STILL KEEP OFF IT" : "THE CROWS ARE USED TO IT: DRESS IT IN A GARMENT FROM THE PACK, OR MOVE IT"); return; }
            Dress(id, g.Inventory);
            g.Toast("DRESSED THE SCARECROW IN THE " + MadMax.Items.ItemIds.Name(id) + ": THE CROWS WILL KEEP OFF A WHILE");
        }

        /// <summary>Put garment <paramref name="id"/> on it (taken from <paramref name="inv"/> when given; the old one goes back in).</summary>
        public bool Dress(string id, MadMax.Items.Inventory inv)
        {
            if (inv != null && !inv.TakeItem(id)) return false;
            if (inv != null && dressed != null) inv.AddItem(dressed);
            dressed = id; since = DayNight.TotalDays; warned = false;
            MadMax.Audio.Sfx.Play("scratch", transform.position + Vector3.up, 0.4f, 0.8f, 8f);
            ShowCoat();
            GetComponent<Placeable>()?.Dirty();
            return true;
        }

        /// <summary>The worn garment: a cloth panel over the cross-pole in the garment's colour, flapping in the wind.</summary>
        void ShowCoat()
        {
            var d = dressed != null ? MadMax.Game.ClothingLibrary.Get(dressed) : null;
            if (d == null) { if (coat) coat.gameObject.SetActive(false); return; }
            if (!coat)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Coat";
                Destroy(go.GetComponent<Collider>());
                coat = go.transform; coat.SetParent(transform, false);
                coat.localPosition = new Vector3(0f, 1.25f, 0.06f);
                coat.localScale = new Vector3(0.62f, 0.55f, 0.05f);
                var mat = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
                coat.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            coat.gameObject.SetActive(true);
            Color c = new Color(0.45f, 0.38f, 0.3f);
            if (d.paint != null) { try { c = d.paint(Vector3Int.zero, MadMax.Game.BodyPart.Chest, 0.5f); } catch { } }
            c.a = 1f;
            coat.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", c);
        }

        void LateUpdate()
        {
            if (!coat || !coat.gameObject.activeSelf) return;
            float w = Fx.Wind.magnitude;
            coat.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * (1.5f + w * 0.4f) + transform.position.x) * (3f + w * 2.2f), 0f, Mathf.Sin(Time.time * 0.9f) * 2f);
        }

        public string SaveState() => since.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "|" + (dressed ?? "");
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var f = s.Split('|');
            float.TryParse(f[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out since);
            dressed = f.Length > 1 && f[1].Length > 0 ? f[1] : null;
            ShowCoat();
        }
    }
}
