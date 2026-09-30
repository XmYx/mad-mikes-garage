using System.Collections.Generic;
using MadMax.World;

namespace MadMax.Story
{
    // Per-quest hooks: each quest's author fills its own partial methods in its own file (Story/Quests/<id>.cs:
    // Author_<id> adds steps and sets Build.Playable, Cast_<id> adds cast members, Anchors_<id> binds its places),
    // so quests can be written in parallel without touching the same lines. Ids with a dot use '_' (P1.1 → P1_1).

    public static partial class StoryLibrary
    {
        static partial void Author_A5(QuestDef q);
        static partial void Author_A6(QuestDef q);
        static partial void Author_B3(QuestDef q);
        static partial void Author_B4(QuestDef q);
        static partial void Author_B5(QuestDef q);
        static partial void Author_C1(QuestDef q);
        static partial void Author_C2(QuestDef q);
        static partial void Author_C3(QuestDef q);
        static partial void Author_C4(QuestDef q);
        static partial void Author_C5(QuestDef q);
        static partial void Author_F1(QuestDef q);
        static partial void Author_L1(QuestDef q);
        static partial void Author_L2(QuestDef q);
        static partial void Author_L3(QuestDef q);
        static partial void Author_L4(QuestDef q);
        static partial void Author_L5(QuestDef q);
        static partial void Author_S01(QuestDef q);
        static partial void Author_S02(QuestDef q);
        static partial void Author_S03(QuestDef q);
        static partial void Author_S04(QuestDef q);
        static partial void Author_S05(QuestDef q);
        static partial void Author_S06(QuestDef q);
        static partial void Author_S07(QuestDef q);
        static partial void Author_S08(QuestDef q);
        static partial void Author_S09(QuestDef q);
        static partial void Author_S10(QuestDef q);
        static partial void Author_S11(QuestDef q);
        static partial void Author_S12(QuestDef q);
        static partial void Author_S13(QuestDef q);
        static partial void Author_S14(QuestDef q);
        static partial void Author_S15(QuestDef q);
        static partial void Author_S16(QuestDef q);
        static partial void Author_S17(QuestDef q);
        static partial void Author_S18(QuestDef q);
        static partial void Author_S19(QuestDef q);
        static partial void Author_S20(QuestDef q);
        static partial void Author_S21(QuestDef q);
        static partial void Author_S22(QuestDef q);
        static partial void Author_S23(QuestDef q);
        static partial void Author_S24(QuestDef q);
        static partial void Author_P1_1(QuestDef q);
        static partial void Author_P1_2(QuestDef q);
        static partial void Author_P1_3(QuestDef q);
        static partial void Author_P2_1(QuestDef q);
        static partial void Author_P2_2(QuestDef q);
        static partial void Author_P2_3(QuestDef q);
        static partial void Author_P2_4(QuestDef q);
        static partial void Author_P3_1(QuestDef q);
        static partial void Author_P3_2(QuestDef q);
        static partial void Author_P3_3(QuestDef q);

