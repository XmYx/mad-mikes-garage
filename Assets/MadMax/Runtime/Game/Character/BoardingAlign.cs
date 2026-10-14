using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Getting in and out of a vehicle (WastelandGame.Boarding): clip names and every alignment number in one
    /// place, matching the clip contract (CLIPS.md, "Vehicles (seat frame)"). The driver door is on the LEFT (-x);
    /// "_r" clips are the right side (authored; mirrored by HumanClip.Mirror when missing); "_high" clips are for seats
    /// whose hips sit more than <see cref="HighSeat"/> above the ground (trucks, bus, APC, semi, cranes).
    /// Seat frame = the root where sit_drive puts it (seat hip − 0.94 h, facing the car's forward). car_enter / car_exit
    /// keep the root there and their pelvis offsets carry the body; the stand spot (where car_open_door plays, car_enter
    /// starts and car_exit ends) is seat-frame (<see cref="DoorSide"/>, <see cref="DoorForward"/>) × h, the clips assume a
    /// seat hip <see cref="SeatHip"/> × h above the ground. A real vehicle differs: while the clip's ground curve says the
    /// feet carry the body outside, the root is lowered by the seat-height difference and shifted by the stand-spot
    /// difference (the spot pushed out until a standing body fits). The door hinge follows the clip's door curve; the
    /// door sounds come from its door_open / door_shut events, the base pose switches at seated / standing.</summary>
    public static class BoardingAlign
    {
        public const string OpenDoor = "car_open_door", Enter = "car_enter", CloseDoor = "car_close_door", Exit = "car_exit";
        /// <summary>Hip height above the ground (m) from which the *_high clips play.</summary>
        public const float HighSeat = 0.75f;
        /// <summary>The clips' stand spot, seat frame, × h (lateral away from the seat on the door side; along the car,
        /// + forward): cars beside the door's rear edge, behind its sweep; _high beside the steps.</summary>
        public const float DoorSide = 0.60f, DoorForward = -0.45f, DoorSideHigh = 0.90f, DoorForwardHigh = 0f;
        /// <summary>Seat-hip height above the ground the clips assume, × h (car, _high).</summary>
        public const float SeatHip = 0.52f, SeatHipHigh = 1.45f;
        /// <summary>The stand spot is pushed out by this much at a time (up to <see cref="DoorPushMax"/>) until a standing body fits.</summary>
        public const float DoorPushStep = 0.15f, DoorPushMax = 0.9f;
        /// <summary>The walk to the stand spot: close enough (m), give up after (s), stuck after (s without progress).</summary>
        public const float Arrive = 0.3f, WalkTimeout = 4f, WalkStuck = 0.5f;
        /// <summary>Last bit of the way (slide + turn to face the car's forward), s.</summary>
        public const float AlignTime = 0.22f;
        /// <summary>Without a door curve in the clip: open between these shares of car_open_door, shut between these of car_close_door.</summary>
        public const float OpenFrom = 0.3f, OpenTo = 0.8f, CloseFrom = 0.35f, CloseTo = 0.85f;
        /// <summary>Door swing (degrees about the front hinge; mirrored doors use the same value) = the clips' door 1.</summary>
        public const float DoorAngle = -68f;
        /// <summary>Clips without a ground curve: the root moves from the start fit to the end fit over this share.</summary>
        public const float RootFrom = 0.15f, RootTo = 0.9f;
        /// <summary>Durations when a clip is missing (procedural stand-ins), s.</summary>
        public const float OpenTime = 0.7f, EnterTime = 0.9f, EnterTimeHigh = 1.3f, CloseTime = 0.55f, ExitTime = 0.85f, ExitTimeHigh = 1.25f;

        public static HumanClip Get(string clip) => clip != null ? HumanClips.Get(clip) : null;
        /// <summary>The clip to play (high / right variant when it exists or can be mirrored, else the plain one); null =
        /// none installed (procedural stand-in).</summary>
        public static string Clip(string baseName, bool right, bool high)
        {
            if (!HumanClips.Enabled) return null;
            if (high)
            {
                string h = right ? baseName + "_high_r" : baseName + "_high";
                if (HumanClips.Get(h) != null) return h;
            }
            string n = right ? baseName + "_r" : baseName;
            return HumanClips.Get(n) != null ? n : null;
        }

        public static float Length(string clip, float fallback) => clip != null && HumanClips.Get(clip) is HumanClip c ? c.length : fallback;

        /// <summary>A clip's pelvis offset (height-1 metres, body frame) at <paramref name="t"/>; zero without the clip.</summary>
        public static Vector3 Pelvis(string clip, float t)
        {
            var c = clip != null ? HumanClips.Get(clip) : null;
            if (c == null) return Vector3.zero;
            c.Sample(t, tmp, out var p);
            return p;
        }

        static readonly Quaternion[] tmp = new Quaternion[HumanClip.Bones];
    }
}
