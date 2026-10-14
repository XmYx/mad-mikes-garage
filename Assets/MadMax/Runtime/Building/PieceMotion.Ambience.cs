using MadMax.Rendering;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Ambience of a piece: fires (flicker light, embers, chimney smoke, crackle), a kettle steaming on a lit
    /// stove, candles and oil lamps flickering, a radio's dial glow, windows lit at night when a lamp burns inside.</summary>
    public partial class PieceMotion
    {
        Light fireLight, flickerLight, dialGlow;
        PoweredLight lamp;
        float fireBase, flickerBase, emberHeat, smokeT, steamT, sparkT, windowT;
        bool fireCapable, hasKettle, windowLit;
        Transform chimney, kettle, flamePoint;
        Vector3 kettleLocal;

        /// <summary>Its fire burns right now (wood stove or fireplace, a cooking or smelting fire at work, a steam
        /// generator's firebox).</summary>
        public bool FireLit => (climate && climate.burnsWood && climate.Active)
                               || (stationFire && station.Busy && station.Powered)
                               || (gen && gen.solid && gen.on && gen.fuel > 0f);

        /// <summary>Its flames show: the fire burns, or (candles, oil lamps, lanterns) the lamp is lit; a piece with
        /// neither always burns (a brazier).</summary>
        public bool FlameLit => fireCapable ? FireLit : lamp ? lamp.Glowing : true;

        /// <summary>Ember glow 0..1: up while the fire burns, fading for a minute and a half after.</summary>
        public float EmberHeat => emberHeat;

        void AmbienceStart()
        {
            fireCapable = (climate && climate.burnsWood) || stationFire || (gen && gen.solid);
            chimney = FindNamed("Chimney");
            kettle = FindNamed("Kettle"); if (!kettle) kettle = FindNamed("Pot"); if (!kettle) kettle = FindNamed("Pan");
            flamePoint = FindNamed("Flame");
            if (!kettle && piece && piece.id == "stove") { kettleLocal = new Vector3(-0.16f, 0.95f, 0f); hasKettle = true; }   // the voxel stove's pot
            else hasKettle = kettle;
            var pl = lamp = GetComponent<PoweredLight>();
            Light own = null;
            foreach (var l in GetComponentsInChildren<Light>(true)) if (l.name == "Light") { own = l; break; }
            if (fireCapable)
            {
                fireLight = own;
                if (!fireLight)
                {
                    fireLight = new GameObject("FireLight").AddComponent<Light>();
                    fireLight.transform.SetParent(transform, false);
                    fireLight.transform.position = FirePoint();
                    fireLight.type = LightType.Point; fireLight.range = 4f; fireLight.intensity = 1.8f; fireLight.shadows = LightShadows.None;
                    fireLight.color = new Color(1f, 0.52f, 0.22f);
                    fireLight.enabled = false;
                }
                fireBase = fireLight.intensity;
            }
            else if (pl && !pl.needsPower && own) { flickerLight = own; flickerBase = own.intensity; }       // candles, oil lamps, lanterns
            if (hd && hd.root && hd.HasLamps)
            {
                if (radio) hd.LampState = () => radio && radio.on;
                else if (!pl) hd.LampState = () => windowLit;                                                     // windows: lit when a lamp burns inside
            }
            else if (radio)
            {
                dialGlow = new GameObject("DialGlow").AddComponent<Light>();
                dialGlow.transform.SetParent(transform, false);
                dialGlow.transform.localPosition = new Vector3(0f, 0.3f, 0.24f);
                dialGlow.type = LightType.Point; dialGlow.range = 1.3f; dialGlow.intensity = 0.55f; dialGlow.shadows = LightShadows.None;
                dialGlow.color = new Color(1f, 0.66f, 0.3f);
                dialGlow.enabled = false;
            }
        }

        Transform FindNamed(string n)
        {
            if (!hd || !hd.root) return null;
            var t = HDAssets.FindDeep(hd.root, n);
            return t;
        }

        Vector3 FirePoint()
        {
            if (flamePoint) return flamePoint.position;
            if (fireLight) return fireLight.transform.position;
            var r = GetComponent<Renderer>();
            if (!r) return transform.position + transform.up * 0.4f;
            var b = r.bounds;
            return new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, 0.3f), b.center.z) + transform.forward * b.extents.z * 0.6f;
        }

        Vector3 ChimneyPoint()
        {
            if (chimney)
            {
                var r = chimney.GetComponentInChildren<Renderer>();
                return r ? new Vector3(r.bounds.center.x, r.bounds.max.y + 0.05f, r.bounds.center.z) : chimney.position;
            }
            var own = GetComponent<Renderer>();
            if (!own) return transform.position + Vector3.up * 1.5f;
            var b = own.bounds;
            return new Vector3(b.center.x, b.max.y + 0.1f, b.center.z);
        }

        void AmbienceTick()
        {
            float dt = Time.deltaTime;
            bool lit = fireCapable && FireLit;
            if (fireCapable) emberHeat = lit ? Mathf.MoveTowards(emberHeat, 1f, dt * 0.5f) : Mathf.MoveTowards(emberHeat, 0f, dt / 90f);
            if (radio && dialGlow) dialGlow.enabled = radio.on && DayNight.Darkness > 0.2f && Near && LightBudget.Allowed(dialGlow);
            if ((windowT -= dt) <= 0f && hd && hd.HasLamps && !radio) { windowT = 1f + Random.value * 0.3f; windowLit = LampInside(); }
            if (!fireCapable && !hasKettle) return;
            float d2 = (transform.position - Focus).sqrMagnitude;
            if (fireCapable)
            {
                bool working = station && station.Busy;
                if (!working) MadMax.Audio.Sfx.Loop(this, "fire", lit && d2 < 18f * 18f ? 0.28f : 0f, 1f, 12f);   // a station at work has its own loop
            }
            if (d2 > 28f * 28f) return;
            if (lit)
            {
                // chimney smoke for fires without their own (wood stoves and fireplaces smoke through Climate, generators at the exhaust)
                if (!(climate && climate.burnsWood) && !gen && (smokeT -= dt) <= 0f)
                {
                    smokeT = Random.Range(0.6f, 1.1f);
                    Fx.Smoke(ChimneyPoint(), Vector3.up * 0.9f + Fx.Wind * 0.3f, 0.42f, new Color(0.32f, 0.31f, 0.3f, 0.4f), 3.2f);
                }
                // embers for looks without flame meshes
                if (!flamePoint && (sparkT -= dt) <= 0f && d2 < 20f * 20f)
                {
                    sparkT = Random.Range(0.7f, 2f);
                    Fx.Sparks(FirePoint(), Vector3.up * 0.6f, Random.Range(1, 3), Pal.Amber);
                }
            }
            bool cooking = lit || (station && station.Busy && station.Powered && (station.type == "range" || station.type == "stove" || station.type == "campfire"));
            if (hasKettle && cooking && (steamT -= dt) <= 0f && d2 < 20f * 20f)
            {
                steamT = Random.Range(0.22f, 0.4f);
                var at = kettle ? KettleTop() : transform.TransformPoint(kettleLocal);
                Color steam = Pal.Steam; steam.a = 0.24f;
                Fx.Smoke(at, Vector3.up * 0.35f + Fx.Wind * 0.1f, 0.09f, steam, 1.3f);
            }
        }

        Vector3 KettleTop()
        {
            var r = kettle.GetComponentInChildren<Renderer>();
            return r ? new Vector3(r.bounds.center.x, r.bounds.max.y + 0.03f, r.bounds.center.z) : kettle.position;
        }

        /// <summary>A lamp burns within 8 m (or a fire) after dark: the windows of this piece glow.</summary>
        bool LampInside()
        {
            if (DayNight.Darkness < 0.35f) return false;
            var p = transform.position;
            foreach (var l in PoweredLight.All) if (l && l.Glowing && (l.transform.position - p).sqrMagnitude < 64f) return true;
            foreach (var c in Climate.All) if (c && c.Active && c.heat > 0f && (c.transform.position - p).sqrMagnitude < 64f) return true;
            return false;
        }

        /// <summary>Lights after their owners' Update: a fire's light follows the fire (also by day), flickering; candles
        /// and oil lamps flicker softly.</summary>
        void LateUpdate()
        {
            float time = Time.time;
            if (fireLight)
            {
                bool on = FireLit || emberHeat > 0.25f;
                if (!on) { if (fireLight.enabled) fireLight.enabled = false; }
                else
                {
                    if (!fireLight.enabled) fireLight.enabled = LightBudget.Allowed(fireLight);
                    if (fireLight.enabled)
                    {
                        float n = Mathf.PerlinNoise(time * 8f, phase) * 0.6f + Mathf.PerlinNoise(time * 21f, phase + 3f) * 0.4f;
                        fireLight.intensity = FireLit ? fireBase * (0.7f + 0.6f * n) : fireBase * emberHeat * 0.45f * (0.8f + 0.4f * n);
                    }
                }
            }
            else if (flickerLight && flickerLight.enabled)
                flickerLight.intensity = flickerBase * (0.86f + 0.22f * Mathf.PerlinNoise(time * 5.5f, phase));
        }
    }
}
