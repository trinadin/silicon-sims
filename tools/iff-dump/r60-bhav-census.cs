// Round 60 IFF-LITERAL BHAV census: tools/iff_bhav_census.py over game-data/The Sims.
// BHAV instruction count from the IFF chunk header (engine BHAV.Read mirror: 0x8000/0x8001
// u16 at payload+2; 0x8002 u16 at payload+2; 0x8003 u32 at payload+8). Basename LAST-wins;
// distinct (type,id). The instructions the behavior VM executes are IFF-literally IFF data.
public const int BhavInstructionCensus = 388503;
