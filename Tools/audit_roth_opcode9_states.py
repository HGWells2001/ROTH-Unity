#!/usr/bin/env python3
"""Static and testable opcode 9 state-machine evidence from a user-owned ROTH.EXE.

No runtime emulation is claimed. Checks byte signatures of the known binary and
relocated dispatchers; outputs an address-only, human-readable JSON analysis.

python Tools/audit_roth_opcode9_states.py path/to/ROTH.EXE --json state_report.json
"""
import argparse
import json
from pathlib import Path
import sys

from inspect_roth_le import verify, initialized_object_bytes
from inspect_roth_fixups import parse_fixups
from audit_roth_opcode9 import analyze as verify_dispatchers, index_relocations

# Signatures are complete machine instructions at established instruction boundaries.
# The site+bytes pair prevents a coincidental matching sequence in the larger binary.
SITES = {
    'initializer_preexisting_state_test': (0x22A9B, 'f6 46 02 20'),
    'initializer_axis_test': (0x22AE3, 'f6 46 06 40'),
    'initializer_initial_direction_test': (0x22B99, 'f6 46 02 02'),
    'initializer_set_direction_0x80': (0x22B9F, 'b2 80'),
    'retrigger_requires_zero_timeout': (0x22BB1, '66 83 7e 0e 00'),
    'retrigger_requires_repeat_flag': (0x22BB8, 'f6 46 06 20'),
    'retrigger_flip_animation_direction': (0x22BCA, '80 70 05 80'),
    'retrigger_flip_command_initial_state': (0x22BCE, '80 76 02 02'),
    'callback_inactive_gate': (0x22BDC, 'f6 47 02 08'),
    'callback_tick_read': (0x22BEB, 'a1 0c 57 01 00'),
    'callback_wait_flag_0x40': (0x22BF0, 'f6 46 05 40'),
    'callback_direction_flag_0x80': (0x22BFA, 'f6 46 05 80'),
    'callback_forward_byte_speed': (0x22C00, '0f b6 57 07 0f af c2'),
    'callback_forward_target_start': (0x22C0A, '0f bf 4f 0a 01 c9 f7 d9'),
    'callback_reverse_byte_speed_negation': (0x22C3D, '0f b6 57 07 0f af c2 f7 d8'),
    'callback_reverse_target_end': (0x22C49, '0f bf 4f 0c 01 c9 f7 d9'),
    'callback_move_first_coordinate': (0x22C7A, '66 8b 04 24 66 8b 56 0e 66 01 04 1a'),
    'callback_move_second_coordinate': (0x22C86, '66 8b 56 10 66 01 04 1a'),
    'callback_move_third_coordinate': (0x22C8E, '66 8b 56 14 66 01 04 1a'),
    'callback_move_fourth_coordinate': (0x22C96, '66 8b 56 12 66 01 04 1a'),
    'completed_special_path_test': (0x22FDA, 'f6 46 05 10'),
    'completed_repeat_test': (0x22FE2, 'f6 47 06 20'),
    'completed_timeout_test': (0x22FE9, '09 d2'),
    'completed_return_or_repeat_path': (0x2300B, '80 76 05 e0 66 89 56 06'),
    'completed_repeat_with_phase_finish': (0x23002, '80 67 02 de 83 c8 ff'),
    'completed_toggle_initial_direction': (0x23017, '80 67 02 de 80 77 02 02'),
    'completed_mark_disabled': (0x23024, '80 67 02 de 80 4f 02 08'),
    'waiting_subtract_elapsed': (0x23033, '66 29 46 06'),
    'waiting_signed_positive_branch': (0x23037, '0f 8f 5f fe ff ff'),
    'waiting_clear_flag_0x40': (0x2303D, '80 66 05 bf'),
}

class SignatureMismatch(ValueError):
    pass


def complete(flags, command_flags, command_timeout):
    """Translate ONLY the verified 0x22FDA..0x23032 completion basic blocks.

    flags corresponds to animation[5], command_flags to command[6],
    command_timeout to command[0xE] (16-bit). Returns a new state snapshot.
    0 means continue animation; -1 means remove the animation record.
    """
    if not 0 <= flags <= 255 or not 0 <= command_flags <= 255 or not 0 <= command_timeout <= 65535:
        raise ValueError('invalid byte/word inputs')
    animation = flags
    cmd_state_and_mask = 0xff
    cmd_state_or_mask = 0
    cmd_state_xor_mask = 0
    timer = None
    if animation & 0x10:
        animation ^= 0x90
        branch, result = 'special_direction_flip', 0
    elif command_timeout:
        if (command_flags & 0x20) and (animation & 0x20):
            cmd_state_and_mask = 0xde
            branch, result = 'timeout_repeat_phase_finish', -1
        else:
            animation ^= 0xe0
            timer = command_timeout
            branch, result = 'arm_timeout_reverse', 0
    elif command_flags & 0x20:
        cmd_state_and_mask = 0xde
        cmd_state_xor_mask = 0x02
        branch, result = 'repeat_no_timeout_toggle_command', -1
    else:
        cmd_state_and_mask = 0xde
        cmd_state_or_mask = 0x08
        branch, result = 'no_repeat_no_timeout_disable_command', -1
    return dict(branch=branch, callback_return=result, animation_flags=animation,
                animation_timeout_word=timer, command_state_and_mask=cmd_state_and_mask,
                command_state_or_mask=cmd_state_or_mask,
                command_state_xor_mask=cmd_state_xor_mask)


