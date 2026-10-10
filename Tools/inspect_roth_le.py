#!/usr/bin/env python3
"""Read-only structural inspection of a user-owned MZ+LE DOS executable.

Usage:
    python Tools/inspect_roth_le.py /path/to/ROTH.EXE --json roth_le.json
    python Tools/inspect_roth_le.py /path/to/ROTH.EXE --extract-dir private/objects

The optional extraction writes *copyrighted executable bytes* to the user's local
folder and MUST NEVER be included in the repository, issue or handoff archive.
No LE relocations are applied to extracted objects. NOT an opcode-9 decompiler.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys

LIMIT = 64 * 1024 * 1024
LE_HEADER_MIN = 0xB0

def u16(data, offset):
    return struct.unpack_from('<H', data, offset)[0]

def u32(data, offset):
    return struct.unpack_from('<I', data, offset)[0]

def verify(data):
    if len(data) < 0x40 or data[:2] != b'MZ':
        raise ValueError('Not a complete DOS MZ header')
    mz_pages = u16(data, 4)
    mz_last = u16(data, 2)
    if mz_pages == 0 or mz_last > 511:
        raise ValueError('Invalid MZ file size fields')
    declared_mz = (mz_pages - 1) * 512 + mz_last if mz_last else mz_pages * 512
    mz_header = u16(data, 8) * 16
    if mz_header < 64 or mz_header > declared_mz or declared_mz > len(data):
        raise ValueError('Invalid MZ stub size')
    le = u32(data, 0x3c)
    if le < 0x40 or le + LE_HEADER_MIN > len(data) or data[le:le+2] != b'LE':
        raise ValueError('No complete LE extended header at e_lfanew')
    if data[le+2:le+4] != b'\x00\x00':
        raise ValueError('Only little-endian LE images are supported')
    page_count = u32(data, le+0x14)
    page_size = u32(data, le+0x28)
    last_page = u32(data, le+0x2c)
    object_offset = u32(data, le+0x40)
    object_count = u32(data, le+0x44)
    map_offset = u32(data, le+0x48)
    data_start = u32(data, le+0x80)
    entry_obj = u32(data, le+0x18)
    entry_off = u32(data, le+0x1c)
    if not 1 <= object_count <= 256 or not 1 <= page_count <= 16384:
        raise ValueError('Unreasonable LE object/page count')
    if page_size < 512 or page_size > 65536 or page_size & (page_size-1):
        raise ValueError('Invalid LE page size')
    if last_page > page_size:
        raise ValueError('LE last-page byte count exceeds page size')
    real_last = last_page or page_size
    if data_start < le + LE_HEADER_MIN or data_start + (page_count-1)*page_size + real_last > len(data):
        raise ValueError('Truncated or overlapping LE data-page section')
    if object_offset < LE_HEADER_MIN or map_offset < LE_HEADER_MIN:
        raise ValueError('LE object table or page map points into header')
    if le + object_offset + object_count*24 > data_start:
        raise ValueError('LE object table outside loader section')
    if le + map_offset + page_count*4 > data_start:
        raise ValueError('LE page map outside loader section')
    pages = []
    for index in range(page_count):
        at = le + map_offset + index*4
        item = data[at:at+4]
        # LE uses big-endian 24-bit page numbers, unlike LX 8-byte map records.
        physical = int.from_bytes(item[:3], 'big')
        flags = item[3]
        if flags not in (0, 3):
            raise ValueError(f'Unsupported LE page flag {flags} on page {index+1}')
        if flags == 0 and not 1 <= physical <= page_count:
            raise ValueError(f'LE page {index+1} has invalid physical page number')
        pages.append({'logical_page': index+1, 'physical_page': physical,
                      'flags': flags})
    objects = []
    for index in range(object_count):
        at = le + object_offset + index*24
        size, base, flags, first, count, reserved = struct.unpack_from('<6I', data, at)
        if count and (first < 1 or first+count-1 > page_count):
            raise ValueError(f'LE object {index+1} page-map index out of range')
        if size < 1:
            raise ValueError(f'LE object {index+1} has zero virtual size')
        objects.append({'number':index+1, 'virtual_base':base, 'virtual_size':size,
                        'flags':flags, 'first_page_map_index':first,
                        'mapped_page_count':count, 'reserved':reserved,
                        'initialized_capacity': count*page_size})
    if not (1 <= entry_obj <= len(objects)):
        raise ValueError('LE initial EIP object does not exist')
    target = objects[entry_obj-1]
    if entry_off >= target['virtual_size']:
        raise ValueError('LE EIP is outside its object virtual size')
    entry_page = entry_off // page_size
    if entry_page >= target['mapped_page_count']:
        raise ValueError('LE EIP is not on a mapped page')
    physical_entry = pages[target['first_page_map_index']-1+entry_page]
    if physical_entry['flags'] != 0:
        raise ValueError('LE EIP lies on an uninitialized page')
    entry_in_page = entry_off % page_size
    effective_bytes = real_last if physical_entry['physical_page'] == page_count else page_size
    if entry_in_page >= effective_bytes:
        raise ValueError('LE EIP lies beyond the physical last-page extent')
    entry_file = data_start + (physical_entry['physical_page']-1)*page_size + entry_in_page
    return {
        'format':'MZ+LE', 'sha256':hashlib.sha256(data).hexdigest(), 'file_size':len(data),
        'mz_stub_declared_bytes':declared_mz,
        'le_header_offset':le, 'le_cpu_type':u16(data,le+8),
        'le_os_type':u16(data,le+10), 'module_flags':u32(data,le+0x10),
        'page_size':page_size, 'last_page_bytes':real_last, 'number_of_pages':page_count,
        'data_pages_file_offset':data_start,
        'object_table_file_offset':le+object_offset,
        'page_map_file_offset':le+map_offset,
        'entry_object':entry_obj, 'entry_object_offset':entry_off,
        'entry_file_offset':entry_file,
        'entry_first_16_bytes_hex':data[entry_file:entry_file+16].hex(),
        'stack_object':u32(data,le+0x20), 'stack_offset':u32(data,le+0x24),
        'fixup_section_size':u32(data,le+0x30),
        'fixup_page_table_file_offset':le+u32(data,le+0x68),
        'fixup_records_file_offset':le+u32(data,le+0x6c),
        'objects':objects,
        'physical_page_numbers_sequential':all(p['physical_page']==i+1 and p['flags']==0
                                                for i,p in enumerate(pages)),
        'page_map':pages,
        'notes':[
            'LE header and mapped entry point validated; relocation fixups are NOT decoded.',
            'LE is a 32-bit capable format; this report does not independently prove DOS opcode 9 behavior.',
            'Object extraction contains original copyrighted executable bytes: do not redistribute.',
        ],
    }

def initialized_object_bytes(data, report, number):
    obj = report['objects'][number-1]
    part = bytearray()
    for index in range(obj['first_page_map_index']-1,
                       obj['first_page_map_index']-1+obj['mapped_page_count']):
        item = report['page_map'][index]
        if item['flags'] == 3:
            part.extend(b'\x00' * report['page_size'])
        else:
            n = item['physical_page']
            start = report['data_pages_file_offset'] + (n-1)*report['page_size']
            last = report['last_page_bytes'] if n == report['number_of_pages'] else report['page_size']
            part.extend(data[start:start+last])
    return bytes(part[:obj['virtual_size']])

def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('exe', type=Path, help='Path to personally owned ROTH.EXE')
    ap.add_argument('--json', type=Path, dest='json_file')
    ap.add_argument('--extract-dir', type=Path, help='Private object extraction; never upload outputs')
    args = ap.parse_args(argv)
    try:
        if args.exe.stat().st_size > LIMIT:
            raise ValueError('Executable larger than 64 MiB refused')
        data = args.exe.read_bytes()
        report = verify(data)
        # Path-independent report permits reproducible hash comparison across systems.
        print(json.dumps(report,indent=2,sort_keys=True))
        if args.json_file:
            args.json_file.write_text(json.dumps(report,indent=2,sort_keys=True)+'\n',encoding='utf-8')
        if args.extract_dir:
            args.extract_dir.mkdir(parents=True,exist_ok=True)
            for obj in report['objects']:
                private_path = args.extract_dir / f"object_{obj['number']:02d}_UNRELOCATED.bin"
                private_path.write_bytes(initialized_object_bytes(data,report,obj['number']))
                print(f'Private extraction: {private_path}',file=sys.stderr)
        return 0
    except (ValueError,OSError,IndexError) as exc:
        print('LE inspection failed: '+str(exc),file=sys.stderr)
        return 2

if __name__=='__main__':
    raise SystemExit(main())