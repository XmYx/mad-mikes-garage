using System.Collections.Generic;

namespace MadMax.Story
{
    /// <summary>A public recital or ceremony (story system "performance"): where it is played (a stage anchor), who
    /// performs there, which cast members come down to watch, how many townsfolk gather, whether a placed radio has to
    /// be playing near the stage, and a short run of beats (someone's line, the crowd, a sound). The game side
    /// (<see cref="MadMax.Game.PerformanceScene"/>) gathers everyone, plays the beats in order and notes
    /// "&lt;key&gt;:done" after the last one. It only ever starts because the player said so, and nothing in it can
    /// run out: when the player walks away, a performer is missing, the music is off or the quest's own
    /// <see cref="hold"/> says so, it waits and says why.</summary>
    public class Performance
    {
        public struct Beat
        {
            /// <summary>A cast key, <see cref="Crowd"/> (a few of the audience call out) or null (narration only).</summary>
            public string who;
            public string line;
            public float seconds;
            /// <summary>A sound played at the stage as the beat starts (Sfx key), or null.</summary>
            public string sfx;
        }

        public const string Crowd = "crowd";

        /// <summary>Event prefix ("s08:ceremony" → "s08:ceremony:done").</summary>
        public string key;
        /// <summary>What the toasts call it ("THE WEDDING").</summary>
        public string title;
        /// <summary>The stage anchor; performers line up across it, facing its yaw.</summary>
        public string stage;
        public readonly List<string> performers = new List<string>();
        /// <summary>Cast members who come and stand at the front of the crowd.</summary>
        public readonly List<string> audience = new List<string>();
        /// <summary>Townsfolk who gather to watch (generated, sent home afterwards).</summary>
        public int extras = 6;
        /// <summary>How far in front of the stage the audience stands (m).</summary>
        public float crowdAt = 6f;
        /// <summary>Music from a placed radio switched on within <see cref="RadioReach"/> m of the stage.</summary>
        public bool radio;
        public const float RadioReach = 16f;
        public readonly List<Beat> beats = new List<Beat>();
        /// <summary>A quest's own reason to pause (null = carry on): "THE ORGAN HAS NO POWER".</summary>
        public System.Func<string> hold;
        /// <summary>Called as each beat starts (index): the quest's own effects (an organ chord, a bell).</summary>
        public System.Action<int> onBeat;
        /// <summary>Called once when the last beat has ended (before the done event).</summary>
        public System.Action onDone;

        public Performance Line(string who, string line, float seconds = 4.5f, string sfx = null)
        {
            beats.Add(new Beat { who = who, line = line, seconds = seconds, sfx = sfx });
            return this;
        }

        /// <summary>Running time of all beats (s): what the player sits through, not a limit.</summary>
        public float Length { get { float t = 0f; foreach (var b in beats) t += b.seconds; return t; } }

        public string DoneEvent => key + ":done";
    }
}
