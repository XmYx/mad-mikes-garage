using MadMax.Game;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Ambulance treatment bay (roadmap 19): while you are inside the vehicle's walk-in space it bandages
    /// bleeding wounds, splints fractures, draws out infection and heals everything several times faster, and tops up
    /// health. Nothing to carry — the bay is stocked.</summary>
    [RequireComponent(typeof(InteriorSpace))]
    public class MedicalBay : MonoBehaviour
    {
        public float healRate = 5f;              // × the normal healing speed
        InteriorSpace space;
        float inside, toastAt;

        void Awake() => space = GetComponent<InteriorSpace>();

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || !g.Player || !space || g.Player.Interior != space || !g.Vitals || g.Vitals.Dead) { inside = 0f; return; }
            float dt = Time.deltaTime;
            inside += dt;
            var s = g.Stats;
            bool treated = false;
            foreach (var inj in s.injuries)
            {
                if (inj.Bleeding && inside > 3f) { inj.bandaged = true; inj.bandageAge = 0f; inj.rough = false; treated = true; }
                if (inj.type == Wound.Fracture && !inj.splinted && inside > 6f) { inj.splinted = true; inj.rough = false; treated = true; }
                if (inj.infection > 0f) inj.infection = Mathf.Max(0f, inj.infection - dt * 0.02f * healRate);
                if (inside > 2f) inj.severity = Mathf.Max(0f, inj.severity - dt * (healRate - 1f) / (inj.HealMinutes * 60f));
            }
            if (inside > 2f) s.health = Mathf.Min(s.MaxHealth, s.health + dt * 0.25f * healRate);
            if (treated && Time.time > toastAt) { toastAt = Time.time + 8f; g.Toast("THE AMBULANCE BAY PATCHES YOU UP"); g.Stats.Practice(Skill.Survival, 1f); }
        }
    }
}
