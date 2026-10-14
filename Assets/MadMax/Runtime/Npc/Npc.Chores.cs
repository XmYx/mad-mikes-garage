using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Town life by the season: residents walk out to the village crop rows at harvest (the rows they work are
    /// cut, <see cref="DeformableTerrain.Trample"/>, and grow back with the flora), split firewood by the town fire before
    /// the first snow, and the town's fishers sit at the nearest shore at dawn with a rod — the seasons visible in
    /// town, not only in prices and errands (<see cref="NpcLore.Chores"/>).</summary>
    public partial class Npc
    {
        public const string ChoreHarvest = "harvest", ChoreWood = "wood", ChoreFish = "fish";
        string chore, choreTool;
        Vector3 choreSpot, choreFace;
        float choreCheck, choreBeat;
        int choreSteps;
        bool shoreSearched;
        Vector3 shoreSpot, shoreFace;

        /// <summary>The chore this person is out doing now (<see cref="ChoreHarvest"/>, <see cref="ChoreWood"/>,
        /// <see cref="ChoreFish"/>) or null.</summary>
        public string Chore => chore;
        /// <summary>A fisher of their town: sits at the shore at dawn when a lake or river is within reach.</summary>
        public bool Fisher => Profile.role == NpcRole.Resident && Mathf.Abs(Profile.seed >> 3) % 3 == 0;

        /// <summary>Wander-mode hook: walks to and works the season's chore (false: nothing to do, wander as usual).</summary>
        bool ChoreTick(Vector3 me, float dt, ref Vector3 move, ref float speed)
        {
            if ((choreCheck -= dt) <= 0f) { choreCheck = 6f + Random.value * 4f; PickChore(me); }
            if (chore == null) return false;
            var to = Flat(choreSpot - me);
            if (to.magnitude > 0.7f) { move = Toward(choreSpot); speed = 1.3f; return true; }
            faceTarget = choreFace; faceUntil = Time.time + 0.6f;
            if (swing >= 0f || (choreBeat -= dt) > 0f) return true;
            swing = 0f;                                                                           // the tool's swing pose, nothing struck
            var g = MadMax.Game.WastelandGame.Instance;
            bool seen = g && g.Player && (g.Player.transform.position - me).sqrMagnitude < 60f * 60f;
            switch (chore)
            {
                case ChoreHarvest:
                    choreBeat = Random.Range(1.6f, 2.8f);
                    if (seen && DeformableTerrain.Instance) DeformableTerrain.Instance.Trample(choreFace, 0.45f);   // the row is cut
                    if (++choreSteps % 3 == 0)                                                    // on along the row
                    {
                        var along = Flat(choreFace - choreSpot).normalized;
                        var side = new Vector3(along.z, 0f, -along.x);
                        choreSpot += side * 1.2f; choreFace += side * 1.2f;
                        if (choreSteps > 24) choreCheck = 0f;                                    // a row done: find the next
                    }
                    break;
                case ChoreWood:
                    choreBeat = Random.Range(1.3f, 2.2f);
                    if (seen)
                    {
                        MadMax.Audio.Sfx.Play("hit_wood", choreFace, 0.35f, Random.Range(0.9f, 1.1f), 25f, 0.2f);
                        if (DebrisSystem.Instance) DebrisSystem.Instance.EmitPuff(choreFace + Vector3.up * 0.3f, MadMax.Voxel.Pal.Wood[2], 0.05f, Vector3.up * 1.5f, 0.6f);
                    }
                    break;
                default:                                                                          // fishing: a cast now and then
                    choreBeat = Random.Range(14f, 30f);
                    if (seen) MadMax.Audio.Sfx.Play("splash", choreFace, 0.15f, Random.Range(1.2f, 1.5f), 20f, 1f);
                    break;
            }
            return true;
        }

        /// <summary>What the season and the hour ask of this resident, and where.</summary>
        void PickChore(Vector3 me)
        {
            string want = null;
            if (Profile.role == NpcRole.Resident && !companion && !leaving && !Hostile && convoy == null && !aboard)
            {
                float h = DayNight.Hours;
                int season = Weather.Season;
                if (Fisher && season != 2 && h >= 5f && h < 9f) want = ChoreFish;
                else if (h >= 8f && h < 17f && Mathf.Abs(Profile.seed >> 5) % 3 != 0)              // two in three lend a hand
                {
                    int c = NpcLore.ChoreNow();
                    if (c >= 0 && NpcLore.Chores[c].item == "any:harvest") want = ChoreHarvest;
                    else if (c >= 0 && NpcLore.Chores[c].item == "res:" + (int)MadMax.Items.ResourceType.Wood) want = ChoreWood;
                }
            }
            if (want == chore && (chore == null || choreSteps <= 24)) return;
            bool found = want != null && ChoreSpot(want, me);
            if (!found) want = null;
            choreSteps = 0; choreBeat = Random.Range(0.5f, 2f);
            if (want == chore) return;
            chore = want;
            string toolId = chore == ChoreHarvest ? "tool_scythe" : chore == ChoreWood ? "tool_axe" : chore == ChoreFish ? "tool_fishing_rod" : Profile.tool;
            if (toolId != choreTool) { choreTool = toolId; SetTool(toolId, rig.material); }
        }

        /// <summary>Called from the brain when the routine leaves wandering (evening fire, night, a fright).</summary>
        void EndChore()
        {
            if (chore == null) return;
            chore = null;
            if (choreTool != Profile.tool) { choreTool = Profile.tool; SetTool(Profile.tool, rig.material); }
        }

        bool ChoreSpot(string kind, Vector3 me)
        {
            var t = DeformableTerrain.Instance;
            if (!t || t.World == null) return false;
            var w = t.World;
            if (kind == ChoreWood)
            {
                var fire = Campfire.Nearest(home, 60f);
                if (!fire) return false;
                float a = (Mathf.Abs(Profile.seed) % 360) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                choreFace = fire.transform.position + dir * 3.6f;                                 // the chopping block, clear of the logs
                choreSpot = choreFace + dir * 0.9f;
                choreFace.y = t.HeightNoLoad(choreFace.x, choreFace.z);
                return true;
            }
            if (kind == ChoreFish)
            {
                if (!shoreSearched) { shoreSearched = true; FindShore(t); }
                if (shoreSpot == Vector3.zero) return false;
                choreSpot = shoreSpot; choreFace = shoreFace;
                return true;
            }
            // harvest: a crop cell of a village field or a town plan's field near home
            var r = new System.Random(Profile.seed * 13 + DayNight.Day);
            for (int i = 0; i < 40; i++)
            {
                float ang = (float)r.NextDouble() * Mathf.PI * 2f, d = 8f + (float)r.NextDouble() * 70f;
                float x = home.x + Mathf.Cos(ang) * d, z = home.z + Mathf.Sin(ang) * d;
                var s = w.Sample(x, z);
                bool field = s.feature == TownPlan.FField
                             || (s.biome == Biome.Village && s.feature == 0 && Mathf.PerlinNoise(x * 0.02f + 40f, z * 0.02f + 12f) > 0.55f);
                if (!field || t.WaterDepthNoLoad(x, z) > 0.05f) continue;
                choreFace = new Vector3(x, t.HeightNoLoad(x, z), z);
                choreSpot = choreFace + new Vector3(0f, 0f, -0.8f);                               // stooped over the row
                return true;
            }
            return false;
        }

        /// <summary>The nearest dry spot at the edge of open water within 240 m of home (once per person).</summary>
        void FindShore(DeformableTerrain t)
        {
            shoreSpot = Vector3.zero;
            float best = float.MaxValue;
            for (int a = 0; a < 16; a++)
            {
                var dir = new Vector3(Mathf.Cos(a * Mathf.PI / 8f), 0f, Mathf.Sin(a * Mathf.PI / 8f));
                for (float d = 15f; d <= 240f; d += 8f)
                {
                    var p = home + dir * d;
                    if (t.WaterDepthNoLoad(p.x, p.z) < 0.4f) continue;
                    if (d >= best) break;
                    for (float b = d - 1f; b > d - 9f; b -= 1f)                                   // back up to dry ground
                    {
                        var q = home + dir * b;
                        if (t.WaterDepthNoLoad(q.x, q.z) > 0.02f) continue;
                        best = d;
                        shoreSpot = new Vector3(q.x, t.HeightNoLoad(q.x, q.z), q.z);
                        shoreFace = p;
                        break;
                    }
                    break;
                }
            }
        }
    }
}
