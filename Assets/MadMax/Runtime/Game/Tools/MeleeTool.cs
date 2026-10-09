using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Hand-held striking tool: sledgehammer, wrench, club, machete, salvage cutter. The whole shaft sweeps
    /// through targets; salvage tools strip ISalvageable objects instead of denting them.</summary>
    public class MeleeTool : HandTool
    {
        public float power = 1f;
        public float carveRadius = 0.2f;
        public float hitRadius = 0.4f;
        public bool salvage;
        public float salvageAmount = 1f;
        public Transform tip;
        public bool digs;                 // shovel: digs soil where it hits bare ground
        public float woodMult = 1f, stoneMult = 1f;   // axe / pickaxe
        public bool pries;                // crowbar: forces locked doors, lockers and containers
        public float bleeds;              // chance a hit on a person opens a bleeding wound (blades, nails)
        /// <summary>Fraction of the tool's life one blow costs (hits cost more than misses).</summary>
        public float wearPerHit = 0.004f;

        /// <summary>The last blow: 0 = nothing hit, else how hard what it met was (flesh 0.2, soil 0.3, wood 0.45, metal 0.85,
        /// stone 0.95) and where. The swinger reads it for hit-stop, bounce and sparks.</summary>
        public float LastHit { get; private set; }
        public Vector3 LastPoint { get; private set; }

        /// <summary>Long two-handed handles (sledge, pick, shovel, spear, cutter): the left hand goes on the handle.</summary>
        public override Transform SecondGrip => TwoHanded && tip && tip.localPosition.y < -0.9f ? transform : null;

        readonly HashSet<object> done = new HashSet<object>();

        Vector3 lastHand, lastTip;
        int lastFrame = -9;
        static readonly List<Collider> swept = new List<Collider>();
        static readonly HashSet<Collider> sweptSet = new HashSet<Collider>();

        // where the blade was at the end of the last frame: the strike sweeps the arc from there
        void LateUpdate()
        {
            lastHand = transform.position; lastTip = tip ? tip.position : lastHand; lastFrame = Time.frameCount;
        }

        /// <summary>Everything the tool passes through: its shaft now and halfway back along the arc since the last frame
        /// (a fast blow covers 30-60 cm in one frame).</summary>
        List<Collider> Sweep(Vector3 hand, Vector3 end)
        {
            swept.Clear(); sweptSet.Clear();
            void Add(Vector3 a, Vector3 b)
            {
                foreach (var c in Physics.OverlapCapsule(a, b, hitRadius, ~0, QueryTriggerInteraction.Ignore))
                    if (sweptSet.Add(c)) swept.Add(c);
            }
            Add(hand, end);
            if (Time.frameCount - lastFrame <= 2) Add(Vector3.Lerp(lastHand, hand, 0.5f), Vector3.Lerp(lastTip, end, 0.5f));
            return swept;
        }

        void Met(float hardness, Vector3 at)
        {
            if (hardness > LastHit) { LastHit = hardness; LastPoint = at; }
        }

        public override void Strike(PlayerCharacter user)
        {
            LastHit = 0f;
            var hand = transform.position;
            var end = tip ? tip.position : hand;
            var chest = user.transform.position + Vector3.up * 1.2f;
            var dir = (user.transform.forward + Vector3.down * 0.5f).normalized;
            var stats = WastelandGame.Instance ? WastelandGame.Instance.Stats : null;
            float pw = power * (stats == null ? 1f : style == ToolStyle.Overhead ? stats.DemolitionPower : stats.MeleePower) * (WastelandGame.Instance ? WastelandGame.Instance.QualityPower(id) : 1f);
            float salvageMult = stats != null ? stats.SalvageYield * GameRules.Current.yield : 1f;
            done.Clear();
            bool hitSomething = false;
            foreach (var c in Sweep(hand, end))
            {
                if (c.transform.IsChildOf(user.transform)) continue;
                var rb = c.attachedRigidbody;
                var point = HitPoint(c, chest, end);
                if (salvage)
                {
                    var s = c.GetComponentInParent<ISalvageable>();
                    if (s != null && done.Add(s) && s.Salvage(point, salvageAmount * salvageMult, user.gameObject)) { hitSomething = true; Met(0.85f, point); MadMax.Audio.Sfx.Play("grinder", point, 0.7f, 1f, 30f, 0.3f); stats?.Practice(MadMax.RPG.Skill.Salvaging, 3f); if (user.GetComponent<PlayerCharacter>() == WastelandGame.Instance?.Player) WastelandGame.Instance.RollLoot(0.03f); continue; }
                }
                var target = c.GetComponentInParent<IDamageable>();
                if (target != null && done.Add(target))
                {
                    float mult = 1f;
                    string hitSound = style == ToolStyle.Overhead ? "hammer" : "hit_metal";
                    var tgo = (target as Component) ? ((Component)target).gameObject : null;
                    if (tgo)
                    {
                        string n = tgo.name;
                        bool wood = n.StartsWith("Tree") || n.StartsWith("Log") || n.StartsWith("Bush") || n.StartsWith("Placed_tree");
                        bool stone = n.StartsWith("Rock");
                        hitSound = wood ? "hit_wood" : stone ? "hammer" : hitSound;
                        if (wood) mult = woodMult; else if (stone) mult = stoneMult;
                        if (wood && woodMult > 1f && n.StartsWith("Tree") && WastelandGame.Instance && user == WastelandGame.Instance.Player && Random.value < 0.06f)
                        {
                            var b = DeformableTerrain.Instance ? DeformableTerrain.Instance.BiomeAt(point.x, point.z) : Biome.Desert;
                            string sap = b == Biome.Tropical ? "sapling_palm" : b == Biome.Forest ? "sapling_pine" : "sapling_apple";
                            WastelandGame.Instance.Inventory.AddItem(sap); WastelandGame.Instance.Toast("FOUND A " + ItemCatalog.Name(sap));
                        }
                        if (stone && stoneMult > 1f && WastelandGame.Instance && user == WastelandGame.Instance.Player) WastelandGame.Instance.MineOre(point);
                        if (wood && WastelandGame.Instance && user == WastelandGame.Instance.Player && Random.value < (n.StartsWith("Log") ? 0.2f : n.StartsWith("Bush") ? 0.12f : 0.04f))
                        {
                            int bugs = Random.Range(1, 4);                                         // grubs and beetles: bait
                            WastelandGame.Instance.Inventory.AddItem("bait_insects", bugs);
                            WastelandGame.Instance.Toast("SHOOK OUT " + bugs + " INSECTS");
                        }
                    }
                    MadMax.Audio.Sfx.Play(hitSound, point, 0.8f, Random.Range(0.9f, 1.1f));
                    Met(Hardness(target, tgo), point);
                    target.ApplyHit(point, dir, pw * mult, carveRadius * Mathf.Sqrt(mult), user.gameObject);
                    if (bleeds > 0f && target is MadMax.Npc.Npc victim && Random.value < bleeds) victim.Bleed(8f);
                    hitSomething = true;
                    stats?.Practice(style == ToolStyle.Overhead ? MadMax.RPG.Skill.Demolition : MadMax.RPG.Skill.Melee, 2f);
                }
                if (rb && !rb.isKinematic) rb.AddForceAtPosition(dir * Mathf.Min(250f * power, rb.mass * 4f), point, ForceMode.Impulse);
            }
            var game = WastelandGame.Instance;
            bool isPlayer = game && user == game.Player;
            if (pries && isPlayer && game.PryNearest(end)) hitSomething = true;
            if (isPlayer) game.WearTool(id, hitSomething ? wearPerHit : wearPerHit * 0.25f);
            var terrain = DeformableTerrain.Instance;
            if (!hitSomething && digs && terrain && end.y - terrain.Height(end.x, end.z) < 0.45f && WastelandGame.Instance && user == WastelandGame.Instance.Player)
            {
                WastelandGame.Instance.ShovelDig(end);
                Met(0.3f, end);
                return;
            }
            if (!hitSomething && power >= 0.9f && terrain && end.y - terrain.Height(end.x, end.z) < 0.35f)
            {
                terrain.Deform(end, user.transform.forward, user.transform.right, 0.3f, 3000f, 0.6f, 0.2f);
                Met(0.3f, new Vector3(end.x, terrain.Height(end.x, end.z), end.z));
            }
        }

        static float Hardness(IDamageable target, GameObject go)
        {
            if (target is MadMax.Npc.Npc || target is MadMax.Animals.Animal) return 0.2f;
            if (!go) return 0.6f;
            string n = go.name;
            if (n.StartsWith("Rock") || n.StartsWith("Ore_")) return 0.95f;
            if (n.StartsWith("Tree") || n.StartsWith("Log") || n.StartsWith("Bush") || n.StartsWith("Placed_tree")) return 0.45f;
            return go.GetComponentInParent<MadMax.Vehicles.VehicleDriver>() ? 0.85f : 0.6f;
        }

        /// <summary>Surface point where the blow lands: ray from the chest towards the tip, else the tip itself.</summary>
        static Vector3 HitPoint(Collider c, Vector3 chest, Vector3 tip)
        {
            var d = tip - chest;
            if (c.Raycast(new Ray(chest, d.normalized), out var hit, d.magnitude + 0.3f)) return hit.point;
            // ClosestPoint is unsupported on non-convex mesh colliders (structures)
            return c is MeshCollider mc && !mc.convex ? tip : c.ClosestPoint(tip);
        }
    }
}
