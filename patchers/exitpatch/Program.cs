// Ends the process as soon as JumpKing.Program.Run returns (game saved, threads stopped):
// inserts JumpKingMac.MacProcess.ExitNow() before every return in SteamJK.Program.Main.
// Usage: exitpatch <input JumpKing.exe> <MacSupport.dll> <output JumpKing.exe>
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
var exitNow = module.ImportReference(support.GetType("JumpKingMac.MacProcess").Methods.Single(m => m.Name == "ExitNow"));

var main = module.EntryPoint;
if (main.Body.Instructions.Any(i => i.Operand is MethodReference mr && mr.Name == "ExitNow"))
    throw new Exception("already patched");

var il = main.Body.GetILProcessor();
foreach (var ret in main.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList())
{
    var call = il.Create(OpCodes.Call, exitNow);
    il.InsertBefore(ret, call);
    // Keep branches that targeted the ret going through the new call.
    foreach (var ins in main.Body.Instructions)
    {
        if (ins.Operand == ret && ins != call) ins.Operand = call;
    }
}

module.Write(args[2]);
Console.WriteLine($"patched {main.DeclaringType.FullName}.{main.Name}");
