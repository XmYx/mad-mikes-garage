using UnityEngine;

namespace MadMax.World
{
    /// <summary>Marks a structure renderer (with its collider on the same object) that the camera cutaway may clip like
    /// a building prop: the rolling city's blocks and storeys (OccluderFade). <see cref="extra"/> renderers (a storey's
    /// glow and furnishings) are clipped with it.</summary>
    public class Cutaway : MonoBehaviour
    {
        public Renderer[] extra;
    }
}
