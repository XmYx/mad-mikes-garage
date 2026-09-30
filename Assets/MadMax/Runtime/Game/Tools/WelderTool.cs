using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Welding torch: each pass on a vehicle beats out dents, welds the most damaged part near the torch and
    /// straightens the frame a little. Burns fuel and a scrap rod per pass; sparks and a white-hot glow.</summary>
    public class WelderTool : HandTool
    {
        public Transform tip;
        Light arc;

        void OnEnable()
        {
            if (!arc) { arc = new GameObject("Arc").AddComponent<Light>(); arc.transform.SetParent(tip ? tip : transform, false); arc.type = LightType.Point; arc.color = new Color(0.75f, 0.85f, 1f); arc.range = 4f; }
            arc.intensity = 0f;
        }

        void Update() { if (arc) arc.intensity = Mathf.MoveTowards(arc.intensity, 0f, Time.deltaTime * 20f); }

        public override void Strike(PlayerCharacter user) => WeldAt(user, tip ? tip.position : transform.position, 1);

        /// <summary><paramref name="passes"/> passes on the vehicle at <paramref name="at"/> (a timed weld at a vehicle
        /// runs several at once): rods, fuel, repairs and practice scale with them. False = nothing welded.</summary>
        public bool WeldAt(PlayerCharacter user, Vector3 at, int passes)
        {
            var g = WastelandGame.Instance;
            VehicleDriver v = null; Vector3 hitPoint = at; float best = 1.6f;
            foreach (var c in Physics.OverlapSphere(at, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var d = c.GetComponentInParent<VehicleDriver>();
                if (!d) continue;
                var p = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(at) : c.ClosestPoint(at);
                float dist = Vector3.Distance(p, at);
                if (dist < best) { best = dist; v = d; hitPoint = p; }
            }
            if (!v) { MadMax.Audio.Sfx.Play("sizzle", at, 0.4f, 1.8f); return false; }
            if (g && user == g.Player)
            {
                // the arc blinds without a welding mask
                if (!g.Wearing("welding_mask"))
                {
                    ScreenFader.Flash(new Color(0.85f, 0.95f, 1f), 1.2f);
                    if (Random.value < 0.25f) g.Toast("ARC FLASH! A WELDING MASK WOULD HELP");
                }
                passes = Mathf.Min(passes, g.Inventory.Get(ResourceType.Scrap));
                if (passes <= 0 || !g.Inventory.TrySpend(ResourceType.Scrap, passes)) { g.Toast("WELDER: NEED SCRAP RODS"); return false; }
                int fuel = 0;
                for (int k = 0; k < passes; k++) if (Random.value < 0.35f) fuel++;
                if (fuel > 0 && !g.Inventory.TrySpend(ResourceType.Fuel, fuel)) { g.Toast("WELDER: NEED FUEL FOR THE TORCH"); return false; }
            }
            // the part nearest to the torch gets welded; dents around it are beaten out
            VehiclePart part = null; float pd = 2f;
            foreach (var p in v.GetComponentsInChildren<VehiclePart>())
            {
                if (!p.Socket || p.damage <= 0.001f) continue;
                float d = Vector3.Distance(p.transform.position, hitPoint);
                if (d < pd) { pd = d; part = p; }
            }
            float skill = g ? 1f + g.Stats.Level(MadMax.RPG.Skill.Mechanics) * 0.08f : 1f;
            if (part) part.damage = Mathf.Max(0f, part.damage - 0.12f * skill * passes);
            foreach (var dm in v.GetComponentsInChildren<DeformableMesh>())
                if (dm.IsDamaged && Vector3.Distance(dm.transform.position, hitPoint) < 3f) dm.RepairPartial(1f - Mathf.Pow(1f - Mathf.Min(1f, 0.25f * skill), passes));   // passes compound
            if (v.TryGetComponent<VehicleDamage>(out var vd)) vd.StraightenFrame(0.04f * skill * passes);
            Fx.Sparks(hitPoint, Vector3.up + Random.insideUnitSphere * 0.5f, 14, new Color(1f, 0.9f, 0.6f));
            if (arc) arc.intensity = 6f;
            MadMax.Audio.Sfx.Play("sizzle", hitPoint, 0.8f, 1.4f);
            g?.Stats.Practice(MadMax.RPG.Skill.Mechanics, 2f * passes);
            g?.WearTool(id, 0.01f * passes);
            return true;
        }
    }
}
