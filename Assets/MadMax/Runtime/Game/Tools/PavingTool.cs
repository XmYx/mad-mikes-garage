using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Hand paving (depth stage C, the first rung of the road ladder). The road rake spreads a unit of gravel
    /// into a ~1.2 m patch in front of the player and levels it a little (raking gravel that is already down only
    /// levels it); the tamper sets cobbles from stone the same way. The rake also patches a pothole in reach with a
    /// unit of asphalt. Terrain edits go through <see cref="DeformableTerrain.ApplyTerraform"/> and are replicated.</summary>
    public class PavingTool : HandTool
    {
        /// <summary>What a strike lays: <see cref="DeformableTerrain.PaveGravel"/> (rake) or <see cref="DeformableTerrain.PaveCobbles"/> (tamper).</summary>
        public byte kind = DeformableTerrain.PaveGravel;
        public ResourceType material = ResourceType.Gravel;
        public int cost = 1;
        public float radius = 1.2f;
        /// <summary>Patch centre ahead of the player (m).</summary>
        public float reach = 1.4f;

        string Title => kind == DeformableTerrain.PaveCobbles ? "TAMPER" : "RAKE";

        static float Terraform(DeformableTerrain.TerraOp op, Vector3 p, float r, float amount, byte extra)
        {
            float done = DeformableTerrain.Instance.ApplyTerraform((byte)op, p, r, amount, extra);
            MadMax.Net.NetSession.Instance?.SendTerraform((byte)op, p, r, amount, extra);
            return done;
        }

        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance; var t = DeformableTerrain.Instance;
            if (!g || !t || t.World == null) return;
            var front = user.transform.position + user.transform.forward * reach;
            var at = new Vector3(front.x, t.Height(front.x, front.z), front.z);

            // the rake patches a pothole in reach with asphalt from the pack
            if (kind == DeformableTerrain.PaveGravel && FindPothole(t, at, 1f, out var hole))
            {
                if (g.Inventory.Get(ResourceType.Asphalt) > 0)
                {
                    if (!g.Vitals.Spend(6f)) return;
                    if (Terraform(DeformableTerrain.TerraOp.Pothole, hole, 0.65f, 0f, 1) > 0f)
                    {
                        g.Inventory.TrySpend(ResourceType.Asphalt, 1);
                        Puff(hole, new Color(0.2f, 0.18f, 0.17f, 0.6f));
                        g.Stats.Practice(MadMax.RPG.Skill.Construction, 1f);
                        g.WearTool(id, 0.01f);
                        g.Toast("PATCHED THE POTHOLE (-1 ASPHALT): KEEP OFF TILL IT SETS");
                        return;
                    }
                }
                else g.Toast("POTHOLE: ASPHALT PATCHES IT (GRAVEL ONLY FILLS IT)");
            }

            string why = Refuse(t, at);
            if (why != null) { g.Toast(Title + ": " + why); return; }
            if (!g.Vitals.Spend(kind == DeformableTerrain.PaveCobbles ? 10f : 6f)) return;
            Terraform(DeformableTerrain.TerraOp.Flatten, at, radius, at.y, 0);                       // level the patch towards its middle
            bool enough = g.Inventory.Get(material) >= cost;
            float laid = enough ? Terraform(DeformableTerrain.TerraOp.Pave, at, radius, 0f, kind) : 0f;
            MadMax.Audio.Sfx.Play("dig", at, 0.5f, kind == DeformableTerrain.PaveCobbles ? 0.8f : 1.2f, 15f);
            if (laid <= 0f)
            {
                g.Toast(!enough && t.PaveAt(at.x, at.z) != kind ? Title + ": NEEDS " + cost + " " + ResourceInfo.Name(material) + (material == ResourceType.Gravel ? " (CRUSH STONE)" : "")
                                                                 : kind == DeformableTerrain.PaveCobbles ? "TAMPED THE COBBLES DOWN" : "LEVELLED THE GRAVEL");
                return;
            }
            g.Inventory.TrySpend(material, cost);
            Puff(at, kind == DeformableTerrain.PaveCobbles ? new Color(0.5f, 0.48f, 0.45f, 0.6f) : new Color(0.55f, 0.5f, 0.44f, 0.7f));
            g.Stats.Practice(MadMax.RPG.Skill.Construction, 1.5f);
            g.WearTool(id, 0.01f);
            g.Toast((kind == DeformableTerrain.PaveCobbles ? "SET COBBLES" : "RAKED A GRAVEL PATCH") + " (-" + cost + " " + ResourceInfo.Name(material) + ")");
        }

        /// <summary>Why the patch can't go here (null = it can): water, a slope, a hard road already.</summary>
        string Refuse(DeformableTerrain t, Vector3 at)
        {
            if (t.WaterDepth(at.x, at.z) > 0.05f) return "NOT UNDER WATER";
            if (Vector3.Angle(t.Normal(at.x, at.z), Vector3.up) > 24f) return "TOO STEEP";
            if (t.HardRoadAt(at.x, at.z) && t.PaveAt(at.x, at.z) != kind) return "ALREADY PAVED";
            return null;
        }

        static bool FindPothole(DeformableTerrain t, Vector3 at, float r, out Vector3 hole)
        {
            const float c = DeformableTerrain.Cell;
            for (float dx = -r; dx <= r; dx += c)
            for (float dz = -r; dz <= r; dz += c)
            {
                if (dx * dx + dz * dz > r * r) continue;
                float x = at.x + dx, z = at.z + dz;
                if (t.PaveAt(x, z) != DeformableTerrain.PavePothole) continue;
                hole = new Vector3(x, t.Height(x, z), z);
                return true;
            }
            hole = default;
            return false;
        }

        static void Puff(Vector3 at, Color c)
        {
            for (int i = 0; i < 3; i++) Fx.Smoke(at + Random.insideUnitSphere * 0.5f + Vector3.up * 0.15f, Vector3.up * 0.4f, 0.4f, c, 0.8f);
        }
    }
}
