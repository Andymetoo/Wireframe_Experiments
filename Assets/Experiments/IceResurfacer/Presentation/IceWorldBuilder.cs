using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using IceResurfacer.Core;

namespace IceResurfacer.Presentation
{
    /// <summary>Presentation-only references; the deterministic simulation owns all motion.</summary>
    public sealed class IceWorld
    {
        public Transform Machine, Conditioner, CameraMount, SteeringWheel, TankPivot;
        public Transform HorizontalAuger, VerticalAuger, Brush, BrushArm, BrushLink, BrushRoller, BladeWheel;
        public Transform WashValve, FloodLever, ConditionerLever, WaterOutlet, SnowOutlet, TankSnow;
        public Transform OperatorStation, Seat, SteeringColumn, Dashboard, Pedals, AccessSteps;
        public Transform SnowBreakerPlunger, BodyTank, HorizontalAugerLever, VerticalAugerLever;
        public Transform AcceleratorPedal, BrakePedal, CuttingBlade, DistributionTowel, ConditionerHousing;
        public Transform TankInspection, TankDischarge, DumpOutlet, TankDoor, PitSnow, GuideRoller, ConditionerLeftEdge;
        public Transform DumpLever, WashValveMount, FloodValveMount;
        public Transform[] TankCylinderBarrels, TankCylinderRods, ConditionerCylinderBarrels, ConditionerCylinderRods;
        public float WorkingWidth;
        // Geometry witnesses survive batching. Built-in cube meshes are referenced,
        // never added to the owned Meshes list or destroyed by this experiment.
        public Mesh CuttingBladeMesh, DistributionTowelMesh, ConditionerHousingMesh;
        public Transform[] FrontWheels, RearWheels;
        public MeshRenderer IceMesh;
        public Light[] ArenaLights;
        public readonly List<Material> Materials = new List<Material>();
        public readonly List<Mesh> Meshes = new List<Mesh>();
    }

    /// <summary>Original mesh-built Northline arena and fictional Aurora R-54 machine.</summary>
    public static class IceWorldBuilder
    {
        public static IceWorld Build(Transform root, Material litTemplate, SimulatorConfig config = null)
        {
            var builder = new Builder(root, litTemplate, config ?? new SimulatorConfig());
            return builder.Build();
        }
        // Explicit Editor preview uses the exact same authoring functions but
        // preserves each selectable part. Runtime keeps material mesh batching.
        public static IceWorld BuildMachinePreview(Transform root,Material litTemplate,SimulatorConfig config)
        {
            config.Validate();
            return new Builder(root,litTemplate,config,false).Build(false);
        }

        sealed class Builder
        {
            readonly Transform root;
            readonly Material template;
            readonly SimulatorConfig config;
            readonly bool batchGeometry;
            readonly ServiceLayout serviceLayout;
            readonly IceWorld world = new IceWorld();
            Material white, navy, cyan, steel, rubber, black, orange, red, yellow, concrete;
            Material roof, glass, lightMat, pale, snow, cloth, blue, lettering;
            Font font;

            public Builder(Transform root, Material template, SimulatorConfig config,bool batchGeometry=true)
            {
                this.root = root;
                this.template = template;
                this.config = config;
                this.batchGeometry=batchGeometry;
                serviceLayout = new ServiceLayout(config);
                world.WorkingWidth = config.ConditionerWidth;
            }

            public IceWorld Build(bool includeArena=true)
            {
                Palette();
                if(includeArena)Arena();
                Machine();
                if(batchGeometry)BatchMachine();
                return world;
            }

            void Palette()
            {
                white = Mat("Porcelain enamel", new Color(.83f,.86f,.84f), .15f, .48f);
                navy = Mat("Aurora midnight blue", new Color(.025f,.105f,.15f), .35f, .58f);
                cyan = Mat("Aurora glacier enamel", new Color(.045f,.43f,.49f), .32f, .63f);
                steel = Mat("Brushed stainless steel", new Color(.47f,.53f,.55f), .8f, .55f);
                rubber = Mat("Tire and hydraulic rubber", new Color(.026f,.031f,.033f), .02f, .21f);
                black = Mat("Instrument graphite", new Color(.035f,.047f,.055f), .18f, .4f);
                orange = Mat("Amber safety enamel", new Color(.96f,.37f,.045f), .16f, .48f);
                red = Mat("Signal vermilion", new Color(.6f,.045f,.035f), .1f, .48f);
                yellow = Mat("Dasher kickplate", new Color(.93f,.68f,.12f), .04f, .35f);
                concrete = Mat("Service concrete", new Color(.27f,.31f,.32f), 0, .15f);
                roof = Mat("Roof charcoal", new Color(.065f,.093f,.115f), .15f, .26f);
                pale = Mat("Arena acoustic panels", new Color(.52f,.59f,.61f), .1f, .23f);
                blue = Mat("Grandstand blue", new Color(.045f,.16f,.22f), .05f, .35f);
                snow = Mat("Collected snow", new Color(.91f,.94f,.95f), 0, .18f);
                cloth = Mat("Flood distribution towel", new Color(.46f,.52f,.47f), 0, .12f);
                lightMat = Mat("Fixture luminous diffuser", new Color(.82f,.92f,1), 0, .5f);
                lightMat.EnableKeyword("_EMISSION");
                lightMat.SetColor("_EmissionColor", new Color(1.7f,1.95f,2.2f));
                glass = Mat("Arena safety glass", new Color(.53f,.78f,.83f,.085f), .05f, .9f);
                glass.SetFloat("_Surface", 1);
                glass.SetFloat("_Blend", 0);
                glass.SetFloat("_ZWrite", 0);
                glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                glass.SetFloat("_Cull", (float)CullMode.Off);
                glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                glass.SetOverrideTag("RenderType", "Transparent");
                glass.renderQueue = 3000;
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                font.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /-",64,FontStyle.Normal);
                lettering=new Material(Resources.Load<Shader>("WorldText")) { name="IceResurfacer / depth tested arena lettering" };
                lettering.mainTexture=font.material.mainTexture;
                world.Materials.Add(lettering);
                root.gameObject.AddComponent<IceWorldTextAtlas>().Initialize(font,lettering);
            }

            Material Mat(string name, Color color, float metallic, float smooth)
            {
                Material m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = "IceResurfacer / " + name;
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smooth);
                world.Materials.Add(m);
                return m;
            }

            Transform Group(string name, Transform parent, Vector3 position = default)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.localPosition = position;
                return t;
            }

            Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
            {
                var obj = GameObject.CreatePrimitive(type);
                obj.name = name;
                obj.transform.SetParent(parent, false);
                obj.transform.localPosition = position;
                obj.transform.localScale = scale;
                obj.GetComponent<MeshRenderer>().sharedMaterial = material;
                var collider = obj.GetComponent<Collider>();
                if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
                else UnityEngine.Object.DestroyImmediate(collider);
                return obj.transform;
            }

            Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
                => Primitive(name, PrimitiveType.Cube, parent, position, size, material);

            Transform Sphere(string name, Transform parent, Vector3 position, float size, Material material)
                => Primitive(name, PrimitiveType.Sphere, parent, position, Vector3.one * size, material);

            Transform Cylinder(string name, Transform parent, Vector3 position, float diameter, float length, Material material, Vector3 axis)
            {
                var t = Primitive(name, PrimitiveType.Cylinder, parent, position, new Vector3(diameter, length * .5f, diameter), material);
                t.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
                return t;
            }

            Transform Beam(string name, Transform parent, Vector3 a, Vector3 b, float diameter, Material material)
                => Cylinder(name, parent, (a+b)*.5f, diameter, (b-a).magnitude, material, b-a);

            MeshRenderer MeshObject(string name, Transform parent, List<Vector3> vertices, List<int> triangles, Material material, List<Vector2> uv = null)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
                var mesh = new Mesh { name = "IceResurfacer / " + name };
                if(vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                if(uv != null) mesh.SetUVs(0, uv);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                world.Meshes.Add(mesh);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                return renderer;
            }

            void Quad(List<Vector3> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int n = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                indices.Add(n);indices.Add(n+1);indices.Add(n+2);
                indices.Add(n);indices.Add(n+2);indices.Add(n+3);
            }

