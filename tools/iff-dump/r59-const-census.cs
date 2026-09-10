// Round 59 IFF-LITERAL constant-table census: tools/iff_consts_census.py over game-data/The Sims.
// BCON = engine BCON.Read blocks (num bytes; distinct ids), TPRP = pCount+lCount (distinct ids),
// GLOB = INSTANCE count (both "GLOB" and lowercase "glob" - engine IffFile registers both and
// List counts instances, e.g. NPC_Superstar_PA/NPC_Vacation_Director carry GLOB+glob id 128).
// Basename LAST-wins; IFF data, engine-independent.
public const int BconChunkCensus = 3663;
public const int BconBlockCensus = 29952;
public const int TprpChunkCensus = 206;
public const int TprpBlockCensus = 434;
public const int GlobChunkCensus = 478;
