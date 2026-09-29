using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>World ambience around the listener: wind always, rain while it rains (muffled indoors or in a cab), thunder in storms.</summary>
    public class AmbientAudio : MonoBehaviour
    {
        float rain, crickets, nextThunder = 20f;

        void Update()
        {
            if (Application.isBatchMode) return;
            var g = MadMax.Game.WastelandGame.Instance;
            bool sheltered = g && (g.Current || (g.Player && g.Player.Interior));
            bool raining = MadMax.World.Weather.Raining && !MadMax.World.Weather.Snowing;
            rain = Mathf.MoveTowards(rain, raining ? 1f : 0f, Time.deltaTime * 0.3f);
            float amb = MadMax.Game.GameSettings.Current.ambientVolume;
            Sfx.Loop(this, "rain", rain * (sheltered ? 0.35f : 0.6f) * amb, 1f, 30f, true);
            // crickets on warm dry nights (not in winter, not in the rain, not deep underground)
            float night = MadMax.World.DayNight.Darkness;
            bool insects = night > 0.5f && !MadMax.World.Weather.Raining && MadMax.World.Weather.Temperature > 9f && !(g && g.Current && g.Current.GetComponent<MadMax.Vehicles.VehicleClimate>() is MadMax.Vehicles.VehicleClimate vc && vc.Enclosed);
            crickets = Mathf.MoveTowards(crickets, insects ? 1f : 0f, Time.deltaTime * 0.2f);
            Sfx.Loop(this, "insects", crickets * 0.22f * amb, 1f, 30f, true);
            Sfx.Loop(this, "wind", Mathf.Clamp(0.05f + MadMax.World.WindDust.Strength * 0.04f + MadMax.World.WindDust.Gust * 0.08f, 0.05f, 0.45f) * amb, 0.8f + MadMax.World.WindDust.Gust * 0.25f, 30f, true);
            var net = MadMax.Net.NetSession.Instance;
            if (raining && Time.time > nextThunder && !(net && net.IsClient))                   // clients get the host's strikes
            {
                nextThunder = Time.time + Random.Range(25f, 70f);
                net?.SendStrikes();
                var cam = Camera.main;
                MadMax.World.Atmosphere.Lightning();                                         // the flash comes first
                if (cam) Sfx.Play("thunder", cam.transform.position + Random.onUnitSphere * 20f, Random.Range(0.4f, 0.8f), Random.Range(0.85f, 1.05f), 200f, 5f);
            }
        }

    }
}
