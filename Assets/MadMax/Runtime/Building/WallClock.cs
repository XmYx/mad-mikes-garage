using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Wall clock: hour and minute hands follow the game time; looking at it shows the time and day.</summary>
    public class WallClock : MonoBehaviour, IInteractable
    {
        static Mesh hourMesh, minuteMesh;
        Transform hour, minute;

        public string Prompt(MadMax.Game.WastelandGame g) => Time12() + "  DAY " + (DayNight.Day + 1);
        public void Use(MadMax.Game.WastelandGame g, bool secondary) => g.Toast("IT'S " + Time12() + ", DAY " + (DayNight.Day + 1));

        static string Time12()
        {
            int m = Mathf.FloorToInt(DayNight.Hours * 60f) % 1440;
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }

        void Start()
        {
            if (!hourMesh) hourMesh = Hand(3, "Furniture_clock_hour");
            if (!minuteMesh) minuteMesh = Hand(4, "Furniture_clock_minute");
            var mat = GetComponent<MeshRenderer>().sharedMaterial;
            hour = Make("Hour", hourMesh, mat, 2f);
            minute = Make("Minute", minuteMesh, mat, 3f);
        }

        Transform Make(string name, Mesh mesh, Material mat, float y)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, y * VoxelMesher.DefaultSize, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Mesh Hand(int length, string name)
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.Box(0, 0, 0, 0, 0, length, Pal.Ramp(Pal.Black, 1));
            return VoxelMesher.Build(g, name);
        }

        void Update()
        {
            if (!hour) return;
            // the face is the XZ plane (+Z up the wall, +Y out of it): +Y rotation turns clockwise for the viewer
            float h = DayNight.Hours % 12f;
            hour.localRotation = Quaternion.Euler(0f, h * 30f, 0f);
            minute.localRotation = Quaternion.Euler(0f, (DayNight.Hours % 1f) * 360f, 0f);
        }
    }
}
