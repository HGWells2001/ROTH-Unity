#!/usr/bin/env python3
import argparse, struct

p=argparse.ArgumentParser(description='Validate the ROTH RAW command section.')
p.add_argument('raw')
a=p.parse_args()
d=open(a.raw,'rb').read()
H=struct.unpack_from('<15H',d,0)
vertices_offset, vertices_size, command_size = H[0],H[10],H[13]
start=vertices_offset+vertices_size
if command_size==0:
    print('commands=0'); raise SystemExit(0)
end=start+command_size
sig,unk,commands_offset,count=struct.unpack_from('<4H',d,start)
assert sig==30003, hex(sig)
cats=[]
pos=start+8
for category in range(1,16):
    off,n=struct.unpack_from('<HH',d,pos); pos+=4
    if n: cats.append((category,off,n))
refs=list(struct.unpack_from('<%dH'%count,d,pos)); pos+=count*2
q=start+commands_offset
offset_map={}
commands=[]
for i in range(count):
    assert q+6<=end
    size,mod,base,nxt=struct.unpack_from('<HBBH',d,q)
    assert size>=6 and size%2==0 and q+size<=end, (i,q-start,size,end-start)
    args=struct.unpack_from('<%dH'%((size-6)//2),d,q+6) if size>6 else ()
    offset_map[q-start]=i
    commands.append((q-start,size,mod,base,nxt,args))
    q+=size
assert q==end,(q-start,end-start)
resolved=sum(1 for r in refs if r and r in offset_map)
unresolved=[r for r in refs if r and r not in offset_map]
print(f'commands={len(commands)} entry_refs={resolved} categories={len(cats)} section={command_size}')
print('categories:', ', '.join(f'{c}:{n}' for c,o,n in cats))
print('unresolved_refs=',len(unresolved))
if unresolved: print('first unresolved:',unresolved[:10])
