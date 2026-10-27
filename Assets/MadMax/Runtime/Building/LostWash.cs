using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A garment a storm tore off a <see cref="ClothesLine"/> (2026-10-26): a cloth lying downwind in the scrub,
    /// stirring in the wind. [E] picks it up: back on the line if the wash is still out, else it is found a little torn.</summary>
    public class LostWash : MonoBehaviour, IInteractable
    {
        public string garment;
        public ClothesLine line;
        Transform cloth;

        public static LostWash Spawn(ClothesLine line, string garment, Vector3 at, Color colour)
        {
            var go = new GameObject("LostWash " + garment);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.value * 360f, 0f));
            var w = go.AddComponent<LostWash>();
            w.garment = garment; w.line = line;
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(c.GetComponent<Collider>());
            c.name = "Cloth";
            w.cloth = c.transform; w.cloth.SetParent(go.transform, false);
            w.cloth.localPosition = new Vector3(0f, 0.04f, 0f);
            w.cloth.localScale = new Vector3(0.42f, 0.03f, 0.5f);
            var m = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", colour * 0.85f);
            c.GetComponent<MeshRenderer>().sharedMaterial = m;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.center = new Vector3(0f, 0.15f, 0f); box.size = new Vector3(0.6f, 0.3f, 0.6f);
            return w;
        }

        void OnEnable() => MadMax.Vehicles.PartFunctions.Interactables.Add(this);
        void OnDisable() => MadMax.Vehicles.PartFunctions.Interactables.Remove(this);

        void OnDestroy()
        {
            var r = cloth ? cloth.GetComponent<MeshRenderer>() : null;
            if (r && r.sharedMaterial) Destroy(r.sharedMaterial);
        }

        void Update()
        {
            if (!cloth) return;
            float w = Fx.Wind.magnitude;
            cloth.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * (2f + w * 0.3f) + transform.position.x) * w * 1.6f, 0f, Mathf.Sin(Time.time * 1.3f) * w);
        }

        public string Prompt(MadMax.Game.WastelandGame g) => "[E] PICK UP YOUR " + MadMax.Items.ItemIds.Name(garment) + " (BLOWN OFF THE LINE)";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (line && line.Hung) { line.Return(garment); g.Toast("PEGGED YOUR " + MadMax.Items.ItemIds.Name(garment) + " BACK ON THE LINE"); }
            else
            {
                var d = MadMax.Game.ClothingLibrary.Get(garment);
                if (d != null) { g.ClothWear.TryGetValue(d.id, out float wr); g.ClothWear[d.id] = Mathf.Min(0.95f, wr + 0.08f); }
                if (line) line.Found(garment);
                g.Toast("FOUND YOUR " + MadMax.Items.ItemIds.Name(garment) + " IN THE SCRUB: A LITTLE TORN");
            }
            MadMax.Audio.Sfx.Play("scratch", transform.position, 0.3f, 1.1f, 6f);
            Destroy(gameObject);
        }
    }
}
