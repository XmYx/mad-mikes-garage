using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The player's limbs (<see cref="Limbs"/>): heavy hits mangle or sever them (<see cref="Injure"/> hands
    /// each fresh limb wound to <see cref="LimbsAfterInjury"/>), a blade takes one off on purpose (health page:
    /// <see cref="Amputate"/>), and a healed stump takes a prosthetic (<see cref="FitProsthetic"/>). What is gone shows on
    /// the body (<see cref="Appearance.lost"/>, saved and sent with the look) and in the gait, the hands and the speed:
    /// <see cref="ArmLoss"/>, <see cref="LegLoss"/>, <see cref="LimbSpeed"/>, <see cref="CanHoldTools"/>,
    /// <see cref="CanClimb"/>, <see cref="HasClutchFoot"/>; a mount arm puts its tool on the hotbar
    /// (<see cref="BuiltInTool"/>).</summary>
    public partial class WastelandGame
    {
        /// <summary>Set by an attacker just before it hurts the player: the blow came from a blade.</summary>
        [System.NonSerialized] public bool HitBladed;

        Appearance Look => Player ? Player.Rig.appearance : null;

        void LimbsAfterInjury(float amount, string cause, int before)
        {
            bool bladed = HitBladed;
            HitBladed = false;
            if (Stats.injuries.Count <= before || !Player) return;
            var inj = Stats.injuries[Stats.injuries.Count - 1];
            var z = inj.zone;
            if (!Limbs.IsLimb(z) || Limbs.Gone(Look.lost, z)) return;
            bool mangled = Stats.injuries.Exists(i => i != inj && i.zone == z && i.type == Wound.Mangled);
            int kind = KindOf(cause);
            int r = Limbs.Roll(z, amount, cause, bladed, mangled, injuryRnd, kind < 0 ? 0f : Protection(z, kind));
            if (r == 1)
            {
                inj.type = Wound.Mangled; inj.severity = 1f; inj.bandaged = inj.splinted = false;
                Toast("YOUR " + Limbs.Name(z) + " IS MANGLED: DRESS AND SPLINT IT, OR LOSE IT  [O]");
                Journal.Add("health", "MY " + Limbs.Name(z) + " IS HANGING ON BY A THREAD.");
            }
            else if (r == 2) Sever(z, false);
        }

        /// <summary>A limb comes off (a blow, a blast, a blade on purpose): the bit drops, the stump bleeds hard until
        /// dressed (<paramref name="clean"/>: cut at a table, already dressed and disinfected).</summary>
        public void Sever(BodyZone z, bool clean)
        {
            var a = Look;
            if (a == null || !Limbs.IsLimb(z) || Limbs.Gone(a.lost, z)) return;
            var rig = Player.Rig;
            var cut = rig.Bone(Limbs.CutBone(z));
            var at = cut.position; var rot = cut.rotation;
            var mesh = HumanDesign.BodyMesh(Limbs.CutBone(z), a);
            // what was fitted below or on it comes off with it
            ReturnProsthetic(z, at);
            if (Limbs.Child(z) is BodyZone c) { ReturnProsthetic(c, at); a.lost &= ~Limbs.Bit(c); }
            a.lost |= Limbs.Bit(z);
            Stats.injuries.RemoveAll(i => i.zone == z || (Limbs.Child(z) is BodyZone cz && i.zone == cz));
            Stats.injuries.Add(new Injury { zone = z, type = Wound.Stump, severity = 1f, bandaged = clean, disinfected = clean });
            Stats.health = Mathf.Max(1f, Stats.health - (clean ? 5f : 15f));
            if (!MadMax.Net.NetSession.Applying)
            {
                var item = WorldItem.Create(Limbs.Item(z), 1, -1f, propMaterial, at, rot, null, mesh);
                if (item.Body) item.Body.linearVelocity = (rot * Vector3.down + Random.insideUnitSphere * 0.5f) * 1.5f;
                if (Player.TryGetComponent<Collider>(out var pc)) Physics.IgnoreCollision(item.Box, pc);
                MadMax.Net.NetSession.Instance?.SendItemSpawn(item, Vector3.zero);
            }
            MadMax.World.BloodStains.Splash(at, clean ? 0.4f : 1f);
            rig.Bleed(at, 1.6f);
            MadMax.Audio.Sfx.Play("punch", at, 1f, 0.6f, 25f);
            if (cameraRig) cameraRig.Shake(8f);
            Player.RebuildBody();
            if (Player.Tool && !CanUse(Player.Tool.id)) Player.Equip(null);
            UpdateHotbarNow();
            MadMax.Net.NetSession.Instance?.SendAppearance();
            Toast((clean ? "AMPUTATED: " : "YOU LOST YOUR ") + Limbs.Name(z) + (clean ? "" : "! STOP THE BLEEDING  [O]"));
            Journal.Add("health", "I LOST MY " + Limbs.Name(z) + ". A PROSTHETIC CAN GO ON ONCE THE STUMP HEALS.");
        }

        /// <summary>Health page on a limb: cut it off on purpose — a mangled one that won't heal, or a sound one for a
        /// mount arm. Needs a blade in the pack; at a surgery table within 3 m (with its supplies) it is clean.
        /// Press twice: the first press only asks.</summary>
        public bool Amputate(BodyZone z)
        {
            if (!Limbs.IsLimb(z) || Limbs.Gone(Look.lost, z)) return false;
            string blade = null;
            foreach (var id in new[] { ItemIds.Cutter, "tool_axe", ItemIds.Machete, "tool_knife", "tool_leaf_blade" }) if (Inventory.GetItem(id) > 0 || (Player.Tool && Player.Tool.id == id)) { blade = id; break; }
            if (blade == null) { Toast("NEED A BLADE (KNIFE, MACHETE, AXE OR CUTTER)"); return false; }
            if (amputateAsk != z || Time.time > amputateUntil) { amputateAsk = z; amputateUntil = Time.time + 4f; Toast("CUT OFF YOUR " + Limbs.Name(z) + "? PRESS AGAIN TO DO IT"); return false; }
            amputateAsk = null;
            SurgeryTable table = null;
            foreach (var t in Object.FindObjectsByType<SurgeryTable>(FindObjectsSortMode.None)) if ((t.transform.position - Player.transform.position).sqrMagnitude < 9f) table = t;
            bool clean = table && table.Missing(this) == null;
            if (clean)
            {
                MedSupply.Take(this, table.transform.position, "med_antibiotics");
                if (!Stats.painkilled) MedSupply.Take(this, table.transform.position, "med_painkillers");
            }
            else Vitals.Hurt(8f, "BLADE");
            Stats.Practice(Skill.Survival, 6f);
            Sever(z, clean);
            return true;
        }
        BodyZone? amputateAsk;
        float amputateUntil;

        // ------------------------------------------------------------------ prosthetics
        /// <summary>The stump on a zone has healed enough to take a prosthetic.</summary>
        public bool StumpReady(BodyZone z)
        {
            if (!Limbs.Severed(Look.lost, z)) return false;
            foreach (var i in Stats.injuries) if (i.zone == z && i.type == Wound.Stump && i.severity > 0.35f) return false;
            return true;
        }

        /// <summary>Strap a prosthetic from the pack onto a healed stump (a clinic bed or surgery table within 3 m makes
        /// it painless; alone it hurts a little).</summary>
        public bool FitProsthetic(BodyZone z, string id)
        {
            var d = ProstheticLibrary.Get(id);
            if (d == null || !d.Fits(z) || !Limbs.Severed(Look.lost, z)) { Toast("IT DOESN'T FIT THERE"); return false; }
            if (!StumpReady(z)) { Toast("THE STUMP HAS TO HEAL FIRST"); return false; }
            if (!Inventory.TakeItem(id)) return false;
            ReturnProsthetic(z, Player.transform.position);
            ProstheticLibrary.SetFitted(Look, z, id);
            bool helped = false;
            foreach (var t in Object.FindObjectsByType<SurgeryTable>(FindObjectsSortMode.None)) if ((t.transform.position - Player.transform.position).sqrMagnitude < 9f) helped = true;
            foreach (var b in ClinicBed.All) if (b && (b.transform.position - Player.transform.position).sqrMagnitude < 9f) helped = true;
            if (!helped) Stats.health = Mathf.Max(1f, Stats.health - 3f);                     // strapped on alone: it chafes
            Player.RebuildBody();
            UpdateHotbarNow();
            MadMax.Net.NetSession.Instance?.SendAppearance();
            MadMax.Audio.Sfx.Play("ratchet", Player.transform.position, 0.6f, 1.3f);
            Toast("FITTED: " + d.name + " ON THE " + Limbs.Name(z));
            return true;
        }

        /// <summary>Take the prosthetic off a zone into the pack (or onto the ground at <paramref name="drop"/> when the
        /// limb it sat on is lost).</summary>
        void ReturnProsthetic(BodyZone z, Vector3 drop)
        {
            var id = ProstheticLibrary.FittedOn(Look, z);
            if (id == null) return;
            ProstheticLibrary.SetFitted(Look, z, null);
            if (Limbs.Severed(Look.lost, z)) Inventory.AddItem(id);
            else SpawnWorldItem(id, 1, -1f, drop, Quaternion.identity, null);
        }

        public bool RemoveProsthetic(BodyZone z)
        {
            if (ProstheticLibrary.FittedOn(Look, z) == null) return false;
            ReturnProsthetic(z, Player.transform.position);
            Player.RebuildBody();
            if (Player.Tool && !CanUse(Player.Tool.id)) Player.Equip(null);
            UpdateHotbarNow();
            MadMax.Net.NetSession.Instance?.SendAppearance();
            return true;
        }

        /// <summary>Health page: what ENTER does on a limb row (fit the first matching prosthetic from the pack, take one
        /// off, or amputate).</summary>
        public void LimbAction(BodyZone z)
        {
            var a = Look;
            if (Limbs.Gone(a.lost, z) && !Limbs.Severed(a.lost, z)) { Toast("GONE WITH THE " + Limbs.Name(Limbs.Parent(z).Value)); return; }
            if (Limbs.Severed(a.lost, z))
            {
                if (ProstheticLibrary.FittedOn(a, z) != null) { RemoveProsthetic(z); return; }
                foreach (var d in ProstheticLibrary.All) if (d.Fits(z) && Inventory.GetItem(d.id) > 0) { FitProsthetic(z, d.id); return; }
                Toast("NO PROSTHETIC FOR A " + Limbs.Name(z) + " IN THE PACK");
                return;
            }
            Amputate(z);
        }

        /// <summary>Health page line for a limb.</summary>
        public string LimbStatus(BodyZone z)
        {
            var a = Look;
            if (Limbs.Gone(a.lost, z) && !Limbs.Severed(a.lost, z)) return "GONE";
            if (Limbs.Severed(a.lost, z))
            {
                var d = ProstheticLibrary.Get(ProstheticLibrary.FittedOn(a, z));
                return d != null ? d.name : StumpReady(z) ? "STUMP (HEALED)" : "STUMP";
            }
            foreach (var i in Stats.injuries) if (i.zone == z && i.type == Wound.Mangled) return "MANGLED";
            return "SOUND";
        }

        ProstheticDef FittedFor(BodyZone limb, BodyZone end)
        {
            var a = Look;
            if (a == null) return null;
            return ProstheticLibrary.Get(ProstheticLibrary.FittedOn(a, Limbs.Severed(a.lost, limb) ? limb : end));
        }

        /// <summary>0 a sound hand .. 1 none: a lost hand / forearm less the prosthetic's grip.</summary>
        public float ArmLoss(bool left)
        {
            var a = Look;
            var hand = left ? BodyZone.HandL : BodyZone.HandR;
            if (a == null || !Limbs.Gone(a.lost, hand)) return 0f;
            var d = FittedFor(left ? BodyZone.ArmL : BodyZone.ArmR, hand);
            return d != null ? 1f - d.grip : 1f;
        }

        /// <summary>0 a sound leg .. 1 none: a lost foot / lower leg less what the prosthetic gives back.</summary>
        public float LegLoss(bool left)
        {
            var a = Look;
            var foot = left ? BodyZone.FootL : BodyZone.FootR;
            if (a == null || !Limbs.Gone(a.lost, foot)) return 0f;
            var d = FittedFor(left ? BodyZone.LegL : BodyZone.LegR, foot);
            return d != null ? d.limp : 1f;
        }

        /// <summary>Walking / running speed from lost legs: hopping and crawling without, the prosthetic's pace with one.</summary>
        public float LimbSpeed
        {
            get
            {
                var a = Look;
                if (a == null || a.lost == 0) return 1f;
                float m = 1f;
                foreach (bool left in new[] { true, false })
                {
                    var foot = left ? BodyZone.FootL : BodyZone.FootR;
                    if (!Limbs.Gone(a.lost, foot)) continue;
                    var d = FittedFor(left ? BodyZone.LegL : BodyZone.LegR, foot);
                    m *= d != null ? d.speed : 0.3f;
                }
                return Mathf.Max(0.12f, m);
            }
        }

        /// <summary>Stamina drain from fitted pieces (heavy arms, springy legs).</summary>
        public float LimbStamina { get { float m = 1f; foreach (var (_, d) in ProstheticLibrary.Fitted(Look)) m *= d.stamina; return m; } }

        /// <summary>Extra kg carried thanks to fitted pieces (the hydraulic arm).</summary>
        public float LimbCarry { get { float kg = 0f; foreach (var (_, d) in ProstheticLibrary.Fitted(Look)) kg += d.carry; return kg; } }

        /// <summary>A hand (or a gripping prosthetic) to hold a tool with.</summary>
        public bool CanHoldTools => Mathf.Min(ArmHurtL, ArmHurtR) < 0.6f;

        /// <summary>Both arms can take the body's weight on a ledge (sound, or a prosthetic that climbs).</summary>
        public bool CanClimb
        {
            get
            {
                var a = Look;
                if (a == null || a.lost == 0) return true;
                foreach (bool left in new[] { true, false })
                {
                    var hand = left ? BodyZone.HandL : BodyZone.HandR;
                    if (!Limbs.Gone(a.lost, hand)) continue;
                    var d = FittedFor(left ? BodyZone.ArmL : BodyZone.ArmR, hand);
                    if (d == null || !d.climbs) return false;
                }
                return true;
            }
        }

        /// <summary>A left foot (or a leg prosthetic) for the clutch: without one the gearbox stays automatic.</summary>
        public bool HasClutchFoot => LegLoss(true) < 0.99f;

        /// <summary>1 when a fitted mount arm carries this tool (it is always at hand on the hotbar).</summary>
        public int BuiltInTool(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            foreach (var (_, d) in ProstheticLibrary.Fitted(Look)) if (d.tool == id) return 1;
            return 0;
        }

        /// <summary>The player can wield this tool: a mount arm's own tool, or one held in a hand / gripping prosthetic.</summary>
        public bool CanUse(string toolId) => BuiltInTool(toolId) > 0 || CanHoldTools;
    }
}
