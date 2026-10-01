using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    public static partial class FurnitureLibrary
    {
        /// <summary>The HD model of piece <paramref name="id"/> on an object that carries its voxel mesh (placed pieces,
        /// loot spots, town fixtures, stashes). The voxel mesh stays for colliders and debris. Lamps hand their HD bulb to
        /// <see cref="PoweredLight"/> and glow with it; a dyed <see cref="Placeable"/> tints the model. Returns null when the
        /// piece has no HD export (the voxel mesh keeps drawing).</summary>
        public static HDVisual DressHD(GameObject go, string id, int flags = 0)
        {
            if (!go || string.IsNullOrEmpty(id)) return null;
            var v = HDVisual.Dress(go, HDDomain.Furniture, id, flags);
            if (!v) return null;
            if (go.TryGetComponent<PoweredLight>(out var pl))
            {
                var bulb = v.Object("Bulb");
                if (bulb && bulb.TryGetComponent<Renderer>(out var br)) { v.Release(br); pl.SetBulb(br); }
                v.LampState = () => pl && pl.Glowing;
            }
            if (go.TryGetComponent<Placeable>(out var p) && p.dye != 0) v.SetTint(DyeTint(p.dye));
            return v;
        }

        /// <summary>Tint of a dye for HD pieces (multiplies the textured albedo, whose average is about 0.55): the dye
        /// ramp's middle shade over that, so dark dyes darken and pale ones keep the wood and metal showing through.</summary>
        public static Color32? DyeTint(int dye)
        {
            var ramp = Pal.DyeRamp(dye);
            if (ramp == null || ramp.Length == 0) return null;
            Color c = ramp[ramp.Length / 2];
            const float k = 1f / 0.55f;
            return (Color32)new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), 1f);
        }
    }
}
