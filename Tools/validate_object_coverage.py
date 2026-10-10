#!/usr/bin/env python3
import argparse, struct, collections, os
p=argparse.ArgumentParser(description='Audit ROTH RAW object resource types against primary and secondary DAS archives.')
p.add_argument('raw'); p.add_argument('primary_das'); p.add_argument('secondary_das')
a=p.parse_args()
R=open(a.raw,'rb').read(); H=struct.unpack_from('<15H',R,0)
obj_start=H[0]+H[10]+H[13]+H[9]
offs=struct.unpack_from('<%dH'%H[14],R,obj_start+2)
objs=[]
for rel in offs:
    if not rel: continue
    q=obj_start+rel; count=R[q]; q+=2
    for _ in range(count):
        vals=struct.unpack_from('<hhBBBBBBhHH',R,q); q+=16
        objs.append((vals[2],vals[3]))

def load_das(path):
    d=open(path,'rb').read(); fat=struct.unpack_from('<I',d,8)[0]; total=sum(struct.unpack_from('<4H',d,48)); return d,fat,total
P=load_das(a.primary_das); S=load_das(a.secondary_das)
def classify(bundle,idx):
    d,fat,total=bundle
    if idx<0 or idx>=total:return 'out-of-range'
    off,size,f1,f2=struct.unpack_from('<IHBB',d,fat+idx*8)
    if not size:return 'empty'
    if f1&0x20:return 'directional/monster'
    if not off:return 'empty'
    mod,it=d[off],d[off+1]
    if it&0x80:return '3d-object'
    if mod&0x40:return 'image-pack'
    if it&1:
        num=struct.unpack_from('<H',d,off+12)[0]
        return 'animation-type2' if num==0xffff or num==0xfffe else 'animation-type1'
    return 'standard'
c=collections.Counter()
for ti,ts in objs:
    if ts==0: c[classify(P,ti+4096)]+=1
    elif ts==1: c[classify(P,ti+4352)]+=1
    elif ts==2: c[classify(S,ti)]+=1
    elif ts==3: c[classify(S,ti+256)]+=1
    else:c['unknown-source']+=1
print('objects=',len(objs))
for k,v in c.most_common():print(f'{k}: {v}')
renderable=sum(c[k] for k in ('standard','animation-type1','animation-type2','image-pack','3d-object','directional/monster'))
print(f'0.6-renderable-or-parsed={renderable}/{len(objs)} ({renderable*100.0/len(objs):.1f}%)')
