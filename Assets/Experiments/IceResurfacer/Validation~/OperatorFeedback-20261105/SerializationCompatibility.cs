// Validation copy only: adapt the field schema consumed by the old packaged scene.
// Current runtime implementations, and every UI method/style, remain unchanged.
using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
class SerializationCompatibility
{
    static bool Serialized(FieldDefinition f) => !f.IsStatic && !f.IsInitOnly && !f.IsNotSerialized &&
        (f.IsPublic || f.CustomAttributes.Any(a=>a.AttributeType.FullName=="UnityEngine.SerializeField"));
    static void Main(string[] args)
    {
        var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
        var parameters=new ReaderParameters{AssemblyResolver=resolver,InMemory=true};
        using var original=AssemblyDefinition.ReadAssembly(args[0],parameters);
        using var updated=AssemblyDefinition.ReadAssembly(args[1],parameters);
        var lines=new System.Collections.Generic.List<string>();
        foreach(var type in updated.MainModule.Types)
        {
            if(type.FullName!="IceResurfacer.Core.SimulatorConfig")continue;
            var old=original.MainModule.GetType(type.FullName);
            if(old==null)continue;
            var originalFields=old.Fields.Where(Serialized).ToArray();
            var currentFields=type.Fields.Where(Serialized).ToArray();
            foreach(var f in currentFields)
            {
                var previous=originalFields.FirstOrDefault(x=>x.Name==f.Name);
                if(previous==null)
                {f.IsNotSerialized=true;lines.Add("Staged only: ignore newly serialized "+f.FullName);}
                else if(previous.FieldType.FullName!=f.FieldType.FullName)
                    throw new Exception("Changed serialized field type: "+f.FullName);
            }
            foreach(var f in originalFields)
            {
                if(currentFields.Any(x=>x.Name==f.Name))continue;
                throw new Exception("Removed serialized field: "+f.FullName);
            }
            // Unity reads fields in metadata order when the player scene omits type trees.
            var ordered=type.Fields.OrderBy(f=>{
                int i=Array.FindIndex(originalFields,x=>x.Name==f.Name);
                return i<0?int.MaxValue:i;
            }).ToArray();
            type.Fields.Clear();foreach(var f in ordered)type.Fields.Add(f);
        }
        string temp=args[1]+".compat";updated.Write(temp);File.Move(temp,args[1],true);
        File.WriteAllLines(args[2],lines);
        Console.WriteLine("Staged serialization schema compatibility: "+lines.Count+" new fields excluded from the old scene stream.");
    }
}
