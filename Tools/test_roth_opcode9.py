#!/usr/bin/env python3
"""Synthetic and negative tests. Never bundles the proprietary ROTH.EXE."""
import struct
import unittest
from inspect_roth_le import verify
from inspect_roth_fixups import parse_fixups, summary, DecodeError
from audit_roth_opcode9 import analyze, index_relocations, NotFound, DISPATCH_SIGNATURE, HANDLER_PREFIX, UPDATE_PREFIX


def mock_with_dispatcher():
    """MZ+LE fixture with 2 pages, 128 relocatable pointers and a synthetic handler."""
    img=bytearray(0x2200)
    img[:2]=b'MZ'
    struct.pack_into('<H',img,4,1)
    struct.pack_into('<H',img,8,4)
    struct.pack_into('<I',img,0x3c,0x200)
    hdr=0x200
    img[hdr:hdr+4]=b'LE\x00\x00'
    struct.pack_into('<H',img,hdr+8,2)
    struct.pack_into('<I',img,hdr+0x14,2)
    struct.pack_into('<I',img,hdr+0x18,1)
    struct.pack_into('<I',img,hdr+0x1c,0x100)
    struct.pack_into('<I',img,hdr+0x28,4096)
    struct.pack_into('<I',img,hdr+0x2c,512)
    struct.pack_into('<I',img,hdr+0x40,0xc4)
    struct.pack_into('<I',img,hdr+0x44,1)
    struct.pack_into('<I',img,hdr+0x48,0xdc)
    struct.pack_into('<I',img,hdr+0x68,0x250)
    struct.pack_into('<I',img,hdr+0x6c,0x400)
    struct.pack_into('<I',img,hdr+0x80,0x1000)
    struct.pack_into('<6I',img,hdr+0xc4,0x2000,0x10000,0x2045,1,2,0)
    img[hdr+0xdc:hdr+0xe4]=bytes.fromhex('0000010000000200')
    off=0x1000
    img[off+0x100:off+0x100+len(DISPATCH_SIGNATURE)]=DISPATCH_SIGNATURE
    # In the original program the address field is a real LE relocation,
    # NOT a trustworthy unrelocated immediate treated as final.
    struct.pack_into('<I',img,off+0x109,0x200)
    img[off+0x700:off+0x700+len(HANDLER_PREFIX)]=HANDLER_PREFIX
    img[off+0x800:off+0x800+len(UPDATE_PREFIX)]=UPDATE_PREFIX
    img[off+0x800+8:off+0x800+8+9]=bytes.fromhex('a1 0c 57 01 00 f6 46 05 40')
    img[off+0x800+17:off+0x800+17+7]=bytes.fromhex('0f b6 57 07 0f af c2')
    img[off+0x900:off+0x900+7]=bytes.fromhex('8b 78 08 f6 47 02 08')
    # 1 dispatcher fixup + 128 entries; LE relocation source 0x07, flags 0x10.
    entries=[(0x109,0x200)]
    for i in range(128):
        dest=0x700 if i==9 else 0x680
        if i==74:dest=0x800
        if i==76:dest=0x900
        struct.pack_into('<I',img,off+0x200+i*4,dest)
        entries.append((0x200+i*4,dest))
    record_start=hdr+0x400
    for n,(source,dest) in enumerate(entries):
        struct.pack_into('<BBHB I',img,record_start+n*9,7,0x10,source,1,dest)
    total=len(entries)*9
    struct.pack_into('<III',img,hdr+0x250,0,total,total)
    return img

class Tests(unittest.TestCase):
    def test_fixture_is_valid_le(self):
        self.assertEqual(verify(mock_with_dispatcher())['number_of_pages'],2)
    def test_fixup_records(self):
        img=mock_with_dispatcher();fix=parse_fixups(img,verify(img))
        self.assertEqual(len(fix),129)
        self.assertEqual(summary(fix,verify(img))['source_target_type_counts']['source_7_target_0'],129)
    def test_signature_and_reloc_verified(self):
        result=analyze(mock_with_dispatcher())
        self.assertEqual(result['dispatch_table_object_offset'],0x200)
        self.assertEqual(result['opcode_9_handler_object_offset'],0x700)
    def test_mask_128_entries_and_callbacks(self):
        result=analyze(mock_with_dispatcher())
        self.assertEqual(result['confirmed_dispatch_entries'],128)
        self.assertEqual([x['table_index'] for x in result['opcode_9_known_callback_entries']],[74,76])
    def test_speed_hint_is_not_assumed_from_pointer(self):
        result=analyze(mock_with_dispatcher())
        self.assertEqual([x['table_index'] for x in result['opcode_9_frame_speed_clues']],[74])
    def test_bad_handler_rejected(self):
        img=mock_with_dispatcher();img[0x1000+0x700]=0xff
        with self.assertRaises(NotFound):analyze(img)
    def test_corrupted_fixup_flags_rejected(self):
        img=mock_with_dispatcher();img[0x200+0x400]=0x01
        with self.assertRaises(DecodeError):analyze(img)
    def test_truncated_fixup_rejected(self):
        img=mock_with_dispatcher();struct.pack_into('<III',img,0x200+0x250,0,9999,9999)
        with self.assertRaises(DecodeError):analyze(img)

if __name__=='__main__': unittest.main(verbosity=2)
