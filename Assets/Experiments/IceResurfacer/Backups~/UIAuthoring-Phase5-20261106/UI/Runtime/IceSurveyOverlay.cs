using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    /// <summary>One sparse uGUI mesh for map lines; no ice cells, textures or simulation state are recreated.</summary>
    public sealed class IceSurveyOverlay : MaskableGraphic
    {
        public IceSurveyMap map;
        public Color machineColor = Color.white, conditionerColor = new Color(1, .7f, .3f), serviceColor = new Color(.6f, .65f, .6f);
        public Color bladeColor = new Color(.5f, .8f, .65f), waterColor = new Color(.35f, .75f, 1, .9f);
        [Min(.1f)] public float lineWidth = 1, routeWidth = 1.8f, markerSize = 5;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (map == null || map.owner == null || map.owner.game == null || map.owner.game.Sim == null) return;
            var game = map.owner.game; var sim = game.Sim; var layout = sim.Layout;
            layout.ToWorld(-layout.RoomHalfWidth, layout.RoomStart, out float ax, out float az);
            layout.ToWorld(layout.RoomHalfWidth, layout.RoomStart, out float bx, out float bz);
            layout.ToWorld(layout.RoomHalfWidth, layout.RoomLength, out float cx, out float cz);
            layout.ToWorld(-layout.RoomHalfWidth, layout.RoomLength, out float dx, out float dz);
            var a = Point(ax, az); var b = Point(bx, bz); var c = Point(cx, cz); var d = Point(dx, dz);
            Line(vh, a, b, lineWidth * 2, serviceColor); Line(vh, b, c, lineWidth * 2, serviceColor);
            Line(vh, c, d, lineWidth * 2, serviceColor); Line(vh, d, a, lineWidth * 2, serviceColor);
            Dot(vh, Point(layout.GateX, layout.GateZ), 3, bladeColor);
            var guide = game.RouteGuide;
            if (guide != null && guide.GuidanceDisplayed && guide.Progress != null)
            {
                for (int i = 1; i < guide.MapPointCount; i += 2)
                {
                    var p = guide.MapPointAt(i - 1); var q = guide.MapPointAt(Mathf.Min(i + 1, guide.MapPointCount - 1));
                    a = Point(p.x, p.z); b = Point(q.x, q.z);
                    Line(vh, a, b, routeWidth * 2, new Color(.06f, 0, .06f, .95f)); Line(vh, a, b, routeWidth, guide.GpsColor);
                }
                int turn = guide.Progress.UpcomingTurn(IceRouteGuide.LookAheadMeters);
                if (turn >= 0 && guide.ActiveFit.samples[turn].distance <= guide.LookEnd)
                { var p = guide.ActiveFit.samples[turn]; Dot(vh, Point(p.x, p.z), 2.5f, conditionerColor); }
                if (guide.MapPointCount > 2)
                {
                    var p = guide.MapPointAt(Mathf.Min(guide.MapPointCount - 1, 6)); var q = guide.MapPointAt(Mathf.Min(guide.MapPointCount - 1, 8));
                    a = Point(p.x, p.z); var forward = (Point(q.x, q.z) - a).normalized; var side = new Vector2(-forward.y, forward.x);
                    Line(vh, a - forward * 4 + side * 3, a, lineWidth * 2, guide.GpsColor); Line(vh, a - forward * 4 - side * 3, a, lineWidth * 2, guide.GpsColor);
                }
                Footprint(vh, sim.Conditioner.X, sim.Conditioner.Z, sim.Conditioner.Heading, sim.Config.ConditionerWidth, sim.Config.ConditionerLength, 0, bladeColor);
                Footprint(vh, sim.Conditioner.X, sim.Conditioner.Z, sim.Conditioner.Heading, sim.Config.WaterDistributionWidth + 2 * sim.Config.WaterFilmSpreadMeters, sim.Config.FloodContactLength, sim.Config.FloodContactFore, waterColor);
            }
            var pos = Point(sim.Vehicle.X, sim.Vehicle.Z); Rect bounds = map.RenderedRect;
            pos.x = Mathf.Clamp(pos.x, bounds.xMin + markerSize, bounds.xMax - markerSize);
            pos.y = Mathf.Clamp(pos.y, bounds.yMin + markerSize, bounds.yMax - markerSize);
            var heading = (Point(sim.Vehicle.X + Mathf.Sin(sim.Vehicle.Heading), sim.Vehicle.Z + Mathf.Cos(sim.Vehicle.Heading)) - Point(sim.Vehicle.X, sim.Vehicle.Z)).normalized;
            var right = new Vector2(heading.y, -heading.x);
            Line(vh, pos - heading * markerSize, pos + heading * markerSize * 1.4f, lineWidth * 3, machineColor);
            Line(vh, pos + heading * markerSize * 1.4f, pos + right * markerSize, lineWidth * 2, machineColor);
            Line(vh, pos + heading * markerSize * 1.4f, pos - right * markerSize, lineWidth * 2, machineColor);
            pos = Point(sim.Conditioner.X, sim.Conditioner.Z);
            if (bounds.Contains(pos))
            {
                right = (Point(sim.Conditioner.X + Mathf.Cos(sim.Conditioner.Heading), sim.Conditioner.Z - Mathf.Sin(sim.Conditioner.Heading)) - pos).normalized;
                Line(vh, pos - right * markerSize, pos + right * markerSize, lineWidth * 3, conditionerColor);
            }
        }
        Vector2 Point(float x, float z)
        {
            Vector3 world = map.rectTransform.TransformPoint(map.WorldToLocal(x, z));
            return rectTransform.InverseTransformPoint(world);
        }
        void Footprint(VertexHelper vh, float x, float z, float heading, float width, float length, float fore, Color tint)
        {
            float sn = Mathf.Sin(heading), cs = Mathf.Cos(heading); Vector2 previous = default;
            for (int i = 0; i < 5; i++)
            {
                int j = i % 4; float side = (j == 0 || j == 3 ? -.5f : .5f) * width, along = (j < 2 ? -.5f : .5f) * length + fore;
                Vector2 point = Point(x + cs * side + sn * along, z - sn * side + cs * along);
                if (i > 0) Line(vh, previous, point, lineWidth, tint); previous = point;
            }
        }
        static void Dot(VertexHelper vh, Vector2 at, float radius, Color tint) => IceSurveyMap.AddQuad(vh, new Rect(at - Vector2.one * radius, Vector2.one * radius * 2), new Rect(0, 0, 1, 1), tint);
        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 delta = b - a; if (delta.sqrMagnitude < .0001f) return;
            Vector2 side = new Vector2(-delta.y, delta.x).normalized * width * .5f; int n = vh.currentVertCount;
            vh.AddVert(a - side, tint, Vector2.zero); vh.AddVert(a + side, tint, Vector2.zero);
            vh.AddVert(b + side, tint, Vector2.zero); vh.AddVert(b - side, tint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); SetVerticesDirty(); }
    }
}
