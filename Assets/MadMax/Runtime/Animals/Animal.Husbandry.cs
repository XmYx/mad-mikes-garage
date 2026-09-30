using System.Collections.Generic;
using System.Globalization;
using MadMax.Audio;
using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Husbandry and animal care (depth stage E). Sheep (goats a little) grow their wool back over days after
    /// shearing (<see cref="Shear"/>: the shears, or [E] with shears in the pack), faster on mixed feed. Hits open
    /// wounds: a bleeding animal loses health until the wound clots or is bandaged (an untended deep one may fester into
    /// sickness), a hard hit lames a leg (slower, heals over days), and a badly hurt one goes down where it stands.
    /// [E] with a honey salve, bandage, splint or antibiotics treats any animal that lets you close — kept, a village's,
    /// a downed or a trusting wild one (a nursed wild horse or dog comes to trust you). Horses rest in a
    /// <see cref="Stable"/>: stamina back fast, calm, rested for the next ride. Leather saddlebags double a saddled
    /// horse's bags. Kept animals save this with <see cref="AnimalSave.husbandry"/>.</summary>
    public partial class Animal
    {
        public const string Saddlebags = "misc_saddlebags", Salve = "vet_salve", Shears = "tool_shears";
        /// <summary>Medicine that works on animals.</summary>
        public static readonly string[] Medicine = { Salve, "med_bandage", "med_splint", "med_antibiotics" };

        /// <summary>An animal was treated (animal, medicine): story hooks (S12).</summary>
        public static event System.Action<Animal, string> Treated;
        static readonly Dictionary<string, Mesh> fleeceMeshes = new Dictionary<string, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetCare() { Treated = null; fleeceMeshes.Clear(); }

        /// <summary>Fleece grown back: 0 just shorn .. 1 full.</summary>
        public float wool = 1f;
        /// <summary>Open wound 0..1: drains health until it clots or is bandaged.</summary>
        public float bleed;
        /// <summary>Lame leg 0..1: slower; heals over days (faster in a stable), a splint sets it.</summary>
        public float limp;
        /// <summary>Sick 0..1: a festering wound; wears the animal down; antibiotics cure it.</summary>
        public float sick;
        /// <summary>Leather saddlebags fitted (the bags hold 90 kg).</summary>
        public bool bigBags;
        float careDays = -1f, careAcc, restedUntil, woolBoost;
        bool inStable, woundedByPlayer, rested;
        Transform fleece;

        /// <summary>Wool a full fleece gives (0: not a wool animal).</summary>
        public int WoolYield => Def.Has("fleece") ? 3 : Def.id == "goat" ? 1 : 0;
        float RegrowDays => Def.Has("fleece") ? 3f : 4f;
        public bool Hurt => Alive && (bleed > 0.01f || limp > 0.05f || sick > 0.05f || health < Def.health * 0.7f);
        /// <summary>Too hurt to stand: lies where it is until treated (or it bleeds out).</summary>
        public bool Downed => Alive && Def.plan != BodyPlan.Snake && !Def.flies && health < Def.health * 0.3f && (bleed > 0.05f || limp > 0.4f || sick > 0.5f);
        public bool InStable => inStable;
        bool Calm => inStable || Downed;
        float Gait => Mathf.Clamp(1f - limp * 0.5f - sick * 0.25f, 0.35f, 1f);
        float StaminaUse => (DayNight.TotalDays < restedUntil ? 0.65f : 1f) * (1f + limp);
        /// <summary>Lets you close enough to treat it.</summary>
        bool Approachable => owned || town >= 0 || Downed || trust >= 0.5f
            || (Def.nature != Nature.Predator && state != State.Flee && state != State.Attack && state != State.Charge && state != State.Stalk);
        bool Shearable => WoolYield > 0 && town < 0 && (owned || trust >= 0.5f) && wool >= 0.5f;
        string TPrompt => !owned ? "" : Def.rideable && saddled && Adult ? "  [T] SADDLEBAGS" : "  [T] " + OrderLabel;

        string Condition
        {
            get
            {
                int pct = Mathf.RoundToInt(Mathf.Clamp01(health / Def.health) * 100f);
                return bleed > 0.01f ? "BLEEDING, " + pct + "%" : limp > 0.05f ? "LAME, " + pct + "%" : sick > 0.05f ? "SICK, " + pct + "%" : pct + "%";
            }
        }

        // ------------------------------------------------------------------ ticking

        void CareTick(WastelandGame g, float dt)
        {
            if (!fleece && WoolYield > 0 && Def.Has("fleece")) BuildFleece();
            if ((careAcc += dt) < 0.5f) return;
            float step = careAcc; careAcc = 0f;
            inStable = Stable.Holds(transform.position);
            // open wounds bleed until they clot (a bandage stops them at once)
            if (bleed > 0f)
            {
                health -= bleed * Def.health * 0.006f * step;
                float was = bleed;
                bleed = Mathf.Max(0f, bleed - 0.004f * step);
                if (Random.value < was * step * 0.6f) BloodStains.Drip(transform.position);
                if (was > 0.25f && bleed <= 0f && Random.value < 0.35f) sick = Mathf.Max(sick, 0.4f);        // an untended deep wound festers
                if (health <= 0f) { health = 0f; Die(g, woundedByPlayer, -transform.right); return; }
            }
            // rest: stamina comes back standing, fast in a stable (then rested for the next ride)
            if (state != State.Ridden && stamina < 100f) stamina = Mathf.Min(100f, stamina + (inStable ? 14f : 1.5f) * step);
            if (inStable && stamina >= 99f)
            {
                restedUntil = DayNight.TotalDays + 0.5f;
                if (!rested && owned && Def.rideable && (transform.position - g.Player.transform.position).sqrMagnitude < 20f * 20f) g.Toast("THE " + Label + " IS RESTED (LESS WINDED FOR HALF A DAY)");
                rested = true;
            }
            else if (!inStable) rested = false;
            // day clocks: wool regrows, a limp heals, sickness wears the animal down, wild ones mend
            float now = DayNight.TotalDays;
            if (careDays < 0f) careDays = now;
            float days = Mathf.Clamp(now - careDays, 0f, 5f);
            if (days < 0.002f) return;
            careDays = now;
            if (WoolYield > 0 && wool < 1f) { wool = Mathf.Min(1f, wool + days / RegrowDays * (1f + woolBoost)); UpdateFleece(); }
            woolBoost = Mathf.Max(0f, woolBoost - days);
            if (limp > 0f) limp = Mathf.Max(0f, limp - days * (inStable ? 0.6f : 0.3f));
            if (sick > 0f) { health = Mathf.Max(Mathf.Min(health, Def.health * 0.35f), health - Def.health * 0.12f * days); sick = Mathf.Max(0f, sick - days * 0.08f); }
            else if (!owned && bleed <= 0f) health = Mathf.Min(Def.health, health + Def.health * 0.2f * days);
        }

        /// <summary>A hit that did not kill: a deep enough one bleeds, a heavy one lames.</summary>
        void Wounded(float dmg, GameObject source)
        {
            if (health <= 0f || Def.plan == BodyPlan.Snake || Def.flies) return;
            var g = WastelandGame.Instance;
            woundedByPlayer = g && g.Player && source && (source.transform.IsChildOf(g.Player.transform) || (g.Current && source.transform.IsChildOf(g.Current.transform)));
            float frac = dmg / Mathf.Max(1f, Def.health);
            if (frac > 0.06f) bleed = Mathf.Clamp01(bleed + frac * 1.2f);
            if (health < Def.health * 0.55f && (frac > 0.15f || Random.value < 0.5f)) limp = Mathf.Max(limp, Mathf.Clamp01(1.1f - health / Def.health));
        }

        /// <summary>A downed animal lies still (no fleeing, no fighting).</summary>
        bool CareThink()
        {
            if (!Downed) return false;
            if (state != State.Sleep) Say(1);
            state = State.Sleep; stateUntil = Time.time + 5f;
            SetMove(Vector3.zero, 0f);
            return true;
        }

        /// <summary>Ate a ration of mixed feed from the trough: heals more, gives a little extra, wool grows faster.</summary>
        public void MixedRation()
        {
            health = Mathf.Min(Def.health, health + Def.health * 0.1f);
            if (Adult && Def.product != null) stock = Mathf.Min(Def.productPerDay * 3, stock + 1);
            woolBoost = 0.5f;
            sick = Mathf.Max(0f, sick - 0.2f);
        }

        // ------------------------------------------------------------------ interaction ([E] before the animal's own)

        bool CareListed(WastelandGame g) => g && Alive
            && ((Hurt && Approachable && MedFor(g) != null)
             || (Shearable && g.Inventory.GetItem(Shears) > 0)
             || (owned && saddled && !bigBags && g.Inventory.GetItem(Saddlebags) > 0));

        /// <summary>The medicine for the worst of its troubles that the pack holds, or null.</summary>
        string MedFor(WastelandGame g)
        {
            var inv = g.Inventory;
            if (bleed > 0.01f) { if (inv.GetItem(Salve) > 0) return Salve; if (inv.GetItem("med_bandage") > 0) return "med_bandage"; }
            if (limp > 0.05f && inv.GetItem("med_splint") > 0) return "med_splint";
            if (sick > 0.05f && inv.GetItem("med_antibiotics") > 0) return "med_antibiotics";
            if (health < Def.health * 0.7f) { if (inv.GetItem(Salve) > 0) return Salve; if (inv.GetItem("med_bandage") > 0) return "med_bandage"; }
            return null;
        }

        string CarePrompt(WastelandGame g)
        {
            if (owned && saddled && !bigBags && g.Inventory.GetItem(Saddlebags) > 0) return "[E] FIT THE LEATHER SADDLEBAGS" + TPrompt;
            if (Hurt && Approachable)
            {
                var med = MedFor(g);
                if (med != null) return "[E] TREAT THE " + Label + " WITH " + ItemCatalog.Name(med) + " (" + Condition + ")" + TPrompt;
            }
            if (Shearable && g.Inventory.GetItem(Shears) > 0) return "[E] SHEAR THE " + Label + " (WOOL " + Mathf.RoundToInt(wool * 100f) + "%)" + TPrompt;
            return null;
        }

        bool CareUse(WastelandGame g)
        {
            if (owned && saddled && !bigBags && g.Inventory.TakeItem(Saddlebags))
            {
                FitBags();
                Sfx.Play("chest", transform.position, 0.5f, 0.9f);
                g.Toast("LEATHER SADDLEBAGS FITTED: THE BAGS HOLD 90 KG ([T])");
                return true;
            }
            if (Hurt && Approachable)
            {
                var med = MedFor(g);
                if (med != null) { TreatWith(g, med); return true; }
            }
            if (Shearable && g.Inventory.GetItem(Shears) > 0) { Shear(g); return true; }
            return false;
        }

        /// <summary>Use one medicine on the animal (it must be in the pack).</summary>
        public bool TreatWith(WastelandGame g, string med)
        {
            if (!Alive || !g.Inventory.TakeItem(med)) return false;
            float skill = 1f + g.Stats.Level(Skill.Survival) * 0.05f + g.Stats.Level(Skill.Farming) * 0.03f;
            string did;
            switch (med)
            {
                case "med_splint": limp = 0f; health += Def.health * 0.1f * skill; did = "SPLINTED"; break;
                case "med_antibiotics": sick = 0f; health += Def.health * 0.2f * skill; did = "DOSED"; break;
                case Salve: bleed = 0f; sick = Mathf.Max(0f, sick - 0.2f); health += Def.health * 0.4f * skill; did = "SALVED"; break;
                default: bleed = 0f; health += Def.health * 0.2f * skill; did = "BANDAGED"; break;
            }
            health = Mathf.Min(Def.health, health);
            if (town >= 0) MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.Settlers, 1);
            g.Stats.Practice(Skill.Survival, 3f);
            g.Stats.Practice(Skill.Farming, 2f);
            Sfx.Play("chest", transform.position, 0.4f, 1.4f);
            Say(0);
            g.Toast(did + ": THE " + Label + " (" + Condition + ")");
            g.HusbandryTally("treated");
            MadMax.Story.Story.Note("treat:" + Def.id);
            Treated?.Invoke(this, med);
            if (!owned && Def.tameable && town < 0)
            {
                trust = Mathf.Min(1f, trust + 0.35f);                                              // it remembers the hand that helped
                if (trust >= 1f) Tame(g);
            }
            return true;
        }

        /// <summary>Shear a sheep or goat: wool into the pack, the fleece grows back over days.</summary>
        public bool Shear(WastelandGame g)
        {
            if (!Alive || WoolYield == 0) return false;
            if (town >= 0) { g.Toast("A VILLAGER'S " + Def.name + " - NOT YOURS TO SHEAR"); return false; }
            if (!owned && trust < 0.5f) { g.Toast("IT WON'T STAND STILL FOR THE SHEARS"); return false; }
            if (wool < 0.5f) { g.Toast("THE FLEECE IS STILL SHORT (" + Mathf.RoundToInt(wool * 100f) + "%)"); return false; }
            int n = Mathf.Max(1, Mathf.RoundToInt(WoolYield * wool * Size * (0.85f + g.Stats.Level(Skill.Farming) * 0.05f) * GameRules.Current.yield));
            g.Inventory.Add(ResourceType.Wool, n);
            wool = 0f;
            UpdateFleece();
            if (g.Inventory.GetItem(Shears) > 0) g.WearTool(Shears, 0.01f);
            g.Stats.Practice(Skill.Farming, 2f);
            Sfx.Play("scratch", transform.position, 0.5f, 1.5f);
            Fx.Smoke(transform.position + Vector3.up * Def.Height * Size, Vector3.up * 0.4f, 0.3f, Pal.Cream[3], 0.8f);
            Say(0);
            g.Toast("SHORN: +" + n + " WOOL (IT GROWS BACK IN " + Mathf.RoundToInt(RegrowDays) + " DAYS)");
            g.HusbandryTally("wool", n);
            MadMax.Story.Story.Note("shear:" + Def.id);
            return true;
        }

        void FitBags() { bigBags = true; if (bags) bags.capacity = 90f; }

        // ------------------------------------------------------------------ fleece

        /// <summary>A woolly shell over the torso (4 cm lumps), hidden when shorn and filling out as it grows.</summary>
        void BuildFleece()
        {
            if (!rig) return;
            if (!fleeceMeshes.TryGetValue(Def.id, out var mesh) || !mesh)
            {
                int L = Def.len, D = Def.depth, W = Def.width, seed = 3150 + L;
                var c = new Vector3(0f, 0.4f, -0.3f);                                                // relative to the torso centre
                var r = new Vector3(W * 0.5f + 1.3f, D * 0.5f + 1.2f, L * 0.5f + 0.7f);
                var g = new VoxelGrid();
                for (int x = -Mathf.CeilToInt(r.x); x <= Mathf.CeilToInt(r.x); x++)
                for (int y = -Mathf.CeilToInt(r.y); y <= Mathf.CeilToInt(r.y + 1f); y++)
                for (int z = -Mathf.CeilToInt(r.z + 1f); z <= Mathf.CeilToInt(r.z); z++)
                {
                    float v = Mathf.Pow(Mathf.Abs(x / r.x), 2.2f) + Mathf.Pow(Mathf.Abs((y - c.y) / r.y), 2.2f) + Mathf.Pow(Mathf.Abs((z - c.z) / r.z), 2.2f);
                    if (v > 1f || v < 0.4f || y < -D * 0.35f) continue;                            // a shell; the belly stays short
                    if (v > 0.8f && Pal.Hash(x, y, z, seed) > 0.78f) continue;                     // lumpy
                    g.Set(x, y, z, p => Pal.Pick(Pal.Cream, p, seed, Pal.Hash(p.x / 2, p.y / 2, p.z / 2, seed + 1) > 0.5f ? 3 : 2));
                }
                g.Bevel();
                mesh = fleeceMeshes[Def.id] = VoxelMesher.Build(g, "AnimalFleece_" + Def.id, VoxelMesher.DefaultSize * Def.scale);
            }
            var mr = rig.GetComponentInChildren<MeshRenderer>();
            fleece = Part(rig, "Fleece", mesh, new Vector3(0f, (Def.leg + Def.depth * 0.5f) * VoxelMesher.DefaultSize * Def.scale, 0f), mr ? mr.sharedMaterial : null);
            UpdateFleece();
        }

        void UpdateFleece()
        {
            if (!fleece) return;
            fleece.gameObject.SetActive(wool > 0.2f);
            fleece.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, wool);
        }

        // ------------------------------------------------------------------ save

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        public string HusbandryState() => F(wool) + ";" + F(bleed) + ";" + F(limp) + ";" + F(sick) + ";" + (bigBags ? "1" : "0");

        public void LoadHusbandry(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            float P(int i, float def) => i < p.Length && float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
            wool = P(0, 1f); bleed = P(1, 0f); limp = P(2, 0f); sick = P(3, 0f);
            if (p.Length > 4 && p[4] == "1") FitBags();
            UpdateFleece();
        }
    }
}
