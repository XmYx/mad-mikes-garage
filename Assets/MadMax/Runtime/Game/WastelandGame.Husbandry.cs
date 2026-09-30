using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Depth stage E hooks (husbandry, textiles and leather, hunting and trapping): a tally of the ladder — hay
    /// cut, wool shorn, honey taken, animals treated, game trapped — with a journal line the first time each happens,
    /// and hints along the way (hay → feed, wool → spinning wheel, honey → jars and mead). The tally saves in
    /// <c>SaveData.blockHusbandry</c> as "key=count". Everything else keeps its own state: pieces through
    /// <c>IPlaceState</c>, kept animals through <c>AnimalSave.husbandry</c>.</summary>
    public partial class WastelandGame
    {
        readonly Dictionary<string, int> husbandryTally = new Dictionary<string, int>();
        float husbandryHintT;

        static readonly Dictionary<string, string> HusbandryFirsts = new Dictionary<string, string>
        {
            { "hay", "CUT MY FIRST HAY. WITH GRAIN AT A WORKBENCH OR A FEED MILL IT MAKES ANIMAL FEED - BETTER THAN RAW CROPS IN THE TROUGH." },
            { "wool", "SHEARED MY FIRST FLEECE. A SPINNING WHEEL TURNS WOOL INTO THREAD, THE LOOM THREAD INTO CLOTH." },
            { "honey", "TOOK HONEY FROM MY OWN HIVE. JARRED IT'S FOOD, WITH WAX AND HERBS A SALVE, AT THE STILL IT'S MEAD." },
            { "treated", "PATCHED UP AN ANIMAL. A BANDAGE OR SALVE STOPS THE BLEEDING, A SPLINT SETS A LEG, ANTIBIOTICS FOR THE FEVER." },
            { "trapped", "SOMETHING WALKED INTO MY TRAP. A BUTCHERING TABLE GETS MORE OUT OF A CARCASS THAN A KNIFE ON THE GROUND." },
        };

        /// <summary>Count a step of the husbandry ladder; the first one goes in the journal.</summary>
        public void HusbandryTally(string what, int n = 1)
        {
            if (n <= 0) return;
            husbandryTally.TryGetValue(what, out int had);
            husbandryTally[what] = had + n;
            if (had == 0 && HusbandryFirsts.TryGetValue(what, out var line)) Journal.Add("FARM", line);
        }

        public int HusbandryCount(string what) => husbandryTally.TryGetValue(what, out int n) ? n : 0;

        partial void HusbandryUpdate()
        {
            if ((husbandryHintT -= Time.deltaTime) > 0f || !Player) return;
            husbandryHintT = 5f;
            if (Inventory.Get(ResourceType.Hay) >= 3 && Inventory.Get(ResourceType.Feed) == 0)
                Hints.Show("husbandry_feed", "HAY: MIX IT WITH WHEAT OR CORN INTO ANIMAL FEED (WORKBENCH, FEED MILL), OR TIP IT IN A TROUGH");
            else if (Inventory.Get(ResourceType.Wool) > 0)
                Hints.Show("husbandry_wool", "WOOL: SPIN IT INTO THREAD AT A SPINNING WHEEL; THE LOOM WEAVES THREAD INTO CLOTH");
            else if (Inventory.Get(ResourceType.Honey) > 0)
                Hints.Show("husbandry_honey", "HONEY: JAR IT AT A WORKBENCH (NEEDS GLASS), BREW MEAD AT THE STILL, OR MAKE A SALVE FOR ANIMALS");
            else if (Inventory.Get(ResourceType.Hide) >= 2)
                Hints.Show("husbandry_hide", "HIDES: TAN THEM AT A TANNING RACK; LEATHER GOODS (BOOTS, BELTS, ARMOUR, SADDLEBAGS) AT A LEATHER BENCH");
        }

        partial void HusbandrySave(SaveData d)
        {
            d.blockHusbandry.Clear();
            foreach (var kv in husbandryTally) d.blockHusbandry.Add(kv.Key + "=" + kv.Value);
        }

        partial void HusbandryLoad(SaveData d)
        {
            husbandryTally.Clear();
            if (d.blockHusbandry == null) return;
            foreach (var e in d.blockHusbandry)
            {
                int eq = e.IndexOf('=');
                if (eq > 0 && int.TryParse(e.Substring(eq + 1), out int n)) husbandryTally[e.Substring(0, eq)] = n;
            }
        }

        partial void HusbandryNewGame() => husbandryTally.Clear();
    }
}
