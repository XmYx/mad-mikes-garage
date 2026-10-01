namespace MadMax.Game
{
    /// <summary>Hooks for the parallel depth blocks (Items, Anim, Roads, Metal, Husbandry, Utilities, MedMine, Defence):
    /// each block implements its partial methods in its own file (WastelandGame.&lt;Block&gt;.cs) and keeps its saved
    /// state in its own <c>SaveData.block&lt;Block&gt;</c> list, so the blocks never edit the same lines.</summary>
    public partial class WastelandGame
    {
        partial void ItemsUpdate();
        partial void ItemsSave(SaveData d);
        partial void ItemsLoad(SaveData d);
        partial void ItemsNewGame();
        partial void AnimUpdate();
        partial void AnimSave(SaveData d);
        partial void AnimLoad(SaveData d);
        partial void AnimNewGame();
        partial void RoadsUpdate();
        partial void RoadsSave(SaveData d);
        partial void RoadsLoad(SaveData d);
        partial void RoadsNewGame();
        partial void MetalUpdate();
        partial void MetalSave(SaveData d);
        partial void MetalLoad(SaveData d);
        partial void MetalNewGame();
        partial void HusbandryUpdate();
        partial void HusbandrySave(SaveData d);
        partial void HusbandryLoad(SaveData d);
        partial void HusbandryNewGame();
        partial void UtilitiesUpdate();
        partial void UtilitiesSave(SaveData d);
        partial void UtilitiesLoad(SaveData d);
        partial void UtilitiesNewGame();
        partial void MedMineUpdate();
        partial void MedMineSave(SaveData d);
        partial void MedMineLoad(SaveData d);
        partial void MedMineNewGame();
        partial void DefenceUpdate();
        partial void DefenceSave(SaveData d);
        partial void DefenceLoad(SaveData d);
        partial void DefenceNewGame();
        partial void LightsUpdate();
        partial void LightsSave(SaveData d);
        partial void LightsLoad(SaveData d);
        partial void LightsNewGame();

        void BlocksUpdate() { ItemsUpdate(); AnimUpdate(); RoadsUpdate(); MetalUpdate(); HusbandryUpdate(); UtilitiesUpdate(); MedMineUpdate(); DefenceUpdate(); LightsUpdate(); }
        void BlocksSave(SaveData d) { ItemsSave(d); AnimSave(d); RoadsSave(d); MetalSave(d); HusbandrySave(d); UtilitiesSave(d); MedMineSave(d); DefenceSave(d); LightsSave(d); }
        void BlocksLoad(SaveData d) { ItemsLoad(d); AnimLoad(d); RoadsLoad(d); MetalLoad(d); HusbandryLoad(d); UtilitiesLoad(d); MedMineLoad(d); DefenceLoad(d); LightsLoad(d); }
        void BlocksNewGame() { ItemsNewGame(); AnimNewGame(); RoadsNewGame(); MetalNewGame(); HusbandryNewGame(); UtilitiesNewGame(); MedMineNewGame(); DefenceNewGame(); LightsNewGame(); }
    }
}
