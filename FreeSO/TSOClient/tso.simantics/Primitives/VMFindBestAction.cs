using System;
using System.Collections.Generic;
using System.Linq;
using FSO.SimAntics.Engine;
using FSO.Files.Utils;
using FSO.SimAntics.Model;
using FSO.Files.Formats.IFF.Chunks;
using FSO.Content;
using FSO.LotView.Model;

namespace FSO.SimAntics.Primitives
{
    // Finds a suitable action and queues it onto this sim. Used for pet free will.
    // TS1 MODE (VM.TS1): this primitive (native opcode 3, "Find Best Interaction",
    // TryFindBestAction__8cXPersonFP9StackElem 0x109860) has been REBUILT to the decoded
    // native free-will winner law — see ExecuteTS1Native below; evidence:
    // tools/iff-dump/r249-freewill/{decode.md,skeptic-corrections.md} +
    // tools/iff-dump/r249-freewill-cfg/decode.md (CFG/FCNS resolution; the maintainer's
    // cutoff-polarity adjudication is binding: ACCEPT IFF score >= cutoff, never <=).
    // R250 IS REFUTED (AUD-18-A, coordination/evidence/AUD-18/a-simantics-audit.md
    // "F-3 RESOLUTION", call-site proof): the score/test pair (TestInteraction
    // 0x109d1c + GetInteractionScore 0x109d48) sits INSIDE the per-candidate gather
    // loop (loop closes 0x100f44), NOT at the winner hand-off — the post-draw tail
    // (0x100f48+) contains no test/score call. Gather DOES score: per candidate,
    // on the tree-mutated ad copy, with the sentinel drop, the atten multiply,
    // and the FCNS min-autonomy-score push gate; the pool sorts DESCENDING (best
    // first, comparator 0x1011b0 ±1e-7); the draw covers the K BEST; the cutoff
    // consumes the WINNER'S POOL SCORE; the hand-off writes the action/object and
    // returns with NO re-test.

    // How autonomy works in The Sims:
    // 
    // Each interaction has "motive advertisements" which determine how many autonomy points are given for each interaction.
    //
    // First, each motive is converted into an "effective motive" using the interaction contribution curves.
    // These curves generally make motive changes more evident at lower values, and cap them at a specific value.
    // This prevents people from considering sleeping at 50% energy, but also allows other motives like fun to use a much higher cap.
    // "Happy" is calculated as an average of all "effective motive"s, including mood.
    // (In Hot Date and onwards this may be weighted by the Happy Weight curves)
    //
    // For each interaction, we only evaluate it if it has the following properties:
    // - non-zero motive range for any advertisement
    // - object is not "occupied"
    // - object has use count of 0 (this will be really slow in freeso so we will skip it for now, TODO)
    // - if any advertisements have a non-zero minimum, they are only considered if the motive is below that minimum (TODO: VERIFY)
    // Interaction check trees are evaluated to see if they are usable. "Auto" is passed to the tree as 1 in param 0, indicating this is an autonomous check.
    // When evaluating an interaction's check tree, it may change the motive ad range and minimum.
    // I don't know if anything changes from 0, but if it does then that would really suck for performance.
    //
    // For each interaction a new Happy score is calculated based on the motive deltas applied to the sim's current motives.
    // Motive delta is divided by 1000 for some reason. I don't really agree with this, but it matches numbers with TS 1.0. May be different in HD+.
    // Personality is applied by **some factor** based on your personality, if present. Likely 0-1 multiplier for personality, 0-2 for skill.
    // Remember to re-apply the interaction curves to the new motives here.Room for optimisation to only update the motives that changed.
    //
    // Attenuation is very simple: Score / (1 + (Attenuation* Distance)). It is applied to the *distance from base happy*, which results in the final score.
    // Individual motive scores can be negative to discourage use of interactions at high motives.These are combined with positives from other motives.
    //
    // Final scores below certain values are ignored:
    // 1E-07 for visitors and family, 1E-06 for sims who are sitting. These constants are in FCNS 2 in global.iff.
    // That means negative scores are ignored. Motive ads when you're in the flat part of the curve are ignored.
    // 

    public class VMFindBestAction : VMPrimitiveHandler
    {
        public static VMPersonDataVariable[] VaryByTypes = new VMPersonDataVariable[]
        {
            VMPersonDataVariable.UnusedAndDoNotUse, //0
            VMPersonDataVariable.NicePersonality,
            VMPersonDataVariable.NicePersonality, //inverted
            VMPersonDataVariable.ActivePersonality,
            VMPersonDataVariable.ActivePersonality, //inverted
            VMPersonDataVariable.GenerousPersonality,
            VMPersonDataVariable.GenerousPersonality, //inverted
            VMPersonDataVariable.PlayfulPersonality,
            VMPersonDataVariable.PlayfulPersonality, //inverted
            VMPersonDataVariable.OutgoingPersonality,
            VMPersonDataVariable.OutgoingPersonality, //inverted
            VMPersonDataVariable.NeatPersonality,
            VMPersonDataVariable.NeatPersonality, //inverted


            VMPersonDataVariable.UnusedAndDoNotUse, // cleaning skill -- 13
            VMPersonDataVariable.CookingSkill, // cooking skill
            VMPersonDataVariable.CharismaSkill, // social skill *
            VMPersonDataVariable.MechanicalSkill, // repair skill *
            VMPersonDataVariable.UnusedAndDoNotUse, // gardening skill
            VMPersonDataVariable.UnusedAndDoNotUse, // music skill
            VMPersonDataVariable.CreativitySkill, // creativity skill
            VMPersonDataVariable.UnusedAndDoNotUse, // literacy skill
            VMPersonDataVariable.BodySkill, // physical skill *
            VMPersonDataVariable.LogicSkill // logic skill
        };

        public static VMMotive[] WeightMotives = new VMMotive[]
        {
            VMMotive.Energy,
            VMMotive.Comfort,
            VMMotive.Hunger,
            VMMotive.Hygiene,
            VMMotive.Bladder,
            VMMotive.Mood,
            VMMotive.Room,
            VMMotive.Social,
            VMMotive.Fun
        };

        public static int[] MotiveToWeight = Enumerable.Range(0, 16).Select(motive => Array.IndexOf(WeightMotives, (VMMotive)motive)).ToArray();

        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            // R249: the TS1 native "find best interaction" law (TryFindBestAction 0x109860,
            // dispatched from TryElement 0x10c430 for prim opcode 3). The TSO path (opcode
            // 65, FreeSO's own autonomy) is unchanged.
            if (context.VM.TS1)
            {
                return ExecuteTS1Native(context);
            }
            return ExecuteTSO(context);
        }

        private VMPrimitiveExitCode ExecuteTSO(VMStackFrame context)
        {
            //if we already have some action, do nothing
            var better = context.Caller.Thread.Queue.FirstOrDefault(x => x.Priority > (context.Caller as VMAvatar).GetPersonData(VMPersonDataVariable.Priority));
            if (better != null) {
                context.StackObject = better.Callee;
                return VMPrimitiveExitCode.GOTO_TRUE;
            }

            var caller = (VMAvatar)context.Caller;

            // Check if free will is disabled for player family Sims
            // Visitors (PersonType == 1) and pets should still have autonomy
            var visitor = (caller.GetPersonData(VMPersonDataVariable.PersonType) == 1);
            if (!VM.FreeWillEnabled && !visitor && !caller.IsPet)
            {
                // Free will is disabled and this is a player family Sim (not visitor, not pet)
                // Return false to indicate no autonomous action was chosen
                return VMPrimitiveExitCode.GOTO_FALSE;
            }

            var ents = new List<VMEntity>(context.VM.Context.ObjectQueries.WithAutonomy);
            var processed = new HashSet<short>();
            var pos1 = caller.Position;

            var child = (caller.IsChild && context.VM.TS1);
            var attenTable = visitor ? TTAB.VisitorAttenuationValues : TTAB.AttenuationValues;
            var global = Content.Content.Get().WorldObjectGlobals;
            var interactionCurve = child ? global.InteractionScoreChild : global.InteractionScore;
            var happyCurve = child ? global.HappyWeightChild : global.HappyWeight;

            var isStray = caller.IsPet && context.VM.TS1 && caller.GetPersonData(VMPersonDataVariable.GreetStatus) == 0 && caller.GetPersonData(VMPersonDataVariable.PersonType) == 1;

            // === HAPPY CALCULATION ===

            var newStyle = false;
            // TODO: new style
            var weights = newStyle ? new float[9] : new float[9] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }; //TODO: weights from curves for new
            var totalWeight = weights.Sum();
            for (int i = 0; i < 9; i++) weights[i] /= totalWeight;
            var minScore = (caller.GetPersonData(VMPersonDataVariable.Posture) > 0) ? 1e-6 : 1e-7;

            var motives = WeightMotives.Select(x => caller.GetMotiveData(x));
            var happyParts = motives
                .Zip(interactionCurve, (motive, curve) => curve.GetPoint(motive))
                .Zip(weights, (motive, weight) => motive * weight)
                .ToArray();

            float baseHappy = happyParts.Sum();

