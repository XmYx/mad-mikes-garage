using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>World ambience around the listener: wind always, rain while it rains (muffled indoors or in a cab), thunder in storms.</summary>
    public class AmbientAudio : MonoBehaviour
    {
        float rain, nextThunder = 20f;

        void Update()
        {
            if (Application.isBatchMode) return;
            var g = MadMax.Game.WastelandGame.Instance;
            bool sheltered = g && (g.Current || (g.Player && g.Player.Interior));
            bool raining = MadMax.World.Weather.Raining && !MadMax.World.Weather.Snowing;
            rain = Mathf.MoveTowards(rain, raining ? 1f : 0f, Time.deltaTime * 0.3f);
            Sfx.Loop(this, "rain", rain * (sheltered ? 0.35f : 0.6f), 1f, 30f, true);
            Sfx.Loop(this, "wind", MadMax.World.Weather.Raining ? 0.3f : 0.15f, 0.9f, 30f, true);
            if (raining && Time.time > nextThunder)
            {
                nextThunder = Time.time + Random.Range(25f, 70f);
                var cam = Camera.main;
                if (cam) Sfx.Play("thunder", cam.transform.position + Random.onUnitSphere * 20f, Random.Range(0.4f, 0.8f), Random.Range(0.85f, 1.05f), 200f, 5f);
            }
        }

    }
}
