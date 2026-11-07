// Patches JumpKing.exe for macOS:
//  - Windows path separators in string literals ("\\") become "/".
// Usage: jkpatch <input JumpKing.exe> <output JumpKing.exe>
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: jkpatch <input> <output>");
    return 1;
}

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
foreach (var monoLib in new[] { "/opt/homebrew/lib/mono/4.5", "/opt/homebrew/lib/mono/4.5/Facades" })
{
    if (Directory.Exists(monoLib)) resolver.AddSearchDirectory(monoLib);
}

var module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });

int literals = 0;
foreach (var type in module.GetTypes())
{
    foreach (var method in type.Methods)
    {
        if (!method.HasBody) continue;
        foreach (var ins in method.Body.Instructions)
        {
            if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string s && s.Contains('\\'))
            {
                ins.Operand = s.Replace('\\', '/');
                literals++;
            }
        }
    }
}

module.Write(args[1]);
Console.WriteLine($"backslash literals rewritten: {literals}");
return 0;
