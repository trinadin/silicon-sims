using FSO.Files.HIT;

namespace FSO.Content.Model
{
    public class HITEventRegistration
    {
        public string Name;
        public HITEvents EventType;
        public uint TrackID;
        public HITResourceGroup ResGroup; //used to access this event's hit code
        // TYPE53-FC1: kSequenceTrackHitList (53) payload fields 4/5 (Hot.cs/EVT.cs
        // parse; TYPE53 audit §4). Zero for every other event type.
        public uint SequenceHitlist;
        public uint SequenceFlag;
    }
}
