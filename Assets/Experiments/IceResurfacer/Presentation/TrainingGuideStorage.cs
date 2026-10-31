using System;
using System.IO;
using System.Text;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public static class TrainingGuideStorage
    {
        [Serializable] sealed class Envelope {public string format=null,sha256=null;public TrainingRouteDefinition guide=null;public int version=0;}
        public static string DefaultDirectory => Path.Combine(RouteRecordingStorage.DefaultDirectory,"Guides");
        public static TrainingRouteDefinition Create(RecordedRouteData raw)
        {
            string json=RouteRecordingStorage.Encode(raw);
            var definition=TrainingRouteDefinition.FromRecording(raw,json,RouteRecordingStorage.Integrity(json));
            RecordedTrackReconstruction.Restore(definition,raw);definition.name=raw.name+" / recorded track";return definition;
        }
        public static string Encode(TrainingRouteDefinition definition)
        {
            definition.Validate();var raw=Raw(definition);
            if(raw.id!=definition.sourceRecordingId)throw new FormatException("Mismatched raw recording.");
            string payload=JsonUtility.ToJson(definition,true);
            return "{\n  \"format\": \"AuroraR54.TrainingGuide\",\n  \"version\": "+definition.version+",\n  \"sha256\": \""+RouteRecordingStorage.Integrity(payload)+"\",\n  \"guide\": "+payload+"\n}\n";
        }
        public static TrainingRouteDefinition Decode(string json)
        {
            if(string.IsNullOrEmpty(json)||Encoding.UTF8.GetByteCount(json)>RouteRecordingStorage.MaximumFileBytes)throw new FormatException("Missing or oversized training guide.");
            var e=JsonUtility.FromJson<Envelope>(json);
            if(e==null||e.format!="AuroraR54.TrainingGuide"||(e.version!=1&&e.version!=2)||e.guide==null||e.guide.version!=e.version||e.sha256!=RouteRecordingStorage.Integrity(RouteRecordingStorage.ExactObject(json,"guide")))throw new FormatException("Training guide version or checksum is invalid.");
            var d=e.guide;d.Validate();Raw(d);return d;
        }
        public static RecordedRouteData Raw(TrainingRouteDefinition d)
        {
            if(RouteRecordingStorage.Integrity(d.rawRecordingJson)!=d.sourceSha256)throw new FormatException("Embedded raw source was modified.");
            var raw=RouteRecordingStorage.Decode(d.rawRecordingJson);if(raw.id!=d.sourceRecordingId)throw new FormatException("Raw source ID mismatch.");return raw;
        }
        public static TrainingRouteDefinition Read(string path)
        {if(new FileInfo(path).Length>RouteRecordingStorage.MaximumFileBytes)throw new FormatException("Guide exceeds file limit.");return Decode(File.ReadAllText(path,Encoding.UTF8));}
        public static string SaveCopy(string directory,TrainingRouteDefinition d)
        {
            string label="";foreach(char c in d.name){label+=char.IsLetterOrDigit(c)||c=='-'?c:'_';if(label.Length>=28)break;}
            string name=label+"_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".guide.json";
            string path=Path.Combine(directory,name);RouteRecordingStorage.Publish(path,Encode(d));return path;
        }
        public static string[] List(string directory)
        {return Directory.Exists(directory)?Directory.GetFiles(directory,"*.guide.json",SearchOption.TopDirectoryOnly):Array.Empty<string>();}
        public static void Draft(string directory,TrainingRouteDefinition d)
        {RouteRecordingStorage.Publish(Path.Combine(directory,"Drafts",d.id+".guide.json"),Encode(d),true);}
    }
}
