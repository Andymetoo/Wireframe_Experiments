using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector2 = System.Numerics.Vector2;

namespace ColdSnap
{
    /// <summary>
    /// Replaceable, scene-owned geometric presentation. Simulation never reads a visual
    /// transform: every skater, puck and stick is drawn from the fixed-step core state.
    /// </summary>
    public sealed class HockeyRenderer : MonoBehaviour
    {
        static readonly Color Ice = new Color(.76f, .87f, .89f);
        static readonly Color IceLine = new Color(.69f, .82f, .85f);
        static readonly Color Blue = new Color(.12f, .43f, .79f);
        static readonly Color Orange = new Color(.95f, .37f, .13f);
        static readonly Color Red = new Color(.76f, .19f, .22f);
        static readonly Color Cream = new Color(.94f, .92f, .79f);
        static readonly Color Navy = new Color(.055f, .085f, .12f);
        static readonly Color Metal = new Color(.52f, .63f, .65f);
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly Dictionary<int, SkaterVisual> skaters = new Dictionary<int, SkaterVisual>();
        readonly Transform[] puckTrail = new Transform[6];
        readonly Vector3[] trailPositions = new Vector3[7];
        readonly float[] trailAges = new float[7];
        Transform generatedRoot, puck, puckRim, puckShadow, ownStick;
        Material material;
        Camera viewCamera;
        HockeySession current;
        float lookYaw, lookPitch = 10, animationTime;
        int cameraMode, trailCount;

        sealed class SkaterVisual
        {
            public Transform Root, Body, LeftLeg, RightLeg, LeftArm, RightArm, Stick, Shadow;
            public Transform SaveGlove, SaveUpperArm, SaveForearm;
            public float PreviousStickCooldown, Swing;
        }

        public Camera ViewCamera { get { return viewCamera; } }
        public int CameraMode { get { return cameraMode; } }
        public static Vector3 World(NVector2 position, float height = 0)
        { return new Vector3(position.X, height, position.Y); }

        public void Build(HockeySession session)
        {
            Clear();
            current = session;
            Shader shader = Resources.Load<Shader>("ColdSnapSurface");
            if (shader == null) shader = Shader.Find("ColdSnap/Surface");
            if (shader == null) throw new InvalidOperationException("Missing Cold Snap geometric surface shader.");
            material = new Material(shader) { name = "Cold Snap / procedural surfaces", hideFlags = HideFlags.DontSave };
            generatedRoot = Child("Cold Snap / generated rink", transform);
            viewCamera = Child("On-ice camera", generatedRoot).gameObject.AddComponent<Camera>();
            viewCamera.tag = "MainCamera";
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(.045f, .075f, .105f);
            viewCamera.nearClipPlane = .055f;
            viewCamera.farClipPlane = 200;
            viewCamera.fieldOfView = 78;
            viewCamera.allowHDR = false;
            viewCamera.allowMSAA = true;
            BuildRink();
            BuildArena();
            foreach (var skater in session.Skaters) skaters.Add(skater.Id, BuildSkater(skater));
            var puckGeometry = new Geometry();
            // The physical radius is 12.5cm. An 18cm visual silhouette is a deliberate
            // restrained aid for identifying a distant black puck in the first-person view.
            puckGeometry.Cylinder(Vector3.zero, .175f, .10f, new Color(.025f, .035f, .045f), 14);
            puckGeometry.Ring(new Vector3(0, .051f, 0), .11f, .13f, Metal * .60f, 14);
            puck = MeshObject("Physical puck", puckGeometry, generatedRoot);
            var rimGeometry = new Geometry();
            // A narrow pale outline helps read the black disc against other dark equipment.
            rimGeometry.Ring(Vector3.zero, .176f, .18f, new Color(.53f, .70f, .72f), 20);
            puckRim = MeshObject("Puck edge / geometric visibility aid", rimGeometry, puck);
            puckRim.localPosition = Vector3.up * .005f;
            var shadowGeometry = new Geometry();
            shadowGeometry.Disc(Vector3.zero, .19f, new Color(.46f, .61f, .65f), 20);
            shadowGeometry.Ring(Vector3.zero, .19f, .225f, new Color(.58f, .72f, .75f), 20);
            puckShadow = MeshObject("Puck ground projection", shadowGeometry, generatedRoot);
            for (int i = 0; i < puckTrail.Length; i++)
            {
                var trailGeometry = new Geometry();
                trailGeometry.Edge(Vector3.zero, Vector3.forward, .016f, new Color(.32f, .48f, .53f));
                puckTrail[i] = MeshObject("Airborne puck trail " + i, trailGeometry, generatedRoot);
                puckTrail[i].gameObject.SetActive(false);
            }
            trailCount = 0;
            ownStick = BuildStick(generatedRoot, true);
            animationTime = 0;
            Sync(session, 0);
        }

