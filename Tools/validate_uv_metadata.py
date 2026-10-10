#!/usr/bin/env python3
"""Audit ROTH RAW texture-mapping extension records (type bit 7)."""
import struct, sys
from pathlib import Path

def audit(path):
    data=Path(path).read_bytes()
    if len(data)<30: raise ValueError('RAW too small')
    h=struct.unpack_from('<15H', data, 0)
    mapping_off=h[4]
    count=struct.unpack_from('<H', data, mapping_off-2)[0]
    pos=mapping_off; extra=shifted=0
    for _ in range(count):
        if pos+10>len(data): raise ValueError('mapping section truncated')
        typ=data[pos+1]; pos+=10
        if typ & 0x80:
            if pos+4>len(data): raise ValueError('extended mapping truncated')
            sx,sy,_fid=struct.unpack_from('<bbH',data,pos); pos+=4
            extra+=1
            shifted += int(bool(sx or sy))
    print(f'{Path(path).name}: mappings={count}, extended={extra}, nonzero_shifts={shifted}, parsed_end=0x{pos:04X}')

if __name__=='__main__':
    if len(sys.argv)<2:
        raise SystemExit('usage: validate_uv_metadata.py MAP.RAW [MAP2.RAW ...]')
    for p in sys.argv[1:]: audit(p)
