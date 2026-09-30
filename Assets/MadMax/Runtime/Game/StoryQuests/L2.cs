using MadMax.Building;
using MadMax.Story;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game
{
    /// <summary>L2 IRON PILGRIMAGE: Old Wick's camp outside the relic bunker (a dry generator for his chain hoist) and
    /// Marisol's corner of the airfield apron (two broken runway lamp posts). Relics found the old way (loot spots) are
    /// recognised; the hoist, the mended lights and the purchase hand them over through <see cref="LastEngine.Give"/>.</summary>
    public partial class WastelandGame
    {
        float l2Check;

        partial void Scene_L2()
        {
            Q3ResetPayoff("L2");
            if (!Build || !Build.Structures) return;
            if (StoryAnchors.Has("l2_wick"))
            {
                var gen = PutAt("l2_wick", "generator", new Vector3(2.2f, 0f, -1.6f), 0f);           // the hoist's generator: dry and off
                if (gen && gen.TryGetComponent<Generator>(out var gc)) { gc.fuel = 0f; gc.on = false; gen.Dirty(); }
                PutAt("l2_wick", "barrel", new Vector3(-2.2f, 0f, -2f), 0f);
                PutAt("l2_wick", "bench", new Vector3(-1.8f, 0f, 1f), 90f);
                PutAt("l2_wick", "lamp", new Vector3(0.8f, 0f, 1.8f), 0f);
                PutAt("l2_wick", "sign", new Vector3(0f, 0f, -3.2f), 0f);
            }
            if (StoryAnchors.Has("l2_marisol"))
            {
                foreach (float x in new[] { -4f, 4f })
                {
                    var lamp = PutAt("l2_marisol", "lamppost", new Vector3(x, 0f, 3.5f), 0f);        // runway lights that don't
                    if (lamp) { lamp.hits = 3; lamp.Dirty(); }
                }
                PutAt("l2_marisol", "table", new Vector3(-1.6f, 0f, -1.2f), 0f);
                PutAt("l2_marisol", "chair", new Vector3(-1.6f, 0f, -2.4f), 0f);
                PutAt("l2_marisol", "barrel", new Vector3(1.8f, 0f, -1.8f), 0f);
            }
        }

        partial void Tick_L2()
        {
            if (!Player || Time.time < l2Check) return;
            l2Check = Time.time + 0.3f;
            // the block: found in the stores (the old way), or up on Wick's hoist once his generator runs
            if (!Plot.StepDone("L2", "block"))
            {
                if (LastEngine.Has("relic_block")) Plot.Note("l2:block_found");
                else
                {
                    var gen = Q3Prop("generator", StoryAnchors.Get("l2_wick"), 6f);
                    if (gen && gen.TryGetComponent<Generator>(out var gc) && gc.on && gc.fuel > 0f) Plot.Note("l2:hoist");
                }
            }
            else if (Q3Route("L2", "block", "LIFTED") && !LastEngine.Has("relic_block"))
            {
                MadMax.Audio.Sfx.Play("chain", StoryAnchors.Get("l2_wick"), 1f, 0.8f, 40f);
                LastEngine.Give(this, "relic_block", "ON OLD WICK'S HOIST");
                Journal.Add("STORY", "WICK'S HOIST GROANED, THE CHAIN RAN, AND THE BLOCK CAME UP THE RAMP LIKE A SLOW SUNRISE. WICK CRIED A BIT. DON'T MENTION IT.");
            }
            // the heads: salvaged from the hut (the old way), for mending the lights, or bought
            if (!Plot.StepDone("L2", "heads"))
            {
                if (LastEngine.Has("relic_heads")) Plot.Note("l2:heads_found");
                else if (L2LightsMended()) Plot.Note("l2:lights");
            }
            else if (!Q3Route("L2", "heads", "SALVAGED") && !LastEngine.Has("relic_heads"))
            {
                LastEngine.Give(this, "relic_heads", Q3Route("L2", "heads", "MENDED") ? "FROM MARISOL, FOR THE LIGHTS" : "BOUGHT FROM MARISOL");
                if (Q3Route("L2", "heads", "MENDED")) Journal.Add("STORY", "MARISOL SWITCHED THE RUNWAY LIGHTS ON AT DUSK, JUST TO SEE THEM, AND GAVE YOU HER BROTHER'S HEADS WRAPPED IN HIS FLYING JACKET.");
            }
            if (LastEngine.Has("relic_block")) Plot.Note("l2:has_block");
            if (LastEngine.Has("relic_heads")) Plot.Note("l2:has_heads");
            L2Payoff();
        }

        /// <summary>Both of Marisol's runway lamp posts are back to full (build-mode repair).</summary>
        bool L2LightsMended()
        {
            if (!StoryAnchors.Has("l2_marisol")) return false;
            var at = StoryAnchors.Get("l2_marisol");
            int lamps = 0;
            foreach (var p in Placeable.All)
            {
                if (!p || p.id != "lamppost" || !IsStoryProp(p) || Q3Flat(p.transform.position, at) > 10f) continue;
                if (p.hits < p.MaxHits) return false;
                lamps++;
            }
            return lamps > 0;
        }

        static void L2Payoff()
        {
            string block = Q3Route("L2", "block", "LIFTED") ? "WICK LIFTED THE BLOCK OUT GENTLY ON HIS HOIST" : "THE BLOCK CAME OUT OF THE BUNKER'S STORES";
            string heads = Q3Route("L2", "heads", "MENDED") ? "MARISOL GAVE THE HEADS FOR HER RUNWAY LIGHTS" : Q3Route("L2", "heads", "I'LL PAY") ? "MARISOL SOLD THE HEADS FOR SIXTY AND THE TRUTH"
                         : "THE HEADS CAME OUT OF THE RADIO HUT";
            Q3Payoff("L2", block + "; " + heads + ". TWO STORIES CAME WITH THEM: A MAN WHO WAITED FOR SOMEONE WITH BETTER KNEES, AND A WOMAN WHO KEPT THE LIGHTS FOR HER BROTHER.");
        }
    }
}
