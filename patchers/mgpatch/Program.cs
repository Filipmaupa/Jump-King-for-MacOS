// Mac patches for MonoGame.Framework.dll (safe to run again on an already patched copy):
//  - Renames MonoGame's built-in "MonoGame.bmp" window icon so SdlGameWindow finds no icon and never
//    calls SDL_SetWindowIcon, which on macOS would replace the app's Dock icon with the MonoGame logo.
//  - ContentManager.OpenStream turns '\' into '/' first, so mods that build Windows-style content
//    paths (e.g. modDir + "\\Content\\leaderboard_header") find their files.
// Usage: mgpatch <input MonoGame.Framework.dll> <output MonoGame.Framework.dll>
using Mono.Cecil;
using Mono.Cecil.Cil;

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
foreach (var monoLib in new[] { "/opt/homebrew/lib/mono/4.5", "/opt/homebrew/lib/mono/4.5/Facades" })
{
    if (Directory.Exists(monoLib)) resolver.AddSearchDirectory(monoLib);
}

var module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });

var icon = module.Resources.SingleOrDefault(r => r.Name == "MonoGame.bmp");
if (icon != null)
{
    icon.Name = "MonoGame.unused.bmp";
    Console.WriteLine("renamed MonoGame.bmp resource");
}

var openStream = module.GetType("Microsoft.Xna.Framework.Content.ContentManager").Methods
    .Single(m => m.Name == "OpenStream" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String");
var body = openStream.Body;
var replace = module.ImportReference(module.TypeSystem.String.Resolve().Methods
    .Single(m => m.Name == "Replace" && m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "System.Char"));
bool patched = body.Instructions.Count > 3 && body.Instructions[3].Operand is MethodReference r && r.Name == "Replace";
if (!patched)
{
    // assetName = assetName.Replace('\\', '/');   (before the try block, which keeps its first instruction)
    var il = body.GetILProcessor();
    var first = body.Instructions[0];
    il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
    il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, (int)'\\'));
    il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, (int)'/'));
    il.InsertBefore(first, il.Create(OpCodes.Callvirt, replace));
    il.InsertBefore(first, il.Create(OpCodes.Starg_S, openStream.Parameters[0]));
    Console.WriteLine("ContentManager.OpenStream now accepts backslash paths");
}

module.Write(args[1]);
