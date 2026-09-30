using MadMax.Game;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Sandbagged MG nest: an unpowered <see cref="AutoTurret"/> that fires only with a gunner — a companion
    /// guarding the base (it mans turrets by itself) or the player: [T] mans the gun (sitting on the ammo crate behind
    /// it), the gun swings onto the raider nearest the aim (the cursor in top-down views, the screen centre otherwise)
    /// and fires while LMB / RT is held; [F] leaves it. Belts go in its ammo box ([E]). Upgrades (U) to the powered
    /// auto turret, belts and all.</summary>
    public class GunNest : MonoBehaviour, IInteractable
    {
        /// <summary>The gunner's hips (on the crate behind the gun), piece-local.</summary>
        public Vector3 spot = new Vector3(0f, 0.62f, -0.62f);
        /// <summary>Automation (<see cref="WastelandGame.ExternalInput"/>): hold the trigger; <see cref="aimAt"/> when set.</summary>
        public bool trigger, forceAim;
        public Vector3 aimAt;
        Seat seat;
        AutoTurret gun;

        void Start() => gun = GetComponent<AutoTurret>();

        public bool Manning(WastelandGame g) => g && g.Player && seat && g.Player.Sitting && g.Player.SeatedOn == seat;

        public string Prompt(WastelandGame g) => Manning(g) ? null : "[T] MAN THE GUN";

        public void Use(WastelandGame g, bool secondary)
        {
            if (!secondary || !g.Player || Manning(g)) return;
            if (!seat) { seat = gameObject.AddComponent<Seat>(); seat.hidden = true; seat.rest = 1f; seat.reading = 1f; }
            g.Player.SitOn(seat, spot);
            g.Toast("ON THE GUN: HOLD FIRE TO SHOOT, " + Controls.Name(Controls.Act.Enter) + " TO LEAVE IT");
        }

        void Update()
        {
            if (!gun) gun = GetComponent<AutoTurret>();
            if (!gun) return;
            var g = WastelandGame.Instance;
            bool at = Manning(g);
            gun.playerGunner = at;
            if (!at) { gun.trigger = false; return; }
            gun.favour = forceAim ? aimAt : Aim(g);
            if (WastelandGame.ExternalInput) { gun.trigger = trigger; return; }
            var mouse = UnityEngine.InputSystem.Mouse.current; var pad = UnityEngine.InputSystem.Gamepad.current;
            gun.trigger = !g.Menus.IsOpen && ((mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed));
        }

        /// <summary>Where the gunner points: under the cursor in the top-down views, the screen centre otherwise.</summary>
        Vector3 Aim(WastelandGame g)
        {
            var rig = g.cameraRig;
            var ahead = transform.position + transform.forward * 20f + Vector3.up;
            if (!rig || !rig.pixel) return ahead;
            var cam = rig.pixel.GetComponent<Camera>();
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var view = rig.TopDownView && mouse != null ? new Vector2(mouse.position.ReadValue().x / Screen.width, mouse.position.ReadValue().y / Screen.height) : new Vector2(0.5f, 0.5f);
            var ray = cam.ViewportPointToRay(new Vector3(view.x, view.y, 0f));
            if (Physics.Raycast(ray, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(g.Player.transform))
                return hit.point + (rig.TopDownView ? Vector3.up : Vector3.zero);
            return ray.GetPoint(60f);
        }
    }
}
