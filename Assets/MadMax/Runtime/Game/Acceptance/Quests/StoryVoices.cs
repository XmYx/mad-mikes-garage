using System.Collections;
using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Voiced story lines (audio/quest_cast): the quest voice manifest loads; talk replies and offers of many
    /// cast members resolve to clips that exist; in a STORY world, choosing Nell's first topic on the Talk page plays
    /// her line through a 3D source at her head, and closing the page stops it; the opening radio loop has a clip.</summary>
    class StoryVoices : Scenario
    {
        public override string Id => "story.voices";
        public override float Timeout => 90f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            // ---- the manifest and a sample of lines across the cast
            if (!c.Check(QuestVoice.Count > 0, "the quest voice manifest loads (" + QuestVoice.Count + " lines)")) yield break;
            var speakers = new HashSet<string>();
            int lines = 0, voiced = 0, missing = 0;
            foreach (var q in StoryLibrary.All)
            {
                var said = new List<(string key, string text)>();
                if (q.giver != null && q.offerReply != null) said.Add((q.giver, q.offerReply));
                foreach (var s in q.steps) foreach (var cond in s.any) if (cond.goal == Goal.Talk && cond.reply != null) said.Add((cond.key, cond.reply));
                foreach (var (key, text) in said)
                {
                    lines++;
                    string file = QuestVoice.FileFor(key, text, out _);
                    if (file == null) continue;
                    voiced++;
                    if (System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, "Voices", file))) speakers.Add(key);
                    else missing++;
                }
            }
            c.Metric("voiced_story_lines", voiced, "lines");
            c.Metric("story_lines", lines, "lines");
            c.Check(missing == 0, "every resolved line's clip exists on disk (" + missing + " missing)");
            c.Check(speakers.Count >= 20, "lines of " + speakers.Count + " different cast members resolve to clips");
            c.Check(voiced >= lines * 0.8f, $"most story lines are voiced ({voiced}/{lines}; dynamic lines with names or numbers stay silent)");
            c.Check(QuestVoice.Has("radio_mara", "...SEVEN, EIGHT... IF YOU MADE IT, KEEP THE LIGHT ON."), "the opening radio loop is voiced");

            // ---- a story topic on the Talk page plays on the speaker and stops with the page
            // A1 in order: the satchel, then Nell's stop (her topic is offered once the stop is reached)
            yield return H.Walk(c, "satchel", 1.5f);
            H.TakeSatchel(c);
            yield return H.Until(() => MadMax.Story.Story.StepDone("A1", "things"), 4f);
            yield return H.Walk(c, "nell", 4f);
            yield return H.Until(() => g.CastBody("nell") != null && MadMax.Story.Story.StepDone("A1", "stop"), 6f);
            var nell = g.CastBody("nell");
            if (!c.Check(nell, "Nell Mercer is at her stop")) yield break;
            var a1 = StoryLibrary.Get("A1");
            string reply = null;
            foreach (var s in a1.steps) foreach (var cond in s.any) if (cond.topic == "a1_arrive") reply = cond.reply;
            string expect = QuestVoice.FileFor("nell", reply, out float secs);
            if (!c.Check(expect != null, "Nell's first reply has a clip")) yield break;
            g.Menus.OpenTalk(nell, false);
            yield return null;
            if (!c.Check(g.Menus.Pick("I CRAWLED OUT"), "the Talk page offers Nell's story topic")) yield break;
            c.Check(g.Menus.TalkLine == reply, "the reply is shown as the caption");
            float t0 = Time.unscaledTime;                                      // the Talk page pauses game time
            while (!(NpcVoice.Instance && NpcVoice.Instance.StorySpeaker == nell) && Time.unscaledTime - t0 < 5f) yield return null;
            var v = NpcVoice.Instance;
            if (!c.Check(v && v.StorySpeaker == nell, "Nell speaks her reply")) { g.Menus.Close(); yield break; }
            c.Check(v.StoryClip == expect, "the clip is her line: " + v.StoryClip);
            var src = GameObject.Find("NpcVoiceStory");
            var head = nell.Head ? nell.Head : nell.transform;
            c.Check(src && src.GetComponent<AudioSource>().spatialBlend > 0.9f && Vector3.Distance(src.transform.position, head.position) < 0.5f, "through a 3D source at her head");
            c.Metric("clip_seconds", secs, "s");
            yield return new WaitForSecondsRealtime(1f);
            c.Check(v.StorySpeaker == nell, "still speaking a second later (the page is open)");
            g.Menus.Close();
            yield return null; yield return null;
            c.Check(v.StorySpeaker == null, "leaving the page stops the line");
        }
    }

    public static class StoryVoiceTests
    {
        public static IEnumerable<Scenario> All() { yield return new StoryVoices(); }
    }
}
