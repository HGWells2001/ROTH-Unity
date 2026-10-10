#!/usr/bin/env python3
import argparse, struct, pathlib, sys

def u16(b,o): return struct.unpack_from('<H',b,o)[0]
def check(path):
    b=path.read_bytes()
    if len(b)<30: raise ValueError('too small')
    h=struct.unpack_from('<15H',b,0)
    vo,ver,so,fo,tmo,mmo,vor,sig,mid,sec7,vss,objss,foot,cmd,sc=h
    if ver!=0x70 or sig!=0x5257: raise ValueError(f'bad header version/signature {ver:#x}/{sig:#x}')
    platforms=0; unresolved=0
    valid=set()
    if mid:
        platforms=u16(b,mid-2)
        valid={mid+i*14 for i in range(platforms)}
    used=0
    for i in range(sc):
        off=u16(b,so+i*26+24)
        if off:
            used+=1
            if off not in valid: unresolved+=1
    objstart=vo+vss+cmd+sec7
    if objstart+2>len(b): raise ValueError('object start outside file')
    size=u16(b,objstart)
    if objstart+size>len(b): raise ValueError('object section outside file')
    total=containers=bad=0
    table=objstart+2
    for i in range(sc):
        rel=u16(b,table+i*2)
        if not rel: continue
        containers+=1; pos=objstart+rel
        if pos+2>objstart+size: bad+=1; continue
        c,c2=struct.unpack_from('<BB',b,pos)
        if c!=c2 or pos+2+c*16>objstart+size: bad+=1
        total+=c
    print(f'{path.name}: sectors={sc} platforms={platforms} platform_refs={used} unresolved_platforms={unresolved} object_containers={containers} objects={total} bad_containers={bad}')
    return unresolved==0 and bad==0

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('raw', nargs='+'); ns=ap.parse_args()
    ok=True
    for f in ns.raw:
        try: ok=check(pathlib.Path(f)) and ok
        except Exception as e: print(f'{f}: ERROR {e}',file=sys.stderr); ok=False
    raise SystemExit(0 if ok else 1)
if __name__=='__main__': main()
