#!/usr/bin/env python3
"""Read-only audit of the original ROTH DOS PIT timer and RAW9 tick source.

Usage: python Tools/audit_roth_pit_timer.py /path/to/ROTH.EXE --json report.json

Offsets are LE *object 1* relative. The report contains addresses and derived
constants, never extracted executable code, map assets or disassembly bytes.
"""
import argparse
import json
from pathlib import Path
import sys

from inspect_roth_le import verify, initialized_object_bytes
from inspect_roth_fixups import parse_fixups
from audit_roth_opcode9 import index_relocations, analyze as verify_dispatchers
from audit_roth_opcode9_states import audit as verify_states

# An IBM-compatible PIT channel 0 is conventionally clocked at this frequency.
# The value is a hardware reference, NOT a constant read from ROTH.EXE.
PIT_INPUT_HZ = 1193182.0

# Each signature starts at a known *instruction* boundary. Sites represent:
# IRQ0 installation, initial programming, interrupt-time reprogramming,
# counter update and game-loop time-delta reads/stores.
SITES = {
    'irq0_vector_8_with_handler': (0x2532, 'b8 08 00 00 00 bb 31 24 00 00'),
    'startup_pit_reload': (0x255a, 'b0 3e e6 43 b0 42 e6 40 e6 40'),
    'isr_pit_reload': (0x2438, 'b0 3e e6 43 b0 16 e6 40 b0 42 e6 40'),
    'irq0_callback_increment': (0x23de, '66 ff 05 ac 0f 02 00'),
    'irq0_wrapper_calls_counter': (0x2426, 'e8 b3 ff ff ff'),
    'irq0_handler_calls_counter': (0x2454, 'e8 85 ff ff ff'),
    'irq0_exit': (0x248d, 'cf'),
    'command_dialog_sets_counter': (0x1325, '66 a3 ac 0f 02 00'),
    'command_dialog_adds_four': (0x1362, '66 83 05 02 6c 00 00 04'),
    'command_dialog_counter_write': (0x1370, '66 a3 ac 0f 02 00'),
    'main_loop_counter_read_a': (0x102d2, '0f bf 05 ac 0f 02 00'),
    'main_loop_delta_write_a': (0x102de, '89 15 0c 57 01 00'),
    'main_loop_counter_read_b': (0x1046d, '0f bf 05 ac 0f 02 00'),
    'main_loop_delta_write_b': (0x10479, '89 15 0c 57 01 00'),
    'raw9_frame_delta_read': (0x22beb, 'a1 0c 57 01 00'),
}

# Source addresses must be relocated to the *data object* (object 3).
TARGET_FIXUPS = {
    'irq0_install_handler': (0x2538, 1, 0x2431),
    'irq0_increment_counter': (0x23e1, 3, 0x20fac),
    'dialog_counter_write_a': (0x1327, 3, 0x20fac),
    'dialog_counter_write_b': (0x1372, 3, 0x20fac),
    'main_loop_counter_read_a': (0x102d5, 3, 0x20fac),
    'main_loop_counter_read_b': (0x10470, 3, 0x20fac),
    'main_loop_delta_write_a': (0x102e0, 3, 0x1570c),
    'main_loop_delta_write_b': (0x1047b, 3, 0x1570c),
    'raw9_frame_delta_read': (0x22bec, 3, 0x1570c),
}

class TimerAuditMismatch(ValueError):
    pass


def pit_control_word(value):
    """Interpret 8254 counter selection and alias mode 6/7 -> 2/3."""
    if not isinstance(value, int) or value < 0 or value > 255:
        raise ValueError('control word must be one unsigned byte')
    mode = (value >> 1) & 7
    return {'counter': (value >> 6) & 3,
            'access_mode': (value >> 4) & 3,
            'mode_bits': mode,
            'canonical_mode': mode - 4 if mode in (6, 7) else mode,
            'bcd': bool(value & 1)}


def hz_from_divisor(divisor, input_hz=PIT_INPUT_HZ):
    if not isinstance(divisor, int) or not 1 <= divisor <= 65536:
        raise ValueError('invalid reload divisor')
    if not (0 < input_hz < float('inf')):
        raise ValueError('invalid input PIT frequency')
    return input_hz / divisor


def signed16(value):
    if not isinstance(value, int) or not 0 <= value <= 65535:
        raise ValueError('counter values must be unsigned 16-bit')
    return value if value < 32768 else value - 65536


def original_frame_delta(counter_now, prior_snapshot):
    """movsx eax,word counter; sub edx,previous_sign_extended_snapshot.

    This is NOT an unsigned wrap-safe difference. A sign-boundary crossing can
    create a very large delta if the previous snapshot came from the other side.
    """
    return signed16(counter_now) - signed16(prior_snapshot)


