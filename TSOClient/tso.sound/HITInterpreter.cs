using System;
using FSO.Files.HIT;

namespace FSO.HIT
{
    public class HITInterpreter
    {
        public static HITInstruction[] Instructions = new HITInstruction[] {
            NOP,            Note,           NoteOn,         NoteOff,        LoadB,          LoadL,          Set,            Call, 
            Return,         Wait,           CallEntryPoint, WaitSamp,       End,            Jump,           Test,           NOP, 
            Add,            Sub,            Div,            Mul,            Cmp,            Less,           Greater,        Not, 
            Rand,           Abs,            Limit,          Error,          Assert,         AddToGroup,     RemoveFromGroup, GetVar, 
            Loop,           SetLoop,        Callback,       SmartAdd,       SmartRemove,    SmartRemoveAll, SmartSetCrit,   SmartChoose, 
            And,            NAnd,           Or,             NOr,            XOr,            Max,            Min,            Inc, 
            Dec,            PrintReg,       PlayTrack,      KillTrack,      Push,           PushMask,       PushVars,       CallMask, 
            CallPush,       Pop,            Test1,          Test2,          Test3,          Test4,          IfEqual,        IfNotEqual,
            IfGreater,      IfLess,         IfGreatOrEq,    IfLessOrEq,     SmartSetList,   SeqGroupKill,   SeqGroupWait,   SeqGroupReturn,
            GetSrcDataField, SeqGroupTrackID, SetLL,        SetLT,          SetTL,          WaitEqual,      WaitNotEqual,   WaitGreater,
            WaitLess,       WaitGreatOrEq,  WaitLessOrEq,   Duck,           Unduck,         TestX,          SetLG,          SetGL,
            Throw,          SetSrcDataField, StopTrack,     SetChanReg,     PlayNote,       StopNote,       KillNote,       SmartIndex,
            NoteOnLoop

        };

        /// <summary>
        /// Does nothing.
        /// </summary>
        public static HITResult NOP(HITThread thread)
        {
            return HITResult.CONTINUE;
        }

        /// <summary>
        /// AUD-14: native CheckedRegId (0x312D40) validates a HIT operand as a
        /// REGISTER ID — 0x01..0x7f pass; id 0 and >0x7f reject (error-report via
        /// the global object's vtbl+0xd0, result discarded) and the handler branches
        /// to the shared CONTINUE tail (0x312A98) with the operand byte already
        /// consumed by the cursor. Two-operand handlers below fetch BOTH operand
        /// bytes before validating, so a first-operand rejection cannot desync the
        /// bytecode cursor.
        /// </summary>
        private static bool IsValidRegId(int id)
        {
            return id >= 1 && id <= 0x7f;
        }

