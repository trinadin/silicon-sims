namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// The per-member bodystring record of an exported .FAM ('uChr' 0x75436872,
    /// r247-fam-import decode §3): one chunk per family member, chunk id = the
    /// member id shared with EXPi/NBRS/CTSS (Tutorial.FAM carries uChr 47/48).
    /// The payload is a plain STR format -3 string set ("bodystring" label, same
    /// shape as the STR# 200 rows of a character file — string 13 is the member
    /// kind string: "dog"/"cat" or anything-else = human). The native import
    /// clones the whole set into the recreated character's STR# 200. Inherits the
    /// STR codec (read/parse/write) verbatim, like FAMs.
    /// </summary>
    public class uChr : STR
    {
    }
}
