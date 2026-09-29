using UnityEngine;

namespace MadMax.World
{
    /// <summary>A bat roost under a cave roof (roadmap 17): a dozen bats hang there until someone comes close or a light
    /// swings in — then they burst into a fluttering cloud, squeak and stream out of the cave, back on the roost a couple
    /// of minutes later. Their droppings make the cave floor a guano mine (the cave's loot table).</summary>
    public class BatColony : MonoBehaviour
    {
        const int Count = 12;
        readonly Transform[] bats = new Transform[Count];
        readonly Vector3[] roost = new Vector3[Count], vel = new Vector3[Count];
        float flushedAt = -999f, checkT;
        bool flying;
        static Mesh batMesh;

        void Start()
        {
            if (!batMesh)
            {
                var g = new MadMax.Voxel.VoxelGrid();
                g.Box(0, 0, 0, 0, 1, 0, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Black, 2));                                // body
                g.Box(-2, 1, 0, -1, 1, 0, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Black, 1)); g.Box(1, 1, 0, 2, 1, 0, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Black, 1));   // wings
                g.Set(-3, 2, 0, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Black[0])); g.Set(3, 2, 0, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Black[0]));
                batMesh = MadMax.Voxel.VoxelMesher.Build(g, "Bat", 0.05f);
            }
            var mat = MadMax.Game.WastelandGame.Instance ? MadMax.Game.WastelandGame.Instance.propMaterial : null;
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Bat", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                roost[i] = new Vector3(Random.Range(-1.4f, 1.4f), 0f, Random.Range(-2.5f, 2.5f));
                // feet on the rock: find the ceiling right above the roost spot
                var w = transform.TransformPoint(roost[i]);
                if (Physics.Raycast(w + Vector3.down * 1.5f, Vector3.up, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                    roost[i] = transform.InverseTransformPoint(hit.point + Vector3.down * 0.01f);
                go.transform.localPosition = roost[i];
                go.transform.localRotation = Quaternion.Euler(180f, Random.Range(0f, 360f), 0f);     // hanging upside down
                go.GetComponent<MeshFilter>().sharedMesh = batMesh;
                var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bats[i] = go.transform;
            }
        }

        void Update()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || !g.Player) return;
            float dt = Time.deltaTime;
            if (!flying)
            {
                if ((checkT -= dt) > 0f) return;
                checkT = 0.4f;
                if (Time.time - flushedAt < 120f) return;
                var who = g.Current ? g.Current.transform.position : g.Player.transform.position;
                float d = Vector3.Distance(who, transform.position);
                bool light = g.Player.Tool is MadMax.Game.LightTool && d < 14f;
                if (d < 7f || light) Flush();
                return;
            }
            // a fluttering cloud that drifts out along the tunnel and away
            float age = Time.time - flushedAt;
            var away = transform.forward;
            for (int i = 0; i < Count; i++)
            {
                var b = bats[i];
                if (!b) continue;
                vel[i] += (Random.insideUnitSphere * 14f + away * (age > 1.5f ? 7f : 0f) + Vector3.up * (b.localPosition.y < -2f ? 6f : 0f)) * dt;
                vel[i] = Vector3.ClampMagnitude(vel[i], 7f);
                b.position += vel[i] * dt;
                b.rotation = Quaternion.LookRotation(vel[i].sqrMagnitude > 0.01f ? vel[i] : Vector3.forward) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 40f + i) * 50f);   // flapping
            }
            if (Random.value < dt * 3f) MadMax.Audio.Sfx.Play("birds", transform.position, 0.3f, 2.2f, 25f, 0.2f);
            if (age > 9f)
            {
                flying = false;                                                                  // gone; back on the roost unseen
                for (int i = 0; i < Count; i++) if (bats[i]) { bats[i].localPosition = roost[i]; bats[i].localRotation = Quaternion.Euler(180f, Random.Range(0f, 360f), 0f); vel[i] = Vector3.zero; }
            }
        }

        /// <summary>Scare them off the roost.</summary>
        public void Flush()
        {
            if (flying) return;
            flying = true; flushedAt = Time.time;
            MadMax.Audio.Sfx.Play("birds", transform.position, 0.8f, 1.9f, 40f);
            for (int i = 0; i < Count; i++) vel[i] = Random.insideUnitSphere * 3f + Vector3.down;
        }
    }
}
