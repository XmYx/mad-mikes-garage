namespace MadMax.Story
{
    /// <summary>Arc C numbers kept in the story's saved flags (a flag set can only grow, so each record is written once):
    /// bit flags "{key}#{bit}" plus "{key}#" marking it written. Survey times, litres delivered, posted shares.</summary>
    public static class ArcCRecord
    {
        const int Bits = 22;

        public static bool Has(string key) => Story.Flag(key + "#");

        /// <summary>Write a non-negative value once (later writes are ignored).</summary>
        public static void Put(string key, int value)
        {
            if (Has(key)) return;
            value = System.Math.Max(0, System.Math.Min(value, (1 << Bits) - 1));
            for (int b = 0; b < Bits; b++) if (((value >> b) & 1) != 0) Story.SetFlag(key + "#" + b);
            Story.SetFlag(key + "#");
        }

        public static int Get(string key)
        {
            int v = 0;
            for (int b = 0; b < Bits; b++) if (Story.Flag(key + "#" + b)) v |= 1 << b;
            return v;
        }
    }

    /// <summary>Where arc C's recurring drivers stand: Isaac (C1), Pru (C2) and Tobias wait at the convoy yard while C5
    /// gathers, ride in their trucks while it rolls (the one whose truck broke down stands beside it until that is
    /// settled), and wait at the destination's depot once it has arrived. Outside C5 each keeps their own chapter's place.</summary>
    public static class ArcCCrew
    {
        public static bool C5Active => Story.StateOf("C5") == Story.State.Active;
        public static bool Rolling => Story.Flag("c5_rolling") && !Story.StepDone("C5", "arrive");

        /// <summary>The driver's truck broke down and it isn't settled yet.</summary>
        public static bool Stranded(string key) => Story.Flag("c5_stranded:" + key) && !Story.StepDone("C5", "breakdown");

        public static bool Recruited(string key) =>
            key == "c1_driver" ? Story.StepDone("C5", "isaac") : key == "c2_hauler" ? Story.StepDone("C5", "pru") : key == "c5_tobias" && Story.StepDone("C5", "tobias");

        public static string Anchor(string key, string home)
        {
            if (!C5Active) return home;
            if (Story.StepDone("C5", "arrive")) return "c5_depot";
            if (Rolling) return Stranded(key) && StoryAnchors.Has("c5_broke") ? "c5_broke" : null;
            return "c5_yard";
        }

        /// <summary>Present for their own chapter (<paramref name="own"/>), or for the convoy.</summary>
        public static bool Present(string key, bool own)
        {
            if (!C5Active) return own;
            if (Story.StepDone("C5", "arrive")) return Recruited(key);
            if (Rolling) return Stranded(key);
            return true;
        }
    }
}
