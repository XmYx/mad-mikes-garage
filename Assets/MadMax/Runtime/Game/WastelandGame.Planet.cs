using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    public partial class WastelandGame
    {
        static readonly int CurveId = Shader.PropertyToID("_MadMaxCurve"), SnowLatId = Shader.PropertyToID("_MadMaxSnowLat");
        float snowLatAt;

        /// <summary>The planet around the player (user additions): the local weather's latitude, the shaders' horizon
        /// curve and polar snow line, and the date line — past x = ±<see cref="WorldGen.HalfX"/> (open sea) the driven
        /// vehicle (with what it tows) or the swimmer is carried round to the other side of the world.</summary>
        void UpdatePlanet()
        {
            var f = FocusPos;
            Weather.FocusZ = f.z;
            Shader.SetGlobalFloat(CurveId, GameSettings.Current.flatWorld ? 0f : 0.5f / WorldGen.CurveRadius);
            if (Time.time > snowLatAt)
            {
                // where lasting snow begins north of the equator (the shader mirrors it south)
                snowLatAt = Time.time + 3f;
                float start = WorldGen.PoleSpan;
                for (float d = 0f; d < WorldGen.PoleSpan; d += 40f)
                    if (Weather.TemperatureAt(WorldGen.ZEquator + d) < -2f) { start = d; break; }
                Shader.SetGlobalVector(SnowLatId, new Vector4(WorldGen.ZEquator, start, 260f, 0f));
            }
            if (Mathf.Abs(f.x) > WorldGen.HalfX + 2f) CrossDateLine(f.x > 0f ? -WorldGen.Circumference : WorldGen.Circumference);
        }

        void CrossDateLine(float dx)
        {
            var d = new Vector3(dx, 0f, 0f);
            if (Current)
            {
                MoveBody(Current.transform, Current.Body, d);
                foreach (var v in AllVehicles)                                                    // what it tows comes along
                    if (v && v != Current && v.TryGetComponent<MadMax.Vehicles.TowCoupling>(out var tc) && tc.Tower == Current) MoveBody(v.transform, v.Body, d);
            }
            else if (Player)
            {
                var cc = Player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                Player.transform.position += d;
                if (cc) cc.enabled = true;
            }
            Physics.SyncTransforms();
            if (cameraRig) cameraRig.SetTarget(Current ? Current.transform : Player.transform);
            ScreenFader.Flash(new Color(0.02f, 0.03f, 0.06f), 0.35f);
            Toast("YOU CROSSED THE DATE LINE: " + (dx < 0f ? "WEST" : "EAST") + " IS BEHIND YOU NOW");
            Journal.Add("TRAVEL", "CROSSED THE DATE LINE ON DAY " + DayNight.Day + ". THE WORLD REALLY IS ROUND.");
        }

        static void MoveBody(Transform t, Rigidbody rb, Vector3 d)
        {
            t.position += d;
            if (rb) rb.position += d;
        }
    }
}
