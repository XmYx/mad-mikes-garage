using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>What taking one world thing apart involves (<see cref="WastelandGame.SalvagePlanFor"/>).</summary>
    public sealed class SalvagePlan
    {
        public enum Kind { Prop, Spot, Lamp, City }
        public Kind kind;
        /// <summary>The thing: a <see cref="DestructibleVoxels"/> prop, a searched furniture spot (<see cref="Lootable"/>),
        /// a street lamp (<see cref="WorldLamp"/>) or a city piece (<see cref="CitySalvage"/> + <see cref="mark"/>).</summary>
        public Component target;
        public int mark = -1;
        /// <summary>The build piece it is (a <c>prop:&lt;template&gt;</c> design or a library piece) and its name.</summary>
        public string pieceId, name;
        /// <summary>The kit it comes apart into (null: too big, it comes apart into materials).</summary>
        public string kit;
        public SalvageDesigns.Survey survey;
        /// <summary>What it is still made of (a damaged prop gives less).</summary>
        public (ResourceType type, int amount)[] yields;
        public string tool, blocked;
    }

    /// <summary>"If you see it in the game, you can have it" (salvage block): RMB on a world prop (trees, rocks, crates,
    /// barrels, shacks, houses, pumps, landmarks, an abandoned stall), a searched furniture spot, a street lamp or a city
    /// fixture (railing section, lamp post, searchlight, flood, greenhouse, cabin, pod) offers TAKE APART. It needs the
    /// tool for what the thing is mostly made of (wood: claw hammer / crowbar / axe; metal: wrench / cutter / gas torch;
    /// stone and brick: sledgehammer / pickaxe; plants: axe / shovel / machete) and, for big things, Salvaging or
    /// Construction; it is timed work (longer for bigger things, quicker with skill). Small things (≤ 2.5 m, ≤ 150 kg) come
    /// away whole as a kit that places the piece for free; bigger ones leave most of their materials on the ground. Either
    /// way the design is learned (<see cref="SalvageDesigns.Known"/>) and can be built from then on (build menu SALVAGED).
    /// The thing is gone for good (destruction state, <see cref="SalvageDesigns.Removed"/>, city fixture set; saved).</summary>
    public partial class WastelandGame
    {
        sealed class TakeApartJob
        {
            public SalvagePlan plan;
            public float seconds, t, age, fxT;
            public Vector3 point;
            public WorkPose pose;
        }

        TakeApartJob takeApart;
        static readonly Collider[] salvageCols = new Collider[16];

        /// <summary>A take-apart job is running.</summary>
        public bool TakingApart => takeApart != null;
        public float TakeApartProgress => takeApart == null ? 0f : Mathf.Clamp01(takeApart.t / Mathf.Max(0.01f, takeApart.seconds));
        /// <summary>The piece id of the last thing taken apart (tests).</summary>
        public string LastTakenApart { get; private set; }

        // ------------------------------------------------------------------ what it is

        /// <summary>The take-apart plan for a context target (null: nothing to take apart there).</summary>
        public SalvagePlan SalvagePlanFor(Component c, Vector3 point)
        {
            if (c && (c.GetComponentInParent<Placeable>() || c.GetComponentInParent<VehicleDriver>() || c.GetComponentInParent<VehiclePart>())) return null;
            if (c is MadMax.Npc.Npc || c is MadMax.Animals.Animal || c is WorldItem) return null;
            SalvagePlan p = null;
            var wl = c ? c.GetComponentInParent<WorldLamp>() : null;
            var loot = c ? c.GetComponentInParent<Lootable>() : null;
            var dv = c ? c.GetComponentInParent<DestructibleVoxels>() : PropAt(point);
            if (wl && wl.gameObject.name != "StreetLight") p = LampPlan(wl);                       // a city streetlight's head goes with its post
            else if (loot && loot.gameObject.name == "LootSpot") p = SpotPlan(loot);
            else if (dv) p = PropPlan(dv);
            else if (!c) p = CityPlan(point);
            if (p != null) p.blocked ??= SalvageBlocked(p);
            return p;
        }

        static DestructibleVoxels PropAt(Vector3 point)
        {
            int n = Physics.OverlapSphereNonAlloc(point, 0.15f, salvageCols, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var d = salvageCols[i].GetComponentInParent<DestructibleVoxels>();
                if (d && !salvageCols[i].GetComponentInParent<Placeable>()) return d;
            }
            return null;
        }

        SalvagePlan PropPlan(DestructibleVoxels dv)
        {
            string tid = dv.TemplateId;
            if (string.IsNullOrEmpty(tid) || tid.StartsWith("site:") || dv.Template == null) return null;   // bunkers and tunnels are dug, not taken apart
            var sv = SalvageDesigns.Of(dv.Template, dv.voxelSize, SalvageDesigns.IsPlant(tid));       // (the design is meshed when it is first built, not here)
            var p = new SalvagePlan { kind = SalvagePlan.Kind.Prop, target = dv, pieceId = SalvageDesigns.PropPrefix + tid, name = SalvageDesigns.TemplateName(tid), survey = sv };
            p.kit = !dv.Owned && sv.Kit ? SalvageDesigns.PropKit + tid : null;                         // a broken one is only good for materials
            p.yields = dv.Owned ? SalvageDesigns.Of(dv.Grid, dv.voxelSize).cost : sv.cost;
            if (tid.StartsWith("Stall_"))
                foreach (var n in MadMax.Npc.Npc.All)
                    if (n && n.Alive && !n.proxy && (n.transform.position - dv.transform.position).sqrMagnitude < 8f * 8f) { p.blocked = "SOMEONE STILL KEEPS THIS STALL"; break; }
            return p;
        }

        SalvagePlan SpotPlan(Lootable loot)
        {
            var def = DefOfMesh(loot.GetComponent<MeshFilter>(), null);
            if (def == null) return null;
            var p = new SalvagePlan { kind = SalvagePlan.Kind.Spot, target = loot, pieceId = def.id, name = def.name, survey = SalvageDesigns.Of(def.grid, def.voxel), kit = SalvageDesigns.KitFor(def) };
            p.yields = def.cost;
            if (!Lootable.Searched.Contains(loot.key) || loot.HasLeftovers) p.blocked = "SEARCH IT AND EMPTY IT FIRST";
            return p;
        }

        SalvagePlan LampPlan(WorldLamp wl)
        {
            var def = DefOfMesh(wl.GetComponent<MeshFilter>(), FurnitureLibrary.StreetLamps);
            if (def == null) return null;
            return new SalvagePlan { kind = SalvagePlan.Kind.Lamp, target = wl, pieceId = def.id, name = def.name, survey = SalvageDesigns.Of(def.grid, def.voxel), kit = SalvageDesigns.KitFor(def), yields = def.cost };
        }

        SalvagePlan CityPlan(Vector3 point)
        {
            CitySalvage best = null; int mark = -1; float bd = float.MaxValue;
            foreach (var cs in CitySalvage.All)
            {
                if (!cs || !cs.Ready) continue;
                int i = cs.Nearest(point, 1.2f, out float d);
                if (i >= 0 && d < bd) { bd = d; best = cs; mark = i; }
            }
            if (!best) return null;
            var def = FurnitureLibrary.Get(best.marks[mark].kind);
            if (def == null) return null;
            var b = best.LocalBounds(mark);
            var sv = new SalvageDesigns.Survey { size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)), cost = def.cost };
            int units = 0; foreach (var (_, n) in def.cost) units += n;
            sv.kg = units * 6f;
            sv.cls = def.cost.Length > 0 ? SalvageDesigns.ClassOf(def.cost[0].type) : SalvageDesigns.MatClass.Metal;
            return new SalvagePlan
            {
                kind = SalvagePlan.Kind.City, target = best, mark = mark, pieceId = def.id, name = def.name, survey = sv, yields = def.cost,
                kit = SalvageDesigns.Portable.Contains(def.id) ? SalvageDesigns.KitFor(def) : null,
            };
        }

        static FurnitureDef DefOfMesh(MeshFilter mf, string[] among)
        {
            if (!mf || !mf.sharedMesh) return null;
            if (among != null) { foreach (var id in among) { var d = FurnitureLibrary.Get(id); if (d != null && d.mesh == mf.sharedMesh) return d; } return null; }
            foreach (var d in FurnitureLibrary.All) if (d.mesh == mf.sharedMesh) return d;
            return null;
        }

        /// <summary>Why it can't be taken apart now (null: it can): hands, tool, skill.</summary>
        string SalvageBlocked(SalvagePlan p)
        {
            if (!Player) return "NOT NOW";
            if (Player.Carried) return "YOUR HANDS ARE FULL";
            var tools = SalvageDesigns.Tools(p.survey.cls, p.survey.plant);
            p.tool = null;
            if (Player.Tool && System.Array.IndexOf(tools, Player.Tool.id) >= 0) p.tool = Player.Tool.id;
            else foreach (var t in tools) if (Inventory.GetItem(t) > 0) { p.tool = t; break; }
            if (p.tool == null)
            {
                var names = new List<string>();
                foreach (var t in tools) names.Add(ItemCatalog.Name(t));
                return "NEEDS A " + string.Join(" OR ", names);
            }
            int need = SalvageDesigns.SkillNeeded(p.survey.size);
            if (Mathf.Max(Stats.Level(Skill.Salvaging), Stats.Level(Skill.Construction)) < need) return "TOO BIG A JOB FOR YOUR SKILL YET";
            return null;
        }

        // ------------------------------------------------------------------ context menu

        void SalvageOptions(ContextTarget t, List<ContextOption> into)
        {
            if (t == null || t.item != null || Current || !Player) return;
            if (!t.ground && !t.target) return;
            var p = SalvagePlanFor(t.ground ? null : t.target, t.point);
            if (p == null) return;
            string what = "TAKE APART: " + p.name + (p.kit != null ? "" : " (FOR MATERIALS)");
            into.Add(Opt(what, () => StartTakeApart(p), true, p.blocked));
        }

        // ------------------------------------------------------------------ the job

        /// <summary>Start taking <paramref name="p"/> apart (timed work with the right tool in hand). False: it can't.</summary>
        public bool StartTakeApart(SalvagePlan p)
        {
            if (p == null || !p.target) return false;
            p.blocked = SalvageBlocked(p) ?? (p.kind == SalvagePlan.Kind.Spot && p.target is Lootable l && (!Lootable.Searched.Contains(l.key) || l.HasLeftovers) ? "SEARCH IT AND EMPTY IT FIRST" : null);
            if (p.blocked != null) { Toast(p.blocked); return false; }
            if (takeApart != null || Working || Current) return false;
            if (!Player.Tool || Player.Tool.id != p.tool) Player.Equip(ToolLibrary.Create(p.tool, propMaterial));
            float pace = 1f / (1f + 0.08f * Mathf.Max(Stats.Level(Skill.Salvaging), Stats.Level(Skill.Construction)));
            var j = new TakeApartJob { plan = p, seconds = SalvageDesigns.Seconds(p.survey.size, p.survey.kg) * pace, point = WorkPointOf(p) };
            float h = j.point.y - Player.transform.position.y;
            j.pose = p.survey.cls == SalvageDesigns.MatClass.Metal ? (h < 0.5f ? WorkPose.GrindLow : h > 1.7f ? WorkPose.GrindHigh : WorkPose.Grind)
                   : h < 0.5f ? WorkPose.Kneel : h > 1.7f ? WorkPose.Reach : WorkPose.LeanIn;
            if (!GameSettings.Current.workAnimation) { FinishTakeApart(j); return true; }
            takeApart = j;
            Hints.Show("take_apart", "TAKING THINGS APART TEACHES YOU HOW THEY GO TOGETHER: LOOK IN THE BUILD MENU (SALVAGED)");
            return true;
        }

        /// <summary>Automation: take it apart at once (still needs the tool and the skill).</summary>
        public bool TakeApartNow(SalvagePlan p)
        {
            if (p == null || !p.target) return false;
            p.blocked = SalvageBlocked(p) ?? p.blocked;
            if (p.blocked != null) return false;
            FinishTakeApart(new TakeApartJob { plan = p, point = WorkPointOf(p) });
            return true;
        }

        Vector3 WorkPointOf(SalvagePlan p)
        {
            var chest = Player ? Player.transform.position + Vector3.up * 1.1f : Vector3.zero;
            if (p.target is CitySalvage cs)
            {
                var lb = cs.LocalBounds(p.mark);
                var wb = new Bounds(cs.transform.TransformPoint(lb.center), Vector3.zero);
                for (int i = 0; i < 8; i++) wb.Encapsulate(cs.transform.TransformPoint(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                return wb.ClosestPoint(chest);
            }
            var col = p.target.GetComponent<Collider>();
            return col && col.enabled ? col.bounds.ClosestPoint(chest) : p.target.transform.position;
        }

        partial void SalvageUpdate()
        {
            if (!ContextActions.Has("world.salvage")) ContextActions.Register("world.salvage", (g, t, into) => g.SalvageOptions(t, into));
            SalvageDesigns.Pack = Inventory;
            var j = takeApart;
            if (j == null) return;
            float dt = Time.deltaTime;
            j.age += dt;
            string why = null;
            if (!j.plan.target || (j.plan.target is CitySalvage cs && cs.Gone(j.plan.mark))) why = "~GONE";
            else if (!Player || Current || Player.Ragdolled || Player.Sitting || Player.SeatedIn || (Vitals && Vitals.Dead) || Working) why = "~";
            else if (j.age > 0.3f && Player.moveInput.sqrMagnitude > 0.1f) why = "STOPPED";
            else if (Vector3.Distance(Player.transform.position + Vector3.up, j.point) > 3.2f) why = "TOO FAR AWAY";
            if (why != null) { EndTakeApart(j); if (why[0] != '~') Toast(why); return; }

            var look = j.point - Player.transform.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) Player.transform.rotation = Quaternion.Slerp(Player.transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-12f * dt));
            Player.PoseOverride = PoseAt(j.pose, j.t);
            Player.ActionClip = j.pose == WorkPose.LeanIn ? "wrench" : j.pose == WorkPose.Kneel ? "weld" : null;
            TakeApartFx(j, dt);
            j.t += dt;
            Prompt = "TAKING APART: " + j.plan.name + "  " + Mathf.RoundToInt(TakeApartProgress * 100f) + "%   MOVE TO STOP";
            if (j.t >= j.seconds) FinishTakeApart(j);
        }

        void TakeApartFx(TakeApartJob j, float dt)
        {
            var cls = j.plan.survey.cls;
            if (cls == SalvageDesigns.MatClass.Metal) MadMax.Audio.Sfx.Loop(Player, "grinder", 0.6f, 1.1f, 30f);
            if ((j.fxT -= dt) > 0f) return;
            var away = Player.transform.position - j.point; away.y = 0f;
            var side = away.sqrMagnitude > 1e-4f ? Vector3.Cross(away.normalized, Vector3.up) : Vector3.right;
            if (cls == SalvageDesigns.MatClass.Metal)
            {
                j.fxT = 0.06f;
                Fx.Sparks(j.point, Vector3.up * 0.6f + side, 3, new Color(1f, 0.75f, 0.35f));
                return;
            }
            j.fxT = 0.55f + Random.value * 0.2f;
            MadMax.Audio.Sfx.Play("hammer", j.point, 0.55f, cls == SalvageDesigns.MatClass.Stone ? 0.7f : 1f + Random.value * 0.15f, 25f);
            var dust = cls == SalvageDesigns.MatClass.Stone ? new Color32(150, 140, 125, 255) : j.plan.survey.plant ? new Color32(90, 110, 60, 255) : new Color32(170, 130, 85, 255);
            if (DebrisSystem.Instance)
                for (int i = 0; i < 3; i++) DebrisSystem.Instance.EmitPuff(j.point, dust, 0.04f, Random.insideUnitSphere * 0.8f + Vector3.up * 0.6f, 0.6f);
        }

        void EndTakeApart(TakeApartJob j)
        {
            if (takeApart == j) takeApart = null;
            if (Player) { Player.PoseOverride = null; Player.ActionClip = null; }
        }

        void FinishTakeApart(TakeApartJob j)
        {
            EndTakeApart(j);
            var p = j.plan;
            if (!p.target) return;
            var at = j.point;
            NoteTakenApart(p);                                                                   // gone on every peer (WastelandGame.SalvageNet)
            switch (p.kind)
            {
                case SalvagePlan.Kind.Prop: RemoveProp((DestructibleVoxels)p.target); break;
                case SalvagePlan.Kind.Spot: SalvageDesigns.Removed.Add(((Lootable)p.target).key); Destroy(p.target.gameObject); break;
                case SalvagePlan.Kind.Lamp: SalvageDesigns.Removed.Add(((WorldLamp)p.target).key); Destroy(p.target.gameObject); break;
                case SalvagePlan.Kind.City: ((CitySalvage)p.target).Remove(p.mark); break;
            }
            if (p.kit != null) using (Inventory.Source("TAKEN APART")) Inventory.AddItem(p.kit);
            else DropMaterials(p.yields, at);
            if (DebrisSystem.Instance)
                for (int i = 0; i < 10; i++) DebrisSystem.Instance.EmitPuff(at + Random.insideUnitSphere * 0.4f, new Color32(160, 145, 120, 255), 0.06f, Random.insideUnitSphere + Vector3.up, 0.9f);
            MadMax.Audio.Sfx.Play(p.survey.size > 4f ? "collapse" : "hammer", at, 0.8f, 0.9f, 40f);
            bool learned = SalvageDesigns.Known.Add(p.pieceId);
            Stats.Practice(Skill.Salvaging, 3f + p.survey.size * 2f);
            Stats.Practice(Skill.Construction, 1f + p.survey.size);
            if (p.tool != null) WearTool(p.tool, 0.01f + 0.008f * p.survey.size);
            MadMax.Story.Story.Note("taken_apart:" + p.pieceId);
            LastTakenApart = p.pieceId;
            Toast((p.kit != null ? "TAKEN APART: " + p.name + " (KIT IN THE PACK)" : "TAKEN DOWN: " + p.name) + (learned ? " - YOU SEE HOW IT GOES TOGETHER" : ""));
        }

        /// <summary>A prop gone for good: an empty grid in the destruction state (saved, honoured when it streams back).</summary>
        void RemoveProp(DestructibleVoxels dv)
        {
            var t = DeformableTerrain.Instance;
            string key = dv.StateKey, tid = dv.TemplateId;
            if (t && key != null)
            {
                t.DestructionState[key] = new VoxelGrid();
                if (tid != null) t.DestructionTemplates[key] = tid;
                DestructibleVoxels.Moved.Remove(key);
            }
            if (tid != null && tid.StartsWith("Streetlight"))                                       // its lamp head went with it
                foreach (var wl in FindObjectsByType<WorldLamp>(FindObjectsSortMode.None))
                {
                    var d = wl.transform.position - dv.transform.position; d.y = 0f;
                    if (wl.gameObject.name == "StreetLight" && d.sqrMagnitude < 2.5f * 2.5f) { SalvageDesigns.Removed.Add(wl.key); Destroy(wl.gameObject); }
                }
            Destroy(dv.gameObject);
        }

        /// <summary>Most of what a big thing is made of, left as piles where it stood.</summary>
        void DropMaterials((ResourceType type, int amount)[] yields, Vector3 at)
        {
            if (yields == null) return;
            float k = 0.75f * Stats.SalvageYield * GameRules.Current.yield;
            var pickups = PickupSystem.Instance;
            foreach (var (type, n) in yields)
            {
                int units = Mathf.Max(1, Mathf.RoundToInt(n * k));
                if (!pickups) { using (Inventory.Source("TAKEN APART")) Inventory.Add(type, units); continue; }
                int bundles = Mathf.Min(5, units), per = Mathf.CeilToInt(units / (float)bundles);
                for (int i = 0; i < bundles && units > 0; i++, units -= per)
                    pickups.Spawn(type, Mathf.Min(per, units), at + Random.insideUnitSphere * 0.5f + Vector3.up * 0.3f, Random.insideUnitSphere * 1.5f + Vector3.up * 2f);
            }
        }

        // ------------------------------------------------------------------ kits from the pack

        /// <summary>BUILD IT on a salvage kit: build mode with that piece selected (true: it was one).</summary>
        bool SalvageKitUse(string id)
        {
            var pid = SalvageDesigns.PieceOfKit(id);
            if (pid == null) return false;
            var def = FurnitureLibrary.Get(pid);
            if (def == null || !Build) { Toast("NOTHING TO BUILD FROM THIS"); return true; }
            Build.SetCategory(def.category == BuildCategory.Hidden ? BuildCategory.Salvaged : def.category);
            var all = Build.Pieces;
            for (int i = 0; i < all.Count; i++) if (all[i].id == pid) { Build.Select(i); break; }
            if ((!Player.Tool || Player.Tool.id != ItemIds.ClawHammer) && Inventory.GetItem(ItemIds.ClawHammer) > 0) Player.Equip(ToolLibrary.Create(ItemIds.ClawHammer, propMaterial));
            if (!Build.Active) Build.SetActive(true);
            return true;
        }

        // ------------------------------------------------------------------ save

        partial void SalvageSave(SaveData d)
        {
            d.blockSalvage ??= new List<string>();
            SalvageDesigns.Save(d.blockSalvage);
        }

        partial void SalvageLoad(SaveData d)
        {
            var mine = JoiningWorld(d) ? new List<string>(SalvageDesigns.Known) : null;            // learned designs are the player's own
            SalvageDesigns.Load(d.blockSalvage);
            if (mine != null) { SalvageDesigns.Known.Clear(); SalvageDesigns.Known.UnionWith(mine); }
            SalvageDesigns.Pack = Inventory;
            // things that streamed in before the save was read
            foreach (var l in FindObjectsByType<Lootable>(FindObjectsSortMode.None)) if (l && l.gameObject.name == "LootSpot" && SalvageDesigns.Removed.Contains(l.key)) Destroy(l.gameObject);
            foreach (var w in FindObjectsByType<WorldLamp>(FindObjectsSortMode.None)) if (w && SalvageDesigns.Removed.Contains(w.key)) Destroy(w.gameObject);
            foreach (var cs in CitySalvage.All) if (cs) cs.ApplySaved();
        }

        partial void SalvageNewGame()
        {
            SalvageDesigns.NewGame();
            SalvageDesigns.Pack = Inventory;
            takeApart = null;
        }
    }
}
