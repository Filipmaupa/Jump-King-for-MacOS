"""Extracts the Windows icon group(s) from a PE file into .ico files (one per group, plus one per image)."""
import struct, sys, os
data = open(sys.argv[1], 'rb').read(); out = sys.argv[2]
pe = struct.unpack_from('<I', data, 0x3C)[0]
nsec = struct.unpack_from('<H', data, pe + 6)[0]
optsz = struct.unpack_from('<H', data, pe + 20)[0]
opt = pe + 24
magic = struct.unpack_from('<H', data, opt)[0]
ddir = opt + (96 if magic == 0x10B else 112)
rsrc_rva, rsrc_size = struct.unpack_from('<II', data, ddir + 2 * 8)
secs = []
for i in range(nsec):
    o = opt + optsz + i * 40
    va, vsz, raw, rawsz = struct.unpack_from('<IIII', data, o + 8)[0:4] if False else (struct.unpack_from('<I', data, o + 12)[0], struct.unpack_from('<I', data, o + 8)[0], struct.unpack_from('<I', data, o + 20)[0], struct.unpack_from('<I', data, o + 16)[0])
    secs.append((va, max(vsz, rawsz), raw))
def off(rva):
    for va, sz, raw in secs:
        if va <= rva < va + sz: return rva - va + raw
    raise ValueError(hex(rva))
base = off(rsrc_rva)
def entries(d):
    named, ids = struct.unpack_from('<HH', data, base + d + 12)
    for i in range(named + ids):
        name, target = struct.unpack_from('<II', data, base + d + 16 + i * 8)
        yield name, target
def leaves(d):
    for name, target in entries(d):
        if target & 0x80000000: yield from ((name, x) for _, x in leaves(target & 0x7FFFFFFF))
        else:
            rva, size = struct.unpack_from('<II', data, base + target)
            yield name, data[off(rva):off(rva) + size]
icons, groups = {}, []
for typ, target in entries(0):
    if typ == 3:
        for name, t2 in entries(target & 0x7FFFFFFF):
            for _, blob in leaves(t2 & 0x7FFFFFFF): icons[name] = blob
    if typ == 14:
        for name, t2 in entries(target & 0x7FFFFFFF):
            for _, blob in leaves(t2 & 0x7FFFFFFF): groups.append(blob)
print('rsrc size', rsrc_size, 'icons', len(icons), 'groups', len(groups))
for gi, g in enumerate(groups):
    _, _, count = struct.unpack_from('<HHH', g, 0)
    items = []
    for i in range(count):
        w, h, cc, _, planes, bpp, size, iid = struct.unpack_from('<BBBBHHIH', g, 6 + i * 14)
        items.append((w or 256, h or 256, bpp, iid))
    for (w, h, bpp, iid) in items:
        img = icons[iid]
        kind = 'png' if img[:4] == b'\x89PNG' else 'dib'
        print(f'group {gi}: {w}x{h} {bpp}bpp {kind} {len(img)} bytes')
        hdr = struct.pack('<HHH', 0, 1, 1) + struct.pack('<BBBBHHII', w % 256, h % 256, 0, 0, 1, bpp, len(img), 22)
        open(os.path.join(out, f'g{gi}_{w}x{h}_{bpp}.ico'), 'wb').write(hdr + img)