            void Label(string text, Transform parent, Vector3 position, float size, Color color, Quaternion rotation, TextAnchor anchor = TextAnchor.MiddleCenter)
            {
                var t = Group(text, parent, position);
                // TextMesh faces -Z; the builder's sign convention describes its visible +Z face.
                t.localRotation = rotation * Quaternion.Euler(0,180,0);
                var tm = t.gameObject.AddComponent<TextMesh>();
                tm.text = text;
                tm.font = font;
                tm.fontSize = 64;
                tm.characterSize = size * 10f / 64f;
                tm.color = color;
                tm.anchor = anchor;
                tm.alignment = TextAlignment.Center;
                var renderer = t.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = lettering;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            List<Vector3> RinkBoundary()
            {
                var points = new List<Vector3>();
                Vector2[] centres = { new Vector2(4.5f,21.5f), new Vector2(4.5f,-21.5f), new Vector2(-4.5f,-21.5f), new Vector2(-4.5f,21.5f) };
                for(int corner=0;corner<4;corner++)
                {
                    var c = centres[corner];
                    for(int i=0;i<=24;i++)
                    {
                        float a=(corner*90+i*90f/24)*Mathf.Deg2Rad;
                        points.Add(new Vector3(c.x+Mathf.Sin(a)*8.5f,0,c.y+Mathf.Cos(a)*8.5f));
                    }
                    var end=points[points.Count-1];
                    var next=centres[(corner+1)%4];
                    float nextA=(corner+1)*90*Mathf.Deg2Rad;
                    var start=new Vector3(next.x+Mathf.Sin(nextA)*8.5f,0,next.y+Mathf.Cos(nextA)*8.5f);
                    int segments=Mathf.CeilToInt(Vector3.Distance(end,start)/.7f);
                    for(int j=1;j<segments;j++)points.Add(Vector3.Lerp(end,start,j/(float)segments));
                }
                return points;
            }

            void RingBand(string name, Transform parent, List<Vector3> boundary, float y0, float y1, float thickness, Material material)
            {
                var v=new List<Vector3>();var tri=new List<int>();
                for(int i=0;i<boundary.Count;i++)
                {
                    Vector3 a=boundary[i], b=boundary[(i+1)%boundary.Count];
                    if(serviceLayout.IsPortal((a.x+b.x)*.5f,(a.z+b.z)*.5f))continue;
                    Vector3 outward = new Vector3(-(b-a).z,0,(b-a).x).normalized*thickness;
                    Vector3 al=a+Vector3.up*y0, bl=b+Vector3.up*y0, ah=a+Vector3.up*y1, bh=b+Vector3.up*y1;
                    Quad(v,tri,al,ah,bh,bl);
                    Quad(v,tri,bl+outward,bh+outward,ah+outward,al+outward);
                    Quad(v,tri,ah,bh,bh+outward,ah+outward);
                }
                var renderer=MeshObject(name,parent,v,tri,material);
                if(material==glass)renderer.shadowCastingMode=ShadowCastingMode.Off;
            }

            void Arena()
            {
                var arena=Group("NORTHLINE ICE CENTRE / enclosed arena",root);
                Box("Foundation",arena,new Vector3(2,-.24f,-3),new Vector3(53,.45f,89),concrete);
                Box("Roof canopy",arena,new Vector3(2,12.7f,-2),new Vector3(52,.28f,88),roof);
                Box("East insulated wall",arena,new Vector3(27,6,-2),new Vector3(.4f,13,88),pale);
                Box("West insulated wall",arena,new Vector3(-23,6,-2),new Vector3(.4f,13,88),pale);
                Box("North insulated wall",arena,new Vector3(2,6,41),new Vector3(50,13,.4f),pale);
                Box("Service exterior wall",arena,new Vector3(2,6,-45),new Vector3(50,13,.4f),pale);
                var edge=RinkBoundary();
                // Meet the kickplate at one edge: the old .13-.17 overlap
                // emitted two coplanar inner faces and visibly flickered.
                RingBand("White impact dasher",arena,edge,.17f,1.13f,.17f,white);
                RingBand("Yellow kick strip",arena,edge,.015f,.17f,.19f,yellow);
                RingBand("Dark top rail",arena,edge,1.13f,1.22f,.21f,navy);
                RingBand("Continuous safety glass",arena,edge,1.22f,2.55f,.012f,glass);
                for(int i=0;i<edge.Count;i+=5)
                {
                    var p=edge[i];
                    if(serviceLayout.IsPortal(p.x,p.z))continue;
                    Cylinder("Glass support",arena,p+Vector3.up*1.87f,.028f,1.34f,steel,Vector3.up);
                }
                var vertices=new List<Vector3>{new Vector3(0,.005f,0)};
                var uvs=new List<Vector2>{new Vector2(.5f,.5f)};
                var triangles=new List<int>();
                foreach(var p in edge){ vertices.Add(p+Vector3.up*.005f);uvs.Add(new Vector2(p.x/26+.5f,p.z/60+.5f)); }
                for(int i=0;i<edge.Count;i++){triangles.Add(0);triangles.Add(i+1);triangles.Add((i+1)%edge.Count+1);}
                world.IceMesh=MeshObject("Continuous spatial ice sheet",arena,vertices,triangles,white,uvs);
                Seating(arena);
                RoofStructure(arena);
                ServiceBay(arena);
                Banners(arena);
                Scoreboard(arena);
                BatchMeshes(arena, new HashSet<Transform>{world.IceMesh.transform});
            }

            // Consolidate generated solid geometry by material while preserving movable parts,
            // dynamic ice, and TextMesh glyphs. This is mesh batching, not thousands of ice objects.
            void BatchMeshes(Transform parent, HashSet<Transform> excluded = null)
            {
                if(!batchGeometry)return;
                var batches = new Dictionary<Material,List<CombineInstance>>();
                var sources = new List<MeshRenderer>();
                Matrix4x4 toLocal = parent.worldToLocalMatrix;
                foreach(var renderer in parent.GetComponentsInChildren<MeshRenderer>())
                {
                    if(renderer.GetComponent<TextMesh>() != null || !renderer.enabled) continue;
                    bool skip = false;
                    if(excluded != null)
                        foreach(var t in excluded)
                            if(renderer.transform == t || renderer.transform.IsChildOf(t)) { skip=true; break; }
                    if(skip) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if(filter == null || filter.sharedMesh == null || renderer.sharedMaterial == null)continue;
                    Material material = renderer.sharedMaterial;
                    if(!batches.TryGetValue(material,out var list))
                    { list=new List<CombineInstance>(); batches.Add(material,list); }
                    list.Add(new CombineInstance { mesh=filter.sharedMesh, transform=toLocal*renderer.transform.localToWorldMatrix });
                    sources.Add(renderer);
                }
                foreach(var entry in batches)
                {
                    var mesh = new Mesh { name="IceResurfacer / Combined " + entry.Key.name, indexFormat=IndexFormat.UInt32 };
                    mesh.CombineMeshes(entry.Value.ToArray(),true,true);
                    world.Meshes.Add(mesh);
                    var node=Group("Batched " + entry.Key.name,parent);
                    node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=node.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=entry.Key;
                    if(entry.Key==glass || entry.Key==lightMat)renderer.shadowCastingMode=ShadowCastingMode.Off;
                }
                foreach(var renderer in sources)
                {
                    renderer.enabled=false;
                    var filter=renderer.GetComponent<MeshFilter>();
                    if(Application.isPlaying) { UnityEngine.Object.Destroy(renderer); UnityEngine.Object.Destroy(filter); }
                    else { UnityEngine.Object.DestroyImmediate(renderer); UnityEngine.Object.DestroyImmediate(filter); }
                }
            }

            void BatchMachine()
            {
                var moving=new HashSet<Transform>
                {
                    world.TankPivot,world.SteeringWheel,world.BladeWheel,world.WashValve,
                    world.FloodLever,world.ConditionerLever,world.VerticalAuger,world.BrushArm,
                    world.HorizontalAugerLever,world.VerticalAugerLever,world.SnowBreakerPlunger,
                    world.AcceleratorPedal,world.BrakePedal,world.GuideRoller,world.DumpLever
                };
                foreach(var cylinder in world.TankCylinderBarrels)moving.Add(cylinder);
                foreach(var cylinder in world.TankCylinderRods)moving.Add(cylinder);
                foreach(var cylinder in world.ConditionerCylinderBarrels)moving.Add(cylinder);
                foreach(var cylinder in world.ConditionerCylinderRods)moving.Add(cylinder);
                foreach(var wheel in world.FrontWheels)moving.Add(wheel);
                foreach(var wheel in world.RearWheels)moving.Add(wheel);
                BatchMeshes(world.Machine,moving);
                foreach(var wheel in world.FrontWheels)BatchMeshes(wheel);
                foreach(var wheel in world.RearWheels)BatchMeshes(wheel);
                BatchMeshes(world.TankPivot,new HashSet<Transform>{world.TankSnow,world.TankDoor});
                BatchMeshes(world.TankDoor);
                BatchMeshes(world.GuideRoller);
                BatchMeshes(world.TankSnow);
                BatchMeshes(world.SteeringWheel);
                BatchMeshes(world.BladeWheel);
                BatchMeshes(world.WashValve);
                BatchMeshes(world.FloodLever);
                BatchMeshes(world.ConditionerLever);
                BatchMeshes(world.HorizontalAugerLever);
                BatchMeshes(world.VerticalAugerLever);
                BatchMeshes(world.DumpLever);
                BatchMeshes(world.SnowBreakerPlunger);
                BatchMeshes(world.AcceleratorPedal);
                BatchMeshes(world.BrakePedal);
                BatchMeshes(world.VerticalAuger);
                BatchMeshes(world.BrushArm,new HashSet<Transform>{world.Brush,world.BrushLink,world.BrushRoller});
                BatchMeshes(world.Brush);
                BatchMeshes(world.Conditioner,new HashSet<Transform>{world.HorizontalAuger,world.CuttingBlade});
                BatchMeshes(world.HorizontalAuger);
            }

            void Seating(Transform parent)
            {
                var stands=Group("Tiered spectator seating",parent);
                for(int side=-1;side<=1;side+=2)
                {
                    for(int row=0;row<6;row++)
                    {
                        float x=side*(15.25f+row*.92f), y=.45f+row*.52f;
                        // Southeast machine-room access occupies the former end
                        // of the east stand. Its risers stop before the portal.
                        Box("Concrete riser",stands,new Vector3(x,y*.5f,side>0?3:0),new Vector3(.93f,y,side>0?50:56),concrete);
                        for(int block=0;block<4;block++)
                        {
                            float z=-21+block*14;
                            if(side>0&&block==0)z=-18;
                            float length=side>0&&block==0?5.8f:11.8f;
                            Box("Moulded seat bench",stands,new Vector3(x,y+.25f,z),new Vector3(.65f,.16f,length),blue);
                            Box("Continuous seat backs",stands,new Vector3(x+side*.28f,y+.57f,z),new Vector3(.13f,.62f,length),blue);
                            for(int seat=0;seat<(side>0&&block==0?8:16);seat++)
                                Box("Seat partition",stands,new Vector3(x,y+.3f,z-length*.5f+.2f+seat*.75f),new Vector3(.63f,.10f,.026f),navy);
                        }
                    }
                    for(int aisle=0;aisle<5;aisle++)
                    {
                        float z=-28+aisle*14;
                        if(side>0&&aisle==0)z=-22;
                        Beam("Aisle rail",stands,new Vector3(side*14.9f,1.1f,z),new Vector3(side*20.9f,4.55f,z),.055f,steel);
                    }
                    Box("Upper walkway",stands,new Vector3(side*21.3f,3.6f,side>0?4.5f:0),new Vector3(1.1f,.24f,side>0?53:62),concrete);
                    Beam("Gallery handrail",stands,new Vector3(side*21.7f,4.6f,side>0?-22:-31),new Vector3(side*21.7f,4.6f,31),.065f,steel);
                }
                for(int row=0;row<5;row++)
                {
                    float y=.4f+row*.55f,z=32+row*.95f;
                    Box("North stand terrace",stands,new Vector3(0,y*.5f,z),new Vector3(29,y,.95f),concrete);
                    Box("North seat bench",stands,new Vector3(0,y+.22f,z),new Vector3(28,.14f,.65f),blue);
                    Box("North seat backs",stands,new Vector3(0,y+.55f,z+.27f),new Vector3(28,.6f,.13f),blue);
                }
            }

            void RoofStructure(Transform parent)
            {
                var structure=Group("Steel trusses and arena lighting",parent);
                var lights=new List<Light>();
                for(int bay=0;bay<9;bay++)
                {
                    float z=-34+bay*8;
                    Beam("Truss lower chord",structure,new Vector3(-22,10.4f,z),new Vector3(22,10.4f,z),.17f,roof);
                    Beam("Truss upper chord",structure,new Vector3(-22,12.25f,z),new Vector3(22,12.25f,z),.17f,roof);
                    for(int i=0;i<11;i++)
                    {
                        float x=-22+i*4;
                        Beam("Triangulated roof web",structure,new Vector3(x,10.4f,z),new Vector3(x+2,12.25f,z),.085f,roof);
                        Beam("Triangulated roof web",structure,new Vector3(x+2,12.25f,z),new Vector3(x+4,10.4f,z),.085f,roof);
                    }
                    for(int side=-1;side<=1;side+=2)
                    {
                        float x=side*8;
                        Beam("Light suspension",structure,new Vector3(x,10.4f,z),new Vector3(x,9.4f,z),.025f,steel);
                        Box("Arena fixture housing",structure,new Vector3(x,9.35f,z),new Vector3(2.7f,.17f,.65f),black);
                        Box("Arena fixture diffuser",structure,new Vector3(x,9.25f,z),new Vector3(2.5f,.035f,.51f),lightMat);
                        if(bay%2==1)
                        {
                            var t=Group("Arena pool light",structure,new Vector3(x,9.1f,z));
                            var light=t.gameObject.AddComponent<Light>();
                            light.type=LightType.Point;light.color=new Color(.8f,.89f,1);light.intensity=24;light.range=19;
                            light.shadows=LightShadows.None;lights.Add(light);
                        }
                    }
                }
                Beam("Ventilation duct",structure,new Vector3(-18,9.7f,-39),new Vector3(-18,9.7f,36),.75f,steel);
                Beam("Ventilation duct",structure,new Vector3(18,9.7f,-39),new Vector3(18,9.7f,36),.75f,steel);
                for(int i=0;i<6;i++)
                {
                    float z=-24+i*10;
                    Cylinder("Air outlet",structure,new Vector3(-18,9.3f,z),.49f,.15f,black,Vector3.up);
                    Cylinder("Air outlet",structure,new Vector3(18,9.3f,z),.49f,.15f,black,Vector3.up);
                }
                world.ArenaLights=lights.ToArray();
            }

            void ServiceBay(Transform parent)
            {
                var b=serviceLayout;
                var bay=Group("Southeast diagonal portal / backstage maintenance room",parent,new Vector3(b.GateX,0,b.GateZ));
                bay.localRotation=Quaternion.Euler(0,-45,0);
                Box("Threshold steel sill",bay,new Vector3(0,.018f,0),new Vector3(b.GateHalfWidth*2,.035f,.23f),steel);
                Box("Machine room concrete apron",bay,new Vector3(0,-.015f,-(b.RoomStart+b.RoomLength)*.5f),new Vector3(b.RoomHalfWidth*2,.025f,b.RoomLength-b.RoomStart),concrete);
                Box("Backstage ceiling",bay,new Vector3(0,4.8f,-(b.RoomStart+b.RoomLength)*.5f),new Vector3(b.RoomHalfWidth*2+.2f,.16f,b.RoomLength-b.RoomStart+.2f),pale);
                Box("Portal overhead lintel",bay,new Vector3(0,4.3f,-.8f),new Vector3(b.GateHalfWidth*2+.8f,.65f,1.5f),pale);
                Box("Rolled maintenance door",bay,new Vector3(0,3.98f,-.85f),new Vector3(b.GateHalfWidth*2,.22f,.40f),steel);
                for(int side=-1;side<=1;side+=2)
                {
                    Box("Gate opened along portal jamb",bay,new Vector3(side*(b.GateHalfWidth+.12f),.61f,-.75f),new Vector3(.18f,1.2f,1.5f),white);
                    Box("Open gate glass",bay,new Vector3(side*(b.GateHalfWidth+.12f),1.88f,-.75f),new Vector3(.025f,1.3f,1.5f),glass);
                    Box("Machine room side wall",bay,new Vector3(side*(b.RoomHalfWidth+.12f),2.4f,-(b.RoomStart+b.RoomLength)*.5f),new Vector3(.24f,4.8f,b.RoomLength-b.RoomStart+.24f),pale);
                    Box("Portal front return wall",bay,new Vector3(side*(b.RoomHalfWidth+b.GateHalfWidth)*.5f,2.4f,-b.RoomStart),new Vector3(b.RoomHalfWidth-b.GateHalfWidth,4.8f,.24f),pale);
                    Box("Portal upright",bay,new Vector3(side*(b.GateHalfWidth+.15f),2.1f,-.75f),new Vector3(.18f,4.2f,1.75f),steel);
                    Cylinder("Portal protection bollard",bay,new Vector3(side*(b.GateHalfWidth+.27f),.55f,.12f),.16f,1.1f,yellow,Vector3.up);
                    Box("Service parking reference",bay,new Vector3(b.ParkSide+side*1.55f,.015f,-b.ParkOutward),new Vector3(.055f,.008f,4.6f),yellow);
                    for(int i=0;i<7;i++)
                    {
                        var stripe=Box("Pit caution stripe",bay,new Vector3(b.PitSide+side*(b.PitHalfWidth+.14f),.02f,-b.PitOutward-.65f+i*.22f),new Vector3(.28f,.012f,.10f),orange);
                        stripe.localRotation=Quaternion.Euler(0,35,0);
                    }
                }
                Box("Machine room rear wall",bay,new Vector3(0,2.4f,-b.RoomLength-.12f),new Vector3(b.RoomHalfWidth*2+.3f,4.8f,.24f),pale);
                Box("Dark slots under traversable dump grate",bay,new Vector3(b.PitSide,.0005f,-b.PitOutward),new Vector3(b.PitHalfWidth*2,.001f,b.PitHalfLength*2),black);
                world.PitSnow=SnowSurface("Accumulating dumped snow / physical pit",bay,new Vector3(b.PitSide,.04f,-b.PitOutward));
                // Load-bearing grate is flush with the navigable service floor.
                // Its painted edge is a visible seam, not a vehicle barrier.
                float rim=ServiceLayout.PitRimWidth,rimHeight=ServiceLayout.PitRimHeight;
                for(int side=-1;side<=1;side+=2)
                {
                    Box("Visible pit side rim",bay,new Vector3(b.PitSide+side*(b.PitHalfWidth-rim*.5f),rimHeight*.5f,-b.PitOutward),new Vector3(rim,rimHeight,b.PitHalfLength*2),yellow);
                    Box(side<0?"Visible pit approach rim":"Visible pit far rim",bay,new Vector3(b.PitSide,rimHeight*.5f,-b.PitOutward-side*(b.PitHalfLength-rim*.5f)),new Vector3(b.PitHalfWidth*2-rim*2,rimHeight,rim),yellow);
                }
                for(int i=0;i<48;i++)
                    Box("Load-bearing dump grate bar",bay,new Vector3(b.PitSide-b.PitHalfWidth+.09f+i*(b.PitHalfWidth*2-.18f)/47,.0025f,-b.PitOutward),new Vector3(.045f,.005f,b.PitHalfLength*2-.10f),steel);
                for(int i=0;i<7;i++)
                    Box("Dump grate cross support",bay,new Vector3(b.PitSide,.0025f,-b.PitOutward-b.PitHalfLength+.08f+i*(b.PitHalfLength*2-.16f)/6),new Vector3(b.PitHalfWidth*2-.10f,.005f,.045f),steel);
                Label("TRAVERSABLE DUMP GRATE",bay,new Vector3(b.PitSide,2.45f,-b.RoomLength+.02f),.22f,Color.white,Quaternion.identity);
                Label("MANUAL APPROACH / WATCH THE OUTLET",bay,new Vector3(b.PitSide,1.96f,-b.RoomLength+.02f),.080f,new Color(.94f,.7f,.28f),Quaternion.identity);
                Label("SOUTHEAST  /  MACHINE ROOM",bay,new Vector3(0,4.22f,-1.57f),.14f,Color.white,Quaternion.Euler(0,180,0));
                Label("PARK / FILL",bay,new Vector3(b.ParkSide,.023f,-b.ParkOutward+2.5f),.19f,new Color(.93f,.72f,.28f),Quaternion.Euler(270,0,0));
                Label("NOSE TO PIT / REVERSE TO ICE",bay,new Vector3(b.ParkSide,.024f,-2.6f),.10f,new Color(.93f,.72f,.28f),Quaternion.Euler(270,0,0));
                Box("Recessed workshop equipment cabinet",bay,new Vector3(b.RoomHalfWidth+.14f,1.15f,-5.6f),new Vector3(.32f,2.3f,2.7f),navy);
                Label("MAINTENANCE\nFILTERS / HAND TOOLS",bay,new Vector3(b.RoomHalfWidth-.04f,1.55f,-5.6f),.07f,Color.white,Quaternion.Euler(0,-90,0));
                // Closed cabinets remain outside the walkable clear wall margin.
                for(int j=0;j<3;j++)Box("Cabinet drawer",bay,new Vector3(b.RoomHalfWidth-.04f,.48f+j*.34f,-5.6f),new Vector3(.025f,.27f,2.4f),steel);
                Cylinder("Wash hose reel",bay,new Vector3(-b.RoomHalfWidth+.17f,1.45f,-6),.65f,.22f,cyan,Vector3.right);
                Torus("Coiled wash hose",bay,new Vector3(-b.RoomHalfWidth+.31f,1.45f,-6),.26f,.04f,rubber,Quaternion.Euler(0,0,90));
                for(int i=0;i<3;i++)
                {
                    float outward=3.8f+i*3.8f;
                    Box("Workshop strip light",bay,new Vector3(0,4.63f,-outward),new Vector3(3.1f,.09f,.22f),lightMat);
                    var lamp=Group("Workshop pool light",bay,new Vector3(0,4.25f,-outward)).gameObject.AddComponent<Light>();
                    lamp.type=LightType.Point;lamp.range=8;lamp.intensity=3.2f;lamp.color=new Color(.86f,.94f,1);lamp.shadows=LightShadows.None;
                }
                Goal(parent,new Vector3(-7,0,-34),-15);
                Goal(parent,new Vector3(-11,0,-34),15);
            }

            void Goal(Transform parent, Vector3 position, float heading)
            {
                var goal=Group("Parked regulation goal",parent,position);goal.localRotation=Quaternion.Euler(0,heading,0);
                Beam("Goal left post",goal,new Vector3(-.915f,0,0),new Vector3(-.915f,1.22f,0),.06f,red);
                Beam("Goal right post",goal,new Vector3(.915f,0,0),new Vector3(.915f,1.22f,0),.06f,red);
                Beam("Goal crossbar",goal,new Vector3(-.915f,1.22f,0),new Vector3(.915f,1.22f,0),.06f,red);
                Beam("Goal rear frame",goal,new Vector3(-.8f,.12f,-.9f),new Vector3(.8f,.12f,-.9f),.04f,red);
                for(int i=0;i<=18;i++)
                {
                    float x=-.9f+i*.1f;
                    Beam("Net vertical cord",goal,new Vector3(x,1.19f,-.03f),new Vector3(x*.88f,.12f,-.9f),.007f,white);
                }
                for(int i=0;i<=11;i++)
                {
                    float f=i/11f;
                    Beam("Net transverse cord",goal,new Vector3(-.9f*(1-f*.12f),1.19f-f*1.07f,-.03f-f*.87f),new Vector3(.9f*(1-f*.12f),1.19f-f*1.07f,-.03f-f*.87f),.007f,white);
                }
            }

            void Banners(Transform parent)
            {
                Box("Northline end-wall identity",parent,new Vector3(0,6.9f,40.7f),new Vector3(22,2.35f,.08f),navy);
                Label("N O R T H L I N E",parent,new Vector3(0,7.2f,40.62f),.62f,Color.white,Quaternion.Euler(0,180,0));
                Label("I C E   C E N T R E",parent,new Vector3(0,6.35f,40.6f),.23f,new Color(.4f,.77f,.82f),Quaternion.Euler(0,180,0));
                for(int side=-1;side<=1;side+=2)
                {
                    for(int i=0;i<5;i++)
                    {
                        float z=-22+i*11;
                        Box("Hanging club pennant",parent,new Vector3(side*21.9f,7.4f,z),new Vector3(.045f,3.1f,1.9f),i%2==0?navy:cyan);
                        Label("N\n"+(2018+i),parent,new Vector3(side*21.85f,7.35f,z),.24f,Color.white,Quaternion.Euler(0,-side*90,0));
                    }
                    Box("Aurora board advertisement",parent,new Vector3(side*12.89f,.67f,6),new Vector3(.02f,.65f,5.8f),navy);
                    Label("A U R O R A",parent,new Vector3(side*12.87f,.69f,6),.18f,Color.white,Quaternion.Euler(0,-side*90,0));
                    Box("Northline board advertisement",parent,new Vector3(side*12.89f,.67f,-9),new Vector3(.02f,.65f,5.8f),cyan);
                    Label("NORTHLINE  /  ICE WORKS",parent,new Vector3(side*12.87f,.69f,-9),.11f,Color.white,Quaternion.Euler(0,-side*90,0));
                }
                var sign=Group("Southeast service exit sign",parent,new Vector3(serviceLayout.GateX,0,serviceLayout.GateZ));
                sign.localRotation=Quaternion.Euler(0,-45,0);
                Box("Service exit overhead",sign,new Vector3(0,4.22f,.07f),new Vector3(5.3f,.5f,.12f),navy);
                Label("SERVICE  /  SNOW PIT",sign,new Vector3(0,4.22f,.15f),.16f,new Color(.68f,.89f,.78f),Quaternion.identity);
            }

            void Scoreboard(Transform parent)
            {
                var board=Group("Centre suspended scoreboard",parent,new Vector3(0,7.7f,0));
                Box("Scoreboard body",board,Vector3.zero,new Vector3(4.1f,1.55f,2.6f),navy);
                for(int side=-1;side<=1;side+=2)
                {
                    Box("Scoreboard display",board,new Vector3(0,.1f,side*1.31f),new Vector3(3.8f,1.1f,.025f),black);
                    Label("NORTHLINE",board,new Vector3(0,.47f,side*1.34f),.12f,new Color(.55f,.82f,.87f),Quaternion.Euler(0,side==1?0:180,0));
                    Label("ICE MAINTENANCE",board,new Vector3(0,-.03f,side*1.35f),.17f,new Color(.96f,.7f,.31f),Quaternion.Euler(0,side==1?0:180,0));
                    Label("PLEASE KEEP OFF THE ICE",board,new Vector3(0,-.44f,side*1.35f),.075f,Color.white,Quaternion.Euler(0,side==1?0:180,0));
                }
                Beam("Scoreboard suspension",board,new Vector3(-1.5f,.8f,0),new Vector3(-1.5f,4.4f,0),.045f,steel);
                Beam("Scoreboard suspension",board,new Vector3(1.5f,.8f,0),new Vector3(1.5f,4.4f,0),.045f,steel);
            }

            void Machine()
            {
                var machine=world.Machine=Group("AURORA R-54 / rear axle origin",root);
                // A narrow central chassis and between-axle sills leave the actual
                // swept tire volumes open. The previous full-width blocks ran
                // straight through both axles and concealed front-wheel steering.
                Box("Central steel chassis",machine,new Vector3(0,.47f,.77f),new Vector3(.72f,.23f,3),black);
                FrontWheelWellSill(machine);
                Box("Rear crossmember",machine,new Vector3(0,.79f,-.71f),new Vector3(1.95f,.15f,.24f),steel);
                Box("Right equipment deck",machine,new Vector3(.48f,.83f,-.37f),new Vector3(.99f,.13f,1),steel);
                BevelBox("Shaped engine cover / right of operator",machine,new Vector3(.46f,1.13f,-.02f),new Vector3(.85f,.63f,.8f),.10f,navy);
                for(int i=0;i<8;i++)Box("Engine cooling louvre",machine,new Vector3(.895f,1.02f+i*.052f,-.08f),new Vector3(.025f,.018f,.48f),black);
                Tank(machine);
                Wheels(machine);
                OperatorStation(machine);
                Conditioner();
                Brush(machine);
                VerticalConveyor(machine);
                Cylinder("Filler neck",machine,new Vector3(.56f,1.48f,.13f),.17f,.11f,steel,Vector3.up);
                Cylinder("Filler cap",machine,new Vector3(.56f,1.55f,.13f),.2f,.035f,black,Vector3.up);
                var roller=Group("Front-left board guide / distinct from board brush",machine,new Vector3(config.GuideRollerX,.53f,config.GuideRollerZ));
                Beam("Guide roller bracket",machine,new Vector3(-.82f,.55f,config.GuideRollerZ),new Vector3(config.GuideRollerX,.55f,config.GuideRollerZ),.055f,steel);
                world.GuideRoller=Group("Board guide freely rotating wheel",roller);
                Cylinder("Guide roller rubber tire",world.GuideRoller,Vector3.zero,config.GuideRollerRadius*2,.16f,rubber,Vector3.up);
                Cylinder("Guide roller axle cap",world.GuideRoller,new Vector3(0,.09f,0),.07f,.025f,steel,Vector3.up);
                Box("Guide roller rotation witness",world.GuideRoller,new Vector3(config.GuideRollerRadius*.6f,.083f,0),new Vector3(.045f,.012f,.019f),orange);
                Box("Rear bumper",machine,new Vector3(0,.48f,-.91f),new Vector3(2.04f,.15f,.12f),rubber);
                Box("Front bumper",machine,new Vector3(0,.5f,config.BodyFront-.06f),new Vector3(1.97f,.17f,.12f),rubber);
                for(int side=-1;side<=1;side+=2)
                {
                    Box("Front lamp housing",machine,new Vector3(side*.69f,.87f,config.BodyFront-.065f),new Vector3(.35f,.18f,.08f),black);
                    Box("Front LED lamp",machine,new Vector3(side*.69f,.87f,config.BodyFront-.0125f),new Vector3(.26f,.11f,.025f),lightMat);
                    Box("Rear marker",machine,new Vector3(side*.74f,.84f,-.84f),new Vector3(.18f,.11f,.04f),red);
                    Beam("Between-axle side rub rail",machine,new Vector3(side*.98f,.67f,.48f),new Vector3(side*.98f,.67f,1.43f),.07f,rubber);
                    for(int z=0;z<3;z++)Cylinder("Frame bolt",machine,new Vector3(side*.935f,.63f,.57f+z*.35f),.045f,.016f,steel,Vector3.right);
                }
                Beam("Exhaust outlet / equipment side",machine,new Vector3(.81f,.7f,-.2f),new Vector3(.81f,1.4f,-.32f),.065f,steel);
            }

            Vector3 TankPoint(float x,float y,float z)
                => new Vector3(config.TankGeometryX(x),config.TankGeometryY(y),z);

            void FrontWheelWellSill(Transform machine)
            {
                // The lower casing tapers between the front wheel wells. The
                // former broad box carried its full .925 m half-width right to
                // the front arch and hid the real tire when it steered inward.
                // Keep the rear equipment volume, with physical sloping panels
                // ahead of the brush mount; wheel track and eye height stay put.
                Vector2[] outline={new Vector2(-.825f,.49f),new Vector2(.825f,.49f),
                    new Vector2(.925f,.59f),new Vector2(.925f,.90f),new Vector2(.720f,1.45f),
                    new Vector2(-.720f,1.45f),new Vector2(-.925f,.90f),new Vector2(-.925f,.59f)};
                var vertices=new List<Vector3>();var triangles=new List<int>();
                for(int i=0;i<outline.Length;i++)
                {
                    int j=(i+1)%outline.Length;
                    Vector3 a=new Vector3(outline[i].x,.845f,outline[i].y),b=new Vector3(outline[j].x,.845f,outline[j].y);
                    Vector3 c=a-Vector3.up*.35f,d=b-Vector3.up*.35f;
                    Quad(vertices,triangles,a,b,d,c);
                    int n=vertices.Count;vertices.Add(new Vector3(0,.845f,.97f));vertices.Add(b);vertices.Add(a);
                    triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
                    n=vertices.Count;vertices.Add(new Vector3(0,.495f,.97f));vertices.Add(c);vertices.Add(d);
                    triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
                }
                MeshObject("Tapered between-axle casing / front wheel wells",machine,vertices,triangles,cyan);
            }

            void BuildTankFloor(Transform tank)
            {
                // Follow the tapered skins rather than using a 1.56 m rectangle
                // that protruded over the left tire sightline at the front.
                Vector3 a=TankPoint(.20f-.763f,.085f,-2.03f),b=TankPoint(.20f+.763f,.085f,-2.03f);
                Vector3 c=TankPoint(.20f+.720f,.085f,.02f),d=TankPoint(.20f-.720f,.085f,.02f);
                Vector3 thickness=Vector3.up*(.060f*config.TankHeightRatio);
                var vertices=new List<Vector3>();var triangles=new List<int>();
                Quad(vertices,triangles,a,d,c,b);
                Quad(vertices,triangles,a-thickness,b-thickness,c-thickness,d-thickness);
                Quad(vertices,triangles,a,b,b-thickness,a-thickness);
                Quad(vertices,triangles,b,c,c-thickness,b-thickness);
                Quad(vertices,triangles,c,d,d-thickness,c-thickness);
                Quad(vertices,triangles,d,a,a-thickness,d-thickness);
                MeshObject("Tapered steel tank floor / inside shell footprint",tank,vertices,triangles,steel);
            }

            void BuildRearTankWall(Transform tank)
            {
                // Tapered rear-wall panels surround an actual inlet opening.
                // The upper shoulders follow the shell, avoiding a square wall
                // sticking out beyond its narrower crown.
                float low=config.TankGeometryY(0),high=config.TankGeometryY(1.02f);
                float x=config.ConveyorOutletX,y=config.ConveyorOutletY-.78f;
                float fold=config.TankGeometryY(.80f);
                float[] rows={low,y-.14f,fold,y+.14f,high};
                Array.Sort(rows);
                for(int i=0;i<rows.Length-1;i++)
                {
                    float a=rows[i],b=rows[i+1];
                    float wa=Mathf.Lerp(.763f,.64f,Mathf.InverseLerp(fold,high,a));
                    float wb=Mathf.Lerp(.763f,.64f,Mathf.InverseLerp(fold,high,b));
                    float center=config.TankLateralOffset;
                    if(a>=y-.14001f&&b<=y+.14001f)
                    {
                        Panel("Tapered rear wall left of inlet",tank,new Vector3(center-wa,a,-2.012f),new Vector3(center-wb,b,-2.012f),new Vector3(x-.14f,b,-2.012f),new Vector3(x-.14f,a,-2.012f),white);
                        Panel("Tapered rear wall right of inlet",tank,new Vector3(x+.14f,a,-2.012f),new Vector3(x+.14f,b,-2.012f),new Vector3(center+wb,b,-2.012f),new Vector3(center+wa,a,-2.012f),white);
                    }
                    else Panel("Tapered rear wall above or below inlet",tank,new Vector3(center-wa,a,-2.012f),new Vector3(center-wb,b,-2.012f),new Vector3(center+wb,b,-2.012f),new Vector3(center+wa,a,-2.012f),white);
                }
            }

            void Tank(Transform machine)
            {
                world.TankPivot=Group("Snow tank forward dump hinge",machine,new Vector3(0,.78f,2.46f));
                var tank=world.TankPivot;
                // Hollow walls and floor, with separate roof panels around actual
                // inspection/inlet holes. There is no opaque cube beneath the window.
                world.BodyTank=Group("Hollow centered snow tank shell",tank,new Vector3(config.TankLateralOffset,config.TankGeometryY(.51f),-.98f));
                var shell=world.BodyTank;
                BuildTankFloor(tank);
                for(int side=-1;side<=1;side+=2)
                {
                    // Keep the machinery envelope and hollow storage, but break the
                    // block silhouette with a broad shoulder and a taper at the nose.
                    float x=side*.763f, top=side*.64f;
                    Panel("Tank lower side skin",shell,new Vector3(x,-.485f*config.TankHeightRatio,-1.05f),new Vector3(x,.29f*config.TankHeightRatio,-1.05f),new Vector3(side*.72f,.24f*config.TankHeightRatio,1.00f),new Vector3(side*.72f,-.485f*config.TankHeightRatio,1.00f),white);
                    Panel("Tank sloped upper shoulder",shell,new Vector3(x,.29f*config.TankHeightRatio,-1.05f),new Vector3(top,.515f*config.TankHeightRatio,-1.05f),new Vector3(side*.60f,.43f*config.TankHeightRatio,1.00f),new Vector3(side*.72f,.24f*config.TankHeightRatio,1.00f),white);
                    // The meeting skin normals are the real folded seam. An
                    // added 12 mm round steel rail here overhung the skin and
                    // obscured the tire crown from the driver's shallow angle.
                }
                BuildRearTankWall(tank);
                world.TankDoor=Group("Forward dump door hinge",tank,TankPoint(.20f,1.02f,.002f));
                BevelBox("Tapered opening snow dump door",world.TankDoor,new Vector3(0,-.55f*config.TankHeightRatio,0),new Vector3(1.44f,.94f*config.TankHeightRatio,.055f),.06f,white);
                Panel("Sloping forward tank crown",tank,TankPoint(-.44f,1.025f,-1.25f),TankPoint(.84f,1.025f,-1.25f),TankPoint(.80f,.94f,.02f),TankPoint(-.40f,.94f,.02f),white);
                Box("Roof rear rim",tank,TankPoint(.20f,1.025f,-2.005f),new Vector3(1.28f,.045f,.05f),white);
                Box("Roof rear left rim",tank,TankPoint(-.425f,1.025f,-1.625f),new Vector3(.03f,.045f,.71f),white);
                Box("Roof right of inspection window",tank,TankPoint(.475f,1.025f,-1.625f),new Vector3(.71f,.045f,.71f),white);
                world.TankInspection=Group("Physical snow inspection opening / clear sightline",tank,TankPoint(-.14f,1.05f,-1.625f));
                for(int side=-1;side<=1;side+=2)
                {
                    Box("Black inspection window side frame",world.TankInspection,new Vector3(side*.25f,0,0),new Vector3(.035f,.035f,.71f),black);
                    Box("Black inspection window end frame",world.TankInspection,new Vector3(0,0,side*.34f),new Vector3(.53f,.035f,.035f),black);
                }
                Beam("Inspection opening protective crossbar",world.TankInspection,new Vector3(-.24f,.01f,0),new Vector3(.24f,.01f,0),.014f,steel);
                // Snow starts at the actual upper face of the raised steel floor.
                // Keeping the old .06 origin buried a nearly empty load below it.
                world.TankSnow=SnowSurface("Uneven snow surface / actual tank contents",tank,new Vector3(config.TankLateralOffset,.085f,config.TankSnowCenterZ));
                world.DumpOutlet=Group("Snow exiting tilted tank at forward door",tank,new Vector3(config.TankLateralOffset,.16f,.12f));
                world.TankCylinderBarrels=new Transform[2];world.TankCylinderRods=new Transform[2];
                for(int side=-1;side<=1;side+=2)
                {
                    var stripe=Box("Flush tank glacier stripe",tank,TankPoint(.20f+side*.741f,.39f,-.93f),new Vector3(.002f,.23f,1.56f),cyan);
                    stripe.localRotation=Quaternion.Euler(0,-side*1.17f,0);
                    Label("AURORA",tank,TankPoint(.20f+side*.77f,.69f,-1.10f),.10f,new Color(.035f,.12f,.16f),Quaternion.Euler(0,side*90,0));
                    Label("R - 54",tank,TankPoint(.20f+side*.8f,.39f,-.9f),.073f,Color.white,Quaternion.Euler(0,side*90,0));
                    int i=(side+1)/2;
                    world.TankCylinderBarrels[i]=Cylinder("Tank lift cylinder barrel / articulated",machine,Vector3.zero,.095f,1,navy,Vector3.up);
                    world.TankCylinderRods[i]=Cylinder("Tank lift chrome piston / telescoping",machine,Vector3.zero,.043f,1,steel,Vector3.up);
                    Cylinder("Tank cylinder lower clevis pin",machine,new Vector3(side*.69f,.55f,.72f),.12f,.10f,steel,Vector3.right);
                    Cylinder("Tank cylinder moving clevis pin",tank,new Vector3(config.TankLateralOffset+side*.69f,config.TankGeometryY(.40f),-.74f),.10f,.11f,steel,Vector3.right);
                }
                Label("AURORA",tank,TankPoint(.20f,.68f,.033f),.13f,new Color(.035f,.12f,.16f),Quaternion.identity);
                Box("Front accent panel",tank,TankPoint(.20f,.34f,.030f),new Vector3(1.35f,.19f,.018f),navy);
            }


            Transform SnowSurface(string name,Transform parent,Vector3 position)
            {
                var surface=Group(name,parent,position);
                surface.gameObject.AddComponent<MeshFilter>();
                surface.gameObject.AddComponent<MeshRenderer>().sharedMaterial=snow;
                return surface;
            }

            void Panel(string name,Transform parent,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Material material)
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();
                Quad(vertices,triangles,a,b,c,d);Quad(vertices,triangles,d,c,b,a);
                MeshObject(name,parent,vertices,triangles,material);
            }

