"""Builds an OpenGL (MonoGame DesktopGL 3.7, MGFX v8) version of Content/shaders/Mask.xnb.

The original DX11 effect is a single pixel shader:
    color = tex2D(SpriteTexture, (uv.x * tiling.x + wind, uv.y * tiling.y)) * tex2D(MaskTexture, uv).a
with params tiling (float2, cbuffer offset 0), wind (float, offset 8),
SpriteTexture (slot 0) and MaskTexture (slot 1); technique MaskDrawing / pass P0.

Usage: python3 -I make_mask_gl.py <original Mask.xnb> <output Mask.xnb>
"""
import struct
import sys

GLSL = b"""#ifdef GL_ES
precision mediump float;
precision mediump int;
#endif

uniform vec4 ps_uniforms_vec4[1];
uniform sampler2D ps_s0;
uniform sampler2D ps_s1;
varying vec4 vTexCoord0;

void main()
{
    vec4 c0 = ps_uniforms_vec4[0];
    vec2 uv = vTexCoord0.xy;
    vec4 sprite = texture2D(ps_s0, vec2(uv.x * c0.x + c0.z, uv.y * c0.y));
    gl_FragColor = sprite * texture2D(ps_s1, uv).w;
}
"""


def s7(n):
    out = bytearray()
    while True:
        b = n & 0x7F
        n >>= 7
        if n:
            out.append(b | 0x80)
        else:
            out.append(b)
            return bytes(out)


def string(s):
    b = s.encode("utf8")
    return s7(len(b)) + b


def u8(v):
    return struct.pack("<B", v)


def i16(v):
    return struct.pack("<h", v)


def i32(v):
    return struct.pack("<i", v)


def build_mgfx(effect_key):
    m = bytearray()
    m += b"MGFX" + u8(8) + u8(0)  # version 8, profile 0 = OpenGL
    m += effect_key

    # constant buffers
    m += u8(1)
    m += string("ps_uniforms_vec4") + i16(16)
    m += u8(2) + u8(0) + struct.pack("<H", 0) + u8(1) + struct.pack("<H", 8)

    # shaders
    m += u8(1)
    m += u8(0)  # pixel shader
    m += i32(len(GLSL)) + GLSL
    m += u8(2)  # samplers: type, texture slot, sampler slot, has state, name, parameter
    m += u8(0) + u8(0) + u8(0) + u8(0) + string("ps_s0") + u8(2)
    m += u8(0) + u8(1) + u8(1) + u8(0) + string("ps_s1") + u8(3)
    m += u8(1) + u8(0)  # cbuffers used: [0]
    m += u8(0)  # vertex attributes

    # parameters: class, type, name, semantic, annotations, rows, cols, elements, members, data
    m += u8(4)
    m += u8(1) + u8(3) + string("tiling") + string("") + u8(0) + u8(1) + u8(2) + u8(0) + u8(0) + b"\0" * 8
    m += u8(0) + u8(3) + string("wind") + string("") + u8(0) + u8(1) + u8(1) + u8(0) + u8(0) + b"\0" * 4
    m += u8(3) + u8(7) + string("SpriteTexture") + string("") + u8(0) + u8(0) + u8(0) + u8(0) + u8(0)
    m += u8(3) + u8(7) + string("MaskTexture") + string("") + u8(0) + u8(0) + u8(0) + u8(0) + u8(0)

    # techniques
    m += u8(1) + string("MaskDrawing") + u8(0)
    m += u8(1) + string("P0") + u8(0) + u8(0xFF) + u8(0)  # no vertex shader, pixel shader 0
    m += u8(0) + u8(0) + u8(0)  # no blend / depth-stencil / rasterizer state
    return bytes(m)


def main(src, dst):
    orig = open(src, "rb").read()
    at = orig.index(b"MGFX")
    effect_key = orig[at + 6 : at + 10]

    header_end = at - 4  # int32 effect length precedes MGFX
    prefix = bytearray(orig[:header_end])
    mgfx = build_mgfx(effect_key)

    body = bytes(prefix[10:]) + i32(len(mgfx)) + mgfx
    out = bytes(prefix[:6]) + i32(10 + len(body)) + body
    open(dst, "wb").write(out)
    print(f"wrote {dst} ({len(out)} bytes, GLSL {len(GLSL)} bytes)")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
