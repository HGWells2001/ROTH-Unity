# DAS / DASP notes - milestone 0.3

The English retail map resources use Gremlin DASP archives such as `DEMO.DAS`.

The 68-byte DASP v5 header identifies the FAT offset, palette, filename table, directional/object sections and the four FAT block counts. FAT records are 8 bytes: data offset (u32), size (u16), flags1 (u8), flags2 (u8).

The first implementation intentionally decodes only ordinary paletted images. A standard image starts with modifier (u8), image type (u8), width (u16), height (u16), then width*height palette indexes. The embedded palette is 256 x RGB 6-bit values and is expanded with `(v * 259 + 33) >> 6`.

Resource classes currently detected but deferred:

- image_type bit 0: animation/compressed image sequence
- image_type bit 7: object data
- modifier bit 6: image pack
- FAT flags1 bit 5: directional/monster mapping rather than direct image payload

Map texture values also have direct palette-colour encodings. Community renderer behaviour distinguishes sector floor/ceiling colour values in 0xFF00..0xFFFF and wall colour values in 0x8000..0x80FF (with 0xFFFF also mapping to palette 255).

The implementation is read-only and never mutates original game files.
