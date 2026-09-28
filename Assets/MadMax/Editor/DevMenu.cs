using UnityEditor;

namespace MadMax.EditorTools
{
    /// <summary>MadMax > Dev: editor equivalents of the player flags --no-intro, --no-menu, --continue (see LaunchOptions).</summary>
    public static class DevMenu
    {
        const string NoIntro = "MadMax/Dev/Skip Intro (--no-intro)", NoMenu = "MadMax/Dev/Skip Menu (--no-menu)", Continue = "MadMax/Dev/Continue Save (--continue)";

        static void Toggle(string flag) => EditorPrefs.SetBool("MadMax.Dev." + flag, !EditorPrefs.GetBool("MadMax.Dev." + flag, false));
        static bool Get(string flag) => EditorPrefs.GetBool("MadMax.Dev." + flag, false);

        [MenuItem(NoIntro)] static void ToggleNoIntro() => Toggle("no-intro");
        [MenuItem(NoIntro, true)] static bool CheckNoIntro() { Menu.SetChecked(NoIntro, Get("no-intro")); return true; }
        [MenuItem(NoMenu)] static void ToggleNoMenu() => Toggle("no-menu");
        [MenuItem(NoMenu, true)] static bool CheckNoMenu() { Menu.SetChecked(NoMenu, Get("no-menu")); return true; }
        [MenuItem(Continue)] static void ToggleContinue() => Toggle("continue");
        [MenuItem(Continue, true)] static bool CheckContinue() { Menu.SetChecked(Continue, Get("continue")); return true; }
    }
}
