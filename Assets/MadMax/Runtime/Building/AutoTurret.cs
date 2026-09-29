using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Powered sentry gun: tracks hostile people within range and line of sight and fires MG rounds fed from
    /// its own ammo box (belts of <see cref="BeltRounds"/>). Never fires at the player.</summary>
    public class AutoTurret : MonoBehaviour, IPlaceState, IInteractable
    {
        public const int BeltRounds = 20;
        public Transform head;
        public float range = 30f, watts = 150f;
        public bool on = true;
        public int rounds;

        UtilityNode node;
        Container box;
        MadMax.Npc.Npc target;
        float scan, fireCd;
        Quaternion rest;
        static readonly RaycastHit[] hits = new RaycastHit[8];

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            int belts = box ? box.inventory.GetItem("ammo_mg") : 0;
            string state = !on ? "OFF" : node && !node.Powered ? "NO POWER" : rounds + belts * BeltRounds == 0 ? "NO AMMO" : target ? "ENGAGING" : "WATCHING";
            return "TURRET " + state + " (" + (rounds + belts * BeltRounds) + " RDS)  [T] " + (on ? "SWITCH OFF" : "SWITCH ON");
        }
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (secondary) { on = !on; GetComponent<Placeable>()?.Dirty(); } }

        void Start() { node = GetComponent<UtilityNode>(); box = GetComponent<Container>(); if (head) rest = head.localRotation; }

        bool Armed => on && node && node.Powered && (rounds > 0 || (box && box.inventory.GetItem("ammo_mg") > 0));

        void Update()
        {
            if (node) node.demand = on ? (target ? watts : 20f) : 0f;
            if (!head) return;
            if (!Armed) { target = null; head.localRotation = Quaternion.Slerp(head.localRotation, rest * Quaternion.Euler(25f, 0f, 0f), Time.deltaTime * 2f); return; }
            if ((scan -= Time.deltaTime) <= 0f) { scan = 0.5f; target = Pick(); }
            if (!target) { head.localRotation = Quaternion.Slerp(head.localRotation, rest * Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.5f) * 70f, 0f), Time.deltaTime * 1.5f); return; }
            var aim = target.transform.position + Vector3.up * 1.1f - head.position;
            var want = Quaternion.LookRotation(aim);
            head.rotation = Quaternion.RotateTowards(head.rotation, want, 160f * Time.deltaTime);
            if ((fireCd -= Time.deltaTime) > 0f || Quaternion.Angle(head.rotation, want) > 6f) return;
            fireCd = 0.28f;
            Fire(aim.magnitude);
        }

        MadMax.Npc.Npc Pick()
        {
            MadMax.Npc.Npc best = null; float bd = range;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || !(n.Hostile || n.raiding)) continue;
                float d = Vector3.Distance(n.transform.position, head.position);
                if (d < bd && Clear(n, d)) { bd = d; best = n; }
            }
            return best;
        }

        bool Clear(MadMax.Npc.Npc n, float d)
        {
            var from = head.position + Vector3.up * 0.25f;
            var dir = (n.transform.position + Vector3.up * 1.1f - from).normalized;
            int c = Physics.RaycastNonAlloc(from, dir, hits, d, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < c; i++)
            {
                var t = hits[i].collider.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(n.transform) || hits[i].distance < 1.2f) continue;   // own mount clutter (battery, crates)
                if (hits[i].distance < d - 0.5f) return false;
            }
            return true;
        }

        void Fire(float dist)
        {
            if (rounds <= 0) { if (!box || !box.inventory.TakeItem("ammo_mg")) return; rounds = BeltRounds; GetComponent<Placeable>()?.Dirty(); }
            rounds--;
            var muzzle = head.position + head.forward * 1.3f + head.up * 0.24f;
            MadMax.World.Fx.Flash(muzzle, new Color(1f, 0.8f, 0.4f), 6f, 3f, 0.06f);
            MadMax.Audio.Sfx.Play("shotgun", muzzle, 0.7f, Random.Range(1.5f, 1.7f), 90f);
            if (Random.value < Mathf.Clamp01(0.8f - dist / 60f)) target.ApplyHit(target.transform.position + Vector3.up * 1.1f, head.forward, 0.55f, 0.1f, gameObject);
            else if (MadMax.World.DebrisSystem.Instance) MadMax.World.DebrisSystem.Instance.EmitPuff(target.transform.position + Random.insideUnitSphere, new Color32(190, 150, 110, 255), 0.06f, Vector3.up, 0.5f);
        }

        public string SaveState() => (on ? "1" : "0") + rounds;
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            on = s[0] == '1';
            if (s.Length > 1 && int.TryParse(s.Substring(1), out int r)) rounds = r;
        }
    }
}
