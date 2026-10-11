#!/usr/bin/env python3
"""Read-only RAW9 four-vertex selector for original user-owned .RAW maps.

Usage: python Tools/audit_roth_raw9_geometry.py path/to/owned/MAP.RAW
       python Tools/audit_roth_raw9_geometry.py path/to/owned/retail.zip --json audit.json
No retail map contents are bundled or altered.
"""
import argparse
import json
from pathlib import Path
import struct
import zipfile


def u16(b, p):
    if p < 0 or p + 2 > len(b):
        raise ValueError('truncated u16 field at ' + hex(p))
    return struct.unpack_from('<H', b, p)[0]


def s16(b, p):
    if p < 0 or p + 2 > len(b):
        raise ValueError('truncated s16 field at ' + hex(p))
    return struct.unpack_from('<h', b, p)[0]


def select_vertices(data, sector, along_x):
    """Match object1+22af8..22b8f: first face vertices at +0,+12,+24,+36.

    DOS dereferences a sector's FirstFaceOffset, then each face's first
    VertexOffset01, and picks vertex record +8 for X or +10 for Y.
    Four (signed-coordinate, vertex-index) pairs are bubble sorted stably.
    """
    first = sector['first_face_offset']
    face_start = sector['face_section_start']
    face_end = sector['face_section_end']
    if sector['face_count'] < 4 or first < face_start or (first-face_start) % 12:
        raise ValueError('sector cannot supply four consecutive face records')
    if first + 4*12 > face_end:
        raise ValueError('four faces extend outside face section')
    selected=[]
    seen=set()
    for i in range(4):
        face=first+i*12
        if u16(data,face+6)!=sector['raw_sector_offset']:
            raise ValueError('face sector pointer mismatch at slot '+str(i))
        vertex_offset=u16(data,face)
        count=sector['vertex_count']
        if vertex_offset < 8 or (vertex_offset-8)%12:
            raise ValueError('invalid vertex record offset at slot '+str(i))
        index=(vertex_offset-8)//12
        if index >= count or index in seen:
            raise ValueError('missing or duplicate first vertex at slot '+str(i))
        seen.add(index)
        p=sector['vertex_section_start']+vertex_offset+(8 if along_x else 10)
        if p+2 > sector['vertex_section_end']:
            raise ValueError('vertex coordinate outside vertex section')
        selected.append((s16(data,p),index,vertex_offset))
    # The assembly makes exactly 3 passes of 3 adjacent comparisons.
    for _ in range(3):
        for i in range(3):
            if selected[i][0] > selected[i+1][0]:
                selected[i],selected[i+1]=selected[i+1],selected[i]
    return selected


def audit_raw(data):
    if len(data)<30:
        raise ValueError('truncated RAW header')
    h=struct.unpack_from('<15H',data)
    vstart,sector_start,face_start=h[0],h[2],h[3]
    vsize,cmdsize,sector_count=h[10],h[13],h[14]
    if face_start<2 or face_start>len(data):
        raise ValueError('invalid face section')
    face_end=face_start+12*u16(data,face_start-2)
    vend=vstart+vsize
    if face_end>len(data) or vstart+8>vend or vend>len(data):
        raise ValueError('invalid geometry sections')
    vertex_count=u16(data,vstart+6)
    if 8+12*vertex_count>vsize:
        raise ValueError('vertex table truncated')
    if sector_start+26*sector_count>len(data):
        raise ValueError('sector table truncated')
    sectors={}
    for i in range(sector_count):
        off=sector_start+i*26
        sector_id=u16(data,off+20)
        if sector_id in sectors:
            raise ValueError('duplicate sector id '+str(sector_id))
        sectors[sector_id]={
            'raw_sector_offset':off,'face_count':data[off+13],
            'first_face_offset':u16(data,off+14),
            'face_section_start':face_start,'face_section_end':face_end,
            'vertex_section_start':vstart,'vertex_section_end':vend,
            'vertex_count':vertex_count,
        }
    cmdstart=vend
    cmdend=vend+cmdsize
    if cmdsize and (cmdend>len(data) or cmdstart+8>cmdend or u16(data,cmdstart)!=30003):
        raise ValueError('invalid command section')
    records=[]
    if cmdsize:
        count=u16(data,cmdstart+6)
        cursor=cmdstart+u16(data,cmdstart+4)
        for cmd_index in range(count):
            if cursor+6>cmdend:
                raise ValueError('command header outside section')
            n=u16(data,cursor)
            if n<6 or n%2 or cursor+n>cmdend:
                raise ValueError('invalid command length')
            if data[cursor+3]==9:
                args=struct.unpack_from('<'+'H'*((n-6)//2),data,cursor+6)
                rec={'command_index':cmd_index}
                if len(args)<6:
                    rec['error']='opcode9 missing six arguments'
                elif args[1] not in sectors:
                    rec['sector_id']=args[1]
                    rec['error']='unknown sector id'
                else:
                    sector=sectors[args[1]]
                    along_x=bool(args[0]&0x40)
                    rec.update({'sector_id':args[1],'axis':'X' if along_x else 'Y',
                                'sector_face_count':sector['face_count']})
                    try:
                        result=select_vertices(data,sector,along_x)
                        rec['vertices_sorted']=[x[1] for x in result]
                        rec['raw_vertex_offsets_sorted']=[x[2] for x in result]
                        rec['coordinate_range']=[result[0][0],result[3][0]]
                        rec['non_quad_sector']=sector['face_count']!=4
                    except ValueError as e:
                        rec['error']=str(e)
                records.append(rec)
            cursor+=n

    return {'opcode9_count':len(records),
            'valid_count':sum('error' not in r for r in records),
            'warnings':sum('error' in r or r.get('non_quad_sector',False) for r in records),
            'commands':records}


def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('source',type=Path)
    ap.add_argument('--json',type=Path)
    args=ap.parse_args()
    results=[]
    if args.source.is_dir():
        items=[(str(p),p.read_bytes()) for p in sorted(args.source.rglob('*')) if p.suffix.lower()=='.raw']
    elif zipfile.is_zipfile(args.source):
        with zipfile.ZipFile(args.source) as z:
            items=[(n,z.read(n)) for n in z.namelist() if n.upper().endswith('.RAW') and not n.endswith('/')]
    else:
        items=[(str(args.source),args.source.read_bytes())]
    for name,body in items:
        try:
            report=audit_raw(body)
        except (ValueError,struct.error) as e:
            report={'error':str(e)}
        results.append({'file':name,**report})
    text=json.dumps({'maps':results},indent=2)
    print(text)
    if args.json:args.json.write_text(text+'\n',encoding='utf-8')
    return 2 if any('error' in m or m.get('warnings',0)>0 for m in results) else 0


if __name__=='__main__':
    raise SystemExit(main())
