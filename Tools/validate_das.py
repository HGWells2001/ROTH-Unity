#!/usr/bin/env python3
import argparse, struct
from pathlib import Path

def cstr(b,p):
    q=b.index(0,p)
    return b[p:q].decode('ascii','replace'), q+1

def parse(path):
    b=Path(path).read_bytes()
    if b[:4] != b'DASP': raise ValueError('bad DASP signature')
    version=struct.unpack_from('<H',b,4)[0]
    fat_off=struct.unpack_from('<I',b,8)[0]
    pal_off=struct.unpack_from('<I',b,0x0c)[0]
    names_off=struct.unpack_from('<I',b,0x14)[0]
    counts=struct.unpack_from('<4H',b,0x30)
    total=sum(counts)
    if version != 5: raise ValueError('unexpected version')
    if fat_off+total*8 > len(b): raise ValueError('FAT outside file')
    names={}
    if names_off:
        c1,c2=struct.unpack_from('<HH',b,names_off); p=names_off+4
        for _ in range(c1+c2):
            size,idx=struct.unpack_from('<HH',b,p); p+=4
            name,p=cstr(b,p); desc,p=cstr(b,p); names[idx]=(name,desc)
    standard=animated=packs=directional=invalid=0
    examples=[]
    for idx in range(total):
        off,size,f1,f2=struct.unpack_from('<IHBB',b,fat_off+idx*8)
        if not size: continue
        if f1&0x20: directional+=1; continue
        hpos=off-4 if f1&0x08 else off
        if hpos<0 or hpos+6>len(b): invalid+=1; continue
        pos=hpos+(4 if f1&0x08 else 0)
        mod,typ,w,h=struct.unpack_from('<BBHH',b,pos)
        if typ&1: animated+=1
        elif typ&0x80: directional+=1
        elif mod&0x40: packs+=1
        elif w and h and pos+6+w*h<=len(b):
            standard+=1
            if len(examples)<8: examples.append((idx,names.get(idx),w,h,hex(typ)))
        else: invalid+=1
    return dict(file_size=len(b),version=version,fat_blocks=counts,fat_total=total,palette_offset=pal_off,
                filename_entries=len(names),standard_images=standard,animated_images=animated,image_packs=packs,
                directional_or_object=directional,invalid=invalid,examples=examples)

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('das',nargs='+',type=Path); a=ap.parse_args(); ok=True
    for p in a.das:
        try:
            r=parse(p); print(p)
            for k,v in r.items(): print(' ',k,':',v)
            ok &= r['invalid']==0
        except Exception as e: print(p,'ERROR',e); ok=False
    raise SystemExit(0 if ok else 1)
if __name__=='__main__': main()
