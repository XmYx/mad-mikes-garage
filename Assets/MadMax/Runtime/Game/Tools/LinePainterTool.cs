using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Road line painter (depth stage C): a spray lance on a paint tin. A click paints the cell under the
    /// nozzle; holding the button paints a line behind it as the player walks. Paint takes only on set asphalt or
    /// concrete (and the old paved highways); white or yellow, one dye per <see cref="MetresPerDye"/> m of line.
    /// Crouch + click switches the colour.</summary>
    public class LinePainterTool : HandTool
    {
        /// <summary>The use button is held (set each frame by the game from the mouse / pad, or by automation).</summary>
        public bool spraying;
        /// <summary>Nozzle ahead of the player's feet (m).</summary>
        public float reach = 0.8f;
        /// <summary>Paint colour: 1 white, 2 yellow (kept across tools and saves).</summary>
        public static byte Colour = 1;
        /// <summary>Metres of line painted since the last dye was used up.</summary>
        public static float Metres;
        public const float MetresPerDye = 4f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Colour = 1; Metres = 0f; }

        static readonly Vector2Int None = new Vector2Int(int.MinValue, int.MinValue);
        Vector2Int last = None;
        PlayerCharacter user;

        public static string DyeFor(byte colour) => colour == 2 ? "dye_yellow" : "dye_white";
        public static string ColourName(byte colour) => colour == 2 ? "YELLOW" : "WHITE";

        public override void Strike(PlayerCharacter u)
        {
            user = u;
            var g = WastelandGame.Instance;
            if (!g) return;
            if (u.crouch) { Colour = Colour == 1 ? (byte)2 : (byte)1; g.Toast("LINE PAINTER: " + ColourName(Colour) + " PAINT"); return; }
            last = None;
            PaintHere(g, true);
        }

        void Update()
        {
            if (!spraying) { last = None; return; }
            if (!user) user = GetComponentInParent<PlayerCharacter>();
            var g = WastelandGame.Instance;
            if (!user || !g || g.Current) return;
            PaintHere(g, false);
            MadMax.Audio.Sfx.Loop(this, "pour", 0.2f, 1.6f, 12f);                              // the hiss of the lance (a loop is asked for every frame)
        }

        /// <summary>Paint the cell under the nozzle (once per cell while the nozzle moves). True when a cell took paint.</summary>
        public bool PaintHere(WastelandGame g, bool report)
        {
            var t = DeformableTerrain.Instance;
            if (!t || t.World == null || !user) return false;
            const float c = DeformableTerrain.Cell;
            var p = user.transform.position + user.transform.forward * reach;
            var cell = new Vector2Int(Mathf.RoundToInt(p.x / c), Mathf.RoundToInt(p.z / c));
            if (cell == last) return false;
            var prev = last; last = cell;
            string dye = DyeFor(Colour);
            if (g.Inventory.GetItem(dye) <= 0)
            {
                byte other = Colour == 1 ? (byte)2 : (byte)1;                                  // out of this colour: the other if carried
                if (g.Inventory.GetItem(DyeFor(other)) <= 0) { if (report) g.Toast("LINE PAINTER: NEEDS WHITE OR YELLOW DYE"); return false; }
                Colour = other; dye = DyeFor(other);
                g.Toast("LINE PAINTER: " + ColourName(Colour) + " PAINT");
            }
            var at = new Vector3(cell.x * c, t.Height(cell.x * c, cell.y * c), cell.y * c);
            float done = t.ApplyTerraform((byte)DeformableTerrain.TerraOp.Paint, at, 0.13f, 0f, Colour);
            if (done <= 0f)
            {
                if (report)
                {
                    byte kind = t.PaveAt(at.x, at.z);
                    g.Toast(DeformableTerrain.PaintOf(kind) == Colour ? "ALREADY PAINTED"
                          : DeformableTerrain.PaveBase(kind) != 0 && t.CureAt(at.x, at.z) < 1f ? "LINE PAINTER: THE PAVING IS STILL WET"
                          : "LINE PAINTER: PAINT TAKES ON ASPHALT OR CONCRETE");
                }
                return false;
            }
            MadMax.Net.NetSession.Instance?.SendTerraform((byte)DeformableTerrain.TerraOp.Paint, at, 0.13f, 0f, Colour);
            Metres += prev == None ? c : Mathf.Min(1f, Vector2Int.Distance(prev, cell) * c);
            if (Metres >= MetresPerDye) { Metres -= MetresPerDye; g.Inventory.TakeItem(dye); }
            if (Random.value < 0.4f) Fx.Smoke(at + Vector3.up * 0.1f, Vector3.up * 0.2f, 0.15f, Colour == 2 ? new Color(0.85f, 0.7f, 0.3f, 0.5f) : new Color(0.9f, 0.9f, 0.85f, 0.5f), 0.5f);
            return true;
        }
    }
}
