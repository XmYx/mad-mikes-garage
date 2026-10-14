using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Secondary motion of HD garments and hair (<see cref="ClothDynamics"/> on the rig's dangle chains).</summary>
    public static class ClothScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new ClothDynamicsRun();
        }
    }

    /// <summary>character.cloth_dynamics: the player in a duster with long hair (HD) gets the coat-tail and hair chains;
    /// standing still the tail tips hang at rest, running makes them swing away from the pelvis, stopping lets them settle
    /// back. Also: the chains snap to rest out of reach of the camera.</summary>
    class ClothDynamicsRun : Scenario
    {
        public override string Id => "character.cloth_dynamics";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!HDCharacterCatalog.Instance) { c.Block("no HD character catalogue (export the character pack, MadMax/HD/Build Character Catalog)"); yield break; }
            if (!HDHuman.Enabled) { c.Block("HD humans are switched off"); yield break; }
            yield return ItemsScenarios.OnFootAtPad(c, 10f);
            var p = g.Player;
            var rig = p.Rig;
            var savedLook = rig.appearance;
            var savedOutfit = rig.outfit.ToList();
            bool savedExt = WastelandGame.ExternalInput;
            try
            {
                var look = new Appearance { hair = HairStyle.Long, beard = 1, body = 0 };
                rig.appearance = look;
                rig.outfit.Clear();
                rig.outfit.AddRange(new[] { "duster", "boots", "pants" }.Where(id => ClothingLibrary.Get(id) != null));
                rig.Rebuild();
                c.Fixture("player rebuilt: HD body M, long hair, stubble, duster");
            }
            catch (System.Exception e) { c.Check(false, "rebuild: " + e.Message); }
            yield return null;
            var cloth = rig.Cloth;
            c.Check(rig.IsHD, $"the player is HD (shape {rig.HDShape})");
            if (!c.Check(cloth, "the HD rig simulates dangle chains (ClothDynamics)")) { Restore(rig, savedLook, savedOutfit); yield break; }
            c.Metric("chains", cloth.ChainCount, "groups");
            bool hasTail = cloth.Tip("TailB").HasValue, hasHair = cloth.Tip("HairA").HasValue;
            c.Check(hasTail, "the duster is bound to the coat-tail chains (TailB)");
            c.Check(hasHair, "long hair is bound to the hair chains (HairA)");
            c.Check(rig.HDPieces.Any(k => k.Contains("beard_stubble")), "stubble is an HD piece (beard_stubble)");
            if (!hasTail) { Restore(rig, savedLook, savedOutfit); yield break; }

            var pelvis = rig.Bone(BodyPart.Pelvis);
            Vector3 Rel(string grp) => pelvis.InverseTransformPoint(cloth.Tip(grp).Value);
            Vector3 RestRel(string grp) => pelvis.InverseTransformPoint(cloth.RestTip(grp).Value);
            yield return new WaitForSeconds(1.5f);
            c.Check(cloth.Simulating, "the chains are simulated near the camera");
            float rest0 = (Rel("TailB") - RestRel("TailB")).magnitude;
            c.Metric("tail_rest_offset", rest0, "m");
            c.Check(rest0 < 0.12f, $"standing still the back tail hangs near its rest pose ({rest0:0.000} m)");

            // run
            WastelandGame.ExternalInput = true;
            p.viewYaw = p.transform.eulerAngles.y; p.run = true; p.moveInput = new Vector2(0f, 1f);
            float maxTail = 0f, maxHair = 0f, t0 = Time.time;
            bool shot = false;
            while (Time.time - t0 < 2.5f)
            {
                g.Stats.stamina = g.Stats.MaxStamina;
                maxTail = Mathf.Max(maxTail, (Rel("TailB") - RestRel("TailB")).magnitude, (Rel("TailL") - RestRel("TailL")).magnitude);
                if (hasHair) maxHair = Mathf.Max(maxHair, (rig.Head.InverseTransformPoint(cloth.Tip("HairA").Value) - rig.Head.InverseTransformPoint(cloth.RestTip("HairA").Value)).magnitude);
                if (!shot && Time.time - t0 > 1.4f) { shot = true; c.Screenshot("cloth_dynamics_run"); }
                yield return null;
            }
            p.run = false; p.moveInput = Vector2.zero;
            c.Metric("tail_swing", maxTail, "m");
            c.Metric("hair_swing", maxHair, "m");
            c.Check(maxTail > 0.05f, $"running swings the coat tails away from the pelvis ({maxTail:0.000} m)");
            if (hasHair) c.Check(maxHair > 0.02f, $"running swings the hair ({maxHair:0.000} m)");

            // settle
            yield return new WaitForSeconds(3f);
            float settled = (Rel("TailB") - RestRel("TailB")).magnitude;
            c.Metric("tail_settled_offset", settled, "m");
            c.Check(settled < Mathf.Max(0.08f, rest0 + 0.04f), $"standing again the tails settle back ({settled:0.000} m)");

            // out of reach: rest pose, no simulation
            var cam = Camera.main;
            if (cam)
            {
                var far = pelvis.position + Vector3.up * (ClothDynamics.PlayerReach + 20f);
                var camT = cam.transform; var pos0 = camT.position;
                var rigCam = g.cameraRig; bool rigOn = rigCam && rigCam.enabled;
                if (rigCam) rigCam.enabled = false;
                camT.position = far;
                yield return null; yield return null;
                c.Check(!cloth.Simulating, "beyond reach of the camera the chains rest (no simulation)");
                camT.position = pos0;
                if (rigCam) rigCam.enabled = rigOn;
                yield return null;
            }
            WastelandGame.ExternalInput = savedExt;
            Restore(rig, savedLook, savedOutfit);
        }

        static void Restore(HumanRig rig, Appearance look, List<string> outfit)
        {
            rig.appearance = look;
            rig.outfit.Clear(); rig.outfit.AddRange(outfit);
            rig.Rebuild();
        }
    }
}
