#!/usr/bin/env python3
"""Identify the RAW command dispatcher and opcode-9 handler from LE fixups.

Read-only analysis of a user-owned ROTH.EXE; no binary bytes are exported.
Requires inspect_roth_le.py and inspect_roth_fixups.py in the same folder.
"""
import argparse
from pathlib import Path
import json
import sys
from inspect_roth_le import verify, initialized_object_bytes
from inspect_roth_fixups import parse_fixups

# cmp/mask command byte from [esi+3], then CALL pointer table indexed by EBX.
DISPATCH_SIGNATURE = bytes.fromhex('8a 5e 03 83 e3 7f ff 14 9d')
HANDLER_PREFIX = bytes.fromhex('29 c0 f6 46 02 20')
UPDATE_PREFIX = bytes.fromhex('83 ec 08 89 c6 8b 7e 08')
ALT_UPDATE_PREFIX = bytes.fromhex('8b 78 08 f6 47 02 08')

class NotFound(ValueError):
    pass

def index_relocations(records):
    # Do not include page-crossing duplicate relocs in this address map.
    loc={}
    for r in records:
        for pos in r['source_offsets']:
            where=(r['source_object'],r['source_object_page_offset']+pos)
            if where in loc:
                old=loc[where]
                # LE cross-page references are deliberately recorded twice.
                same=(old['source_type']==r['source_type'] and
                      old['target_type']==r['target_type'] and
                      old['target_index']==r['target_index'] and
                      old['target_object_offset']==r['target_object_offset'])
                if not same:raise ValueError(f'conflicting source relocation {where!r}')
                continue
            loc[where]=r
    return loc

def analyze(executable_bytes):
    r=verify(executable_bytes)
    records=parse_fixups(executable_bytes,r)
    loc=index_relocations(records)
    data=initialized_object_bytes(executable_bytes,r,1)
    found=[]
    where=0
    while True:
        at=data.find(DISPATCH_SIGNATURE,where)
        if at<0:break
        where=at+1
        entry=loc.get((1,at+len(DISPATCH_SIGNATURE)))
        if entry is None or entry['source_type']!=7 or entry['target_type']!=0 or entry['target_index']!=1:
            continue
        base=entry['target_object_offset']
        if base is None or base+10*4>len(data):continue
        entries=[]
        for opcode in range(10):
            fix=loc.get((1,base+opcode*4))
            if (fix is None or fix['source_type']!=7 or fix['target_type']!=0 or fix['target_index']!=1 or
                fix['target_object_offset'] is None):
                break
            entries.append(fix['target_object_offset'])
        if len(entries)!=10:continue
        h=entries[9]
        if data[h:h+len(HANDLER_PREFIX)]!=HANDLER_PREFIX:continue
        # Extend the contiguous opcode table up to exactly the indices usable
        # with EBX AND 0x7F, which is 128 possible values (not 201+).
        mapped={}
        for opcode in range(128):
            fix=loc.get((1,base+opcode*4))
            if fix is None or fix['target_type']!=0 or fix['target_index']!=1:break
            mapped[opcode]=fix['target_object_offset']
        if len(mapped)!=128:continue
        found.append((at,base,mapped))
    if len(found)!=1:
        raise NotFound(f'Expected exactly one verified 128-entry RAW dispatcher, found {len(found)}')
    at,table,opcode_table=found[0]
    handler=opcode_table[9]
    # Animation callbacks are two function pointers in the same dispatch region.
    # They are identified structurally, NOT assumed equivalent to DOS opcode 9.
    candidates=[]
    for index,off in sorted(opcode_table.items()):
        if (data[off:off+len(UPDATE_PREFIX)]==UPDATE_PREFIX or
            data[off:off+len(ALT_UPDATE_PREFIX)]==ALT_UPDATE_PREFIX):
            candidates.append({'table_index':index,'target_object':1,'target_offset':off})
    # The update routine next to the opcode-9 handler exposes an unambiguous
    # frame-delta multiply (global 32-bit time value * per-command byte speed).
    tick_hint=bytes.fromhex('a1 0c 57 01 00 f6 46 05 40')
    speed_hint=bytes.fromhex('0f b6 57 07 0f af c2')
    hints=[]
    for index,off in sorted(opcode_table.items()):
        if index!=74 or data[off:off+len(UPDATE_PREFIX)]!=UPDATE_PREFIX:continue
        segment=data[off:off+64]
        if tick_hint in segment and speed_hint in segment:
            hints.append({'table_index':index,'object':1,'offset':off,
                          'global_tick_read_object3_offset':0x1570c,
                          'speed_operand':'BYTE [command + 7]',
                          'multiply':'imul eax,edx',
                          'fraction_branch_test':'BYTE [command + 6] & 0x04',
                          'fraction_right_shift':6})
    return {
        'exe_sha256':r['sha256'],
        'validated_fixup_records':len(records),
        'dispatch_object':1,
        'dispatch_object_offset':at,
        'dispatch_call_object_offset':at+6,
        'dispatch_table_object':1,
        'dispatch_table_object_offset':table,
        'confirmed_dispatch_entries':128,
        'opcode_9_table_entry_object_offset':table+9*4,
        'opcode_9_handler_object_offset':handler,
        'opcode_9_initial_state_check':'BYTE [esi + 2] & 0x20',
        'opcode_9_axis_test':'BYTE [esi + 6] & 0x40',
        'opcode_9_known_callback_entries': [x for x in candidates if x['table_index'] in (74,76)],
        'opcode_9_frame_speed_clues':hints,
        'opcode_9_handler_prefix_validated':True,
        'interpretation_limits':[
            'Object offsets are not file offsets; use an LE loader in Ghidra.',
            'Opcode 9 entry is verified from a relocated 128-entry call table.',
            'Callback relationship to opcode 9 is supported by code adjacency and table refs; dynamic registration needs confirmation.',
            'Movement scale, timing base, return-state semantics and clamping still require more analysis and DOS runtime validation.',
            'The previous Unity assumption end-start displacement is not established by this audit.'
        ]
    }

def main(argv=None):
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('exe',type=Path,help='Your own ROTH.EXE')
    ap.add_argument('--json',type=Path,help='Write address-only JSON report')
    a=ap.parse_args(argv)
    try:
        report=analyze(a.exe.read_bytes())
        msg=json.dumps(report,indent=2)
        print(msg)
        if a.json:a.json.write_text(msg+'\n',encoding='utf-8')
        return 0
    except (ValueError,OSError,IndexError) as e:
        print('Opcode9 audit failed: '+str(e),file=sys.stderr)
        return 2

if __name__=='__main__':raise SystemExit(main())