            List<VMPieMenuInteraction> validActions = new List<VMPieMenuInteraction>();
            foreach (var iobj in ents)
            {
                if (iobj.Position == LotTilePos.OUT_OF_WORLD) continue;
                var obj = iobj.MultitileGroup.GetInteractionGroupLeader(iobj);
                if (processed.Contains(obj.ObjectID) || (obj is VMGameObject && ((VMGameObject)obj).Disabled > 0)) continue;
                processed.Add(obj.ObjectID);

                if (isStray)
                {
                    if (obj is VMGameObject) continue;
                    //determine if the object is indoors
                    var roomID = context.VM.Context.GetObjectRoom(obj);
                    roomID = (ushort)Math.Max(0, Math.Min(context.VM.Context.RoomInfo.Length - 1, roomID));
                    var room = context.VM.Context.RoomInfo[roomID];
                    var outside = room.Room.IsOutside;
                    if (!outside) continue;
                }
                var pos2 = obj.Position;
                var distance = (float)Math.Sqrt(Math.Pow(pos1.x - pos2.x, 2) + Math.Pow(pos1.y - pos2.y, 2) + Math.Pow((pos1.Level - pos2.Level) * 320, 2.0)) / 16.0f;
                var inUse = obj.GetFlag(VMEntityFlags.Occupied);

                if (obj.TreeTable == null) continue;
                foreach (var entry in obj.TreeTable.AutoInteractions)
                {
                    var id = entry.TTAIndex;
                    if (inUse && !obj.TreeTable.Interactions.Any(x => x.JoiningIndex == id)) continue;
                    var advertisements = entry.ActiveMotiveEntries; //TODO: cache this
                    if (advertisements.Length == 0) continue; //no ads on this object.

                    var action = obj.GetAction((int)id, caller, context.VM.Context, false);
                    TTAs ttas = obj.TreeTableStrings;

                    caller.ObjectData[(int)VMStackObjectVariable.HideInteraction] = 0;
                    var actionStrings = caller.Thread.CheckAction(action, true);

                    var pie = new List<VMPieMenuInteraction>();
                    if (actionStrings != null)
                    {
                        if (actionStrings.Count > 0)
                        {
                            foreach (var actionS in actionStrings)
                            {
                                if (actionS.Name == null) actionS.Name = ttas?.GetString((int)id) ?? "***MISSING***";
                                actionS.ID = (byte)id;
                                actionS.Entry = entry;
                                actionS.Global = false;
                                pie.Add(actionS);
                            }
                        }
                        else
                        {
                            if (ttas != null)
                            {
                                pie.Add(new VMPieMenuInteraction()
                                {
                                    Name = ttas.GetString((int)id),
                                    ID = (byte)id,
                                    Entry = entry,
                                    Global = false,
                                });
                            }
                        }
                    }
                    var first = pie.FirstOrDefault();
                    if (first != null)
                    {
                        // calculate score for this tree.
                        // start with the base happy value, and modify it for each motive changed.
                        float score = baseHappy;
                        for (int i = 0; i < advertisements.Length; i++)
                        {
                            var motiveI = advertisements[i].MotiveIndex;
                            var motiveScore = entry.MotiveEntries[motiveI];
                            short min = motiveScore.EffectRangeMinimum;
                            short max = motiveScore.EffectRangeDelta;
                            short personality = (short)motiveScore.PersonalityModifier;
                            if (first.MotiveAdChanges != null)
                            {
                                first.MotiveAdChanges.TryGetValue((0 << 16) | motiveI, out min);
                                first.MotiveAdChanges.TryGetValue((1 << 16) | motiveI, out max);
                                first.MotiveAdChanges.TryGetValue((2 << 16) | motiveI, out personality);
                            }

                            if (max == 0 && min > 0)
                            {
                                //invalid delta. do from 0..delta instead (fix child-talk preference?)
                                max = min;
                                min = 0;
                            }
                            max += min; //it's a delta, add min to it

                            var myMotive = caller.GetMotiveData((VMMotive)motiveI);
                            if (min != 0 && myMotive > min) continue;

                            // subtract the base contribution for this motive from happy
                            var weightInd = MotiveToWeight[motiveI];
                            if (weightInd == -1) continue;
                            score -= happyParts[weightInd];

                            float personalityMul = 1;
                            if (personality > 0 && personality < VaryByTypes.Length)
                            {
                                personalityMul = caller.GetPersonData(VaryByTypes[personality]);
                                personalityMul /= 1000f;
                                if (personality < 13)
                                {
                                    if ((personality & 1) == 0)
                                    {
                                        personalityMul = 1 - personalityMul;
                                    }
                                } else
                                {
                                    personalityMul *= 2;
                                }
                            }

                            // then add the new contribution for this motive.
                            score += interactionCurve[weightInd].GetPoint(myMotive + (max * personalityMul) / 1000f) * weights[weightInd];
                        }

                        // score relative to base
                        score -= baseHappy;
                        // modify score using attenuation
                        float atten = (entry.AttenuationCode == 0 || entry.AttenuationCode >= attenTable.Length) ?
                            entry.AttenuationValue : attenTable[entry.AttenuationCode];

                        score = score / (1 + atten * distance);

                        if (score > minScore)
                        {
                            AddScoredActions(validActions, pie, score, obj);
                        }
                    }
                    //if (attenScore != 0) attenScore += (int)context.VM.Context.NextRandom(31) - 15;
                    
                    //TODO: Lockout interactions that have been used before for a few sim hours (in ts1 ticks. same # of ticks for tso probably)
                    //TODO: special logic for socials?
                }
            }
            List<VMPieMenuInteraction> sorted = validActions.OrderByDescending(x => x.Score).ToList();
            sorted = TakeTopActions(sorted, 4);
            var selection = sorted.FirstOrDefault();
            if (selection == null) return VMPrimitiveExitCode.GOTO_FALSE;
            if (!selection.Entry.AutoFirst)
            {
                // weighted random selection
                //var slice = sorted.Take(Math.Min(4, sorted.Count)).ToList();
                var totalScore = sorted.Sum(x => x.Score);
                var random = context.VM.Context.NextRandom(10000);

                float randomTotal = 0;
                for (int i=0; i < sorted.Count; i++)
                {
                    var action = sorted[i];
                    randomTotal += (sorted[i].Score / totalScore) * 10000;
                    if (random <= randomTotal)
                    {
                        selection = action;
                        break;
                    }
                }
            }

