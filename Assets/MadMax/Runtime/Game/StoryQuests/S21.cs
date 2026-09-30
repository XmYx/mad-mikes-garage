using MadMax.Building;
using MadMax.Story;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game
{
    /// <summary>S21 THE LAST HONEST SAFE: Ruth's yard with the safe (a steel locker with a seized linkage: it starts
    /// damaged, so build-mode repair is "mending the linkage"; no lid to open by hand) and her old shop on the road out
    /// with the order books. A safe that disappears (blown up, smashed, dismantled) was opened the hard way; the
    /// prepared charge is a small cosmetic blast that leaves the contents whole.</summary>
    public partial class WastelandGame
    {
        Placeable s21Safe;
        bool s21Seen;
        int s21Missing;
        float s21Check;

        partial void Scene_S21()
        {
            Q3ResetPayoff("S21");
            if (!StoryAnchors.Has("ruth") || !Build || !Build.Structures) return;
            PutAt("ruth", "porch_awning", new Vector3(-0.6f, 0f, -2f), 0f);
            PutAt("ruth", "table", new Vector3(-2.3f, 0f, 0.2f), 0f);
            PutAt("ruth", "chair", new Vector3(-2.3f, 0f, -1.1f), 0f);
            PutAt("ruth", "lamp", new Vector3(-3.4f, 0f, 1.2f), 0f);
            var safe = PutAt("s21_safe", "locker", Vector3.zero, 180f);
            if (safe) { safe.hits = 2; safe.owner = "RUTH SLATE"; safe.Dirty(); S21Prep(safe); }
            // the old shop on the road out: a broken corner of wall, the shelves, the order books
            if (StoryAnchors.Has("s21_oldshop"))
            {
                PutAt("s21_oldshop", "wall_brick", new Vector3(-1f, 0f, -2.6f), 0f);
                PutAt("s21_oldshop", "wall_brick_window", new Vector3(1f, 0f, -2.6f), 0f);
                PutAt("s21_oldshop", "wall_brick", new Vector3(-2.6f, 0f, -1.2f), 90f);
                PutAt("s21_oldshop", "shelf", new Vector3(1.4f, 0f, -1.6f), 0f);
                PutAt("s21_oldshop", "table", new Vector3(1.2f, 0f, 0.6f), 10f);
                PutAt("s21_oldshop", "sign", new Vector3(3.2f, 0f, 1.4f), 0f);
                PutAt("s21_records", "bookshelf", Vector3.zero, 0f);
            }
        }

        /// <summary>A safe has no lid you can lift: the locker's storage comes off (the prefab adds it back after a load).</summary>
        void S21Prep(Placeable safe)
        {
            if (safe.TryGetComponent<Container>(out var box)) Destroy(box);
        }

        partial void Tick_S21()
        {
            if (!Player || Time.time < s21Check) return;
            s21Check = Time.time + 0.2f;
            if (!s21Safe)
            {
                if (s21Seen)
                {
                    s21Seen = false;                                                             // it was here a moment ago
                    Plot.SetFlag("s21_gone");
                    if (!Plot.StepDone("S21", "open"))
                    {
                        Plot.Note("s21:wrecked"); Plot.Note("s21:demolition");
                        Toast("THE SAFE'S IN PIECES. SO IS HALF OF WHAT WAS IN IT");
                    }
                }
                else if (!Plot.Flag("s21_gone") && StoryAnchors.Has("s21_safe"))
                {
                    s21Safe = Q3Prop("locker", StoryAnchors.Get("s21_safe"), 6f);
                    if (s21Safe) { S21Prep(s21Safe); s21Seen = true; s21Missing = 0; }
                    else if (++s21Missing >= 3) s21Seen = true;                                  // gone before we looked (broken before the job was taken)
                }
            }
            if (s21Safe && !Plot.StepDone("S21", "open") && s21Safe.hits >= s21Safe.MaxHits) Plot.Note("s21:linkage");
            if (Plot.StepDone("S21", "open") && !Plot.Flag("s21_opened"))
            {
                Plot.SetFlag("s21_opened");
                var at = s21Safe ? s21Safe.transform.position : StoryAnchors.Get("s21_safe");
                if (Q3Route("S21", "open", "IT'S IN YOUR ORDER BOOK")) { Plot.Note("s21:intact"); Plot.Note("s21:records"); MadMax.Audio.Sfx.Play("lock", at, 0.9f); }
                else if (Q3Route("S21", "open", "MENDED")) { Plot.Note("s21:intact"); Plot.Note("s21:mechanics"); MadMax.Audio.Sfx.Play("click", at, 0.9f); }
                else if (Q3Route("S21", "open", "ONE SMALL CHARGE"))
                {
                    Inventory.TakeItem("throw_dynamite");
                    var face = s21Safe ? s21Safe.transform.forward : Vector3.forward;
                    MadMax.World.Explosion.Blast(at + face * 0.5f + Vector3.up * 0.8f, 1.2f, 1f, 0f, null, false);   // noise, smoke and a shake: nothing breaks
                    Plot.Note("s21:intact"); Plot.Note("s21:demolition");
                    Toast("A FLAT CRACK, A PUFF OF SMOKE, AND THE BOLT IS GONE. THE ENVELOPES ARE FINE");
                }
            }
            S21Payoff();
        }

        static void S21Payoff()
        {
            string how = Q3Route("S21", "open", "IT'S IN YOUR ORDER BOOK") ? "THE COMBINATION WAS THE DAY HER SHOP OPENED, WRITTEN IN HER OWN ORDER BOOK"
                       : Q3Route("S21", "open", "MENDED") ? "YOU MENDED THE LINKAGE AND IT OPENED LIKE A SIGH"
                       : Q3Route("S21", "open", "ONE SMALL CHARGE") ? "ONE SMALL CHARGE ON THE BOLT, AND NOT A SINGED ENVELOPE"
                       : Q3Route("S21", "open", "TOOK IT APART") ? "IT CAME OPEN THE HARD WAY: HALF THE WAGES ARE CONFETTI AND THE POEM IS SINGED"
                       : "RUTH'S SAFE IS OPEN";
            Q3Payoff("S21", how + ". RUTH WILL FIND HER LADS AND PAY THEM; THE POEM IS BACK WHERE IT CAN'T EMBARRASS ANYONE.");
        }
    }
}
