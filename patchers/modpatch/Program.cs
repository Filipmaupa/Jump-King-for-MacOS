// Routes every Assembly.LoadFrom(string) in JumpKing.Mods.ModLoader through JumpKingMac.MacMods.LoadFrom,
// which swaps a mod's old 0Harmony.dll for the Apple Silicon-capable copy bundled with the game.
// Usage: modpatch <input JumpKing.exe> <MacSupport.dll> <output JumpKing.exe>
using Mono.Cecil;
using Mono.Cecil.Cil;

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
foreach (var monoLib in new[] { "/opt/homebrew/lib/mono/4.5", "/opt/homebrew/lib/mono/4.5/Facades" })
{
    if (Directory.Exists(monoLib)) resolver.AddSearchDirectory(monoLib);
}

var module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });
var support = ModuleDefinition.ReadModule(args[1], new ReaderParameters { AssemblyResolver = resolver });
var macLoadFrom = module.ImportReference(support.GetType("JumpKingMac.MacMods").Methods.Single(m => m.Name == "LoadFrom"));

int count = 0;
foreach (var method in module.GetType("JumpKing.Mods.ModLoader").Methods.Where(m => m.HasBody))
{
    foreach (var ins in method.Body.Instructions)
    {
        if (ins.OpCode == OpCodes.Call && ins.Operand is MethodReference mr
            && mr.Name == "LoadFrom" && mr.DeclaringType.FullName == "System.Reflection.Assembly"
            && mr.Parameters.Count == 1 && mr.Parameters[0].ParameterType.FullName == "System.String")
        {
            ins.Operand = macLoadFrom;
            count++;
        }
    }
}
if (count == 0) throw new Exception("no Assembly.LoadFrom calls found in ModLoader");

module.Write(args[2]);
Console.WriteLine($"redirected {count} Assembly.LoadFrom call(s) in ModLoader");
