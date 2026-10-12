using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector3 = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace Meridian
{
    /// <summary>
    /// Replaceable presentation. All models, terrain, cockpit and effects are mesh geometry;
    /// the simulation is independent of Unity and never reads back a rendered transform.
    /// </summary>
    public sealed class AirRenderer : MonoBehaviour
    {
        static readonly Color Grass = new Color(.24f, .34f, .20f);
        static readonly Color Olive = new Color(.28f, .34f, .25f);
        static readonly Color Dark = new Color(.065f, .085f, .080f);
        static readonly Color Cream = new Color(.76f, .73f, .57f);
        static readonly Color Amber = new Color(.94f, .64f, .21f);
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly Dictionary<int, AircraftVisual> aircraft = new Dictionary<int, AircraftVisual>();
        readonly Dictionary<int, GroundVisual> groundTargets = new Dictionary<int, GroundVisual>();
        readonly List<Transform> tracerPool = new List<Transform>();
        readonly List<Transform> effectPool = new List<Transform>();
        readonly List<Transform> smokePool = new List<Transform>();
        Material material, skyMaterial, instrumentMaterial;
        Transform generatedRoot, sky, cockpit, speedNeedle, altitudeNeedle, climbNeedle, compass;
        Camera viewCamera;
        int cameraMode;
        bool cameraInitialized;
        float propellerAngle;
        float lookYaw, lookPitch;
        Quaternion flightCameraRotation;

        sealed class AircraftVisual
        {
            public Transform Root, Canopy;
            public readonly List<Transform> Propellers = new List<Transform>();
        }

        sealed class GroundVisual
        {
            public Transform Root, Intact, Wreck, Gun;
        }

        public Camera ViewCamera { get { return viewCamera; } }
        public int CameraMode { get { return cameraMode; } }
        public static Vector3 World(NVector3 p) { return new Vector3(p.X, p.Y, p.Z); }
        public static Quaternion World(NQuaternion q) { return new Quaternion(q.X, q.Y, q.Z, q.W); }

        public void Build(AirMission mission)
        {
            Clear();
            material = MakeMaterial("MeridianSurface", "Meridian/Surface");
            instrumentMaterial = MakeMaterial("MeridianSurface", "Meridian/Surface");
            instrumentMaterial.SetFloat("_FogScale", 0);
            skyMaterial = MakeMaterial("MeridianSky", "Meridian/Sky");
            generatedRoot = NewChild("Meridian — procedural theatre", transform);
            var cameraObject = NewChild("Flight camera", generatedRoot).gameObject;
            viewCamera = cameraObject.AddComponent<Camera>();
            viewCamera.tag = "MainCamera";
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(.66f, .73f, .71f);
            viewCamera.nearClipPlane = .12f;
            viewCamera.farClipPlane = 50000;
            viewCamera.fieldOfView = 68;
            viewCamera.allowHDR = false;
            viewCamera.allowMSAA = true;
            BuildSky();
            BuildTerrain(mission.Terrain, mission.GroundTargets);
            foreach (var plane in mission.Aircraft) aircraft.Add(plane.Id, BuildAircraft(plane));
            foreach (var target in mission.GroundTargets) groundTargets.Add(target.Id, BuildGroundTarget(target));
            BuildSupplyRoute(mission);
            BuildCockpit();
            BuildEffectPools();
            cameraInitialized = false;
            lookYaw = lookPitch = 0;
            Sync(mission, 0);
        }

        public void SetLook(float yawDegrees, float pitchDegrees)
        {
            lookYaw = Mathf.Clamp(yawDegrees, -130, 130);
            lookPitch = Mathf.Clamp(pitchDegrees, -65, 65);
        }

        public void SetCameraMode(int mode)
        {
            cameraMode = mode == 0 ? 0 : 1;
            cameraInitialized = false;
            if (cockpit != null) cockpit.gameObject.SetActive(cameraMode == 0);
        }

        public void Sync(AirMission mission, float dt)
        {
            if (viewCamera == null || mission == null || mission.Player == null) return;
            propellerAngle = (propellerAngle + dt * 2200) % 360;
            foreach (var plane in mission.Aircraft)
            {
                AircraftVisual visual;
                if (!aircraft.TryGetValue(plane.Id, out visual))
                {
                    visual = BuildAircraft(plane);
                    aircraft.Add(plane.Id, visual);
                }
                visual.Root.position = World(plane.Position);
                visual.Root.rotation = World(plane.Rotation);
                visual.Root.gameObject.SetActive(plane.Alive);
                if (visual.Canopy != null) visual.Canopy.gameObject.SetActive(!plane.IsPlayer || cameraMode != 0);
                foreach (var prop in visual.Propellers)
                {
                    // Solid blades alias across the sight at low frame rates. The forward view
                    // omits the player's rotor; external views retain the actual rotating model.
                    prop.gameObject.SetActive(!plane.IsPlayer || cameraMode != 0);
                    prop.localRotation = Quaternion.Euler(0, 0, propellerAngle * (.3f + .7f * plane.Throttle));
                }
            }
            foreach (var target in mission.GroundTargets)
            {
                GroundVisual visual;
                if (!groundTargets.TryGetValue(target.Id, out visual))
                {
                    visual = BuildGroundTarget(target);
                    groundTargets.Add(target.Id, visual);
                }
                visual.Root.SetPositionAndRotation(World(target.Position), target.IsAntiAircraft ? Quaternion.identity : Quaternion.Euler(0, target.Heading * Mathf.Rad2Deg, 0));
                visual.Intact.gameObject.SetActive(target.Alive);
                visual.Wreck.gameObject.SetActive(!target.Alive);
                if (visual.Gun != null && target.Alive)
                {
                    Vector3 aim = World(target.AimDirection);
                    if (aim.sqrMagnitude > .01f) visual.Gun.rotation = Quaternion.LookRotation(aim);
                }
            }

            var player = mission.Player;
            Vector3 position = World(player.Position);
            Quaternion rotation = World(player.Rotation);
            Vector3 desired = position + rotation * (cameraMode == 0 ? new Vector3(0, 1.08f, .2f) : new Vector3(0, 4.2f, -19));
            if (!cameraInitialized || cameraMode == 0)
            {
                viewCamera.transform.position = desired;
                flightCameraRotation = rotation;
                cameraInitialized = true;
            }
            else
            {
                // Follow inertially, but keep the horizon physically tied to attitude.
                viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, desired, 1 - Mathf.Exp(-dt * 8));
                flightCameraRotation = Quaternion.Slerp(flightCameraRotation, rotation, 1 - Mathf.Exp(-dt * 10));
            }
            Quaternion look = Quaternion.Euler(lookPitch, lookYaw, 0);
            viewCamera.transform.rotation = flightCameraRotation * look;
            // The instrument panel belongs to the airframe, not to the pilot's eyes.
            cockpit.localRotation = Quaternion.Inverse(look);
            sky.position = viewCamera.transform.position;
            cockpit.gameObject.SetActive(cameraMode == 0);
            speedNeedle.localRotation = Quaternion.Euler(0, 0, 130 - (float)player.Velocity.Length() * 1.6f);
            altitudeNeedle.localRotation = Quaternion.Euler(0, 0, 130 - player.Position.Y * .11f);
            climbNeedle.localRotation = Quaternion.Euler(0, 0, -player.Velocity.Y * 2);
            compass.localRotation = Quaternion.Euler(0, 0, -rotation.eulerAngles.y);
            SyncEffects(mission);
        }

        static Material MakeMaterial(string resource, string shaderName)
        {
            Shader shader = Resources.Load<Shader>(resource);
            if (shader == null) shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing Meridian shader: " + shaderName);
            return new Material(shader) { name = "Runtime " + shaderName, hideFlags = HideFlags.DontSave };
        }

        void BuildSky()
        {
            var g = new Geometry();
            const int meridians = 32, latitudes = 16;
            for (int y = 0; y < latitudes; y++) for (int x = 0; x < meridians; x++)
            {
                Vector3 a = Sphere(x, y, meridians, latitudes) * 47000;
                Vector3 b = Sphere(x + 1, y, meridians, latitudes) * 47000;
                Vector3 c = Sphere(x + 1, y + 1, meridians, latitudes) * 47000;
                Vector3 d = Sphere(x, y + 1, meridians, latitudes) * 47000;
                g.Quad(a, b, c, d, Color.white);
            }
            sky = MeshObject("Geometric atmospheric dome", g, generatedRoot, skyMaterial);
        }

        static Vector3 Sphere(int x, int y, int meridians, int latitudes)
        {
            float longitude = x * Mathf.PI * 2 / meridians, latitude = y * Mathf.PI / latitudes - Mathf.PI * .5f;
            return new Vector3(Mathf.Cos(longitude) * Mathf.Cos(latitude), Mathf.Sin(latitude), Mathf.Sin(longitude) * Mathf.Cos(latitude));
        }

        void BuildTerrain(TerrainField field, IList<GroundTarget> targets)
        {
            var ocean = new Geometry();
            ocean.Quad(new Vector3(-47000, -.5f, -47000), new Vector3(-47000, -.5f, 47000),
                new Vector3(47000, -.5f, 47000), new Vector3(47000, -.5f, -47000), new Color(.19f, .34f, .37f));
            MeshObject("Sea / continuous horizon", ocean, generatedRoot);
            const float spacing = 150;
            const int chunkCells = 20, chunks = 12;
            const float half = spacing * chunkCells * chunks * .5f;
            // Chunking keeps individual meshes small; all vertices sample the collision height field.
            for (int cz = 0; cz < chunks; cz++) for (int cx = 0; cx < chunks; cx++)
            {
                var g = new Geometry();
                for (int z = 0; z < chunkCells; z++) for (int x = 0; x < chunkCells; x++)
                {
                    float px = -half + (cx * chunkCells + x) * spacing;
                    float pz = -half + (cz * chunkCells + z) * spacing;
                    bool closeToTarget = false;
                    for (int t = 0; t < targets.Count; t++)
                    {
                        NVector3 start = targets[t].Position, end = start + targets[t].Velocity * 600;
                        float mx = px + spacing * .5f, mz = pz + spacing * .5f;
                        if (mx > Mathf.Min(start.X, end.X) - 220 && mx < Mathf.Max(start.X, end.X) + 220 &&
                            mz > Mathf.Min(start.Z, end.Z) - 220 && mz < Mathf.Max(start.Z, end.Z) + 220)
                        { closeToTarget = true; break; }
                    }
                    // Keep gun targets and their collision surfaces in agreement at strafing height.
                    // The broad terrain remains the original low polygon landscape.
                    int subdivisions = closeToTarget ? 10 : 1;
                    float cell = spacing / subdivisions;
                    for (int sz = 0; sz < subdivisions; sz++) for (int sx = 0; sx < subdivisions; sx++)
                    {
                        float tx = px + sx * cell, tz = pz + sz * cell;
                        Vector3 a = Ground(field, tx, tz), b = Ground(field, tx, tz + cell);
                        Vector3 c = Ground(field, tx + cell, tz + cell), d = Ground(field, tx + cell, tz);
                        float max = Mathf.Max(a.y, Mathf.Max(b.y, Mathf.Max(c.y, d.y)));
                        if (max <= .2f) continue;
                        Color color = TerrainColor(tx, tz, (a.y + b.y + c.y + d.y) * .25f, field.Seed);
                        if (((x + z + sx + sz) & 1) == 0) { g.Triangle(a, b, c, color); g.Triangle(a, c, d, color * .97f); }
                        else { g.Triangle(a, b, d, color); g.Triangle(b, c, d, color * .97f); }
                    }
                }
                if (g.Count > 0) MeshObject("Terrain " + cx + "," + cz, g, generatedRoot);
            }
            BuildGroundDetails(field);
        }

        static Vector3 Ground(TerrainField field, float x, float z) { return new Vector3(x, field.Height(x, z), z); }
        static Color TerrainColor(float x, float z, float height, int seed)
        {
            if (height < 8) return new Color(.57f, .56f, .39f);
            if (height > 420) return Color.Lerp(new Color(.34f, .36f, .28f), new Color(.51f, .49f, .39f), Mathf.InverseLerp(420, 1250, height));
            int cellX = Mathf.FloorToInt(x / 600), cellZ = Mathf.FloorToInt(z / 600);
            uint hash = Hash(cellX, cellZ, seed);
            float patch = (hash % 100) * .01f;
            if (patch < .24f) return new Color(.39f, .39f, .23f);
            if (patch < .45f) return new Color(.29f, .36f, .19f);
            return Grass * (.91f + patch * .22f);
        }

        static uint Hash(int x, int z, int seed)
        {
            unchecked { uint n = (uint)(x * 374761393 + z * 668265263 + seed * 1442695041); n = (n ^ (n >> 13)) * 1274126177; return n ^ (n >> 16); }
        }

        void BuildGroundDetails(TerrainField field)
        {
            var g = new Geometry();
            var rng = new System.Random(field.Seed ^ 87519);
            // Compact clusters and field boundaries make terrain movement legible at low altitude.
            for (int settlement = 0; settlement < 65; settlement++)
            {
                float x = (float)(rng.NextDouble() * 22000 - 11000), z = (float)(rng.NextDouble() * 22000 - 11000);
                float h = field.Height(x, z);
                if (h < 18 || h > 430 || Mathf.Abs(field.Height(x + 100, z) - h) > 18) continue;
                int count = 4 + rng.Next(8);
                for (int i = 0; i < count; i++)
                {
                    float bx = x + (i % 4) * 36, bz = z + (i / 4) * 45;
                    Vector3 p = Ground(field, bx, bz);
                    House(g, p, 9 + rng.Next(7), 7 + rng.Next(4));
                }
            }
            for (int grove = 0; grove < 190; grove++)
            {
                float x = (float)(rng.NextDouble() * 24000 - 12000), z = (float)(rng.NextDouble() * 24000 - 12000);
                float h = field.Height(x, z);
                if (h < 16 || h > 600) continue;
                for (int i = 0; i < 9; i++)
                {
                    Vector3 p = Ground(field, x + (float)rng.NextDouble() * 125, z + (float)rng.NextDouble() * 125);
                    float size = 9 + (float)rng.NextDouble() * 8;
                    g.Pyramid(p + Vector3.up * 4, size, size * 1.9f, new Color(.16f, .25f, .16f));
                }
            }
            MeshObject("Villages and wooded groves", g, generatedRoot);
            BuildAirfield(field);
            var clouds = new Geometry();
            for (int i = 0; i < 32; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 28000 - 14000, 3500 + (float)rng.NextDouble() * 1000,
                    (float)rng.NextDouble() * 28000 - 14000);
                for (int n = 0; n < 4; n++) clouds.Octahedron(p + new Vector3(n * 250, n % 2 * 40, 0), new Vector3(470, 65, 280), new Color(.77f, .79f, .74f));
            }
            MeshObject("High polygon cloud banks", clouds, generatedRoot);
        }

        void BuildAirfield(TerrainField field)
        {
            Vector3 center = World(field.AirfieldPosition);
            var g = new Geometry();
            // Sample the same field along every strip segment instead of floating a runway above hills.
            for (int i = -6; i < 6; i++)
            {
                float x1 = center.x + i * 200, x2 = x1 + 200;
                Vector3 a = Ground(field, x1, center.z - 28) + Vector3.up * .25f;
                Vector3 b = Ground(field, x1, center.z + 28) + Vector3.up * .25f;
                Vector3 c = Ground(field, x2, center.z + 28) + Vector3.up * .25f;
                Vector3 d = Ground(field, x2, center.z - 28) + Vector3.up * .25f;
                g.Quad(a, b, c, d, new Color(.24f, .26f, .23f));
                Vector3 stripe = Ground(field, x1 + 70, center.z) + Vector3.up * .45f;
                g.Box(stripe, new Vector3(64, .1f, 2), Cream);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = Ground(field, center.x - 180 + i * 100, center.z + 90);
                House(g, p, 36, 15);
            }
            Vector3 tower = Ground(field, center.x - 240, center.z - 65);
            g.Box(tower + Vector3.up * 14, new Vector3(9, 28, 9), Cream * .75f);
            g.Box(tower + Vector3.up * 30, new Vector3(16, 6, 16), Dark);
            MeshObject("Home airfield", g, generatedRoot);
        }

        static void House(Geometry g, Vector3 p, float width, float height)
        {
            float length = width * 1.4f;
            g.Box(p + Vector3.up * height * .5f, new Vector3(width, height, length), new Color(.58f, .56f, .44f));
            Vector3 a = p + new Vector3(-width * .6f, height, -length * .6f);
            Vector3 b = p + new Vector3(width * .6f, height, -length * .6f);
            Vector3 c = p + new Vector3(width * .6f, height, length * .6f);
            Vector3 d = p + new Vector3(-width * .6f, height, length * .6f);
            Vector3 front = p + new Vector3(0, height + width * .4f, -length * .6f);
            Vector3 back = p + new Vector3(0, height + width * .4f, length * .6f);
            var roof = new Color(.32f, .25f, .20f);
            g.Quad(a, d, back, front, roof); g.Quad(front, back, c, b, roof * 1.14f);
            g.Triangle(a, front, b, roof); g.Triangle(d, c, back, roof);
        }

        AircraftVisual BuildAircraft(AircraftState state)
        {
            if (state.Role == AircraftRole.Bomber) return BuildBomber(state);
            var visual = new AircraftVisual { Root = NewChild(state.Callsign + " / polygon airframe", generatedRoot) };
            bool courier = state.Role == AircraftRole.Courier;
            bool enemy = state.Team != 0;
            Color body = enemy ? new Color(.32f, .34f, .30f) : Olive;
            var g = new Geometry();
            float[] z = { -4.8f, -3.7f, -1.5f, .8f, 3.1f, 4.1f, 4.65f };
            float[] widths = { .12f, .30f, .57f, .67f, .57f, .47f, .08f };
            float[] heights = { .17f, .40f, .61f, .66f, .53f, .45f, .10f };
            for (int ring = 0; ring < z.Length - 1; ring++) for (int segment = 0; segment < 8; segment++)
            {
                float angle1 = segment * Mathf.PI / 4, angle2 = (segment + 1) * Mathf.PI / 4;
                Vector3 a = new Vector3(Mathf.Cos(angle1) * widths[ring], Mathf.Sin(angle1) * heights[ring], z[ring]);
                Vector3 b = new Vector3(Mathf.Cos(angle2) * widths[ring], Mathf.Sin(angle2) * heights[ring], z[ring]);
                Vector3 c = new Vector3(Mathf.Cos(angle2) * widths[ring + 1], Mathf.Sin(angle2) * heights[ring + 1], z[ring + 1]);
                Vector3 d = new Vector3(Mathf.Cos(angle1) * widths[ring + 1], Mathf.Sin(angle1) * heights[ring + 1], z[ring + 1]);
                Color color = segment >= 4 ? Cream * .75f : body;
                if (ring == 4 && enemy) color = Amber;
                g.Quad(a, b, c, d, color);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] wing = enemy ? new[] {
                    new Vector3(.5f*side,-.18f,1.05f),new Vector3(5.3f*side,.02f,.65f),
                    new Vector3(5.3f*side,.02f,-.7f),new Vector3(.55f*side,-.18f,-1.3f)
                } : new[] {
                    new Vector3(.5f*side,-.18f,1.10f),new Vector3(3.6f*side,-.02f,1.04f),
                    new Vector3(5.2f*side,.06f,.58f),new Vector3(5.65f*side,.08f,-.12f),
                    new Vector3(5.15f*side,.06f,-.78f),new Vector3(3.8f*side,-.02f,-1.12f),new Vector3(.5f*side,-.18f,-1.44f)
                };
                g.Extrude(wing, .16f, body, Cream * .78f);
                Vector3[] tail = { new Vector3(.12f*side,.1f,-3.2f),new Vector3(2.1f*side,.17f,-3.58f),
                    new Vector3(1.98f*side,.17f,-4.3f),new Vector3(.12f*side,.1f,-4.48f) };
                g.Extrude(tail, .07f, body, Cream * .75f);
                g.Box(new Vector3(side * 2.4f, -.13f, 1.17f), new Vector3(.075f, .075f, .8f), Dark);
                g.Box(new Vector3(side * 2.78f, -.11f, 1.08f), new Vector3(.06f, .06f, .64f), Dark);
                if (!enemy)
                {
                    Vector3 roundel = new Vector3(side * 3.75f, .086f, -.2f);
                    g.Disc(roundel, .49f, Vector3.up, new Color(.12f,.23f,.35f), 16);
                    g.Disc(roundel + Vector3.up * .005f, .31f, Vector3.up, Cream, 16);
                    g.Disc(roundel + Vector3.up * .01f, .16f, Vector3.up, new Color(.65f,.20f,.14f), 12);
                    if (state.Role == AircraftRole.Wingman)
                    {
                        // Flight bands distinguish the wingmen without changing the Kestrel silhouette.
                        Color flight = (state.Id & 1) == 0 ? Cream : new Color(.37f, .61f, .70f);
                        g.Box(new Vector3(side * 2.6f, .08f, -.04f), new Vector3(.23f, .03f, 2.15f), flight);
                        g.Box(new Vector3(side * 2.95f, .08f, -.04f), new Vector3(.14f, .03f, 2.15f), flight);
                    }
                }
                else
                {
                    // Fictional theatre markings keep opposing silhouettes readable.
                    g.Box(new Vector3(side*3.9f,.11f,-.03f),new Vector3(.36f,.035f,1.15f),Cream);
                    g.Box(new Vector3(side*3.9f,.115f,-.03f),new Vector3(1.1f,.035f,.32f),Cream);
                }
            }
            Vector3 finA = new Vector3(0, .15f, -3.15f), finB = new Vector3(0, 1.8f, -3.9f), finC = new Vector3(0, 1.65f, -4.6f), finD = new Vector3(0, .1f, -4.7f);
            g.Quad(finA, finB, finC, finD, enemy ? Amber : body);
            g.Edge(new Vector3(.62f,.12f,2.5f),new Vector3(.65f,.12f,1.3f),.09f,Dark);
            g.Edge(new Vector3(-.62f,.12f,2.5f),new Vector3(-.65f,.12f,1.3f),.09f,Dark);
            MeshObject("Opaque wings, fuselage and tail", g, visual.Root);
            var glass = new Geometry();
            glass.Octahedron(new Vector3(0,.71f,-.28f),new Vector3(.47f,.6f,1.25f),new Color(.23f,.42f,.46f));
            glass.Edge(new Vector3(-.39f,.8f,.6f),new Vector3(0,1.28f,.15f),.055f,Dark);
            glass.Edge(new Vector3(.39f,.8f,.6f),new Vector3(0,1.28f,.15f),.055f,Dark);
            visual.Canopy = MeshObject("Canopy",glass,visual.Root);
            visual.Root.localScale = new Vector3(state.Definition.Wingspan / (enemy ? 10.6f : 11.3f),
                courier ? 1.4f : 1f, state.Definition.Length / 9.45f);
            if(courier)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    var nacelle=new Geometry();
                    nacelle.Octahedron(new Vector3(side*2.15f,-.02f,.65f),new Vector3(.5f,.53f,1.8f),body);
                    MeshObject("Courier engine",nacelle,visual.Root);
                    visual.Propellers.Add(BuildPropeller(visual.Root,new Vector3(side*2.15f,0,2.45f)));
                }
            }
            else visual.Propellers.Add(BuildPropeller(visual.Root,new Vector3(0,0,4.62f)));
            return visual;
        }

        AircraftVisual BuildBomber(AircraftState state)
        {
            var visual = new AircraftVisual { Root = NewChild(state.Callsign + " / twin-engine bomber", generatedRoot) };
            var g = new Geometry();
            Color body = new Color(.38f, .41f, .30f), belly = Cream * .82f, glass = new Color(.26f, .43f, .47f);
            float[] stations = { -7.4f, -5.8f, -2.7f, 1.5f, 4.6f, 6.2f, 7.4f };
            float[] radii = { .18f, .46f, .89f, 1.08f, .96f, .75f, .12f };
            for (int ring = 0; ring < stations.Length - 1; ring++) for (int face = 0; face < 8; face++)
            {
                float a = face * Mathf.PI / 4, b = (face + 1) * Mathf.PI / 4;
                Vector3 p = new Vector3(Mathf.Cos(a) * radii[ring], Mathf.Sin(a) * radii[ring], stations[ring]);
                Vector3 q = new Vector3(Mathf.Cos(b) * radii[ring], Mathf.Sin(b) * radii[ring], stations[ring]);
                Vector3 r = new Vector3(Mathf.Cos(b) * radii[ring + 1], Mathf.Sin(b) * radii[ring + 1], stations[ring + 1]);
                Vector3 s = new Vector3(Mathf.Cos(a) * radii[ring + 1], Mathf.Sin(a) * radii[ring + 1], stations[ring + 1]);
                Color paint = ring >= 4 && face < 5 ? glass : face >= 4 ? belly : body;
                g.Quad(p, q, r, s, paint);
                if (ring >= 4) { g.Edge(p, s, .04f, Dark); g.Edge(p, q, .04f, Dark); }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] wing = { new Vector3(.7f * side, -.06f, 2.15f), new Vector3(5.7f * side, .10f, 1.48f),
                    new Vector3(10.7f * side, .31f, -.12f), new Vector3(10.3f * side, .31f, -1.35f),
                    new Vector3(5.6f * side, .10f, -1.78f), new Vector3(.7f * side, -.06f, -2.15f) };
                g.Extrude(wing, .24f, body, belly);
                Vector3[] tail = { new Vector3(.2f * side, .2f, -5.2f), new Vector3(3.3f * side, .3f, -5.7f),
                    new Vector3(3.3f * side, .3f, -7.05f), new Vector3(.1f * side, .2f, -7.05f) };
                g.Extrude(tail, .12f, body, belly);
                // Twin vertical fins and a glazed nose make the bomber distinct at a glance.
                g.Quad(new Vector3(3.1f * side, -.08f, -5.65f), new Vector3(3.1f * side, 2.08f, -5.92f),
                    new Vector3(3.1f * side, 2.01f, -7.05f), new Vector3(3.1f * side, -.15f, -7.17f), body);
                g.Edge(new Vector3(3.11f * side, .72f, -6.1f), new Vector3(3.11f * side, .72f, -6.87f), .23f, Cream);
                g.Octahedron(new Vector3(side * 3.4f, -.12f, 1.25f), new Vector3(.69f, .73f, 2.55f), body);
                g.Box(new Vector3(side * 3.4f, -.74f, .1f), new Vector3(.68f, .22f, 1.65f), Dark);
                Vector3 roundel = new Vector3(side * 7.4f, .27f, -.4f);
                g.Disc(roundel, .77f, Vector3.up, new Color(.12f, .23f, .35f), 16);
                g.Disc(roundel + Vector3.up * .008f, .49f, Vector3.up, Cream, 16);
                g.Disc(roundel + Vector3.up * .016f, .25f, Vector3.up, new Color(.65f, .20f, .14f), 12);
                visual.Propellers.Add(BuildPropeller(visual.Root, new Vector3(side * 3.4f, -.12f, 3.73f)));
            }
            // Closed bomb-bay doors are geometry, as are the dorsal observation blister and framing.
            g.Box(new Vector3(0, -.86f, -.5f), new Vector3(.81f, .10f, 3.2f), Dark);
            g.Edge(new Vector3(0, -.925f, -2.1f), new Vector3(0, -.925f, 1.1f), .04f, Cream);
            g.Octahedron(new Vector3(0, 1.08f, -1.4f), new Vector3(.62f, .5f, .67f), glass);
            MeshObject("Bomber fuselage / wings / nacelles / twin fins", g, visual.Root);
            var cockpitGlass = new Geometry();
            cockpitGlass.Octahedron(new Vector3(0, 1.04f, 3.48f), new Vector3(.81f, .64f, 1.35f), glass);
            cockpitGlass.Edge(new Vector3(0, 1.67f, 3.45f), new Vector3(0, 1.05f, 4.82f), .055f, Dark);
            visual.Canopy = MeshObject("Raised bomber cockpit", cockpitGlass, visual.Root);
            visual.Root.localScale = new Vector3(state.Definition.Wingspan / 21.4f, 1.15f, state.Definition.Length / 14.8f);
            return visual;
        }

        GroundVisual BuildGroundTarget(GroundTarget target)
        {
            var visual = new GroundVisual { Root = NewChild(target.Name + " / ground target", generatedRoot) };
            visual.Intact = NewChild("Intact vehicle", visual.Root);
            var g = new Geometry();
            Color paint = new Color(.43f, .41f, .26f), canvas = new Color(.52f, .48f, .32f);
            if (target.IsAntiAircraft)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    g.Edge(new Vector3(side * 3.2f, -1.5f, -2.7f), new Vector3(-side * 3.2f, -1.5f, 2.7f), .5f, paint);
                    for (int n = -2; n <= 2; n++)
                    {
                        g.Box(new Vector3(side * 4.3f, -1.45f, n * 1.65f), new Vector3(1.15f, 1.1f, 1.55f), canvas);
                        g.Box(new Vector3(n * 1.65f, -1.45f, side * 4.3f), new Vector3(1.55f, 1.1f, 1.15f), canvas);
                    }
                }
                g.Box(new Vector3(0, -1.1f, 0), new Vector3(2.6f, 1.8f, 2.6f), paint);
                g.Box(new Vector3(0, .4f, 0), new Vector3(.65f, 1.7f, .65f), Dark);
                MeshObject("Gun carriage and sandbags", g, visual.Intact);
                var gun = new Geometry();
                gun.Box(new Vector3(0, 0, .1f), new Vector3(1.6f, 1.5f, .25f), paint);
                gun.Box(new Vector3(0, -.1f, -.6f), new Vector3(.5f, .55f, 1.3f), Dark);
                gun.Edge(new Vector3(-.25f, .08f, 0), new Vector3(-.25f, .08f, 3.1f), .16f, Dark);
                gun.Edge(new Vector3(.25f, .08f, 0), new Vector3(.25f, .08f, 3.1f), .16f, Dark);
                gun.Box(new Vector3(-.65f, -.4f, -.4f), new Vector3(.65f, .6f, .8f), Cream * .6f);
                visual.Gun = MeshObject("Tracking twin anti-aircraft gun", gun, visual.Intact);
                visual.Gun.localPosition = Vector3.up * 1.3f;
            }
            else
            {
                g.Box(new Vector3(0, -.75f, -.2f), new Vector3(2.6f, .38f, 6.7f), Dark);
                g.Box(new Vector3(0, -.17f, -1.05f), new Vector3(2.65f, .78f, 4.45f), paint);
                g.Box(new Vector3(0, .98f, -1.05f), new Vector3(2.55f, 1.5f, 4.4f), canvas);
                g.Box(new Vector3(0, .4f, 2.05f), new Vector3(2.5f, 2.2f, 2.05f), paint);
                g.Box(new Vector3(0, -.1f, 3.22f), new Vector3(2.45f, 1.0f, .52f), paint);
                g.Box(new Vector3(0, .80f, 3.09f), new Vector3(2.1f, .7f, .05f), new Color(.17f, .29f, .32f));
                g.Box(new Vector3(0, .8f, 3.14f), new Vector3(.08f, .78f, .07f), paint);
                g.Box(new Vector3(0, -.26f, 3.51f), new Vector3(1.1f, .55f, .05f), Dark);
                g.Box(new Vector3(0, -.66f, 3.59f), new Vector3(2.85f, .18f, .17f), Dark);
                for (int side = -1; side <= 1; side += 2)
                {
                    g.Box(new Vector3(side * 1.27f, .83f, 2.17f), new Vector3(.05f, .72f, 1.25f), new Color(.17f, .29f, .32f));
                    g.Box(new Vector3(side * 1.04f, -.06f, 3.52f), new Vector3(.23f, .23f, .07f), Cream);
                    for (int wheel = 0; wheel < 3; wheel++)
                    {
                        float z = wheel == 0 ? 2.32f : -1.42f - (wheel - 1) * 1.35f;
                        g.Box(new Vector3(side * 1.25f, -1.19f, z), new Vector3(.55f, 1.25f, 1.25f), Dark);
                        g.Disc(new Vector3(side * 1.54f, -1.19f, z), .26f, Vector3.right * side, paint, 8);
                    }
                }
                MeshObject("Canvas troop truck / cab / wheels", g, visual.Intact);
                visual.Intact.localScale = new Vector3(target.Extents.X / 1.55f, target.Extents.Y / 2f, target.Extents.Z / 3.65f);
            }
            var wreck = new Geometry();
            Color charcoal = new Color(.13f, .14f, .12f);
            wreck.Box(new Vector3(0, -target.Extents.Y + .45f, 0), new Vector3(target.Extents.X * 1.85f, .9f, target.Extents.Z * 1.65f), charcoal);
            wreck.Octahedron(new Vector3(-.6f, -target.Extents.Y + 1, 1), new Vector3(target.Extents.X * .72f, 1.6f, target.Extents.Z * .48f), charcoal * .8f);
            wreck.Box(new Vector3(target.Extents.X * .8f, -target.Extents.Y + .6f, -1), new Vector3(.5f, 1.2f, 1.2f), Dark);
            visual.Wreck = MeshObject("Blackened wreckage", wreck, visual.Root);
            visual.Root.position = World(target.Position);
            return visual;
        }

        void BuildSupplyRoute(AirMission mission)
        {
            GroundTarget first = null;
            for (int i = 0; i < mission.GroundTargets.Count; i++)
                if (!mission.GroundTargets[i].IsAntiAircraft) { first = mission.GroundTargets[i]; break; }
            if (first == null) return;
            Vector3 forward = new Vector3(Mathf.Sin(first.Heading), 0, Mathf.Cos(first.Heading));
            Vector3 right = new Vector3(forward.z, 0, -forward.x);
            var road = new Geometry();
            for (int i = -42; i < 150; i++)
            {
                Vector3 a = World(first.Position) + right * 6.5f + forward * (i * 12), b = a + forward * 12;
                Vector3 al = a - right * 12, ar = a + right * 12, bl = b - right * 12, br = b + right * 12;
                road.Quad(Ground(mission.Terrain, al.x, al.z) + Vector3.up * .2f,
                    Ground(mission.Terrain, bl.x, bl.z) + Vector3.up * .2f,
                    Ground(mission.Terrain, br.x, br.z) + Vector3.up * .2f,
                    Ground(mission.Terrain, ar.x, ar.z) + Vector3.up * .2f, new Color(.46f, .43f, .31f));
            }
            MeshObject("Supply road / terrain following", road, generatedRoot);
        }

        Transform BuildPropeller(Transform parent,Vector3 position)
        {
            var g=new Geometry();
            for(int blade=0;blade<3;blade++)
            {
                float angle=blade*Mathf.PI*2/3;
                Vector3 end=new Vector3(Mathf.Cos(angle)*1.65f,Mathf.Sin(angle)*1.65f,0);
                g.Edge(Vector3.zero,end,.115f,Dark);
                g.Edge(end*.88f,end,.125f,Amber);
            }
            g.Octahedron(Vector3.zero,new Vector3(.22f,.22f,.3f),Cream);
            var prop=MeshObject("Three blade propeller",g,parent); prop.localPosition=position;
            return prop;
        }

        void BuildCockpit()
        {
            cockpit=NewChild("Cockpit / physical instruments",viewCamera.transform);
            var g=new Geometry();
            // A low coaming gives a broad unobstructed forward view. Instruments are real geometry.
            g.Box(new Vector3(0,-.59f,1.13f),new Vector3(2.15f,.38f,.18f),Dark);
            g.Box(new Vector3(0,-.40f,1.11f),new Vector3(2.17f,.045f,.20f),new Color(.19f,.20f,.17f));
            g.Edge(new Vector3(-.98f,-.4f,.84f),new Vector3(-1.20f,.52f,1.3f),.032f,Dark);
            g.Edge(new Vector3(.98f,-.4f,.84f),new Vector3(1.20f,.52f,1.3f),.032f,Dark);
            g.Edge(new Vector3(-.98f,-.4f,.84f),new Vector3(-1.20f,.52f,1.3f),.009f,Cream*.65f);
            g.Edge(new Vector3(.98f,-.4f,.84f),new Vector3(1.20f,.52f,1.3f),.009f,Cream*.65f);
            MeshObject("Instrument panel and canopy frame",g,cockpit,instrumentMaterial);
            speedNeedle=BuildDial(new Vector3(-.36f,-.45f,1.01f),.065f);
            altitudeNeedle=BuildDial(new Vector3(-.12f,-.45f,1.01f),.065f);
            climbNeedle=BuildDial(new Vector3(.12f,-.45f,1.01f),.065f);
            compass=BuildDial(new Vector3(.36f,-.45f,1.01f),.065f);
        }

        Transform BuildDial(Vector3 position,float radius)
        {
            var g=new Geometry();
            g.Disc(position,radius+ .008f,Vector3.back,new Color(.37f,.38f,.32f),24);
            g.Disc(position+Vector3.back*.002f,radius,Vector3.back,new Color(.032f,.043f,.042f),24);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                Vector3 d=new Vector3(Mathf.Sin(a),Mathf.Cos(a),0);
                g.Edge(position+Vector3.back*.004f+d*radius*.73f,position+Vector3.back*.004f+d*radius*.9f,.003f,Cream);
            }
            MeshObject("Analogue dial",g,cockpit,instrumentMaterial);
            var needle=new Geometry();
            needle.Edge(Vector3.zero,Vector3.up*radius*.72f,.004f,Amber);
            needle.Disc(Vector3.zero,.009f,Vector3.back,Cream,8);
            var t=MeshObject("Instrument needle",needle,cockpit,instrumentMaterial);
            t.localPosition=position+Vector3.back*.008f;
            return t;
        }

        void BuildEffectPools()
        {
            var tracer=new Geometry();
            tracer.Edge(Vector3.zero,Vector3.forward,1,new Color(1,.73f,.28f));
            var tracerMesh=BuildMesh(tracer,"Shared tracer geometry");
            for(int i=0;i<320;i++) tracerPool.Add(MeshObject("Ballistic tracer",tracerMesh,generatedRoot,instrumentMaterial));
            var flash=new Geometry(); flash.Octahedron(Vector3.zero,Vector3.one,new Color(1,.67f,.23f));
            var flashMesh=BuildMesh(flash,"Shared impact geometry");
            for(int i=0;i<80;i++) effectPool.Add(MeshObject("Impact / debris",flashMesh,generatedRoot,material));
            var smoke=new Geometry(); smoke.Octahedron(Vector3.zero,Vector3.one,new Color(.21f,.23f,.23f));
            var smokeMesh=BuildMesh(smoke,"Shared smoke geometry");
            for(int i=0;i<160;i++) smokePool.Add(MeshObject("Damage smoke",smokeMesh,generatedRoot,material));
            foreach(var t in tracerPool) t.gameObject.SetActive(false);
            foreach(var t in effectPool) t.gameObject.SetActive(false);
            foreach(var t in smokePool) t.gameObject.SetActive(false);
        }

        void SyncEffects(AirMission mission)
        {
            int cursor=0;
            foreach(var projectile in mission.Projectiles)
            {
                if(cursor>=tracerPool.Count) break;
                var t=tracerPool[cursor++]; t.gameObject.SetActive(true);
                Vector3 end=World(projectile.Position), velocity=World(projectile.Velocity);
                float length=Mathf.Clamp(velocity.magnitude*.027f,8,32);
                t.SetPositionAndRotation(end-velocity.normalized*length,Quaternion.LookRotation(velocity.normalized));
                float thickness=Mathf.Clamp(Vector3.Distance(end,viewCamera.transform.position)*.00045f,.12f,.72f);
                t.localScale=new Vector3(thickness,thickness,length);
            }
            for(int i=cursor;i<tracerPool.Count;i++) tracerPool[i].gameObject.SetActive(false);
            cursor=0;
            foreach(var effect in mission.Events)
            {
                bool destroyed=effect.Kind==CombatEventKind.Destroyed;
                int pieces=destroyed?8:2;
                for(int piece=0;piece<pieces&&cursor<effectPool.Count;piece++)
                {
                    var t=effectPool[cursor++];
                    float age=effect.Age;
                    bool visible=destroyed?age<3.8f:age<.32f;
                    t.gameObject.SetActive(visible);
                    if(!visible) continue;
                    Vector3 drift=new Vector3(Mathf.Sin(piece*9.1f)*22,15+piece*3,Mathf.Cos(piece*7.3f)*22);
                    t.position=World(effect.Position)+drift*age+Vector3.down*age*age*5;
                    t.rotation=Quaternion.Euler(age*70+piece*60,age*110,age*91);
                    float size=destroyed?(1.2f+Mathf.Min(age,.4f)*8)*Mathf.Clamp01(1-age/4):Mathf.Max(.2f,1.2f-age*3);
                    t.localScale=new Vector3(size,size*.7f,size*.65f);
                }
            }
            foreach (var target in mission.GroundTargets)
            {
                if (target.Alive) continue;
                for (int flame = 0; flame < 2 && cursor < effectPool.Count; flame++)
                {
                    var t = effectPool[cursor++]; t.gameObject.SetActive(true);
                    float pulse = 1.0f + .3f * Mathf.Sin(mission.Time * 8 + target.Id * 7 + flame * 3);
                    t.position = World(target.Position) + new Vector3(flame * 1.2f - .6f, -.4f, flame * 2 - 1);
                    t.localScale = new Vector3(.8f, 2.2f, .9f) * pulse;
                    t.rotation = Quaternion.Euler(0, mission.Time * 23 + flame * 70, 0);
                }
            }
            for(int i=cursor;i<effectPool.Count;i++) effectPool[i].gameObject.SetActive(false);
            cursor=0;
            foreach(var plane in mission.Aircraft)
            {
                if(!plane.Alive||plane.Health>65) continue;
                int count=plane.Health<30?17:9;
                for(int puff=0;puff<count&&cursor<smokePool.Count;puff++)
                {
                    var t=smokePool[cursor++]; t.gameObject.SetActive(true);
                    float age=((float)mission.Time*.7f+puff/(float)count)%1.0f;
                    t.position=World(plane.Position)-World(plane.Velocity)*age*1.2f+Vector3.up*age*5;
                    float size=1+age*6; t.localScale=new Vector3(size,size*.85f,size);
                    t.rotation=Quaternion.Euler(puff*60,puff*27,puff*43);
                }
            }
            foreach (var target in mission.GroundTargets)
            {
                if (target.Alive && target.Health > target.MaxHealth * .55f) continue;
                int count = target.Alive ? 5 : 12;
                for (int puff = 0; puff < count && cursor < smokePool.Count; puff++)
                {
                    var t = smokePool[cursor++]; t.gameObject.SetActive(true);
                    float age = (mission.Time * .19f + puff / (float)count) % 1f;
                    t.position = World(target.Position) + new Vector3(age * 7, 1.5f + age * 31, age * 3);
                    float size = 1.3f + age * 6;
                    t.localScale = new Vector3(size, size * 1.15f, size);
                    t.rotation = Quaternion.Euler(puff * 37, puff * 71, puff * 13);
                }
            }
            for(int i=cursor;i<smokePool.Count;i++) smokePool[i].gameObject.SetActive(false);
        }

        static Transform NewChild(string name,Transform parent)
        {
            var t=new GameObject(name).transform; t.SetParent(parent,false); return t;
        }
        Transform MeshObject(string name,Geometry geometry,Transform parent,Material chosen=null)
        { return MeshObject(name,BuildMesh(geometry,name),parent,chosen??material); }
        Transform MeshObject(string name,Mesh mesh,Transform parent,Material chosen)
        {
            var t=NewChild(name,parent);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=chosen;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return t;
        }
        Mesh BuildMesh(Geometry g,string name)
        {
            var mesh=new Mesh { name=name, indexFormat=g.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16 };
            mesh.SetVertices(g.Vertices);mesh.SetNormals(g.Normals);mesh.SetColors(g.Colors);mesh.SetTriangles(g.Triangles,0);
            mesh.RecalculateBounds(); meshes.Add(mesh);return mesh;
        }
        void Clear()
        {
            if(generatedRoot!=null) { generatedRoot.gameObject.SetActive(false); Destroy(generatedRoot.gameObject); }
            foreach(var mesh in meshes) if(mesh!=null) Destroy(mesh);
            meshes.Clear();aircraft.Clear();groundTargets.Clear();tracerPool.Clear();effectPool.Clear();smokePool.Clear();
            if(material!=null) Destroy(material);if(skyMaterial!=null) Destroy(skyMaterial);if(instrumentMaterial!=null) Destroy(instrumentMaterial);
            material=null;skyMaterial=null;instrumentMaterial=null;viewCamera=null;cameraInitialized=false;
        }
        void OnDestroy() { Clear(); }

        sealed class Geometry
        {
            public readonly List<Vector3> Vertices=new List<Vector3>();
            public readonly List<Vector3> Normals=new List<Vector3>();
            public readonly List<Color> Colors=new List<Color>();
            public readonly List<int> Triangles=new List<int>();
            public int Count { get { return Vertices.Count; } }
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
            {
                int i=Vertices.Count;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);
                Normals.Add(normal);Normals.Add(normal);Normals.Add(normal);
                Colors.Add(color);Colors.Add(color);Colors.Add(color);
                Triangles.Add(i);Triangles.Add(i+1);Triangles.Add(i+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
            { Triangle(a,b,c,color);Triangle(a,c,d,color); }
            public void Box(Vector3 center,Vector3 size,Color color)
            {
                Vector3 p=size*.5f;
                Vector3 a=center+new Vector3(-p.x,-p.y,-p.z),b=center+new Vector3(p.x,-p.y,-p.z),c=center+new Vector3(p.x,p.y,-p.z),d=center+new Vector3(-p.x,p.y,-p.z);
                Vector3 e=center+new Vector3(-p.x,-p.y,p.z),f=center+new Vector3(p.x,-p.y,p.z),g=center+new Vector3(p.x,p.y,p.z),h=center+new Vector3(-p.x,p.y,p.z);
                Quad(a,d,c,b,color);Quad(e,f,g,h,color);Quad(a,e,h,d,color);Quad(b,c,g,f,color);Quad(d,h,g,c,color);Quad(a,b,f,e,color);
            }
            public void Edge(Vector3 a,Vector3 b,float width,Color color)
            {
                Vector3 axis=(b-a).normalized;
                Vector3 tangent=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized*width*.5f;
                Vector3 bitangent=Vector3.Cross(axis,tangent).normalized*width*.5f;
                Quad(a-tangent-bitangent,b-tangent-bitangent,b+tangent-bitangent,a+tangent-bitangent,color);
                Quad(a+tangent-bitangent,b+tangent-bitangent,b+tangent+bitangent,a+tangent+bitangent,color);
                Quad(a+tangent+bitangent,b+tangent+bitangent,b-tangent+bitangent,a-tangent+bitangent,color);
                Quad(a-tangent+bitangent,b-tangent+bitangent,b-tangent-bitangent,a-tangent-bitangent,color);
            }
            public void Extrude(Vector3[] perimeter,float thickness,Color top,Color bottom)
            {
                Vector3 center=Vector3.zero; foreach(var p in perimeter) center+=p;center/=perimeter.Length;
                Vector3 down=Vector3.down*thickness;
                for(int i=0;i<perimeter.Length;i++)
                {
                    Vector3 a=perimeter[i],b=perimeter[(i+1)%perimeter.Length];
                    // Mirrored left/right planforms must still light their upper surfaces upward.
                    if(Vector3.Cross(a-center,b-center).y<0) { Vector3 swap=a;a=b;b=swap; }
                    Triangle(center,a,b,top);Triangle(center+down,b+down,a+down,bottom);Quad(a,a+down,b+down,b,top*.85f);
                }
            }
            public void Disc(Vector3 center,float radius,Vector3 normal,Color color,int segments)
            {
                Vector3 tangent=Vector3.Cross(normal,Mathf.Abs(normal.y)>.9f?Vector3.right:Vector3.up).normalized;
                Vector3 bitangent=Vector3.Cross(normal,tangent);
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    Triangle(center,center+(tangent*Mathf.Cos(a)+bitangent*Mathf.Sin(a))*radius,center+(tangent*Mathf.Cos(b)+bitangent*Mathf.Sin(b))*radius,color);
                }
            }
            public void Pyramid(Vector3 p,float radius,float height,Color color)
            {
                Vector3 a=p+new Vector3(-radius,0,-radius),b=p+new Vector3(radius,0,-radius),c=p+new Vector3(radius,0,radius),d=p+new Vector3(-radius,0,radius),top=p+Vector3.up*height;
                Triangle(a,top,b,color);Triangle(b,top,c,color);Triangle(c,top,d,color);Triangle(d,top,a,color);
            }
            public void Octahedron(Vector3 p,Vector3 size,Color color)
            {
                Vector3 top=p+Vector3.up*size.y,bottom=p+Vector3.down*size.y;
                Vector3[] equator={p+Vector3.right*size.x,p+Vector3.forward*size.z,p+Vector3.left*size.x,p+Vector3.back*size.z};
                for(int i=0;i<4;i++) {Triangle(top,equator[i],equator[(i+1)%4],color);Triangle(bottom,equator[(i+1)%4],equator[i],color);}
            }
        }
    }
}
