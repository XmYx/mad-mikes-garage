using System;

namespace MadMax.Game
{
    /// <summary>Startup switches for quick testing. Player: command-line flags (either -flag or --flag):
    /// --no-intro (skip the boot film and title flyover, land on the menu), --no-menu (straight into the game:
    /// no film, no title, no menu), --continue (with --no-menu: load the save instead of a fresh world), --dev (debug keys), --voxel-humans,
    /// --procedural-anim (character look / motion fallbacks).
    /// Editor: the same switches as toggles under MadMax > Dev (EditorPrefs).</summary>
    public static class LaunchOptions
    {
        public static bool NoIntro => Has("no-intro") || NoMenu;
        public static bool NoMenu => Has("no-menu");
        public static bool Continue => Has("continue");
        /// <summary>--dev: debug keys (weather cycle, drop a part, instant repair; see <see cref="Controls"/>).</summary>
        public static bool Dev => Has("dev");
        /// <summary>--no-hd: voxel visuals instead of the HD asset pack (world, furniture, items, tools, animals).</summary>
        public static bool NoHD => Has("no-hd");
        /// <summary>--voxel-humans: the voxel HumanRig bodies even when the HD character pack is installed.</summary>
        public static bool VoxelHumans => Has("voxel-humans");
        /// <summary>--procedural-anim: HumanAnimator's procedural poses only (no keyframed clips).</summary>
        public static bool ProceduralAnim => Has("procedural-anim");

        static string[] args;

        static bool Has(string flag)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool("MadMax.Dev." + flag, false)) return true;
#endif
            args ??= Environment.GetCommandLineArgs();
            foreach (var a in args) if (a == "--" + flag || a == "-" + flag) return true;
            return false;
        }
    }
}
