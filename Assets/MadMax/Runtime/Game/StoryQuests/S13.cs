using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S13 ONE GOOD ROOF in the world: Cal's flat-roofed workshop (4 × 4 m on timber foundations, roof 2.6 m up:
    /// too high to mantle from the ground), the outside stair lying on its side, three crates stacked at the back wall
    /// (mantle onto them, then onto the roof). While it runs: Margo is kept on the roof until she's brought down, the way
    /// up is read from how the player arrived (ladder, hook or climb), and a ladder the player builds against the workshop
    /// can be climbed by walking into it (and down again by crouching at its top).</summary>
    public partial class WastelandGame
    {
        float s13Roof = -9999f, s13HookAt = -99f, s13LadderCd, s13HintAt;
        bool s13ViaLadder, s13Climbing;
        string s13Up, s13Steady, s13Down;
        bool s13Set;

        partial void Scene_S13()
        {
            if (!Build || !Build.Structures || !StoryAnchors.Has("s13_house")) return;
            float y = Q2House("s13_house", Vector3.zero, 2, 2, "wall_wood", "doorway_wood", "wall_wood_window", true);
            // the outside stair that came down under Margo, on its side along the right wall
            var st = Q2Put("s13_house", "stairs", new Vector3(2.4f, 0.6f, 0.2f), 0f, null, new Vector3(0f, 0f, -90f));
            if (st) { st.hits = 2; st.Dirty(); }
            // three crates stacked against the back wall: a climber's way up (clear of the wall so there's room on top)
            for (int i = 0; i < 3; i++) Q2Put("s13_house", "crate", new Vector3(-0.9f, 0.56f * i, -2.85f), i * 7f);
            // roofing sheets waiting on the roof, a barrel by the door
            Q2Put("s13_house", "crate", new Vector3(-1.3f, 2.46f, 1.1f), 15f, y);
            Q2Put("s13_house", "barrel", new Vector3(-3.1f, 0f, 2.6f), 0f);
            Q2Put("s13_house", "tyres", new Vector3(3.4f, 0f, 3.2f), 40f);
            s13Roof = y + 2.46f;
        }

        partial void Tick_S13()
        {
            if (!Player || !StoryAnchors.Has("s13_house")) return;
            var a = StoryAnchors.Get("s13_house"); var rot = Quaternion.Euler(0f, StoryAnchors.Yaw("s13_house"), 0f);
            if (s13Roof < -1000f)                                                                      // after a reload: read it off the roof
            {
                if (Time.frameCount % 30 != 0) return;
                foreach (var p in Placeable.All)
                    if (p && p.id == "roof_flat" && IsStoryProp(p) && Q2Flat(p.transform.position, a) < 2.2f) s13Roof = Mathf.Max(s13Roof, p.transform.position.y + 0.04f);
                if (s13Roof < -1000f) return;
            }

            // Margo stays on the roof until she's brought down (then the cast moves her to the yard)
            if (!Story.Story.StepDone("S13", "down"))
            {
                var spot = a + rot * new Vector3(0.6f, 0f, -0.8f); spot.y = s13Roof + 0.03f;
                Q2Pin(CastBody("s13_margo"), spot, StoryAnchors.Yaw("s13_house") + 150f);
            }

            var pp = Player.transform.position;
            if (Player.Zipping && !s13Climbing) s13HookAt = Time.time;
            if (!Player.Traversing) s13Climbing = false;

            // on the roof: how did they get up?
            if (!Current && !Player.Traversing && !Story.Story.StepDone("S13", "up"))
            {
                var lp = Quaternion.Inverse(rot) * (pp - a);
                if (Mathf.Abs(lp.x) < 2.1f && Mathf.Abs(lp.z) < 2.1f && pp.y > s13Roof - 0.35f)
                    Story.Story.Note(s13ViaLadder ? "s13:up_ladder" : Time.time - s13HookAt < 5f ? "s13:up_hook" : "s13:up_climb");
            }

            // a ladder against the workshop: walk into it to climb, crouch at its top to climb down
            if (!Current && !Player.Traversing && Time.time > s13LadderCd)
                foreach (var l in Ladder.All)
                {
                    if (!l) continue;
                    var lb = l.transform.position;
                    if (Q2Flat(lb, a) > 3.6f) continue;
                    var toC = a - lb; toC.y = 0f;
                    if (toC.sqrMagnitude < 0.01f) continue;
                    toC.Normalize();
                    var off = pp - lb; off.y = 0f;
                    bool atFoot = pp.y < lb.y + 1f && off.magnitude < 1.2f && Vector3.Dot(off, toC) < 0.15f;
                    bool atTop = pp.y > s13Roof - 0.4f && off.magnitude < 1.3f;
                    if (atFoot && Player.moveInput.sqrMagnitude > 0.1f)
                    {
                        var from = lb + toC * 0.9f; from.y = s13Roof + 1.4f;
                        if (Physics.Raycast(from, Vector3.down, out var hit, 2.4f, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f
                            && hit.point.y > s13Roof - 0.4f && !hit.collider.GetComponentInParent<MadMax.Npc.Npc>())
                        {
                            s13Climbing = true;
                            if (Player.Zip(hit.point, hit.normal)) { s13ViaLadder = true; s13LadderCd = Time.time + 1f; MadMax.Audio.Sfx.Play("hammer", lb, 0.25f, 1.6f); }
                            else s13Climbing = false;
                        }
                        break;
                    }
                    if (atTop && Player.crouch)
                    {
                        var down = lb - toC * 0.8f; down.y = terrain.Height(down.x, down.z) + 0.02f;
                        s13Climbing = true;
                        if (Player.Zip(down, Vector3.up)) s13LadderCd = Time.time + 1f; else s13Climbing = false;
                        break;
                    }
                    if ((atFoot || atTop) && Time.time > s13HintAt)
                    {
                        s13HintAt = Time.time + 6f;
                        Toast(atFoot ? "WALK INTO THE LADDER TO CLIMB IT" : Controls.Localize("[CTRL] AT THE LADDER TO CLIMB DOWN"));
                    }
                }

            // the kit used on her ankle leaves the pack; the way down gets a line
            if (!Story.Story.Flag("s13_kit") && Story.Story.StepDone("S13", "steady"))
            {
                Story.Story.SetFlag("s13_kit");
                var r = Story.Story.Route("S13", "steady");
                if (r != null && r.StartsWith("LET ME STRAP")) Inventory.TakeItem("med_bandage");
                else if (r != null && r.StartsWith("SPLINT")) Inventory.TakeItem("med_splint");
            }
            if (!Story.Story.Flag("s13_down_told") && Story.Story.StepDone("S13", "down"))
            {
                Story.Story.SetFlag("s13_down_told");
                var r = Story.Story.Route("S13", "down");
                Toast(r != null && r.StartsWith("I'LL SHOUT") ? "CAL'S CREW ARRIVE WITH THE LONG LADDER AND A GREAT DEAL OF ADVICE" : "MARGO COMES DOWN THE LADDER RUNG BY RUNG, SWEARING POLITELY");
            }

            string up = Story.Story.Route("S13", "up"), std = Story.Story.Route("S13", "steady"), dn = Story.Story.Route("S13", "down");
            if (!s13Set || !ReferenceEquals(up, s13Up) || !ReferenceEquals(std, s13Steady) || !ReferenceEquals(dn, s13Down))
            {
                s13Set = true; s13Up = up; s13Steady = std; s13Down = dn;
                var q = StoryLibrary.Get("S13"); if (q != null) q.payoff = StoryLibrary.S13Payoff();
            }
        }
    }
}
