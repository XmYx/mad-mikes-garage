using System.Collections.Generic;
using System.Globalization;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Items block: every item and resource can be out in the world as a <see cref="WorldItem"/> — dropped from
    /// the pack page or the hand (Q), placed with a preview (pack page PLACE, rotate, LMB), taken back with [E], saved in
    /// <c>SaveData.blockItems</c> — and the HUD item feed (<see cref="ItemFeed"/>) of everything that enters or leaves the
    /// pack. World items are local to each peer online (not replicated; the host saves its own).</summary>
    public partial class WastelandGame
    {
        bool feedHooked, carriedLastFrame;
        float worldItemTick;
        static readonly CultureInfo ItemsInv = CultureInfo.InvariantCulture;

        partial void ItemsNewGame() => ItemFeed.Clear();

        partial void ItemsLoad(SaveData d) { ItemFeed.Clear(); LoadWorldItems(d); }

        partial void ItemsSave(SaveData d) => SaveWorldItems(d);

        partial void ItemsUpdate()
        {
            HookItemFeed();
            TickWorldItems();
            TickPlacing();
            DropToolKey();
        }

        /// <summary>The pack's gains and losses go to the HUD feed from the first playing frame on (the starting kit and a
        /// loaded pack are not news).</summary>
        void HookItemFeed()
        {
            if (feedHooked) return;
            feedHooked = true;
            Inventory.Delta += (id, t, n) => ItemFeed.Push(id, t, n, Inventory.Label(n));
        }

        /// <summary>Label the pack changes made inside a using block for the item feed: gain / loss label.</summary>
        public static Inventory.SourceScope FeedSource(string gain, string loss = null) => Inventory.Source(gain, loss);

        // ------------------------------------------------------------------ the pack
        static bool IsResKey(string key, out ResourceType t)
        {
            t = ResourceType.None;
            if (key == null || !key.StartsWith("res:") || !int.TryParse(key.Substring(4), out int i) || i <= 0 || i >= ResourceInfo.Count) return false;
            t = (ResourceType)i;
            return true;
        }

        /// <summary>How many of an item id or "res:N" the pack holds.</summary>
        public int PackCount(string key) => IsResKey(key, out var t) ? Inventory.Get(t) : key == null ? 0 : Inventory.GetItem(key);

        bool TakeFromPack(string key, int n) => IsResKey(key, out var t) ? Inventory.TrySpend(t, n) : Inventory.TakeItem(key, n);

        void GiveToPack(string key, int n, float quality)
        {
            if (IsResKey(key, out var t)) { Inventory.Add(t, n); return; }
            if (quality >= 0f) { int have = Inventory.GetItem(key); ItemQuality[key] = (QualityOf(key) * have + quality * n) / Mathf.Max(1, have + n); }
            Inventory.AddItem(key, n);
            if (key.StartsWith("tool_")) UpdateHotbarNow();
        }

        float WorldMakeOf(string key) => !IsResKey(key, out _) && HasMake(key) ? QualityOf(key) : -1f;

        /// <summary>The last one left the pack: out of the hand, off the body, no more reading it.</summary>
        void LetGoOf(string key)
        {
            if (IsResKey(key, out _) || Inventory.GetItem(key) > 0 || !Player) return;
            if (Player.Tool && Player.Tool.id == key) Player.Equip(null);
            var cd = ClothingLibrary.Get(key);
            if (cd != null && Player.Rig.outfit.Remove(cd.id)) Player.RebuildBody();
            if (LearningId == key) StopLearning(null);
        }

        // ------------------------------------------------------------------ drop, take
        /// <summary>Drop <paramref name="n"/> of an item id or "res:N" from the pack in front of you (beside the vehicle when
        /// driving). It joins a loose stack of the same thing lying within a metre. Null when the pack has none.</summary>
        public WorldItem DropFromPack(string key, int n)
        {
            n = Mathf.Min(n, PackCount(key));
            if (n <= 0 || !Player) return null;
            float q = WorldMakeOf(key);
            using (Inventory.Source(null, "DROPPED")) if (!TakeFromPack(key, n)) return null;
            LetGoOf(key);
            DropSpot(out var at, out var vel);
            foreach (var w in WorldItem.All)
            {
                if (!w || w.key != key || w.placed || w.OnVehicle || Mathf.Abs(w.quality - q) > 0.01f) continue;
                var d = w.transform.position - at;
                if (d.y > -2f && d.y < 0.5f && new Vector2(d.x, d.z).sqrMagnitude < 1f)
                {
                    w.count += n; w.Refresh(); w.Body.WakeUp();
                    MadMax.Audio.Sfx.Play("pickup", at, 0.3f, 0.7f, 15f);
                    return w;
                }
            }
            var item = SpawnWorldItem(key, n, q, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), null);
            item.Body.linearVelocity = vel;
            item.Body.angularVelocity = Random.insideUnitSphere * 2f;
            MadMax.Audio.Sfx.Play("pickup", at, 0.3f, 0.7f, 15f);
            return item;
        }

        /// <summary>Hand height in front of you (short of a wall), tossed forward; or out of the driver's door.</summary>
        void DropSpot(out Vector3 at, out Vector3 vel)
        {
            if (Current)
            {
                at = ExitPoint(Current) + Vector3.up * 0.5f;
                vel = Current.Body ? Current.Body.linearVelocity * 0.8f : Vector3.zero;
                return;
            }
            var t = Player.transform;
            var chest = t.position + t.up * 1.1f;
            float reach = 0.55f;
            if (Physics.Raycast(chest, t.forward, out var hit, 0.75f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(t))
                reach = Mathf.Max(0f, hit.distance - 0.2f);
            at = chest + t.forward * reach;
            // beside anything dropped a moment ago (still in the air there)
            for (int k = 0; k < 7; k++)
            {
                var c = at + t.right * (k == 0 ? 0f : ((k & 1) == 1 ? 0.35f : -0.35f) * ((k + 1) / 2));
                bool taken = false;
                foreach (var w in WorldItem.All) if (w && (w.transform.position - c).sqrMagnitude < 0.35f * 0.35f) { taken = true; break; }
                if (!taken) { at = c; break; }
            }
            vel = Player.Velocity + t.forward * 1.2f + t.up * 0.6f;
        }

        /// <summary>Q on foot with empty arms: the tool in hand goes down in front of you as its own object.</summary>
        public WorldItem DropHeldTool()
        {
            if (!Player || !Player.Tool) return null;
            var id = Player.Tool.id;
            var w = Inventory.GetItem(id) > 0 ? DropFromPack(id, 1) : null;
            Player.Equip(null);                                                                     // the feed shows "-1 ... DROPPED"
            return w;
        }

        void DropToolKey()
        {
            bool carrying = Player.Carried;
            if (Controls.Down(Controls.Act.Drop) && !carrying && !carriedLastFrame && !Current && Player.Tool && !Player.Sitting && !PlacingItem
                && !(Build && Build.Active) && !Boarding && !ExternalInput && !(Vitals && Vitals.Dead))
                DropHeldTool();
            carriedLastFrame = carrying;
        }

        /// <summary>An object for <paramref name="count"/> of <paramref name="key"/> in the world (the pack is not touched).
        /// With a <paramref name="vehicle"/> it rides along as part of it.</summary>
        public WorldItem SpawnWorldItem(string key, int count, float quality, Vector3 pos, Quaternion rot, Transform vehicle)
        {
            var w = WorldItem.Create(key, count, quality, propMaterial, pos, rot, vehicle);
            if (Player && Player.TryGetComponent<Collider>(out var pc)) Physics.IgnoreCollision(w.Box, pc);   // never trips you up
            return w;
        }

        /// <summary>[E] on an item in the world: the whole stack into the pack.</summary>
        public bool PickUpItem(WorldItem w)
        {
            if (!w || w.count <= 0) return false;
            using (Inventory.Source("PICKED UP")) GiveToPack(w.key, w.count, w.quality);
            MadMax.Audio.Sfx.Play("pickup", w.transform.position, 0.5f, 1.1f, 20f);
            w.count = 0;
            w.gameObject.SetActive(false);
            Destroy(w.gameObject);
            return true;
        }

        /// <summary>Near the player: offered to [E]; far away: frozen (the ground has no colliders out there), woken on the
        /// way back like loose parts; anything that slipped under the ground comes back up.</summary>
        void TickWorldItems()
        {
            if (Time.time < worldItemTick || WorldItem.All.Count == 0) return;
            worldItemTick = Time.time + 0.4f;
            var focus = FocusPos; var me = Player.transform.position;
            foreach (var w in WorldItem.All)
            {
                if (!w) continue;
                var p = w.transform.position;
                w.SetNear(!Current && (p - me).sqrMagnitude < 64f);
                if (w.OnVehicle) continue;
                var rb = w.Body;
                float d = Vector3.Distance(p, focus);
                if (!rb.isKinematic && d > 70f) { rb.isKinematic = true; continue; }
                if (d >= 50f) continue;
                float ground = terrain.Height(p.x, p.z);
                if (rb.isKinematic)
                {
                    if (p.y < ground) { p.y = ground + 0.02f; rb.position = p; }
                    rb.isKinematic = false;
                    if (w.placed) rb.Sleep();
                }
                else if (p.y < ground - 2f) { p.y = ground + 0.3f; rb.position = p; rb.linearVelocity = Vector3.zero; }
            }
        }

        // ------------------------------------------------------------------ PLACE: a preview at the aim point
        string placeKey;
        int placeCount, placeYaw, placeFrame = -1;
        GameObject placeGhost;
        string ghostKey;
        MeshRenderer ghostRenderer;
        MaterialPropertyBlock ghostTint;
        Bounds ghostBounds;
        static readonly RaycastHit[] placeHits = new RaycastHit[16];
        static readonly Collider[] placeOverlap = new Collider[16];

        /// <summary>The PLACE preview's line for the HUD (null when not placing); <see cref="PlaceOk"/>: it can go there.</summary>
        public string PlaceStatus { get; private set; }
        public bool PlaceOk { get; private set; }
        /// <summary>A PLACE preview is up (or was confirmed this frame): the click belongs to it, not to the tool.</summary>
        public bool PlacingItem => placeKey != null || placeFrame == Time.frameCount;
        public string PlacingKey => placeKey;

        public bool CanPlaceItem(string key, out string why)
        {
            why = !Player || !Player.gameObject.activeSelf ? "NOT NOW" : Current ? "GET OUT TO PUT THINGS DOWN" : Player.Sitting ? "STAND UP FIRST" : PackCount(key) <= 0 ? "NONE LEFT" : null;
            return why == null;
        }

        /// <summary>Start the preview for one item (resources: a bundle of up to 10) from the pack.</summary>
        public bool BeginPlaceItem(string key)
        {
            if (!CanPlaceItem(key, out var why)) { Toast(why); return false; }
            if (Build && Build.Active) Build.SetActive(false);
            placeKey = key;
            placeCount = IsResKey(key, out _) ? Mathf.Min(10, PackCount(key)) : 1;
            return true;
        }

        /// <summary>Esc / RMB: put the preview away (the item stays in the pack). False when there was none.</summary>
        public bool CancelPlacing()
        {
            if (placeKey == null) return false;
            placeKey = null; PlaceStatus = null;
            if (placeGhost) placeGhost.SetActive(false);
            return true;
        }

        void TickPlacing()
        {
            if (placeKey == null) { PlaceStatus = null; if (placeGhost && placeGhost.activeSelf) placeGhost.SetActive(false); return; }
            var mouse = Mouse.current; var pad = Gamepad.current;
            if (Current || Player.Sitting || (Build && Build.Active) || !Player.gameObject.activeSelf || PackCount(placeKey) <= 0 || (Vitals && Vitals.Dead)) { CancelPlacing(); return; }
            placeCount = Mathf.Clamp(placeCount, 1, PackCount(placeKey));
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) { CancelPlacing(); return; }
            if (Controls.Down(Controls.Act.BuildRotate) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) placeYaw = (placeYaw + 1) % 8;
            ShowGhost(placeKey);
            PlaceOk = false;
            if (!AimPlace(out var hit))
            {
                placeGhost.SetActive(false);
                SetPlaceStatus("AIM AT A SURFACE");
                return;
            }
            float yaw = PlaceYaw();
            var rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, yaw, 0f);
            placeGhost.transform.SetPositionAndRotation(hit.point + hit.normal * 0.002f, rot);
            var eye = Player.Eye.position;
            string why = Vector3.Distance(hit.point, eye) > Build.reach ? "TOO FAR"
                : hit.normal.y < 0.6f ? "NEEDS A LEVEL SURFACE"
                : !Build.Visible(eye, hit.point + hit.normal * 0.04f, hit.collider) ? "OUT OF SIGHT"
                : !PlaceFree(hit.point, rot, hit.collider) ? "NO ROOM THERE" : null;
            ghostTint ??= new MaterialPropertyBlock();
            ghostTint.SetColor("_Tint", why == null ? new Color(0.7f, 1.3f, 0.7f) : new Color(1.4f, 0.5f, 0.45f));
            ghostRenderer.SetPropertyBlock(ghostTint);
            SetPlaceStatus(why);
            PlaceOk = why == null;
            bool click = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            if (!click) return;
            placeFrame = Time.frameCount;
            if (why != null) { Toast(why); return; }
            PlaceItemAt(placeKey, placeCount, hit.point, hit.normal, yaw, hit.collider);
        }

        string placeWhy, placeWhyKey; int placeWhyCount;

        /// <summary>"PLACE 10 SCRAP: TOO FAR  [LMB] SET ..." (rebuilt only when it changes).</summary>
        void SetPlaceStatus(string why)
        {
            if (PlaceStatus != null && why == placeWhy && placeKey == placeWhyKey && placeCount == placeWhyCount) return;
            placeWhy = why; placeWhyKey = placeKey; placeWhyCount = placeCount;
            string label = IsResKey(placeKey, out var rt) ? placeCount + (ResourceInfo.IsFluid(rt) ? "L " : " ") + ResourceInfo.Name(rt) : ItemCatalog.Name(placeKey);
            PlaceStatus = "PLACE " + label + (why != null ? ": " + why : "") + "  [LMB] SET  [" + Controls.Name(Controls.Act.BuildRotate) + "] TURN  [ESC] BACK";
        }

        /// <summary>Front to the viewer, then the player's eighth turns.</summary>
        float PlaceYaw()
        {
            var f = cameraRig && cameraRig.pixel ? cameraRig.pixel.transform.forward : Player.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.01f) f = Player.transform.forward;
            return Quaternion.LookRotation(-f).eulerAngles.y + placeYaw * 45f;
        }

        void ShowGhost(string key)
        {
            if (!placeGhost) { placeGhost = new GameObject("ItemGhost"); ghostKey = null; }
            if (ghostKey != key)
            {
                for (int i = placeGhost.transform.childCount - 1; i >= 0; i--) Destroy(placeGhost.transform.GetChild(i).gameObject);
                var vis = WorldItemModels.AddVisual(placeGhost.transform, key, propMaterial, out ghostBounds);
                ghostRenderer = vis.GetComponent<MeshRenderer>();
                ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ghostKey = key;
            }
            if (!placeGhost.activeSelf) placeGhost.SetActive(true);
        }

        /// <summary>The surface under the crosshair (third / first person) or the mouse (top-down views), skipping the
        /// player and cut-away roofs.</summary>
        bool AimPlace(out RaycastHit best)
        {
            best = default;
            if (!cameraRig || !cameraRig.pixel) return false;
            var cam = cameraRig.pixel.GetComponent<Camera>();
            Ray ray;
            if (cameraRig.CrosshairView) ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            else
            {
                var m = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
                ray = cam.ViewportPointToRay(new Vector3(m.x / Screen.width, m.y / Screen.height, 0f));
            }
            int n = Physics.RaycastNonAlloc(ray, placeHits, 250f, ~0, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue; bool found = false;
            var me = Player.transform;
            for (int i = 0; i < n; i++)
            {
                var h = placeHits[i];
                if (h.collider.transform.IsChildOf(me)) continue;
                if (OccluderFade.Active && OccluderFade.Active.Hides(h.collider, h.point)) continue;
                if (h.distance < bd) { bd = h.distance; best = h; found = true; }
            }
            return found;
        }

        /// <summary>Nothing but the surface (and bare ground) inside the item's box set down there.</summary>
        bool PlaceFree(Vector3 point, Quaternion rot, Collider surface)
        {
            var half = Vector3.Max(ghostBounds.extents - Vector3.one * 0.02f, Vector3.one * 0.01f);
            var centre = point + rot * (ghostBounds.center + Vector3.up * 0.03f);
            int n = Physics.OverlapBoxNonAlloc(centre, half, placeOverlap, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = placeOverlap[i];
                if (c == surface || c.transform.IsChildOf(Player.transform)) continue;
                if (c is MeshCollider && !c.GetComponent<MadMax.World.DestructibleVoxels>()) continue;   // terrain
                return false;
            }
            return true;
        }

        /// <summary>Put <paramref name="n"/> of <paramref name="key"/> from the pack down with care at <paramref name="point"/>
        /// (its lowest point), upright on <paramref name="normal"/> and turned to <paramref name="yaw"/>. On a vehicle it
        /// rides along. Null when the pack has none.</summary>
        public WorldItem PlaceItemAt(string key, int n, Vector3 point, Vector3 normal, float yaw, Collider surface)
        {
            n = Mathf.Min(n, PackCount(key));
            if (n <= 0) return null;
            float q = WorldMakeOf(key);
            using (Inventory.Source(null, "PLACED")) if (!TakeFromPack(key, n)) return null;
            LetGoOf(key);
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            var chassis = surface ? surface.GetComponentInParent<VehicleChassis>() : null;
            var rot = Quaternion.FromToRotation(Vector3.up, normal.normalized) * Quaternion.Euler(0f, yaw, 0f);
            var w = SpawnWorldItem(key, n, q, point + normal.normalized * 0.002f, rot, chassis ? chassis.transform : null);
            w.placed = true;
            if (w.Body) w.Body.Sleep();
            if (placeKey == key) CancelPlacing();
            placeFrame = Time.frameCount;
            MadMax.Audio.Sfx.Play("hit_wood", point, 0.3f, 1.5f, 15f);
            return w;
        }

        // ------------------------------------------------------------------ save
        /// <summary>One line per world item: "i1|key|count|quality|placed|vehicle netId|world pos|world rot|local pos|local rot"
        /// (local = on the vehicle; the world pose is the fallback when that vehicle is gone).</summary>
        public void SaveWorldItems(SaveData d)
        {
            if (d.blockItems == null) d.blockItems = new List<string>();
            foreach (var w in WorldItem.All)
            {
                if (!w || w.count <= 0) continue;
                var drv = w.OnVehicle ? w.GetComponentInParent<VehicleDriver>() : null;
                bool onCar = drv && !drv.aiDriven;
                var t = w.transform;
                var lp = onCar ? drv.transform.InverseTransformPoint(t.position) : t.position;
                var lr = onCar ? Quaternion.Inverse(drv.transform.rotation) * t.rotation : t.rotation;
                d.blockItems.Add(string.Join("|", "i1", w.key, w.count.ToString(ItemsInv), w.quality.ToString("0.###", ItemsInv), w.placed ? "1" : "0", (onCar ? drv.netId : 0).ToString(ItemsInv),
                    ItemsV(t.position), ItemsQ(t.rotation), ItemsV(lp), ItemsQ(lr)));
            }
        }

        static string ItemsV(Vector3 v) => v.x.ToString("R", ItemsInv) + "," + v.y.ToString("R", ItemsInv) + "," + v.z.ToString("R", ItemsInv);
        static string ItemsQ(Quaternion q) => q.x.ToString("R", ItemsInv) + "," + q.y.ToString("R", ItemsInv) + "," + q.z.ToString("R", ItemsInv) + "," + q.w.ToString("R", ItemsInv);

        static bool ItemsFloats(string s, float[] into)
        {
            var f = s.Split(',');
            if (f.Length != into.Length) return false;
            for (int i = 0; i < f.Length; i++) if (!float.TryParse(f[i], NumberStyles.Float, ItemsInv, out into[i])) return false;
            return true;
        }

        /// <summary>Put the saved world items back (replacing any there are): frozen until the player comes near.</summary>
        public void LoadWorldItems(SaveData d)
        {
            ClearWorldItems();
            if (d == null || d.blockItems == null) return;
            float[] wp = new float[3], wr = new float[4], lp = new float[3], lr = new float[4];
            foreach (var line in d.blockItems)
            {
                var f = line.Split('|');
                if (f.Length < 10 || f[0] != "i1" || string.IsNullOrEmpty(f[1])) continue;
                if (!int.TryParse(f[2], NumberStyles.Integer, ItemsInv, out int count) || count <= 0) continue;
                float.TryParse(f[3], NumberStyles.Float, ItemsInv, out float q);
                ushort.TryParse(f[5], NumberStyles.Integer, ItemsInv, out ushort netId);
                if (!ItemsFloats(f[6], wp) || !ItemsFloats(f[7], wr)) continue;
                var pos = new Vector3(wp[0], wp[1], wp[2]); var rot = new Quaternion(wr[0], wr[1], wr[2], wr[3]);
                Transform car = null;
                if (netId != 0 && ItemsFloats(f[8], lp) && ItemsFloats(f[9], lr))
                    foreach (var v in vehicles)
                        if (v && v.netId == netId)
                        {
                            car = v.transform;
                            pos = car.TransformPoint(new Vector3(lp[0], lp[1], lp[2]));
                            rot = car.rotation * new Quaternion(lr[0], lr[1], lr[2], lr[3]);
                            break;
                        }
                var w = SpawnWorldItem(f[1], count, q, pos, rot, car);
                w.placed = f[4] == "1";
                if (w.Body) w.Body.isKinematic = true;                                           // woken near the player
            }
            worldItemTick = 0f;
        }

        /// <summary>Remove every world item at once.</summary>
        public void ClearWorldItems()
        {
            for (int i = WorldItem.All.Count - 1; i >= 0; i--)
            {
                var w = WorldItem.All[i];
                if (!w) { WorldItem.All.RemoveAt(i); continue; }
                w.gameObject.SetActive(false);
                Destroy(w.gameObject);
            }
        }
    }
}