            void Wheels(Transform machine)
            {
                world.FrontWheels=new Transform[2];world.RearWheels=new Transform[2];
                for(int axle=0;axle<2;axle++)for(int side=0;side<2;side++)
                {
                    float sx=side==0?-1:1,z=axle==0?config.Wheelbase:0;
                    var pivot=Group(axle==0?"Front steering knuckle":"Rear wheel hub",machine,new Vector3(sx*config.WheelTrack*.5f,.39f,z));
                    var wheel=Group("Treaded drive wheel",pivot);
                    Cylinder("Tire carcass",wheel,Vector3.zero,.78f,config.TireWidth,rubber,Vector3.right);
                    float sectionHalf=config.TireWidth*.5f;
                    Cylinder("Pressed steel rim",wheel,new Vector3(sx*(sectionHalf+.008f),0,0),.43f,.025f,steel,Vector3.right);
                    Cylinder("Hub cap",wheel,new Vector3(sx*(sectionHalf+.030f),0,0),.19f,.055f,navy,Vector3.right);
                    for(int lug=0;lug<6;lug++)
                    {
                        float a=lug*Mathf.PI/3;
                        Cylinder("Wheel lug",wheel,new Vector3(sx*(sectionHalf+.031f),Mathf.Sin(a)*.145f,Mathf.Cos(a)*.145f),.032f,.025f,black,Vector3.right);
                    }
                    for(int tread=0;tread<26;tread++)
                    {
                        float a=tread*360f/26;
                        var t=Box("Tire traction block",wheel,new Vector3(0,Mathf.Cos(a*Mathf.Deg2Rad)*.382f,Mathf.Sin(a*Mathf.Deg2Rad)*.382f),new Vector3(config.TireWidth-.01f,.028f,.057f),rubber);
                        t.localRotation=Quaternion.Euler(a,0,8);
                    }
                    if(axle==0)world.FrontWheels[side]=pivot;else world.RearWheels[side]=pivot;
                    Box(axle==0?"Inboard front wheel splash guard":"Rear wheel arch mudguard",machine,
                        new Vector3(sx*(axle==0?.55f:.76f),.88f,z),new Vector3(axle==0?.22f:.49f,.07f,.94f),navy);
                }
            }

