using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    public interface IGpsProjectedStrokeSink { void Segment(Vector2 from, Vector2 to, bool alignment); }
    /// <summary>Editable uGUI view of the existing bounded, machine-occluded GPS projection.</summary>
    public sealed class IceGpsProjectedGraphic : MaskableGraphic, IGpsProjectedStrokeSink
    {
        public IceOperatorOverlay owner;
        public bool followGuideColor = true;
        public Color routeColor = new Color(1, .035f, .72f, .95f);
        public Color routeBorder = new Color(.06f, 0, .055f, .88f);
        public Color alignmentColor = new Color(1, .83f, .035f, 1);
        public Color alignmentBorder = new Color(.09f, .075f, .015f, .9f);
        [Min(0)] public float routeWidth = 2.4f, routeBorderWidth = 4.8f, alignmentWidth = 2.1f, alignmentBorderWidth = 4.5f;
        [Range(0, 1)] public float routeOpacity = .78f;
        public bool showDesignSample = true;
        public Vector2[] designRoute = { new Vector2(.46f, .3f), new Vector2(.5f, .45f), new Vector2(.55f, .6f) };
        struct Stroke { public Vector2 from, to; public bool alignment; }
        readonly Stroke[] strokes = new Stroke[GpsGuidanceGeometry.MaximumSilhouetteIntervals + GpsMachineAlignment.Intervals];
        int count, writing;
        bool changed;
        float width = 1600, height = 900;
        Color observedRoute;
        public int SegmentCount => count;
        public void Segment(Vector2 from, Vector2 to, bool alignment)
        {
            if (writing >= strokes.Length) return;
            var old = strokes[writing]; if (old.from != from || old.to != to || old.alignment != alignment) changed = true;
            strokes[writing++] = new Stroke { from = from, to = to, alignment = alignment };
        }
        void LateUpdate() { if (Application.IsPlaying(gameObject)) ProjectNow(Screen.width, Screen.height); }
        public void ProjectNow(float pixelWidth, float pixelHeight)
        {
            writing = 0; changed = width != pixelWidth || height != pixelHeight; width = pixelWidth; height = pixelHeight;
            var game = owner == null ? null : owner.game;
            if (game != null && owner.Visible && game.RouteGuide != null) game.RouteGuide.ProjectSilhouette(width, height, this);
            Color tint = followGuideColor && game != null && game.RouteGuide != null ? game.RouteGuide.GpsColor : routeColor;
            if (writing != count || observedRoute != tint) changed = true;
            count = writing; observedRoute = tint; if (changed) SetVerticesDirty();
        }
        Vector2 Local(Vector2 at)
        {
            float scale = Mathf.Min(width / 1600, height / 900);
            Vector2 offset = new Vector2((width - 1600 * scale) * .5f, (height - 900 * scale) * .5f);
            var rect = rectTransform.rect;
            return new Vector2(rect.xMin + (at.x * scale + offset.x) / width * rect.width,
                rect.yMin + (1 - (at.y * scale + offset.y) / height) * rect.height);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = rectTransform.rect;
            if (!Application.IsPlaying(gameObject))
            {
                if (!showDesignSample || designRoute == null) return;
                for (int i = 1; i < designRoute.Length; i++)
                {
                    Vector2 a = rect.min + Vector2.Scale(designRoute[i - 1], rect.size), b = rect.min + Vector2.Scale(designRoute[i], rect.size);
                    Line(vh, a, b, routeBorderWidth, routeBorder * color); Line(vh, a, b, routeWidth, routeColor * color);
                }
                return;
            }
            for (int i = 0; i < count; i++)
            {
                var stroke = strokes[i]; Vector2 a = Local(stroke.from), b = Local(stroke.to);
                Color tint = stroke.alignment ? alignmentColor : observedRoute; if (!stroke.alignment) tint.a *= routeOpacity;
                Line(vh, a, b, stroke.alignment ? alignmentBorderWidth : routeBorderWidth, (stroke.alignment ? alignmentBorder : routeBorder) * color);
                Line(vh, a, b, stroke.alignment ? alignmentWidth : routeWidth, tint * color);
            }
        }
        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color tint)
        {
            if (thickness <= 0 || (b - a).sqrMagnitude < .000001f) return;
            Vector2 direction = b - a, side = new Vector2(-direction.y, direction.x).normalized * thickness * .5f;
            int n = vh.currentVertCount; vh.AddVert(a - side, tint, Vector2.zero); vh.AddVert(a + side, tint, Vector2.zero);
            vh.AddVert(b + side, tint, Vector2.zero); vh.AddVert(b - side, tint, Vector2.zero); vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
