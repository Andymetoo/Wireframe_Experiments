using System;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Menu metadata and explicit file operations over the existing route formats.</summary>
    public sealed class RouteLibraryCatalog
    {
        [Serializable] sealed class Alias{public string file,name;}
        [Serializable] sealed class Names{public List<Alias> names=new List<Alias>();}
        [Serializable] sealed class Envelope{public int version;public string sha256;public Names data;}
        static string NamesPath(string directory)=>Path.Combine(directory,"RouteNames.json");
        static Names ReadNames(string directory)
        {
            string path=NamesPath(directory);if(!File.Exists(path))return new Names();
            try{string json=File.ReadAllText(path);var e=JsonUtility.FromJson<Envelope>(json);if(e.version==1&&e.sha256==RouteRecordingStorage.Integrity(RouteRecordingStorage.ExactObject(json,"data")))return e.data;}
            catch(Exception){ }return new Names();
        }
        public static string DisplayName(string path,string fallback)
        {var item=ReadNames(Path.GetDirectoryName(path)).names.Find(n=>n.file==Path.GetFileName(path));return item==null?fallback:item.name;}
        public static void Rename(Entry entry,string name)
        {
            if(string.IsNullOrWhiteSpace(name)||name.Length>80)throw new InvalidOperationException("Enter a route name of 1–80 characters.");
            var names=ReadNames(Path.GetDirectoryName(entry.path));names.names.RemoveAll(n=>n.file==Path.GetFileName(entry.path));names.names.Add(new Alias{file=Path.GetFileName(entry.path),name=name.Trim()});
            string payload=JsonUtility.ToJson(names,true);string json="{\n\"version\":1,\n\"sha256\":\""+RouteRecordingStorage.Integrity(payload)+"\",\n\"data\":"+payload+"\n}";
            RouteRecordingStorage.Publish(NamesPath(Path.GetDirectoryName(entry.path)),json,true);
        }
        public sealed class Entry
        {
            public string path,name,date,error;public RecordedRouteData raw;public TrainingRouteDefinition guide;
            public double Length=>raw==null?0:raw.distanceMeters;
            public bool Valid=>raw!=null&&error==null;
        }
        public readonly List<Entry> entries=new List<Entry>();
        public void Refresh(RouteRecordingStorage library,string guides)
        {
            entries.Clear();var paths=new List<string>();
            if(Directory.Exists(library.DirectoryPath))paths.AddRange(Directory.GetFiles(library.DirectoryPath,"*.route.json"));
            if(Directory.Exists(guides))paths.AddRange(Directory.GetFiles(guides,"*.guide.json"));
            paths.Sort((a,b)=>File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            foreach(string path in paths)
            {
                var e=new Entry{path=path,name=Path.GetFileName(path),date=""};
                try
                {
                    if(path.EndsWith(".guide.json",StringComparison.OrdinalIgnoreCase)){e.guide=TrainingGuideStorage.Read(path);e.raw=TrainingGuideStorage.Raw(e.guide);e.name=e.guide.name;}
                    else{e.raw=RouteRecordingStorage.Read(path);e.name=e.raw.name;}
                    e.name=DisplayName(path,e.name);
                    if(DateTimeOffset.TryParse(e.raw.createdUtc,out var date))e.date=date.ToLocalTime().ToString("g");
                }
                catch(Exception ex){e.error=ex.Message;}
                entries.Add(e);
            }
        }
        public static string SaveCopy(Entry entry,RouteRecordingStorage library,string guides)
        {
            if(entry.guide!=null){var d=TrainingGuideStorage.Read(entry.path);d.name=entry.name;return TrainingGuideStorage.SaveCopy(guides,d);}
            var raw=RouteRecordingStorage.Read(entry.path);raw.name=entry.name;return library.SaveCopy(raw);
        }
        public static void DeleteConfirmed(Entry entry,RouteRecordingStorage library,string guides)
        {
            string path=Path.GetFullPath(entry.path),folder=Path.GetDirectoryName(path);
            if(!string.Equals(folder,Path.GetFullPath(library.DirectoryPath),StringComparison.OrdinalIgnoreCase)&&!string.Equals(folder,Path.GetFullPath(guides),StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a selected saved route in this library can be deleted.");
            File.Delete(path); // Called only by the explicit per-file confirmation button.
        }
    }
}
