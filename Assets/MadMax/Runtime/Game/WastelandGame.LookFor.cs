using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The journal's LOOK FOR list (2026-10-12): what the player has heard of but not found — story camps
    /// known only as a search circle, wrecks in the news nobody marked, gangs still carrying the player's stolen parts.
    /// Binoculars (<see cref="SpotCamp"/>, <see cref="SpotWrecks"/>) and taking parts back tick them off
    /// (<see cref="LookTicked"/>).</summary>
    public partial class WastelandGame
    {
        /// <summary>A news wreck is searched for within this circle (the news names the place, not the spot).</summary>
        public const float NewsWreckRadius = 150f;
        /// <summary>Recently found LOOK FOR items, newest first (this session).</summary>
        public readonly List<string> LookTicked = new List<string>();

        void TickLookFor(string what)
        {
            LookTicked.Insert(0, what);
            if (LookTicked.Count > 4) LookTicked.RemoveAt(LookTicked.Count - 1);
        }

        /// <summary>Everything to look for, as pins (radius > 0 = a search circle).</summary>
        public void LookFor(List<Pin> into)
        {
            var col = new Color32(200, 220, 255, 255);
            var me = FocusPos;
            // camps heard of from strangers: a circle
            if (HeardVague.Count > 0)
                foreach (var q in StoryLibrary.All)
                {
                    if (q.giver == null || Story.Story.StateOf(q.id) != Story.Story.State.Open) continue;
                    var m = StoryCast.Find(q.giver);
                    if (m == null || !HeardVague.Contains(m.Value.key) || m.Value.anchor == null || !StoryAnchors.Has(m.Value.anchor)) continue;
                    if (into.Exists(p => p.label.Contains(m.Value.title))) continue;
                    float r = VagueRadiusOf(m.Value.key);
                    into.Add(new Pin { label = "THE " + m.Value.title + "'S CAMP", pos = VagueCentre(m.Value.key, StoryAnchors.Get(m.Value.anchor), r), color = col, radius = r });
                }
            // wrecks the news told of that nobody marked or found
            foreach (var e in TownNews.Entries)
            {
                if (!e.wreck) continue;
                var at = new Vector3(e.x, 0f, e.z);
                if (Flat(at - me) > TownNews.Reach) continue;
                bool known = false;
                foreach (var q in NewsPins) if (Flat(q - at) < 10f) { known = true; break; }
                foreach (var w in RoadWrecks) if (Found(w) && Flat(new Vector3(w.x, 0f, w.z) - at) < 10f) { known = true; break; }
                if (known) continue;
                string key = "news:" + Mathf.RoundToInt(e.x) + "," + Mathf.RoundToInt(e.z);
                into.Add(new Pin { label = "A WRECK IN THE NEWS (DAY " + e.day + ")", pos = VagueCentre(key, at, NewsWreckRadius), color = col, radius = NewsWreckRadius });
            }
            // gangs riding with the player's parts before market day
            var gangs = new HashSet<string>(Trade.StolenBy.Values);
            foreach (var gang in gangs)
            {
                var ids = Trade.CarriedBy(gang);
                if (ids.Count == 0 || !GangWhereabouts(gang, me, out var at)) continue;
                into.Add(new Pin { label = "THE " + gang + " (YOUR " + (ids.Count == 1 ? ItemName(ids[0]) : ids.Count + " PARTS") + ")", pos = at, color = col });
            }
        }
    }
}
