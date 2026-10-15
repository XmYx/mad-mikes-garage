using MadMax.Game;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Boss car chase (2026-10-14): a gang that knows the player wants a stolen part back
    /// (<see cref="WastelandGame.GangRuns"/>) does not stop for a toll — the boss car runs along its road away from the
    /// player while the escorts harass. Disabled, or boxed in for a few seconds, the crew come out fighting and the
    /// trunk can be opened (<see cref="WastelandGame.OpenBossCar"/>); far enough ahead, it got away.</summary>
    public partial class Convoy
    {
        /// <summary>The boss car is running from the player.</summary>
        public bool running;
        public const float RunSpeed = 26f, RunGiveUp = 260f, BoxedSeconds = 2.5f;
        float boxedT, redirectT;

        void Run(WastelandGame g, Transform target)
        {
            var lead = cars[0];
            running = true; phase = Phase.Flee; timer = 0f; boxedT = 0f; redirectT = 0f;
            ResetChase();
            lead.SetPath(route, AwayDir(lead.transform.position, target.position));
            lead.cruise = RunSpeed;
            for (int i = 1; i < cars.Count; i++)
            {
                var c = cars[i];
                if (!c || !c.enabled) continue;
                c.target = target; c.goal = AiDriver.Goal.Chase; c.avoidTarget = false; c.chaseSpeed = 24f;   // the escorts cover the getaway
            }
            MadMax.Audio.Sfx.Play("horn", lead.transform.position, 1f, 1.15f, 150f);
            g.NoteRun(Gang);
        }

        /// <summary>The route direction (+1/−1) whose points lead away from <paramref name="threat"/>.</summary>
        int AwayDir(Vector3 from, Vector3 threat)
        {
            if (route == null || route.Count < 2) return 1;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < route.Count; i++) { float d = (route[i] - from).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            var ahead = route[Mathf.Min(route.Count - 1, best + 4)];
            var back = route[Mathf.Max(0, best - 4)];
            return (ahead - threat).sqrMagnitude >= (back - threat).sqrMagnitude ? 1 : -1;
        }

        void RunTick(WastelandGame g, Transform target, float dist, float dt)
        {
            var lead = cars.Count > 0 ? cars[0] : null;
            if (!lead || !lead.enabled || lead.Disabled) { running = false; Attack(g); return; }   // stopped by force: the crew fight (BailOut puts them out)
            float speed = lead.Vehicle ? Mathf.Abs(lead.Vehicle.ForwardSpeed) : 0f;
            ChaseRadio(g, lead.transform.position, dt);                                            // a long chase calls the gang (Convoy.Call)
            boxedT = speed < 2f && dist < 14f && timer > 3f ? boxedT + dt : 0f;
            if (boxedT > BoxedSeconds)
            {
                running = false;
                g.Toast("THE " + Gang + "'S BOSS CAR IS BOXED IN: THEY COME OUT FIGHTING");
                Attack(g);
                return;
            }
            if (dist > RunGiveUp)                                                                  // got away: back on the road
            {
                running = false; phase = Phase.Travel; timer = 0f;
                foreach (var c in cars) if (c) c.cruise = 12f;
                lead.SetPath(route, lead.dir);
                g.Toast("THE " + Gang + "'S BOSS CAR GOT AWAY");
                return;
            }
            redirectT += dt;
            if (redirectT > 4f)                                                                    // keep the road leading away
            {
                redirectT = 0f;
                int away = AwayDir(lead.transform.position, target.position);
                if (away != lead.dir || lead.goal != AiDriver.Goal.Path) lead.SetPath(route, away);
                lead.cruise = RunSpeed;
            }
        }
    }
}
