using UnityEngine;

namespace MadMax.World
{
    /// <summary>A chunk's batched town decor (<see cref="BiomeProps"/>): owns its combined mesh and frees it with the chunk.</summary>
    public class TownDecor : MonoBehaviour
    {
        public Mesh mesh;
        void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
