#!/usr/bin/env python3
import struct, sys
from pathlib import Path

def parse(path):
    b=Path(path).read_bytes()
    h=struct.unpack_from('<15H', b, 0)
    vo,ver,so,fo,tmo,mo,vor,sig,mid,s7,vss,oss,fs,css,sc=h
    sectors=[]; p=so
    for _ in range(sc):
        v=struct.unpack_from('<hhHHHBBbBHBBBBHHH', b, p)
        sectors.append({'floor':v[1],'ceil':v[0],'faces':v[8],'first':v[9]}); p+=0x1A
    face_count=struct.unpack_from('<H',b,fo-2)[0]
    faces=[]; face_offsets={}; p=fo
    for i in range(face_count):
        face_offsets[p]=i; faces.append(struct.unpack_from('<6H',b,p)); p+=0x0C
    vertex_count=struct.unpack_from('<H',b,vo+6)[0]
    vertices=[]; vertex_offsets={}; p=vo+8
    for i in range(vertex_count):
        vertex_offsets[p-vo]=i; vertices.append(struct.unpack_from('<hh',b,p+8)); p+=0x0C
    return sectors,faces,face_offsets,vertices,vertex_offsets

def audit(path):
    sectors,faces,fo,vertices,vo=parse(path)
    discontinuities=degenerate=cw=ccw=0
    solid=portal=0
    cap_tris=0
    for s in sectors:
        first=fo[s['first']]
        poly=[]
        for j in range(s['faces']):
            f=faces[first+j]
            n=faces[first+((j+1)%s['faces'])]
            if f[1] != n[0]: discontinuities += 1
            poly.append(vertices[vo[f[0]]])
            if f[4] in (0,0xFFFF): solid += 1
            else: portal += 1
        area=sum(poly[i][0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*poly[i][1] for i in range(len(poly)))
        if area==0: degenerate+=1
        elif area<0: cw+=1
        else: ccw+=1
        cap_tris += max(0,len(poly)-2)
    print(path)
    print(f'  sectors: {len(sectors)}')
    print(f'  discontinuities: {discontinuities}')
    print(f'  degenerate sectors: {degenerate}')
    print(f'  clockwise sectors: {cw}')
    print(f'  counter-clockwise sectors: {ccw}')
    print(f'  solid/boundary faces: {solid}')
    print(f'  portal faces: {portal}')
    print(f'  cap triangles per floor/ceiling: {cap_tris}')
    return discontinuities==0 and degenerate==0

if __name__=='__main__':
    ok=True
    for p in sys.argv[1:]: ok = audit(p) and ok
    raise SystemExit(0 if ok else 1)
