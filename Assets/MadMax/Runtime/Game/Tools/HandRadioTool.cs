using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Handheld radio: held, it is a small <see cref="MadMax.Audio.RadioReceiver"/> (use or the radio power key
    /// switches it, the tune / volume keys work as in a car). Its station and power survive putting it away. In the pack
    /// it still picks up the home frequency (<see cref="WastelandGame.HandRadioCalls"/>).</summary>
    public class HandRadioTool : HandTool
    {
        static bool wasOn; static int lastStation; static float lastVolume = 0.6f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { wasOn = false; lastStation = 0; lastVolume = 0.6f; }

        MadMax.Audio.RadioReceiver rx;
        public MadMax.Audio.RadioReceiver Receiver => rx;

        void Awake()
        {
            rx = gameObject.AddComponent<MadMax.Audio.RadioReceiver>();
            rx.range = 9f; rx.lowPass = 3200f; rx.speaker = Vector3.zero;
            rx.station = lastStation; rx.volume = lastVolume; rx.on = wasOn;
        }

        void OnDestroy() { if (rx) { wasOn = rx.on; lastStation = rx.station; lastVolume = rx.volume; } }

        public override void Strike(PlayerCharacter user) { if (rx) rx.TogglePower(); }
    }
}