def verify_sites(code, relocation_index):
    for label, (offset, signature) in SITES.items():
        expected = bytes.fromhex(signature)
        if code[offset:offset + len(expected)] != expected:
            raise TimerAuditMismatch(f'changed timer instruction at object1+{offset:#x}: {label}')
    for label, (source, target_object, target_offset) in TARGET_FIXUPS.items():
        record = relocation_index.get((1, source))
        if not record or (record['source_type'], record['target_type'],
                          record['target_index'], record['target_object_offset']) != (
                            7, 0, target_object, target_offset):
            raise TimerAuditMismatch(f'wrong relocation for {label} at object1+{source:#x}')
    if pit_control_word(0x3e) != {
        'counter': 0, 'access_mode': 3, 'mode_bits': 7, 'canonical_mode': 3, 'bcd': False,
    }:
        raise TimerAuditMismatch('PIT mode decode invalid')
    return len(SITES), len(TARGET_FIXUPS)


def audit(executable):
    le = verify(executable)
    # Fail closed on the complete versioned RAW9 dispatcher and state signatures.
    dispatch = verify_dispatchers(executable)
    states = verify_states(executable)
    code = initialized_object_bytes(executable, le, 1)
    index = index_relocations(parse_fixups(executable, le))
    site_count, fixup_count = verify_sites(code, index)
    initial_divisor = code[0x255f] | (code[0x255f] << 8)  # AL is reused by both OUT 0x40 instructions
    irq_divisor = code[0x243d] | (code[0x2441] << 8)
    if (initial_divisor, irq_divisor) != (0x4242, 0x4216):
        raise TimerAuditMismatch('PIT reload constants unexpectedly changed')
    control = pit_control_word(code[0x2439])
    if dispatch['opcode_9_update_object_offset'] != 0x22bd9:
        raise TimerAuditMismatch('not the verified RAW9 callback')
    if states['verified_signature_count'] != 30:
        raise TimerAuditMismatch('RAW9 state audit has changed')
    return {
        'exe_sha256': le['sha256'],
        'status': 'PIT programming and counter provenance verified statically; runtime scheduling not measured',
        'verified_timer_instruction_sites': site_count,
        'verified_data_relocations': fixup_count,
        'pit_control_word_hex': '0x3e',
        'pit_control_decoded': control,
        'pit_reference_input_hz': PIT_INPUT_HZ,
        'initial_divisor': initial_divisor,
        'initial_nominal_hz': hz_from_divisor(initial_divisor),
        'irq_handler_reload_divisor': irq_divisor,
        'irq_handler_nominal_hz': hz_from_divisor(irq_divisor),
        'irq_handler_nominal_tick_seconds': 1 / hz_from_divisor(irq_divisor),
        'irq_vector': 8,
        'irq_entry_object1_offset': '0x2431',
        'irq_counter_increment_object1_offset': '0x23de',
        'counter_relocation_target': 'LE object3 + 0x20FAC',
        'delta_relocation_target': 'LE object3 + 0x1570C',
        'raw9_updater_object1_offset': '0x22bd9',
        'counter_16bit_wrap_seconds_nominal': 65536 / hz_from_divisor(irq_divisor),
        'other_counter_writes': ['object1+0x1325 (dialog reset)',
                                 'object1+0x1370 (dialog +4-counter path)'],
        'coordinate_evidence': {
            'axis_select': 'command options +6 bit 0x40 chooses offset +8 or +10 in the source vertex references',
            'target_unsigned_word_wrapped': 'low16(-2 * signed16(command words +0xA/+0xC))',
            'forward_step': 'frame_delta * unsigned_byte(command+7), truncated to 16-bit coordinate arithmetic',
            'reverse_step': 'negative of forward step, also truncated to 16-bit coordinate arithmetic',
            'vertex_updates': 4,
            'note': 'Do not replace Unity end-start formula until the map vertex mapping is validated with retail data.'
        },
        'limitations': [
            '70.527 Hz is an inferred nominal PIT IRQ rate; not a measured emulator/real-machine runtime cadence.',
            '0x4242 startup and 0x4216 interrupt-time reload both occur in the original binary.',
            'The signed-16 main-loop subtraction can jump at sign boundary/wrap; do not silently reinterpret as unsigned modulo.',
            'Alternate dialog/debug code can assign a counter and advance by four rather than by the interrupt alone.',
            'Full original physical timing and RAW9 geometry/state behavior still need DOS runtime capture and map playtests.',
        ],
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('exe', type=Path, help='Unmodified, user-owned ROTH.EXE')
    ap.add_argument('--json', type=Path, help='Save address-only JSON')
    args = ap.parse_args(argv)
    try:
        result = audit(args.exe.read_bytes())
        report = json.dumps(result, indent=2)
        print(report)
        if args.json:
            args.json.write_text(report + '\n', encoding='utf-8')
        return 0
    except (ValueError, IndexError, OSError) as exc:
        print(f'ROTH timer audit failed: {exc}', file=sys.stderr)
        return 2

if __name__ == '__main__':
    raise SystemExit(main())