            void OperatorStation(Transform machine)
            {
                // Rebuild the actual station on the vehicle's left, without a reflection
                // transform. Forward remains +Z; mechanical rotation axes remain right-handed.
                var deck=world.OperatorStation=Group("Left-hand operator station",machine,new Vector3(-.55f+config.CockpitOffsetX,0,-.49f+config.CockpitOffsetZ));
                Box("Left operator floor",deck,new Vector3(-.01f,.86f,.02f),new Vector3(.86f,.09f,1.16f),steel);
                Box("Non-slip operator floor mat",deck,new Vector3(-.01f,.911f,.11f),new Vector3(.68f,.014f,.77f),rubber);
                Box("Left platform support",deck,new Vector3(-.33f,.69f,-.14f),new Vector3(.10f,.30f,.71f),navy);
                Box("Inboard operator side panel",deck,new Vector3(.43f,1.02f,.28f),new Vector3(.07f,.27f,.48f),cyan);
                Box("Seat pedestal",deck,new Vector3(0,1.03f,-.05f),new Vector3(.39f,.35f,.37f),black);
                for(int side=-1;side<=1;side+=2)
                {
                    Beam("Seat slide rail",deck,new Vector3(side*.16f,1.19f,-.28f),new Vector3(side*.16f,1.19f,.18f),.035f,steel);
                    Cylinder("Seat suspension bushing",deck,new Vector3(side*.16f,1.12f,-.10f),.075f,.11f,rubber,Vector3.up);
                }
                var seat=world.Seat=Group("Driver seat assembly / left",deck,new Vector3(0,1.23f,-.08f));
                BevelBox("Operator cushion",seat,Vector3.zero,new Vector3(.55f,.14f,.48f),.045f,rubber);
                BevelBox("Operator seat back",seat,new Vector3(0,.25f,-.22f),new Vector3(.53f,.55f,.13f),.045f,rubber);
                // Short rear arm pads leave the hydraulic labels and valve stems
                // exposed. Long rails here previously crossed the actual controls.
                Beam("Seat left rear arm pad",seat,new Vector3(-.32f,.17f,-.22f),new Vector3(-.32f,.17f,-.03f),.060f,rubber);
                Beam("Seat right rear arm pad",seat,new Vector3(.32f,.17f,-.22f),new Vector3(.32f,.17f,-.03f),.060f,rubber);
                world.CameraMount=Group("Seated eyes / attached to driver seat",seat,new Vector3(-.21f,.67f,.09f));
                world.SteeringColumn=Group("Complete steering column",deck,new Vector3(0,config.SteeringWheelHeight,config.SteeringWheelZ));
                var steeringTilt=Quaternion.Euler(-config.SteeringWheelTilt,0,0);
                Vector3 upperShaft=-(steeringTilt*Vector3.up)*.24f;
                Vector3 columnFoot=new Vector3(0,.94f-config.SteeringWheelHeight,.03f);
                Beam("Steering column lower shaft",world.SteeringColumn,columnFoot,upperShaft,.065f,black);
                Sphere("Steering column universal joint",world.SteeringColumn,upperShaft,.080f,steel);
                Beam("Steering column upper shaft",world.SteeringColumn,upperShaft,Vector3.zero,.045f,steel);
                Cylinder("Steering column foot",world.SteeringColumn,columnFoot,.14f,.075f,steel,Vector3.up);
                Box("Steering column support",world.SteeringColumn,columnFoot+Vector3.up*.08f,new Vector3(.10f,.15f,.10f),navy);
                world.SteeringWheel=Group("Steering wheel rotation",world.SteeringColumn,Vector3.zero);
                world.SteeringWheel.localRotation=steeringTilt;
                Torus("Steering wheel rim",world.SteeringWheel,Vector3.zero,.19f,.022f,rubber,Quaternion.identity);
                for(int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3;
                    Beam("Steering wheel spoke",world.SteeringWheel,Vector3.zero,new Vector3(Mathf.Cos(a)*.18f,0,Mathf.Sin(a)*.18f),.021f,steel);
                }
                Cylinder("Steering centre",world.SteeringWheel,Vector3.zero,.085f,.04f,black,Vector3.up);
                Cylinder("Steering retaining nut",world.SteeringWheel,new Vector3(0,.027f,0),.029f,.018f,steel,Vector3.up);
                var dash=world.Dashboard=Group("Driver-facing dashboard / left",deck,new Vector3(0,1.31f,config.SteeringWheelZ+.27f));
                dash.localRotation=Quaternion.Euler(-15,0,0);
                BevelBox("Angled console fascia",dash,Vector3.zero,new Vector3(.59f,.13f,.25f),.025f,navy);
                Box("LCD bezel",dash,new Vector3(-.09f,.071f,.015f),new Vector3(.28f,.013f,.15f),black);
                Box("LCD face",dash,new Vector3(-.09f,.079f,.015f),new Vector3(.24f,.008f,.11f),cyan);
                Label("AURORA",dash,new Vector3(-.09f,.085f,.015f),.024f,Color.white,Quaternion.Euler(270,180,0));
                Cylinder("Pressure dial bezel",dash,new Vector3(.18f,.077f,.015f),.093f,.018f,steel,Vector3.up);
                Cylinder("Pressure dial face",dash,new Vector3(.18f,.089f,.015f),.077f,.008f,black,Vector3.up);
                Beam("Pressure dial needle",dash,new Vector3(.18f,.097f,.015f),new Vector3(.195f,.097f,.042f),.006f,orange);
                // Mount the instrument lamp above the dashboard, inboard of the
                // driver's front-tire sightline. Its old x=-.20 position lay
                // exactly between the seated eye and the upper tire tread.
                var lamp=Group("Complete operator instrument lamp",deck,new Vector3(-.02f,0,config.SteeringWheelZ+.37f));
                Beam("Operator work lamp stalk",lamp,new Vector3(0,1.35f,.02f),new Vector3(0,1.65f,.02f),.018f,steel);
                Box("Shielded operator work lamp",lamp,new Vector3(0,1.67f,0),new Vector3(.12f,.055f,.06f),black);
                Box("Operator lamp downward diffuser",lamp,new Vector3(0,1.64f,0),new Vector3(.095f,.008f,.044f),lightMat);
                var taskLamp=Group("Operator instrument illumination",lamp,new Vector3(0,1.615f,0)).gameObject.AddComponent<Light>();
                taskLamp.type=LightType.Point;taskLamp.range=1.6f;taskLamp.intensity=.18f;
                taskLamp.color=new Color(.83f,.92f,1f);taskLamp.shadows=LightShadows.None;
                var pedals=world.Pedals=Group("Left operator pedal floor",deck,new Vector3(0,.923f,.36f));
                Box("Pedal backing plate",pedals,Vector3.zero,new Vector3(.4f,.025f,.32f),black);
                world.AcceleratorPedal=Group("Accelerator pedal pivot",pedals,new Vector3(.11f,.035f,.04f));
                Box("Accelerator pedal",world.AcceleratorPedal,new Vector3(0,0,.04f),new Vector3(.09f,.035f,.18f),steel);
                world.AcceleratorPedal.localRotation=Quaternion.Euler(-18,0,0);
                world.BrakePedal=Group("Brake pedal pivot",pedals,new Vector3(-.1f,.035f,.04f));
                Box("Brake pedal",world.BrakePedal,new Vector3(0,0,.02f),new Vector3(.12f,.035f,.12f),steel);
                world.BrakePedal.localRotation=Quaternion.Euler(-18,0,0);
                for(int rib=0;rib<5;rib++)
                {
                    Box("Accelerator rubber tread",world.AcceleratorPedal,new Vector3(0,.02f,-.02f+rib*.03f),new Vector3(.085f,.009f,.009f),rubber);
                    Box("Brake rubber tread",world.BrakePedal,new Vector3(0,.02f,-.02f+rib*.02f),new Vector3(.115f,.009f,.009f),rubber);
                }
                Cylinder("Accelerator hinge pin",pedals,new Vector3(.11f,.035f,-.025f),.028f,.11f,steel,Vector3.right);
                Cylinder("Brake hinge pin",pedals,new Vector3(-.10f,.035f,-.025f),.028f,.14f,steel,Vector3.right);
                // Four actual adjacent spools in one transverse bank. Their X
                // positions, travel axes, labels and moving pick targets agree.
                var panel=Group("Transverse four spool hydraulic bank",deck,new Vector3(.47f,1.42f,.31f));
                BevelBox("Valve bank steel chassis",panel,Vector3.zero,new Vector3(.70f,.12f,.25f),.025f,navy);
                Box("Brushed hydraulic label fascia",panel,new Vector3(0,.065f,-.07f),new Vector3(.68f,.012f,.10f),steel);
                world.VerticalAugerLever=Lever("Vertical auger lever",panel,new Vector3(-.255f,.08f,0),"V AUG",cyan);
                world.ConditionerLever=Lever("Conditioner lift lever",panel,new Vector3(-.085f,.08f,0),"LIFT",orange);
                world.HorizontalAugerLever=Lever("Horizontal auger lever",panel,new Vector3(.085f,.08f,0),"H AUG",cyan);
                world.DumpLever=Lever("Snow dump hydraulic lever",panel,new Vector3(.255f,.08f,0),"DUMP",orange);
                for(int i=0;i<4;i++)
                {
                    float x=-.255f+i*.17f;
                    Beam("Hydraulic spool pipe",panel,new Vector3(x,-.05f,.06f),new Vector3(x,-.27f,.06f),.026f,rubber);
                    Cylinder("Valve manifold fitting",panel,new Vector3(x,-.09f,.06f),.044f,.04f,steel,Vector3.up);
                }
                // Independent quarter-turn handles have separate bearings in a
                // second transverse row. At either stop each clears its neighbor.
                var waterPanel=Group("Adjacent flood and wash water manifold",deck,new Vector3(.65f,1.44f,.04f));
                Beam("Water manifold left bracket",deck,new Vector3(.40f,1.13f,.04f),new Vector3(.40f,1.43f,.04f),.035f,navy);
                Beam("Water manifold right bracket",deck,new Vector3(.88f,1.13f,.04f),new Vector3(.88f,1.43f,.04f),.035f,navy);
                BevelBox("Water valve mounting plate",waterPanel,Vector3.zero,new Vector3(.56f,.035f,.28f),.02f,steel);
                world.FloodValveMount=Group("Flood valve fixed bearing",waterPanel,new Vector3(-.13f,.02f,0));
                world.WashValveMount=Group("Wash valve fixed bearing",waterPanel,new Vector3(.13f,.02f,0));
                world.FloodLever=WaterValve("FLOOD WATER",world.FloodValveMount,blue);
                world.WashValve=WaterValve("WASH WATER",world.WashValveMount,cyan);
                // Behind and between the taller water stems, within seated reach.
                // One stationary assembly owns spindle, bearings, index and rim.
                float bladeControlX=config.BladeWheelX,bladeControlZ=config.BladeWheelZ;
                var bladeAssembly=Group("Complete blade screw and handwheel assembly",deck,new Vector3(bladeControlX,config.BladeWheelHeight,bladeControlZ));
                world.BladeWheel=Group("Blade depth handwheel",bladeAssembly,Vector3.zero);
                Torus("Blade handwheel rim",world.BladeWheel,Vector3.zero,.13f,.018f,orange,Quaternion.identity);
                for(int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3;
                    Beam("Blade handwheel spoke",world.BladeWheel,Vector3.zero,new Vector3(Mathf.Cos(a)*.12f,0,Mathf.Sin(a)*.12f),.015f,steel);
                }
                Cylinder("Handwheel crank",world.BladeWheel,new Vector3(.11f,.045f,0),.032f,.09f,black,Vector3.up);
                Cylinder("Blade handwheel machined hub",world.BladeWheel,Vector3.zero,.055f,.07f,steel,Vector3.up);
                Cylinder("Blade spindle fixed bearing",bladeAssembly,new Vector3(0,-.08f,0),.075f,.09f,navy,Vector3.up);
                Cylinder("Blade spindle lower collar",bladeAssembly,new Vector3(0,.69f-config.BladeWheelHeight,0),.058f,.09f,steel,Vector3.up);
                Beam("Blade index support",bladeAssembly,new Vector3(0,-.115f,0),new Vector3(.10f,-.115f,-.17f),.018f,steel);
                Box("Blade travel index plate",bladeAssembly,new Vector3(.10f,-.12f,-.17f),new Vector3(.25f,.018f,.09f),navy);
                Label("BLADE  /  3.5 TURNS",bladeAssembly,new Vector3(.10f,-.107f,-.17f),.015f,Color.white,Quaternion.Euler(270,180,0));
                Beam("Blade adjustment spindle",bladeAssembly,new Vector3(0,.48f-config.BladeWheelHeight,0),new Vector3(0,-.03f,0),.022f,steel);
                // Accessible manual plunger sits inboard/right of the operator. Only the
                // rod and T-handle move; the guide and guard remain fixed to the deck.
                var breakerAssembly=Group("Complete snow breaker assembly",deck,new Vector3(config.SnowBreakerX,1.45f,config.SnowBreakerZ));
                Cylinder("Snow breaker fixed guide",breakerAssembly,new Vector3(0,-.23f,0),.075f,.36f,navy,Vector3.up);
                Cylinder("Snow breaker guide collar",breakerAssembly,new Vector3(0,-.045f,0),.091f,.027f,steel,Vector3.up);
                world.SnowBreakerPlunger=Group("Snow breaker plunger / manual stroke",breakerAssembly);
                Cylinder("Snow breaker sliding rod",world.SnowBreakerPlunger,new Vector3(0,-.03f,0),.025f,.47f,steel,Vector3.up);
                Beam("Snow breaker T handle",world.SnowBreakerPlunger,new Vector3(-.105f,.22f,0),new Vector3(.105f,.22f,0),.038f,orange);
                Beam("Snow breaker hand guard",breakerAssembly,new Vector3(-.08f,-.21f,-.05f),new Vector3(.08f,-.21f,-.05f),.025f,black);
                Beam("Equipment-side rear safety rail",machine,new Vector3(.89f,1.16f,-.82f),new Vector3(.89f,1.81f,-.82f),.045f,steel);
                Beam("Equipment-side top safety rail",machine,new Vector3(.89f,1.81f,-.82f),new Vector3(.89f,1.81f,-.12f),.045f,steel);
                Beam("Operator forward boarding grab rail",deck,new Vector3(-.40f,.91f,.63f),new Vector3(-.40f,1.66f,.63f),.045f,steel);
                Beam("Operator rear boarding grab rail",deck,new Vector3(-.40f,.91f,-.39f),new Vector3(-.40f,1.59f,-.39f),.045f,steel);
                var access=world.AccessSteps=Group("Left-side boarding access",machine,new Vector3(-.91f,0,-.63f));
                Box("Boarding lower step",access,new Vector3(0,.30f,0),new Vector3(.28f,.06f,.44f),steel);
                Box("Boarding upper step",access,new Vector3(.035f,.58f,0),new Vector3(.21f,.06f,.44f),steel);
                for(int i=0;i<5;i++)Box("Step non-slip tread",access,new Vector3(0,.335f,-.16f+i*.08f),new Vector3(.23f,.012f,.025f),black);
                Label("R-54",deck,new Vector3(0,1.13f,-.33f),.07f,Color.white,Quaternion.Euler(0,180,0));
            }