            var qaction = selection.Callee.GetAction(selection.ID, context.Caller, context.VM.Context, false, new short[] { selection.Param0, 0, 0, 0 });
            if (qaction != null)
            {
                qaction.Priority = (short)VMQueuePriority.Autonomous;
                context.Caller.Thread.EnqueueAction(qaction);
                context.StackObject = selection.Callee;
                return VMPrimitiveExitCode.GOTO_TRUE;
            } else
            {
                return VMPrimitiveExitCode.GOTO_FALSE;
            }
            
        }

        private static void AddScoredActions(List<VMPieMenuInteraction> validActions,
            List<VMPieMenuInteraction> pie, float score, VMEntity callee)
        {
            foreach (var item in pie)
            {
                item.Score = score;
                item.Callee = callee;
                validActions.Add(item);
            }
        }

        private List<VMPieMenuInteraction> TakeTopActions(List<VMPieMenuInteraction> list, int count)
        {
            var result = new List<VMPieMenuInteraction>();
            foreach (var action in list)
            {
                var users = action.Callee.GetValue(VMStackObjectVariable.UseCount);
                if (users == 0 || action.Callee.TreeTable.Interactions.Any(x => x.JoiningIndex == action.ID))
                {
                    result.Add(action);
                    if (result.Count >= count) break;
                }
            }
            return result;
        }

        // =====================================================================================
        // R249 — the TS1 native free-will winner selection (TryFindBestAction 0x109860).
        // Every step below carries its decode address. Evidence: tools/iff-dump/
        // r249-freewill/{decode.md, skeptic-corrections.md} and r249-freewill-cfg/decode.md;
        // the maintainer's adjudication on the cutoff polarity (0x109ed4-0x109ee0) is binding:
        // standing (attr slot 0 == 0) accepts unconditionally; an engaged sim accepts iff
        // winner score >= cutoff (bge -> accept), never score <= cutoff.
        // R250 (binding, maintainer call-site scan): the score exists ONLY at hand-off —
        // gather never scores; see the R250 SCORE LAW comments at gather and hand-off.
        // =====================================================================================

        /// <summary>The nine fixed MotiveEffects motive ids (TOC-0x5b38 table, sec1+0x450f8).</summary>
        private static readonly int[] ScoreCurveMotives = { 5, 6, 7, 8, 9, 3, 13, 14, 15 };

        // MOTIVETAB (*(TOC-0x5b30) = 0x59a1e0, 12-byte rows {pad, idx, invert}), recovered
        // from the binary: rows 1..12 are the personality attributes in direct/inverted
        // pairs (Nice 2, Active 3, Generous 4, Playful 5, Outgoing 6, Neat 7); rows 13..22
        // are the skill attributes (9,10,11,12,55,55,15,55,17,18), never inverted.
        // Row 0 is the {idx -1} sentinel; the selector==0 ad case never reaches it.
        private static readonly short[] MotiveTabIdx =
            { 0, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 9, 10, 11, 12, 55, 55, 15, 55, 17, 18 };
        // Rows 13..22 are the SKILL attributes (see MotiveTabIdx) — never inverted
        // (the binary's MOTIVETAB invert column is 0 for the whole skill block; the
        // landed 13-entry array made any skill-selector ad row throw
        // IndexOutOfRangeException inside GetInteractionScore once the admission
        // gates let the general corpus reach scoring).
        private static readonly bool[] MotiveTabInv =
            { false, false, true, false, true, false, true, false, true, false, true, false, true,
              false, false, false, false, false, false, false, false, false, false };

        // AutonomyConstants (CFG = *(TOC-0x58e8) = 0x59a524, code-section literal pool).
        private const float CFG_ATTENUATION_N = 1.0f;      // CFG+0x20, read live at 0x109d2c
        private const float CFG_SCORE_SENTINEL = 0.0f;     // CFG+4 — AUD-18-A restored: the
        // gather-time H' == sentinel drop (0x109d4c-0x109d54); R250 had misattributed it
        // to a hand-off block that does not exist.

        private static WorldGlobalProvider Globals
        {
            get { return Content.Content.Get().WorldObjectGlobals; }
        }

        /// <summary>
        /// The NATIVE word-65 person-class the autonomy gates were decoded against.
        /// The decoded law tests masks 0x1/0x2/0x4 of this word (Append 0x105b00-0x105b5c;
        /// Test 0x106488-0x1064d0; actor gate 0x105934-0x105944). Native classes:
        /// 0 = resident human — the character-object DEFAULT: no creation path or IFF tree
        /// writes word 65 (character inits write only mypd[20]; EditPerson::SetGender
        /// 0x6b2e0 stores sex in the BODY STRINGS via UpdateSuits), so runtime-created
        /// residents of both sexes carry 0; 1 = NPC/special character (the shipped template
        /// hood's maid/officer records carry 1 — ENG-25/AUD-16 P2-2 CORRECTION:
        /// word-65 = 1 sets neither bit 1 nor bit 2, so the Append gates demand
        /// NEITHER 0x400 NOR 0x200 for class 1; it flows through the same class-0
        /// gates (including the 0x40 NoAdult block), not a restricted funnel);
        /// 2/3 = dogs, 4/5 = cats (the EditPerson PersonGender string table
        /// {"male","female","dogmale","dogfemale","catmale","catfemale"}).
        /// The PORT stores its own sex bit (1=female) and species bits (8/16) in the same
        /// word (SimitoneNeighbourGenerator pd[65]; VMAvatar.IsPet/IsDog/IsCat). AUD-16
        /// P2-2 CORRECTION: the earlier claim that a port female (1) "would land in the
        /// special/NPC class and lose autonomy (Append demands 0x400)" was WRONG —
        /// word-65 = 1 sets neither gate bit, so class 1 demands neither mask and flows
        /// through the class-0 gates; the adapter still maps all humans to 0 (the
        /// native runtime default for created residents).
        /// This adapter derives the native class from the port's stored identity so the
        /// decoded gates consume native-semantic data without rewriting the stored word
        /// (GetPersonSuitTS1 still reads it as the port sex bit).
        /// </summary>
        private static int NativeGenderClass(VMAvatar avatar)
        {
            var g = avatar.GetPersonData(VMPersonDataVariable.Gender);
            if ((g & 8) != 0) return 2 | (g & 1);   // dog (native classes 2/3)
            if ((g & 16) != 0) return 4 | (g & 1);  // cat (native classes 4/5)
            return 0;                               // human resident class
        }


        // R249: re-entrancy guard. Candidate check trees execute arbitrary routines
        // (the TTAB action/guard function ids resolve through the engine's routine
        // space) and some of those trees dispatch prim 3 again. The native bounds the
        // recursion with its interpreter stack limit (a re-entrant selection fails at
        // max depth); the port's interpreter is loop-based, so the recursion would be
        // unbounded — a re-entrant selection fails instead.
        [ThreadStatic] private static bool InTS1Select;

        private VMPrimitiveExitCode ExecuteTS1Native(VMStackFrame context)
        {
            var caller = (VMAvatar)context.Caller;
            var vm = context.VM;
            if (InTS1Select) return VMPrimitiveExitCode.GOTO_FALSE;
            InTS1Select = true;
            try {

            // ---- LAW STEP 1: the prelude (0x109860-0x109970) --------------------------
            // Free-will byte (u8 at data 0x45f1c, init 1; port: VM.FreeWillEnabled). With
            // free will ON the entire free-will-gated prelude is skipped (0x1098a0
            // `bne 0x109928`); selection runs unconditionally.
            if (!VM.FreeWillEnabled)
            {
                var personType = caller.GetPersonData(VMPersonDataVariable.PersonType); // attr 32 (+0x5cc)
                if (personType == 0 && !caller.IsPet)
                {
                    // 0x1098a4-0x1098d8: a plain controlled family member selects nothing.
                        // (The native's (word65 & 6) continuation arms and the *(TOC-0x7114)
                        // deref gate (0x1098b0-0x1098d0) cover flagged/pet classes whose port
                        // encoding lives in different bits; the *(TOC-0x7114) subsystem has no
                        // port writer and defaults to pass. DISCLOSED port alignment.)
                        return VMPrimitiveExitCode.GOTO_FALSE;
                    }
                if (personType != 0)
                {
                    // 0x1098e4-0x10991c: visitor gate — continue unless the active-house
                    // chain word equals word 61 (TS1FamilyNumber) AND the word-65 parity
                    // idiom fails (passes iff g >= 2 for non-negative g; S4).
                    var familyNumber = caller.GetPersonData(VMPersonDataVariable.TS1FamilyNumber);
                    var chainWord = (vm.TS1State != null && vm.TS1State.CurrentFamily != null)
                        ? vm.TS1State.CurrentFamily.ChunkID : 0;
                    if (chainWord == familyNumber)
                    {
                        var g = NativeGenderClass(caller);
                        var x = g ^ 1;
                        var parity = (x >> 1) - (x & g); // 0x109900 idiom (srawi/and/subf)
                        if ((parity & 0x40000000) == 0) return VMPrimitiveExitCode.GOTO_FALSE;
                    }
                }
            }
            // ALWAYS-APPLY gates (0x109928-0x109970): execute regardless of the free-will
            // byte (skeptic S4 — both live after the free-will branch join at 0x109928).
            {
                // 0x109934-0x109958 (R249 gate re-derivation, instruction-exact): the CHAIN
                // WORD is *( *( *(data@0x8510c) ) + 0x10 ) + 0x10c — the active-family NUMBER
                // of the loaded house (TOC-0x778c -> data 0x8510c is the world singleton; +0x10
                // is its Family object — the SAME object GetPersonInHouseIndex walks via
                // Family::CountMembers/GetIndexedMember, 0x2440f0-0x2441a8; the visitor
                // prelude at 0x1098e4-0x1098f8 compares this word to person word 61
                // TS1FamilyNumber). The gate returns 0 IFF chain == 0 AND
                // GetInstance(GetPersonInHouseIndex(0)) == person (0x109948 bne skips the
                // return when chain != 0; 0x109950 when the resolved person differs) — i.e.
                // it blocks ONLY the first family member, and ONLY on a family-number-0 lot.
                // Port mapping: chain word = the active FAMI chunk id (the port already
                // equates family number <-> FAMI ChunkID in VMAvatar.InheritNeighbor);
                // house person 0 = the first family avatar (PersonType == 0).
                var chainWord = (vm.TS1State != null && vm.TS1State.CurrentFamily != null)
                    ? vm.TS1State.CurrentFamily.ChunkID : 0;
                if (chainWord == 0 && caller.GetPersonData(VMPersonDataVariable.PersonType) == 0)
                {
                    foreach (var ent in vm.Entities)
                    {
                        var ava = ent as VMAvatar;
                        if (ava == null || ava.Dead || ava.Position == LotTilePos.OUT_OF_WORLD) continue;
                        if (ava.GetPersonData(VMPersonDataVariable.PersonType) != 0) continue;
                        if (ava == caller)
                        {
                            return VMPrimitiveExitCode.GOTO_FALSE;
                        }
                        break; // the first (lowest-indexed) family member decides the gate
                    }
                }
                // 0x10995c-0x109970: the *(TOC-0x7110) autonomy-suspended byte (code-BSS
                // 0x92664, default 0). No port writer for this subsystem; gate inert at the
                // native default. DISCLOSED.
            }

            // ---- LAW STEP 3a: H = the current score mean (0x10997c-0x109980) -----------
            // GetCurrentScore (0x9fc20): mean over the 9 fixed MotiveEffects entries of
            // curve_j(motiveFloat_j); entries with an empty curve contribute CFG2+0 = 0.0;
            // denominator = MotiveEffects+4 = 9.
            var curves = GetScoreCurves(caller);
            var motiveSnapshot = new float[ScoreCurveMotives.Length];
            for (int j = 0; j < ScoreCurveMotives.Length; j++)
                motiveSnapshot[j] = caller.GetMotiveData((VMMotive)ScoreCurveMotives[j]);
            var baseH = GetCurrentScore(curves, motiveSnapshot);

            var gender = NativeGenderClass(caller); // the native word-65 person-class (see adapter doc)
            // 0x1099b8-0x1099ec: the STRATUM — (*(world->0x1c->0x98) + *(person+0xf0)) & 3
            // on residential lots (GetZoningType == 0 keeps it; any other zoning forces 0),
            // where world->0x1c->0x98 is the current house number (port: global 10 —
            // VM.SetGlobalValue(10, HouseNumber) in VMTS1Activator) and person+0xf0 is the
            // cXObject object id (skeptic S10 "target+0xf0 objectID"; VMEntity.ObjectID).
            // Load spreading: each resident only ever sees its own stratum's objects.
            var houseNumber = (vm.GlobalState != null && vm.GlobalState.Length > 10) ? vm.GlobalState[10] : (short)0;
            var stratum = (houseNumber + caller.ObjectID) & 3;
            var isVisitor = caller.GetPersonData(VMPersonDataVariable.PersonType) != 0; // attr 32 != 0 (skeptic S8)
            var isChild = caller.GetPersonData(VMPersonDataVariable.PersonsAge) > 0
                && caller.GetPersonData(VMPersonDataVariable.PersonsAge) < 0x12; // attr 58 in 1..17
            var autonomyLevel = caller.GetPersonData(VMPersonDataVariable.AutonomyLevel); // attr 36 (+0x5d4) ad ceiling
            // 0x109994/0x1099a0 (the f28 load R250 orphaned — AUD-18-A restored): the
            // per-decision MIN-AUTONOMY-SCORE push gate over the gather pool. FCNS
            // "min autonomy score for family" (compiled default 0.2, [TOC-0x70c0]) /
            // "...for visitors" (default 0.05, [TOC-0x70bc]); the shipped Global.iff
            // FCNS runtime value of both is 1e-7 — the push gate is live "score > 0".
            var minScore = isVisitor
                ? Globals.GetAutonomyConstant("min autonomy score for visitors", 0.05f)
                : Globals.GetAutonomyConstant("min autonomy score for family", 0.2f);

            // ---- LAW STEP 2: candidate gathering (0x109a98-0x109dd4) -------------------
            // NATIVE GATHER ORDER (load-bearing — it feeds the draw): the native walks the
            // person's OBJECT MODULE array (person+0xdc -> count +0x1c, object pointers
            // +0x20) in index order (0x109aa0-0x109ad0) — i.e. object creation order inside
            // the loaded house module — and, per object, walks its TTAB entries BACKWARDS
            // (AppendInteractionsForAuto 0x1059a0-0x105c6c). One candidate per (object,
            // advertised entry); no variant multiplication. PORT MAPPING (corrected,
            // P2-11): ObjectQueries.WithAutonomy is maintained in ASCENDING OBJECT-ID
            // order — VMObjectQueries.NewObject routes through VM.AddToObjList, which
            // BINARY-SEARCH-INSERTS by ObjectID (it does not append). This reproduces
            // the native module order only while ids are monotonic with creation order
            // (the common case: loaded-house ids are assigned sequentially and runtime
            // objects take fresh ids); ID REUSE (an id recycled after a delete)
            // REORDERS the list relative to native creation order. Kept as the
            // disclosed approximation. Multi-tile parts are separate module objects
            // natively and stay separate here (the port's interaction-group-leader
            // dedupe is NOT applied).
            var ents = vm.Context.ObjectQueries.WithAutonomy;
            // EXP-14 follow-up diagnostics (probe-gated): coarse skip counters
            // discriminating entry-gate rejection from TestInteraction failure.
            var diag = TS1GatherDiag;
            // EXP-14 hdserve-residual (probe-gated, target-caller only): per-PODIUM-row
            // fate line — names the exact leg each podium TTAB row dies on for the
            // armed traveler (static gate / test / sentinel / score-below-min).
            var diagPod = diag && (_diagTargetOid == 0 || context.Caller == null
                || context.Caller.ObjectID == _diagTargetOid);
            var dPodLog = new List<string>();
            int dObjSeen = 0, dGatePassed = 0, dTestFail = 0, dPool = 0;
            var candidates = new List<ScoredCandidate>();
            foreach (var obj in ents)
            {
                if (diag) dObjSeen++;
                if (obj.Position == LotTilePos.OUT_OF_WORLD) continue;
                // 0x109ae8-0x109afc: module parallel-flags word bit PPC 12 (0x80000) must be
                // set. Port mapping: the VMGameObject.Disabled counter (OBJD Disabled /
                // engine disable ops) is the port's "object dead for interaction" state.
                var vmGameObject = obj as VMGameObject;
                if (vmGameObject != null && vmGameObject.Disabled > 0) continue;
                // 0x109ad8-0x109ae4: target ObjSelector+0xe (OBJD TreeTableID) must be set.
                var treeTable = obj.TreeTable;
                if (treeTable == null || treeTable.Interactions.Length == 0) continue;
                // 0x109b00-0x109b4c: community-lot downtown probe (zoning == 2 AND lot id in
                // [0x28,0x32) clears r22). Simitone mounts no such lots; r22 == 1 always.
                // DISCLOSED: the probe is omitted as dead.
                // 0x109b50-0x109b84: master ObjSelector+0x8a (OBJD field 67, BuildModeType)
                // in {4,5} excludes the object entirely (unless the dead r22 case).
                var objd = obj.Object != null ? obj.Object.OBJ : null;
                var master = (objd != null && obj.MasterDefinition != null) ? obj.MasterDefinition : objd;
                if (master != null && (master.BuildModeType == 4 || master.BuildModeType == 5)) continue;
                // 0x109b88-0x109bcc: self-target gate — the object being the person requires
                // the word-65 parity idiom to pass (bit 30 of ((g^1)>>1) - ((g^1)&g)).
                if (obj == caller)
                {
                    var g = NativeGenderClass(caller);
                    var x = g ^ 1;
                    var parity = (x >> 1) - (x & g);
                    if ((parity & 0x40000000) == 0) continue;
                }
                // 0x109bd0-0x109bd8: in-use counter (obj+0x7c s16) > 0 -> skip. Port mapping:
                // the UseCount stack object variable. (The TSO path's JoiningIndex carve-out
                // has no native counterpart at selection time.)
                if (obj.GetValue(VMStackObjectVariable.UseCount) > 0) continue;
                // 0x109c34-0x109c88: the STRATUM test, per object. Stratum 0: every
                // object. Stratum 1: CalcShortDistance(person, obj) <= CFG+0x1c (the
                // distance-capped stratum; CFG+0x1c has no FCNS name — the runtime pool
                // value is unrecoverable per the r249-freewill-cfg container facts — so
                // the cap is a DISCLOSED 20.0 default; house 5 + obj18 lands in stratum 3
                // and never evaluates it). Stratum 2: objects with an EVEN object id
                // (lha 0xf0(r20); clrlwi 0x1f; beq 0x109c8c at 0x109c70-0x109c7c).
                // Stratum >= 3 (and negative): ODD object ids (0x109c80-0x109c88).
                if (stratum == 1)
                {
                    // ORIG-02: CFG+0x1c recovered = 7.0f STATIC (14 readers,
                    // zero writers across the entire code section; the old
                    // disclosed 20.0f was r249's placeholder).
                    if (Distance(caller, obj) > 7.0f) continue;
                }
                else if (stratum == 2)
                {
                    if ((obj.ObjectID & 1) != 0) continue;
                }
                else if (stratum != 0)
                {
                    if ((obj.ObjectID & 1) == 0) continue;
                }
                // 0x109bdc-0x109c30: the r19 downtown probe (indoor class {5,6,7} exclusion)
                // requires the current lot id in [0x28,0x32); dead in Simitone. DISCLOSED.
                // (The 0x109c34-0x109c88 stratum filters are implemented LIVE above —
                // the old "neutral stratum 0" note here was stale and removed, P2-10.)
                // AppendInteractionsForAuto 0x10591c-0x105948 (R249 gate re-derivation,
                // instruction-exact): the gate is TARGET-conditioned — when the TARGET
                // object's OBJD ObjectType (arg0->+4 -> +0x118 -> +0x12, cmpwi 7 at
                // 0x105928) is SimType AND the ACTOR's word 65 has neither species bit
                // (lha 0x60e at 0x105934; rlwinm 0x1e/0x1d test masks 0x2|0x4) — i.e. the
                // actor is a plain HUMAN (PersonGender 0=male/1=female; 2/3=dog, 4/5=cat
                // per EditPerson::SetGender 0x6b2e0 string table {"male","female","dogmale",
                // "dogfemale","catmale","catfemale"}) — then THIS target yields nothing
                // (autonomous human->sim targets belong to the person-person Append tail).
                // The previous landing tested the ACTOR's own OBJD here and `break`ed the
                // whole module scan: every avatar is OBJD SimType, so the scan died on the
                // first object and every decision gathered n=0 (the R249 n=0 root cause).
                var targetObjd = (objd != null) ? objd : obj.MasterDefinition;
                if (targetObjd != null && targetObjd.ObjectType == OBJDType.SimType
                    && (NativeGenderClass(caller) & 6) == 0) continue;

                var distance = Distance(caller, obj); // CalcShortDistance (0xc5e20) proxy
                var isPod = diagPod && (obj.Object != null && obj.Object.OBJ != null
                    && (obj.Object.OBJ.ChunkLabel ?? "").ToLowerInvariant().Contains("podium"));

                // AppendInteractionsForAuto (0x105900): iterate the object's TTAB BACKWARDS
                // (0x1059a0-0x105c6c: index count-1 .. 0) — the entry order feeds the draw.
                for (int e = treeTable.Interactions.Length - 1; e >= 0; e--)
                {
                    var entry = treeTable.Interactions[e];
                    Action<string> pod = null;
                    if (isPod) pod = (fate) => dPodLog.Add("obj" + obj.ObjectID + " row" + e
                        + " fn" + entry.ActionFunction + ": " + fate);
                    // 0x1059f8-0x105a00: advertise weight (entry+0x1c s16) must be > 0.
                    // 0x105b60-0x105b68: signed gate attr 36 (AutonomyLevel) >= weight.
                    var weight = (short)((ushort)entry.AutonomyThreshold & 0xFFFF);
                    if (weight <= 0 || autonomyLevel < weight)
                    {
                        if (pod != null) pod("weight-gate (weight=" + weight + " autonomyLevel=" + autonomyLevel + ")");
                        continue;
                    }
                    // 0x105b6c-0x105b74: unconditional candidate skip, entry flags mask
                    // 0x80 (rlwinm 0x18/0x18; TTAB "Debug" — the native "always block" bit).
                    if ((entry.Flags & (TTABFlags)0x80) != 0) continue;
                    // 0x105b00-0x105b28: APPEND gender gates — word-65 bit 0x2 (dog)
                    // requires entry mask 0x400 (rlwinm 0x15/0x15, raw 0x1059c0
                    // rlwinm SH=0x1f feeds bit 1), bit 0x4 (cat) requires entry mask
                    // 0x200 (rlwinm 0x16/0x16, 0x1059c4 bit 2). ENG-25 (AUD-16
                    // interpreter F1): the first landing TRANSPOSED these — a
                    // register-tracking slip the decompiler exposed; the hand-off gates
                    // (0x106488-0x1064b8) already carried the native pairing, so the
                    // file contradicted itself. The word is consumed through
                    // NativeGenderClass (dogs 2/3, cats 4/5).
                    if ((gender & 2) != 0 && (entry.Flags & (TTABFlags)0x400) == 0) continue;
                    if ((gender & 4) != 0 && (entry.Flags & (TTABFlags)0x200) == 0) continue;
                    // 0x105b28-0x105b38: children (attr58 in 1..17) are blocked by entry
                    // mask 0x10 (TS1NoChild) — rlwinm 0x1b/0x1b.
                    if (isChild && (entry.Flags & (TTABFlags)0x10) != 0)
                    {
                        if (pod != null) pod("CHILD-GATE (TS1NoChild 0x10; caller attr58="
                            + caller.GetPersonData(VMPersonDataVariable.PersonsAge) + ")");
                        continue;
                    }
                    // 0x105b3c-0x105b5c: a plain APPEND person (NOT a child, and
                    // word-65 bits 0x2|0x4 clear) is blocked by entry mask 0x40
                    // (TS1NoAdult) — rlwinm 0x19/0x19. ENG-25 (AUD-16 interpreter P3-5):
                    // the native tests the precomputed CHILD register (0x105b3c
                    // clrlwi/bne on the attr58∈[1,17] word computed at 0x105978) —
                    // every NON-child with the pet bits clear takes the 0x40 test,
                    // including attr58 ≥ 18 (the port's transformed adults carry
                    // PersonsAge=27, which the old `age == 0` form wrongly exempted).
                    if (!isChild && (gender & 6) == 0 && (entry.Flags & (TTABFlags)0x40) != 0) continue;
                    // 0x105b78-0x105bc0: visitor admission — RESIDENTIAL lots (GetZoningType
                    // != 1, the subfic/cntlzw idiom at 0x105b80-0x105b88) admit a visitor
                    // only with entry & (0x1|0x20) (0x105b90-0x105bac); downtown requires
                    // 0x1 only (0x105bb0-0x105bc0). Downtown (GetZoningType == 1) has no
                    // Simitone mount; the residential branch is the live one. DISCLOSED
                    // downtown reduction. (The previous landing admitted only mask 0x1.)
                    if (isVisitor && (entry.Flags & (TTABFlags)0x21) == 0) continue;
                    if (diag) dGatePassed++;
                    // 0x105bc4-0x105c18: downtown-visitor outdoor restriction (room == 0);
                    // dead without downtown lots. DISCLOSED.
                    // 0x105a04-0x105a8c: the vehicle-class {4,7} family-token override
                    // (GetInventoryByNeighbourID + FindToken) has no port inventory
                    // subsystem; a vehicle that failed the flag gates stays rejected.
                    // DISCLOSED omission.

                    // ---- R250 GATHER RESTORE: per-candidate TestInteraction ---------------
                    // The native runs TestInteraction PER CANDIDATE during gather: the two
                    // `bl TestInteraction` sites (0x106038 and 0x106128) sit INSIDE
                    // AppendInteractionsForAuto (0x105900..0x106220) — the two arms of its
                    // candidate-shape branch (object-target ctor 0x9ca90 vs person-person
                    // ctor 0x9cc50). Disassembly of both arms (R250 maintainer scan) shows
                    // the SAME call shape — TestInteraction(tester, candidate, NULL) — and
                    // IDENTICAL post-call tails (Interaction flags bit3 "passed" required,
                    // bit4 checked, then the 0x1068e0 push), so one uniform port path is
                    // faithful; the person-person arm stays the disclosed omission it
                    // already was (0x105c70-0x105cf4 below). TestInteraction's tree call
                    // (0x106570) runs the target's tree at the TTAB action number with
                    // params (auto=1, 0, actionNumber, 0) and the actor's temps copied in;
                    // the result s16 lands at person+0xae (Interaction bit4 = person+0xae
                    // != 0). person+0xae and Interaction bit4 have NO port-side consumer
                    // (no port field maps to the raw cXObject offset and nothing reads
                    // bit4), so the result is represented here by the CheckTS1Action
                    // null=fail / list=pass law. DISCLOSED.
                    // A false/null result excludes the candidate from the pool (the native
                    // requires the passed flag before the push; the R249 repair removed
                    // this run on a misreading of AppendInteractionsForAuto — the R250
                    // maintainer scan of the raw binary re-established the per-candidate
                    // law). Safety scale (P2-6, corrected): gate-passing entries are NOT
                    // "dozens" — the arm-time gather census on the default house-5 window
                    // admits 677 entries after the static gates, so most of the corpus
                    // reaches a test call. The cost bound is structural, not size: the
                    // InTS1Select guard above bounds test trees
                    // that dispatch prim 3, and VMThread.EvaluateCheck's synchronous-check
                    // law (yield/stuck/depth caps) fails fast on yielding/cycling check
                    // trees — the R249 window freeze cannot recur.
                    // P2-2 DISCLOSED NATIVE-VS-PORT NUANCE (shared temps): the native runs
                    // each candidate's TestInteraction against isolated per-candidate temp
                    // space, while this port's EvaluateCheck runs check trees on the actor's
                    // LIVE TempRegisters — the ~170-340 check trees a single decision runs
                    // all see (and can overwrite) each other's temp writes, so a tree that
                    // reads an actor temp written by an EARLIER candidate's tree diverges
                    // from the native per-candidate isolation. This is the pre-existing
                    // EvaluateCheck law (R249-audited); the mechanism is deliberately NOT
                    // changed here. A per-candidate temp clone is the candidate FUTURE
                    // adjudication if a divergence is ever observed — the symptom to watch
                    // is a candidate that passes its gather test but fails the identical
                    // hand-off re-test.
                    caller.ObjectData[(int)VMStackObjectVariable.HideInteraction] = 0;
                    List<VMPieMenuInteraction> candStrings = null;
                    try
                    {
                        var candAction = obj.GetAction((int)entry.TTAIndex, caller, vm.Context, false);
                        if (candAction != null)
                        {
                            candStrings = caller.Thread.CheckTS1Action(candAction, true,
                                new short[] { 1, 0, (short)entry.ActionFunction, 0 });
                        }
                    }
                    catch { candStrings = null; } // a test that dies fails the candidate (the hand-off's own convention)
                    if (candStrings == null) { if (diag) dTestFail++; if (pod != null) pod("TEST-FAIL"); continue; } // TestInteraction failed: the candidate never reaches the pool (native passed-flag law)
                    // 0x109cfc-0x109d08: interaction flags bit0 -> ScoredInteraction+0xc
                    // byte. The ctor tail (0x9cbb4-0x9cbcc) copies TTAB entry flags mask
                    // 0x01000000 (decoded this round from the binary; the person-person
                    // ctor sets it unconditionally).
                    var flag = (byte)(((entry.Flags & (TTABFlags)0x01000000) != 0) ? 1 : 0);

                    // ---- GATHER SCORING (AUD-18-A F-3 RESOLUTION; refutes R250) --------
                    // The binary runs the WHOLE score sequence per candidate inside the
                    // gather loop (bl TestInteraction 0x109d1c -> bl GetInteractionScore
                    // 0x109d48, loop closing 0x100f44 `ble 0x100c10` back to the object
                    // scan); the post-draw tail 0x100f48+ contains no test/score call.
                    // The check tree above (CheckTS1Action) IS the native TestInteraction
                    // run: its result strings carry both the harvested Param0 and the
                    // MotiveAdChanges of the TREE-MUTATED ad copy the score consumes.
                    short candParam0 = (short)0;
                    var adChanges = (candStrings.Count > 0) ? candStrings[0].MotiveAdChanges : null;
                    if (candStrings.Count > 0) candParam0 = candStrings[0].Param0;
                    var ads = (TTABMotiveEntry[])entry.MotiveEntries.Clone();
                    if (adChanges != null)
                    {
                        foreach (var kv in adChanges)
                        {
                            var motiveI = kv.Key & 0xFFFF;
                            if (motiveI < 0 || motiveI >= ads.Length) continue;
                            switch (kv.Key >> 16)
                            {
                                case 0: ads[motiveI].EffectRangeMinimum = kv.Value; break;
                                case 1: ads[motiveI].EffectRangeDelta = kv.Value; break;
                                case 2: ads[motiveI].PersonalityModifier = (ushort)kv.Value; break;
                            }
                        }
                    }
                    // GetAttenuationValue (0x155340) + atten = N / (N + dist * att),
                    // N = CFG+0x20 = 1.0 (0x109d2c-0x109d44).
                    var attValue = GetAttenuationValue(entry, isVisitor);
                    var atten = CFG_ATTENUATION_N / (CFG_ATTENUATION_N + distance * attValue);
                    // GetInteractionScore (0x9f9d4, call site 0x109d48) on the mutated ads.
                    var hPrime = GetInteractionScore(caller, ads, curves);
                    // 0x109d4c-0x109d54: the sentinel drop — H' == CFG+4 (0.0) excludes
                    // the candidate outright.
                    if (hPrime == CFG_SCORE_SENTINEL)
                    {
                        if (pod != null) pod("SENTINEL (hPrime==0)");
                        continue;
                    }
                    // 0x109d58-0x109d5c: score = atten * (H' - H).
                    var score = atten * (hPrime - baseH);
                    // 0x109d64-0x109d74: the push gate — score >= the FCNS min autonomy
                    // score for the actor's class (family/visitor; live 1e-7).
                    if (score < minScore)
                    {
                        if (pod != null) pod("SCORE " + score.ToString("E3") + " < minScore " + minScore.ToString("E3")
                            + " (hPrime " + hPrime.ToString("F4") + " baseH " + baseH.ToString("F4")
                            + " atten " + atten.ToString("F4") + " dist " + distance.ToString("F1") + ")");
                        continue;
                    }
                    if (diag) dPool++;
                    if (pod != null) pod("POOLED score " + score.ToString("E3"));
                    candidates.Add(new ScoredCandidate
                    {
                        Callee = obj,
                        Entry = entry,
                        CalleeId = obj.ObjectID,
                        // ScoredInteraction+4 = the mutated copy's +0x18 action number.
                        // The port's check-tree surface does not rewrite the action
                        // number (no MotiveActionChanges channel exists), so the entry's
                        // own ActionFunction stands. DISCLOSED.
                        ActionNumber = entry.ActionFunction,
                        Param0 = candParam0, // harvested from the gather TestInteraction
                        Score = score,
                        Flag = flag,
                        Dist = distance
                    });
                    // 0x105c70-0x105cf4: the person-person tail (actor OBJDType == Person and
                    // a specific PersonFinder lookup with the target's current-entry flags
                    // bit) is not reproduced; port social coverage flows from avatar entities
                    // in the module order like every other object. DISCLOSED omission.
                }
            }
            // EXP-14 hdserve-residual: the per-target diag stash now runs on the
            // ZERO-CANDIDATE path too (it previously sat after the sort point, so an
            // n=0 target gather never refreshed TS1GatherDiagTargetLast and the probe
            // read a stale '(no target gather yet)' while the traveler HAD gathered —
            // diag7's tgt line was an artifact, its decision log showed 9 n=0 runs).
            if (diag)
            {
                _diagObjSeen = dObjSeen; _diagGatePassed = dGatePassed;
                _diagTestFail = dTestFail; _diagPool = dPool;
                _diagCaller = context.Caller != null ? context.Caller.ObjectID : 0;
                if (_diagCaller == _diagTargetOid)
                    TS1GatherDiagTargetLast = "objSeen=" + dObjSeen + " gatePassed=" + dGatePassed
                        + " testFail=" + dTestFail + " pool=" + dPool + " caller=obj" + _diagCaller;
                if (diagPod && dPodLog.Count > 0)
                    TS1GatherDiagPodiumLast = string.Join(" | ", dPodLog.Take(12));
            }
            // 0x109dd8-0x109de0: no candidates -> return 0.
            if (candidates.Count == 0)
            {
                EmitDecision(caller, vm, isVisitor, isChild, false, 0f, -1, 0, null, null, null, motiveSnapshot);
                return VMPrimitiveExitCode.GOTO_FALSE;
            }

            // Gather-order snapshot (the seam's PreSortCandidates) — captured BEFORE the
            // transpile mutates the list in place.
            var gatherOrder = ProjectCandidates(candidates);

            // ---- LAW STEP 5: the sort (0x109df8 -> qsort wrapper 0x5914e0 -> 0x59a370) -
            // CW heapsort with the game comparator CompareScoredInteractions
            // (0x1011b0, eps +1e-7 / -1e-7, AUD-18-A corrected polarity). With
            // the restored REAL gather scores this genuinely sorts the pool DESCENDING
            // (best first) — the old "net rotate-left-by-one" claim was an artifact of
            // the R250 lazy-zero scores and is retired. The routine is ported
            // LITERALLY (element granularity) so permutations match the CFG agent's
            // transpile (tools/iff-dump/r249-freewill-cfg/verify.py, group H).
            GameHeapsort(candidates);

            // ---- LAW STEP 6: the winner draw (0x109dd8-0x109ea8) -----------------------
            // K = max(1, FCNS "random selection count" - trunc(attr18 / 2000)) (0x109dfc-
            // 0x109e2c; the 0x10624dd3 magic is trunc(t/2000)).
            var randomSelectionCount = (int)Globals.GetAutonomyConstant("random selection count", 10.0f);
            var attr18 = caller.GetPersonData((VMPersonDataVariable)18); // LogicSkill (+0x5b0)
            var k = Math.Max(1, randomSelectionCount - (attr18 / 2000));
            var pool = Math.Min(k, candidates.Count);
            // The rand source is the port's seeded VMContext xorshift (documented
            // adaptation of GetNextRandomNumber); the frozen seam captures the seed
            // immediately BEFORE the draw so a checker can replay it.
            var seed = vm.Context.RandomSeed;
            int idx;
            // 0x109e30-0x109ea8: deterministic idx = 0 iff (visitor OR post-sort[0].flag)
            // AND GetZoningType == 1 (downtown). The raw branch (verified mechanically this
            // round against the pinned idiom bytes: the subfic/cntlzw chain yields 1 iff
            // zoning == 1, and bne targets the `li r3,0`) forces idx 0 ON downtown; the CFG
            // decode §4(c) states the same. Simitone mounts no downtown lots, so the
            // uniform draw runs in practice. (Task step 6 quoted the inverse zoning
            // polarity — superseded by the cfg decode per the task's own priority order;
            // DISCLOSED.)
            bool deterministic = (isVisitor || candidates[0].Flag != 0);
            if (deterministic && ZoningIsDowntown())
            {
                idx = 0; // 0x109ea8: li r3, 0
            }
            else
            {
                idx = (int)vm.Context.NextRandom((ulong)pool); // 0x109e70-0x109ea4: rand() % min(K, count)
            }
            // 0x109eac-0x109eb0: idx >= count -> return 0.
            if (idx >= candidates.Count)
            {
                EmitDecision(caller, vm, isVisitor, isChild, false, 0f, -1, seed, gatherOrder, candidates, null, motiveSnapshot);
                return VMPrimitiveExitCode.GOTO_FALSE;
            }
            var winner = candidates[idx];

            // ---- (no hand-off re-test — AUD-18-A call-site proof) ---------------------
            // The native post-draw tail (0x100f48+) runs NO TestInteraction and NO
            // GetInteractionScore: the R250 "winner re-test + hand-off score" block
            // was the gather loop's per-candidate sequence (0x109d1c/0x109d48 inside
            // the loop, closing 0x100f44) misattributed to the hand-off, and is
            // REMOVED. The winner carries its gather score (the cutoff operand) and
            // the Param0 harvested by its gather check run; the native hand-off
            // itself only writes person+0x72 = action number and StackElem+4 = the
            // object id (LAW STEP 8's queued-action mapping below).

            // ---- LAW STEP 7: the cutoff (0x109eb4-0x109ee8; MAINTAINER ADJUDICATION) ---
            // 0x109eb4-0x109ec4: r0 = attr slot 0 (posture/engagement; person+0x58c).
            // attr0 == 0 (standing/not engaged) -> accept unconditionally (0x109ecc beq).
            // engaged -> 0x109ed0-0x109ee0: f1 = the WINNER'S POOL SCORE (lfs f1,8(r5) —
            // winner+8, the gather score, NOT a recomputed hand-off score), f0 = the FCNS
            // "min autonomy score for sitting" (lfs f0,0(r3) through TOC-0x70cc; runtime
            // 1e-6), fcmpo cr0,f1,f0, bge -> ACCEPT IFF score >= cutoff (never <=).
            var sittingCutoff = Globals.GetAutonomyConstant("min autonomy score for sitting", 0.2f);
            var attr0 = caller.GetPersonData(VMPersonDataVariable.Posture);
            var accept = (attr0 == 0) || (winner.Score >= sittingCutoff);

            // ---- OBSERVATION SEAM (frozen contract, read-only, exception-isolated) -----
            EmitDecision(caller, vm, isVisitor, isChild, accept, sittingCutoff, idx, seed,
                gatherOrder, candidates, winner, motiveSnapshot);

            if (!accept)
            {
                return VMPrimitiveExitCode.GOTO_FALSE; // the port's false path (GOTO_FALSE)
            }

            // ---- LAW STEP 8: winner insertion (0x109ee8-0x109f10 + TryGosubFoundAction)
            // Native hand-off: person+0x72 (u16) = winner action number, stack+4 (s16) =
            // winner object id; the sibling prim re-finds (object, entry) and makes it the
            // current action. Port mapping: the queued-action flow the TSO path already
            // uses — GetAction re-resolves the entry by its TTA index, Args[0] carries the
            // gather check-tree Param0, Priority stays Autonomous, StackObject = the callee.
            var qaction = winner.Callee.GetAction((int)winner.Entry.TTAIndex, caller, vm.Context, false,
                new short[] { winner.Param0, 0, 0, 0 });
            if (qaction != null)
            {
                qaction.Priority = (short)VMQueuePriority.Autonomous;
                caller.Thread.EnqueueAction(qaction);
                context.StackObject = winner.Callee;
                return VMPrimitiveExitCode.GOTO_TRUE;
            }
            return VMPrimitiveExitCode.GOTO_FALSE;
            } catch (Exception ex_) {
                // P2-8: a decision that dies in this catch-all must not vanish — a silent
                // GOTO_FALSE is indistinguishable from a lawful reject. Mirror once per
                // decision to the gate log with the exception (the [SimAnticsExc] convention).
                System.Console.WriteLine("[SimAnticsFBA] TS1 select catch-all: av=obj" + caller.ObjectID +
                    " exc=" + ex_.GetType().Name + ": " + ex_.Message +
                    " @" + (ex_.StackTrace?.Split('\n').FirstOrDefault(x => x.Contains(".cs")) ?? "no-cs-frame"));
                InTS1Select = false;
                return VMPrimitiveExitCode.GOTO_FALSE;
            } finally
            {
                InTS1Select = false;
            }
        }

        /// <summary>
        /// GetZoningType == 1 (downtown). Simitone mounts no downtown lots — the port's
        /// zoning notion has no downtown value, so this is the constant false (DISCLOSED
        /// reduction; the deterministic draw branch is dead in practice).
        /// </summary>
        private static bool ZoningIsDowntown()
        {
            return false;
        }

        /// <summary>
        /// The port's decoded TS1 distance law (VMGetDistanceTo TS1 branch) as the
        /// CalcShortDistance (0xc5e20) proxy: floor(euclid/16) tiles, +5 per extra level
        /// with a 20-per-level floor.
        /// </summary>
        private static float Distance(VMAvatar caller, VMEntity obj)
        {
            var pos1 = caller.Position;
            var pos2 = obj.Position;
            var result = (float)Math.Floor(Math.Sqrt(Math.Pow(pos1.x - pos2.x, 2) + Math.Pow(pos1.y - pos2.y, 2)) / 16.0);
            var levelDiff = Math.Abs(pos2.Level - pos1.Level);
            return (float)Math.Max(20 * levelDiff, result + 5 * levelDiff);
        }

        /// <summary>
        /// GetAttenuationValue__14TreeTableEntryFb (0x155340): 0 -> the entry's own float
        /// (+0x10); 1 -> the default pair's first (0.0, const pool 0x59a840); 2/3/4 ->
        /// low/moderate/high from the family or visitor set (FCNS-overridden:
        /// {0.1,0.3,0.6} / {0.01,0.02,0.03}); other -> the default pair's second (0.02).
        /// (Skeptic S7 enum mapping; r249-freewill-cfg §7.)
        /// </summary>
        private static float GetAttenuationValue(TTABInteraction entry, bool visitor)
        {
            switch (entry.AttenuationCode)
            {
                case 0: return entry.AttenuationValue;
                case 1: return 0.0f;
                case 2:
                    return visitor ? Globals.GetAutonomyConstant("visitor low attenuation", 0.002f)
                                   : Globals.GetAutonomyConstant("low attenuation", 0.002f);
                case 3:
                    return visitor ? Globals.GetAutonomyConstant("visitor moderate attenuation", 0.02f)
                                   : Globals.GetAutonomyConstant("moderate attenuation", 0.02f);
                case 4:
                    return visitor ? Globals.GetAutonomyConstant("visitor high attenuation", 0.1f)
                                   : Globals.GetAutonomyConstant("high attenuation", 0.1f);
                default: return 0.02f;
            }
        }

        /// <summary>
        /// GetCurrentScore__13MotiveEffectsFv (0x9fc20): mean over the 9 fixed entries of
        /// curve_j(motiveFloat_j); empty curves contribute 0.0 (CFG2+0); divide by 9.
        /// </summary>
        private static float GetCurrentScore(VMTS1NativeCurve[] curves, float[] motives)
        {
            float sum = 0f;
            for (int j = 0; j < ScoreCurveMotives.Length; j++)
            {
                var curve = curves[j];
                if (curve != null && curve.Count > 0) sum += curve.Eval(motives[j]);
            }
            return sum / 9f;
        }

        /// <summary>
        /// GetInteractionScore__13MotiveEffectsFP11TreeTableAd (0x9f9d0): mean over the 9
        /// fixed entries of curve_j(motiveFloat_j + effect_j) where effect_j is read from
        /// the ad row for this entry's motive id and scaled by CFG2+8 (0.001). R250: takes
        /// the ad ROWS directly — the hand-off call site (0x109d48) passes the COPY of the
        /// winner entry's rows with the re-test's MotiveAdChanges applied (the native
        /// consumes the tree-mutated ctor copy).
        /// </summary>
        private static float GetInteractionScore(VMAvatar person, TTABMotiveEntry[] ads, VMTS1NativeCurve[] curves)
        {
            float sum = 0f;
            for (int j = 0; j < ScoreCurveMotives.Length; j++)
            {
                var curve = curves[j];
                if (curve == null || curve.Count == 0) continue; // empty curve contributes CFG2+0 = 0.0
                var effect = AdEffect(person, ads, ScoreCurveMotives[j]);
                sum += curve.Eval(person.GetMotiveData((VMMotive)ScoreCurveMotives[j]) + effect);
            }
            return sum / 9f;
        }

        /// <summary>
        /// One ad row (0x9fa08-0x9fad4): row = 3 s16 at ad + 6*motiveId. DATA-VERIFIED
        /// port-field mapping (this round's TTAB corpus scan): the in-memory row is
        /// {selector, base, magnitude} = {PersonalityModifier, EffectRangeMinimum,
        /// EffectRangeDelta} — only the third on-disk halfword stays within MOTIVETAB's
        /// row range across the whole shipped corpus (0..22), so it is the selector.
        /// row0 == 0 -> effect = base + magnitude; else mv = attr[MOTIVETAB[row0].idx]
        /// (1000-mv when inverted) and effect = base + mv*magnitude/1000 (CFG2+4 = 1000).
        /// Finally scaled by CFG2+8 = 0.001.
        /// </summary>
        private static float AdEffect(VMAvatar person, TTABMotiveEntry[] ads, int motiveId)
        {
            if (motiveId >= ads.Length) return 0f;
            var row = ads[motiveId];
            var selector = (short)row.PersonalityModifier;
            float core;
            if (selector == 0)
            {
                core = row.EffectRangeMinimum + row.EffectRangeDelta; // 0x9faac-0x9fad4
            }
            else
            {
                var mv = (float)MotiveValue(person, selector); // 0x9fa1c-0x9fa44
                core = row.EffectRangeMinimum + mv * row.EffectRangeDelta / 1000f; // CFG2+4 divisor
            }
            return core * 0.001f; // CFG2+8 scale (0x9fad8-0x9fae0)
        }

        /// <summary>
        /// mv = the person attribute at MOTIVETAB[row].idx (the s16 at person+0x58c+2*idx;
        /// MotiveEffects+0xbc holds the person pointer), inverted 1000-mv when the row's
        /// invert flag is set (0x9fa44 subfic 0x3e8). Rows outside the recovered table
        /// (native would read past the 23-row pool) fall back to mv 0. DISCLOSED guard.
        /// </summary>
        private static short MotiveValue(VMAvatar person, int motivetabRow)
        {
            if (motivetabRow < 1 || motivetabRow >= MotiveTabIdx.Length) return 0;
            var attr = MotiveTabIdx[motivetabRow];
            var mv = person.GetPersonData((VMPersonDataVariable)attr);
            if (MotiveTabInv[motivetabRow]) mv = (short)(1000 - mv);
            return mv;
        }

        /// <summary>
        /// The scoring curve set (cXPerson::PostLoad 0x1113b0 / 0x111584-0x1115e0):
        /// per-person custom private-file STR#MotiveEffectsID (selector+0x66) when the
        /// person OBJD carries one, else the child set GLOB STR#503 when attr58 is in
        /// 1..17, else the adult set GLOB STR#501. Entry j is filled from string j+1 via
        /// AddPointsFromText (0x117920, sscanf "(%d;%d)") — POINTS KEEP STRING ORDER
        /// (PiecewiseFn::AddPoint appends; the top-down evaluation scan depends on it).
        /// Global sets are cached (static content); custom sets are cached per object id.
        /// </summary>
        private static VMTS1NativeCurve[] GetScoreCurves(VMAvatar caller)
        {
            var objd = caller.Object != null ? caller.Object.OBJ : null;
            var customId = (objd != null) ? objd.MotiveEffectsID : (ushort)0;
            if (customId != 0 && caller.Object != null && caller.Object.Resource != null)
            {
                var cacheKey = ((long)customId << 16) | (long)caller.ObjectID;
                VMTS1NativeCurve[] custom;
                lock (CustomCurveCache)
                {
                    if (!CustomCurveCache.TryGetValue(cacheKey, out custom))
                    {
                        custom = CurvesFromSTR(caller.Object.Resource.Get<STR>(customId));
                        CustomCurveCache[cacheKey] = custom;
                    }
                }
                if (custom != null) return custom;
            }
            var age = caller.GetPersonData(VMPersonDataVariable.PersonsAge);
            var child = age > 0 && age < 0x12;
            var curves = child ? ChildScoreCurves : AdultScoreCurves;
            return curves ?? EmptyCurves;
        }

        private static readonly Dictionary<long, VMTS1NativeCurve[]> CustomCurveCache = new Dictionary<long, VMTS1NativeCurve[]>();
        private static VMTS1NativeCurve[] _AdultScoreCurves;
        private static VMTS1NativeCurve[] _ChildScoreCurves;
        private static readonly VMTS1NativeCurve[] EmptyCurves = new VMTS1NativeCurve[9];

        private static VMTS1NativeCurve[] AdultScoreCurves
        {
            get { return _AdultScoreCurves ?? (_AdultScoreCurves = CurvesFromSTR(GlobalsGlobalSTR(501))); }
        }

        private static VMTS1NativeCurve[] ChildScoreCurves
        {
            get { return _ChildScoreCurves ?? (_ChildScoreCurves = CurvesFromSTR(GlobalsGlobalSTR(503))); }
        }

        private static STR GlobalsGlobalSTR(ushort id)
        {
            var provider = Globals;
            var global = provider.Get("global");
            return global.Resource.Get<STR>(id);
        }

        /// <summary>Parse "(x;y)" pairs in STRING order; entry j uses string j+1.</summary>
        private static VMTS1NativeCurve[] CurvesFromSTR(STR str)
        {
            if (str == null) return null;
            var result = new VMTS1NativeCurve[9];
            for (int j = 0; j < 9; j++)
            {
                var text = str.GetString(j + 1);
                if (text == null) continue;
                result[j] = VMTS1NativeCurve.FromText(text);
            }
            return result;
        }

        /// <summary>
        /// Faithful transpile of the CW routine at 0x59a370 ("qsort", reached through
        /// the 0x5914e0 wrapper whose prologue matches this shape) with the game
        /// comparator: build phase r24 = n/2+1 -> 1 with sift-down, extraction phase
        /// swapping heap[1] &lt;-&gt; heap[end], children at 2i/2i+1. With the AUD-18-A
        /// corrected comparator and real gather scores the net permutation is the
        /// true descending sort (identity permutations only within the 1e-7 band).
        /// </summary>
        private static void GameHeapsort(List<ScoredCandidate> a)
        {
            int n = a.Count;
            if (n < 2) return;
            int r24 = (n >> 1) + 1;
            int r30 = r24;        // element cursor (the routine runs at width 16; element granularity here)
            int r25 = n;
            int r27 = r24 - 1;    // (r24-1)*w
            int r28 = n - 1;      // (n-1)*w
            while (true)          // L3c0
            {
                if (r24 > 1)
                {
                    r30 -= 1; r27 -= 1; r24 -= 1;
                }
                else
                {
                    var t = a[r27]; a[r27] = a[r28]; a[r28] = t;
                    r25 -= 1;
                    if (r25 == 1) return;
                    r28 -= 1;
                }
                int r0 = r30 - 1; // r30 + r31 with r31 = -w (L414)
                int r26 = r24;
                int r29 = r0;
                while (true)      // L4bc
                {
                    if (r26 * 2 <= r25)
                    {
                        r26 *= 2; // L424
                        r0 = r26 - 1;
                        int r19 = r29;
                        r29 = r0; // left child
                        if (r26 < r25)
                        {
                            int r20 = r29 + 1;
                            if (CompareCandidates(a[r29], a[r20]) < 0)
                            {
                                r29 = r20;
                                r26 += 1;
                            }
                        }
                        if (CompareCandidates(a[r19], a[r29]) >= 0) break;
                        var t2 = a[r19]; a[r19] = a[r29]; a[r29] = t2;
                    }
                    else break;
                }
            }
        }

        /// <summary>
        /// CompareScoredInteractions (comparator 0x1011b0, AUD-18-A corrected):
        /// delta = b.score - a.score (fsubs); +1 iff delta &gt; +1e-7; -1 iff
        /// delta &lt; -1e-7; else 0 (ties inside the band compare EQUAL). The
        /// first landing transposed the -1/0 arms (ties returned -1) — fixed;
        /// with real gather scores the heapsort therefore yields the pool in
        /// DESCENDING (best-first) order.
        /// </summary>
        private static int CompareCandidates(ScoredCandidate a, ScoredCandidate b)
        {
            float delta = b.Score - a.Score;
            if (delta > 1e-7f) return 1;
            if (delta < -1e-7f) return -1;
            return 0;
        }

        // ---- R249 OBSERVATION SEAM (frozen contract) -----------------------------------
        // Read-only, exception-isolated, static. Invoked once per TS1 decision just before
        // cutoff evaluation/insertion (and on the empty-pool and out-of-range exits, with
        // DrawnIdx -1). No VM behavior change when no listener is attached. The snapshot
        // types (VMTS1Decision / VMTS1DecisionCandidate) live at namespace level below so
        // external observers can name them directly.

        /// <summary>
        /// Frozen battery contract: fired once per TS1 free-will decision with the full
        /// snapshot. Handlers must stay fast and non-mutating; exceptions are swallowed.
        /// </summary>
        public static event Action<VMTS1Decision> TS1DecisionObserved;

        // EXP-14 follow-up diagnostics (probe-gated, inert unless set).
        public static bool TS1GatherDiag;
        public static string TS1GatherDiagLast = "";
        public static string TS1GatherDiagTargetLast = "(no target gather yet)";
        // EXP-14 hdserve-residual: per-podium-row fate lines from the target caller's
        // gather (probe-gated; empty in play).
        public static string TS1GatherDiagPodiumLast = "";
        internal static int _diagObjSeen, _diagGatePassed, _diagTestFail, _diagPool, _diagCaller;
        public static int _diagTargetOid;

        /// <summary>Frozen battery contract: the most recent decision snapshot (pollable).</summary>
        public static VMTS1Decision LastDecision { get; private set; }

        private static void EmitDecision(VMAvatar caller, VM vm, bool isVisitor, bool isChild,
            bool accepted, float cutoff, int drawnIdx, ulong seed,
            List<VMTS1DecisionCandidate> gatherOrder, List<ScoredCandidate> sorted, ScoredCandidate winner,
            float[] motives)
        {
            try
            {
                var decision = new VMTS1Decision
                {
                    CallerId = caller.ObjectID,
                    IsVisitor = isVisitor,
                    IsChild = isChild,
                    FreeWill = VM.FreeWillEnabled,
                    Cutoff = cutoff,
                    DrawnIdx = drawnIdx,
                    Accepted = accepted,
                    RandomSeed = seed,
                    MotiveSnapshot = (float[])motives.Clone(),
                    Candidates = ProjectCandidates(sorted),
                    PreSortCandidates = gatherOrder,
                    Winner = (winner == null) ? null : new VMTS1DecisionCandidate
                    {
                        CalleeId = winner.CalleeId,
                        ActionNumber = winner.ActionNumber,
                        Param0 = winner.Param0,
                        // AUD-18-A: the winner's POOL score (gather-time, from the
                        // tree-mutated ad copy) — the cutoff's operand.
                        Score = winner.Score,
                        Flag = winner.Flag,
                        Dist = winner.Dist
                    }
                };
                LastDecision = decision;
                TS1GatherDiagLast = "objSeen=" + _diagObjSeen + " gatePassed=" + _diagGatePassed
                    + " testFail=" + _diagTestFail + " pool=" + _diagPool
                    + " caller=obj" + _diagCaller;
                var evt = TS1DecisionObserved;
                if (evt == null) return;
                foreach (Action<VMTS1Decision> handler in evt.GetInvocationList())
                {
                    try { handler(decision); }
                    catch { } // the seam must never break the VM
                }
            }
            catch { } // the seam must never break the VM
        }

        private static List<VMTS1DecisionCandidate> ProjectCandidates(List<ScoredCandidate> list)
        {
            if (list == null) return null;
            var result = new List<VMTS1DecisionCandidate>(list.Count);
            foreach (var c in list)
            {
                result.Add(new VMTS1DecisionCandidate
                {
                    CalleeId = c.CalleeId,
                    ActionNumber = c.ActionNumber,
                    Param0 = c.Param0,
                    Score = c.Score,
                    Flag = c.Flag,
                    Dist = c.Dist
                });
            }
            return result;
        }

        // ---- internal candidate record -------------------------------------------------

        private sealed class ScoredCandidate
        {
            public VMEntity Callee;
            public TTABInteraction Entry;
            public short CalleeId;
            public ushort ActionNumber;
            public short Param0;
            public float Score;      // the gather score (AUD-18-A restored: real, gated
                                     // by the FCNS min-autonomy push + sentinel drop)
            public byte Flag;
            public float Dist;
        }
    }

    /// <summary>
    /// The native piecewise-linear motive curve (PiecewiseFn): points in INSERTION order
    /// (AddPointsFromText appends), evaluated f32 with the top-down scan and precomputed
    /// reciprocal spans (0x9faf4-0x9fb98). Below the first x -> y[0]; above the last x ->
    /// y[last]; else y[i] + (v-x[i]) * (1/(x[i+1]-x[i])) * (y[i+1]-y[i]).
    /// </summary>
    public sealed class VMTS1NativeCurve
    {
        public float[] X;
        public float[] Y;
        public int Count;

        public static VMTS1NativeCurve FromText(string text)
        {
            // AddPointsFromText (0x117920): scan for "(%d;%d)" pairs, in order.
            var xs = new List<float>();
            var ys = new List<float>();
            int i = 0;
            while (i < text.Length)
            {
                var open = text.IndexOf('(', i);
                if (open < 0) break;
                var semi = text.IndexOf(';', open + 1);
                var close = (semi < 0) ? -1 : text.IndexOf(')', semi + 1);
                if (semi < 0 || close < 0) break;
                int x, y;
                if (int.TryParse(text.Substring(open + 1, semi - open - 1).Trim(), out x) &&
                    int.TryParse(text.Substring(semi + 1, close - semi - 1).Trim(), out y))
                {
                    xs.Add(x);
                    ys.Add(y);
                }
                i = close + 1;
            }
            if (xs.Count == 0) return null;
            return new VMTS1NativeCurve { X = xs.ToArray(), Y = ys.ToArray(), Count = xs.Count };
        }

        public float Eval(float v)
        {
            // 0x9fb10-0x9fb34: scan from the top while v <= x[i]; i ends at the first
            // index with x[i] < v (or -1 when v is below the whole range).
            int i = Count - 1;
            while (i >= 0 && v <= X[i]) i--;
            if (i == Count - 1) return Y[Count - 1]; // above range (0x9fb34-0x9fb50)
            if (i < 0) return Y[0];                  // below range (0x9fb54-0x9fb64)
            var span = 1f / (X[i + 1] - X[i]);       // precomputed reciprocal spans (f32)
            return Y[i] + ((v - X[i]) * span) * (Y[i + 1] - Y[i]); // 0x9fb68-0x9fb98, f32 order
        }
    }

    /// <summary>One post-"sort" (or pre-sort, in <c>VMTS1Decision.PreSortCandidates</c>) candidate
    /// on the R249 TS1 free-will observation seam.</summary>
    public sealed class VMTS1DecisionCandidate
    {
        public short CalleeId;
        public ushort ActionNumber;
        public short Param0;
        public float Score;
        public byte Flag;
        public float Dist;
    }

    /// <summary>The decision snapshot carried by <see cref="VMFindBestAction.TS1DecisionObserved"/>
    /// (frozen R249 battery contract; exposed by <see cref="VMFindBestAction.LastDecision"/>).</summary>
    public sealed class VMTS1Decision
    {
        public short CallerId;
        public bool IsVisitor;
        public bool IsChild;
        public bool FreeWill;
        public float Cutoff;
        public int DrawnIdx;
        public bool Accepted;
        public ulong RandomSeed;
        /// <summary>The 9 curve motive floats at decision (ids {5,6,7,8,9,3,13,14,15}).</summary>
        public float[] MotiveSnapshot;
        /// <summary>The candidate array in post-transpile ("sort") order.</summary>
        public List<VMTS1DecisionCandidate> Candidates;
        /// <summary>The candidate array in gather order (before the transpile).</summary>
        public List<VMTS1DecisionCandidate> PreSortCandidates;
        /// <summary>The drawn winner, when one exists.</summary>
        public VMTS1DecisionCandidate Winner;
    }

    public class VMFindBestActionOperand : VMPrimitiveOperand
    {
        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes)
        {
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN))
            {
            }
        }

        public void Write(byte[] bytes) { }
        #endregion
    }
}
