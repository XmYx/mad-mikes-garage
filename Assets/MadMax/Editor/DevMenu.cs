using UnityEditor;

namespace MadMax.EditorTools
{
    /// <summary>MadMax > Dev: editor equivalents of the player flags --no-intro, --no-menu, --continue, --dev, --no-hd, --voxel-humans,
    /// --procedural-anim (see LaunchOptions).</summary>
    public static class DevMenu
    {
        const string NoIntro = "MadMax/Dev/Skip Intro (--no-intro)", NoMenu = "MadMax/Dev/Skip Menu (--no-menu)", Continue = "MadMax/Dev/Continue Save (--continue)", DevKeys = "MadMax/Dev/Debug Keys (--dev)", NoHD = "MadMax/Dev/Voxel Visuals (--no-hd)";

        static void Toggle(string flag) => EditorPrefs.SetBool("MadMax.Dev." + flag, !EditorPrefs.GetBool("MadMax.Dev." + flag, false));
        static bool Get(string flag) => EditorPrefs.GetBool("MadMax.Dev." + flag, false);

        [MenuItem(NoIntro)] static void ToggleNoIntro() => Toggle("no-intro");
        [MenuItem(NoIntro, true)] static bool CheckNoIntro() { Menu.SetChecked(NoIntro, Get("no-intro")); return true; }
        [MenuItem(NoMenu)] static void ToggleNoMenu() => Toggle("no-menu");
        [MenuItem(NoMenu, true)] static bool CheckNoMenu() { Menu.SetChecked(NoMenu, Get("no-menu")); return true; }
        [MenuItem(Continue)] static void ToggleContinue() => Toggle("continue");
        [MenuItem(Continue, true)] static bool CheckContinue() { Menu.SetChecked(Continue, Get("continue")); return true; }
        [MenuItem(DevKeys)] static void ToggleDev() => Toggle("dev");
        [MenuItem(DevKeys, true)] static bool CheckDev() { Menu.SetChecked(DevKeys, Get("dev")); return true; }
        [MenuItem(NoHD)] static void ToggleNoHD() => Toggle("no-hd");
        [MenuItem(NoHD, true)] static bool CheckNoHD() { Menu.SetChecked(NoHD, Get("no-hd")); return true; }
        const string Voxel = "MadMax/Dev/Voxel Humans (--voxel-humans)", Procedural = "MadMax/Dev/Procedural Animation (--procedural-anim)";
        [MenuItem(Voxel)] static void ToggleVoxel() => Toggle("voxel-humans");
        [MenuItem(Voxel, true)] static bool CheckVoxel() { Menu.SetChecked(Voxel, Get("voxel-humans")); return true; }
        [MenuItem(Procedural)] static void ToggleProcedural() => Toggle("procedural-anim");
        [MenuItem(Procedural, true)] static bool CheckProcedural() { Menu.SetChecked(Procedural, Get("procedural-anim")); return true; }
    }
}
