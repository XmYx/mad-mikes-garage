using MadMax.Story;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>A6 TELL IT STRAIGHT: the Remnant checkpoint (a concrete hut of shelves where Dr. Ivo Rusk keeps the
    /// archive, sandbags, a flag), the script or the sealed dossier handed over as the decision allows, the rehearsal and
    /// the broadcast (<see cref="Broadcast"/>), and Mara's decision played out: a room at the garage, a companion, or the
    /// north road. Afterwards WasteTalk follows the story up for two days.</summary>
    public partial class WastelandGame
    {
        int a6AiredDay = -2;

        partial void Scene_A6()
        {
            if (!StoryAnchors.Has("a6_archive") || !Build || !Build.Structures) return;
            // a 4 x 4 m concrete hut, door to the road, roofed
            PutAt("a6_archive", "wall_concrete", new Vector3(-1f, 0f, 2f), 0f);
            PutAt("a6_archive", "doorway_concrete", new Vector3(1f, 0f, 2f), 0f);
            PutAt("a6_archive", "wall_concrete", new Vector3(-1f, 0f, -2f), 180f);
            PutAt("a6_archive", "wall_concrete", new Vector3(1f, 0f, -2f), 180f);
            foreach (float z in new[] { -1f, 1f }) { PutAt("a6_archive", "wall_concrete", new Vector3(-2f, 0f, z), -90f); PutAt("a6_archive", "wall_concrete", new Vector3(2f, 0f, z), 90f); }
            var a = StoryAnchors.Get("a6_archive"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("a6_archive"), 0f);
            float y = terrain.HeightNoLoad(a.x, a.z);
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                {
                    var p = a + r * new Vector3(x, 0f, z); p.y = y + 2.42f;
                    var roof = MadMax.Building.FurnitureLibrary.Spawn("roof_flat", Build.Structures, p, r, propMaterial);
                    if (roof) storyProps.Add(roof.Id);
                }
            // the archive inside: shelves, a table, a lamp and a radio
            PutAt("a6_archive", "bookshelf", new Vector3(-1.2f, 0f, -1.55f), 0f);
            PutAt("a6_archive", "bookshelf", new Vector3(0.4f, 0f, -1.55f), 0f);
            PutAt("a6_archive", "table", new Vector3(0.9f, 0f, -0.2f), 90f);
            PutAt("a6_archive", "lamp", new Vector3(1.5f, 0f, 1.3f), 0f);
            PutAt("a6_archive", "radio", new Vector3(-1.5f, 0f, 1.2f), 90f);
            // the checkpoint outside: sandbags either side, the archive's flag, a sign
            PutAt("a6_archive", "sandbag_wall", new Vector3(-3.4f, 0f, 4.2f), 0f);
            PutAt("a6_archive", "sandbag_wall", new Vector3(4f, 0f, 4.2f), 0f);
            PutAt("a6_archive", "flag", new Vector3(-3.6f, 0f, 2.6f), 0f);
            PutAt("a6_archive", "sign", new Vector3(3.6f, 0f, 2.8f), 0f);
            PutAt("a6_archive", "barrel", new Vector3(3.2f, 0f, -1.2f), 0f);
            Journal.Add("PLACE", "THE REMNANT CHECKPOINT: DR. IVO RUSK KEEPS THE BUNKER ARCHIVE IN A CONCRETE HUT BY THE ROAD");
        }

        partial void Tick_A6()
        {
            var q = StoryLibrary.Get("A6");
            if (q == null) return;
            if (StoryAnchors.Has("a6_archive")) Q7Pose("ivo_archive", Q7At("a6_archive", -0.3f, -0.4f), StoryAnchors.Yaw("a6_archive"));
            if (Time.frameCount % 15 != 0) return;
            string decide = Story.Story.Route("A6", "decide"), mara = Story.Story.Route("A6", "mara");
            q.payoff = StoryLibrary.A6_Payoff(decide, mara);
            var kind = StoryLibrary.A6Kind(decide);
            // the paper that lets the next step happen: a script (a redacted one needs Wes's logbook first) or the dossier
            if (kind != null && !Story.Story.Flag("a6:paper"))
            {
                if (kind == Broadcast.Kind.Held) { Story.Story.SetFlag("a6:paper"); Inventory.AddItem(StoryLibrary.A6Dossier); }
                else if (kind == Broadcast.Kind.Full || Story.Story.StepDone("A6", "logbook")) { Story.Story.SetFlag("a6:paper"); Inventory.AddItem(StoryLibrary.A6Script); Toast("JUNE HANDS YOU THE SCRIPT"); }
            }
            if (Story.Story.Flag("a6:paper") && kind != null && !Story.Story.StepDone("A6", "release"))                      // June keeps a copy of whatever you lose
            {
                string paper = kind == Broadcast.Kind.Held ? StoryLibrary.A6Dossier : StoryLibrary.A6Script;
                if (Inventory.GetItem(paper) == 0 && (kind != Broadcast.Kind.Held || !Story.Story.StepDone("A6", "prepare"))) Inventory.AddItem(paper);
            }
            var prep = StoryLibrary.Q7Step("A6", "prepare");
            if (prep != null && kind != null && !Story.Story.StepDone("A6", "prepare"))
                prep.text = kind == Broadcast.Kind.Held ? "SEAL THE RECORDS WITH JUNE: ONE COPY FOR IVO'S VAULT, ONE FOR THE TABLE"
                    : kind == Broadcast.Kind.Redacted && !Story.Story.StepDone("A6", "logbook") ? "REDACTED: FIRST A SECOND SOURCE FOR THE QUANTITIES (WES CALLOWAY'S LOGBOOK, AT THE GARAGE), THEN A RUN-THROUGH WITH JUNE"
                    : "PREPARE THE BROADCAST WITH JUNE: A RUN-THROUGH WITH CAPTIONS";
            var rel = StoryLibrary.Q7Step("A6", "release");
            if (rel != null && kind != null) rel.text = kind == Broadcast.Kind.Held ? "THE SEALED COPY GOES IN IVO'S VAULT. NOTHING GOES ON AIR" : "ON AIR WHEN YOU SAY SO: TELL JUNE";
            // the run-through, the broadcast (or the seal)
            if (Story.Story.StepDone("A6", "prepare") && !Story.Story.Flag("a6:prepared"))
            {
                Story.Story.SetFlag("a6:prepared");
                if (kind == Broadcast.Kind.Held) Story.Story.Note("a6:held");
                else if (kind != null) Broadcast.Rehearse(kind.Value);
            }
            if (Story.Story.StepDone("A6", "release") && kind != null && Broadcast.Aired == null)
            {
                if (kind != Broadcast.Kind.Held) Inventory.TakeItem(StoryLibrary.A6Script);
                Broadcast.Air(this, kind.Value);
                Story.Story.SetFlag("a6:aired_day:" + DayNight.Day);
                a6AiredDay = DayNight.Day;
            }
            // Mara's choice played out, then the last line
            if (mara != null && !Story.Story.Flag("a6:mara"))
            {
                Story.Story.SetFlag("a6:mara");
                Story.Story.SetFlag(mara == StoryLibrary.A6Stay ? "mara_stays" : mara == StoryLibrary.A6Travel ? "mara_travels" : "mara_departs");
                var settled = StoryLibrary.Q7Step("A6", "settled");
                if (settled != null) settled.text = mara == StoryLibrary.A6Stay ? "MARA CARRIES HER BAG INTO THE BACK ROOM" : mara == StoryLibrary.A6Travel ? "MARA THROWS HER BAG IN YOUR CAR" : "MARA WALKS TO THE NORTH ROAD";
                if (mara == StoryLibrary.A6Depart) Inventory.AddItem("keepsake_badge");
                Story.Story.Note("a6:settled");
            }
        }

        /// <summary>After A6: Mara joins the player if asked (once; a dismissed companion goes her own way), WasteTalk
        /// follows the story up the next two days.</summary>
        void A6Aftermath()
        {
            if (Story.Story.StateOf("A6") != Story.Story.State.Done) return;
            if (Story.Story.Flag("mara_travels") && !Story.Story.Flag("mara:recruited") && Player && Time.frameCount % 30 == 0)
            {
                if (!MadMax.Npc.NpcRegistry.IsDead("cast:mara_road") && !MadMax.Npc.Companions.Has("cast:mara_road"))
                {
                    var p = StoryCast.Profile("mara_road", World.seed);
                    var at = (Current ? Current.transform.position : Player.transform.position) - Player.transform.forward * 2.2f + Player.transform.right * 1.2f;
                    at.y = terrain.Height(at.x, at.z) + 0.1f;
                    var n = MadMax.Npc.Npc.Spawn(p, at, Player.transform.eulerAngles.y, null, propMaterial);
                    MadMax.Npc.Companions.Recruit(this, n);
                }
                Story.Story.SetFlag("mara:recruited");
            }
            if (a6AiredDay == -2) a6AiredDay = Q7State.Number("a6:aired_day:", DayNight.Day + 1);
            var kind = Broadcast.Aired;
            if (a6AiredDay < 0 || kind == null || kind == Broadcast.Kind.Held) return;
            if (DayNight.Day >= a6AiredDay + 1 && !Story.Story.Flag("a6:news1"))
            {
                Story.Story.SetFlag("a6:news1");
                MadMax.Audio.RadioNetwork.Flash(kind == Broadcast.Kind.Full
                    ? "CALLERS ON WASTETALK: THREE DRIVERS NAMED ON AIR HAVE BEEN PULLED OFF THEIR ROUTES BY THE GUILD. THE TOWN MEETINGS ARE LOUD"
                    : "CALLERS ON WASTETALK HAVE BEEN CHECKING THEIR OWN DELIVERY BOOKS AGAINST JUNE BELL'S FIGURES. THEY MATCH");
            }
            if (DayNight.Day >= a6AiredDay + 2 && !Story.Story.Flag("a6:news2"))
            {
                Story.Story.SetFlag("a6:news2");
                MadMax.Audio.RadioNetwork.Flash(kind == Broadcast.Kind.Full
                    ? "THE FUEL GUILD CALLS THE BROADCAST RECORDS STOLEN PAPERWORK. IT HAS NOT SAID THEY ARE WRONG"
                    : "THE FUEL GUILD CALLS THE BROADCAST FIGURES UNVERIFIED. THE REMNANT ARCHIVE OFFERS ANYONE A LOOK AT THE LOGBOOK");
            }
        }
    }
}