        static void Author(QuestDef q)
        {
            switch (q.id)
            {
                case "A5": Author_A5(q); break;
                case "A6": Author_A6(q); break;
                case "B3": Author_B3(q); break;
                case "B4": Author_B4(q); break;
                case "B5": Author_B5(q); break;
                case "C1": Author_C1(q); break;
                case "C2": Author_C2(q); break;
                case "C3": Author_C3(q); break;
                case "C4": Author_C4(q); break;
                case "C5": Author_C5(q); break;
                case "F1": Author_F1(q); break;
                case "L1": Author_L1(q); break;
                case "L2": Author_L2(q); break;
                case "L3": Author_L3(q); break;
                case "L4": Author_L4(q); break;
                case "L5": Author_L5(q); break;
                case "S01": Author_S01(q); break;
                case "S02": Author_S02(q); break;
                case "S03": Author_S03(q); break;
                case "S04": Author_S04(q); break;
                case "S05": Author_S05(q); break;
                case "S06": Author_S06(q); break;
                case "S07": Author_S07(q); break;
                case "S08": Author_S08(q); break;
                case "S09": Author_S09(q); break;
                case "S10": Author_S10(q); break;
                case "S11": Author_S11(q); break;
                case "S12": Author_S12(q); break;
                case "S13": Author_S13(q); break;
                case "S14": Author_S14(q); break;
                case "S15": Author_S15(q); break;
                case "S16": Author_S16(q); break;
                case "S17": Author_S17(q); break;
                case "S18": Author_S18(q); break;
                case "S19": Author_S19(q); break;
                case "S20": Author_S20(q); break;
                case "S21": Author_S21(q); break;
                case "S22": Author_S22(q); break;
                case "S23": Author_S23(q); break;
                case "S24": Author_S24(q); break;
                case "P1.1": Author_P1_1(q); break;
                case "P1.2": Author_P1_2(q); break;
                case "P1.3": Author_P1_3(q); break;
                case "P2.1": Author_P2_1(q); break;
                case "P2.2": Author_P2_2(q); break;
                case "P2.3": Author_P2_3(q); break;
                case "P2.4": Author_P2_4(q); break;
                case "P3.1": Author_P3_1(q); break;
                case "P3.2": Author_P3_2(q); break;
                case "P3.3": Author_P3_3(q); break;
            }
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_A5(List<Member> into);
        static partial void Cast_A6(List<Member> into);
        static partial void Cast_B3(List<Member> into);
        static partial void Cast_B4(List<Member> into);
        static partial void Cast_B5(List<Member> into);
        static partial void Cast_C1(List<Member> into);
        static partial void Cast_C2(List<Member> into);
        static partial void Cast_C3(List<Member> into);
        static partial void Cast_C4(List<Member> into);
        static partial void Cast_C5(List<Member> into);
        static partial void Cast_F1(List<Member> into);
        static partial void Cast_L1(List<Member> into);
        static partial void Cast_L2(List<Member> into);
        static partial void Cast_L3(List<Member> into);
        static partial void Cast_L4(List<Member> into);
        static partial void Cast_L5(List<Member> into);
        static partial void Cast_S01(List<Member> into);
        static partial void Cast_S02(List<Member> into);
        static partial void Cast_S03(List<Member> into);
        static partial void Cast_S04(List<Member> into);
        static partial void Cast_S05(List<Member> into);
        static partial void Cast_S06(List<Member> into);
        static partial void Cast_S07(List<Member> into);
        static partial void Cast_S08(List<Member> into);
        static partial void Cast_S09(List<Member> into);
        static partial void Cast_S10(List<Member> into);
        static partial void Cast_S11(List<Member> into);
        static partial void Cast_S12(List<Member> into);
        static partial void Cast_S13(List<Member> into);
        static partial void Cast_S14(List<Member> into);
        static partial void Cast_S15(List<Member> into);
        static partial void Cast_S16(List<Member> into);
        static partial void Cast_S17(List<Member> into);
        static partial void Cast_S18(List<Member> into);
        static partial void Cast_S19(List<Member> into);
        static partial void Cast_S20(List<Member> into);
        static partial void Cast_S21(List<Member> into);
        static partial void Cast_S22(List<Member> into);
        static partial void Cast_S23(List<Member> into);
        static partial void Cast_S24(List<Member> into);
        static partial void Cast_P1_1(List<Member> into);
        static partial void Cast_P1_2(List<Member> into);
        static partial void Cast_P1_3(List<Member> into);
        static partial void Cast_P2_1(List<Member> into);
        static partial void Cast_P2_2(List<Member> into);
        static partial void Cast_P2_3(List<Member> into);
        static partial void Cast_P2_4(List<Member> into);
        static partial void Cast_P3_1(List<Member> into);
        static partial void Cast_P3_2(List<Member> into);
        static partial void Cast_P3_3(List<Member> into);

        static void CastExtra(List<Member> into) { Cast_A5(into); Cast_A6(into); Cast_B3(into); Cast_B4(into); Cast_B5(into); Cast_C1(into); Cast_C2(into); Cast_C3(into); Cast_C4(into); Cast_C5(into); Cast_F1(into); Cast_L1(into); Cast_L2(into); Cast_L3(into); Cast_L4(into); Cast_L5(into); Cast_S01(into); Cast_S02(into); Cast_S03(into); Cast_S04(into); Cast_S05(into); Cast_S06(into); Cast_S07(into); Cast_S08(into); Cast_S09(into); Cast_S10(into); Cast_S11(into); Cast_S12(into); Cast_S13(into); Cast_S14(into); Cast_S15(into); Cast_S16(into); Cast_S17(into); Cast_S18(into); Cast_S19(into); Cast_S20(into); Cast_S21(into); Cast_S22(into); Cast_S23(into); Cast_S24(into); Cast_P1_1(into); Cast_P1_2(into); Cast_P1_3(into); Cast_P2_1(into); Cast_P2_2(into); Cast_P2_3(into); Cast_P2_4(into); Cast_P3_1(into); Cast_P3_2(into); Cast_P3_3(into); }
    }

