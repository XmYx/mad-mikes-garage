namespace MadMax.Game
{
    /// <summary>Per-quest scene and tick hooks (see Story/StoryHooks.cs): Scene_&lt;id&gt; builds the quest's props once,
    /// when it first opens (flag "hook:&lt;id&gt;", saved), Tick_&lt;id&gt; runs every frame while it is active.
    /// Implement them in Game/StoryQuests/&lt;id&gt;.cs (public partial class WastelandGame).</summary>
    public partial class WastelandGame
    {
        partial void Scene_A1();
        partial void Tick_A1();
        partial void Scene_A5();
        partial void Tick_A5();
        partial void Scene_A6();
        partial void Tick_A6();
        partial void Scene_B3();
        partial void Tick_B3();
        partial void Scene_B4();
        partial void Tick_B4();
        partial void Scene_B5();
        partial void Tick_B5();
        partial void Scene_C1();
        partial void Tick_C1();
        partial void Scene_C2();
        partial void Tick_C2();
        partial void Scene_C3();
        partial void Tick_C3();
        partial void Scene_C4();
        partial void Tick_C4();
        partial void Scene_C5();
        partial void Tick_C5();
        partial void Scene_F1();
        partial void Tick_F1();
        partial void Scene_L1();
        partial void Tick_L1();
        partial void Scene_L2();
        partial void Tick_L2();
        partial void Scene_L3();
        partial void Tick_L3();
        partial void Scene_L4();
        partial void Tick_L4();
        partial void Scene_L5();
        partial void Tick_L5();
        partial void Scene_S01();
        partial void Tick_S01();
        partial void Scene_S02();
        partial void Tick_S02();
        partial void Scene_S03();
        partial void Tick_S03();
        partial void Scene_S04();
        partial void Tick_S04();
        partial void Scene_S05();
        partial void Tick_S05();
        partial void Scene_S06();
        partial void Tick_S06();
        partial void Scene_S07();
        partial void Tick_S07();
        partial void Scene_S08();
        partial void Tick_S08();
        partial void Scene_S09();
        partial void Tick_S09();
        partial void Scene_S10();
        partial void Tick_S10();
        partial void Scene_S11();
        partial void Tick_S11();
        partial void Scene_S12();
        partial void Tick_S12();
        partial void Scene_S13();
        partial void Tick_S13();
        partial void Scene_S14();
        partial void Tick_S14();
        partial void Scene_S15();
        partial void Tick_S15();
        partial void Scene_S16();
        partial void Tick_S16();
        partial void Scene_S17();
        partial void Tick_S17();
        partial void Scene_S18();
        partial void Tick_S18();
        partial void Scene_S19();
        partial void Tick_S19();
        partial void Scene_S20();
        partial void Tick_S20();
        partial void Scene_S21();
        partial void Tick_S21();
        partial void Scene_S22();
        partial void Tick_S22();
        partial void Scene_S23();
        partial void Tick_S23();
        partial void Scene_S24();
        partial void Tick_S24();
        partial void Scene_P1_1();
        partial void Tick_P1_1();
        partial void Scene_P1_2();
        partial void Tick_P1_2();
        partial void Scene_P1_3();
        partial void Tick_P1_3();
        partial void Scene_P2_1();
        partial void Tick_P2_1();
        partial void Scene_P2_2();
        partial void Tick_P2_2();
        partial void Scene_P2_3();
        partial void Tick_P2_3();
        partial void Scene_P2_4();
        partial void Tick_P2_4();
        partial void Scene_P3_1();
        partial void Tick_P3_1();
        partial void Scene_P3_2();
        partial void Tick_P3_2();
        partial void Scene_P3_3();
        partial void Tick_P3_3();

        void QuestHooks()
        {
            if (Opened("A1")) Scene_A1(); if (Running("A1")) Tick_A1();
            if (Opened("A5")) Scene_A5(); if (Running("A5")) Tick_A5();
            if (Opened("A6")) Scene_A6(); if (Running("A6")) Tick_A6();
            if (Opened("B3")) Scene_B3(); if (Running("B3")) Tick_B3();
            if (Opened("B4")) Scene_B4(); if (Running("B4")) Tick_B4();
            if (Opened("B5")) Scene_B5(); if (Running("B5")) Tick_B5();
            if (Opened("C1")) Scene_C1(); if (Running("C1")) Tick_C1();
            if (Opened("C2")) Scene_C2(); if (Running("C2")) Tick_C2();
            if (Opened("C3")) Scene_C3(); if (Running("C3")) Tick_C3();
            if (Opened("C4")) Scene_C4(); if (Running("C4")) Tick_C4();
            if (Opened("C5")) Scene_C5(); if (Running("C5")) Tick_C5();
            if (Opened("F1")) Scene_F1(); if (Running("F1")) Tick_F1();
            if (Opened("L1")) Scene_L1(); if (Running("L1")) Tick_L1();
            if (Opened("L2")) Scene_L2(); if (Running("L2")) Tick_L2();
            if (Opened("L3")) Scene_L3(); if (Running("L3")) Tick_L3();
            if (Opened("L4")) Scene_L4(); if (Running("L4")) Tick_L4();
            if (Opened("L5")) Scene_L5(); if (Running("L5")) Tick_L5();
            if (Opened("S01")) Scene_S01(); if (Running("S01")) Tick_S01();
            if (Opened("S02")) Scene_S02(); if (Running("S02")) Tick_S02();
            if (Opened("S03")) Scene_S03(); if (Running("S03")) Tick_S03();
            if (Opened("S04")) Scene_S04(); if (Running("S04")) Tick_S04();
            if (Opened("S05")) Scene_S05(); if (Running("S05")) Tick_S05();
            if (Opened("S06")) Scene_S06(); if (Running("S06")) Tick_S06();
            if (Opened("S07")) Scene_S07(); if (Running("S07")) Tick_S07();
            if (Opened("S08")) Scene_S08(); if (Running("S08")) Tick_S08();
            if (Opened("S09")) Scene_S09(); if (Running("S09")) Tick_S09();
            if (Opened("S10")) Scene_S10(); if (Running("S10")) Tick_S10();
            if (Opened("S11")) Scene_S11(); if (Running("S11")) Tick_S11();
            if (Opened("S12")) Scene_S12(); if (Running("S12")) Tick_S12();
            if (Opened("S13")) Scene_S13(); if (Running("S13")) Tick_S13();
            if (Opened("S14")) Scene_S14(); if (Running("S14")) Tick_S14();
            if (Opened("S15")) Scene_S15(); if (Running("S15")) Tick_S15();
            if (Opened("S16")) Scene_S16(); if (Running("S16")) Tick_S16();
            if (Opened("S17")) Scene_S17(); if (Running("S17")) Tick_S17();
            if (Opened("S18")) Scene_S18(); if (Running("S18")) Tick_S18();
            if (Opened("S19")) Scene_S19(); if (Running("S19")) Tick_S19();
            if (Opened("S20")) Scene_S20(); if (Running("S20")) Tick_S20();
            if (Opened("S21")) Scene_S21(); if (Running("S21")) Tick_S21();
            if (Opened("S22")) Scene_S22(); if (Running("S22")) Tick_S22();
            if (Opened("S23")) Scene_S23(); if (Running("S23")) Tick_S23();
            if (Opened("S24")) Scene_S24(); if (Running("S24")) Tick_S24();
            if (Opened("P1.1")) Scene_P1_1(); if (Running("P1.1")) Tick_P1_1();
            if (Opened("P1.2")) Scene_P1_2(); if (Running("P1.2")) Tick_P1_2();
            if (Opened("P1.3")) Scene_P1_3(); if (Running("P1.3")) Tick_P1_3();
            if (Opened("P2.1")) Scene_P2_1(); if (Running("P2.1")) Tick_P2_1();
            if (Opened("P2.2")) Scene_P2_2(); if (Running("P2.2")) Tick_P2_2();
            if (Opened("P2.3")) Scene_P2_3(); if (Running("P2.3")) Tick_P2_3();
            if (Opened("P2.4")) Scene_P2_4(); if (Running("P2.4")) Tick_P2_4();
            if (Opened("P3.1")) Scene_P3_1(); if (Running("P3.1")) Tick_P3_1();
            if (Opened("P3.2")) Scene_P3_2(); if (Running("P3.2")) Tick_P3_2();
            if (Opened("P3.3")) Scene_P3_3(); if (Running("P3.3")) Tick_P3_3();
        }

        /// <summary>True once, the first frame the quest is no longer locked (its scene gets built).</summary>
        static bool Opened(string id)
        {
            if (Story.Story.StateOf(id) == Story.Story.State.Locked || Story.Story.Flag("hook:" + id)) return false;
            Story.Story.SetFlag("hook:" + id);
            return true;
        }

        static bool Running(string id) => Story.Story.StateOf(id) == Story.Story.State.Active;
    }
}
