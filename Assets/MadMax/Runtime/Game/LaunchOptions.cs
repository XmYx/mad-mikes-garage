using System;

namespace MadMax.Game
{
    /// <summary>Startup switches for quick testing. Player: command-line flags (either -flag or --flag):
    /// --no-intro (skip the boot film and title flyover, land on the menu), --no-menu (straight into the game:
    /// no film, no title, no menu), --continue (with --no-menu: load the save instead of a fresh world).
    /// Editor: the same switches as toggles under MadMax > Dev (EditorPrefs).</summary>
    public static class LaunchOptions
    {
        public static bool NoIntro => Has("no-intro") || NoMenu;
        public static bool NoMenu => Has("no-menu");
        public static bool Continue => Has("continue");

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