            Transform Lever(string name,Transform parent,Vector3 position,string label,Material accent)
            {
                Cylinder("Fixed valve spool collar",parent,position-Vector3.up*.015f,.088f,.028f,steel,Vector3.up);
                for(int ring=0;ring<4;ring++)
                    Cylinder("Accordion lever boot",parent,position+Vector3.up*(ring*.012f),.075f-ring*.009f,.013f,rubber,Vector3.up);
                Cylinder("Hydraulic lever cross pivot",parent,position+Vector3.up*.025f,.033f,.085f,steel,Vector3.right);
                var lever=Group(name,parent,position);
                Beam("Polished hydraulic lever stem",lever,Vector3.zero,new Vector3(0,.205f,0),.018f,steel);
                Sphere("Black phenolic hydraulic grip",lever,new Vector3(0,.225f,0),.070f,black);
                Cylinder("Lever identification collar",lever,new Vector3(0,.176f,0),.026f,.022f,accent,Vector3.up);
                Box("Engraved lever identification plate",parent,position+new Vector3(0,-.003f,-.085f),new Vector3(.145f,.008f,.070f),black);
                Label(label,parent,position+new Vector3(0,.004f,-.085f),.0135f,Color.white,Quaternion.Euler(270,180,0));
                Label(label=="LIFT"||label=="DUMP"?"DOWN":"RUN",parent,position+new Vector3(0,.004f,.078f),.0105f,new Color(.4f,.9f,.9f),Quaternion.Euler(270,180,0));
                Label(label=="LIFT"||label=="DUMP"?"UP":"OFF",parent,position+new Vector3(0,.004f,-.112f),.0105f,new Color(.95f,.66f,.2f),Quaternion.Euler(270,180,0));
                return lever;
            }

