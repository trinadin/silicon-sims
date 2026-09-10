// Round 63 IFF-LITERAL SLOT/SPR census: tools/iff_slot_spr_census.py over game-data/The Sims.
// SLOT chunk instances + slot entries (engine SLOT.Read numSlots u32 at payload+12); SPR#
// chunk instances + frames (spriteCount u32 at +4, BE if v1==0); SPR2 chunks + frames
// (spriteCount u32 at +4 for v1000, +8 for v1001). Per member basename LAST-wins; chunks
// distinct by (type,id) = one instance each. IFF data - object slot tables and sprite-frame
// surfaces.
public const int SlotChunkCensus = 1472;
public const int SlotEntryCensus = 6969;
public const int SprChunkCensus = 758;
public const int SprFrameCensus = 3120;
public const int Spr2ChunkCensus = 17140;
public const int Spr2FrameCensus = 138948;
