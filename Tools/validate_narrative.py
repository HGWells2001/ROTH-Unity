#!/usr/bin/env python3
import argparse,struct,os

def main():
 ap=argparse.ArgumentParser();ap.add_argument('dbase100');ap.add_argument('dbase400');ap.add_argument('dbase500');a=ap.parse_args()
 d100=open(a.dbase100,'rb').read();d400=open(a.dbase400,'rb').read();d500=open(a.dbase500,'rb').read()
 if d100[:8]!=b'DBASE100':raise SystemExit('bad DBASE100 signature')
 if d400[:8]!=b'DBASE400':raise SystemExit('bad DBASE400 signature')
 if d500[:8]!=b'DBASE500':raise SystemExit('bad DBASE500 signature')
 vals=struct.unpack_from('<11I',d100,8);cutn,cuto=vals[6],vals[7]
 texts=voices=subbed=subentries=bad=0
 for i in range(cutn):
  o=cuto+i*20
  if o+20>len(d100):bad+=1;break
  name=d100[o:o+8].split(b'\0')[0].decode('ascii','ignore');_,slen,to,so=struct.unpack_from('<HHII',d100,o+8)
  if to:
   if to+8>len(d400):bad+=1
   else:
    vo,l,col=struct.unpack_from('<IHH',d400,to);texts+=1
    if to+8+l>len(d400):bad+=1
    if vo:
     p=vo*8
     if p+44<=len(d500) and d500[p:p+4]==b'FFIR':voices+=1
     else:bad+=1
  if so:
   subbed+=1;p=so;guard=10000
   while p+4<=len(d400) and guard:
    guard-=1;l,t=struct.unpack_from('<HH',d400,p);p+=4
    if t==0xffff:break
    if l==0:p-=2;continue
    if l<5 or p+(l-4)>len(d400):bad+=1;break
    p+=l-4
    if p&1:p+=1
    subentries+=1
 print('cutscenes=%d text_entries=%d voiced=%d subtitled=%d subtitle_entries=%d errors=%d'%(cutn,texts,voices,subbed,subentries,bad))
 raise SystemExit(1 if bad else 0)
if __name__=='__main__':main()