        /// <param name="yaw">World yaw in degrees: zero looks toward the blue attacking end.</param>
        /// <param name="pitch">Downward look in degrees. Body facing and momentum remain independent.</param>
        public void SetView(float yaw, float pitch, int mode = 0)
        {
            lookYaw = yaw;
            lookPitch = Mathf.Clamp(pitch, -35, 65);
            cameraMode = Mathf.Clamp(mode, 0, 2);
            if (current != null && viewCamera != null) UpdateCamera(current);
        }

        public void Sync(HockeySession session, float dt)
        {
            if (session == null || viewCamera == null) return;
            current = session;
            animationTime += Mathf.Max(0, dt);
            foreach (var state in session.Skaters)
            {
                SkaterVisual visual;
                if (!skaters.TryGetValue(state.Id, out visual))
                {
                    visual = BuildSkater(state);
                    skaters.Add(state.Id, visual);
                }
                float speed = state.Speed;
                visual.Root.position = World(state.Position);
                visual.Root.rotation = Quaternion.Euler(0, state.Facing * Mathf.Rad2Deg, 0);
                bool firstPerson = state.Id == session.Player.Id && cameraMode == 0;
                visual.Body.gameObject.SetActive(!firstPerson);
                visual.Stick.gameObject.SetActive(!firstPerson);
                visual.Shadow.gameObject.SetActive(!firstPerson);
                float stride = Mathf.Sin(animationTime * (3 + speed * .9f) + state.Id * 1.1f) * Mathf.Clamp01(speed / 3);
                float lean = Mathf.Clamp(speed * 1.3f, 0, 13);
                visual.Body.localRotation = Quaternion.Euler(lean + (state.Stun > 0 ? 12 : 0), 0, state.Stun > 0 ? 9 : 0);
                if (state.Goalie)
                {
                    float distance = NVector2.Distance(state.Position, session.Puck.Position);
                    float highPuck = Mathf.Clamp01((session.Puck.Height - .45f) / .85f);
                    float crouch = Mathf.Clamp01((10 - distance) / 8) * (1 - highPuck * .75f);
                    visual.Body.localPosition = Vector3.down * crouch * .16f;
                    visual.LeftLeg.localRotation = Quaternion.Euler(0, -8, -12 - crouch * 20);
                    visual.RightLeg.localRotation = Quaternion.Euler(0, 8, 12 + crouch * 20);
                    visual.LeftArm.localRotation = Quaternion.Euler(-8, 0, 12 + crouch * 22);
                    visual.RightArm.localRotation = Quaternion.Euler(-8, 0, -12 - crouch * 15);
                    PoseSaveHand(visual, state);
                }
                else
                {
                    visual.LeftLeg.localRotation = Quaternion.Euler(stride * 20 - lean, 0, -Mathf.Max(0, stride) * 12);
                    visual.RightLeg.localRotation = Quaternion.Euler(-stride * 20 - lean, 0, Mathf.Max(0, -stride) * 12);
                    visual.LeftArm.localRotation = Quaternion.Euler(stride * -4, 0, 0);
                    visual.RightArm.localRotation = Quaternion.Euler(stride * 4, 0, 0);
                }
                if (state.StickCooldown > visual.PreviousStickCooldown + .05f) visual.Swing = .32f;
                visual.PreviousStickCooldown = state.StickCooldown;
                visual.Swing = Mathf.Max(0, visual.Swing - dt);
                float shot = visual.Swing > 0 ? Mathf.Sin(visual.Swing / .32f * Mathf.PI) : 0;
                bool controlling = session.Puck.ControllerId == state.Id;
                visual.Stick.localRotation = Quaternion.Euler(-shot * 25, shot * -35 + (controlling ? Mathf.Sin(animationTime * 4) * 4 : 0), 0);
            }
            float puckHeight = Mathf.Max(.05f, session.Puck.Height);
            puck.position = World(session.Puck.Position, puckHeight);
            float airborne = Mathf.Clamp01((puckHeight - .09f) * 3);
            // Orientation is presentation only. The core models height, gravity and impacts,
            // not rigid-body angular momentum; a small airborne wobble communicates lift.
            puck.rotation = Quaternion.Euler(Mathf.Sin(animationTime * 19) * 11 * airborne,
                animationTime * session.Puck.Velocity.Length() * 5,
                Mathf.Cos(animationTime * 13) * 7 * airborne);
            puckShadow.position = World(session.Puck.Position, .024f);
            float shadowScale = 1 + Mathf.Min(2.2f, puckHeight) * .16f;
            puckShadow.localScale = new Vector3(shadowScale, 1, shadowScale);
            SyncPuckTrail(session.Puck, dt);
            UpdateCamera(session);
        }

