using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Ore cart (depth stage H): a 300 kg steel tub on flanged wheels. [E] opens it, and crafting stations within
    /// 5 m draw from it like any container, so a cart parked at the stamp mill or wash plant feeds it. Set on a
    /// <see cref="MineRail"/> it snaps onto the track; [T] pushes it away from you to the end of the line (up to
    /// 80 m, through every joint), slower when loaded.</summary>
    public class OreCart : MonoBehaviour, IInteractable
    {
        public const float MaxRun = 80f;
        readonly List<Vector3> path = new List<Vector3>();
        readonly HashSet<MineRail> visited = new HashSet<MineRail>();
        int leg;
        float speed, rumble;
        Container box;

        public bool Moving { get; private set; }

        void Awake() => box = GetComponent<Container>();
        void Start() => Snap();

        /// <summary>Settle onto the nearest rail within 0.9 m (keeps its facing along the track). False: no rail.</summary>
        public bool Snap()
        {
            var rail = MineRail.Nearest(transform.position, 0.9f, out float t);
            if (!rail) return false;
            var a = rail.A; var b = rail.B;
            var dir = (b - a).normalized;
            if (Vector3.Dot(transform.forward, dir) < 0f) dir = -dir;
            transform.SetPositionAndRotation(Vector3.Lerp(a, b, t), Quaternion.LookRotation(dir, rail.transform.up));
            return true;
        }

        public bool OnRails => MineRail.Nearest(transform.position, 0.9f, out _);

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (Moving) return "ORE CART: ROLLING";
            if (!OnRails) return "ORE CART: SET IT ON A MINE RAIL TO PUSH IT";
            return "[T] PUSH ORE CART" + (box ? " (" + Mathf.RoundToInt(box.Weight) + " KG)" : "");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary || !g.Player) return;
            float run = Push(g.Player.transform.position);
            if (run <= 0.05f) g.Toast(OnRails ? "THE CART IS AT THE END OF THE LINE" : "LAY MINE RAIL UNDER THE CART");
        }

        /// <summary>Push the cart along the track away from <paramref name="from"/>, to the end of the line. Returns the
        /// length of the run (0: not on rails or nowhere to go).</summary>
        public float Push(Vector3 from)
        {
            if (Moving) return 0f;
            var rail = MineRail.Nearest(transform.position, 0.9f, out _);
            if (!rail) return 0f;
            Snap();
            var a = rail.A; var b = rail.B;
            var away = transform.position - from;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            var end = Vector3.Dot(b - a, away) >= 0f ? b : a;
            path.Clear(); visited.Clear();
            path.Add(transform.position); path.Add(end);
            visited.Add(rail);
            float length = Vector3.Distance(transform.position, end);
            while (length < MaxRun)
            {
                var next = MineRail.Joined(end, visited, out var far);
                if (!next) break;
                visited.Add(next);
                length += Vector3.Distance(end, far);
                path.Add(far); end = far;
            }
            if (length < 0.05f) return 0f;
            speed = Mathf.Lerp(3f, 1.4f, box ? Mathf.Clamp01(box.Weight / box.capacity) : 0f);
            leg = 1; Moving = true;
            MadMax.Audio.Sfx.Play("chain", transform.position, 0.5f, 0.8f, 20f);
            return length;
        }

        void Update()
        {
            if (!Moving) return;
            float dt = Time.deltaTime;
            var target = path[leg];
            var dir = target - transform.position;
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), 1f - Mathf.Exp(-12f * dt));
            transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
            if ((rumble -= dt) <= 0f) { rumble = 0.45f; MadMax.Audio.Sfx.Play("creak", transform.position, 0.25f, Random.Range(0.9f, 1.2f), 18f); }
            if ((transform.position - target).sqrMagnitude > 1e-4f) return;
            if (++leg < path.Count) return;
            Moving = false;
            Snap();
            GetComponent<Placeable>()?.Dirty();
        }

        /// <summary>Finish a run at once (tests, and a cart pushed as the game saves).</summary>
        public void Arrive()
        {
            if (!Moving) return;
            transform.position = path[path.Count - 1];
            Moving = false;
            Snap();
            GetComponent<Placeable>()?.Dirty();
        }
    }
}
