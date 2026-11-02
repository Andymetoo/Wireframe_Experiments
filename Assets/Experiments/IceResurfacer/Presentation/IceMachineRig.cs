using System;
using System.Collections.Generic;
using System.Reflection;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Saved, editable Aurora assemblies. Binds the authored parts into the same IceWorld
    /// contract the generated machine fills, so every consumer works unchanged. Owns no
    /// meshes or materials: nothing here is ever added to IceWorld's destroy lists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceMachineRig : MonoBehaviour
    {
        [Serializable] public sealed class Part { public string field; public Transform target; }
        [Serializable] public sealed class PartArray { public string field; public Transform[] targets; }
        [Serializable] public sealed class MountPose { public string label; public Transform mount; public Vector3 position; public Quaternion rotation; }

        [Tooltip("Combine static authored meshes by material on the Play Mode instance. Saved assets are never modified.")]
        public bool batchAtRuntime = true;
        [SerializeField] List<Part> parts = new List<Part>();
        [SerializeField] List<PartArray> partArrays = new List<PartArray>();
        // Bake-time snapshots: configuration for rebake warnings, and operator-control mount
        // poses (machine space) for reporting a rearranged cockpit. Neither drives runtime.
        [SerializeField, HideInInspector] string bakedConfigJson;
        [SerializeField, HideInInspector] List<MountPose> bakedMounts = new List<MountPose>();

        // Fields generated with the arena, never by the machine.
        static readonly HashSet<string> ArenaFields = new HashSet<string> { "PitSnow" };

        public static IEnumerable<FieldInfo> MachineTransformFields()
        {
            foreach(var field in typeof(IceWorld).GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(field.FieldType==typeof(Transform)&&!ArenaFields.Contains(field.Name))yield return field;
        }
        public static IEnumerable<FieldInfo> MachineTransformArrayFields()
        {
            foreach(var field in typeof(IceWorld).GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(field.FieldType==typeof(Transform[]))yield return field;
        }

        public string BakedConfigJson { get { return bakedConfigJson; } }
        public IReadOnlyList<Part> Parts { get { return parts; } }
        public IReadOnlyList<PartArray> PartArrays { get { return partArrays; } }

        public Transform Get(string field)
        {
            foreach(var part in parts)if(part.field==field)return part.target;
            return null;
        }

        /// <summary>Fills the runtime contract from the saved parts and swaps labels to the
        /// runtime depth-tested lettering. Play Mode instance changes revert on exit.</summary>
        internal void Bind(IceWorld world)
        {
            Fill(world);
            if(world.Lettering!=null)
                foreach(var text in GetComponentsInChildren<TextMesh>(true))
                {
                    var renderer=text.GetComponent<MeshRenderer>();
                    if(renderer!=null)renderer.sharedMaterial=world.Lettering;
                }
        }

        /// <summary>Reference-only view of the authored parts, for editor tools and validation.</summary>
        public IceWorld Collect()
        {
            var world=new IceWorld();
            Fill(world);
            return world;
        }

        void Fill(IceWorld world)
        {
            var missing=new List<string>();
            foreach(var field in MachineTransformFields())
            {
                var target=Get(field.Name);
                if(target==null)missing.Add(field.Name);
                field.SetValue(world,target);
            }
            foreach(var field in MachineTransformArrayFields())
            {
                Transform[] targets=null;
                foreach(var entry in partArrays)if(entry.field==field.Name)targets=entry.targets;
                if(targets==null||targets.Length==0||Array.IndexOf(targets,null)>=0)missing.Add(field.Name);
                field.SetValue(world,targets==null?null:(Transform[])targets.Clone());
            }
            if(missing.Count>0)
                throw new InvalidOperationException("Authored Aurora machine '"+name+"' has unassigned parts: "+string.Join(", ",missing)+
                    ". Reassign them on the IceMachineRig or rebake the prefab.");
            // Geometry witnesses follow whatever mesh the authored part currently carries.
            world.CuttingBladeMesh=SharedMesh(world.CuttingBlade);
            world.DistributionTowelMesh=SharedMesh(world.DistributionTowel);
            world.ConditionerHousingMesh=SharedMesh(world.ConditionerHousing);
        }
        static Mesh SharedMesh(Transform part)
        {
            var filter=part!=null?part.GetComponent<MeshFilter>():null;
            return filter!=null?filter.sharedMesh:null;
        }

        // ---- Ownership classes ------------------------------------------------------------

        /// <summary>A part whose position the simulation reads from SimulatorConfig.</summary>
        public readonly struct MechanicalAnchor
        {
            public readonly string Label; public readonly Transform Part, Frame; public readonly Vector3 Expected;
            public MechanicalAnchor(string label,Transform part,Transform frame,Vector3 expected)
            { Label=label; Part=part; Frame=frame; Expected=expected; }
            /// <summary>Authored position of the part in its reference frame.</summary>
            public Vector3 Actual { get { return Frame.InverseTransformPoint(Part.position); } }
        }

        /// <summary>Config is authoritative for these. Positions are in the frame the
        /// simulation and runtime animation use: the machine, tank hinge or conditioner.</summary>
        public static List<MechanicalAnchor> MechanicalAnchors(IceWorld w,SimulatorConfig c)
        {
            var list=new List<MechanicalAnchor>();
            for(int side=0;side<2;side++)
            {
                float x=(side==0?-1:1)*c.WheelTrack*.5f;
                list.Add(new MechanicalAnchor("Front wheel pivot "+(side==0?"L":"R"),w.FrontWheels[side],w.Machine,new Vector3(x,.39f,c.Wheelbase)));
                list.Add(new MechanicalAnchor("Rear wheel hub "+(side==0?"L":"R"),w.RearWheels[side],w.Machine,new Vector3(x,.39f,0)));
            }
            list.Add(new MechanicalAnchor("Snow tank dump hinge",w.TankPivot,w.Machine,new Vector3(0,.78f,2.46f)));
            list.Add(new MechanicalAnchor("Tank shell (snow capacity)",w.BodyTank,w.TankPivot,new Vector3(c.TankLateralOffset,c.TankGeometryY(.51f),-.98f)));
            list.Add(new MechanicalAnchor("Conveyor discharge into tank",w.TankDischarge,w.Machine,new Vector3(c.ConveyorOutletX,c.ConveyorOutletY,c.ConveyorOutletZ)));
            // An IceBoardBrushRig owns the (cosmetic) pivot height; X/Z stay calibration-bound.
            var brushRig=w.BrushArm!=null?w.BrushArm.GetComponent<IceBoardBrushRig>():null;
            list.Add(new MechanicalAnchor("Board brush pivot",w.BrushArm,w.Machine,new Vector3(c.BrushPivotX,brushRig!=null?brushRig.pivotHeight:.38f,c.BrushPivotZ)));
            list.Add(new MechanicalAnchor("Board guide roller",w.GuideRoller,w.Machine,new Vector3(c.GuideRollerX,.53f,c.GuideRollerZ)));
            return list;
        }

        /// <summary>Operator-control mounts: authored freely. Only lever/valve rotation and
        /// plunger stroke are animated, always relative to the authored rest pose.</summary>
        public static List<KeyValuePair<string,Transform>> ControlMounts(IceWorld w)
        {
            return new List<KeyValuePair<string,Transform>>
            {
                new KeyValuePair<string,Transform>("Operator station deck",w.OperatorStation),
                new KeyValuePair<string,Transform>("Driver seat",w.Seat),
                new KeyValuePair<string,Transform>("Steering column",w.SteeringColumn),
                new KeyValuePair<string,Transform>("Dashboard",w.Dashboard),
                new KeyValuePair<string,Transform>("Pedal floor",w.Pedals),
                new KeyValuePair<string,Transform>("Hydraulic lever bank",w.ConditionerLever.parent),
                new KeyValuePair<string,Transform>("Water valve manifold",w.FloodValveMount.parent),
                new KeyValuePair<string,Transform>("Flood valve bearing",w.FloodValveMount),
                new KeyValuePair<string,Transform>("Wash valve bearing",w.WashValveMount),
                new KeyValuePair<string,Transform>("Blade handwheel assembly",w.BladeWheel.parent),
                new KeyValuePair<string,Transform>("Snow breaker assembly",w.SnowBreakerPlunger.parent),
            };
        }

        /// <summary>Runtime writes these pivots' rotation, position or scale every frame:
        /// edit their children, not the pivot itself.</summary>
        public static HashSet<Transform> RuntimeDrivenPivots(IceWorld w)
        {
            var set=new HashSet<Transform>{ w.Machine,w.Conditioner,w.TankDoor,w.GuideRoller,w.BrushArm,w.Brush,w.BrushLink,
                w.BrushRoller,w.CuttingBlade,w.HorizontalAuger,w.VerticalAuger };
            foreach(var group in new[]{w.TankCylinderBarrels,w.TankCylinderRods,w.ConditionerCylinderBarrels,w.ConditionerCylinderRods})
                foreach(var t in group)set.Add(t);
            return set;
        }

        /// <summary>Configuration that changes mesh shape, not just placement: changing it
        /// after the bake needs a rebake of the affected subassembly. (Brush arm length is not
        /// shape: the link and head are posed from it at runtime.)</summary>
        public static readonly string[] ShapeFields =
            { "TankRoofHeight","TankLateralOffset","ConveyorHeightOffset","ConditionerWidth","WaterDistributionWidth",
              "TireWidth","GuideRollerRadius","WaterValveStemHeight" };

        public List<string> ShapeFieldsChangedSinceBake(SimulatorConfig current)
        {
            var changed=new List<string>();
            if(string.IsNullOrEmpty(bakedConfigJson)||current==null)return changed;
            var baked=JsonUtility.FromJson<SimulatorConfig>(bakedConfigJson);
            foreach(string name in ShapeFields)
            {
                var field=typeof(SimulatorConfig).GetField(name);
                if(field!=null&&!Equals(field.GetValue(baked),field.GetValue(current)))changed.Add(name);
            }
            return changed;
        }

        /// <summary>Control mounts whose machine-space pose differs from the bake.</summary>
        public List<string> RearrangedControls(float positionTolerance=.0005f,float angleTolerance=.05f)
        {
            var moved=new List<string>();
            var machine=Get("Machine");
            if(machine==null)return moved;
            foreach(var pose in bakedMounts)
            {
                if(pose.mount==null){moved.Add(pose.label+" (removed)");continue;}
                Vector3 position=machine.InverseTransformPoint(pose.mount.position);
                Quaternion rotation=Quaternion.Inverse(machine.rotation)*pose.mount.rotation;
                if(Vector3.Distance(position,pose.position)>positionTolerance||Quaternion.Angle(rotation,pose.rotation)>angleTolerance)
                    moved.Add(pose.label);
            }
            return moved;
        }

#if UNITY_EDITOR
        /// <summary>Editor bake only: records part references and bake-time snapshots.</summary>
        public void RecordBake(IceWorld world,SimulatorConfig config)
        {
            parts.Clear(); partArrays.Clear(); bakedMounts.Clear();
            foreach(var field in MachineTransformFields())
                parts.Add(new Part { field=field.Name, target=(Transform)field.GetValue(world) });
            foreach(var field in MachineTransformArrayFields())
                partArrays.Add(new PartArray { field=field.Name, targets=(Transform[])((Transform[])field.GetValue(world)).Clone() });
            bakedConfigJson=JsonUtility.ToJson(config);
            foreach(var mount in ControlMounts(world))
                bakedMounts.Add(new MountPose { label=mount.Key, mount=mount.Value,
                    position=world.Machine.InverseTransformPoint(mount.Value.position),
                    rotation=Quaternion.Inverse(world.Machine.rotation)*mount.Value.rotation });
        }
#endif
    }
}
