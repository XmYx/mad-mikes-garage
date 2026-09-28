using MadMax.Audio;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A placed battery radio: [E] power, [T] next station; while looking at it , . tune and [ ] set the volume.</summary>
    [RequireComponent(typeof(RadioReceiver))]
    public class RadioSet : MonoBehaviour, IPlaceState, IInteractable
    {
        RadioReceiver rx;
        RadioReceiver Rx => rx ? rx : rx = GetComponent<RadioReceiver>();

        void Awake() { Rx.range = 22f; Rx.lowPass = 5000f; Rx.speaker = new Vector3(0f, 0.25f, 0.1f); }

        public string Prompt(MadMax.Game.WastelandGame g) =>
            Rx.on ? Rx.StationLabel() + "  [E] OFF  [T] TUNE  , . [ ] VOL" : "[E] RADIO ON  [T] TUNE";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) Rx.Tune(1); else Rx.TogglePower();
        }

        public string SaveState() => Rx.SaveState();
        public void LoadState(string s) => Rx.LoadState(s);
    }
}
