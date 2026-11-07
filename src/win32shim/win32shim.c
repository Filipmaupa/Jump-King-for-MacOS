// Mac stand-ins for the few Win32 calls Jump King mods make (JK Runtime and the maps built on it).
// Mono maps user32.dll, winmm.dll and gdi32.dll here (Contents/etc/mono/config).
//  - GetAsyncKeyState: Windows virtual-key codes answered from SDL's keyboard and mouse state,
//    the same state MonoGame reads, so it only sees keys while the game window has focus.
//  - GetForegroundWindow: the SDL window with keyboard focus, which is what MonoGame's
//    Window.Handle is on DesktopGL, so "is the game in front" checks keep working.
//  - Timer resolution and cursor/icon calls: harmless no-ops.
#include <dlfcn.h>
#include <mach-o/dyld.h>
#include <stdint.h>
#include <string.h>

typedef const uint8_t *(*GetKeyboardStateFn)(int *);
typedef uint32_t (*GetMouseStateFn)(int *, int *);
typedef void *(*GetKeyboardFocusFn)(void);

static GetKeyboardStateFn sdl_keyboard_state;
static GetMouseStateFn sdl_mouse_state;
static GetKeyboardFocusFn sdl_keyboard_focus;

// SDL is already loaded by MonoGame; find that copy instead of loading another one.
static int load_sdl(void)
{
    if (sdl_keyboard_state) return 1;
    for (uint32_t i = 0; i < _dyld_image_count(); i++) {
        const char *name = _dyld_get_image_name(i);
        if (!name || !strstr(name, "libSDL2")) continue;
        void *lib = dlopen(name, RTLD_LAZY | RTLD_NOLOAD);
        if (!lib) continue;
        sdl_mouse_state = (GetMouseStateFn)dlsym(lib, "SDL_GetMouseState");
        sdl_keyboard_focus = (GetKeyboardFocusFn)dlsym(lib, "SDL_GetKeyboardFocus");
        GetKeyboardStateFn keyboard = (GetKeyboardStateFn)dlsym(lib, "SDL_GetKeyboardState");
        if (keyboard && sdl_mouse_state && sdl_keyboard_focus) {
            sdl_keyboard_state = keyboard;
            return 1;
        }
    }
    return 0;
}

