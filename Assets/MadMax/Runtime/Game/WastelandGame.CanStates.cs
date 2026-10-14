using System.Collections.Generic;
using System.Globalization;
using MadMax.Building;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Liquid containers keep their own contents outside the pack: a can put in a chest, a car compartment or
    /// any storage carries its litres with it (per container, per item id), and a can dropped on the ground keeps them
    /// on its world item. A move goes pack → pool → storage within one frame (the pool entry pushed that frame is the
    /// one that moved), so nothing is made or lost; storage that gains a can on its own (loot rolls) gets an empty one.
    /// Saved in <c>SaveData.blockFluids</c>: "b" lines per placed piece id, "w" lines per world item position; storage
    /// that isn't a placed piece hands its litres back to the pack's pool on save (as before).</summary>
    public partial class WastelandGame
    {
        readonly Dictionary<Container, Dictionary<string, List<CanContents>>> boxCans = new Dictionary<Container, Dictionary<string, List<CanContents>>>();
        readonly Dictionary<WorldItem, List<CanContents>> itemCans = new Dictionary<WorldItem, List<CanContents>>();
        readonly Dictionary<CanContents, int> pooledFrame = new Dictionary<CanContents, int>();
        readonly List<string[]> pendingCanLines = new List<string[]>();
        float canHookAt, pendingCanUntil;

        /// <summary>The contents of the containers of <paramref name="id"/> held in a storage (tests, labels).</summary>
        public List<CanContents> CansIn(Container box, string id)
        {
            if (!box || !boxCans.TryGetValue(box, out var d)) return null;
            return d.TryGetValue(id, out var l) ? l : null;
        }

        /// <summary>A can's contents that left the pack this frame (null: none moved, an empty one).</summary>
        CanContents TakeTransit(string id)
        {
            if (!canPool.TryGetValue(id, out var p) || p.Count == 0) return null;
            var c = p[p.Count - 1];
            if (!pooledFrame.TryGetValue(c, out int f) || f != Time.frameCount) return null;
            p.RemoveAt(p.Count - 1); pooledFrame.Remove(c);
            return c;
        }

        void PushTransit(string id, CanContents c)
        {
            if (c == null || c.Empty) return;
            if (!canPool.TryGetValue(id, out var p)) canPool[id] = p = new List<CanContents>();
            p.Add(c); pooledFrame[c] = Time.frameCount;
        }

        /// <summary>From FluidsUpdate: hook storage as it appears, settle what was taken from the world, resolve loaded lines.</summary>
        void CanStatesTick()
        {
            if (Time.time < canHookAt) return;
            canHookAt = Time.time + 0.5f;
            foreach (var c in Container.All) if (c && !boxCans.ContainsKey(c)) HookBox(c);
            List<Container> gone = null;
            foreach (var kv in boxCans) if (!kv.Key) (gone ??= new List<Container>()).Add(kv.Key);
            if (gone != null) foreach (var c in gone) boxCans.Remove(c);                                // broken storage: its cans went with it
            List<WorldItem> taken = null;
            foreach (var kv in itemCans) if (!kv.Key || kv.Key.count <= 0) (taken ??= new List<WorldItem>()).Add(kv.Key);
            if (taken != null) foreach (var w in taken) itemCans.Remove(w);
            if (pendingCanLines.Count > 0)
            {
                for (int i = pendingCanLines.Count - 1; i >= 0; i--)
                {
                    var a = pendingCanLines[i];
                    if (a[0] != "w") continue;
                    var at = ParseV(a[2]);
                    foreach (var w in WorldItem.All)
                    {
                        if (!w || w.key != a[1] || (w.transform.position - at).sqrMagnitude > 0.5f * 0.5f) continue;
                        var l = ItemList(w);
                        if (l.Count >= w.count) continue;
                        l.Add(ParseCan(a[3], a[4]));
                        pendingCanLines.RemoveAt(i);
                        break;
                    }
                }
                if (Time.time > pendingCanUntil)                                                       // never matched: back to the pack's pool
                {
                    foreach (var a in pendingCanLines)
                    {
                        var c = ParseCan(a[3], a[4]);
                        string id = a[0] == "b" ? a[2] : a[1];
                        if (c.Empty || !FluidContainers.Is(id)) continue;
                        if (!canPool.TryGetValue(id, out var p)) canPool[id] = p = new List<CanContents>();
                        p.Add(c);
                    }
                    pendingCanLines.Clear();
                }
            }
        }

        void HookBox(Container box)
        {
            var d = new Dictionary<string, List<CanContents>>();
            boxCans[box] = d;
            var piece = box.GetComponent<Placeable>();
            string pid = piece ? piece.Id.ToString(CultureInfo.InvariantCulture) : null;
            foreach (var def in FluidContainers.All)
            {
                int n = box.inventory.GetItem(def.id);
                if (n <= 0) continue;
                var l = d[def.id] = new List<CanContents>();
                for (int i = pendingCanLines.Count - 1; i >= 0 && l.Count < n && pid != null; i--)
                {
                    var a = pendingCanLines[i];
                    if (a[0] == "b" && a[1] == pid && a[2] == def.id) { l.Add(ParseCan(a[3], a[4])); pendingCanLines.RemoveAt(i); }
                }
                while (l.Count < n) l.Add(new CanContents());
            }
            box.inventory.Delta += (id, t, n) => BoxDelta(box, id, n);
        }

        void BoxDelta(Container box, string id, int n)
        {
            if (id == null || !FluidContainers.Is(id) || !boxCans.TryGetValue(box, out var d)) return;
            if (!d.TryGetValue(id, out var l)) d[id] = l = new List<CanContents>();
            for (int i = 0; i < n; i++) l.Add(TakeTransit(id) ?? new CanContents());
            for (int i = 0; i < -n && l.Count > 0; i++) { var c = l[l.Count - 1]; l.RemoveAt(l.Count - 1); PushTransit(id, c); }
        }

        List<CanContents> ItemList(WorldItem w)
        {
            if (!itemCans.TryGetValue(w, out var l)) itemCans[w] = l = new List<CanContents>();
            return l;
        }

        /// <summary><paramref name="n"/> containers of the world item's id just left the pack onto it.</summary>
        public void CanDropped(WorldItem w, int n)
        {
            if (!w || !FluidContainers.Is(w.key)) return;
            var l = ItemList(w);
            for (int i = 0; i < n; i++) l.Add(TakeTransit(w.key) ?? new CanContents());
        }

        /// <summary><paramref name="n"/> containers are about to go from the world item into the pack: their contents
        /// go first (the pack's gain takes them back).</summary>
        public void CanTaken(WorldItem w, int n)
        {
            if (!w || !itemCans.TryGetValue(w, out var l)) return;
            for (int i = 0; i < n && l.Count > 0; i++) { var c = l[l.Count - 1]; l.RemoveAt(l.Count - 1); PushTransit(w.key, c); }
        }

        void SaveCanStates(List<string> into)
        {
            string L(CanContents c) => c.litres.ToString("0.###", CultureInfo.InvariantCulture) + "\u001f" + c.mix.Save();
            foreach (var kv in boxCans)
            {
                if (!kv.Key) continue;
                var piece = kv.Key.GetComponent<Placeable>();
                foreach (var e in kv.Value)
                    foreach (var c in e.Value)
                    {
                        if (c.Empty) continue;
                        if (piece) into.Add("b\u001f" + piece.Id.ToString(CultureInfo.InvariantCulture) + "\u001f" + e.Key + "\u001f" + L(c));
                        else into.Add("p\u001f" + e.Key + "\u001f" + L(c));                            // not restored as a piece: back to the pool
                    }
            }
            foreach (var kv in itemCans)
            {
                if (!kv.Key || kv.Key.count <= 0) continue;
                var p = kv.Key.transform.position;
                string at = p.x.ToString("R", CultureInfo.InvariantCulture) + "," + p.y.ToString("R", CultureInfo.InvariantCulture) + "," + p.z.ToString("R", CultureInfo.InvariantCulture);
                foreach (var c in kv.Value) if (!c.Empty) into.Add("w\u001f" + kv.Key.key + "\u001f" + at + "\u001f" + L(c));
            }
            foreach (var a in pendingCanLines) into.Add(string.Join("\u001f", a));                     // not resolved yet: keep them
        }

        /// <summary>A saved "b" / "w" line (true) waits until its storage or world item is there.</summary>
        bool LoadCanState(string line)
        {
            if (!(line.StartsWith("b\u001f") || line.StartsWith("w\u001f"))) return false;
            var a = line.Split('\u001f');
            if (a.Length >= 5) pendingCanLines.Add(a);
            pendingCanUntil = Time.time + 60f;
            return true;
        }

        void ClearCanStates() { boxCans.Clear(); itemCans.Clear(); pooledFrame.Clear(); pendingCanLines.Clear(); }

        static CanContents ParseCan(string litres, string mix)
        {
            var c = new CanContents();
            float.TryParse(litres, NumberStyles.Float, CultureInfo.InvariantCulture, out c.litres);
            c.mix.Load(mix);
            if (c.mix.Empty) c.litres = 0f;
            return c;
        }

        static Vector3 ParseV(string s)
        {
            var f = s.Split(',');
            if (f.Length != 3) return Vector3.zero;
            float.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
            float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z);
            return new Vector3(x, y, z);
        }
    }
}
