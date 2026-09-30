using System.Collections.Generic;
using MadMax.Game;

namespace MadMax.Story
{
    /// <summary>What a cast member can talk about right now: the Talk steps waiting on them and the quests they offer.
    /// <see cref="MadMax.Npc.Dialogue"/> adds these to its hub; choosing one notes the event or starts the quest.</summary>
    public static class StoryTalk
    {
        public struct Topic { public string say, reply; public System.Action act; }

        public static List<Topic> Topics(WastelandGame g, MadMax.Npc.Npc npc)
        {
            var list = new List<Topic>();
            string key = StoryCast.KeyOf(npc);
            if (key == null) return list;
            foreach (var q in StoryLibrary.All)
            {
                if (!Story.Runs(q)) continue;
                var st = Story.StateOf(q.id);
                if (st == Story.State.Active)
                {
                    foreach (var s in q.steps)
                    {
                        if (Story.StepDone(q.id, s.id) || (s != Story.Current(q) && !s.optional)) continue;
                        foreach (var c in s.any)
                            if (c.goal == Goal.Talk && c.key == key && c.say != null)
                            {
                                string note = "talk:" + key + ":" + c.topic;
                                list.Add(new Topic { say = c.say, reply = c.reply, act = () => Story.Note(note) });
                            }
                    }
                }
                else if (st == Story.State.Open && q.giver == key && q.offerSay != null)
                {
                    string id = q.id;
                    list.Add(new Topic { say = q.offerSay, reply = q.offerReply, act = () => Story.Activate(g, id) });
                }
            }
            return list;
        }
    }
}
