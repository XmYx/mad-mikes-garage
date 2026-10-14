using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Resting on the ground and going prone from the game's side (animation revamp 2026-10-13): the context
    /// menu on open ground offers SIT DOWN / LIE DOWN / GO PRONE (walking there first when it was clicked further off),
    /// the prone key, and eating or drinking while seated (the eat / drink clip over the sitting pose, chewing after a
    /// bite). See PlayerCharacter.Low.</summary>
    public partial class WastelandGame
    {
        /// <summary>Context provider "body.rest": on open ground, on foot.</summary>
        void RestOptions(ContextTarget t, List<ContextOption> into)
        {
            if (t == null || !t.ground || Current || !Player || Player.Interior || Player.Carried || Player.Swimming || Player.Sitting || Boarding) return;
            if (Player.LowState != PlayerCharacter.Low.None)
            {
                into.Add(Opt(Player.Prone ? "GET UP" : "STAND UP", () => Player.GetUpFromLow(), false, null, Player.Prone ? Controls.Act.Prone : (Controls.Act?)null));
                return;
            }
            into.Add(Opt("SIT DOWN", () => Player.GoLow(PlayerCharacter.Low.Sit)));
            into.Add(Opt("LIE DOWN", () => Player.GoLow(PlayerCharacter.Low.Lie)));
            into.Add(Opt("GO PRONE", () => Player.GoLow(PlayerCharacter.Low.Prone), true, null, Controls.Act.Prone));
        }

        /// <summary>From <see cref="Gesture"/>: a bite starts the chewing; seated (a chair, the ground) the eat / drink
        /// clip plays over the sitting pose with the food in the hand. True = handled here.</summary>
        bool SeatedGestureHook(UseKind kind, string prop)
        {
            if (!Player || (kind != UseKind.Eat && kind != UseKind.Drink)) return false;
            // the eat clip's bite events start the chewing (PlayerCharacter.ClipEvent); without them, chew from now
            var bite = HumanClips.Enabled ? HumanClips.Get("eat") : null;
            if (kind == UseKind.Eat && (bite == null || bite.EventAt("bite") < 0f)) Player.ChewUntil = Time.time + 1f + PlayerCharacter.ChewSeconds;
            if (!GameSettings.Current.workAnimation) return false;
            var mesh = UsePropMesh(prop);
            return Player.SeatedGesture(kind == UseKind.Eat ? "eat" : "drink", mesh, mesh ? PropMaterialFor(prop) : null);
        }
    }
}
