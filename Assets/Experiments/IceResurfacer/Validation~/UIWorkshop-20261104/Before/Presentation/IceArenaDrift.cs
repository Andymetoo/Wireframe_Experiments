using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Geometry-versus-simulation report for the authored arena. Every check measures the
    /// saved geometry against SimulatorConfig / ServiceLayout (the collision authority), not
    /// against a remembered copy, so a moved board, a resized wall or a changed calibration
    /// is reported the same way. Errors are metres at the affected surface.
    /// </summary>
    public static class IceArenaDrift
    {
        public const float Tolerance = .002f;

        public struct Finding
        {
            public string Label, Detail;
            public float Error;
            public bool Drifted { get { return Error > Tolerance; } }
        }

        public static List<Finding> Evaluate(IceArenaRig rig, SimulatorConfig config)
        {
            var findings = new List<Finding>();
            var layout = new ServiceLayout(config);
            Transform frame = rig.transform.parent;
            Matrix4x4 toGame = frame != null ? frame.worldToLocalMatrix : Matrix4x4.identity;

            // The arena's own origin must stay on the game origin, or everything shifts.
            Matrix4x4 root = toGame * rig.transform.localToWorldMatrix;
            Add(findings, "Arena origin on the simulation origin", PoseError(root, Matrix4x4.identity, 30), "moved/rotated relative to the game root");

            var ice = IceFilter(rig, findings);
            if (ice != null) IceSheet(findings, ice, toGame, config);
            foreach (var band in rig.Parts(IceArenaRole.RinkBoardBand)) Band(findings, band, toGame, config, layout);
            if (rig.Parts(IceArenaRole.RinkBoardBand).Count == 0) Missing(findings, "Rink boards, kickplate, rail and glass");

            // Service bay: one expected frame from ServiceLayout; parts are measured in it.
            Matrix4x4 bayFrame = Matrix4x4.TRS(new Vector3(layout.GateX, 0, layout.GateZ), Quaternion.Euler(0, -45, 0), Vector3.one);
            Matrix4x4 toBay = bayFrame.inverse * toGame;
            var bay = Optional(rig, IceArenaRole.ServiceBay);
            if (bay == null) Missing(findings, "Southeast service gate frame");
            else Add(findings, "Southeast service gate frame", PoseError(toGame * bay.localToWorldMatrix, bayFrame, 6), "gate centre and diagonal heading");

            foreach (var part in rig.Parts(IceArenaRole.ServiceFloor))
            {
                var b = BayBounds(part, toBay);
                float e = Max(Mathf.Abs(b.min.x + layout.RoomHalfWidth), Mathf.Abs(b.max.x - layout.RoomHalfWidth),
                    Mathf.Abs(-b.max.z - layout.RoomStart), Mathf.Abs(-b.min.z - layout.RoomLength), Mathf.Max(0, Mathf.Abs(b.max.y) - .01f));
                Add(findings, "Service-bay floor", e, "spans side " + R(b.min.x) + ".." + R(b.max.x) + ", outward " + R(-b.max.z) + ".." + R(-b.min.z) +
                    " (collision floor +-" + R(layout.RoomHalfWidth) + ", " + R(layout.RoomStart) + ".." + R(layout.RoomLength) + ")");
            }
            foreach (var part in rig.Parts(IceArenaRole.ServiceSideWall))
            {
                var b = BayBounds(part, toBay); bool east = b.center.x > 0;
                float face = east ? b.min.x : -b.max.x;
                float e = Max(Mathf.Abs(face - layout.RoomHalfWidth), Mathf.Max(0, -b.max.z - layout.RoomStart - .01f), Mathf.Max(0, layout.RoomLength - .01f + b.min.z));
                Add(findings, "Service-bay " + (east ? "northeast" : "southwest") + " side wall", e, "inner face " + R(face) + " m from the bay axis (collision " + R(layout.RoomHalfWidth) + ")");
            }
            foreach (var part in rig.Parts(IceArenaRole.ServiceRearWall))
            {
                var b = BayBounds(part, toBay);
                float face = -b.max.z;
                float e = Max(Mathf.Abs(face - layout.RoomLength), Mathf.Max(0, layout.RoomHalfWidth - .01f - b.max.x), Mathf.Max(0, layout.RoomHalfWidth - .01f + b.min.x));
                Add(findings, "Service-bay rear wall", e, "inner face " + R(face) + " m outward (collision " + R(layout.RoomLength) + ")");
            }
            foreach (var part in rig.Parts(IceArenaRole.PortalReturnWall))
            {
                var b = BayBounds(part, toBay); bool east = b.center.x > 0;
                float edge = east ? b.min.x : -b.max.x;
                float e = Max(Mathf.Abs(edge - layout.GateHalfWidth), Mathf.Abs(-b.center.z - layout.RoomStart));
                Add(findings, "Service gate return wall (" + (east ? "northeast" : "southwest") + ")", e, "opening edge " + R(edge) + " (gate half width " + R(layout.GateHalfWidth) + ")");
            }
            foreach (var part in rig.Parts(IceArenaRole.GateJamb))
            {
                var b = BayBounds(part, toBay); bool east = b.center.x > 0;
                float face = east ? b.min.x : -b.max.x;
                // Visible frames may sit up to 10 cm outside the collision opening, never inside it.
                float e = Mathf.Max(0, layout.GateHalfWidth - face) + Mathf.Max(0, face - layout.GateHalfWidth - .1f);
                Add(findings, "Service gate opening: " + part.name, e, "clear face " + R(face) + " m from the gate axis (collision " + R(layout.GateHalfWidth) + ")");
            }
            foreach (var part in rig.Parts(IceArenaRole.SnowPit))
            {
                var b = BayBounds(part, toBay);
                float e = Max(Mathf.Abs(b.min.x - (layout.PitSide - layout.PitHalfWidth)), Mathf.Abs(b.max.x - (layout.PitSide + layout.PitHalfWidth)),
                    Mathf.Abs(-b.max.z - (layout.PitOutward - layout.PitHalfLength)), Mathf.Abs(-b.min.z - (layout.PitOutward + layout.PitHalfLength)));
                Add(findings, "Snow pit boundary", e, "receiver " + R(b.size.x) + " x " + R(b.size.z) + " m (simulated " + R(layout.PitHalfWidth * 2) + " x " + R(layout.PitHalfLength * 2) + ")");
            }
            var pit = Optional(rig, IceArenaRole.PitSnow);
            if (pit == null) Missing(findings, "Dumped snow surface");
            else
            {
                Vector3 p = toBay.MultiplyPoint3x4(pit.position);
                Add(findings, "Dumped snow surface over the pit", Vector2.Distance(new Vector2(p.x, -p.z), new Vector2(layout.PitSide, layout.PitOutward)), "centre of the simulated pit pile");
            }
            if (rig.Parts(IceArenaRole.ServiceFloor).Count == 0) Missing(findings, "Service-bay floor");
            if (rig.Parts(IceArenaRole.SnowPit).Count == 0) Missing(findings, "Snow pit boundary");

            // Water station: the hose holder, supply valve and drain are simulated at fixed
            // ServiceLayout points, so the fixture frame is physical; its valve and holder
            // must stay where they were generated inside it.
            var station = Optional(rig, IceArenaRole.WaterStation);
            if (station != null)
            {
                layout.ToWorld(6 - layout.RoomHalfWidth, Simulation.ServiceAnchorOutward * 2, out float sx, out float sz);
                Add(findings, "Water station (hose holder, supply valve, drain)", PoseError(toGame * station.localToWorldMatrix,
                    Matrix4x4.TRS(new Vector3(sx, 0, sz), Quaternion.Euler(0, 135, 0), Vector3.one), 2), "fixture frame at the simulated service wall");
                foreach (var role in new[] { IceArenaRole.SupplyValveHandle, IceArenaRole.NozzleHolderHighlight })
                    foreach (var part in rig.Parts(role))
                        Add(findings, "Water station " + (role == IceArenaRole.SupplyValveHandle ? "supply valve" : "nozzle holder"), part.AtBakedPose ? 0 : 1,
                            part.AtBakedPose ? "at its generated place in the station" : "moved inside the station; interaction stays at the simulated point");
            }
            return findings;
        }

        static void IceSheet(List<Finding> findings, MeshFilter ice, Matrix4x4 toGame, SimulatorConfig config)
        {
            var vertices = ice.sharedMesh.vertices;
            Matrix4x4 m = toGame * ice.transform.localToWorldMatrix;
            float boundary = 0, corner = 0, height = 0, minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            float hx = config.RinkWidth * .5f - config.CornerRadius, hz = config.RinkLength * .5f - config.CornerRadius;
            foreach (var v in vertices)
            {
                Vector3 p = m.MultiplyPoint3x4(v);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                height = Mathf.Max(height, Mathf.Max(0, Mathf.Abs(p.y) - .02f));
                float d = Distance(p.x, p.z, config);
                if (d < -1) continue; // interior fan centre
                boundary = Mathf.Max(boundary, Mathf.Abs(d));
                if (Mathf.Abs(p.x) > hx + .01f && Mathf.Abs(p.z) > hz + .01f) corner = Mathf.Max(corner, Mathf.Abs(d));
            }
            float extent = Mathf.Max(Mathf.Abs(maxX - minX - config.RinkWidth), Mathf.Abs(maxZ - minZ - config.RinkLength));
            extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(maxX + minX), Mathf.Abs(maxZ + minZ)) * .5f);
            Add(findings, "Rink width and length (ice sheet footprint)", Mathf.Max(extent, height),
                R(maxX - minX) + " x " + R(maxZ - minZ) + " m (simulated " + R(config.RinkWidth) + " x " + R(config.RinkLength) + ")");
            Add(findings, "Ice sheet edge on the simulated boards", boundary, "largest edge offset " + Mm(boundary));
            Add(findings, "Rounded corner positions", corner, "corner radius " + R(config.CornerRadius) + " m, largest offset " + Mm(corner));
        }

        static void Band(List<Finding> findings, IceArenaPart part, Matrix4x4 toGame, SimulatorConfig config, ServiceLayout layout)
        {
            var filter = part.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) { Missing(findings, part.name); return; }
            Matrix4x4 m = toGame * part.transform.localToWorldMatrix;
            float inner = float.MaxValue, outer = float.MinValue, gate = 0;
            foreach (var v in filter.sharedMesh.vertices)
            {
                Vector3 p = m.MultiplyPoint3x4(v);
                float d = Distance(p.x, p.z, config);
                inner = Mathf.Min(inner, d); outer = Mathf.Max(outer, d);
                // No board face may stand inside the open service gate.
                layout.ToLocal(p.x, p.z, out float side, out float outward);
                if (d < .3f && outward > layout.NeckStart && outward < layout.RoomStart) gate = Mathf.Max(gate, layout.GateHalfWidth - .3f - Mathf.Abs(side));
            }
            // Inner face exactly on the simulated board line; the band only extends outward (<= 25 cm).
            float e = Max(Mathf.Abs(inner), Mathf.Max(0, outer - .25f), gate > 0 ? gate + Tolerance * 2 : 0);
            Add(findings, "Rink boards: " + part.name, e, gate > 0 ? "crosses the open service gate" : "inner face " + Mm(inner) + " from the simulated boards");
        }

        // Signed distance to the simulated rounded boards (IceField.BoardDistance); negative is on the ice.
        static float Distance(float x, float z, SimulatorConfig config)
        {
            float qx = Mathf.Abs(x) - (config.RinkWidth * .5f - config.CornerRadius);
            float qz = Mathf.Abs(z) - (config.RinkLength * .5f - config.CornerRadius);
            float ox = Mathf.Max(0, qx), oz = Mathf.Max(0, qz);
            return Mathf.Sqrt(ox * ox + oz * oz) + Mathf.Min(Mathf.Max(qx, qz), 0) - config.CornerRadius;
        }

        // Mesh bounds in the expected bay frame: x = Side, -z = Outward.
        static Bounds BayBounds(IceArenaPart part, Matrix4x4 toBay)
        {
            var filter = part.GetComponent<MeshFilter>();
            var local = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.zero);
            Matrix4x4 m = toBay * part.transform.localToWorldMatrix;
            var bounds = new Bounds(m.MultiplyPoint3x4(local.min), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z)));
            return bounds;
        }

        // Position error plus rotation expressed as displacement at a lever arm (metres), plus scale.
        static float PoseError(Matrix4x4 actual, Matrix4x4 expected, float lever)
        {
            float position = Vector3.Distance(actual.GetColumn(3), expected.GetColumn(3));
            float angle = Quaternion.Angle(actual.rotation, expected.rotation) * Mathf.Deg2Rad * lever;
            float scale = (actual.lossyScale - expected.lossyScale).magnitude * lever;
            return position + angle + scale;
        }

        static MeshFilter IceFilter(IceArenaRig rig, List<Finding> findings)
        {
            var part = Optional(rig, IceArenaRole.IceSheet);
            var filter = part != null ? part.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) { Missing(findings, "Ice sheet"); return null; }
            return filter;
        }
        static Transform Optional(IceArenaRig rig, IceArenaRole role)
        {
            var parts = rig.Parts(role);
            return parts.Count == 1 ? parts[0].transform : null;
        }
        static void Missing(List<Finding> findings, string label)
        { findings.Add(new Finding { Label = label, Error = float.PositiveInfinity, Detail = "missing (no single part marked with its IceArenaPart role)" }); }
        static void Add(List<Finding> findings, string label, float error, string detail)
        { findings.Add(new Finding { Label = label, Error = error, Detail = (error > Tolerance ? Mm(error) + " off: " : "") + detail }); }
        static float Max(params float[] values) { float m = 0; foreach (float v in values) m = Mathf.Max(m, v); return m; }
        static string R(float v) { return v.ToString("0.###"); }
        static string Mm(float metres) { return (metres * 1000).ToString("0.0") + " mm"; }
    }
}
