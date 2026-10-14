using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Railway pieces: RAIL TRACK / SCRAP TRACK are laid point to point by <c>BuildMode.Rail</c> (from a track
    /// end, or branching off any track — that makes points with a lever stand — or anywhere), costs per 2 m; a buffer
    /// stop, a low platform section (walkable, drivable), a signal (red while a train is near) and a halt shelter are
    /// ordinary pieces.</summary>
    public static partial class FurnitureLibrary
    {
        public const string RailTrack = "rail_track", RailTrackScrap = "rail_track_scrap";

        /// <summary>Is this one of the pseudo pieces laid by the rail tool?</summary>
        public static bool IsTrack(FurnitureDef d) => d != null && d.id.StartsWith("rail_track");

        static IEnumerable<FurnitureDef> RailPieces()
        {
            var S = BuildCategory.Structure;
            FurnitureDef T(string id, string name, bool scrap, string desc, params (ResourceType, int)[] cost)
            {
                var g = new VoxelGrid().Mat((byte)ResourceType.Iron);
                for (int z = 0; z < 24; z += 3) g.Box(-9, 0, z, 9, 1, z + 1, Pal.Ramp(scrap ? Pal.Rust : Pal.Wood, 1, 3100));
                g.Box(-7, 2, 0, -6, 3, 24, Pal.Ramp(Pal.Chrome, 1, 3101));
                g.Box(6, 2, 0, 7, 3, 24, Pal.Ramp(Pal.Chrome, 1, 3101));
                var d = Def(id, name, g, cost);
                d.category = S; d.hits = 30; d.desc = desc;
                return d;
            }
            yield return T(RailTrack, "RAIL TRACK", false, "STANDARD GAUGE ON TIMBER SLEEPERS AND BALLAST: CLICK A TRACK END (OR THE SIDE OF A TRACK FOR NEW POINTS), THEN WHERE IT GOES. CURVES NO TIGHTER THAN 25 M. PER 2 M", (ResourceType.Iron, 1), (ResourceType.Wood, 1));
            yield return T(RailTrackScrap, "SCRAP TRACK", true, "RAILS BENT OUT OF SCRAP ON WHATEVER SLEEPERS ARE TO HAND: CHEAP, RATTLY. PER 2 M", (ResourceType.Scrap, 2), (ResourceType.Wood, 1));

            var b = new VoxelGrid().Mat((byte)ResourceType.Wood);
            b.Box(-10, 0, -6, 10, 6, 6, Pal.Ramp(Pal.Fur, 1, 3110));                                  // a heap of ballast and sleepers
            b.Mat((byte)ResourceType.Iron);
            for (int s = -1; s <= 1; s += 2) b.Box(s * 9 - 1, 0, -2, s * 9 + 1, 13, 0, Pal.Ramp(Pal.Rust, 1, 3111));
            b.Mat((byte)ResourceType.Wood);
            b.Box(-11, 11, 0, 11, 14, 2, Pal.Stripe(Pal.Ramp(Pal.Crimson, 2, 3112), Pal.Ramp(Pal.Cream, 2, 3113), 0, 6, 3));
            var buf = D("rail_buffer", "BUFFER STOP", S, b, 25, false, null, (ResourceType.Iron, 4), (ResourceType.Wood, 4), (ResourceType.Stone, 4));
            buf.desc = "A BEAM ACROSS THE END OF A TRACK: TRAINS STOP AGAINST IT (ANY DEAD END STOPS THEM ANYWAY)";
            yield return buf;

            var p = new VoxelGrid().Mat((byte)ResourceType.Concrete);
            p.Box(-12, 0, -25, 12, 3, 25, Pal.Ramp(Pal.Cream, 0, 3120));
            p.Box(-12, 4, -25, -10, 4, 25, Pal.Ramp(Pal.Ochre, 3, 3121));
            yield return D("rail_platform", "PLATFORM SECTION", S, p, 30, true, null, (ResourceType.Concrete, 3), (ResourceType.Stone, 2)).Deck(1f, 2f, 0.36f, 0.36f);

            var sg = new VoxelGrid().Mat((byte)ResourceType.Iron);
            sg.Box(-1, 0, -1, 1, 57, 1, Pal.Ramp(Pal.Black, 1, 3130));
            sg.Box(-3, 48, -2, 3, 62, 2, Pal.Ramp(Pal.Black, 0, 3131));
            sg.Box(-4, 0, -4, 4, 1, 4, Pal.Ramp(Pal.Cream, 0, 3132));
            yield return D("rail_signal", "SIGNAL", S, sg, 8, false, go => { var g = MadMax.Game.WastelandGame.Instance; if (g) MadMax.World.RailSignal.Attach(go, 4.4f, MadMax.World.RollingCity.GlowMaterial(g.propMaterial)); }, (ResourceType.Iron, 3), (ResourceType.Glass, 2), (ResourceType.Copper, 1));

            var sh = new VoxelGrid().Mat((byte)ResourceType.Wood);
            sh.Box(-19, 0, -11, 19, 30, -10, Pal.Ramp(Pal.Wood, 1, 3140));
            sh.Box(-19, 0, -11, -18, 30, 6, Pal.Ramp(Pal.Wood, 1, 3140));
            sh.Box(18, 0, -11, 19, 30, 6, Pal.Ramp(Pal.Wood, 1, 3140));
            sh.Mat((byte)ResourceType.Scrap);
            sh.Box(-21, 31, -13, 21, 32, 9, Pal.Weathered(Pal.Metal, 0.4f, 3141));
            sh.Mat((byte)ResourceType.Wood);
            sh.Box(-15, 5, -9, 15, 6, -6, Pal.Ramp(Pal.Wood, 2, 3142));
            sh.Bevel();
            yield return D("rail_shelter", "HALT SHELTER", S, sh, 12, true, go => Sit(go, 1.2f, 1.1f, new Vector3(-0.6f, 0.62f, -0.6f), new Vector3(0.6f, 0.62f, -0.6f)), (ResourceType.Wood, 10), (ResourceType.Scrap, 4));
        }
    }
}
