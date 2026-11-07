// Routes Jump King's window handling through MacSupport.dll (JumpKingMac.MacWindow):
//  - WindowsManager..ctor: AllowUserResizing = true (resizable window, working green button)
//  - WindowsManager.SetFullscreen(ScreenMode): MacWindow.SetFullscreen(Game1.instance, Game1.graphics, mode)
//  - WindowsManager.OnWindowSizeChanged(): MacWindow.OnClientSizeChanged(Game1.instance, Game1.graphics)
// Usage: winpatch <input JumpKing.exe> <MacSupport.dll> <output JumpKing.exe>
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: winpatch <input exe> <MacSupport.dll> <output exe>");
    return 1;
}

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
foreach (var monoLib in new[] { "/opt/homebrew/lib/mono/4.5", "/opt/homebrew/lib/mono/4.5/Facades" })
{
    if (Directory.Exists(monoLib)) resolver.AddSearchDirectory(monoLib);
}

var module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });
var support = ModuleDefinition.ReadModule(args[1], new ReaderParameters { AssemblyResolver = resolver });
var macWindow = support.GetType("JumpKingMac.MacWindow");
var setFullscreen = module.ImportReference(macWindow.Methods.Single(m => m.Name == "SetFullscreen"));
var onClientSizeChanged = module.ImportReference(macWindow.Methods.Single(m => m.Name == "OnClientSizeChanged"));

var game1 = module.GetType("JumpKing.Game1");
var getInstance = game1.Properties.Single(p => p.Name == "instance").GetMethod;
var graphics = game1.Fields.Single(f => f.Name == "graphics");
var windows = module.GetType("JumpKing.Persocom.WindowsManager");

// 1. Allow resizing.
var ctor = windows.Methods.Single(m => m.IsConstructor && !m.IsStatic);
var setResizing = ctor.Body.Instructions.Single(i => i.Operand is MethodReference mr && mr.Name == "set_AllowUserResizing");
if (setResizing.Previous.OpCode != OpCodes.Ldc_I4_0) throw new Exception("unexpected AllowUserResizing pattern");
setResizing.Previous.OpCode = OpCodes.Ldc_I4_1;

// 2 + 3. Replace method bodies.
void Redirect(MethodDefinition method, MethodReference target, bool passMode)
{
    var body = method.Body;
    body.Instructions.Clear();
    body.ExceptionHandlers.Clear();
    body.Variables.Clear();
    var il = body.GetILProcessor();
    il.Emit(OpCodes.Call, getInstance);
    il.Emit(OpCodes.Ldsfld, graphics);
    if (passMode) il.Emit(OpCodes.Ldarg_1);
    il.Emit(OpCodes.Call, target);
    il.Emit(OpCodes.Ret);
}

Redirect(windows.Methods.Single(m => m.Name == "SetFullscreen"), setFullscreen, passMode: true);
Redirect(windows.Methods.Single(m => m.Name == "OnWindowSizeChanged"), onClientSizeChanged, passMode: false);

// 4. Settings live at Content.RootDirectory + "/settings/", and RootDirectory is empty, so on macOS
//    they'd go to /settings/ at the disk root (not writable). Keep them next to the saves instead.
var basePath = module.GetType("JumpKing.PlayerPreferences.ISettingManager`2").Methods.Single(m => m.Name == "GetBasePath");
var settingsLiteral = basePath.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "/settings/");
settingsLiteral.Operand = "Content/settings/";

module.Write(args[2]);
Console.WriteLine("patched: resizable window, SetFullscreen, OnWindowSizeChanged, settings path");
return 0;
