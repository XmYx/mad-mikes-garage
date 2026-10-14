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
        partial void ContextSave(SaveData d);
        partial void ContextLoad(SaveData d);
        partial void ContextNewGame();
        partial void FluidsUpdate();
        partial void FluidsSave(SaveData d);
        partial void FluidsLoad(SaveData d);
        partial void FluidsNewGame();
        partial void BagsUpdate();
        partial void BagsSave(SaveData d);
        partial void BagsLoad(SaveData d);
        partial void BagsNewGame();

        void BlocksUpdate() { UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.ItemsUpdate"); ItemsUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.AnimUpdate"); AnimUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.RoadsUpdate"); RoadsUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.MetalUpdate"); MetalUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.HusbandryUpdate"); HusbandryUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.UtilitiesUpdate"); UtilitiesUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.MedMineUpdate"); MedMineUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.DefenceUpdate"); DefenceUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.LightsUpdate"); LightsUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.FluidsUpdate"); FluidsUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.BagsUpdate"); BagsUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.UpdateTrail"); UpdateTrail(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.SalvageUpdate"); SalvageUpdate(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.UpdateFireScenes"); UpdateFireScenes(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.UpdateLeaseReminders"); UpdateLeaseReminders(); UnityEngine.Profiling.Profiler.EndSample(); UnityEngine.Profiling.Profiler.BeginSample("MadMax.Block.RailUpdate"); RailUpdate(); UnityEngine.Profiling.Profiler.EndSample(); }
        partial void SalvageUpdate();
        partial void SalvageSave(SaveData d);
        partial void SalvageLoad(SaveData d);
        partial void SalvageNewGame();
        partial void CitySave(SaveData d);
        partial void CityLoad(SaveData d);
        partial void CityNewGame();

        void BlocksSave(SaveData d) { CitySave(d); ItemsSave(d); AnimSave(d); RoadsSave(d); MetalSave(d); HusbandrySave(d); UtilitiesSave(d); MedMineSave(d); DefenceSave(d); LightsSave(d); ContextSave(d); FluidsSave(d); BagsSave(d); SalvageSave(d); RailSave(d); }
        void BlocksLoad(SaveData d) { CityLoad(d); ItemsLoad(d); AnimLoad(d); RoadsLoad(d); MetalLoad(d); HusbandryLoad(d); UtilitiesLoad(d); MedMineLoad(d); DefenceLoad(d); LightsLoad(d); ContextLoad(d); FluidsLoad(d); BagsLoad(d); SalvageLoad(d); RailLoad(d); }
        void BlocksNewGame() { CityNewGame(); ItemsNewGame(); AnimNewGame(); RoadsNewGame(); MetalNewGame(); HusbandryNewGame(); UtilitiesNewGame(); MedMineNewGame(); DefenceNewGame(); LightsNewGame(); ContextNewGame(); FluidsNewGame(); BagsNewGame(); SalvageNewGame(); RailNewGame(); }
    }
}