        void PoseSaveHand(SkaterVisual visual, SkaterState state)
        {
            bool leftHand = state.GoalieHandReach < 0;
            visual.LeftArm.gameObject.SetActive(!leftHand);
            visual.RightArm.gameObject.SetActive(leftHand);
            Vector3 glove = World(state.Position, Mathf.Clamp(state.GoalieHandHeight, .6f, 1.9f))
                + visual.Root.right * Mathf.Clamp(state.GoalieHandReach, -.85f, .85f)
                + visual.Root.forward * .17f;
            Vector3 shoulder = visual.Body.TransformPoint(new Vector3(leftHand ? -.36f : .36f, 1.30f, .01f));
            Vector3 elbow = Vector3.Lerp(shoulder, glove, .46f) + visual.Root.forward * .14f + Vector3.down * .07f;
            visual.SaveGlove.SetPositionAndRotation(glove, visual.Root.rotation);
            SetSegment(visual.SaveUpperArm, shoulder, elbow);
            SetSegment(visual.SaveForearm, elbow, glove);
        }

        static void SetSegment(Transform segment, Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < .000001f) { segment.gameObject.SetActive(false); return; }
            segment.gameObject.SetActive(true);
            segment.SetPositionAndRotation(start, Quaternion.LookRotation(delta));
            segment.localScale = new Vector3(1, 1, delta.magnitude);
        }

        void SyncPuckTrail(PuckState state, float dt)
        {
            for (int i = 0; i < trailCount; i++) trailAges[i] += Mathf.Max(0, dt);
            Vector3 position = World(state.Position, state.Height);
            if (state.Height <= .11f || state.Velocity.LengthSquared() + state.VerticalVelocity * state.VerticalVelocity < 25)
                trailCount = 0;
            else if (dt > 0 && (trailCount == 0 || Vector3.Distance(trailPositions[0], position) > .025f))
            {
                // Discontinuous faceoff resets never leave a line spanning the rink.
                if (trailCount > 0 && Vector3.Distance(trailPositions[0], position) > 5) trailCount = 0;
                trailCount = Mathf.Min(trailPositions.Length, trailCount + 1);
                for (int i = trailCount - 1; i > 0; i--) { trailPositions[i] = trailPositions[i - 1]; trailAges[i] = trailAges[i - 1]; }
                trailPositions[0] = position; trailAges[0] = 0;
            }
            for (int i = 0; i < puckTrail.Length; i++)
            {
                bool active = i + 1 < trailCount && trailAges[i + 1] < .12f;
                puckTrail[i].gameObject.SetActive(active);
                if (!active) continue;
                SetSegment(puckTrail[i], trailPositions[i + 1], trailPositions[i]);
                float thin = Mathf.Lerp(1, .35f, Mathf.Clamp01(trailAges[i + 1] / .12f));
                Vector3 scale = puckTrail[i].localScale;
                puckTrail[i].localScale = new Vector3(thin, thin, scale.z);
            }
        }

