using System.Collections;
using System.Linq;
using MadMax.Audio;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Radio: eleven stations from the manifest (ten music, WasteTalk FM 90.1 talk) each have something on air
    /// right now, on one clock and a gapless schedule; the car's head unit switches on and tunes with the radio keys (the
    /// production key path); reception is full near a mast and fades far from every mast (a set placed out there says
    /// WEAK SIGNAL); a news flash is carried.</summary>
    class RadioStations : Scenario
    {
        public override string Id => "radio.stations";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var net = RadioNetwork.Instance;
            if (!c.Check(net && RadioNetwork.Manifest != null, "the radio network is up")) yield break;
            var stations = RadioNetwork.Manifest.stations;
            c.Metric("stations", stations.Count, "");
            c.Check(stations.Count == 11, "eleven stations: " + string.Join(", ", stations.Select(s => s.freq + " " + s.name)));
            var talk = stations.Where(s => s.talk).ToList();
            c.Check(talk.Count == 1 && talk[0].freq == "90.1", "one talk station on 90.1: " + string.Join(", ", talk.Select(s => s.freq + " " + s.name)));
            c.Check(stations.Count(s => !s.talk && s.tracks.Count(t => t.kind != "jingle") > 0) == 10, "ten music stations with songs");
            int tracks = stations.Sum(s => s.tracks.Count(t => t.kind != "jingle"));
            c.Metric("songs", tracks, "");
            bool onAir = true, gapless = true;
            for (int i = 0; i < stations.Count; i++)
            {
                var now = net.Now(i, out float off);
                var next = net.Next(i);
                if (now.file == null || string.IsNullOrEmpty(now.file.file) || now.file.len <= 0f || off < 0f || off > now.file.len + 0.05f) { onAir = false; c.Note(stations[i].name + ": nothing on air"); }
                if (next.file == null || System.Math.Abs(next.start - now.End) > 1e-3) { gapless = false; c.Note(stations[i].name + ": gap before the next item"); }
                c.Note($"{stations[i].freq}: {(now.file != null ? now.file.title ?? now.file.file : "-")} ({off:0}/{(now.file != null ? now.file.len : 0f):0} s)");
            }
            c.Check(onAir, "every station has an item on air");
            c.Check(gapless, "each schedule runs gaplessly into the next item");
            var again = net.Now(3, out _);
            c.Check(again.file == net.Now(3, out _).file, "one broadcast clock: asking twice hears the same item");

            // ---- the car radio, by its keys
            var car = TestWorld.Vehicle("Sedan") ?? g.Fleet.FirstOrDefault(v => v && v.driveable);
            if (!c.Check(car, "a fleet car")) yield break;
            if (g.Current != car) { if (g.Current) g.Exit(); yield return new WaitForSeconds(0.3f); g.Enter(car); yield return new WaitForSeconds(0.5f); }
            var radio = RadioReceiver.On(car.gameObject);
            if (radio.on) { ActionPress.Press(Controls.Act.RadioPower); yield return PH.Until(() => !radio.on, 1f); }
            ActionPress.Press(Controls.Act.RadioPower);
            yield return PH.Until(() => radio.on, 1f);
            c.Check(radio.on, "the power key switches the head unit on");
            int st0 = radio.station;
            ActionPress.Press(Controls.Act.RadioNext);
            yield return PH.Until(() => radio.station != st0, 1f);
            c.Check(radio.station == net.Wrap(st0 + 1), $"tune up: station {st0} -> {radio.station} ({radio.StationLabel()})");
            ActionPress.Press(Controls.Act.RadioPrev);
            yield return PH.Until(() => radio.station == st0, 1f);
            c.Check(radio.station == st0, "tune down: back again");
            if (!Application.isBatchMode)
            {
                yield return PH.Until(() => !string.IsNullOrEmpty(radio.NowPlaying), 3f);
                c.Check(!string.IsNullOrEmpty(radio.NowPlaying), "now playing: " + radio.NowPlaying);
                yield return new WaitForSeconds(1.2f);
                c.Check(Mathf.Abs(radio.Signal - BiomeProps.Signal(g.World, car.transform.position)) < 0.01f, $"reception in the car follows the masts ({radio.Signal:0.00})");
            }
            ActionPress.Press(Controls.Act.RadioPower);
            yield return PH.Until(() => !radio.on, 1f);
            c.Check(!radio.on, "and off again");

            // ---- reception by the masts
            var masts = BiomeProps.Landmarks(g.World).Where(m => m.kind == BiomeProps.MarkKind.Mast).ToList();
            if (!c.Check(masts.Count > 0, "radio masts stand outside the towns")) yield break;
            var at = new Vector3(masts[0].pos.x, 0f, masts[0].pos.y);
            c.Check(BiomeProps.Signal(g.World, at) > 0.99f, "full signal at a mast");
            Vector3 far = default; float worst = 2f;
            for (int k = 0; k < 400; k++)
            {
                float a = k * 2.39996f, r = 500f + k * 12f;
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float s = BiomeProps.Signal(g.World, p);
                if (s < worst && g.World.SettlementAt(p.x, p.z) == null) { worst = s; far = p; }
            }
            c.Metric("weakest_signal", worst, "");
            c.Check(worst < 0.5f, $"far from every mast the station fades ({worst:0.00} at {far.x:0},{far.z:0})");
            if (!Application.isBatchMode && worst < 0.5f)
            {
                var set = PH.Piece(g, "radio", far, Vector3.forward);
                var rr = set ? set.GetComponent<RadioReceiver>() : null;
                c.Fixture("a radio set placed out in the waste at " + far.x.ToString("0") + "," + far.z.ToString("0"));
                if (c.Check(rr, "the radio set has a receiver"))
                {
                    if (!rr.on) rr.TogglePower();
                    yield return PH.Until(() => rr.Signal < 0.5f, 3f);
                    c.Check(rr.StationLabel().Contains("WEAK SIGNAL"), "it says so: " + rr.StationLabel());
                    rr.TogglePower();
                }
                if (set) Object.Destroy(set.gameObject);
            }

            // ---- news flash
            RadioNetwork.Flash("TEST FLASH: THE ROAD IS OPEN");
            c.Check(RadioNetwork.FlashOn && RadioNetwork.FlashText == "TEST FLASH: THE ROAD IS OPEN", "WasteTalk carries a news flash");
        }
    }
}