    public static partial class StoryAnchors
    {
        static partial void Anchors_A5(WorldGen world, Settlement town);
        static partial void Anchors_A6(WorldGen world, Settlement town);
        static partial void Anchors_B3(WorldGen world, Settlement town);
        static partial void Anchors_B4(WorldGen world, Settlement town);
        static partial void Anchors_B5(WorldGen world, Settlement town);
        static partial void Anchors_C1(WorldGen world, Settlement town);
        static partial void Anchors_C2(WorldGen world, Settlement town);
        static partial void Anchors_C3(WorldGen world, Settlement town);
        static partial void Anchors_C4(WorldGen world, Settlement town);
        static partial void Anchors_C5(WorldGen world, Settlement town);
        static partial void Anchors_F1(WorldGen world, Settlement town);
        static partial void Anchors_L1(WorldGen world, Settlement town);
        static partial void Anchors_L2(WorldGen world, Settlement town);
        static partial void Anchors_L3(WorldGen world, Settlement town);
        static partial void Anchors_L4(WorldGen world, Settlement town);
        static partial void Anchors_L5(WorldGen world, Settlement town);
        static partial void Anchors_S01(WorldGen world, Settlement town);
        static partial void Anchors_S02(WorldGen world, Settlement town);
        static partial void Anchors_S03(WorldGen world, Settlement town);
        static partial void Anchors_S04(WorldGen world, Settlement town);
        static partial void Anchors_S05(WorldGen world, Settlement town);
        static partial void Anchors_S06(WorldGen world, Settlement town);
        static partial void Anchors_S07(WorldGen world, Settlement town);
        static partial void Anchors_S08(WorldGen world, Settlement town);
        static partial void Anchors_S09(WorldGen world, Settlement town);
        static partial void Anchors_S10(WorldGen world, Settlement town);
        static partial void Anchors_S11(WorldGen world, Settlement town);
        static partial void Anchors_S12(WorldGen world, Settlement town);
        static partial void Anchors_S13(WorldGen world, Settlement town);
        static partial void Anchors_S14(WorldGen world, Settlement town);
        static partial void Anchors_S15(WorldGen world, Settlement town);
        static partial void Anchors_S16(WorldGen world, Settlement town);
        static partial void Anchors_S17(WorldGen world, Settlement town);
        static partial void Anchors_S18(WorldGen world, Settlement town);
        static partial void Anchors_S19(WorldGen world, Settlement town);
        static partial void Anchors_S20(WorldGen world, Settlement town);
        static partial void Anchors_S21(WorldGen world, Settlement town);
        static partial void Anchors_S22(WorldGen world, Settlement town);
        static partial void Anchors_S23(WorldGen world, Settlement town);
        static partial void Anchors_S24(WorldGen world, Settlement town);
        static partial void Anchors_P1_1(WorldGen world, Settlement town);
        static partial void Anchors_P1_2(WorldGen world, Settlement town);
        static partial void Anchors_P1_3(WorldGen world, Settlement town);
        static partial void Anchors_P2_1(WorldGen world, Settlement town);
        static partial void Anchors_P2_2(WorldGen world, Settlement town);
        static partial void Anchors_P2_3(WorldGen world, Settlement town);
        static partial void Anchors_P2_4(WorldGen world, Settlement town);
        static partial void Anchors_P3_1(WorldGen world, Settlement town);
        static partial void Anchors_P3_2(WorldGen world, Settlement town);
        static partial void Anchors_P3_3(WorldGen world, Settlement town);

        static void AnchorsExtra(WorldGen world, Settlement town) { Anchors_A5(world, town); Anchors_A6(world, town); Anchors_B3(world, town); Anchors_B4(world, town); Anchors_B5(world, town); Anchors_C1(world, town); Anchors_C2(world, town); Anchors_C3(world, town); Anchors_C4(world, town); Anchors_C5(world, town); Anchors_F1(world, town); Anchors_L1(world, town); Anchors_L2(world, town); Anchors_L3(world, town); Anchors_L4(world, town); Anchors_L5(world, town); Anchors_S01(world, town); Anchors_S02(world, town); Anchors_S03(world, town); Anchors_S04(world, town); Anchors_S05(world, town); Anchors_S06(world, town); Anchors_S07(world, town); Anchors_S08(world, town); Anchors_S09(world, town); Anchors_S10(world, town); Anchors_S11(world, town); Anchors_S12(world, town); Anchors_S13(world, town); Anchors_S14(world, town); Anchors_S15(world, town); Anchors_S16(world, town); Anchors_S17(world, town); Anchors_S18(world, town); Anchors_S19(world, town); Anchors_S20(world, town); Anchors_S21(world, town); Anchors_S22(world, town); Anchors_S23(world, town); Anchors_S24(world, town); Anchors_P1_1(world, town); Anchors_P1_2(world, town); Anchors_P1_3(world, town); Anchors_P2_1(world, town); Anchors_P2_2(world, town); Anchors_P2_3(world, town); Anchors_P2_4(world, town); Anchors_P3_1(world, town); Anchors_P3_2(world, town); Anchors_P3_3(world, town); }
    }
}
