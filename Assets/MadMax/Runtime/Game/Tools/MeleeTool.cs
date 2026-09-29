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
        /// <summary>Fraction of the tool's life one blow costs (hits cost more than misses).</summary>
        public float wearPerHit = 0.004f;

        readonly HashSet<object> done = new HashSet<object>();

        public override void Strike(PlayerCharacter user)
        {
            var hand = transform.position;
            var end = tip ? tip.position : hand;
            var chest = user.transform.position + Vector3.up * 1.2f;
            var dir = (user.transform.forward + Vector3.down * 0.5f).normalized;
            var stats = WastelandGame.Instance ? WastelandGame.Instance.Stats : null;
            float pw = power * (stats == null ? 1f : style == ToolStyle.Overhead ? stats.DemolitionPower : stats.MeleePower) * (WastelandGame.Instance ? WastelandGame.Instance.QualityPower(id) : 1f);
            float salvageMult = stats != null ? stats.SalvageYield * GameRules.Current.yield : 1f;
            done.Clear();
            bool hitSomething = false;
            foreach (var c in Physics.OverlapCapsule(hand, end, hitRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(user.transform)) continue;
                var rb = c.attachedRigidbody;
                var point = HitPoint(c, chest, end);
                if (salvage)
                {
                    var s = c.GetComponentInParent<ISalvageable>();
                    if (s != null && done.Add(s) && s.Salvage(point, salvageAmount * salvageMult, user.gameObject)) { hitSomething = true; MadMax.Audio.Sfx.Play("grinder", point, 0.7f, 1f, 30f, 0.3f); stats?.Practice(MadMax.RPG.Skill.Salvaging, 3f); if (user.GetComponent<PlayerCharacter>() == WastelandGame.Instance?.Player) WastelandGame.Instance.RollLoot(0.03f); continue; }
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
                    }
                    MadMax.Audio.Sfx.Play(hitSound, point, 0.8f, Random.Range(0.9f, 1.1f));
                    target.ApplyHit(point, dir, pw * mult, carveRadius * Mathf.Sqrt(mult), user.gameObject);
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
                return;
            }
            if (!hitSomething && power >= 0.9f && terrain && end.y - terrain.Height(end.x, end.z) < 0.35f)
                terrain.Deform(end, user.transform.forward, user.transform.right, 0.3f, 3000f, 0.6f, 0.2f);
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
