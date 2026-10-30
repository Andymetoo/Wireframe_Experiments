using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Portable, checksummed JSON; independent of Play Mode object/scene persistence.</summary>
    public sealed class RouteRecordingStorage
    {
        [Serializable]
        sealed class FileEnvelope
        {
            public string format;
            public int version;
            public string sha256;
            public RecordedRouteData route;
        }
        public const string Format="AuroraR54.RecordedRoute";
        public const long MaximumFileBytes=64L*1024*1024;
        public string DirectoryPath {get;}
        public static string DefaultDirectory
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath,"Experiments/IceResurfacer/RouteRecordings~"));
#else
                return Path.Combine(Application.persistentDataPath,"IceResurfacer","Routes");
#endif
            }
        }
        public RouteRecordingStorage(string directory) {DirectoryPath=Path.GetFullPath(directory);}
        static string Hash(string text)
        {
            using(var sha=SHA256.Create())
            {var bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(text));var value=new StringBuilder(64);foreach(byte b in bytes)value.Append(b.ToString("x2"));return value.ToString();}
        }
        public static string Encode(RecordedRouteData route)
        {
            route.Validate();
            string semantic=JsonUtility.ToJson(route);
            string payload=route.sourceJson!=null&&route.parsedJsonFingerprint==semantic?route.sourceJson:JsonUtility.ToJson(route,true);
            // Hash the exact stored payload, not a reserialization of parsed floats.
            // This also keeps integrity independent of future configuration fields.
            return "{\n  \"format\": \""+Format+"\",\n  \"version\": 1,\n  \"sha256\": \""+Hash(payload)+"\",\n  \"route\": "+payload+"\n}\n";
        }
        public static RecordedRouteData Decode(string json)
        {
            if(string.IsNullOrWhiteSpace(json)||Encoding.UTF8.GetByteCount(json)>MaximumFileBytes)throw new FormatException("Empty or oversized route file.");
            FileEnvelope envelope;
            try {envelope=JsonUtility.FromJson<FileEnvelope>(json);}
            catch(Exception e){throw new FormatException("Invalid route JSON.",e);}
            if(envelope==null||envelope.format!=Format||envelope.version!=1||envelope.route==null||
                !string.Equals(envelope.sha256,Hash(ExtractPayload(json)),StringComparison.Ordinal))
                throw new FormatException("Route format/version or SHA-256 integrity check failed. The current route has been retained.");
            envelope.route.Validate();
            envelope.route.sourceJson=ExtractPayload(json);envelope.route.parsedJsonFingerprint=JsonUtility.ToJson(envelope.route);
            return envelope.route;
        }
        static string ExtractPayload(string json)
        {
            int depth=0,keyStart=0,count=0;bool quoted=false,escaped=false;string payload=null;
            for(int i=0;i<json.Length;i++)
            {
                char c=json[i];
                if(quoted)
                {
                    if(escaped){escaped=false;continue;}if(c=='\\'){escaped=true;continue;}
                    if(c!='\"')continue;quoted=false;
                    if(depth!=1||json.Substring(keyStart,i-keyStart+1)!="\"route\"")continue;
                    int start=i+1;while(start<json.Length&&char.IsWhiteSpace(json[start]))start++;
                    if(start>=json.Length||json[start++]!=':')continue;
                    while(start<json.Length&&char.IsWhiteSpace(json[start]))start++;
                    if(start>=json.Length||json[start]!='{')throw new FormatException("Route payload must be an object.");
                    int nesting=0,end=start;bool text=false,escape=false;
                    for(;end<json.Length;end++)
                    {
                        char value=json[end];
                        if(text){if(escape)escape=false;else if(value=='\\')escape=true;else if(value=='\"')text=false;continue;}
                        if(value=='\"')text=true;else if(value=='{')nesting++;else if(value=='}'&&--nesting==0)break;
                    }
                    if(end>=json.Length)throw new FormatException("Truncated route payload.");
                    payload=json.Substring(start,end-start+1);count++;i=end;
                }
                else if(c=='\"'){quoted=true;keyStart=i;}
                else if(c=='{')depth++;else if(c=='}')depth--;
            }
            if(count!=1)throw new FormatException("Route file must contain exactly one raw payload.");return payload;
        }
        public static RecordedRouteData Read(string path)
        {
            var info=new FileInfo(path);
            if(info.Length>MaximumFileBytes)throw new FormatException("Route file exceeds 64 MB.");
            return Decode(File.ReadAllText(path,Encoding.UTF8));
        }
        public string SaveCopy(RecordedRouteData data)
        {
            if(!data.complete)throw new InvalidOperationException("Stop recording before saving a named copy.");
            string label=SafeName(data.name);
            string path=Path.Combine(DirectoryPath,label+"_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".route.json");
            AtomicWrite(path,Encode(data),false);return path;
        }
        public static void Export(string path,RecordedRouteData data,bool replace=false)
        {
            if(!data.complete)throw new InvalidOperationException("Stop recording before exporting a copy.");
            AtomicWrite(Path.GetFullPath(path),Encode(data),replace);
        }
        public string SaveDraft(RecordedRouteData data)
        {
            // A separate recovery file per recording protects earlier unsaved takes.
            string path=Path.Combine(DirectoryPath,"Drafts",data.id+".route.json");
            AtomicWrite(path,Encode(data),true);return path;
        }
        public string[] ListFiles()
        {
            if(!Directory.Exists(DirectoryPath))return Array.Empty<string>();
            var paths=new System.Collections.Generic.List<string>(Directory.GetFiles(DirectoryPath,"*.route.json",SearchOption.TopDirectoryOnly));
            string drafts=Path.Combine(DirectoryPath,"Drafts");
            if(Directory.Exists(drafts))paths.AddRange(Directory.GetFiles(drafts,"*.route.json",SearchOption.TopDirectoryOnly));
            paths.Sort((a,b)=>File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));return paths.ToArray();
        }
        static string SafeName(string name)
        {
            var value=new StringBuilder();foreach(char c in name)
                if(char.IsLetterOrDigit(c)||c=='-'||c=='_')value.Append(c);else if(c==' ')value.Append('_');
            return value.Length==0?"Operator_route":value.ToString(0,Math.Min(48,value.Length));
        }
        static void AtomicWrite(string path,string content,bool replace)
        {
            byte[] bytes=new UTF8Encoding(false).GetBytes(content);
            if(bytes.LongLength>MaximumFileBytes)throw new IOException("Route exceeds the 64 MB file budget; the existing file has been retained.");
            string folder=Path.GetDirectoryName(path);Directory.CreateDirectory(folder);
            // A short sibling name also works in deeply nested Windows projects.
            string temp=Path.Combine(folder,".route-"+Guid.NewGuid().ToString("N").Substring(0,12)+".tmp");
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                // Only publish complete files. A failed replacement leaves the old draft intact.
                if(replace&&File.Exists(path))File.Replace(temp,path,path+".bak");
                else File.Move(temp,path);
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
