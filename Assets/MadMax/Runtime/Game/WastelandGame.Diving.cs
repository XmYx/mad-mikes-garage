using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    public partial class WastelandGame
    {
        /// <summary>Seconds of air left in the diver's tank (saved).</summary>
        public float TankAir = TankSize;
        public const float TankSize = 180f;
        public const string O2Bottle = "use_o2_bottle";
        float diveHurt;

        /// <summary>Wearing the brass helmet with an air tank on the back: the player can go under and walk the bottom.</summary>
        public bool DiveReady => Wearing("dive_helmet") && Wearing("air_tank");

        /// <summary>Tank air while the helmet is under (user additions), drowning when it runs dry, cold water without
        /// the suit. Topped up at an air compressor or from O2 bottles.</summary>
        void UpdateDiving()
        {
            if (!Player) return;
            float dt = Time.deltaTime;
            if (Player.Diving && Player.HeadUnder)
            {
                float before = TankAir;
                TankAir = Mathf.Max(0f, TankAir - dt * (Player.Velocity.sqrMagnitude > 1f ? 1.25f : 1f));
                if (before > 30f && TankAir <= 30f) Toast("AIR LOW: 30 SECONDS. SURFACE!");
                if (TankAir <= 0f && (diveHurt -= dt) <= 0f) { diveHurt = 1f; Vitals.Hurt(6f, "DROWNED"); }
                if (!Wearing("dive_suit")) Stats.bodyTemp = Mathf.MoveTowards(Stats.bodyTemp, 30f, dt * 0.02f);   // cold water through bare clothes
            }
        }

        /// <summary>Refill the tank (compressor seconds, or a bottle).</summary>
        public void FillTank(float seconds) => TankAir = Mathf.Min(TankSize, TankAir + seconds);

        /// <summary>Using an O2 bottle: into the submarine's cabin if aboard one, else into the diving tank.</summary>
        bool UseO2Bottle()
        {
            var sub = Player && Player.Interior ? Player.Interior.GetComponent<MadMax.Vehicles.Submarine>() : Current ? Current.GetComponent<MadMax.Vehicles.Submarine>() : null;
            if (!Inventory.TakeItem(O2Bottle)) return false;
            if (sub) { sub.AddAir(35f); Toast("O2 BOTTLE HISSES INTO THE CABIN: AIR +35%"); }
            else { FillTank(90f); Toast("TANK TOPPED UP FROM A BOTTLE: " + Mathf.RoundToInt(TankAir) + " S OF AIR"); }
            return true;
        }
    }
}
