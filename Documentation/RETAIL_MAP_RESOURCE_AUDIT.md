# Retail map -> primary DAS audit

Derived with the same strategy as `RothMapResourceResolver`: unique RAW texture/object references are compared against DEMO.DAS .. DEMO4.DAS FAT entries, keeping the first candidate on exact ties.

| Map | Best DAS | Coverage |
|---|---|---:|
| ABAGATE2 | DEMO3.DAS | 25/25 (100.0%) |
| AELF | DEMO4.DAS | 117/117 (100.0%) |
| ANUBIS | DEMO2.DAS | 99/99 (100.0%) |
| AQUA1 | DEMO1.DAS | 90/90 (100.0%) |
| AQUA2 | DEMO1.DAS | 73/73 (100.0%) |
| CAVERNS | DEMO.DAS | 75/75 (100.0%) |
| CAVERNS2 | DEMO4.DAS | 27/27 (100.0%) |
| CAVERNS3 | DEMO4.DAS | 178/178 (100.0%) |
| CHURCH1 | DEMO1.DAS | 311/312 (99.7%) |
| DOMINION | DEMO.DAS | 23/23 (100.0%) |
| DOPPLE | DEMO3.DAS | 95/95 (100.0%) |
| ELOHIM1 | DEMO1.DAS | 41/41 (100.0%) |
| GNARL1 | DEMO2.DAS | 32/32 (100.0%) |
| GRAVE | DEMO4.DAS | 105/105 (100.0%) |
| LRINTH | DEMO1.DAS | 30/30 (100.0%) |
| LRINTH1 | DEMO1.DAS | 30/30 (100.0%) |
| MAS3 | DEMO4.DAS | 74/74 (100.0%) |
| MAS4 | DEMO4.DAS | 40/40 (100.0%) |
| MAS6 | DEMO4.DAS | 35/35 (100.0%) |
| MAS7 | DEMO4.DAS | 126/126 (100.0%) |
| MAUSO1EA | DEMO4.DAS | 113/113 (100.0%) |
| MAUSO1EB | DEMO4.DAS | 62/62 (100.0%) |
| MAZE | DEMO4.DAS | 39/40 (97.5%) |
| OPTEMP1 | DEMO.DAS | 33/33 (100.0%) |
| RAQUIA1 | DEMO1.DAS | 44/44 (100.0%) |
| RAQUIA2 | DEMO1.DAS | 47/47 (100.0%) |
| RAQUIA3 | DEMO1.DAS | 71/71 (100.0%) |
| RAQUIA4 | DEMO1.DAS | 31/31 (100.0%) |
| RAQUIA5 | DEMO1.DAS | 121/121 (100.0%) |
| SALVAT | DEMO3.DAS | 86/86 (100.0%) |
| SOULST2 | DEMO1.DAS | 63/63 (100.0%) |
| SOULST3 | DEMO1.DAS | 67/67 (100.0%) |
| STUDY1 | DEMO.DAS | 339/340 (99.7%) |
| STUDY2 | DEMO.DAS | 152/152 (100.0%) |
| STUDY3 | DEMO.DAS | 123/123 (100.0%) |
| STUDY4 | DEMO.DAS | 129/129 (100.0%) |
| TEMPLE1 | DEMO.DAS | 84/84 (100.0%) |
| TGATE1F | DEMO2.DAS | 34/34 (100.0%) |
| TGATE1G | DEMO2.DAS | 38/38 (100.0%) |
| TGATE1H | DEMO1.DAS | 28/28 (100.0%) |
| TGATE1I | DEMO1.DAS | 21/21 (100.0%) |
| TOWER1 | DEMO2.DAS | 42/42 (100.0%) |
| VICAR | DEMO1.DAS | 164/165 (99.4%) |
| VICAR1 | DEMO1.DAS | 98/98 (100.0%) |

Warp commands scanned separately: 127.
All named warp targets resolve to an installed RAW except `MAS5`, referenced once from `MAUSO1EA`.
The runtime does not guess an alias for MAS5; it logs the missing target and leaves the current map loaded.