            Transform WaterValve(string label,Transform mount,Material accent)
            {
                Cylinder("Water valve body",mount,new Vector3(0,-.075f,0),.075f,.13f,accent,Vector3.up);
                Cylinder("Water valve mounting collar",mount,Vector3.zero,.075f,.022f,steel,Vector3.up);
                Cylinder("Water valve gland nut",mount,new Vector3(0,.023f,0),.045f,.025f,black,Vector3.up);
                Beam("Independent valve outlet pipe",mount,new Vector3(0,-.11f,0),new Vector3(.08f,-.11f,0),.029f,steel);
                Beam("Independent valve rubber line",mount,new Vector3(.08f,-.11f,0),new Vector3(.08f,-.43f,-.07f),.029f,rubber);
                var handle=Group(label+" / quarter turn L handle",mount,new Vector3(0,.025f,0));
                float stem=config.WaterValveStemHeight;
                Cylinder("Visible rotating valve stem",handle,new Vector3(0,stem*.5f,0),.018f,stem,steel,Vector3.up);
                Box("Quarter turn valve stop dog",handle,new Vector3(-.046f,.012f,0),new Vector3(.055f,.020f,.020f),steel);
                Beam("Right angle valve handle",handle,new Vector3(0,stem,0),new Vector3(-.115f,stem,0),.022f,steel);
                Beam("Valve grip",handle,new Vector3(-.055f,stem,0),new Vector3(-.115f,stem,0),.034f,accent);
                for(int i=0;i<=6;i++)
                {
                    float a=i*Mathf.PI/12;
                    Vector3 p=new Vector3(-Mathf.Cos(a)*.115f,.002f,-Mathf.Sin(a)*.115f);
                    Cylinder("Quarter turn valve index",mount,p,.008f,.009f,black,Vector3.up);
                }
                Cylinder("Valve closed mechanical stop",mount,new Vector3(-.052f,.018f,.025f),.015f,.035f,steel,Vector3.up);
                Cylinder("Valve open mechanical stop",mount,new Vector3(.025f,.018f,-.052f),.015f,.035f,steel,Vector3.up);
                Label(label,mount,new Vector3(.003f,.004f,.078f),.012f,Color.black,Quaternion.Euler(270,180,0));
                Label("CLOSED",mount,new Vector3(-.065f,.006f,.029f),.010f,Color.black,Quaternion.Euler(270,180,0));
                Label("OPEN",mount,new Vector3(.027f,.006f,-.080f),.010f,Color.black,Quaternion.Euler(270,180,0));
                return handle;
            }

