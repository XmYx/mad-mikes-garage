using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A crafting station (workbench, stove, oven, furnace, kiln, wash plant, mixer, still, composter, garage):
    /// standing next to it and pressing E opens its recipes. Crafted parts and vehicles appear at the output point.</summary>
    public class CraftingStation : MonoBehaviour, IInteractable
    {
        public static readonly System.Collections.Generic.List<CraftingStation> All = new System.Collections.Generic.List<CraftingStation>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string type = "workbench";
        public string title = "WORKBENCH";
        public float watts;                       // > 0: needs power
        public Vector3 output = new Vector3(0, 1.1f, 0);
        UtilityNode node;

        void Awake() => node = GetComponent<UtilityNode>();
        void Update() { if (node && watts > 0f) node.demand = watts * 0.1f; }     // idle draw; full draw is momentary

        public bool Powered => watts <= 0f || (node && node.Powered);
        public Vector3 OutputPoint => transform.TransformPoint(output);

        public string Prompt(MadMax.Game.WastelandGame g) => Powered ? "[E] " + title : title + ": NO POWER";
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary && Powered) g.Menus.OpenCrafting(this); }
    }
}
