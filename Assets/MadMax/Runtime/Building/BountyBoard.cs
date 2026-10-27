using MadMax.Npc;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A town's bounty board (roadmap 15), spawned by the NpcDirector at every settlement: [E] reads it — today's
    /// bounties and hauls, jobs to claim, crates to hand in, and what the local market pays; [T] signs up for today's
    /// race or arena (<see cref="MadMax.Game.Racing"/>). Its notices are posters of what is really on offer
    /// (BountyBoard.Posters.cs).</summary>
    public partial class BountyBoard : MonoBehaviour, IInteractable
    {
        public int town;
        public static readonly System.Collections.Generic.List<BountyBoard> All = new System.Collections.Generic.List<BountyBoard>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() { All.Add(this); Contracts.Taken += OnTaken; Contracts.Paid += OnPaid; }
        void OnDisable() { All.Remove(this); Contracts.Taken -= OnTaken; Contracts.Paid -= OnPaid; }

        public MadMax.World.Settlement Settlement
        {
            get
            {
                var w = MadMax.World.DeformableTerrain.Instance ? MadMax.World.DeformableTerrain.Instance.World : null;
                if (w == null) return null;
                foreach (var st in w.settlements) if (st.index == town) return st;
                return null;
            }
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            int ready = 0;
            foreach (var c in Contracts.Active) if (c.completed || (c.Delivery && c.dest == town && !c.failed)) ready++;
            return "[E] BOUNTY BOARD - " + Market.TownName(Settlement) + (ready > 0 ? "  (" + ready + " TO HAND IN)" : "") + (MadMax.Game.Racing.Instance ? MadMax.Game.Racing.Instance.Prompt(town) : "");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) { MadMax.Game.Racing.Instance?.SignUp(town); return; }
            g.Menus.OpenBoard(this);
        }
    }
}