            void Conditioner()
            {
                var c=world.Conditioner=Group("Trailing conditioner / articulated hitch",root);
                float width=config.ConditionerWidth, half=width*.5f;
                float wetWidth=config.WaterDistributionWidth;
                // Blade and towel use the simulation's exact working width. The housing
                // surrounds them rather than squeezing the working parts inside a blade-wide shell.
                world.ConditionerHousing=BevelBox("Conditioner stainless housing",c,new Vector3(0,.24f,0),new Vector3(Mathf.Max(width+.12f,wetWidth+.02f),.32f,.73f),.075f,steel);
                world.ConditionerHousingMesh=world.ConditionerHousing.GetComponent<MeshFilter>().sharedMesh;
                Box("Conditioner upper guard",c,new Vector3(0,.43f,.02f),new Vector3(width+.09f,.08f,.62f),navy);
                world.DistributionTowel=Box("Trailing distribution towel",c,new Vector3(0,.018f,-.44f),new Vector3(wetWidth,.025f,.27f),cloth);
                world.DistributionTowelMesh=world.DistributionTowel.GetComponent<MeshFilter>().sharedMesh;
                for(int i=0;i<17;i++)Box("Towel longitudinal fold",c,new Vector3(Mathf.Lerp(-wetWidth*.5f+.02f,wetWidth*.5f-.02f,i/16f),.033f,-.44f),new Vector3(.018f,.01f,.26f),cloth);
                world.CuttingBlade=Box("Cutting blade edge",c,new Vector3(0,.043f,.30f),new Vector3(width,.0127f,.127f),steel);
                world.CuttingBladeMesh=world.CuttingBlade.GetComponent<MeshFilter>().sharedMesh;
                for(int side=-1;side<=1;side+=2)
                {
                    var edge=Box("Exposed conditioner side plate",c,new Vector3(side*(Mathf.Max(width,wetWidth)*.5f+.007f),.14f,-.04f),new Vector3(.014f,.24f,.8f),steel);
                    if(side==-1)world.ConditionerLeftEdge=edge;
                    Box("Amber conditioner clearance edge",c,new Vector3(side*(Mathf.Max(width,wetWidth)*.5f+.007f),.034f,-.04f),new Vector3(.018f,.035f,.8f),orange);
                    Beam("Lift linkage",c,new Vector3(side*.58f,.34f,.25f),new Vector3(side*.58f,.58f,.76f),.055f,steel);
                    Cylinder("Conditioner runner",c,new Vector3(side*(half-.08f),.07f,.22f),.13f,.045f,black,Vector3.right);
                }
                // A cutaway rear-facing inspection grille exposes moving flights without an open operator-side hazard.
                Box("Auger inspection recess",c,new Vector3(0,.25f,-.373f),new Vector3(width-.16f,.23f,.028f),black);
                world.HorizontalAuger=Group("Horizontal auger shaft",c,new Vector3(0,.25f,-.395f));
                Cylinder("Horizontal auger core",world.HorizontalAuger,Vector3.zero,.075f,width-.12f,steel,Vector3.right);
                Helix("Left conveyor flight",world.HorizontalAuger,-half+.09f,0,.125f,.19f,steel,Vector3.right);
                Helix("Right conveyor flight",world.HorizontalAuger,0,half-.09f,.125f,-.19f,steel,Vector3.right);
                for(int i=0;i<11;i++)
                {
                    float x=Mathf.Lerp(-half+.1f,half-.1f,i/10f);
                    Beam("Auger inspection guard rod",c,new Vector3(x,.12f,-.44f),new Vector3(x,.38f,-.44f),.012f,navy);
                    Cylinder("Flood outlet nozzle",c,new Vector3(x,.08f,-.42f),.014f,.05f,black,Vector3.down);
                }
                Beam("Wash water manifold",c,new Vector3(-half+.1f,.42f,-.19f),new Vector3(half-.1f,.42f,-.19f),.042f,cyan);
                Box("Wash pickup squeegee contact",c,new Vector3(0,.027f,config.WashPickupContactFore),new Vector3(width,.025f,config.WashPickupContactLength),rubber);
                Beam("Flood distribution manifold",c,new Vector3(-wetWidth*.5f+.02f,.11f,-.42f),new Vector3(wetWidth*.5f-.02f,.11f,-.42f),.035f,steel);
                world.WaterOutlet=Group("Visible flood emission",c,new Vector3(0,.09f,-.43f));
                world.SnowOutlet=Group("Escaping snow at conditioner",c,new Vector3(half-.2f,.16f,-.41f));
                Label("AURORA  /  CONDITIONER",c,new Vector3(0,.477f,.01f),.065f,Color.white,Quaternion.Euler(270,180,0));
                world.ConditionerCylinderBarrels=new Transform[2];world.ConditionerCylinderRods=new Transform[2];
                for(int side=-1;side<=1;side+=2)
                {
                    Beam("Hydraulic flexible hose",c,new Vector3(side*.23f,.44f,.12f),new Vector3(side*.29f,.70f,.64f),.03f,rubber);
                    int i=(side+1)/2;
                    world.ConditionerCylinderBarrels[i]=Cylinder("Conditioner lift barrel / articulated",world.Machine,Vector3.zero,.074f,1,navy,Vector3.up);
                    world.ConditionerCylinderRods[i]=Cylinder("Conditioner lift chrome piston / telescoping",world.Machine,Vector3.zero,.035f,1,steel,Vector3.up);
                    Cylinder("Conditioner lift lower clevis",c,new Vector3(side*.55f,.43f,.30f),.09f,.10f,steel,Vector3.right);
                    Cylinder("Conditioner lift chassis clevis",world.Machine,new Vector3(side*.55f,.76f,-.57f),.09f,.10f,steel,Vector3.right);
                }
            }