        void UpdateCamera(HockeySession session)
        {
            Vector3 position = World(session.Player.Position);
            Quaternion look = Quaternion.Euler(lookPitch, lookYaw, 0);
            Quaternion horizontal = Quaternion.Euler(0, lookYaw, 0);
            if (cameraMode == 0)
            {
                // Deliberately no camera roll, stride bob or velocity-based camera steering.
                viewCamera.transform.SetPositionAndRotation(position + Vector3.up * 1.65f, look);
                viewCamera.fieldOfView = 78;
            }
            else if (cameraMode == 1)
            {
                Vector3 desired = position + horizontal * new Vector3(0, 3.5f, -5.2f);
                // Keep the chase camera inside the boards so it cannot look through the stands.
                desired.x = Mathf.Clamp(desired.x, -13.6f, 13.6f);
                desired.z = Mathf.Clamp(desired.z, -28.6f, 28.6f);
                if (Mathf.Abs(desired.x) > 8 && Mathf.Abs(desired.z) > 23)
                {
                    Vector2 cornerOffset = new Vector2(Mathf.Abs(desired.x) - 8, Mathf.Abs(desired.z) - 23);
                    if (cornerOffset.magnitude > 5.6f) cornerOffset = cornerOffset.normalized * 5.6f;
                    desired.x = Mathf.Sign(desired.x) * (8 + cornerOffset.x);
                    desired.z = Mathf.Sign(desired.z) * (23 + cornerOffset.y);
                }
                viewCamera.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(position + horizontal * Vector3.forward * 4 + Vector3.up * .8f - desired));
                viewCamera.fieldOfView = 74;
            }
            else
            {
                viewCamera.transform.SetPositionAndRotation(new Vector3(0, 39, -20), Quaternion.Euler(62, 0, 0));
                viewCamera.fieldOfView = 66;
            }
            ownStick.gameObject.SetActive(cameraMode == 0);
            ownStick.SetPositionAndRotation(position, horizontal);
            SkaterVisual human;
            if (skaters.TryGetValue(session.Player.Id, out human))
            {
                float shot = human.Swing > 0 ? Mathf.Sin(human.Swing / .32f * Mathf.PI) : 0;
                ownStick.rotation = horizontal * Quaternion.Euler(-shot * 24, -shot * 30, 0);
            }
        }

        void BuildRink()
        {
            var ice = new Geometry();
            var boundary = Boundary();
            for (int i = 0; i < boundary.Count; i++)
                ice.Triangle(Vector3.zero, boundary[(i + 1) % boundary.Count], boundary[i], Ice);
            // Broad, nearly imperceptible ice bands provide motion reference without texture artwork.
            for (int z = -22; z <= 22; z += 4)
                ice.Box(new Vector3(0, .001f, z), new Vector3(27.9f, .002f, .025f), IceLine);
            MeshObject("Opaque ice surface", ice, generatedRoot);
            var lines = new Geometry();
            lines.Box(new Vector3(0, .006f, 0), new Vector3(28, .006f, .22f), Red);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                lines.Box(new Vector3(0, .006f, sign * 9), new Vector3(28, .006f, .34f), Blue);
                lines.Box(new Vector3(0, .006f, sign * 25), new Vector3(27.2f, .006f, .11f), Red);
                foreach (float x in new[] { -7f, 7f })
                {
                    var center = new Vector3(x, .018f, sign * 18);
                    // Broad paint survives the grazing first-person angle at the far end.
                    lines.Ring(center, 3.62f, 3.77f, Red, 64);
                    lines.Disc(center + Vector3.up * .004f, .20f, Red, 20);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        lines.Box(center + new Vector3(side * 3.68f, .001f, -.4f), new Vector3(.08f, .006f, .8f), Red);
                        lines.Box(center + new Vector3(side * 3.68f, .001f, .4f), new Vector3(.08f, .006f, .8f), Red);
                    }
                    lines.Disc(new Vector3(x, .011f, sign * 6.7f), .18f, Red, 16);
                }
                // Painted semicircular creases are geometry on the ice, never image textures.
                Color crease = new Color(.43f, .68f, .82f);
                Vector3 goalCenter = new Vector3(0, .008f, sign * 25);
                for (int step = 0; step < 32; step++)
                {
                    float a = step * Mathf.PI / 32, b = (step + 1) * Mathf.PI / 32;
                    Vector3 va = goalCenter + new Vector3(Mathf.Cos(a) * 2.15f, 0, -sign * Mathf.Sin(a) * 2.15f);
                    Vector3 vb = goalCenter + new Vector3(Mathf.Cos(b) * 2.15f, 0, -sign * Mathf.Sin(b) * 2.15f);
                    lines.Triangle(goalCenter, va, vb, crease);
                    lines.Edge(va + Vector3.up * .003f, vb + Vector3.up * .003f, .06f, Red);
                }
                BuildGoal(sign);
            }
            lines.Ring(new Vector3(0, .018f, 0), 3.63f, 3.78f, Blue, 64);
            lines.Disc(new Vector3(0, .015f, 0), .25f, Blue, 20);
            // Central faceted snowflake is composed of narrow solid triangles and bars.
            Color centerPaint = new Color(.59f, .73f, .79f);
            for (int spoke = 0; spoke < 6; spoke++)
            {
                float angle = spoke * Mathf.PI / 3;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Vector3 side = new Vector3(direction.z, 0, -direction.x);
                Vector3 start = direction * .6f + Vector3.up * .008f, end = direction * 2.6f + Vector3.up * .008f;
                lines.Edge(start, end, .09f, centerPaint);
                lines.Edge(direction * 1.6f + Vector3.up * .008f, direction * 2.1f + side * .6f + Vector3.up * .008f, .09f, centerPaint);
                lines.Edge(direction * 1.6f + Vector3.up * .008f, direction * 2.1f - side * .6f + Vector3.up * .008f, .09f, centerPaint);
            }
            MeshObject("Rink markings / geometric paint", lines, generatedRoot);
            var boards = new Geometry();
            float boardTop = RinkPhysics.BoardHeight, barrierTop = RinkPhysics.GlassHeight;
            for (int i = 0; i < boundary.Count; i++)
            {
                Vector3 a = boundary[i], b = boundary[(i + 1) % boundary.Count];
                Color panel = (i & 1) == 0 ? new Color(.83f, .87f, .85f) : new Color(.78f, .83f, .82f);
                // White board and kick strip occupy adjacent faces. Overlapping coplanar
                // quads create severe depth flicker when viewed along the ice.
                boards.Quad(a + Vector3.up * .15f, b + Vector3.up * .15f, b + Vector3.up * boardTop, a + Vector3.up * boardTop, panel);
                boards.Quad(a, b, b + Vector3.up * .15f, a + Vector3.up * .15f, new Color(.91f, .65f, .22f));
                boards.Edge(a + Vector3.up * (boardTop + .01f), b + Vector3.up * (boardTop + .01f), .09f, Navy);
                boards.Edge(a, a + Vector3.up * (boardTop - .03f), .025f, new Color(.52f, .62f, .64f));
                // The safety barrier is drawn as sparse opaque posts, avoiding sorted transparency.
                if (i % 2 == 0) boards.Edge(a + Vector3.up * (boardTop + .05f), a + Vector3.up * barrierTop, .045f, Metal * .60f);
                boards.Edge(a + Vector3.up * barrierTop, b + Vector3.up * barrierTop, .035f, Metal * .65f);
            }
            MeshObject("Rounded dasher boards and barrier", boards, generatedRoot);
        }

        static List<Vector3> Boundary()
        {
            var result = new List<Vector3>();
            // All corners sample the same radius and extents as RinkPhysics.
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = corner == 0 || corner == 3 ? 8 : -8;
                float cz = corner < 2 ? 23 : -23;
                for (int j = 0; j <= 12; j++)
                {
                    float angle = (corner * 90 + j * 90f / 12) * Mathf.Deg2Rad;
                    result.Add(new Vector3(cx + Mathf.Cos(angle) * 6, 0, cz + Mathf.Sin(angle) * 6));
                }
            }
            return result;
        }

        void BuildGoal(int sign)
        {
            var net = new Geometry();
            float front = sign * RinkPhysics.GoalLine, back = sign * (RinkPhysics.GoalLine + RinkPhysics.GoalDepth);
            float mouth = RinkPhysics.GoalHalfWidth, height = RinkPhysics.GoalHeight;
            float upright = height - .09f, roof = height - .03f;
            Vector3 fl = new Vector3(-mouth, .09f, front), fr = new Vector3(mouth, .09f, front);
            Vector3 bl = new Vector3(-mouth, .09f, back), br = new Vector3(mouth, .09f, back);
            net.Edge(fl, fl + Vector3.up * upright, RinkPhysics.PostRadius * 2, Red);
            net.Edge(fr, fr + Vector3.up * upright, RinkPhysics.PostRadius * 2, Red);
            net.Edge(fl + Vector3.up * upright, fr + Vector3.up * upright, RinkPhysics.PostRadius * 2, Red);
            net.Edge(fl, bl, .09f, Red); net.Edge(fr, br, .09f, Red); net.Edge(bl, br, .09f, Red);
            net.Edge(bl, bl + Vector3.up * upright, .06f, Cream);
            net.Edge(br, br + Vector3.up * upright, .06f, Cream);
            net.Edge(bl + Vector3.up * upright, br + Vector3.up * upright, .06f, Cream);
            net.Edge(fl + Vector3.up * upright, bl + Vector3.up * upright, .06f, Cream);
            net.Edge(fr + Vector3.up * upright, br + Vector3.up * upright, .06f, Cream);
            Color cord = new Color(.53f, .65f, .66f);
            for (float x = -mouth; x <= mouth + .01f; x += .225f)
            {
                net.Edge(new Vector3(x, .1f, back), new Vector3(x, roof, back), .013f, cord);
                net.Edge(new Vector3(x, roof, front), new Vector3(x, roof, back), .013f, cord);
            }
            for (float row = .1f; row <= roof; row += .18f)
            {
                net.Edge(new Vector3(-mouth, row, back), new Vector3(mouth, row, back), .013f, cord);
                net.Edge(new Vector3(-mouth, row, front), new Vector3(-mouth, row, back), .013f, cord);
                net.Edge(new Vector3(mouth, row, front), new Vector3(mouth, row, back), .013f, cord);
            }
            for (float depth = .225f; depth < RinkPhysics.GoalDepth; depth += .225f)
            {
                float z = front + sign * depth;
                net.Edge(new Vector3(-mouth, .1f, z), new Vector3(-mouth, roof, z), .013f, cord);
                net.Edge(new Vector3(mouth, .1f, z), new Vector3(mouth, roof, z), .013f, cord);
                net.Edge(new Vector3(-mouth, roof, z), new Vector3(mouth, roof, z), .013f, cord);
            }
            MeshObject(sign > 0 ? "Orange goal / blue attack" : "Blue goal / defend", net, generatedRoot);
        }

        void BuildArena()
        {
            var arena = new Geometry();
            arena.Box(new Vector3(0, -.25f, 0), new Vector3(54, .45f, 84), Navy);
            Color wall = new Color(.105f, .155f, .205f);
            arena.Box(new Vector3(-25, 7, 0), new Vector3(1, 14, 82), wall);
            arena.Box(new Vector3(25, 7, 0), new Vector3(1, 14, 82), wall);
            arena.Box(new Vector3(0, 7, -40), new Vector3(50, 14, 1), wall);
            arena.Box(new Vector3(0, 7, 40), new Vector3(50, 14, 1), wall);
            for (int side = -1; side <= 1; side += 2)
            {
                Color endColor = side > 0 ? Orange : Blue;
                arena.Box(new Vector3(0, 5.4f, side * 39.3f), new Vector3(16, 1.5f, .15f), endColor * .60f);
                // Distinct end-wall chevrons make orientation readable from the skater's view.
                for (int x = -6; x <= 6; x += 3)
                {
                    Vector3 center = new Vector3(x, 5.4f, side * 39.15f);
                    arena.Edge(center + new Vector3(-.5f, -.45f, 0), center + new Vector3(.3f, 0, 0), .09f, Cream);
                    arena.Edge(center + new Vector3(.3f, 0, 0), center + new Vector3(-.5f, .45f, 0), .09f, Cream);
                }
                for (int row = 0; row < 5; row++)
                {
                    float elevation = .5f + row * .8f;
                    float x = side * (15.4f + row * 1.3f);
                    arena.Box(new Vector3(x, elevation, 0), new Vector3(1.4f, .65f, 55), new Color(.14f + row * .012f, .21f + row * .012f, .28f + row * .012f));
                    for (int seat = -22; seat <= 22; seat += 2)
                    {
                        Color crowd = ((seat + row) % 3 == 0 ? Blue : (seat + row) % 3 == 1 ? Orange : Metal) * .55f;
                        arena.Box(new Vector3(x, elevation + .54f, seat), new Vector3(.48f, .55f, .65f), crowd);
                        arena.Cylinder(new Vector3(x, elevation + .94f, seat), .13f, .22f, new Color(.44f, .43f, .38f), 5);
                    }
                }
                for (int row = 0; row < 4; row++)
                    arena.Box(new Vector3(0, .7f + row * .8f, side * (31 + row * 1.5f)), new Vector3(28, .7f, 1.4f), new Color(.15f, .23f, .29f));
            }
            for (int z = -30; z <= 30; z += 10)
            {
                arena.Edge(new Vector3(-25, 12, z), new Vector3(0, 14, z), .22f, new Color(.18f, .27f, .33f));
                arena.Edge(new Vector3(0, 14, z), new Vector3(25, 12, z), .22f, new Color(.18f, .27f, .33f));
                arena.Box(new Vector3(0, 12.9f, z), new Vector3(9, .08f, .32f), new Color(.90f, .96f, .91f));
            }
            MeshObject("Low-polygon winter arena", arena, generatedRoot);
        }

        SkaterVisual BuildSkater(SkaterState state)
        {
            var visual = new SkaterVisual();
            visual.Root = Child((state.Goalie ? "Goalkeeper " : "Skater ") + state.Id, generatedRoot);
            visual.Body = Child("Animated equipment", visual.Root);
            Color jersey = state.Team == 0 ? Blue : Orange;
            var body = new Geometry();
            body.TaperedBox(new Vector3(0, 1.12f, 0), new Vector3(.48f, .55f, .31f), new Vector3(.65f, .55f, .37f), jersey);
            body.Box(new Vector3(0, .87f, 0), new Vector3(.51f, .13f, .33f), Cream);
            body.Box(new Vector3(0, 1.15f, .192f), new Vector3(.17f, .22f, .018f), Cream);
            body.Box(new Vector3(0, .76f, -.01f), new Vector3(.54f, .28f, .34f), Navy);
            body.Cylinder(new Vector3(0, 1.54f, .04f), .18f, .26f, new Color(.70f, .57f, .43f), 8);
            body.Cylinder(new Vector3(0, 1.69f, .02f), .215f, .22f, state.Team == 0 ? Navy : Cream, 10);
            body.Box(new Vector3(0, 1.70f, .18f), new Vector3(.36f, .085f, .16f), state.Team == 0 ? Navy : Cream);
            if (state.Goalie)
            {
                body.Box(new Vector3(0, 1.52f, .23f), new Vector3(.36f, .30f, .035f), Cream);
                for (int bar = -1; bar <= 1; bar++) body.Edge(new Vector3(bar * .11f, 1.40f, .253f), new Vector3(bar * .11f, 1.65f, .253f), .018f, Navy);
                for (int bar = 0; bar < 3; bar++) body.Edge(new Vector3(-.17f, 1.44f + bar * .075f, .253f), new Vector3(.17f, 1.44f + bar * .075f, .253f), .018f, Navy);
            }
            else
            {
                body.Edge(new Vector3(-.18f, 1.55f, .20f), new Vector3(.18f, 1.55f, .20f), .028f, Metal);
                // Jersey number is a seven-segment geometric badge, no imported font texture.
                int[] jerseyNumbers = { 9, 7, 12, 11, 17, 4, 30, 31 };
                Number(body, jerseyNumbers[state.Id % jerseyNumbers.Length], new Vector3(0, 1.12f, -.188f), Cream);
            }
            MeshObject("Helmet jersey and protective equipment", body, visual.Body);
            visual.LeftLeg = BuildLeg(visual.Body, -.17f, state.Goalie, jersey);
            visual.RightLeg = BuildLeg(visual.Body, .17f, state.Goalie, jersey);
            visual.LeftArm = BuildArm(visual.Body, -.36f, state.Goalie, jersey);
            visual.RightArm = BuildArm(visual.Body, .36f, state.Goalie, jersey);
            if (state.Goalie)
            {
                var gloveGeometry = new Geometry();
                gloveGeometry.Box(Vector3.zero, new Vector3(.37f, .36f, .22f), Cream);
                gloveGeometry.Box(new Vector3(0, -.07f, .115f), new Vector3(.35f, .055f, .016f), jersey);
                visual.SaveGlove = MeshObject("Height-aware save glove", gloveGeometry, visual.Root);
                var sleeve = new Geometry(); sleeve.Edge(Vector3.zero, Vector3.forward, .21f, jersey);
                visual.SaveUpperArm = MeshObject("Save arm / shoulder", sleeve, visual.Root);
                var forearm = new Geometry(); forearm.Edge(Vector3.zero, Vector3.forward, .18f, jersey);
                visual.SaveForearm = MeshObject("Save arm / forearm", forearm, visual.Root);
            }
            // Keep the blade on the ice while the torso leans into a stride.
            visual.Stick = BuildStick(visual.Root, false);
            var shadow = new Geometry();
            shadow.Disc(Vector3.zero, .42f, new Color(.55f, .69f, .73f), 20);
            visual.Shadow = MeshObject("Ground contact", shadow, visual.Root);
            visual.Shadow.localPosition = Vector3.up * .004f;
            visual.Shadow.localScale = new Vector3(1, 1, 1.3f);
            return visual;
        }

        Transform BuildLeg(Transform parent, float x, bool goalie, Color jersey)
        {
            var geometry = new Geometry();
            geometry.Box(new Vector3(0, -.16f, 0), new Vector3(.23f, .34f, .27f), Navy);
            geometry.Box(new Vector3(0, -.44f, .025f), new Vector3(.19f, .30f, .22f), jersey);
            geometry.Box(new Vector3(0, -.40f, .028f), new Vector3(.20f, .08f, .23f), Cream);
            geometry.Box(new Vector3(0, -.61f, .08f), new Vector3(.20f, .14f, .37f), Navy);
            geometry.Edge(new Vector3(0, -.707f, -.11f), new Vector3(0, -.707f, .27f), .034f, Metal);
            if (goalie)
            {
                geometry.Box(new Vector3(0, -.39f, .22f), new Vector3(.34f, .64f, .16f), Cream);
                geometry.Box(new Vector3(0, -.39f, .308f), new Vector3(.065f, .62f, .02f), jersey);
            }
            Transform leg = MeshObject(x < 0 ? "Left skate and pads" : "Right skate and pads", geometry, parent);
            leg.localPosition = new Vector3(x, .76f, 0);
            return leg;
        }

        Transform BuildArm(Transform parent, float x, bool goalie, Color jersey)
        {
            var geometry = new Geometry();
            geometry.Edge(Vector3.zero, new Vector3(x * .18f, -.25f, .07f), .21f, jersey);
            geometry.Edge(new Vector3(x * .18f, -.25f, .07f), new Vector3(-x * .28f, -.35f, .34f), .17f, jersey);
            geometry.Box(new Vector3(-x * .28f, -.35f, .34f), new Vector3(goalie ? .28f : .19f, goalie ? .28f : .16f, .22f), goalie ? Cream : Navy);
            Transform arm = MeshObject(x < 0 ? "Left glove and sleeve" : "Right glove and sleeve", geometry, parent);
            arm.localPosition = new Vector3(x, 1.30f, .01f);
            return arm;
        }

        Transform BuildStick(Transform parent, bool firstPerson)
        {
            var geometry = new Geometry();
            // The forward grip remains visible at a level shooting view. The blade stays
            // on the real handling region and comes into view when looking down at the puck.
            Vector3 top = firstPerson ? new Vector3(.36f, 1.24f, .58f) : new Vector3(.27f, 1.08f, .32f);
            Vector3 heel = new Vector3(.40f, .10f, 1.07f);
            geometry.Edge(top, heel, .042f, firstPerson ? new Color(.48f, .33f, .20f) : new Color(.62f, .46f, .27f));
            geometry.Edge(top, Vector3.Lerp(top, heel, .13f), .052f, Navy);
            geometry.Edge(heel, new Vector3(-.16f, .09f, 1.17f), .072f, Navy);
            geometry.Edge(new Vector3(-.16f, .09f, 1.17f), new Vector3(-.26f, .10f, 1.25f), .071f, Navy);
            for (int tape = 0; tape < 3; tape++)
                geometry.Box(new Vector3(.06f + tape * .10f, .095f, 1.13f - tape * .017f), new Vector3(.035f, .082f, .09f), Cream * .75f);
            if (firstPerson)
            {
                Vector3 grip = Vector3.Lerp(top, heel, .04f);
                geometry.Box(grip, new Vector3(.115f, .105f, .13f), Navy);
                geometry.Edge(new Vector3(.53f, 1.11f, .34f), grip, .13f, Blue);
            }
            return MeshObject(firstPerson ? "First-person stick and glove" : "Hockey stick", geometry, parent);
        }

        static void Number(Geometry g, int number, Vector3 center, Color color)
        {
            if (number > 9)
            {
                Digit(g, number / 10, center - Vector3.right * .11f, color);
                Digit(g, number % 10, center + Vector3.right * .11f, color);
            }
            else Digit(g, number, center, color);
        }

        static void Digit(Geometry g, int number, Vector3 center, Color color)
        {
            int[] masks = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
            int mask = masks[number % 10];
            Vector3[] starts = { new Vector3(-.075f,.12f,0),new Vector3(.075f,.12f,0),new Vector3(.075f,0,0),new Vector3(-.075f,-.12f,0),new Vector3(-.075f,0,0),new Vector3(-.075f,.12f,0),new Vector3(-.075f,0,0) };
            Vector3[] ends = { new Vector3(.075f,.12f,0),new Vector3(.075f,0,0),new Vector3(.075f,-.12f,0),new Vector3(.075f,-.12f,0),new Vector3(-.075f,-.12f,0),new Vector3(-.075f,0,0),new Vector3(.075f,0,0) };
            for (int segment = 0; segment < 7; segment++) if ((mask & (1 << segment)) != 0) g.Edge(center + starts[segment], center + ends[segment], .03f, color);
        }

        static Transform Child(string name, Transform parent)
        { var child = new GameObject(name).transform; child.SetParent(parent, false); return child; }

        Transform MeshObject(string name, Geometry geometry, Transform parent)
        {
            var mesh = new Mesh { name = name, indexFormat = geometry.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(geometry.Vertices); mesh.SetColors(geometry.Colors); mesh.SetNormals(geometry.Normals); mesh.SetTriangles(geometry.Triangles, 0); mesh.RecalculateBounds();
            meshes.Add(mesh);
            var node = Child(name, parent);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return node;
        }

        void Clear()
        {
            if (generatedRoot != null) { generatedRoot.gameObject.SetActive(false); Destroy(generatedRoot.gameObject); }
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            meshes.Clear(); skaters.Clear();
            for (int i = 0; i < puckTrail.Length; i++) puckTrail[i] = null;
            trailCount = 0;
            if (material != null) Destroy(material);
            material = null; current = null; viewCamera = null; generatedRoot = null; ownStick = null;
            puck = null; puckRim = null; puckShadow = null;
        }
        void OnDestroy() { Clear(); }

        sealed class Geometry
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<int> Triangles = new List<int>();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                int start = Vertices.Count; Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
                for (int i = 0; i < 3; i++) { Normals.Add(normal); Colors.Add(color); Triangles.Add(start + i); }
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            { Triangle(a, b, c, color); Triangle(a, c, d, color); }
            public void Box(Vector3 center, Vector3 size, Color color)
            { TaperedBox(center, size, size, color); }
            public void TaperedBox(Vector3 center, Vector3 lower, Vector3 upper, Color color)
            {
                Vector3 a = center + new Vector3(-lower.x, -lower.y, -lower.z) * .5f;
                Vector3 b = center + new Vector3(lower.x, -lower.y, -lower.z) * .5f;
                Vector3 c = center + new Vector3(upper.x, upper.y, -upper.z) * .5f;
                Vector3 d = center + new Vector3(-upper.x, upper.y, -upper.z) * .5f;
                Vector3 e = center + new Vector3(-lower.x, -lower.y, lower.z) * .5f;
                Vector3 f = center + new Vector3(lower.x, -lower.y, lower.z) * .5f;
                Vector3 g = center + new Vector3(upper.x, upper.y, upper.z) * .5f;
                Vector3 h = center + new Vector3(-upper.x, upper.y, upper.z) * .5f;
                Quad(a,d,c,b,color); Quad(e,f,g,h,color); Quad(a,e,h,d,color); Quad(b,c,g,f,color); Quad(d,h,g,c,color); Quad(a,b,f,e,color);
            }
            public void Edge(Vector3 a, Vector3 b, float width, Color color)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 tangent = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.right : Vector3.up).normalized * width * .5f;
                Vector3 bitangent = Vector3.Cross(axis, tangent).normalized * width * .5f;
                Quad(a-tangent-bitangent,b-tangent-bitangent,b+tangent-bitangent,a+tangent-bitangent,color);
                Quad(a+tangent-bitangent,b+tangent-bitangent,b+tangent+bitangent,a+tangent+bitangent,color);
                Quad(a+tangent+bitangent,b+tangent+bitangent,b-tangent+bitangent,a-tangent+bitangent,color);
                Quad(a-tangent+bitangent,b-tangent+bitangent,b-tangent-bitangent,a-tangent-bitangent,color);
            }
            public void Disc(Vector3 center, float radius, Color color, int segments)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                    Triangle(center, center + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius, center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, color);
                }
            }
            public void Ring(Vector3 center, float inner, float outer, Color color, int segments)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                    Vector3 va = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), vb = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                    Quad(center + va * inner, center + vb * inner, center + vb * outer, center + va * outer, color);
                }
            }
            public void Cylinder(Vector3 center, float radius, float height, Color color, int segments)
            {
                Vector3 top = center + Vector3.up * height * .5f, bottom = center - Vector3.up * height * .5f;
                Disc(top, radius, color, segments);
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                    Vector3 va = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, vb = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
                    Quad(bottom + va, top + va, top + vb, bottom + vb, color);
                }
            }
        }
    }
}
