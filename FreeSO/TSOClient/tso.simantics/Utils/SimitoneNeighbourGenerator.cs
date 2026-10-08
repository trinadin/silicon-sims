using FSO.Files.Formats.IFF.Chunks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FSO.SimAntics.Utils
{
    public static class SimitoneNeighbourGenerator
    {
        //Template Person GUID
        public static uint TEMPLATE_GUID = 0x7FD96B54;

        //process of creating a family
        // - other function provides persondata and bodystrings
        // - create FAMI with a new ID
        //   - generate GUID for each sim, and populate FamilyGUIDs
        // - set family in generated persondatas
        // - make the sims
        //   - AddNeighbour to NBRS as below (get back ID. do not set neighborID in personData, as the original doesn't)
        //   - gets a free user_#### id and makes that the name
        //   - replace relevant body strings in TemplatePerson
        //   - add/replace CTSS in TemplatePerson
        //   - change OBJD name to "user_#### - name". maybe change ctss id?
        //   - save sim to userdata

        public static void PrepareTemplatePerson(uint guid, SimTemplateCreateInfo info)
        {
            var neigh = Content.Content.Get().Neighborhood;
            //userid
            var userid = neigh.NextSim;
            // R252: the NBRS record Name is the lowercase character-file stem
            // ("user" + userid zero-padded to 5). SaveNewNeighbour writes
            // "User"+(NextSim++).iff from the same userid, so capture the lowercase
            // stem here (before NextSim++ in SaveNewNeighbour) for the caller.
            info.CharStem = "user" + userid.ToString().PadLeft(5, '0');

            var tempObj = Content.Content.Get().WorldObjects.Get(info.CustomGUID ?? TEMPLATE_GUID);
            tempObj.OBJ.ChunkParent.RetainChunkData = true;
            tempObj.OBJ.GUID = guid;
            tempObj.OBJ.ChunkLabel = "user" + userid.ToString().PadLeft(5, '0') + " - " + info.Name;

            var ctss = tempObj.Resource.Get<CTSS>(2000);
            if (ctss == null)
            {
                ctss = new CTSS()
                {
                    ChunkLabel = "",
                    ChunkID = 2000,
                    ChunkProcessed = true,
                    ChunkType = "CTSS",
                    ChunkParent = tempObj.Resource.MainIff,
                    AddedByPatch = true,
                };
                tempObj.Resource.MainIff.AddChunk(ctss);
                ctss.InsertString(0, new STRItem() { Value = "", Comment = "" });
                ctss.InsertString(0, new STRItem() { Value = "", Comment = "" });
            }
            ctss.SetString(0, info.Name);
            ctss.SetString(1, info.Bio);
            tempObj.OBJ.CatalogStringsID = 2000;

            var bodyStrings = tempObj.Resource.Get<STR>(200);
            foreach (var item in info.BodyStringReplace)
            {
                bodyStrings.SetStringForce(item.Key, item.Value); // ENG-17/18: the expanded slots (30-34) may need growth on truncated STRs
            }

            neigh.SaveNewNeighbour(tempObj);
            bodyStrings.SetString(1, "");
            bodyStrings.SetString(2, "");
        }

        public static FAMI CreateFamily(string name, int count, SimTemplateCreateInfo[] infos)
        {
            var fami = CreateFamily(name, count);
            for (int i=0; i<count; i++)
            {
                var guid = fami.FamilyGUIDs[i];
                var info = infos[i];
                info.FamilyID = (short)fami.ChunkID;
                PrepareTemplatePerson(guid, info);
                AddNeighbor(guid, NbrsFormat.PERSON_MODE, info.MakePersonData(), info.CharStem);
            }
            Content.Content.Get().Neighborhood.SaveNeighbourhood(true);
            return fami;
        }

        public static Neighbour CreateNeighbor(uint guid, SimTemplateCreateInfo info)
        {
            PrepareTemplatePerson(guid, info);
            return AddNeighbor(guid, NbrsFormat.PERSON_MODE, info.MakePersonData(), info.CharStem);
        }

        public static FAMI CreateFamily(string name, int count)
        {
            var neigh = Content.Content.Get().Neighborhood;
            var families = neigh.MainResource.List<FAMI>() ?? new List<FAMI>();
            families = families.OrderBy(x => x.ChunkID).ToList();
            ushort newID = 0;
            for (int i = 0; i < families.Count; i++)
            {
                if (families[i].ChunkID == newID) newID++;
                else break;
            }

            var guids = new uint[count];
            for (int i=0; i<count; i++)
            {
                guids[i] = GenerateGUID(guids);
            }

            var newFam = new FAMI()
            {
                ChunkLabel = "",
                ChunkID = newID,
                ChunkProcessed = true,
                ChunkType = "FAMI",
                ChunkParent = neigh.MainResource,
                AddedByPatch = true,

                FamilyGUIDs = guids,
                FamilyNumber = families.Max(x => x.FamilyNumber) + 1,
                // SAV-02: the original writes 1 for a created+moved-in family
                // (r252 decode.md §3 "0 for bin, 1/17 for moved-in"; round.md:69
                // pins "original 1" against the old port-chosen 24).
                Unknown = 1,
                Budget = 20000,
            };
            neigh.MainResource.AddChunk(newFam);

            var newFams = new FAMs()
            {
                ChunkLabel = "",
                ChunkID = newID,
                ChunkProcessed = true,
                ChunkType = "FAMs",
                ChunkParent = neigh.MainResource,
                AddedByPatch = true,
            };
            newFams.InsertString(0, new STRItem() { Comment = "", Value = name });
            neigh.MainResource.AddChunk(newFams);

            return newFam;
        }

        public static uint GenerateGUID(uint[] avoid)
        {
            var objProvider = Content.Content.Get().WorldObjects;
            lock (objProvider.Entries)
            {
                var rand = new Random();
                var guid = (uint)rand.Next();
                //doesnt cover entire uint space, but not really a problem right now.
                while (objProvider.Entries.ContainsKey(guid) || avoid.Contains(guid))
                {
                    guid = (uint)rand.Next();
                    //todo: if you get really unlucky, you can get stuck here forever. I mean really unlucky...
                }
                return guid;
            }
        }

        public static Neighbour AddNeighbor(uint guid, int personMode, short[] personData, string name)
        {
            var neigh = Content.Content.Get().Neighborhood;
            var ns = neigh.Neighbors.Entries;
            //find the lowest id that is free
            short newID = 1;
            for (int i=0; i<ns.Count; i++)
            {
                if (ns[i].NeighbourID == newID) newID++;
                else if (ns[i].NeighbourID < newID) continue;
                else break;
            }

            var newN = new Neighbour()
            {
                // R252: write the original record shape — Version 0x4 (80-short
                // PersonData, no Unknown3) and the lowercase character-file stem as
                // Name. The port previously left Version at its 0xA class default
                // and wrote the literal "iffname".
                Version = NbrsFormat.RECORD_VERSION,
                Name = name,
                NeighbourID = newID,
                GUID = guid,
                Relationships = new Dictionary<int, List<short>>(),
                PersonMode = personMode,
                PersonData = personData
            };
            neigh.Neighbors.AddNeighbor(newN);
            
            return newN;
        }
    }

    public class SimTemplateCreateInfo {
        public string Name;
        public string Bio;
        public short Gender;
        public short SkinTone;
        public bool Child;
        public short FamilyID;
        // R252: the lowercase character-file stem (e.g. "user00024") captured in
        // PrepareTemplatePerson from neigh.NextSim; used as the NBRS record Name.
        public string CharStem;
        // six base personality slots in IFF order: pd[2..7] = Nice, Active, Generous, Playful, Outgoing, Neat
        // (VMPersonDataVariable 2..7). IFF 8298 'init NPC' writes ALL SIX = 1000; make_new_character
        // NPCs mirror that (was 5 slots - Generous/4 was never written).
        public short[] PersonalityPoints = new short[6];
        public uint? CustomGUID;

        public Dictionary<int, string> BodyStringReplace;

        //code is mafat, etc
        public SimTemplateCreateInfo(string code, string skin)
        {
            //mcchd, mafat, etc
            Child = code[1] == 'c';
            var gender = code[0];
            var bodytype = code.Substring(2, 3);
            var codeunisex = (Child) ? "uchd" : (gender+bodytype);
            BodyStringReplace = new Dictionary<int, string>()
            {
                {0, Child?"child":"adult" },
                {12, (gender == 'm')?"male":"female" },
                {13, Child?"9":"27" },
                {14, skin },
                {15, "n"+codeunisex+"_01,BODY=n"+codeunisex+skin+"_01" },
                {16, "n"+codeunisex+"_01,BODY=u"+gender+bodytype+skin+(Child?"undies":"lguard")+"_01" },

                {27, (gender == 'm')?",BODY=f"+gender+bodytype+skin+"_01":"ff"+bodytype+"_01,BODY=ff"+bodytype+skin+"_01" },
                {28, (gender == 'm')?"HmLO,HAND=gmao_yeti":"HfLO,HAND=gfao_bear1" },
                {29, (gender == 'm')?"HmRO,HAND=gmao_yeti":"HfRO,HAND=gfao_bear1" },
                {30, (Child)?"ADDED":("f100"+code+"_original,BODY=f100"+code+skin+"_original") },
                {31, "s100"+code+"_original,BODY=s100"+code+skin+"_original" },
                {32, "l100"+code+"_original,BODY=l100"+code+skin+"_original" },
                {33, "w100"+code+"_original,BODY=w100"+code+skin+"_original" },
                // ENG-18: a table-backed HD default — the hardcoded h533<code>_zoot
                // resolves only for mafat (ENG-17 §3: the sole h533 set in the corpus);
                // 5 of 6 adult builds got a dangling slot-34. Deterministic first-valid
                // pick at creation (the transform's runtime roll is ENG-17's surface).
                {34, (Child)?"ADDED":HDSkinDefaultFor(code, skin) },
            };

            Gender = (short)((gender == 'f') ? 1 : 0);
            switch (skin)
            {
                case "lgt":
                    SkinTone = 0; break;
                case "med":
                    SkinTone = 1; break;
                case "drk":
                    SkinTone = 2; break;
            }
        }

        public SimTemplateCreateInfo(string petType, bool gender)
        {
            //kat/dog. 
            Child = false;
            BodyStringReplace = new Dictionary<int, string>()
            {
                {0, petType },
                {12, ((petType == "kat")?"cat":"dog") + ((!gender)?"male":"female") },
                {13, Child?"9":"27" },
            };

            Gender = (short)((gender) ? 1 : 0);
            SkinTone = 0;
        }

        public short[] MakePersonData()
        {
            var pd = new short[88];
            var rand = new Random();

            pd[2] = PersonalityPoints[0];
            pd[3] = PersonalityPoints[1];
            pd[4] = PersonalityPoints[2]; // Generous - IFF 8298 writes it, engine never did
            pd[5] = PersonalityPoints[3];
            pd[6] = PersonalityPoints[4];
            pd[7] = PersonalityPoints[5];

            // IFF 'Init person' (8192, reached only via init-NPC 8298, engine-driven in the
            // original - 0 IFF callers corpus-wide) also writes the new-sim job/autonomy/priority
            // PersonData canon. Mirror each write IFF-literally, in IFF chain execution order
            // (Init person 36/56/57/63 first, then init-NPC 33):
            //   pd[36] AutonomyLevel        = 50
            //   pd[56] JobType               = -1 ONLY if currently 0 (exact IFF Equals conditional)
            //   pd[57] JobPromotionLevel     = IFF Tuning[17412] which resolves to 4 at runtime
            //                                 (global BCON 264 'Children Grades' key 4)
            //   pd[63] JobPerformance        = 0
            //   pd[33] Priority              = 25  (init-NPC ins1)
            // The original save corroborates: Goth Cassandra carries jobType=-1/jobLevel=4/perf=0.
            pd[36] = 50;                       // AutonomyLevel (VMPersonDataVariable.AutonomyLevel)
            if (pd[56] == 0) pd[56] = -1;      // JobType: IFF Equals(56,0) true-branch -> -1 (unemployed)
            pd[57] = 4;                        // JobPromotionLevel: IFF Tuning[17412] -> global BCON 264 key 4
            pd[63] = 0;                        // JobPerformance

            // IFF 'Init person' (8192) opcode-8 random writes: pd[9..12,15,17,18] =
            // NextRandom(1000) (VMRandomNumber: DestinationScope MyPersonData, RangeData
            // 1000 Literal -> IFF range literal 1000; NextRandom gives [0,1000)). These map
            // to VMPersonDataVariable SkillEfficiency/Cooking/Charisma/Mechanical/Creativity/
            // Body/Logic. Engine previously left all seven at 0. IFF-literal mirror.
            pd[9]  = (short)rand.Next(1000);   // SkillEfficiency
            pd[10] = (short)rand.Next(1000);   // CookingSkill
            pd[11] = (short)rand.Next(1000);   // CharismaSkill
            pd[12] = (short)rand.Next(1000);   // MechanicalSkill
            pd[15] = (short)rand.Next(1000);   // CreativitySkill
            pd[17] = (short)rand.Next(1000);   // BodySkill
            pd[18] = (short)rand.Next(1000);   // LogicSkill

            pd[33] = 25;                       // Priority (VMPersonDataVariable.Priority; init-NPC ins1)

            // IFF 'init NPC' (8298) trailing writes: pd[29]=1 (Cheats), pd[32]=
            // Tuning[16898] (PersonType -> global BCON 260 'Person Types' key 2 = 2).
            pd[29] = 1;                        // Cheats
            pd[32] = 2;                        // PersonType (IFF Tuning[16898] -> BCON 260 key 2)

            // R152: interests = the ENGINE'S OWN creation law, replacing the
            // pre-decode guesses (pd[13]/[14]=500..800, pd[16]=pd[26]=600)
            // and the R150 tree-dialect mirror. The original creates CAS/
            // townie sims through cXPerson::Initialize -> RandomizeAllInterests
            // (x100 dialect): loop 1 words 46..55 with counters {4,3,3}, loop
            // 2 words 13/14/16/20/26 with {1,2,2}, ranges {0..3,4..6,7..10}
            // x100 (VMInterestRandomizer, decoded r151 sec 2.1 + r152 verify).
            // Character-FILE sims keep the trees' raw dialect; the Interest
            // panel bridges both.
            VMInterestRandomizer.ApplyTo(pd, rand);

            pd[58] = (short)(Child ? 9 : 27);
            pd[60] = SkinToDisk(SkinTone);
            pd[61] = FamilyID;
            pd[65] = Gender;

            return pd;
        }

        /// <summary>
        /// R252: map the logical SkinTone (AppearanceType 0=Light, 1=Medium, 2=Dark)
        /// to the original TS1 disk encoding on pd[60]: lgt=1, drk=2, med=3
        /// (non-monotonic). The port previously wrote the logical 0/1/2 straight
        /// into pd[60], which a native engine reads as 0=invalid/1=light/2=dark.
        /// </summary>
        public static short SkinToDisk(short logical)
        {
            switch (logical)
            {
                case 1: return 3; // Medium -> med=3
                case 2: return 2; // Dark   -> drk=2
                default: return 1; // Light (0) -> lgt=1
            }
        }

        private static string HDSkinDefaultFor(string code, string skin)
        {
            try
            {
                var suits = VMTS1PurchasableOutfitHelper.GetValidOutfitsByKey("h", code, skin, 5);
                if (suits.Length > 0) return suits[0].Item1;
            }
            catch { }
            return "h533" + code + "_zoot,BODY=h533" + code + skin + "_zoot"; // legacy fallback (table absent)
        }
    }
}
