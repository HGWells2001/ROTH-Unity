#!/usr/bin/env python3
import struct,sys
p=sys.argv[1] if len(sys.argv)>1 else 'DBASE100.DAT'
d=open(p,'rb').read()
if d[:8]!=b'DBASE100': raise SystemExit('bad signature')
vals=struct.unpack_from('<11I',d,8); action_count, action_off=vals[4],vals[5]
offs=struct.unpack_from('<%dI'%action_count,d,action_off)
actions=[]
for idx,off in enumerate(offs,1):
    cmds=[]
    if off:
        length=struct.unpack_from('<H',d,off)[0]; pos=off+4
        for _ in range(max(0,length//4-1)):
            b0,b1,b2,op=struct.unpack_from('<BBBB',d,pos);pos+=4;cmds.append((op,b0|(b1<<8)|(b2<<16)))
    actions.append(cmds)
choice=bad_choice=random_blocks=flat_random=complex_random=0
for idx,cmds in enumerate(actions,1):
    ops=[x[0] for x in cmds]
    if 9 in ops:
        choice+=1; first=ops.index(9); options=sum(o==8 for o in ops[:first]); branches=0;depth=0;bad=0
        for o in ops[first:]:
            if o==9:
                if depth==0: branches+=1
                depth+=1
            elif o==10:
                depth-=1
                if depth<0:bad+=1;depth=0
        if options!=branches or depth or bad:
            bad_choice+=1;print('BAD CHOICE',idx,'options',options,'branches',branches,'depth',depth)
    i=0
    while i<len(cmds):
        if cmds[i][0]==11:
            j=i+1
            while j<len(cmds) and cmds[j][0]!=12:j+=1
            if j<len(cmds):
                random_blocks+=1; inside=[o for o,a in cmds[i+1:j]]
                if all(o in {5,7,17,25,145} for o in inside):flat_random+=1
                else:complex_random+=1
                i=j
        i+=1
print('actions',len(actions))
print('choice_actions',choice,'bad_choice_actions',bad_choice)
print('random_blocks',random_blocks,'flat_safe',flat_random,'complex_observed',complex_random)
if bad_choice: raise SystemExit(2)
