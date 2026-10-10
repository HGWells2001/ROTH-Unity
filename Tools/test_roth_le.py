#!/usr/bin/env python3
"""Self-contained LE parser tests: generate fixtures, never package game bytes."""
from pathlib import Path
import importlib.util
import struct
import unittest

spec=importlib.util.spec_from_file_location('inspect_roth_le',Path(__file__).with_name('inspect_roth_le.py'))
mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)

def mock_image():
    b=bytearray(0x2200)
    b[:2]=b'MZ'
    struct.pack_into('<H',b,2,0)
    struct.pack_into('<H',b,4,1)
    struct.pack_into('<H',b,8,4)
    struct.pack_into('<I',b,0x3c,0x200)
    le=0x200
    b[le:le+4]=b'LE\x00\x00'
    struct.pack_into('<H',b,le+8,2)
    struct.pack_into('<I',b,le+0x14,2)
    struct.pack_into('<I',b,le+0x18,1)
    struct.pack_into('<I',b,le+0x1c,16)
    struct.pack_into('<I',b,le+0x28,4096)
    struct.pack_into('<I',b,le+0x2c,0x200)
    struct.pack_into('<I',b,le+0x40,0xc4)
    struct.pack_into('<I',b,le+0x44,1)
    struct.pack_into('<I',b,le+0x48,0xdc)
    struct.pack_into('<I',b,le+0x80,0x1000)
    struct.pack_into('<6I',b,le+0xc4,0x2000,0x10000,0x2045,1,2,0)
    b[le+0xdc:le+0xe4]=bytes.fromhex('0000010000000200')
    b[0x1010:0x1014]=b'TEST'
    b[0x2000:0x2004]=b'LAST'
    return b

class Tests(unittest.TestCase):
    def test_parse(self):
        r=mod.verify(mock_image())
        self.assertEqual((r['format'],r['entry_file_offset']),('MZ+LE',0x1010))
        self.assertEqual(r['entry_first_16_bytes_hex'][:8],b'TEST'.hex())
    def test_page_count(self):
        r=mod.verify(mock_image())
        self.assertEqual(r['number_of_pages'],2)
        self.assertTrue(r['physical_page_numbers_sequential'])
    def test_extract(self):
        b=mock_image()
        data=mod.initialized_object_bytes(b,mod.verify(b),1)
        self.assertEqual(len(data),0x1200)
        self.assertEqual(data[:0x20][16:20],b'TEST')
        self.assertEqual(data[4096:4100],b'LAST')
    def test_reject_corrupt_magic(self):
        b=mock_image();b[0x200:0x202]=b'LX'
        with self.assertRaisesRegex(ValueError,'No complete LE'):
            mod.verify(b)
    def test_reject_bad_eip(self):
        b=mock_image();struct.pack_into('<I',b,0x200+0x1c,0x2ffe)
        with self.assertRaisesRegex(ValueError,'outside its object'):
            mod.verify(b)
    def test_reject_unmapped_eip(self):
        b=mock_image();struct.pack_into('<I',b,0x200+0x1c,0x1100)
        # second page has physical 512 bytes, EIP offset 0x100 still valid
        self.assertEqual(mod.verify(b)['entry_file_offset'],0x2100)
        struct.pack_into('<I',b,0x200+0x1c,0x1300)
        with self.assertRaisesRegex(ValueError,'physical last-page'):
            mod.verify(b)
    def test_reject_invalid_physical_page(self):
        b=mock_image();b[0x200+0xdc:0x200+0xe0]=bytes.fromhex('00000300')
        with self.assertRaisesRegex(ValueError,'invalid physical page'):
            mod.verify(b)
    def test_reject_truncated_data(self):
        with self.assertRaisesRegex(ValueError,'Truncated'):
            mod.verify(mock_image()[:-8])
    def test_reject_invalid_little_endian(self):
        b=mock_image();b[0x202]=1
        with self.assertRaisesRegex(ValueError,'little-endian'):
            mod.verify(b)
    def test_known_signature_has_expected_shape(self):
        self.assertEqual(len(mod.verify(mock_image())['sha256']),64)

if __name__=='__main__':unittest.main(verbosity=2)