using MadMax.Building;
using MadMax.Game;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>[T] drags a carcass of up to 150 kg behind you on foot (to a <see cref="ButcherTable"/>, away from the
    /// pen); [T] again, climbing into a vehicle or pulling it onto something lets go.</summary>
    public class CarcassDrag : MonoBehaviour, IInteractable
    {
        public const float MaxKg = 150f;
        Carcass carcass;
        bool dragging;

        void Awake() => carcass = GetComponent<Carcass>();

        float Kg => carcass && carcass.def != null ? carcass.def.mass * carcass.size : 999f;

        public string Prompt(WastelandGame g)
        {
            if (!carcass || carcass.butchered || carcass.def == null || g.Current || Kg > MaxKg) return null;
            return dragging ? "[T] LET GO" : "[T] DRAG";
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (!secondary || !carcass || carcass.butchered || Kg > MaxKg) return;
            dragging = !dragging;
            MadMax.Audio.Sfx.Play("dig", transform.position, 0.4f, 0.8f);
            if (dragging) g.Toast("DRAGGING THE " + carcass.def.name + (ButcherTable.All.Count > 0 ? " - TO THE BUTCHERING TABLE?" : ""));
        }

        void Update()
        {
            if (!dragging) return;
            var g = WastelandGame.Instance;
            if (!g || !g.Player || g.Current || !carcass || carcass.butchered) { dragging = false; return; }
            var p = g.Player.transform;
            var want = p.position - p.forward * (0.9f + Mathf.Max(0.2f, carcass.def.len * 0.04f * carcass.size));
            var me = transform.position;
            var d = want - me; d.y = 0f;
            if (d.magnitude > 4f) { dragging = false; g.Toast("LOST YOUR GRIP"); return; }
            float slow = Mathf.Lerp(1f, 0.45f, Kg / MaxKg);                                        // heavy ones lag behind
            var step = Vector3.MoveTowards(new Vector3(me.x, 0f, me.z), new Vector3(want.x, 0f, want.z), Mathf.Max(0f, d.magnitude - 0.05f) * Mathf.Min(1f, Time.deltaTime * 6f * slow));
            var t = DeformableTerrain.Instance;
            step.y = t ? t.Height(step.x, step.z) : me.y;
            transform.position = step;
            var face = p.position - step; face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-face), 1f - Mathf.Exp(-4f * Time.deltaTime));
            if (Random.value < Time.deltaTime * 2f && Fresh()) BloodStains.Drip(step);
        }

        bool Fresh() => carcass && carcass.Fresh;
    }
}
