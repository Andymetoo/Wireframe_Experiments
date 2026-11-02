using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Root of the authored (saved, editable) arena. At Play the builder binds these parts
    /// instead of generating a second rink. Physical dimensions stay owned by SimulatorConfig
    /// and ServiceLayout; IceArenaDrift reports any authored geometry that disagrees.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceArenaRig : MonoBehaviour
    {
        [Tooltip("Combine static parts by material on the Play instance (assets are never modified).")]
        public bool batchAtRuntime = true;
        [SerializeField, HideInInspector] string bakedConfigJson;

        /// <summary>Calibration fields that define the physical rink and service bay.</summary>
        public static readonly string[] PhysicalFields = { "RinkWidth", "RinkLength", "CornerRadius", "GateHalfWidth", "BayLength" };

        public string BakedConfigJson { get { return bakedConfigJson; } }
        /// <summary>Play Mode: the geometry report taken when the arena was bound.</summary>
        public List<IceArenaDrift.Finding> BoundFindings { get; private set; }

        public static bool IsPhysical(IceArenaRole role)
        {
            return role != IceArenaRole.None && role != IceArenaRole.ArenaLight && role != IceArenaRole.StationLevels;
        }

        /// <summary>Parts with a role, in hierarchy order (inactive included).</summary>
        public List<IceArenaPart> Parts(IceArenaRole role)
        {
            var list = new List<IceArenaPart>();
            foreach (var part in GetComponentsInChildren<IceArenaPart>(true)) if (part.role == role) list.Add(part);
            return list;
        }
        public List<IceArenaPart> Sections()
        {
            var list = new List<IceArenaPart>();
            foreach (var part in GetComponentsInChildren<IceArenaPart>(true)) if (part.sectionRoot) list.Add(part);
            return list;
        }
        public Transform Single(IceArenaRole role)
        {
            var parts = Parts(role);
            if (parts.Count != 1)
                throw new InvalidOperationException("Authored arena needs exactly one part marked " + role + " (found " + parts.Count +
                    "). Restore it, or add an IceArenaPart with that role to its replacement.");
            return parts[0].transform;
        }

        /// <summary>The authored part for a role under the game, or null without an authored arena
        /// or without that role (the caller then keeps its generated fallback).</summary>
        public static Transform FindPart(Component scope, IceArenaRole role)
        {
            var rig = scope != null ? scope.GetComponentInChildren<IceArenaRig>(true) : null;
            if (rig == null) return null;
            var parts = rig.Parts(role);
            if (parts.Count > 1) throw new InvalidOperationException("Authored arena has " + parts.Count + " parts marked " + role + "; keep one.");
            return parts.Count == 1 ? parts[0].transform : null;
        }

        /// <summary>Fills the arena half of the IceWorld contract from saved parts. Adds nothing to
        /// world.Meshes or world.Materials: saved assets are never runtime-owned.</summary>
        internal void Bind(IceWorld world, SimulatorConfig config)
        {
            var ice = Single(IceArenaRole.IceSheet).GetComponent<MeshRenderer>();
            if (ice == null) throw new InvalidOperationException("The authored ice sheet needs its MeshRenderer.");
            world.IceMesh = ice;
            world.PitSnow = Single(IceArenaRole.PitSnow);
            if (world.PitSnow.GetComponent<MeshFilter>() == null || world.PitSnow.GetComponent<MeshRenderer>() == null)
                throw new InvalidOperationException("The authored pit snow surface needs a MeshFilter and MeshRenderer.");
            var lights = new List<Light>();
            foreach (var part in Parts(IceArenaRole.ArenaLight)) { var light = part.GetComponent<Light>(); if (light != null) lights.Add(light); }
            world.ArenaLights = lights.ToArray();
            // Edit Mode labels use the font's own material; Play uses the depth-tested runtime one.
            foreach (var text in GetComponentsInChildren<TextMesh>(true))
            {
                var renderer = text.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = world.Lettering;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            // Measured before runtime batching merges the individual renderers.
            BoundFindings = IceArenaDrift.Evaluate(this, config);
            var problems = new List<string>();
            foreach (var finding in BoundFindings) if (finding.Drifted) problems.Add(finding.Label + ": " + finding.Detail);
            foreach (string field in PhysicalFieldsChangedSinceBake(config)) problems.Add(field + " changed since the arena was generated");
            if (problems.Count > 0)
                Debug.LogWarning("Authored arena geometry disagrees with the simulated rink (physics uses SimulatorConfig/ServiceLayout):\n  " +
                    string.Join("\n  ", problems), this);
        }

        /// <summary>Physical calibration fields that differ from the arena's generated shapes.</summary>
        public List<string> PhysicalFieldsChangedSinceBake(SimulatorConfig config)
        {
            var changed = new List<string>();
            if (string.IsNullOrEmpty(bakedConfigJson) || config == null) return changed;
            var baked = JsonUtility.FromJson<SimulatorConfig>(bakedConfigJson);
            foreach (string name in PhysicalFields)
            {
                var field = typeof(SimulatorConfig).GetField(name);
                if (!Equals(field.GetValue(baked), field.GetValue(config))) changed.Add(name);
            }
            return changed;
        }

#if UNITY_EDITOR
        /// <summary>Editor bake only (parts record their own pose before their section is saved).</summary>
        public void RecordBake(SimulatorConfig config) { bakedConfigJson = JsonUtility.ToJson(config); }
#endif
    }
}
