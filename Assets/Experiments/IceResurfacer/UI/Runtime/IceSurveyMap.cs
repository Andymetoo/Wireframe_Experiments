using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>A RawImage with a fitted rendered region. Geometry and picking share this exact projection.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class IceSurveyMap : RawImage, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
    {
        public IceSurfaceSurvey owner;
        public bool preserveMapAspect = true;
        [Tooltip("Map includes the service room, preserving the original survey's orientation and extent.")]
        public Vector2 serviceExtent = new Vector2(12, 13);
        [Tooltip("Representative Edit Mode map extent only; Play uses the actual simulation configuration.")]
        public Vector2 previewRinkSize = new Vector2(30, 60);
        public Vector2 RinkSize => Application.IsPlaying(gameObject) && owner != null && owner.game != null && owner.game.Sim != null ?
            new Vector2(owner.game.Sim.Config.RinkWidth, owner.game.Sim.Config.RinkLength) : previewRinkSize;
        Vector2 TextureWorldSize => Application.IsPlaying(gameObject) && owner != null && owner.game != null && owner.game.Sim != null ?
            new Vector2(owner.game.Sim.Ice.Columns * owner.game.Sim.Ice.CellSize, owner.game.Sim.Ice.Rows * owner.game.Sim.Ice.CellSize) : previewRinkSize;
        public Rect MapWorld => new Rect(-RinkSize.x * .5f, -RinkSize.y * .5f - serviceExtent.y, RinkSize.x + serviceExtent.x, RinkSize.y + serviceExtent.y);
        public Rect RenderedRect
        {
            get
            {
                Rect r = rectTransform.rect; if (!preserveMapAspect) return r;
                Rect world = MapWorld; float scale = Mathf.Min(r.width / world.width, r.height / world.height);
                var size = world.size * scale; return new Rect(r.center - size * .5f, size);
            }
        }
        public Vector2 WorldToLocal(float x, float z)
        {
            Vector2 size = TextureWorldSize, min = -RinkSize * .5f;
            if (x >= min.x && x <= min.x + size.x && z >= min.y && z <= min.y + size.y && Mathf.Abs(uvRect.width) > .00001f && Mathf.Abs(uvRect.height) > .00001f)
            {
                x = min.x + ((x - min.x) / size.x - uvRect.x) / uvRect.width * size.x;
                z = min.y + ((z - min.y) / size.y - uvRect.y) / uvRect.height * size.y;
            }
            return ExtentToLocal(x, z);
        }
        Vector2 ExtentToLocal(float x, float z)
        {
            Rect world = MapWorld, r = RenderedRect;
            return new Vector2(r.x + (x - world.x) / world.width * r.width, r.y + (z - world.y) / world.height * r.height);
        }
        public bool TryWorld(Vector2 screen, Camera camera, out Vector2 world)
        {
            world = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out Vector2 local)) return false;
            Rect r = RenderedRect; if (!r.Contains(local) || r.width <= 0 || r.height <= 0) return false;
            Rect extent = MapWorld;
            world = new Vector2(extent.x + (local.x - r.x) / r.width * extent.width, extent.y + (local.y - r.y) / r.height * extent.height);
            // Respect authored RawImage cropping/flips as well as the physical image/field dimensions.
            Vector2 size = TextureWorldSize, min = -RinkSize * .5f;
            if (world.x >= min.x && world.x < min.x + size.x && world.y >= min.y && world.y < min.y + size.y)
                world = min + Vector2.Scale(new Vector2(uvRect.x + (world.x - min.x) / size.x * uvRect.width,
                    uvRect.y + (world.y - min.y) / size.y * uvRect.height), size);
            return true;
        }
        public bool TryCell(Vector2 screen, Camera camera, out int index)
        {
            index = -1;
            if (owner == null || owner.game == null || owner.game.Sim == null || !TryWorld(screen, camera, out Vector2 world)) return false;
            var field = owner.game.Sim.Ice; index = field.IndexAt(world.x, world.y);
            if (index < 0 || !field.Cells[index].IsIce) { index = -1; return false; } return true;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = RenderedRect;
            // The authoritative textures represent only the ice field, while this viewport also includes the service bay.
            Vector2 size = TextureWorldSize, min = -RinkSize * .5f;
            Vector2 lo = ExtentToLocal(min.x, min.y), hi = ExtentToLocal(min.x + size.x, min.y + size.y);
            AddQuad(vh, Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y), uvRect, color);
        }
        internal static void AddQuad(VertexHelper vh, Rect rect, Rect uv, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, new Vector2(uv.xMin, uv.yMin));
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, new Vector2(uv.xMin, uv.yMax));
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, new Vector2(uv.xMax, uv.yMax));
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, new Vector2(uv.xMax, uv.yMin));
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
        public void OnPointerMove(PointerEventData e) { if (owner != null) owner.Inspect(e.position, e.enterEventCamera, false); }
        public void OnPointerEnter(PointerEventData e) { if (owner != null) owner.Inspect(e.position, e.enterEventCamera, false); }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && owner != null) owner.Inspect(e.position, e.pressEventCamera, true); }
        public void OnPointerExit(PointerEventData e) { if (owner != null) owner.EndHover(); }
    }
}