        /// <summary>
        /// AUD-14 (native decode: cTrackPlayer dispatch[0x01]): TWO register-id
        /// operands, each CheckedRegId-validated — NOT immediates, and NOT a NoteOn
        /// call (note_on 0x02 is the playback op; AUD-09's NoteOn semantics were
        /// the integration-crash cause). The historic 0-operand no-op desynced the
        /// bytecode cursor at every one of the 361 corpus sites
        /// (SoundData/simsgeneratedhitsource.hit); this handler now consumes and
        /// validates both operands natively. Action side, decoded but bounded out
        /// of this tranche: reg[a] routes to the guarded channel (vtbl+0x9c on
        /// self+0x1e8); reg[b] OVERWRITES the thread volume with the active
        /// track's base volume + reg[b] (self+0x194 := [[r30]+0x58] + reg[b]) then
        /// UpdateVolPan; instance pitch = pitchBase + reg[0x15] − 0xe10 (vtbl+0x4c).
        /// Porting those writes needs the vtbl-slot semantics and the native
        /// volume-scale decode — documented bounded subset until then; in the
        /// bounded subset a rejected id pair and a valid pair both CONTINUE, exactly
        /// like the native rejection tail.
        /// </summary>
        public static HITResult Note(HITThread thread)
        {
            var a = thread.ReadByte();
            var b = thread.ReadByte();
            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Play a note, whose ID resides in the specified variable.
        /// </summary>
        public static HITResult NoteOn(HITThread thread)
        {
            var dest = thread.ReadByte();

            thread.WriteVar(dest, thread.NoteOn());
            
            return HITResult.CONTINUE;
        }

        public static HITResult NoteOff(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Sign-extend a 1-byte constant to 4 bytes and write to a variable.
        /// </summary>
        public static HITResult LoadB(HITThread thread)
        {
            var dest = thread.ReadByte();
            var value = (sbyte)thread.ReadByte();

            thread.WriteVar(dest, value);

            thread.SetFlags(value);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Write a 4-byte constant to a variable.
        /// </summary>
        public static HITResult LoadL(HITThread thread)
        {
            var dest = thread.ReadByte();
            var value = thread.ReadInt32();

            thread.WriteVar(dest, value);
            thread.SetFlags(value);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Copy the contents of one variable into another.
        /// </summary>
        public static HITResult Set(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadVar(thread.ReadByte());

            thread.WriteVar(dest, src);

            thread.SetFlags(src);

            return HITResult.CONTINUE;
        }

        public static uint PCTrans(uint id, HITThread thread)
        {
            if (Content.Content.Get().TS1)
            {
                //dunno why this is ts1 specific, but whatever
                //need to remap from the call id to the function offset

                thread.ResGroup.hot.TrackData.TryGetValue(id, out id);
            }
            return id;
        }

        /// <summary>
        /// Call a function; push the instruction pointer and jump to the given address.
        /// </summary>
        public static HITResult Call(HITThread thread)
        {
            uint targ = thread.ReadUInt32();

            thread.Stack.Push((int)thread.PC);
            thread.PC = PCTrans(targ, thread);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Kill this thread.
        /// </summary>
        public static HITResult Return(HITThread thread)
        {
            return HITResult.KILL;
        }

        /// <summary>
        /// Wait for a length of time in milliseconds, specified by a variable.
        /// </summary>
        public static HITResult Wait(HITThread thread)
        {
            var src = thread.ReadByte();
            if (thread.WaitRemain == -1) thread.WaitRemain = thread.ReadVar(src);
            thread.WaitRemain -= 16; //assuming tick rate is 60 times a second
            if (thread.WaitRemain > 0)
            {
                thread.PC -= 2;
                return HITResult.HALT;
            }
            else
            {
                thread.WaitRemain = -1;
                return HITResult.CONTINUE;
            }
        }

        public static HITResult CallEntryPoint(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Wait for the previously selected note to finish playing.
        /// </summary>
        public static HITResult WaitSamp(HITThread thread)
        {
            if (thread.NoteActive(thread.LastNote))
            {
                thread.PC--;
                return HITResult.HALT;
            }
            else
                return HITResult.HALT;
        }

        /// <summary>
        /// Return from this function; pop the instruction pointer from the stack and jump.
        /// </summary>
        public static HITResult End(HITThread thread)
        {
            if (thread.Stack.Count > 0)
            {
                thread.PC = (uint)thread.Stack.Pop();
                return HITResult.CONTINUE;
            }
            else
            {
                if (thread.Loop && thread.HasSetLoop)
                {
                    return Loop(thread);
                } else return HITResult.KILL;
            }
        }

        /// <summary>
        /// Jump to a given address.
        /// </summary>
        public static HITResult Jump(HITThread thread)
        {
            var read = thread.ReadByte();

            if (read > 15) //literal
            {
                thread.PC--; //backtraaackkk
                thread.PC = PCTrans(thread.ReadUInt32(), thread);
            }
            else //no idea if there are collisions. if there are i'm blaming fatbag. >:)
            {
                thread.PC = PCTrans((uint)thread.ReadVar(read), thread);
                if (thread.ReadByte() == 0) thread.PC += 2; //if next is no-op, the operand is 4 byte
                else thread.PC--; //operand is 1 byte (next is an instruction), backtrack
            }

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Examine a variable and set the flags.
        /// </summary>
        public static HITResult Test(HITThread thread)
        {
            var value = thread.ReadVar(thread.ReadByte());

            thread.SetFlags(value);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Increment a "dest" variable by a "src" variable.
        /// </summary>
        public static HITResult Add(HITThread thread) //0x10
        {
            var dest = thread.ReadByte();
            var src = thread.ReadByte();

            var result = thread.ReadVar(dest) + thread.ReadVar(src);
            thread.WriteVar(dest, result);

            thread.SetFlags(result);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Decrement a "dest" variable by a "src" variable.
        /// </summary>
        public static HITResult Sub(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadByte();

            var result = thread.ReadVar(dest) - thread.ReadVar(src);
            thread.WriteVar(dest, result);

            thread.SetFlags(result);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Divide a "dest" variable by a "src" variable, and round the result towards zero (truncate).
        /// </summary>
        public static HITResult Div(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadByte();

            var result = thread.ReadVar(dest) / thread.ReadVar(src);
            thread.WriteVar(dest, result);

            thread.SetFlags(result);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Multiply a "dest" variable by a "src" variable.
        /// </summary>
        public static HITResult Mul(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadByte();

            var result = thread.ReadVar(dest) * thread.ReadVar(src);
            thread.WriteVar(dest, result);

            thread.SetFlags(result);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Compare two variables and set the flags.
        /// </summary>
        public static HITResult Cmp(HITThread thread) //same as sub, but does not set afterwards.
        {
            var dest = thread.ReadByte();
            var src = thread.ReadByte();

            var result = thread.ReadVar(dest) - thread.ReadVar(src);

            thread.SetFlags(result);

            return HITResult.CONTINUE;
        }

        public static HITResult Less(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult Greater(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult Not(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Generate a random number between "low" and "high" variables, inclusive, 
        /// and store the result in the "dest" variable.
        /// </summary>
        public static HITResult Rand(HITThread thread)
        {
            var dest = thread.ReadByte();
            var low = thread.ReadByte();
            var high = thread.ReadByte();

            var result = (new Random()).Next(high+1-low)+low;
            thread.WriteVar(dest, result);

            thread.SetFlags(result);

            return HITResult.CONTINUE;
        }

        public static HITResult Abs(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult Limit(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult Error(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult Assert(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult AddToGroup(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult RemoveFromGroup(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult GetVar(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Jump back to the loop point (start of track subroutine by default).
        /// </summary>
        public static HITResult Loop(HITThread thread) //0x20
        {
            thread.PC = (uint)thread.LoopPointer;
            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Set the loop point to the current position.
        /// </summary>
        public static HITResult SetLoop(HITThread thread)
        {
            thread.LoopPointer = (int)thread.PC;
            thread.HasSetLoop = true;
            return HITResult.CONTINUE;
        }

        public static HITResult Callback(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult SmartAdd(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult SmartRemove(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult SmartRemoveAll(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult SmartSetCrit(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Set the specified variable to a random entry from the selected hitlist.
        /// </summary>
        public static HITResult SmartChoose(HITThread thread)
        {
            var dest = thread.ReadByte();
            thread.WriteVar(dest, (int)thread.HitlistChoose());
            return HITResult.CONTINUE;
        }

        public static HITResult And(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult NAnd(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult Or(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult NOr(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult XOr(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Find the higher of a "dest" variable and a "src" constant and store the result in the variable.
        /// </summary>
        public static HITResult Max(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadInt32();

            var result = Math.Max(thread.ReadVar(dest), src);
            thread.WriteVar(dest, result);

            thread.SetFlags(result);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Find the lower of a "dest" variable and a "src" constant and store the result in the variable.
        /// </summary>
        public static HITResult Min(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadInt32();

            var result = Math.Min(thread.ReadVar(dest), src);
            thread.WriteVar(dest, result);

            thread.SetFlags(result);
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult Inc(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult Dec(HITThread thread) //0x30
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult PrintReg(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Play a track, whose ID resides in the specified variable.
        /// AUD-14 (native decode, dispatch[0x32]): ONE CheckedRegId-validated
        /// register-id operand; the register's value goes through a SoundId hash
        /// lookup and starts that track, then RegisterVal(self+0x178, 3). Arity was
        /// already right (1 byte); the action side is a documented bounded subset —
        /// the SoundId-hash table is not decoded and HITVM's sound registry is not
        /// reachable from the interpreter — so the operand is consumed and validated
        /// (bytecode alignment + the native long-gate law) and the start is skipped.
        /// Corpus: 2 sites, both in simsgeneratedhitsource.hit (TS1 content).
        /// </summary>
        public static HITResult PlayTrack(HITThread thread)
        {
            var id = thread.ReadByte();
            if (IsValidRegId(id))
            {
                _ = thread.ReadVar(id); // native: SoundId-hash lookup -> track start (bounded out)
            }
            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Kill a track, whose ID resides in the specified variable.
        /// AUD-14 (native decode, dispatch[0x33]): ONE CheckedRegId-validated
        /// register-id operand; the register's value goes through the SoundId hash
        /// lookup and kills that track. Bounded subset as with play_trk (lookup
        /// table + registry not reachable): consume, validate, skip the kill.
        /// Corpus: 8 sites, simsgeneratedhitsource.hit.
        /// </summary>
        public static HITResult KillTrack(HITThread thread)
        {
            var id = thread.ReadByte();
            if (IsValidRegId(id))
            {
                _ = thread.ReadVar(id); // native: SoundId-hash lookup -> kill (bounded out)
            }
            return HITResult.CONTINUE;
        }

        public static HITResult Push(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult PushMask(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult PushVars(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult CallMask(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult CallPush(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult Pop(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// AUD-14 (native decode): test1-4 (0x3A-0x3D) take ZERO operands — the
        /// port's consumption is already bytecode-correct, only semantics are
        /// missing. test1: guarded SoundId-hash predicate whose bool lands in the
        /// context slot [r27] (vtbl+0xa8 on the result). test2: writes the 3600
        /// (0xe10) pitch-base constant via vtbl+0x4c on [r28]. test3/test4:
        /// 0x24-byte fixed-context handlers. Corpus: 1 site each; bounded no-ops
        /// pending the vtbl-slot decode.
        /// </summary>
        public static HITResult Test1(HITThread thread)
        {
            return HITResult.CONTINUE;
        }

        public static HITResult Test2(HITThread thread)
        {
            return HITResult.CONTINUE;
        }

        public static HITResult Test3(HITThread thread)
        {
            return HITResult.CONTINUE;
        }

        public static HITResult Test4(HITThread thread)
        {
            return HITResult.CONTINUE;
        }

        /// <summary>
        /// If the zero flag is set, jump to the given address.
        /// </summary>
        public static HITResult IfEqual(HITThread thread)
        {
            var loc = thread.ReadUInt32();

            if (thread.ZeroFlag) thread.PC = PCTrans(loc, thread);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// If the zero flag is not set, jump to the given address.
        /// </summary>
        public static HITResult IfNotEqual(HITThread thread)
        {
            var loc = thread.ReadUInt32();

            if (!thread.ZeroFlag) thread.PC = PCTrans(loc, thread);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// If the sign flag is not set and the zero flag is not set, jump to the given address
        /// </summary>
        public static HITResult IfGreater(HITThread thread) //0x40
        {
            var loc = thread.ReadUInt32();

            if (!thread.SignFlag && !thread.ZeroFlag) thread.PC = PCTrans(loc, thread); //last set/compare result was > 0

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// If the sign flag is set, jump to the given address.
        /// </summary>
        public static HITResult IfLess(HITThread thread)
        {
            var loc = thread.ReadUInt32();

            if (thread.SignFlag) thread.PC = PCTrans(loc, thread); //last set/compare result was < 0

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// If the sign flag is not set, jump to the given address.
        /// </summary>
        public static HITResult IfGreatOrEq(HITThread thread)
        {
            var loc = thread.ReadUInt32();

            if (!thread.SignFlag) thread.PC = PCTrans(loc, thread); //last set/compare result was >= 0

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// If the sign flag is set or the zero flag is set, jump to the given address.
        /// </summary>
        public static HITResult IfLessOrEq(HITThread thread)
        {
            var loc = thread.ReadUInt32();

            if (thread.SignFlag || thread.ZeroFlag) thread.PC = PCTrans(loc, thread); //last set/compare result was <= 0

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Choose a global hitlist, or 0 for the one local to the track (source: defaultsyms.txt).
        /// </summary>
        public static HITResult SmartSetList(HITThread thread)
        { //sets the hitlist
            var src = thread.ReadByte();
            thread.LoadHitlist((uint)thread.ReadVar(src));

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Kill an actor's vocals, given a constant ID.
        /// </summary>
        public static HITResult SeqGroupKill(HITThread thread)
        {
            var src = thread.ReadByte();

            if (src == (byte)HITPerson.Instance)
                thread.KillVocals();
            else
            {
                //TODO: Implement system for keeping track of which object created a thread
                //      and kill that thread's sounds (src == ObjectID).
            }

            return HITResult.CONTINUE;
        }

        public static HITResult SeqGroupWait(HITThread thread) //unused in the sims
        {
            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Kill a sequence group with the return value specified by a constant.
        /// AUD-14 (native decode, dispatch[0x47]): ONE CheckedRegId-validated
        /// register-id operand; the register's value drives the seqgroup return
        /// logic (likely the write side of the waiteq sync vars). Arity already
        /// correct; the return plumbing is not decoded — bounded subset: consume,
        /// validate, skip. Corpus: 8 sites, simsgeneratedhitsource.hit.
        /// </summary>
        public static HITResult SeqGroupReturn(HITThread thread)
        {
            var id = thread.ReadByte();
            if (IsValidRegId(id))
            {
                _ = thread.ReadVar(id); // native: seqgroup return on the value (bounded out)
            }
            return HITResult.CONTINUE;
        }

        public static HITResult GetSrcDataField(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadByte();
            var field = thread.ReadByte();

            int ObjectVar = thread.ReadVar(src); //always 12?
            int FieldVar = thread.ReadVar(field);

            ObjectVar = thread.ReadVar(10010 + FieldVar);
            thread.WriteVar(dest, ObjectVar);
            thread.SetFlags(ObjectVar);

            return HITResult.CONTINUE;
        }

        public static HITResult SeqGroupTrackID(HITThread thread)
        {
            var dest = thread.ReadByte(); //uhhhh
            var src = thread.ReadByte();
            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Copy the contents of one variable into another (equivalent to set and settt; 
        /// defaultsyms.txt says "ISN'T THIS THE SAME AS SET TOO?")
        /// </summary>
        public static HITResult SetLL(HITThread thread)
        {
            Set(thread);
            return HITResult.CONTINUE;
        }

        public static HITResult SetLT(HITThread thread)
        {
            // AUD-14 (native decode, dispatch[0x4B]): TWO CheckedRegId-validated
            // register-id operands — consumption below already matches. Native
            // performs a single store keyed by operand 1:
            // vtbl+0x8c(self, id1, vtbl+0xa0(self), 0); operand 2 is validated but
            // its VALUE is never read (AUD-08's ">127 long-variable routing" reading
            // is superseded — that was CheckedRegId's rejection path). The stored
            // value's source (vtbl+0xa0 getter on self) is not decoded — bounded
            // subset: keep the 2-byte consumption, skip the store. Corpus: 1 site.
            var dest = thread.ReadByte();
            var src = thread.ReadByte();

            return HITResult.CONTINUE;
        }

        public static HITResult SetTL(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadByte();

            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Wait until two variables are equal.
        /// AUD-14 (native decode, dispatch[0x4D]): TWO CheckedRegId-validated
        /// register-id operands, DEST then SRC; compare ReadVar(dest) vs
        /// ReadVar(src) and retry while unequal — the native retry is cursor −3 +
        /// halt-until-equal (0x310A0C: cmpw r28,r3 / beq tail), matching the port's
        /// PC -= 3 HALT shape. AUD-09's one-line fix re-landed: the historic
        /// self-compare (ReadVar(dest) != ReadVar(dest)) was always false, so the
        /// sync wait NEVER fired. A rejected id (0 or >0x7f) skips the wait — the
        /// native CheckedRegId rejection branches to the CONTINUE tail with nothing
        /// read or compared.
        /// </summary>
        public static HITResult WaitEqual(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadByte();

            if (IsValidRegId(dest) && IsValidRegId(src)
                && thread.ReadVar(dest) != thread.ReadVar(src))
            {
                thread.PC -= 3;
                return HITResult.HALT;
            }
            else
                return HITResult.CONTINUE;
        }

        public static HITResult WaitNotEqual(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult WaitGreater(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult WaitLess(HITThread thread) //0x50
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult WaitGreatOrEq(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult WaitLessOrEq(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Ducks all audio with priority lower than this. 
        /// Imagine all the other sounds getting quieter when the fire music plays.
        /// </summary>
        public static HITResult Duck(HITThread thread)
        {
            thread.Duck();
            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Unducks all audio back to the volume before Duck() was called.
        /// </summary>
        public static HITResult Unduck(HITThread thread)
        {
            thread.Unduck();
            return HITResult.CONTINUE; //quack
        }

        public static HITResult TestX(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Set global = local (source: defaultsyms.txt).
        /// </summary>
        public static HITResult SetLG(HITThread thread)
        {
            var local = thread.ReadByte();
            var global = thread.ReadInt32();

            thread.VM.WriteGlobal(global, local);

            return HITResult.CONTINUE;
        }

        /// <summary>
        /// Read globally, set locally (source: defaultsyms.txt).
        /// </summary>
        public static HITResult SetGL(HITThread thread)
        {
            var dest = thread.ReadByte();
            var src = thread.ReadInt32();

            int Global = thread.VM.ReadGlobal(src);
            thread.WriteVar(dest, Global);

            return HITResult.CONTINUE;
        }

        public static HITResult Throw(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult SetSrcDataField(HITThread thread)
        {
            var value = thread.ReadByte();
            var src = thread.ReadByte();
            var field = thread.ReadByte();

            //TODO: System for keeping track of which objects correspond to ObjectID.

            return HITResult.CONTINUE; //you can set these??? what
        }

        /// <summary>
        /// Stop playing a track, whose ID resides in the specified variable.
        /// </summary>
        public static HITResult StopTrack(HITThread thread)
        {
            var src = thread.ReadByte();
            return HITResult.CONTINUE;
        }

        public static HITResult SetChanReg(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult PlayNote(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult StopNote(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        public static HITResult KillNote(HITThread thread)
        {
            return HITResult.CONTINUE; //unused in the sims
        }

        /// <summary>
        /// Load a track ("index" variable) from a hitlist ("table" variable).
        /// </summary>
        public static HITResult SmartIndex(HITThread thread)
        {
            var destVar = thread.ReadByte();
            //var dest = thread.ReadVar(thread.ReadByte());
            var index = thread.ReadVar(thread.ReadByte());

            //thread.LoadHitlist((byte)dest);
            int TrackID = (int)thread.LoadTrack(index);

            thread.WriteVar(destVar, TrackID);

            return HITResult.CONTINUE; //Appears to be unused.
        }

        public static HITResult NoteOnLoop(HITThread thread) //0x60
        {
            var dest = thread.ReadByte();
            thread.WriteVar(dest, thread.NoteLoop());
            return HITResult.HALT;
        }
    }
    public delegate HITResult HITInstruction(HITThread thread);

    public enum HITResult
    {
        CONTINUE,
        HALT,
        KILL
    }
}
