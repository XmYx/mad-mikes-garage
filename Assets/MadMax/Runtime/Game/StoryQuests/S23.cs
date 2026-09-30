using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // S23 NO TEETH, STILL TROUBLE: Nia's market (three stalls with awnings, tables and wares), the ring in front (four
    // corner posts, flags) and Brick's crate by it with his debt book inside. Once Brick has accepted a challenge, stepping
    // into the ring starts a supervised bout (BoutRing): it ends at the third knockdown, a yield (stepping out) or a foul;
    // only a win settles it, and stepping back in is a rematch.
    public partial class WastelandGame
    {
        const float S23Ring = 3.8f;
        float s23T;
        bool s23WasIn, s23Cleaned, s23Told;

        partial void Scene_S23()
        {
            Q5ResetPayoff("S23");
            if (!StoryAnchors.Has("nia") || !Build || !Build.Structures) return;
            foreach (var (stall, ware) in new[] { ("s23_stall1", "barrel"), ("s23_stall2", "tyres"), ("s23_stall3", "crate") })
            {
                Q5Put(stall, "porch_awning", new Vector3(0f, 0f, -1.4f), 0f);
                Q5Put(stall, "table", new Vector3(0f, 0f, -0.6f), 0f);
                Q5Put(stall, ware, new Vector3(1.8f, 0f, -1.8f), 15f);
            }
            foreach (var c in new[] { new Vector3(-2.8f, 0f, -2.8f), new Vector3(2.8f, 0f, -2.8f), new Vector3(-2.8f, 0f, 2.8f), new Vector3(2.8f, 0f, 2.8f) }) Q5Put("s23_ring", "post", c, 0f);
            Q5Put("s23_ring", "flag", new Vector3(-3.4f, 0f, 3.4f), 0f);
            Q5Put("s23_ring", "bench", new Vector3(5f, 0f, 0f), 90f);
            var crate = Q5Put("s23_ring", "crate", new Vector3(4.6f, 0f, -2.6f), 0f);
            if (crate && crate.TryGetComponent<Container>(out var box)) { box.inventory.AddItem("story_s23_ledger", 1); box.inventory.Add(MadMax.Items.ResourceType.Scrap, 3); crate.Dirty(); }
        }

        /// <summary>The ring's rules for a bout with Brick.</summary>
        static NonlethalBout S23Bout() => new NonlethalBout { key = "s23:bout", fighter = "s23_brick", ring = "s23_ring", ringRadius = S23Ring, fighterGuard = 0.85f };

        partial void Tick_S23()
        {
            if ((s23T += Time.deltaTime) < 0.2f) return;
            s23T = 0f;
            var brick = CastBody("s23_brick");
            if (!brick || !StoryAnchors.Has("s23_ring")) return;
            // a bout can't outlive a reload: whatever hostility a saved bout left behind is cleared once
            if (!s23Cleaned) { s23Cleaned = true; if (!BoutRing.Current && brick.State.Has(MadMax.Npc.NpcSave.Hostile)) brick.State.Set(MadMax.Npc.NpcSave.Hostile, false); }
            var ring = StoryAnchors.Get("s23_ring");
            if (!BoutRing.Current) Q5Keep(brick, Q5At("s23_ring", new Vector3(3.2f, 0f, 0.6f)), StoryAnchors.Yaw("s23_ring") - 90f);
            bool won = MadMax.Story.Story.StepDone("S23", "settle");
            bool inRing = !Current && Player && Q5Flat(Player.transform.position, ring) < S23Ring - 0.3f;
            if (inRing && !s23WasIn && !won && !BoutRing.Current && MadMax.Story.Story.StepDone("S23", "challenge"))
            {
                var bout = S23Bout();
                string why = BoutRing.Refuse(this, bout, brick);
                if (why != null) Toast("NIA: " + why);
                else BoutRing.Begin(this, bout, brick);
            }
            s23WasIn = inRing;
            // a lost bout settles nothing; say so once per loss
            var last = BoutRing.Finished;
            if (last != null && last.key == "s23:bout" && last.result == NonlethalBout.Result.Lost && !s23Told)
            {
                s23Told = true;
                Journal.Add("JOB", "BRICK WON IN THE RING, FAIR AND SQUARE, AND GRINS ABOUT IT. THE DEBT BOOK OR A DEAL CAN STILL SETTLE IT; OR STEP BACK INTO THE RING FOR A REMATCH.");
            }
            if (last != null && last.result != NonlethalBout.Result.Lost) s23Told = false;
            // how it was settled decides the closing line
            if (won && !MadMax.Story.Story.Flag("s23:payoff"))
            {
                MadMax.Story.Story.SetFlag("s23:payoff");
                string r = MadMax.Story.Story.Route("S23", "settle") ?? "";
                Q5Payoff("S23", r.StartsWith("WON")
                    ? "BRICK HALLORAN LOST IN NIA'S RING, FAIR AND SQUARE, AND PAID THE STALLHOLDERS BACK. HE SAYS YOU CAN HAVE A REMATCH ANY TIME. HE MEANS IT KINDLY. PROBABLY."
                    : r.StartsWith("YOUR BOOK")
                    ? "BRICK'S DEBT BOOK WAS READ OUT AT THE MARKET: THE SAME LOANS, TWICE AND THREE TIMES. HE PAID EVERY STALL BACK WHILE EVERYONE WATCHED."
                    : "BRICK PAID THE STALLS BACK HALF AND YOU COVERED THE REST. HE STILL SMILES AT EVERYONE. NOBODY PAYS HIM FOR IT ANY MORE.");
            }
        }
    }
}
