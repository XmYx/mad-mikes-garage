using UnityEngine;

namespace MadMax.World
{
    /// <summary>A level crossing: posts with cross boards and lamps either side of the line, barrier arms across the
    /// road. A train within ~150 m brings the arms down, sets the lamps flashing and the bell ringing.</summary>
    public class RailCrossing : MonoBehaviour
    {
        Transform armA, armB;
        GameObject[] lamps = new GameObject[4];
        float lowered, check;
        bool active;

        public static RailCrossing Create(RailNetwork net, RailLine l, float s, float span, Material mat, Material glow, Transform parent)
        {
            var p = l.At(s, out var t); t.y = 0f; t.Normalize();
            var n = Vector3.Cross(Vector3.up, t);
            var go = new GameObject("LevelCrossing");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(t, Vector3.up));
            var c = go.AddComponent<RailCrossing>();
            float off = 3.6f, along = Mathf.Max(4.5f, span * 0.5f + 1f);
            for (int i = 0; i < 2; i++)
            {
                float sg = i == 0 ? 1f : -1f;
                var local = new Vector3(sg * off, 0f, sg * along);
                var post = new GameObject("Post", typeof(MeshFilter), typeof(MeshRenderer));
                post.transform.SetParent(go.transform, false);
                post.transform.localPosition = local;
                post.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 90f : -90f, 0f);
                post.GetComponent<MeshFilter>().sharedMesh = RailProps.Post;
                post.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var bc = post.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 1.6f, 0f); bc.size = new Vector3(0.3f, 3.2f, 0.3f);
                var pivot = new GameObject("Arm", typeof(MeshFilter), typeof(MeshRenderer));
                pivot.transform.SetParent(go.transform, false);
                pivot.transform.localPosition = local + Vector3.up * 0.95f;
                pivot.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, -sg), Vector3.up) * Quaternion.Euler(-85f, 0f, 0f);
                pivot.GetComponent<MeshFilter>().sharedMesh = RailProps.Arm;
                pivot.GetComponent<MeshRenderer>().sharedMaterial = mat;
                if (i == 0) c.armA = pivot.transform; else c.armB = pivot.transform;
                for (int k = 0; k < 2; k++)
                {
                    var lamp = new GameObject("Lamp", typeof(MeshFilter), typeof(MeshRenderer));
                    lamp.transform.SetParent(post.transform, false);
                    lamp.transform.localPosition = new Vector3(k == 0 ? -0.3f : 0.3f, 2.25f, 0.15f);
                    lamp.GetComponent<MeshFilter>().sharedMesh = RailProps.LampLens;
                    lamp.GetComponent<MeshRenderer>().sharedMaterial = glow;
                    lamp.SetActive(false);
                    c.lamps[i * 2 + k] = lamp;
                }
            }
            return c;
        }

        void Update()
        {
            if (Time.time >= check)
            {
                check = Time.time + 0.3f;
                active = false;
                var p = transform.position;
                foreach (var t in Train.All)
                {
                    if (Mathf.Abs(t.speed) < 0.3f) continue;
                    foreach (var car in t.cars) if (car && (car.transform.position - p).sqrMagnitude < 150f * 150f) { active = true; break; }
                    if (active) break;
                }
            }
            lowered = Mathf.MoveTowards(lowered, active ? 1f : 0f, Time.deltaTime * 0.4f);
            float a = Mathf.Lerp(-85f, 0f, lowered);
            armA.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Euler(a, 0f, 0f);
            armB.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.Euler(a, 0f, 0f);
            bool flash = active && (Time.time * 1.6f) % 1f > 0.5f;
            for (int i = 0; i < lamps.Length; i++) lamps[i].SetActive(active && ((i & 1) == 0) == flash);
            if (active) MadMax.Audio.Sfx.Play("bell", transform.position + Vector3.up * 2.5f, 0.35f, 1.6f, 70f, 0.75f);
        }
    }
}
