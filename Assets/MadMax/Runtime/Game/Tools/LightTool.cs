using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Hand-held light source (tool_torch, tool_gas_torch, tool_lantern). Carries a point light at the tip
    /// (flickering flame, hissing blue gas jet or steady lantern). Torch and gas torch still strike like a light club,
    /// set flammable things alight, and the gas torch cuts metal like the salvage cutter while burning fuel.</summary>
    public class LightTool : MeleeTool
    {
        public enum Kind { Torch, GasTorch, Lantern, Flashlight }
        public Kind kind;
        Light lamp;
        float seed;

        void OnEnable()
        {
            if (!lamp)
            {
                lamp = new GameObject("Flame").AddComponent<Light>();
                lamp.transform.SetParent(tip ? tip : transform, false);
                lamp.type = kind == Kind.Flashlight ? LightType.Spot : LightType.Point; lamp.shadows = LightShadows.None;
                lamp.color = kind == Kind.GasTorch ? new Color(0.55f, 0.75f, 1f) : kind == Kind.Lantern ? new Color(1f, 0.85f, 0.6f) : kind == Kind.Flashlight ? new Color(0.95f, 0.97f, 1f) : new Color(1f, 0.62f, 0.3f);
                lamp.range = kind == Kind.Lantern ? 9f : kind == Kind.Torch ? 8f : kind == Kind.Flashlight ? 22f : 5f;
                if (kind == Kind.Flashlight) { lamp.spotAngle = 38f; lamp.innerSpotAngle = 18f; }
                seed = Random.value * 100f;
            }
            lamp.enabled = true;
        }

        void OnDisable() { if (lamp) lamp.enabled = false; }

        void Update()
        {
            if (!lamp) return;
            float t = Time.time * (kind == Kind.GasTorch ? 20f : 7f) + seed;
            float baseI = kind == Kind.Lantern ? 2.4f : kind == Kind.Torch ? 2.8f : kind == Kind.Flashlight ? 9f : 1.8f;
            lamp.intensity = baseI * (kind == Kind.Lantern || kind == Kind.Flashlight ? 0.95f + 0.05f * Mathf.PerlinNoise(t * 0.2f, 1f) : 0.7f + 0.5f * Mathf.PerlinNoise(t, 3.1f));
            if (kind == Kind.Flashlight)
            {
                // the beam points where the holder faces (slightly down)
                var holder = GetComponentInParent<PlayerCharacter>();
                var fwd = holder ? holder.transform.forward : transform.forward;
                lamp.transform.rotation = Quaternion.LookRotation(fwd + Vector3.down * 0.25f);
            }
            // a burning torch is used up; the owner's condition bar shows it
            var game = WastelandGame.Instance;
            if (kind == Kind.Torch && game && GetComponentInParent<PlayerCharacter>() == game.Player) game.WearTool(id, Time.deltaTime / 480f);
            if (kind == Kind.Flashlight && game && GetComponentInParent<PlayerCharacter>() == game.Player)
            {
                // runs down over ~40 minutes; a dead battery dims it to nothing
                game.WearTool(id, Time.deltaTime / 2400f);
                if (game.ToolWear.TryGetValue(id, out var charge) && charge >= 1f) lamp.intensity = 0f;
            }
            if (kind == Kind.GasTorch) MadMax.Audio.Sfx.Loop(this, "sizzle", 0.25f, 1.6f, 12f);
            else if (kind == Kind.Torch) MadMax.Audio.Sfx.Loop(this, "fire", 0.2f, 1.3f, 10f);
            if (kind == Kind.Torch && Random.value < Time.deltaTime * 6f && tip)
                Fx.Smoke(tip.position + Vector3.up * 0.1f, Vector3.up * 0.6f + Fx.Wind * 0.2f, 0.12f, new Color(0.25f, 0.22f, 0.2f, 0.45f), 1.2f);
        }

        public override void Strike(PlayerCharacter user)
        {
            if (kind == Kind.Lantern || kind == Kind.Flashlight) return;        // lights, not weapons
            var game = WastelandGame.Instance;
            if (kind == Kind.GasTorch && game && user == game.Player && !game.Inventory.TrySpend(ResourceType.Fuel, 1) && !game.Inventory.TrySpend(ResourceType.Ethanol, 1))
            {
                game.Toast("GAS TORCH: NO FUEL");
                return;
            }
            base.Strike(user);
            var at = tip ? tip.position : transform.position;
            foreach (var c in Physics.OverlapSphere(at, 0.35f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(user.transform)) continue;
                var go = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;
                if (Fire.Flammable(go)) { Fire.Ignite(c.ClosestPoint(at), go.transform, 12f, 0.5f); break; }
            }
            if (kind == Kind.GasTorch) Fx.Sparks(at, -transform.up + Vector3.up, 6, new Color(0.6f, 0.8f, 1f));
        }
    }
}