def elapsed_wait(flags, remaining, elapsed):
    """Equivalent to `sub word [anim+6],ax; jg ...; and byte [anim+5],bf`.

    x86 signed JG considers signed *operands* (a > b), not an unsigned
    countdown. A single frame with out-of-range inputs may have unexpected
    behavior; no wall-clock frequency can be inferred from these instructions.
    """
    if not (0 <= flags <= 255 and 0 <= remaining <= 65535 and 0 <= elapsed <= 65535):
        raise ValueError('input must be 8-bit flags or 16-bit unsigned words')
    signed = lambda v: v if v < 32768 else v - 65536
    next_counter = (remaining - elapsed) & 0xffff
    waiting = signed(remaining) > signed(elapsed)
    return {'remaining_word':next_counter, 'animation_flags':flags if waiting else flags & 0xbf,
            'waiting':waiting, 'callback_return':0}


def audit(executable):
    base = verify_dispatchers(executable)
    le = verify(executable)
    code = initialized_object_bytes(executable, le, 1)
    relocation_index = index_relocations(parse_fixups(executable, le))
    mismatches = []
    for name, (pos, hex_bytes) in SITES.items():
        expected = bytes.fromhex(hex_bytes)
        if code[pos:pos + len(expected)] != expected:
            mismatches.append(f'{name} at object 1 + {pos:#x}')
    if mismatches:
        raise SignatureMismatch('Opcode 9 completion-site signature mismatch: '+', '.join(mismatches))
    frame_reference = relocation_index.get((1, 0x22BEC))
    if not frame_reference or (frame_reference['target_type'], frame_reference['target_index'],
        frame_reference['target_object_offset']) != (0, 3, 0x1570C):
        raise SignatureMismatch('Opcode 9 update lacks the expected relocated frame-delta global')
    if base['opcode_9_update_object_offset'] != 0x22BD9:
        raise SignatureMismatch('Opcode 9 dispatcher no longer resolves to object 1 + 0x22BD9')
    rows=[]
    for delay in (0, 12):
        for repeat in (False, True):
            for phase in (False, True):
                r=complete(0x20 if phase else 0,0x20 if repeat else 0, delay)
                rows.append(dict(timeout=delay, repeat_bit_0x20=repeat,
                                 animation_phase_bit_0x20=phase, **r))
    return {
        'exe_sha256':le['sha256'],
        'opcode_9_init_object1_offset':'0x22a99',
        'opcode_9_update_object1_offset':'0x22bd9',
        'shared_completion_object1_range':['0x22fd1','0x23032'],
        'callback_tick_wait_object1_range':['0x23033','0x23041'],
        'frame_delta_global':'object3+0x1570c',
        'proven_coordinate_target_formula':'low16(-2 * signed16(command word at +0xa or +0xc)); compare uses signed 16-bit',
        'verified_signature_count':len(SITES),
        'completion_state_truth_table':rows,
        'limitations':[
            'The truth table models completion branches, not every in-flight or collision path.',
            'The repeat flag is a command bit and not directly equivalent to Unity Repeat.',
            'The true DOS counter frequency, command-state transitions between triggers, and signed coordinate conversion to Unity remain unverified.',
            'An apparent loop in this isolated routine is not proof of repeated movement in the complete DOS runtime.',
            'No original executable bytes are included in the report.'
        ]
    }


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('exe',type=Path)
    parser.add_argument('--json',type=Path)
    opts=parser.parse_args(argv)
    try:
        result=audit(opts.exe.read_bytes())
        output=json.dumps(result,indent=2)
        print(output)
        if opts.json:opts.json.write_text(output+'\n',encoding='utf-8')
        return 0
    except (ValueError,IndexError,OSError) as e:
        print('Opcode9 state audit failed: '+str(e),file=sys.stderr)
        return 2

if __name__=='__main__':raise SystemExit(main())