// Windows virtual-key code -> up to two SDL scancodes (0 = none).
static void scancodes_for(int vk, int out[2])
{
    out[0] = out[1] = 0;
    if (vk >= 'A' && vk <= 'Z') { out[0] = 4 + (vk - 'A'); return; }
    if (vk >= '1' && vk <= '9') { out[0] = 30 + (vk - '1'); return; }
    if (vk == '0') { out[0] = 39; return; }
    if (vk >= 0x70 && vk <= 0x7B) { out[0] = 58 + (vk - 0x70); return; }   // F1-F12
    if (vk >= 0x7C && vk <= 0x87) { out[0] = 104 + (vk - 0x7C); return; }  // F13-F24
    if (vk >= 0x61 && vk <= 0x69) { out[0] = 89 + (vk - 0x61); return; }   // numpad 1-9
    switch (vk) {
    case 0x08: out[0] = 42; break;                  // backspace
    case 0x09: out[0] = 43; break;                  // tab
    case 0x0D: out[0] = 40; out[1] = 88; break;     // enter, keypad enter
    case 0x10: out[0] = 225; out[1] = 229; break;   // shift
    case 0x11: out[0] = 224; out[1] = 228; break;   // control
    case 0x12: out[0] = 226; out[1] = 230; break;   // alt (option)
    case 0x13: out[0] = 72; break;                  // pause
    case 0x14: out[0] = 57; break;                  // caps lock
    case 0x1B: out[0] = 41; break;                  // escape
    case 0x20: out[0] = 44; break;                  // space
    case 0x21: out[0] = 75; break;                  // page up
    case 0x22: out[0] = 78; break;                  // page down
    case 0x23: out[0] = 77; break;                  // end
    case 0x24: out[0] = 74; break;                  // home
    case 0x25: out[0] = 80; break;                  // left
    case 0x26: out[0] = 82; break;                  // up
    case 0x27: out[0] = 79; break;                  // right
    case 0x28: out[0] = 81; break;                  // down
    case 0x2C: out[0] = 70; break;                  // print screen
    case 0x2D: out[0] = 73; break;                  // insert
    case 0x2E: out[0] = 76; break;                  // delete
    case 0x5B: out[0] = 227; break;                 // left windows (command)
    case 0x5C: out[0] = 231; break;                 // right windows (command)
    case 0x5D: out[0] = 101; break;                 // menu
    case 0x60: out[0] = 98; break;                  // numpad 0
    case 0x6A: out[0] = 85; break;                  // numpad *
    case 0x6B: out[0] = 87; break;                  // numpad +
    case 0x6D: out[0] = 86; break;                  // numpad -
    case 0x6E: out[0] = 99; break;                  // numpad .
    case 0x6F: out[0] = 84; break;                  // numpad /
    case 0x90: out[0] = 83; break;                  // num lock
    case 0x91: out[0] = 71; break;                  // scroll lock
    case 0xA0: out[0] = 225; break;                 // left shift
    case 0xA1: out[0] = 229; break;                 // right shift
    case 0xA2: out[0] = 224; break;                 // left control
    case 0xA3: out[0] = 228; break;                 // right control
    case 0xA4: out[0] = 226; break;                 // left alt
    case 0xA5: out[0] = 230; break;                 // right alt
    case 0xBA: out[0] = 51; break;                  // ;
    case 0xBB: out[0] = 46; break;                  // =
    case 0xBC: out[0] = 54; break;                  // ,
    case 0xBD: out[0] = 45; break;                  // -
    case 0xBE: out[0] = 55; break;                  // .
    case 0xBF: out[0] = 56; break;                  // /
    case 0xC0: out[0] = 53; break;                  // `
    case 0xDB: out[0] = 47; break;                  // [
    case 0xDC: out[0] = 49; break;                  // backslash
    case 0xDD: out[0] = 48; break;                  // ]
    case 0xDE: out[0] = 52; break;                  // '
    case 0xE2: out[0] = 100; break;                 // non-US backslash
    }
}

// SDL button masks: left 1, middle 2, right 4, X1 8, X2 16.
static uint32_t mouse_mask_for(int vk)
{
    switch (vk) {
    case 0x01: return 1u << 0;
    case 0x02: return 1u << 2;
    case 0x04: return 1u << 1;
    case 0x05: return 1u << 3;
    case 0x06: return 1u << 4;
    }
    return 0;
}

short GetAsyncKeyState(int vk)
{
    if (!load_sdl()) return 0;
    uint32_t mask = mouse_mask_for(vk);
    if (mask) return (sdl_mouse_state(NULL, NULL) & mask) ? (short)0x8000 : 0;

    int count = 0;
    const uint8_t *keys = sdl_keyboard_state(&count);
    int codes[2];
    scancodes_for(vk, codes);
    for (int i = 0; i < 2; i++) {
        if (codes[i] > 0 && codes[i] < count && keys[codes[i]]) return (short)0x8000;
    }
    return 0;
}

void *GetForegroundWindow(void)
{
    return load_sdl() ? sdl_keyboard_focus() : NULL;
}

unsigned int timeBeginPeriod(unsigned int ms) { (void)ms; return 0; }
unsigned int timeEndPeriod(unsigned int ms) { (void)ms; return 0; }

int GetIconInfo(void *icon, void *info) { (void)icon; (void)info; return 0; }
void *CreateIconIndirect(void *info) { (void)info; return NULL; }
int DestroyIcon(void *icon) { (void)icon; return 1; }
int DestroyCursor(void *cursor) { (void)cursor; return 1; }
int DeleteObject(void *object) { (void)object; return 1; }
