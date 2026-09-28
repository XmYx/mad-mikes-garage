using UnityEngine;

namespace MadMax.World
{
    /// <summary>Marks a roof that puts whoever stands under it underground (bunkers, rock tunnels): the camera
    /// switches to the radial underground cutaway (<see cref="MadMax.Game.OccluderFade"/>), rain stays outside.</summary>
    public class Subterranean : MonoBehaviour { }
}
