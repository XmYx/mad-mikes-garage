using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A bridge piece's drivable deck. Bridges are placed from the bank and span forward (local −Z, away from
    /// the builder), so the deck is registered with <see cref="StructureGround"/> around a point half a span out, not
    /// around the piece's origin: wheels roll across it above the ditch or water below. The deck keeps its collider out
    /// of the carried-body list, so the parapets still stop a car.</summary>
    public class RoadBridge : MonoBehaviour
    {
        public float halfWidth = 1.8f, length = 8f, top = 0.08f;
        Transform middle;

        public RoadBridge Set(float halfW, float len, float deckTop) { halfWidth = halfW; length = len; top = deckTop; return this; }

        /// <summary>The deck top at mid-span (world).</summary>
        public Vector3 Middle => transform.TransformPoint(new Vector3(0f, top, -length * 0.5f));

        void Start()
        {
            middle = new GameObject("DeckMiddle").transform;
            middle.SetParent(transform, false);
            middle.localPosition = new Vector3(0f, 0f, -length * 0.5f);
            if (transform.up.y < 0.9f || GetComponentInParent<Rigidbody>()) return;             // only upright bridges on the ground
            StructureGround.AddDeck(this, middle, null, new Vector4(halfWidth, length * 0.5f, top, top));
        }

        void OnDestroy() => StructureGround.Remove(this);
    }
}
