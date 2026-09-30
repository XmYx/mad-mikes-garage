using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Work going on at the forge (hammer on the anvil, sparks off the billet) or the machine shop (swarf and the
    /// grinder's whine) while its <see cref="CraftingStation"/> has a job; only near the player.</summary>
    public class MetalShopFx : MonoBehaviour
    {
        public bool forge;
        static readonly Color Hot = new Color(1f, 0.62f, 0.2f);
        CraftingStation station;
        float next;

        void Awake() => station = GetComponent<CraftingStation>();

        void Update()
        {
            if (!station || !station.Busy || !station.Powered || (next -= Time.deltaTime) > 0f) return;
            next = forge ? Random.Range(0.45f, 0.8f) : Random.Range(0.9f, 1.8f);
            var g = MadMax.Game.WastelandGame.Instance;
            var near = g && g.Current ? g.Current.transform : g && g.Player ? g.Player.transform : null;
            if (!near || (near.position - transform.position).sqrMagnitude > 28f * 28f) return;
            var at = station.OutputPoint;
            if (forge)
            {
                MadMax.World.Fx.Sparks(at, Vector3.up, 5, Hot);
                MadMax.Audio.Sfx.Play("hit_metal", at, 0.35f, Random.Range(1.1f, 1.3f), 22f, 0.2f);
            }
            else
            {
                MadMax.World.Fx.Sparks(at + Vector3.up * 0.6f, transform.forward, 3, MadMax.Voxel.Pal.Accent);
                MadMax.Audio.Sfx.Play("grinder", at, 0.25f, 1.5f, 18f, 0.5f);
            }
        }
    }
}
