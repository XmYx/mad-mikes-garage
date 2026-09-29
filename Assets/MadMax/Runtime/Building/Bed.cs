using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Sleep until morning (heals, sets the respawn point). Sleep quality follows the room's
    /// <see cref="Comfort"/>: a cosy room gives the WELL RESTED buff.</summary>
    public class Bed : MonoBehaviour, IInteractable
    {
        float comfort, checkedAt = -10f;
        string note;

        void Refresh()
        {
            if (Time.unscaledTime - checkedAt < 2f) return;
            checkedAt = Time.unscaledTime;
            comfort = Comfort.At(transform.position + transform.up * 0.1f, out note, GetComponentInParent<Rigidbody>());
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            Refresh();
            string c = " (" + Comfort.Word(comfort) + " " + Mathf.RoundToInt(comfort) + "/10)";
            return (MadMax.World.DayNight.Darkness > 0.2f ? "[E] SLEEP" : "[E] REST") + c + "  [T] SET SPAWN";
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) { g.SetSpawn(transform.position + transform.forward * 1.2f + Vector3.up * 0.2f); return; }
            checkedAt = -10f;
            Refresh();
            g.Sleep(comfort, note);
        }
    }
}
