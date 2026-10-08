using FSO.Content.Framework;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using System.Linq;

namespace FSO.Content.TS1
{
    public class TS1JobProvider
    {
        public IffFile JobResource;
        public int NumJobs;
        
        public TS1JobProvider(TS1Provider provider)
        {
            JobResource = (IffFile)provider.Get("work.iff");
            NumJobs = JobResource.List<CARR>().Count-1; //loads all jobs
        }

        public short GetJobData(ushort jobID, int jobLevel, int data)
        {
            return (short)(JobResource.Get<CARR>(jobID)?.GetJobData(jobLevel, data) ?? 0);
        }

        public CARR GetJob(ushort jobID)
        {
            return JobResource.Get<CARR>(jobID);
        }

        public short SetToNext(short current)
        {
            return (short)(JobResource.List<CARR>().FirstOrDefault(x => x.ChunkID > current)?.ChunkID ?? -1);
        }

        public string JobOffer(short jobID, int jobLevel, bool female)
        {
            // EXP-16: the native offer-dialog law is cCareer::GetOfferDialogText
            // @ 0x10051710 — female reads STR entry [2] (1-based 3) and falls
            // back to [1] (1-based 2) when the female text is empty; males read
            // [1]. The English corpus populates [1] for every track and leaves
            // [2] empty everywhere, so the fallback is the common case.
            var strings = JobStrings(jobID);
            if (strings != null)
            {
                if (female)
                {
                    var femaleOffer = strings.GetString(2);
                    if (!string.IsNullOrEmpty(femaleOffer)) return femaleOffer;
                }
                var offer = strings.GetString(1);
                if (!string.IsNullOrEmpty(offer)) return offer;
            }
            // DISCLOSED fallback (no STR table for this track): port-authored
            // composition, matching the pre-EXP-16 behaviour.
            var job = JobResource.Get<CARR>((ushort)jobID);
            return (job?.Name ?? "(unknown)") + " career track for a " + job?.JobLevels[jobLevel].JobName + ".";
        }

        /// <summary>
        /// EXP-16: the native gendered job-title law.
        ///
        /// cJob::GetName(bool female) @ 0x10051ab0: return the female title
        /// only when requested AND non-empty, otherwise the male title.
        /// LoadCareers @ 0x10050470 populates cJob from the career STR tables
        /// (per-language block of a 10-level track, 0-based):
        ///   [0] track name, [1] offer dialog male, [2] offer dialog female,
        ///   [3+2*level] male title, [4+2*level] male description,
        ///   [23+level] female title (empty when gender-neutral).
        /// The native prefers STR#(id+1000) for titles and STR#(id) for
        /// descriptions/short names; in the owned Complete corpus both tables
        /// carry identical title values (verified entry-for-entry in
        /// evidence/EXP-16/gendered-job-titles-law.md), so reading STR#(id)
        /// is corpus-exact. The CARR JobName is the final fallback.
        /// </summary>
        public string JobTitle(short jobID, int jobLevel, bool female)
        {
            var strings = JobStrings(jobID);
            if (strings != null && jobLevel >= 0)
            {
                if (female)
                {
                    var femaleTitle = strings.GetString(23 + jobLevel);
                    if (!string.IsNullOrEmpty(femaleTitle)) return femaleTitle;
                }
                var title = strings.GetString(3 + jobLevel * 2);
                if (!string.IsNullOrEmpty(title)) return title;
            }
            var job = JobResource.Get<CARR>((ushort)jobID);
            if (job == null || jobLevel < 0 || jobLevel >= job.JobLevels.Length)
                return null;
            return job.JobLevels[jobLevel].JobName;
        }

        public STR JobStrings(short jobID)
        {
            //TODO: use STR#
            return JobResource.Get<STR>((ushort)jobID);
        }

        public JobLevel GetJobLevel(short jobID, int jobLevel)
        {
            var job = JobResource.Get<CARR>((ushort)jobID);
            return job?.JobLevels[jobLevel];
        }
    }
}
