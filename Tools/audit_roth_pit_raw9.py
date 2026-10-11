#!/usr/bin/env python3
"""Read-only ground-truth audit of ROTH.EXE PIT IRQ0 and RAW opcode 9 math.

Uses only original owner-supplied executable, never redistributes its contents.
Emits only original-object addresses, numeric constants and calculated rates.
"""
import argparse
import json
from pathlib import Path
import sys

from inspect_roth_le import verify, initialized_object_bytes
from inspect_roth_fixups import parse_fixups
from audit_roth_opcode9 import analyze as audit_dispatch, index_relocations
from audit_roth_opcode9_states import audit as audit_states

PIT_INPUT_HZ = 1193182  # conventional nominal IBM PC PIT clock
COUNTER_OBJECT = 3
COUNTER_OFFSET = 0x20FAC
DELTA_OBJECT = 3
DELTA_OFFSET = 0x1570C

# All bytes below are small structural checks of instruction sites, not a
# disassembly export. Sites are relative to the LE object's start.
SITES = {
    'irq0_installer_vector_8': (0x2532, 'b8 08 00 00 00 bb 31 24 00 00'),
    'irq0_hook_request': (0x2550, 'b8 08 00 00 00 e8'),
    'irq0_init_pit_command': (0x255A, 'b0 3e e6 43'),
    'irq0_init_divisor': (0x255E, 'b0 42 e6 40 e6 40'),
    'irq0_handler_pit_command': (0x2438, 'b0 3e e6 43'),
    'irq0_handler_divisor': (0x243C, 'b0 16 e6 40 b0 42 e6 40'),
    'irq0_handler_core_call': (0x2454, 'e8 85 ff ff ff'),
    'irq0_handler_end': (0x2482, 'b0 20 e6 20'),
    'tick_increment': (0x23DE, '66 ff 05 ac 0f 02 00'),
    'clock_read_loop_a': (0x102D2, '0f bf 05 ac 0f 02 00'),
    'delta_store_loop_a': (0x102DE, '89 15 0c 57 01 00'),
    'clock_read_loop_b': (0x1046D, '0f bf 05 ac 0f 02 00'),
    'delta_store_loop_b': (0x10479, '89 15 0c 57 01 00'),
    'pit_reset_default_divisor': (0x25BC, 'b0 3e e6 43 b0 ff e6 40 e6 40'),
    'raw9_speed_forward': (0x22C00, '0f b6 57 07 0f af c2'),
    'raw9_target_forward': (0x22C0A, '0f bf 4f 0a 01 c9 f7 d9'),
    'raw9_signed_clamp_forward': (0x22C2F, '66 39 c8 7e 07'),
    'raw9_speed_reverse': (0x22C3D, '0f b6 57 07 0f af c2 f7 d8'),
    'raw9_target_reverse': (0x22C49, '0f bf 4f 0c 01 c9 f7 d9'),
    'raw9_signed_clamp_reverse': (0x22C6E, '66 39 c8 7d 07'),
}

# Check the semantic identity of the absolute address operands using LE
# relocations; the same immediate byte sequence alone does not prove linkage.
RELOCATIONS = {
    'tick_increment': (0x23E1, COUNTER_OBJECT, COUNTER_OFFSET),
    'clock_read_loop_a': (0x102D5, COUNTER_OBJECT, COUNTER_OFFSET),
    'clock_read_loop_b': (0x10470, COUNTER_OBJECT, COUNTER_OFFSET),
    'delta_store_loop_a': (0x102E0, DELTA_OBJECT, DELTA_OFFSET),
    'delta_store_loop_b': (0x1047B, DELTA_OBJECT, DELTA_OFFSET),
}

class EvidenceMismatch(ValueError):
    pass

def int16(n):
    n &= 0xffff
    return n if n < 0x8000 else n - 0x10000

def target_from_command(raw_signed_word):
    """The x86 expression movsx; add ecx,ecx; neg ecx; cmp AX,CX."""
    if not -32768 <= raw_signed_word <= 32767:
        raise ValueError('command bound must be signed 16-bit')
    return int16(-2 * raw_signed_word)