            void VerticalConveyor(Transform machine)
            {
                float x=config.ConveyorOutletX,y=config.ConveyorOutletY;
                var tube=Group("Compact vertical snow conveyor / rear tank inlet",machine,new Vector3(x,.40f,.235f));
                Beam("Covered snow transfer throat",machine,new Vector3(0,.52f,-.78f),new Vector3(x,.63f,.235f),.23f,navy);
                Cylinder("Lower elevator shroud",tube,new Vector3(0,.29f,0),.29f,.58f,navy,Vector3.up);
                world.VerticalAuger=Group("Vertical auger moving flights",tube,new Vector3(0,.58f,0));
                Cylinder("Elevator drive shaft",world.VerticalAuger,new Vector3(0,.21f,0),.055f,.48f,steel,Vector3.up);
                Helix("Vertical elevator flight",world.VerticalAuger,0,.46f,.105f,.16f,steel,Vector3.up);
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI*.25f;
                    Beam("Elevator inspection guard",tube,new Vector3(Mathf.Cos(a)*.145f,.57f,Mathf.Sin(a)*.145f),new Vector3(Mathf.Cos(a)*.145f,.99f,Mathf.Sin(a)*.145f),.012f,navy);
                }
                Cylinder("Compact discharge casing",tube,new Vector3(0,y-.40f,0),.32f,.23f,navy,Vector3.up);
                Cylinder("Vertical conveyor hydraulic motor",tube,new Vector3(0,y-.40f+.23f,0),.17f,.23f,cyan,Vector3.up);
                Cylinder("Conveyor motor end cap",tube,new Vector3(0,y-.40f+.36f,0),.185f,.035f,steel,Vector3.up);
                Beam("Short rear-wall delivery throat",machine,new Vector3(x,y,.235f),new Vector3(x,y,config.ConveyorOutletZ),.245f,navy);
                // Detachable inlet: lifting the tank separates its rear wall from
                // this fixed chute. Core handles the resulting missed-bin spill.
                world.TankDischarge=Group("Rear-wall auger discharge into tank",machine,new Vector3(x,y,config.ConveyorOutletZ));
                Beam("Hydraulic motor return hose",tube,new Vector3(.11f,y-.40f+.22f,0),new Vector3(.18f,.35f,0),.035f,rubber);
            }

            void Brush(Transform machine)
            {
                world.BrushArm=Group("Board brush articulated arm / left side",machine,new Vector3(config.BrushPivotX,.38f,config.BrushPivotZ));
                world.BrushLink=Beam("Board brush link / compliant extension",world.BrushArm,Vector3.zero,new Vector3(-config.BrushArmLength,-.18f,0),.065f,steel);
                Cylinder("Brush deployment hinge",world.BrushArm,Vector3.zero,.13f,.18f,navy,Vector3.up);
                world.Brush=Group("Rotating board brush",world.BrushArm,new Vector3(-config.BrushArmLength,-.23f,0));
                Cylinder("Brush motor housing",world.Brush,new Vector3(0,.15f,0),.15f,.19f,black,Vector3.up);
                Cylinder("Brush plate",world.Brush,new Vector3(0,.055f,0),.42f,.055f,cyan,Vector3.up);
                world.BrushRoller=Group("Brush contact roller carriage",world.BrushArm);
                Beam("Brush contact roller stalk",world.BrushRoller,Vector3.zero,new Vector3(-.15f,0,0),.033f,steel);
                Cylinder("Board brush kickplate contact roller",world.BrushRoller,new Vector3(-.15f,0,0),.16f,.13f,rubber,Vector3.up);
                BatchMeshes(world.BrushRoller);
                for(int i=0;i<32;i++)
                {
                    float a=i*Mathf.PI/16;
                    Beam("Board brush bristle bundle",world.Brush,new Vector3(Mathf.Cos(a)*.16f,.045f,Mathf.Sin(a)*.16f),new Vector3(Mathf.Cos(a)*.25f,-.06f,Mathf.Sin(a)*.25f),.033f,rubber);
                }
            }

            Transform BevelBox(string name,Transform parent,Vector3 centre,Vector3 size,float bevel,Material material)
            {
                // Octagonal footprint gives bodywork manufactured edge chamfers instead of primitive cubes.
                Vector3 localPosition=centre;
                centre=Vector3.zero;
                float hx=size.x*.5f,hz=size.z*.5f,hy=size.y*.5f;
                Vector2[] ring={new Vector2(-hx+bevel,-hz),new Vector2(hx-bevel,-hz),new Vector2(hx,-hz+bevel),new Vector2(hx,hz-bevel),new Vector2(hx-bevel,hz),new Vector2(-hx+bevel,hz),new Vector2(-hx,hz-bevel),new Vector2(-hx,-hz+bevel)};
                var v=new List<Vector3>();var t=new List<int>();
                for(int i=0;i<8;i++)
                {
                    Vector2 a=ring[i],b=ring[(i+1)%8];
                    Quad(v,t,centre+new Vector3(a.x,-hy,a.y),centre+new Vector3(a.x,hy,a.y),centre+new Vector3(b.x,hy,b.y),centre+new Vector3(b.x,-hy,b.y));
                    int n=v.Count;v.Add(centre+Vector3.up*hy);v.Add(centre+new Vector3(b.x,hy,b.y));v.Add(centre+new Vector3(a.x,hy,a.y));t.Add(n);t.Add(n+1);t.Add(n+2);
                    n=v.Count;v.Add(centre-Vector3.up*hy);v.Add(centre+new Vector3(a.x,-hy,a.y));v.Add(centre+new Vector3(b.x,-hy,b.y));t.Add(n);t.Add(n+1);t.Add(n+2);
                }
                var renderer=MeshObject(name,parent,v,t,material);
                renderer.transform.localPosition=localPosition;
                return renderer.transform;
            }

            void Torus(string name,Transform parent,Vector3 position,float radius,float tube,Material material,Quaternion rotation)
            {
                var v=new List<Vector3>();var tri=new List<int>();
                const int segments=40, sides=8;
                for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)
                {
                    float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides;
                    v.Add(position+rotation*new Vector3(Mathf.Cos(a)*(radius+Mathf.Cos(b)*tube),Mathf.Sin(b)*tube,Mathf.Sin(a)*(radius+Mathf.Cos(b)*tube)));
                }
                for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)
                {
                    int a=i*sides+j,b=((i+1)%segments)*sides+j,c=((i+1)%segments)*sides+(j+1)%sides,d=i*sides+(j+1)%sides;
                    tri.Add(a);tri.Add(c);tri.Add(b);tri.Add(a);tri.Add(d);tri.Add(c);
                }
                MeshObject(name,parent,v,tri,material);
            }

            void Helix(string name,Transform parent,float start,float end,float radius,float pitch,Material material,Vector3 axis)
            {
                var v=new List<Vector3>();var t=new List<int>();
                int count=Mathf.CeilToInt(Mathf.Abs((end-start)/pitch)*40);
                Quaternion rotation=Quaternion.FromToRotation(Vector3.up,axis);
                for(int i=0;i<=count;i++)
                {
                    float y=Mathf.Lerp(start,end,i/(float)count),a=(y-start)/pitch*Mathf.PI*2;
                    Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    v.Add(rotation*(new Vector3(0,y,0)+radial*.039f));
                    v.Add(rotation*(new Vector3(0,y,0)+radial*radius));
                }
                for(int i=0;i<count;i++)
                {
                    int n=i*2;t.Add(n);t.Add(n+1);t.Add(n+3);t.Add(n);t.Add(n+3);t.Add(n+2);
                    t.Add(n+3);t.Add(n+1);t.Add(n);t.Add(n+2);t.Add(n+3);t.Add(n);
                }
                MeshObject(name,parent,v,t,material);
            }
        }
    }

    // Legacy dynamic fonts can replace their texture when another UI requests new glyph sizes.
    // Keep the owned depth-tested material in sync without modifying the shared builtin material.
    public sealed class IceWorldTextAtlas : MonoBehaviour
    {
        Font font;
        Material material;
        public void Initialize(Font source,Material target)
        {
            font=source; material=target;
            Font.textureRebuilt+=TextureRebuilt;
        }
        void TextureRebuilt(Font rebuilt)
        {
            if(rebuilt==font && material!=null) material.mainTexture=font.material.mainTexture;
        }
        void OnDestroy() { Font.textureRebuilt-=TextureRebuilt; }
    }
}
