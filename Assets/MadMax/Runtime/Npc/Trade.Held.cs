using System.Collections.Generic;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>2026-10-15: a part a rescued survivor had a stall in their town hold for the player at half price until
    /// the week is out (<see cref="Held"/>), and stalls a caught digger soured (<see cref="SourFactor"/>: +10 % for a
    /// few days).</summary>
    public static partial class Trade
    {
        public const string HeldNote = "HELD FOR YOU";
        public const float HeldFactor = 0.5f;
        /// <summary>Settlement index → (part id "part:key", last day it is held). Saved with the crew road lines.</summary>
        public static readonly Dictionary<int, (string id, int until)> Held = new Dictionary<int, (string, int)>();

        public static bool HoldsPart(string kind) => kind == "parts" || kind == "salvage";

        static void HeldStock(NpcProfile p, List<Offer> into, float bargain)
        {
            if (Town == null || !HoldsPart(p.kind) || !Held.TryGetValue(Town.index, out var h)) return;
            if (MadMax.World.DayNight.Day > h.until) { Held.Remove(Town.index); return; }
            into.Insert(0, new Offer { id = h.id, count = 1, price = Mathf.Max(1, Mathf.RoundToInt(BuyPrice(h.id, bargain) * HeldFactor)), note = HeldNote });
        }

        /// <summary>Price factor of the stalls in the market being traded in (a caught digger's word).</summary>
        public static float SourFactor => Town != null && WastelandGame.Instance ? WastelandGame.Instance.StallSour(Town.index) : 1f;
    }
}
