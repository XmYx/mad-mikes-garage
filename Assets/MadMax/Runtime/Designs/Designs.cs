using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    public class PartDesign
    {
        public string key;
        public PartCategory category;
        public VoxelGrid grid;
        public int sizeClass = 1;
        public float mass = 25f;
        public float radius;
        // engine
        public float torque, maxRpm, peakAt = 0.6f;
        // wheel (0 = defaults: wetGrip 0.8, rolling 1, wearRate 1, footprint 1)
        public float grip, mudGrip, width, wetGrip, rolling, wearRate, footprint;
        /// <summary>Hinged pieces (machine booms, sticks, buckets, blades, crane booms): voxels labelled with the
        /// segment name become a child object pivoting at <c>pivot</c> (part voxel coords) under its parent segment.</summary>
        public readonly List<SegmentDesign> segments = new List<SegmentDesign>();
        public PartDesign Segment(string name, Vector3Int pivot, string parent = null) { segments.Add(new SegmentDesign { name = name, pivot = pivot, parent = parent }); return this; }
    }

    public class SegmentDesign
    {
        public string name, parent;
        public Vector3Int pivot;
    }

    public class SocketDesign
    {
        public string name;
        public PartCategory accepts;
        public Vector3Int position;   // voxel coordinates on the chassis
        public bool mirrored;
        public string part;           // part key mounted by default (null = empty socket)
        public int maxSizeClass = 4;
    }

    /// <summary>Walk-in interior (voxel coordinates). The player walks on floorY inside min..max (x,z).</summary>
    public class InteriorDesign
    {
        public float floorY, ceilingY;
        public Vector2 min, max;
        public readonly List<(Vector2 inside, Vector3 outside)> doors = new List<(Vector2, Vector3)>();
        public Vector2 seat, stand;              // driver seat and where you stand up to
        public readonly List<BoundsInt> obstacles = new List<BoundsInt>();
        public readonly List<(string id, Vector3 pos, Vector3 euler)> furniture = new List<(string, Vector3, Vector3)>();
    }

    public class VehicleDesign
    {
        public string name;
        public float mass = 900f;
        public VehicleDriver.Drive drive = VehicleDriver.Drive.Rear;
        public float travel = 0.28f, finalDrive = 3.7f, frequency = 1.7f, brakeForce = 16000f, maxSteer = 32f;
        public float[] gears;                  // null = VehicleDriver default
        public bool driveable = true;          // false for trailers
        public bool awdSelectable, diffLock;   // driver-switchable 4WD, lockable differentials
        public float fuelL = 60f, oilL = 5f, coolantL = 8f;
        public bool usesCoolant = true, oilInFuel;
        public Vector3Int? hitch;              // tow ball (voxel coords)
        public Vector3Int? coupler;            // trailer drawbar eye (voxel coords)
        public float pumpLps;                  // fuel tanker: pump rate (litres/s); 0 = not a tanker
        public bool medical;                   // ambulance: the walk-in bay treats whoever is inside (MedicalBay)
        public float cargoKg;                  // box trailer / van: a cargo hold of this many kg on the vehicle (Container)
        public VoxelGrid roof;                 // separate mesh, hidden for interior cutaway
        public readonly List<BoundsInt> colliders = new List<BoundsInt>();   // voxel-space boxes; empty = one box around the body
        public InteriorDesign interior;
        public string machine;                 // construction machine behaviour (MadMax.Vehicles.Machine.Kind name)
        public bool crawler;                   // tracked: wheels hide inside the tracks (no arch carving)
        public bool bike, sidecar;             // two-wheeler (BikeBalance leans it); sidecar outfit: three wheels, no lean
        public float comX;                     // centre of mass offset (m) to the right (sidecar outfits)
        public Vector3Int? passenger;          // pillion / chair passenger eye (voxel coords); null = beside the driver
        public Vector3Int eye;       // driver eye, voxel coordinates
        public VoxelGrid body;
        public VoxelGrid glass;
        public VoxelGrid legs;
        /// <summary>Moving sub-bodies (decks, ramps): child objects with their own mesh + box collider, placed at pivot with a start rotation.</summary>
        public readonly List<(string name, VoxelGrid grid, Vector3Int pivot, Vector3 euler)> movable = new List<(string, VoxelGrid, Vector3Int, Vector3)>();       // trailer landing legs: raised while coupled
        public VoxelGrid driver;     // seated occupant, shown only while occupied      // separate mesh so first-person view can hide it
        public readonly List<SocketDesign> sockets = new List<SocketDesign>();
        public readonly List<PartDesign> parts = new List<PartDesign>();   // vehicle-specific panels cut from the body

        public static BoundsInt Box(int x0, int y0, int z0, int x1, int y1, int z1) =>
            new BoundsInt(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Min(z0, z1), Mathf.Abs(x1 - x0) + 1, Mathf.Abs(y1 - y0) + 1, Mathf.Abs(z1 - z0) + 1);

        public void Socket(string name, PartCategory c, int x, int y, int z, string part, bool mirrorPair = false, int maxSize = 4)
        {
            sockets.Add(new SocketDesign { name = mirrorPair ? name + "_R" : name, accepts = c, position = new Vector3Int(x, y, z), part = part, maxSizeClass = maxSize });
            if (mirrorPair)
                sockets.Add(new SocketDesign { name = name + "_L", accepts = c, position = new Vector3Int(-x, y, z), part = part, mirrored = true, maxSizeClass = maxSize });
        }

        /// <summary>Carves wheel arches out of the body (and glass): for every wheel socket, the space its default tyre
        /// sweeps (radius + 1 voxel, full width, suspension travel upward, steering clearance on steered axles) is cleared,
        /// and the arch rim is darkened like an inner fender. Keeps any design free of tyres poking through panels.</summary>
        public void CarveWheelArches(System.Func<string, PartDesign> part)
        {
            if (body == null || crawler || bike) return;                                   // bikes: fenders sit above the travel by design
            float travelVox = Mathf.Ceil(travel / VoxelMesher.DefaultSize);
            foreach (var s in sockets)
            {
                if (s.accepts != PartCategory.Wheel || string.IsNullOrEmpty(s.part)) continue;
                var w = part(s.part);
                if (w == null || w.radius <= 0f) continue;
                float r = w.radius / VoxelMesher.DefaultSize;
                int minX = int.MaxValue, maxX = int.MinValue;
                foreach (var k in w.grid.voxels.Keys) { if (k.x < minX) minX = k.x; if (k.x > maxX) maxX = k.x; }
                int width = maxX >= minX ? maxX - minX + 1 : 3;
                int sign = s.mirrored ? -1 : 1;
                int x0 = s.position.x, x1 = s.position.x + sign * (width + 1);   // inner face to beyond the outer sidewall
                if (x0 > x1) (x0, x1) = (x1, x0);
                float zMargin = driveable && maxSteer > 0f && s.position.z > 0 ? 1.5f : 0.5f;   // steered wheels swing forward/back
                foreach (var g in new[] { body, glass })
                {
                    if (g == null) continue;
                    var clear = new List<Vector3Int>(); var rim = new List<Vector3Int>();
                    foreach (var p in g.voxels.Keys)
                    {
                        if (p.x < x0 || p.x > x1) continue;
                        float dz = Mathf.Max(0f, Mathf.Abs(p.z - s.position.z) - zMargin);
                        float dy = p.y - s.position.y;
                        float ey = dy - Mathf.Clamp(dy, 0f, travelVox);      // capsule: the wheel rises by the suspension travel
                        float d = Mathf.Sqrt(dz * dz + ey * ey);
                        if (d <= r + 1f) clear.Add(p); else if (d <= r + 2f && g == body) rim.Add(p);
                    }
                    foreach (var p in clear) g.voxels.Remove(p);
                    foreach (var p in rim)
                    {
                        var v = g.voxels[p];
                        v.color = new Color32((byte)(v.color.r * 0.55f), (byte)(v.color.g * 0.55f), (byte)(v.color.b * 0.55f), v.color.a);
                        g.voxels[p] = v;
                    }
                }
            }
        }

        /// <summary>Cut a labelled region out of the body grid into its own part.</summary>
        public void Cut(VoxelGrid g, string label, string key, PartCategory c, Vector3Int origin, float mass, int size = 1)
        {
            parts.Add(new PartDesign { key = key, category = c, grid = g.Extract(label, origin), mass = mass, sizeClass = size });
        }
    }
}
