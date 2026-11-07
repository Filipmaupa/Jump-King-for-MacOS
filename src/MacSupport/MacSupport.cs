// macOS window handling for the Jump King port, called from patched JumpKing.Persocom.WindowsManager.
//  - Windowed: a normal, movable, resizable window (the green button enters native fullscreen).
//  - Fullscreen / Borderless: native macOS fullscreen in its own Space (SDL desktop fullscreen,
//    no display mode switch) instead of exclusive fullscreen or a screen-sized borderless window.
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace JumpKingMac
{
    public static class MacMods
    {
        // Called by the patched JumpKing.Mods.ModLoader instead of Assembly.LoadFrom.
        // Harmony before 2.3 can't patch code on Apple Silicon (it can't make JIT-compiled code
        // writable), and mods ship their own 0Harmony.dll, so give them the newer copy bundled
        // with the game when theirs is older.
        public static Assembly LoadFrom(string path)
        {
            if (string.Equals(Path.GetFileName(path), "0Harmony.dll", StringComparison.OrdinalIgnoreCase))
            {
                var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "0Harmony.dll");
                if (File.Exists(path) && File.Exists(bundled)
                    && AssemblyName.GetAssemblyName(path).Version < AssemblyName.GetAssemblyName(bundled).Version)
                {
                    return Assembly.LoadFrom(bundled);
                }
            }
            return Assembly.LoadFrom(path);
        }
    }

    public static class MacProcess
    {
        [DllImport("libc", EntryPoint = "_exit")]
        private static extern void _exit(int status);

        // Called from the patched entry point after JumpKing.Program.Run returns, by which time the game
        // has saved and stopped its threads. Mono's own shutdown would then wait ~40 s on a stuck
        // finalizer thread before the process ends, so end it here.
        public static void ExitNow()
        {
            Console.Out.Flush();
            Console.Error.Flush();
            _exit(0);
        }
    }

    // OpenAL Soft (MonoGame's audio) opens whatever output is the Mac's default when the game starts and
    // stays on it. Headphones plugged in or AirPods connecting later leave the game on the speakers, where
    // the mute and volume keys (which act on the new default) don't reach it. This watches the default
    // output and moves the game's audio to it whenever it changes (ALC_SOFT_reopen_device).
    public static class MacAudio
    {
        private const uint SystemObject = 1;                  // kAudioObjectSystemObject
        private const uint DefaultOutputDevice = 0x644F7574;  // 'dOut'
        private const uint GlobalScope = 0x676C6F62;          // 'glob'

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyAddress
        {
            public uint Selector, Scope, Element;
        }

        [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
        private static extern int AudioObjectGetPropertyData(uint objectId, ref PropertyAddress address, uint qualifierSize,
            IntPtr qualifier, ref uint size, out uint data);

        [DllImport("libc")] private static extern IntPtr dlopen(string path, int mode);
        [DllImport("libc")] private static extern IntPtr dlsym(IntPtr handle, string name);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetCurrentContext();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetContextsDevice(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetProcAddress(IntPtr device, string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte ReopenDevice(IntPtr device, IntPtr name, IntPtr attributes);

        private static bool s_started;

        public static void Start()
        {
            if (s_started) return;
            s_started = true;
            new System.Threading.Thread(Watch) { IsBackground = true, Name = "mac-audio" }.Start();
        }

        private static uint DefaultOutput()
        {
            var address = new PropertyAddress { Selector = DefaultOutputDevice, Scope = GlobalScope, Element = 0 };
            uint size = 4, device;
            return AudioObjectGetPropertyData(SystemObject, ref address, 0, IntPtr.Zero, ref size, out device) == 0 ? device : 0;
        }

        private static void Watch()
        {
            try
            {
                // the game's own copy, already loaded (dlopen hands back the same library)
                var lib = dlopen(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libopenal.1.dylib"), 2 /* RTLD_NOW */);
                if (lib == IntPtr.Zero) return;
                var currentContext = Fn<GetCurrentContext>(dlsym(lib, "alcGetCurrentContext"));
                var contextsDevice = Fn<GetContextsDevice>(dlsym(lib, "alcGetContextsDevice"));
                var procAddress = Fn<GetProcAddress>(dlsym(lib, "alcGetProcAddress"));
                if (currentContext == null || contextsDevice == null || procAddress == null) return;
                bool test = Environment.GetEnvironmentVariable("JK_AUDIO_REOPEN_TEST") == "1";

                uint last = DefaultOutput();
                int ticks = 0;
                while (true)
                {
                    System.Threading.Thread.Sleep(500);
                    uint now = DefaultOutput();
                    bool forced = test && ++ticks == 10;   // test hook: reopen once, ~5 s in
                    if (now == 0 || (now == last && !forced)) continue;
                    var context = currentContext();
                    var device = context == IntPtr.Zero ? IntPtr.Zero : contextsDevice(context);
                    var reopen = device == IntPtr.Zero ? null : Fn<ReopenDevice>(procAddress(device, "alcReopenDeviceSOFT"));
                    if (reopen == null) continue;          // no audio yet: try again on the next check
                    bool ok = reopen(device, IntPtr.Zero, IntPtr.Zero) != 0;
                    Console.WriteLine("[mac] default output " + last + " -> " + now + (forced ? " (test)" : "") + ": game audio " + (ok ? "moved" : "could not move"));
                    last = now;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[mac] audio watcher stopped: " + e.Message);
            }
        }

        private static T Fn<T>(IntPtr pointer) where T : class
        {
            return pointer == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer(pointer, typeof(T)) as T;
        }
    }

    public static class MacWindow
    {
        // Matches JumpKing.PlayerPreferences.Persocom.ScreenMode.
        private const int Windowed = 1;

        public static Point SetFullscreen(Game game, GraphicsDeviceManager graphics, int mode)
        {
            MacAudio.Start();   // the window is set up once at startup, after the audio is
            graphics.HardwareModeSwitch = false;
            game.Window.IsBorderless = false;

            if (mode == Windowed)
            {
                graphics.IsFullScreen = false;
                graphics.ApplyChanges();
                return new Point(graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight);
            }

            var display = game.GraphicsDevice.Adapter.CurrentDisplayMode;
            graphics.PreferredBackBufferWidth = display.Width;
            graphics.PreferredBackBufferHeight = display.Height;
            graphics.IsFullScreen = true;
            graphics.ApplyChanges();
            return new Point(display.Width, display.Height);
        }

        // The window was resized by the user, the green button or a fullscreen transition:
        // match the back buffer so the game letterboxes into the new size.
        public static void OnClientSizeChanged(Game game, GraphicsDeviceManager graphics)
        {
            var bounds = game.Window.ClientBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            if (bounds.Width == graphics.PreferredBackBufferWidth && bounds.Height == graphics.PreferredBackBufferHeight) return;

            graphics.PreferredBackBufferWidth = bounds.Width;
            graphics.PreferredBackBufferHeight = bounds.Height;
            graphics.ApplyChanges();
        }
    }
}
