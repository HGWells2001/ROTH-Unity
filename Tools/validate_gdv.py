#!/usr/bin/env python3
import argparse,struct,collections,os

def main():
 p=argparse.ArgumentParser(description='Validate ROTH GDV structure without decoding assets for redistribution.')
 p.add_argument('gdv',nargs='+');a=p.parse_args();bad=0
 for path in a.gdv:
  try:
   b=open(path,'rb').read();
   if len(b)<24: raise ValueError('short header')
   sig,sizeid,nf,fr,sflags,freq,itype,fsize,unk,loss,w,h=struct.unpack_from('<IHHHHHHHBBHH',b,0)
   if sig!=0x29111994: raise ValueError('bad signature')
   pos=24+(768 if itype&1 else 0);alen=0
   if sflags&1:
    if fr==0: raise ValueError('zero framerate')
    alen=freq//fr
    if sflags&2:alen*=2
    if sflags&4:alen*=2
    if sflags&8:alen//=2
   types=collections.Counter();frames=0
   for i in range(nf):
    pos+=alen
    if fsize:
     if pos+8>len(b): raise ValueError('truncated frame header')
     fsig,l,t=struct.unpack_from('<HHI',b,pos);pos+=8
     if pos+l>len(b): raise ValueError('truncated frame payload')
     types[t&15]+=1;pos+=l;frames+=1
   if pos!=len(b): raise ValueError('size mismatch parsed=%d file=%d'%(pos,len(b)))
   print('%s: OK %dx%d frames=%d/%d fps=%d audio=%s types=%s'%(os.path.basename(path),w,h,frames,nf,fr,'yes' if sflags&1 else 'no',dict(types)))
  except Exception as e:
   bad+=1;print('%s: FAIL %s'%(path,e))
 raise SystemExit(1 if bad else 0)
if __name__=='__main__':main()
