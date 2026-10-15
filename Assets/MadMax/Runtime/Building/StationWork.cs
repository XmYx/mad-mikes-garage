using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The piece in work on a bench (2026-10-15): while a station of a hand-work type runs a job that makes an
    /// item, that item's world model sits at the station's output point — growing from a rough 70 % to full size with the
    /// job's progress, turned a little now and then as if worked by hand, nudged on each tool tap. Hidden when idle.</summary>
    public class StationWork : MonoBehaviour
    {
        Transform piece;
        MeshFilter mf;
        MeshRenderer mr;
        string shown;
        float baseScale = 1f, turnFrom, turnTo, turnT = 1f, nextTurn, tap;
        Quaternion lie = Quaternion.identity;
        /// <summary>The item on the bench now (tests).</summary>
        public string Showing => piece && piece.gameObject.activeSelf ? shown : null;

        /// <summary>Station types whose work is done by hand on a surface (the item shows while it is made).</summary>
        public static bool HandWork(string type) => type == "workbench" || type == "garage" || type == "gunsmith" || type == "sewing" || type == "leather"
                                                    || type == "tanning" || type == "forge" || type == "machine_shop" || type == "spinning" || type == "loom";

        public void Show(Recipe r, float progress, Vector3 at)
        {
            if (r == null || r.kind != OutputKind.Item || string.IsNullOrEmpty(r.output)) { Hide(); return; }
            var game = MadMax.Game.WastelandGame.Instance;
            if (!game) return;
            if (!piece)
            {
                var go = new GameObject("WorkPiece", typeof(MeshFilter), typeof(MeshRenderer));
                piece = go.transform;
                piece.SetParent(transform, false);
                mf = go.GetComponent<MeshFilter>(); mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (shown != r.output || !mf.sharedMesh)
            {
                shown = r.output;
                var mesh = WorldItemModels.For(r.output, out baseScale, out lie);
                mf.sharedMesh = mesh;
                mr.sharedMaterial = game.propMaterial;
                if (!mesh) { Hide(); return; }
            }
            if (!piece.gameObject.activeSelf) piece.gameObject.SetActive(true);
            float t = Time.time;
            if (t > nextTurn) { nextTurn = t + Random.Range(1.5f, 4f); turnFrom = turnTo; turnTo = Random.Range(-35f, 35f); turnT = 0f; }
            turnT = Mathf.Min(1f, turnT + Time.deltaTime * 2.5f);
            tap = Mathf.MoveTowards(tap, 0f, Time.deltaTime * 6f);
            float yaw = Mathf.LerpAngle(turnFrom, turnTo, Mathf.SmoothStep(0f, 1f, turnT));
            float s = baseScale * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(progress)) * (1f - 0.06f * tap);
            piece.position = at + Vector3.up * (0.01f + 0.015f * tap);
            piece.rotation = transform.rotation * Quaternion.Euler(0f, yaw, 0f) * lie;
            piece.localScale = Vector3.one * s;
            piece.position += Vector3.up * (at.y - mr.bounds.min.y);                               // rest the lowest point on the surface
        }

        /// <summary>A tool tap (from the station's work beat).</summary>
        public void Tap() => tap = 1f;

        public void Hide() { if (piece && piece.gameObject.activeSelf) piece.gameObject.SetActive(false); }
    }
}
