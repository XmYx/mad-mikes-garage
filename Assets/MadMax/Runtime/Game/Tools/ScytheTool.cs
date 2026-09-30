using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scythe (depth stage E): a sweep in front of you cuts the standing grass, flowers and reeds into hay
    /// (<see cref="DeformableTerrain.Mow"/>; the swath grows back over days) and reaps ripe grain in beds within reach —
    /// the grain to the pack, the straw to hay. Green country gives an armful a sweep, the desert a wisp.</summary>
    public class ScytheTool : HandTool
    {
        public const float Reach = 1.2f;
        /// <summary>Crops whose stalks make hay when reaped (seed id → hay per bed).</summary>
        static int Straw(string crop) => crop == "seed_wheat" ? 3 : crop == "seed_corn" ? 2 : 0;
        static float carry;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => carry = 0f;

        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            var t = DeformableTerrain.Instance;
            if (!g || !t) return;
            if (!g.Vitals.Spend(4f)) return;
            var front = user.transform.position + user.transform.forward * 1.1f;
            int hay = 0, reaped = 0;
            foreach (var plot in GardenPlot.All.ToArray())
            {
                if (!plot || !plot.Ripe || Straw(plot.crop) == 0) continue;
                var d = plot.transform.position - front; d.y = 0f;
                if (d.magnitude > Reach + 0.8f) continue;
                int straw = Straw(plot.crop);
                if (!plot.Reap(g, 1f)) continue;                                                      // the grain to the pack
                hay += straw; reaped++;
            }
            float cover = t.Mow(front, Reach);
            carry += cover * DeformableTerrain.HayPerCell * (1f + g.Stats.Level(MadMax.RPG.Skill.Farming) * 0.04f);
            int cut = Mathf.FloorToInt(carry);
            carry -= cut;
            hay += cut;
            var at = new Vector3(front.x, t.Height(front.x, front.z) + 0.3f, front.z);
            MadMax.Audio.Sfx.Play("scratch", at, 0.5f, Random.Range(1.1f, 1.3f), 15f);
            if (cover > 0.5f || reaped > 0) Fx.Smoke(at, user.transform.right * 0.8f + Vector3.up * 0.4f, 0.3f, MadMax.Voxel.Pal.Ochre[3], 0.9f);
            if (hay > 0) g.Inventory.Add(ResourceType.Hay, hay);
            g.Stats.Practice(MadMax.RPG.Skill.Farming, cover > 0.5f || reaped > 0 ? 0.6f : 0.1f);
            g.WearTool(id, 0.004f);
            if (hay > 0) g.HusbandryTally("hay", hay);
            if (reaped > 0) g.Toast("REAPED " + reaped + " BED" + (reaped > 1 ? "S" : "") + (hay > 0 ? ", +" + hay + " HAY" : ""));
            else if (hay > 0) g.Toast("+" + hay + " HAY");
            else if (cover < 0.5f) g.Toast("NOTHING STANDING TO CUT HERE");
        }
    }
}
