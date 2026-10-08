namespace FSO.Files.HIT
{
    public enum HITArgs
    {
        kArgsNormal = 0,
        kArgsVolPan = 1,
        kArgsIdVolPan = 2,
        kArgsXYZ = 3
    }

    public enum HITControlGroups
    {
        kGroupSFX = 1,
        kGroupMusic = 2,
        kGroupVox = 3
    }

    public enum HITDuckingPriorities
    {
        // AUD-06 (native law, instruction-level): TS1 duck priorities are
        // always=0, low=10, normal=20, high=30, higher=40, evenhigher=50,
        // never=100. The spurious 32/5000 members are absent from the TS1
        // equates and are removed; duckpri_low was mislabelled 1 (should be 10).
        duckpri_always = 0x0,
        duckpri_low = 0xA,
        duckpri_normal = 0x14,
        duckpri_high = 0x1e,
        duckpri_higher = 0x28,
        duckpri_evenhigher = 0x32,
        duckpri_never = 0x64
    }

    public enum HITEvents
    {
        kSoundobPlay = 1,
        kSoundobStop = 2,
        kSoundobKill = 3,
        kSoundobUpdate = 4,
        kSoundobSetVolume = 5,
        kSoundobSetPitch = 6,
        kSoundobSetPan = 7,
        kSoundobSetPosition = 8,
        kSoundobSetFxType = 9,
        kSoundobSetFxLevel = 10,
        kSoundobPause = 11,
        kSoundobUnpause = 12,
        kSoundobLoad = 13,
        kSoundobUnload = 14,
        kSoundobCache = 15,
        kSoundobUncache = 16,
        kSoundobCancelNote = 19,
        kKillAll = 20,
        kPause = 21,
        kUnpause = 22,
        kKillInstance = 23,
        kTurnOnTV = 30,
        kTurnOffTV = 31,
        kUpdateSourceVolPan = 32,
        kSetMusicMode = 36,
        kPlayPiano = 43,
        debugeventson = 44,
        debugeventsoff = 45,
        debugsampleson = 46,
        debugsamplesoff = 47,
        debugtrackson = 48,
        debugtracksoff = 49,
        // TYPE53-FC1: kSequenceTrackHitList (sep6snds.hot equate; TYPE53 audit §1).
        // Native dispatch is cBox::Event's TOC jump table, types 0..105 (arm itself
        // undecoded — TOC drift, disclosed); 62 corpus registrations, 59 live via
        // MusicRecordingStudio.iff play_sound sites.
        kSequenceTrackHitList = 53
    }

    public enum HITPerson
    {
        Instance = 0x0,
        Gender = 0x1
    }
}
