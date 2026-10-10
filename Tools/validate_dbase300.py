#!/usr/bin/env python3
import struct,sys
p=sys.argv[1]; d=open(p,'rb').read()
if d[:8]!=b'DBASE300': raise SystemExit('bad signature')
arg=int(sys.argv[2]) if len(sys.argv)>2 else 2803984
off=arg*8
size=struct.unpack_from('<I',d,off)[0]; typ=struct.unpack_from('<I',d,off+4)[0]
if typ!=1: raise SystemExit('offset is not IMG1: '+hex(typ))
w,h=struct.unpack_from('<HH',d,off+8); pal=d[off+12:off+780]; rle=d[off+780:off+4+size]
out=[];i=0
while len(out)<w*h and i<len(rle):
    b=rle[i];i+=1;n=1
    if b>0xF0:n=b&15;b=rle[i];i+=1
    out.extend([b]*n)
print('IMG1',w,h,'pixels',len(out),'palette',len(pal),'rle_bytes_used',i)
if len(out)!=w*h or len(pal)!=768:raise SystemExit(2)