def axis_step(current, raw_bound, elapsed, speed_byte, reverse=False):
    """Replicate verified 16-bit RAW9 step/clamp basic blocks only.

    current is the signed value of the selected reference map vertex;
    the result is a signed delta to apply to the four reference coordinates.
    Does not model vertex-selection, trigger/retrigger or DOS object lifetime.
    """
    if not -32768 <= current <= 32767:
        raise ValueError('current must be signed16')
    if not 0 <= speed_byte <= 255:
        raise ValueError('speed must be unsigned8')
    if not -(1 << 31) <= elapsed < (1 << 31):
        raise ValueError('frame delta must be signed 32-bit')
    bound = target_from_command(raw_bound)
    if current == bound:
        return dict(delta=0, reached=True, clamped=False, target=bound)
    step = elapsed * speed_byte * (-1 if reverse else 1)
    candidate = int16(current + int16(step))
    clamped = (candidate < bound if reverse else candidate > bound)
    delta = int16(step - (candidate - bound)) if clamped else int16(step)
    # The output of the four vertex additions is 16-bit modulo arithmetic.
    reached = int16(current + delta) == bound
    return dict(delta=delta, reached=reached, clamped=clamped, target=bound)

def audit(data):
    parent = audit_states(data)  # also verifies full dual relocated command dispatch
    le = verify(data)
    code = initialized_object_bytes(data,le,1)
    relocs = index_relocations(parse_fixups(data,le))
    for name,(offset,encoding) in SITES.items():
        sig=bytes.fromhex(encoding)
        if code[offset:offset+len(sig)] != sig:
            raise EvidenceMismatch('instruction mismatch: '+name+' at obj1+'+hex(offset))
    for name,(pos,obj,offs) in RELOCATIONS.items():
        ref=relocs.get((1,pos))
        if not ref or (ref['target_type'],ref['target_index'],ref['target_object_offset'])!=(0,obj,offs):
            raise EvidenceMismatch('LE relocation mismatch: '+name+' at obj1+'+hex(pos))
    if parent['opcode_9_update_object1_offset'] != '0x22bd9':
        raise EvidenceMismatch('RAW9 animation dispatcher target differs')
    # The exact source writes mode control 0x3e then two bytes to port 0x40.
    initial = code[0x255f] | (code[0x255f] << 8)
    steady = code[0x243d] | (code[0x2441] << 8)
    return {
        'exe_sha256':le['sha256'],
        'evidence_sites':len(SITES),'verified_absolute_relocations':len(RELOCATIONS),
        'interrupt_vector':8,'interrupt_entry_object1_offset':'0x2431',
        'tick_increment_object1_offset':'0x23de',
        'timer_counter':'object3+0x20fac','timer_delta':'object3+0x1570c',
        'timer_width_bits':16,'pit_io_ports':{'command':0x43,'channel0':0x40},
        'pit_mode_control_hex':'0x3e','pit_mode':'3 (mode 7 alias), channel 0, lo/hi, binary',
        'initial_divisor':initial,'irq_reprogram_divisor':steady,
        'nominal_pit_input_hz':PIT_INPUT_HZ,
        'nominal_initial_irq_hz':PIT_INPUT_HZ/initial,
        'nominal_steady_irq_hz':PIT_INPUT_HZ/steady,
        'nominal_steady_tick_seconds':steady/PIT_INPUT_HZ,
        'irq_default_restore_divisor':65536,
        'raw9_forward_bound':'signed16(-2 * signed16(command[0xA:0xC]))',
        'raw9_reverse_bound':'signed16(-2 * signed16(command[0xC:0xE]))',
        'raw9_step':'signed16(frame_delta * unsigned8(command[7])) with optional negation, then signed16 clamp',
        'remaining_unknowns':[
            'Real timing may differ from nominal PIT arithmetic because IRQ handler reprograms the counter.',
            'The code uses signed-extension of the 16-bit timer word before subtraction; rollover behavior still requires tracing.',
            'Sector-local selected vertex references have not yet been mapped to Unity mesh indices.',
            'This analysis does not yet justify switching on DOS simulation in the Unity component.'
        ],
    }

def main(argv=None):
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('exe',type=Path)
    ap.add_argument('--json',type=Path)
    a=ap.parse_args(argv)
    try:
        report=audit(a.exe.read_bytes()); js=json.dumps(report,indent=2,sort_keys=True)
        print(js)
        if a.json:a.json.write_text(js+'\n',encoding='utf-8')
        return 0
    except (OSError,ValueError,IndexError) as ex:
        print('PIT/RAW9 audit failed: '+str(ex),file=sys.stderr)
        return 2

if __name__=='__main__':raise SystemExit(main())
