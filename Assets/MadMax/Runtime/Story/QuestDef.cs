using System.Collections.Generic;

namespace MadMax.Story
{
    /// <summary>Campaign arcs (storyline.md): A Dead Air, B A Place at the Bend, C The Price of Passage, F the finale,
    /// L the Last Engine epic, S standalone side quests, P personal chains.</summary>
    public enum Arc { A, B, C, F, L, S, P }

    /// <summary>How far a quest is built: Outline = in the graph with its dependencies but no playable steps yet;
    /// Playable = every step and reward works.</summary>
    public enum Build { Outline, Playable }

    /// <summary>What finishes a step. Every kind reads live game state or a noted event, never a timer.</summary>
    public enum Goal
    {
        Reach,      // be within radius of an anchor (on foot or driving)
        Talk,       // choose the quest's topic with the named cast member (event "talk:{npc}:{key}")
        Have,       // carry n of an item id (or "res:N" for a resource)
        Build,      // a placed piece of the id within radius of an anchor
        Craft,      // a crafting job with the output id finished (event "craft:{id}")
        Drive,      // drive n metres in a fleet vehicle
        Event,      // any other noted event key (Story.Note)
    }

    public class Condition
    {
        public Goal goal;
        public string key;          // anchor / npc / item / piece / event
        public string topic;        // Talk: topic key
        public float amount = 1f;   // Have: count, Drive: metres, Reach/Build: radius
        public string label;        // shown when a step has alternatives
        public string say, reply;   // Talk: the player's line and the answer
        public string requires;     // Talk: the topic only shows while carrying this item
    }

    public class StepDef
    {
        public string id, text;
        /// <summary>Any one finishes the step; the one that did is recorded as the step's route.</summary>
        public readonly List<Condition> any = new List<Condition>();
        public bool optional;
        public string waypoint;     // anchor to point the map at while the step is current
        public Reward reward;       // paid when the step finishes
    }

    public class ChoiceDef
    {
        public string id, label, consequence;
    }

    /// <summary>What a quest or step hands over. Paid once per (quest, step) through the ledger.</summary>
    public class Reward
    {
        public int scrap;
        public readonly List<(string item, int n)> items = new List<(string, int)>();
        public readonly List<(MadMax.Items.ResourceType type, int n)> resources = new List<(MadMax.Items.ResourceType, int)>();
        public readonly List<(MadMax.RPG.Skill skill, float xp)> training = new List<(MadMax.RPG.Skill, float)>();
        public string flag;         // a world flag set on payment ("car_owned", ...)
        public readonly List<(string item, int n)> take = new List<(string, int)>();   // handed over by the player
        public bool Empty => scrap == 0 && items.Count == 0 && resources.Count == 0 && training.Count == 0 && flag == null && take.Count == 0;
    }

    public class QuestDef
    {
        public string id, title, giver, summary;
        public Arc arc;
        public Build build;
        public bool storyOnly = true;
        /// <summary>Quests that must be done first (all of them).</summary>
        public readonly List<string> after = new List<string>();
        /// <summary>System keys (Systems register) the quest's verbs need.</summary>
        public readonly List<string> needs = new List<string>();
        public readonly List<StepDef> steps = new List<StepDef>();
        public readonly List<ChoiceDef> choices = new List<ChoiceDef>();
        public Reward reward = new Reward();
        public string hook;         // journal line when the quest opens
        public string offerSay, offerReply;   // the giver's dialogue topic that starts the quest
        public string payoff;       // journal line when it is done
    }
}
