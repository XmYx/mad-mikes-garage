using MadMax.Game;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>NPC limbs (<see cref="Limbs"/>): a heavy blade blow (the player's knife, machete, axe...) or a crushing
    /// hit can take off the limb nearest the strike. The bit drops as an item, the body bleeds, a lost weapon hand drops
    /// the weapon (the NPC flees), a lost leg slows it to a hop or a crawl. Remembered in <see cref="NpcSave.lost"/>.</summary>
    public partial class Npc
    {
        /// <summary>Walking pace left with the legs it has.</summary>
        float LimbPace
        {
            get
            {
                int lost = rig.appearance.lost;
                float m = 1f;
                if (Limbs.Gone(lost, BodyZone.FootL)) m *= 0.35f;
                if (Limbs.Gone(lost, BodyZone.FootR)) m *= 0.35f;
                return m;
            }
        }

        void TryDismember(Vector3 point, float dmg, GameObject source)
        {
            if (proxy || !rig || mode == Mode.Dead) return;
            var g = WastelandGame.Instance;
            bool bladed = g && g.Player && g.Player.Tool && source && source.transform.IsChildOf(g.Player.transform) && Limbs.Bladed(g.Player.Tool.id);
            float chance = bladed && dmg > 22f ? 0.14f : dmg > 45f ? 0.12f : 0f;
            if (chance <= 0f || Random.value > chance) return;
            BodyZone best = BodyZone.Head; float bd = float.MaxValue;
            foreach (var z in Limbs.All)
            {
                if (Limbs.Gone(rig.appearance.lost, z)) continue;
                float d = (rig.Bone(Limbs.CutBone(z)).position - point).sqrMagnitude;
                if (d < bd) { bd = d; best = z; }
            }
            if (best != BodyZone.Head) SeverLimb(best);
        }

        /// <summary>Take a limb off this NPC (the bit drops, it bleeds; weapon hand → disarmed and fleeing).</summary>
        public void SeverLimb(BodyZone z)
        {
            var a = rig.appearance;
            if (!Limbs.IsLimb(z) || Limbs.Gone(a.lost, z)) return;
            var cut = rig.Bone(Limbs.CutBone(z));
            var at = cut.position; var rot = cut.rotation;
            var mesh = HumanDesign.BodyMesh(Limbs.CutBone(z), a);
            if (Limbs.Child(z) is BodyZone c) a.lost &= ~Limbs.Bit(c);
            a.lost |= Limbs.Bit(z);
            State.lost = a.lost;
            string held = tool ? tool.id : null;
            if (tool) tool.transform.SetParent(transform, false);                               // off the bone that goes with the rebuild
            rig.Rebuild();
            anim = new HumanAnimator(rig);
            anim.Footstep += left => OnFootstep();
            bool armed = held != null && (!Limbs.Gone(a.lost, BodyZone.HandR) || !Limbs.Gone(a.lost, BodyZone.HandL));
            SetTool(armed ? held : null, rig.material);
            var item = WorldItem.Create(Limbs.Item(z), 1, -1f, rig.material, at, rot, null, mesh);
            if (item.Body) item.Body.linearVelocity = rot * Vector3.down * 1.5f + Random.insideUnitSphere;
            MadMax.Net.NetSession.Instance?.SendItemSpawn(item, Vector3.zero);
            BloodStains.Splash(at, 1f);
            rig.Bleed(at, 1.6f);
            Bleed(40f);
            MadMax.Audio.Sfx.Play("punch", at, 1f, 0.6f, 25f);
            if (Limbs.IsArm(z) && held != null && !armed) { mode = Mode.Flee; fleeUntil = Time.time + 15f; }
        }
    }
}
