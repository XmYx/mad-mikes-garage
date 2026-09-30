using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Alarm bell: rings by itself when hostiles come within <see cref="watch"/> m (heard far across the base),
    /// or by hand ([E]).</summary>
    public class AlarmBell : MonoBehaviour, IInteractable
    {
        public float watch = 35f;
        float scan, quietUntil;

        public static readonly System.Collections.Generic.List<AlarmBell> All = new System.Collections.Generic.List<AlarmBell>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Prompt(MadMax.Game.WastelandGame g) => "[E] RING THE BELL";
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary) Ring(false); }

        /// <summary>When it last rang (Time.time; -1 never).</summary>
        public float LastRung { get; private set; } = -1f;

        /// <summary>Three clangs; <paramref name="alarm"/> also warns the player (toast) wherever they are nearby.</summary>
        public void Ring(bool alarm)
        {
            LastRung = Time.time;
            var at = transform.position + transform.up * 2f;
            for (int i = 0; i < 3; i++) Invoke(nameof(Clang), i * 0.55f);
            if (!alarm) return;
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Player && Vector3.Distance(g.Player.transform.position, at) < 300f) g.Toast("ALARM BELL: HOSTILES AT THE BASE");
        }

        void Clang() => MadMax.Audio.Sfx.Play("bell", transform.position + transform.up * 2f, 1f, Random.Range(0.92f, 1.02f), 160f);

        void Update()
        {
            if ((scan -= Time.deltaTime) > 0f) return;
            scan = 1f;
            if (Time.time < quietUntil) return;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || !(n.Hostile || n.raiding)) continue;
                if ((n.transform.position - transform.position).sqrMagnitude > watch * watch) continue;
                quietUntil = Time.time + 25f;
                Ring(true);
                return;
            }
        }
    }
}
