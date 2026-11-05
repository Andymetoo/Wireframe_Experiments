using System;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>Native Mesh regressions; no scene loads, asset saves or gameplay changes.</summary>
    public static class IceSnowMeshValidation
    {
        const string Out="Assets/Experiments/IceResurfacer/Validation~/MeshResize-20261104/";
        [MenuItem("Experiments/Ice Resurfacer/Validate Snow Mesh Rebuilds")]
        public static void RunInEditor()
        {
            try{RunChecks(Debug.Log);}
            catch(Exception e){Debug.LogException(e);}
        }
        [MenuItem("Experiments/Ice Resurfacer/Validate Snow Mesh Rebuilds",true)]
        static bool CanRun()=>!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling;
        public static void RunBatch()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("Use the isolated validation project.");
            Directory.CreateDirectory(Out);var lines=new List<string>();
            try{RunChecks(lines.Add);File.WriteAllLines(Out+"native-results.txt",lines);EditorApplication.Exit(0);}
            catch(Exception e){lines.Add("FAIL "+e);File.WriteAllLines(Out+"native-results.txt",lines);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void RunChecks(Action<string> log)
        {
            int errors=0;string firstError=null;
            Application.LogCallback capture=(message,stack,type)=>
            {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;if(firstError==null)firstError=message;}};
            Application.logMessageReceived+=capture;
            try
            {
                ResizeSequence(log);IncrementalWetPickup(log);
                Require(errors==0,"Native mesh errors: "+errors+"; "+firstError);
                log("PASS snow mesh shrink/grow/empty/regrowth and incremental wet recollection; zero native errors");
            }
            finally{Application.logMessageReceived-=capture;}
        }
        static void Require(bool ok,string message)=>OperatorMaterialValidation.Require(ok,message);
        sealed class MeshState
        {
            readonly List<MeshFilter> filters=new List<MeshFilter>();
            readonly List<Vector3> normals=new List<Vector3>();
            readonly List<int> indices=new List<int>();
            readonly Dictionary<Mesh,int> previous=new Dictionary<Mesh,int>();
            public int PartialShrinks;
            public void Check(GameObject root,IceDepositedSnowView view)
            {
                root.GetComponentsInChildren(true,filters);int vertices=0;
                foreach(var filter in filters)
                {
                    var mesh=filter.sharedMesh;int count=mesh.vertexCount;
                    if(previous.TryGetValue(mesh,out int old)&&count>0&&count<old)PartialShrinks++;
                    previous[mesh]=count;vertices+=count;
                    if(count==0){Require(mesh.subMeshCount==0||mesh.GetIndexCount(0)==0,"empty chunk retains triangles");continue;}
                    mesh.GetNormals(normals);Require(normals.Count==count,"normal count differs from vertices");
                    mesh.GetIndices(indices,0);Require(indices.Count==count,"stale triangle count after resize");
                    foreach(int index in indices)Require(index>=0&&index<count,"triangle references a missing vertex");
                }
                Require(vertices==view.LastVertices,"uploaded vertex count differs from occupied snow cells");
            }
        }
        static void ResizeSequence(Action<string> log)
        {
            var field=new IceField(new SimulatorConfig(),546);
            var host=new GameObject("Snow mesh resize regression"){hideFlags=HideFlags.HideAndDontSave};
            var view=new IceDepositedSnowView(field,host.transform);var state=new MeshState();
            // One chunk, with partial occupancy changes and a final regrowth.
            int x0=field.Columns/2/40*40+4,z0=field.Rows/2/40*40+4;
            try
            {
                foreach(int side in new[]{10,6,12,4,2,0,1,10,2,0})
                {
                    double expectedVolume=0;
                    for(int z=0;z<12;z++)for(int x=0;x<12;x++)
                    {
                        int i=field.Index(x0+x,z0+z);var cell=field.Cells[i];
                        cell.SnowMm=x<side&&z<side?30:0;field.Cells[i]=cell;
                        expectedVolume+=cell.SnowMm*field.CellSize*field.CellSize*.001;
                        field.MarkCellChanged(i);view.MarkSnowChanged(field,i);
                    }
                    field.MarkChanged();for(int i=0;i<64;i++)view.Refresh(field,false);
                    state.Check(host,view);
                    Require(view.LastVertices==side*side*12,"partial chunk has the wrong number of facets");
                    OperatorMaterialValidation.Near(view.VisibleVolumeM3,expectedVolume,1e-8,"resize changed represented snow volume");
                    log("PASS occupied_cells="+(side*side)+" uploaded_vertices="+view.LastVertices);
                }
                Require(state.PartialShrinks>=4,"regression never shrank a nonempty mesh");
            }
            finally{view.Dispose();UnityEngine.Object.DestroyImmediate(host);}
        }
        static void IncrementalWetPickup(Action<string> log)
        {
            var s=new Simulation(new SimulatorConfig{LooseSnowPickupDepthMm=50});
            var apparatus=new OperatorMaterialValidation.Apparatus(s);
            var host=new GameObject("Snow wet recollection regression"){hideFlags=HideFlags.HideAndDontSave};
            var view=new IceDepositedSnowView(s.Ice,host.transform);var state=new MeshState();
            double initial=OperatorMaterialValidation.SnowStock(s);
            void Refresh()
            {
                for(int n=0;n<s.Ice.DirtyCellCount;n++)view.MarkSnowChanged(s.Ice,s.Ice.DirtyCellIndex(n));
                view.Refresh(s.Ice,false);state.Check(host,view);s.Ice.ClearDirtyCells();
            }
            try
            {
                s.Controls.BladeDepthMm=.35f;apparatus.Line(0,-24,24);Refresh();
                for(int i=0;i<64;i++)view.Refresh(s.Ice,false);
                state.Check(host,view);
                Require(view.ActiveChunks>0,"off-auger fixture produced no visible deposits");
                s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;s.Controls.FloodFlow=1;apparatus.Drain();
                foreach(float x in new[]{-1.265f,1.265f})
                    for(int n=0;n<=500;n++){apparatus.Sample(x,-25+n*.1f);Refresh();}
                apparatus.Drain();Refresh();
                Require(state.PartialShrinks>0,"wet recollection never partially cleared a chunk");
                Require(view.ActiveChunks==0,"collected banks retained visible mesh triangles");
                Require(s.SnowTankM3>0&&s.TotalFloodUsedLiters>0,"fixture did not convey snow while flooding");
                OperatorMaterialValidation.Near(OperatorMaterialValidation.SnowStock(s)-initial-s.TotalExpandedShavingsM3,0,.00005,"wet pickup snow balance");
                log("PASS incremental wet pickup: partial_shrinks="+state.PartialShrinks+" remaining_chunks=0 tank_m3="+s.SnowTankM3);
            }
            finally{view.Dispose();UnityEngine.Object.DestroyImmediate(host);}
        }
    }
}
