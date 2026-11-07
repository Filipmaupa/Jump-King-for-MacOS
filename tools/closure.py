"""Lists the Mono framework assemblies (paths relative to the framework folder) that the .exe/.dll files in
an app folder need, transitively, plus any extra framework assemblies named on the command line.
Usage: closure.py <mono lib/mono/4.5> <app folder> [extra assembly names...]"""
import os, re, subprocess, sys
fw, app, extra = sys.argv[1], sys.argv[2], sys.argv[3:]
dirs = [fw, os.path.join(fw, 'Facades')]
todo = [os.path.join(app, f) for f in os.listdir(app) if f.endswith(('.exe', '.dll'))]
todo += [os.path.join(fw, n + '.dll') for n in extra if os.path.exists(os.path.join(fw, n + '.dll'))]
need = {p for p in todo if os.path.dirname(p) == fw}
seen, missing = set(), set()
while todo:
    p = todo.pop()
    if p in seen: continue
    seen.add(p)
    out = subprocess.run(['monodis', '--assemblyref', p], capture_output=True, text=True).stdout
    for name in re.findall(r'Name=(\S+)', out):
        if os.path.exists(os.path.join(app, name + '.dll')) or os.path.exists(os.path.join(app, name + '.exe')): continue
        for d in dirs:
            c = os.path.join(d, name + '.dll')
            if os.path.exists(c):
                if c not in need: need.add(c); todo.append(c)
                break
        else: missing.add(name)
for n in sorted(need): print(os.path.relpath(n, fw))
print('MISSING:', sorted(missing), file=sys.stderr)
