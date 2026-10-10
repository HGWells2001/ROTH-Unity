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
    # A second indirect call in the animation list uses the SAME function
    # pointer table, but with an independently relocated base. The scheduler
    # loads the original command opcode from animation[4]. The base shift
    # maps command 9 to the callback entry 76, command 7 to entry 74.
    animation_pattern=bytes.fromhex('0f b6 58 04 ff 14 9d')
    animation_dispatches=[]
    search=0
    while True:
        p=data.find(animation_pattern,search)
        if p<0:break
        search=p+1
        fix=loc.get((1,p+len(animation_pattern)))
        if (fix is None or fix['source_type']!=7 or
            fix['target_type']!=0 or fix['target_index']!=1 or
            fix['target_object_offset'] is None):continue
        offset=fix['target_object_offset']-table
        if offset<0 or offset%4:continue
        shift=offset//4
        if shift+9>=128:continue
        horizontal=opcode_table[shift+9]
        vertical=opcode_table[shift+7]
        if data[horizontal:horizontal+len(ALT_UPDATE_PREFIX)]!=ALT_UPDATE_PREFIX:continue
        if data[vertical:vertical+len(UPDATE_PREFIX)]!=UPDATE_PREFIX:continue
        animation_dispatches.append((p,shift,horizontal,vertical))
    if len(animation_dispatches)!=1:
        raise NotFound('Expected one relocated animation dispatcher for RAW 7/9, found '+str(len(animation_dispatches)))
    animation_site,shift,horizontal,vertical=animation_dispatches[0]
    # Both callbacks load a shared frame delta from the identical object-3
    # global. Check relocation *targets* rather than trusting MZ offsets.
    def tick_reference(callback, operand_relative):
        fix=loc.get((1,callback+operand_relative))
        return fix is not None and fix['target_type']==0 and fix['target_index']==3 and fix['target_object_offset']==0x1570c
    if not tick_reference(horizontal,0x13) or not tick_reference(vertical,0x9):
        raise NotFound('The expected frame-delta references could not be validated')
    speed_signature=bytes.fromhex('0f b6 57 07 0f af c2')
    if speed_signature not in data[horizontal:horizontal+0x70]:
        raise NotFound('Horizontal callback does not contain speed multiply')
    fraction_signature=bytes.fromhex('f6 47 06 04 74 16 8a 56 06 83 e2 3f 01 d0 88 46 06 c1 e8 06')
    if fraction_signature not in data[vertical:vertical+0x60]:
        raise NotFound('Vertical callback does not contain 6-bit fraction path')
    # The horizontal callback region does not contain that vertical path.
    if fraction_signature in data[horizontal:vertical]:
        raise NotFound('Unexpected fraction instruction signature in horizontal callback')
    # Main loop writes the frame/tick delta to the shared global. The
    # preceding instruction derives it from a 16-bit counter in two loops.
    tick_writer_signatures=(
        bytes.fromhex('0f bf 05 ac 0f 02 00 89 c2 2b 55 fc 89 15 0c 57 01 00'),
        bytes.fromhex('0f bf 05 ac 0f 02 00 89 c2 2b 55 f8 89 15 0c 57 01 00'),
    )
    tick_writers=[]
    for sign in tick_writer_signatures:
        pos=data.find(sign)
        if pos>=0:
            fix=loc.get((1,pos+14))
            if fix is not None and fix['target_type']==0 and fix['target_index']==3 and fix['target_object_offset']==0x1570c:
                tick_writers.append(pos+12)
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
        'animation_dispatch_object_offset':animation_site,
        'animation_dispatch_table_object_offset':table+shift*4,
        'animation_dispatch_table_index_shift':shift,
        'opcode_9_update_table_index':shift+9,
        'opcode_9_update_object_offset':horizontal,
        'opcode_7_update_table_index':shift+7,
        'opcode_7_update_object_offset':vertical,
        'opcode_9_speed_operand':'BYTE [command + 7]',
        'opcode_9_speed_expression':'frame_delta * unsigned_byte_speed; no 6-bit fractional path in this callback',
        'opcode_7_fractional_path':'flag byte[command+6]&4; remainder from animation[6]&63; right shift 6',
        'frame_delta_object_3_offset':0x1570c,
        'frame_delta_writer_object_offsets':tick_writers,
        'frame_delta_real_seconds_per_tick':'not identified',
        'opcode_9_handler_prefix_validated':True,
        'interpretation_limits':[
            'Object offsets are not file offsets; use an LE loader in Ghidra.',
            'Opcode 9 entry is verified from a relocated 128-entry call table.',
            'Opcode 9 callback is proven by the second relocated animation dispatcher at object1+0x247c8; index 76 points to object1+0x22bd9.',
            'The 1/64 fractional accumulator previously attributed to opcode 9 belongs to opcode 7 instead; movement geometry, repeat states and real-time tick frequency still need validation.',
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
