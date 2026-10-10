#!/usr/bin/env python3
"""Read-only LE fixup audit for a user-owned ROTH.EXE.

No binary extraction or relocations are written. The report contains only
aggregated counts and selected source->target address summaries.

Usage: python inspect_roth_fixups.py ROTH.EXE --json private_report.json
"""
import argparse
from collections import Counter
import json
from pathlib import Path
import struct
import sys
from inspect_roth_le import verify, u32

SOURCE_WIDTH = {0:1, 2:2, 3:4, 5:2, 6:6, 7:4, 8:4}

class DecodeError(ValueError):
    pass

def parse_fixups(data, report):
    total = report['number_of_pages']
    fixpage = report['fixup_page_table_file_offset']
    recbase = report['fixup_records_file_offset']
    pages = [u32(data,fixpage+4*i) for i in range(total+1)]
    if pages[0] != 0 or any(a>b for a,b in zip(pages,pages[1:])):
        raise DecodeError('invalid fixup page offset table')
    if recbase+pages[-1]>len(data):
        raise DecodeError('fixup record bytes exceed file')
    sources = {}
    for ob in report['objects']:
        for pg in range(ob['first_page_map_index'],ob['first_page_map_index']+ob['mapped_page_count']):
            if pg in sources:raise DecodeError('overlapping logical pages')
            sources[pg] = (ob['number'],(pg-ob['first_page_map_index'])*report['page_size'])
    records=[]
    for page_index in range(1,total+1):
        start=recbase+pages[page_index-1]
        end=recbase+pages[page_index]
        offset=start
        if page_index not in sources:
            raise DecodeError(f'page {page_index} does not belong to an object')
        objnum, pageoffset=sources[page_index]
        while offset < end:
            record_start=offset
            def get(n):
                nonlocal offset
                if offset+n>end:raise DecodeError(f'truncated record page {page_index}, at {offset:#x}')
                out=data[offset:offset+n]
                offset+=n
                return int.from_bytes(out,'little')
            source=get(1);target_flags=get(1)
            typ=source & 0xf
            if typ not in SOURCE_WIDTH or source & 0xc0:
                raise DecodeError(f'unknown source type {source:#x} at {record_start:#x}')
            list_source = bool(source & 0x20)
            count=get(1) if list_source else 1
            if list_source and count==0:raise DecodeError('source list is empty')
            raw_sources=[] if list_source else [struct.unpack('<h',struct.pack('<H',get(2)))[0]]
            trg_type=target_flags & 3
            target_index=get(2 if target_flags & 0x40 else 1)
            target_offset=None
            if trg_type==0:
                if not 1<=target_index<=len(report['objects']):
                    raise DecodeError(f'bad target object {target_index} at {record_start:#x}')
                if typ !=2:
                    target_offset=get(4 if target_flags & 0x10 else 2)
            elif trg_type==1:
                target_offset=get(1 if target_flags & 0x80 else (4 if target_flags & 0x10 else 2))
            elif trg_type==2:
                target_offset=get(4 if target_flags & 0x10 else 2)
            else:
                target_offset=None   # Internal via entry table: only ordinal.
            additive=get(4 if target_flags & 0x20 else 2) if target_flags & 4 else 0
            if list_source:
                raw_sources=[struct.unpack('<h',struct.pack('<H',get(2)))[0] for _ in range(count)]
            if target_flags & 8:
                # Chained records require following the pointers in the *image*.
                # Do not silently treat the first record as the entire chain.
                if typ!=7 or trg_type not in (0,3) or list_source:
                    raise DecodeError(f'unsupported invalid chain at {record_start:#x}')
            for rel in raw_sources:
                if rel < -SOURCE_WIDTH[typ]+1 or rel >= report['page_size']:
                    raise DecodeError(f'bad source offset {rel} in page {page_index}')
            records.append({
                'record_file_offset':record_start,'page':page_index,
                'source_object':objnum,'source_object_page_offset':pageoffset,
                'source_offsets':raw_sources,'source_type':typ,'source_flags':source,
                'target_type':trg_type,'target_flags':target_flags,
                'target_index':target_index,'target_object_offset':target_offset,
                'additive':additive,'chain':bool(target_flags & 8),
            })
        if offset != end:raise DecodeError(f'page {page_index} not fully decoded')
    return records

def summary(records,report):
    pairs=Counter()
    types=Counter()
    targets=Counter()
    chains=0
    expanded=0
    bad_internal_targets=[]
    for r in records:
        types[(r['source_type'],r['target_type'])]+=1
        expanded+=len(r['source_offsets'])
        chains+=r['chain']
        if r['target_type']==0:
            pairs[(r['source_object'],r['target_index'])]+=len(r['source_offsets'])
            if r['target_object_offset'] is not None:
                obj=report['objects'][r['target_index']-1]
                if r['target_object_offset'] >= obj['virtual_size']:
                    bad_internal_targets.append({
                        'record_offset':r['record_file_offset'],
                        'object':r['target_index'],
                        'target_offset':r['target_object_offset'],
                    })
        targets[(r['target_type'],r['target_index'])]+=1
    return {
        'exe_sha256':report['sha256'],
        'fixup_records':len(records),
        'explicit_source_locations':expanded,
        'chain_record_count':chains,
        'source_target_type_counts':{f'source_{a}_target_{b}':n for (a,b),n in sorted(types.items())},
        'source_object_to_target_object_explicit_counts':{f'{a}->{b}':n for (a,b),n in sorted(pairs.items())},
        'noncontiguous_or_oob_target_count':len(bad_internal_targets),
        'out_of_bounds_target_examples':bad_internal_targets[:15],
        'warning':'Fixup record decoding, not a relocated executable or verified RAW opcode dispatcher. Chained fixups are not expanded.'
    }

def main(argv=None):
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('exe',type=Path)
    ap.add_argument('--json',type=Path)
    args=ap.parse_args(argv)
    try:
        d=args.exe.read_bytes();r=verify(d)
        result=summary(parse_fixups(d,r),r)
        print(json.dumps(result,indent=2))
        if args.json:args.json.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
        return 0
    except (ValueError,IndexError,OSError,struct.error) as exc:
        print(f'Fixup audit rejected file: {exc}',file=sys.stderr)
        return 2

if __name__=='__main__':raise SystemExit(main())
