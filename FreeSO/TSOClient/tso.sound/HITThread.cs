using System;
using System.Collections.Generic;
using System.Diagnostics;
using FSO.Files.HIT;
using Microsoft.Xna.Framework.Audio;
using FSO.Content.Interfaces;
using FSO.Content.Model;
using FSO.Common;

namespace FSO.HIT
{
    public class HITThread : HITSound
    {
        public uint PC; //program counter
        public HITFile Src;
        public HITResourceGroup ResGroup;
        private Hitlist Hitlist;
        private int[] Registers; //includes args, vars, whatever "h" is up to 0xf
        private int[] LocalVar; //the sims online set, 0x10 "argstyle" up to 0x45 orientz. are half of these even used? no. but even in the test files? no
        public int[] ObjectVar; //IsInsideViewFrustrum 0x271a to Neatness 0x2736. Set by object on thread invocation.

        private Track ActiveTrack;
        public int LoopPointer = -1;
        public int WaitRemain = -1;

        public bool SimpleMode; //certain sounds play with no HIT.
        public bool Loop;
        private bool PlaySimple;

        private Patch Patch; //sound id
        public bool HasSetLoop;
        public bool LoopDefined;
        public bool ThreadDead;

        public bool Interruptable
        {
            get { return LocalVar != null && (LocalVar[0x21] > 0 || LocalVar[0x27] > 0); }
        }
        public bool Interrupted;
        public HITThread InterruptWaiter;
        public HITThread InterruptBlocker;

        // TYPE53-FC1: the kSequenceTrackHitList (53) payload hitlist of the event
        // that started this thread (HITVM.PlaySoundEvent type-53 arm). When the
        // thread completes, HITVM.Tick chains one continuation that plays the
        // chosen patch from this list (audit §2 native law: "play the track and
        // sequence the payload hitlist"). 0 = not a type-53 thread.
        public uint SequenceHitlist;
        public bool SequenceChained;

        private List<HITNoteEntry> Notes;
        private Dictionary<SoundEffectInstance, HITNoteEntry> NotesByChannel;
        public int LastNote
        {
            get { return Notes.Count - 1; }
        }

        public HITDuckingPriorities DuckPriority
        {
            get
            {
                if (ActiveTrack != null)
                    return ActiveTrack.DuckingPriority;
                else
                    return HITDuckingPriorities.duckpri_normal;
            }
        }

        /// <summary>
        /// AUD-21: the thread's track kSpl ([Track] column 7 — the attenuation
        /// the native reads from the per-track sound object's register 0x39).
        /// Synthetic/fallback tracks carry the Track class default 0x14.
        /// </summary>
        public override uint kSpl
        {
            get { return (ActiveTrack != null) ? ActiveTrack.kSpl : 0x14; }
        }

        public bool ZeroFlag; //flags set by instructions
        public bool SignFlag;
        public int TickN;
        public bool Paused;

        public Stack<int> Stack;

        private IAudioProvider audContent;

        public void Interrupt(HITThread waiter)
        {
            Interrupted = true;
            InterruptWaiter = waiter;
            waiter.InterruptBlocker = this;
            for (int i=0; i<Notes.Count; i++)
            {
                var note = Notes[i];
                if (note.EndTick == -1)
                {
                    var tickDuration = (note.Duration * FSOEnvironment.RefreshRate);
                    note.EndTick = note.StartTick + (int)(Math.Ceiling((TickN - note.StartTick) / tickDuration) * tickDuration);
                    Notes[i] = note;
                }
            }
        }

        public void Unblock()
        {
            InterruptBlocker = null;
        }

        public override void Dispose()
        {
            InterruptWaiter?.Unblock();
            VM?.DuckRemove(this); // AUD-06: kill/dispose deregisters the duck registry
            IsDisposed = true;
            foreach (var note in Notes) note.instance.Dispose();
        }

        public override bool Tick() //true if continue, false if kill
        {
            if (IsDisposed) return false; // reap externally disposed threads

            if (Paused) return true;
            TickN++;
            if (InterruptBlocker != null) return !Dead;
            if (EverHadOwners && Owners.Count == 0)
            {
                KillVocals();
                Dead = true;
                return false;
            }

            if (VolumeSet)
            {
                for (int i = 0; i < Notes.Count; i++)
                {
                    var note = Notes[i];
                    var inst = note.instance;
                    if (note.EndTick != -1 && TickN > note.EndTick) inst.Stop();
                    if (!note.started && inst.State == SoundState.Stopped)
                    {
                        if (!inst.IsDisposed) inst.Dispose();
                        continue;
                    }
                    if (Emitter3D == null) inst.Pan = Pan;
                    else Apply3D(inst);
                    inst.Volume = Math.Min(1.0f, Volume);
                }
            }

            VolumeSet = false;

            if (SimpleMode)
            {
                if (PlaySimple)
                {
                    NoteOn();
                    PlaySimple = false;
                }
                if (NoteActive(LastNote)) return true;
                else
                {
                    Dead = true;
                    return false;
                }
            }
            else
            {
                if (ThreadDead)
                {
                    if (NoteActive(LastNote)) return true;
                    else
                    {
                        Dead = true;
                        return false;
                    }
                }
                else
                {
                    try
                    {
                        while (true)
                        {
                            var opcode = Src.Data[PC++];
                            if (opcode >= HITInterpreter.Instructions.Length) opcode = 0;
                            var result = HITInterpreter.Instructions[opcode](this);
                            if (result == HITResult.HALT) return true;
                            else if (result == HITResult.KILL)
                            {
                                ThreadDead = true;
                                return true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// Kills all playing sounds.
        /// </summary>
        public void KillVocals()
        {
            for (int i = 0; i < Notes.Count; i++)
            {
                if (NoteActive(i))
                {
                    Notes[i].instance.Stop();
                    Notes[i].instance.Dispose();
                }
            }
        }

        public HITThread(HITResourceGroup Src, HITVM VM)
        {
            this.ResGroup = Src;
            this.Src = Src.hit;
            this.VM = VM;
            Registers = new int[16];
            Registers[1] = 12; //gender (offset into object var table)
            LocalVar = new int[54];
            // AUD-05/AUD-04 pitch law: reg 21 is the integer pitch, native default
            // 3600 = neutral (rate 22050). The shipped corpus never writes pitch
            // (AUD-04), so defaulting to 3600 keeps all unpitched tracks neutral
            // instead of the port's implicit 0 (which would clamp to -1 octave).
            LocalVar[21 - 0x10] = 3600; //pitch register (reg 21 -> LocalVar[5])
            ObjectVar = new int[29];

            Notes = new List<HITNoteEntry>();
            NotesByChannel = new Dictionary<SoundEffectInstance, HITNoteEntry>();
            Owners = new List<int>();

            Stack = new Stack<int>();
            audContent = Content.Content.Get().Audio;
        }

        public HITThread(uint TrackID, HITVM VM, HITResourceGroup Src)
        {
            this.VM = VM;
            ResGroup = Src;
            // AUD-05: this construction path (one-shot "simple mode" thread) must
            // initialise the same register/var arrays as the primary constructor, or
            // NoteOn()'s GetPitch() -> ReadVar(21) -> LocalVar[5] NREs on null (seen
            // as a startup NullReferenceException in the integrated build). Mirror the
            // primary constructor's init + reg-21 pitch default (3600 = neutral).
            Registers = new int[16];
            Registers[1] = 12; //gender (offset into object var table)
            LocalVar = new int[54];
            LocalVar[21 - 0x10] = 3600; //pitch register (reg 21 -> LocalVar[5])
            ObjectVar = new int[29];
            Stack = new Stack<int>();

            Owners = new List<int>();
            Notes = new List<HITNoteEntry>();
            NotesByChannel = new Dictionary<SoundEffectInstance, HITNoteEntry>();

            audContent = Content.Content.Get().Audio;
            SetTrack(TrackID);

            SimpleMode = true;
            PlaySimple = true; //play next frame, so we have time to set volumes.
        }

        /// <summary>
        /// TYPE53-FC1: the continuation chained when a kSequenceTrackHitList (53)
        /// event thread completes. Plays one patch chosen from the event's payload
        /// sequence hitlist (one-shot, no HIT code — the payload lists are the
        /// section-sample selectors, e.g. sep6snds.hot's camerock_* sections).
        /// Native law (TYPE53 audit §2, mechanism level): "play the track and
        /// sequence the payload hitlist"; the card's sketch chains LoadHitlist(seq)
        /// on track completion. Returns null if the hitlist yields no patch
        /// (unmounted list), in which case nothing is chained.
        /// </summary>
        public static HITThread SequenceContinuation(HITThread finished, HITVM vm)
        {
            var thread = new HITThread(finished.ResGroup, vm);
            thread.LoadHitlist(finished.SequenceHitlist);
            var pick = thread.HitlistChoose();
            if (pick == 0) return null;
            // hitlist entries are patch IDs here; SetTrack's fallback resolves
            // unknown track IDs as patches (same convention as LoadTrack users).
            thread.SetTrack(pick);
            thread.SimpleMode = true;
            thread.PlaySimple = true; //play next frame, so we have time to set volumes.
            thread.Name = finished.Name + "_seq";
            return thread;
        }

        public void LoadHitlist(uint id)
        {
            Hitlist = audContent.GetHitlist(id, ResGroup);
        }

        public uint HitlistChoose() //returns a random id from the hitlist
        {
            Random rand = new Random();
            if (Hitlist != null) return Hitlist.IDs[rand.Next(Hitlist.IDs.Count)];
            else return 0;
        }

        public byte ReadByte()
        {
            return Src.Data[PC++];
        }

        public uint ReadUInt32()
        {
            uint result = 0;
            result |= ReadByte();
            result |= ((uint)ReadByte() << 8);
            result |= ((uint)ReadByte() << 16);
            result |= ((uint)ReadByte() << 24);
            return result;
        }

        public int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        public void SetTrack(uint value)
        {
            SetTrack(value, 0);
        }

        public void SetTrack(uint value, uint fallback)
        {
            ActiveTrack = audContent.GetTrack(value, fallback, ResGroup);
            if (ActiveTrack != null)
            {
                if (ActiveTrack.HitlistID != 0)
                {
                    LoadHitlist(ActiveTrack.HitlistID);
                    if (ActiveTrack.SoundID == 0) ActiveTrack.SoundID = HitlistChoose();
                }
                Patch = audContent.GetPatch(ActiveTrack.SoundID, ResGroup);
            } else
            {
                //make it up?
                ActiveTrack = new Track() { SoundID = value, TrackID = value };
                Patch = audContent.GetPatch(value, ResGroup);
            }
            if (ActiveTrack.LoopDefined)
            {
                Loop = ActiveTrack.Looped != 0;
                HasSetLoop = Loop;
                LoopDefined = true;
            }
            DuckPri = (int)DuckPriority; // AUD-06: suffered duckpri (reg 25) from the [Track] kDuckPri
        }

        /// <summary>
        /// Loads a track from the current HitList.
        /// </summary>
        /// <param name="value">ID of track to load.</param>
        public uint LoadTrack(int value)
        {
            SetTrack(Hitlist.IDs[value]);
            return Hitlist.IDs[value];
        }

        /// <summary>
        /// AUD-05/AUD-04 native pitch law. Reg 21 = integer pitch (3600 neutral,
        /// 100 per semitone, rate = 22050 · 2^((pitch−3600)/1200)). MonoGame's
        /// SoundEffectInstance.Pitch is a ratio exponent from −1 (half rate) to
        /// +1 (double rate): Pitch = (pitch−3600)/1200, clamped to [−1,1].
        /// </summary>
        public float GetPitch()
        {
            float pitch = (ReadVar(21) - 3600) / 1200f;
            if (pitch < -1f) pitch = -1f;
            else if (pitch > 1f) pitch = 1f;
            return pitch;
        }

        /// <summary>
        /// Plays the current patch.
        /// </summary>
        /// <returns>-1 if unsuccessful, or the id of the note played.</returns>
        public int NoteOn()
        {
            var sound = audContent.GetSFX(Patch);

            if (sound != null)
            {
                ResolveVolumeGroup(sound);
                RecalculateVolume();

                var instance = sound.CreateInstance();
                instance.Volume = Volume;
                instance.Pitch = GetPitch(); //AUD-05: native reg-21 pitch law
                if (Emitter3D == null) instance.Pan = Pan;
                else Apply3D(instance);
                //instance.Play();

                var entry = new HITNoteEntry(sound, instance, Patch, TickN);
                entry.Source = this; // AUD-13: marker-stamp attribution (NoteQueued fires at PLAY time; handle identity breaks across retire-and-refire)
                VM.QueuePlay(entry);
                Notes.Add(entry);
                NotesByChannel.Add(instance, entry);
                // AUD-19: as-played note trace (volume is post group-master + duck).
                if (HITTrace.Enabled) HITTrace.Note(HITTrace.KIND_NOTE, Name,
                    Patch?.Name, Patch != null ? Patch.FileID : 0,
                    ActiveTrack != null ? ActiveTrack.TrackID : 0,
                    instance.Volume, InstVolume, Pan, instance.Pitch, (byte)VolGroup, LastMainOwner);
                return Notes.Count - 1;
            }
            else
            {
                Debug.WriteLine("HITThread: Couldn't find sound");
            }

            return -1;
        }

        /// <summary>
        /// Plays the current patch and loops it indefinitely.
        /// </summary>
        /// <returns>-1 if unsuccessful, or the id of the note played.</returns>
        public int NoteLoop() //todo, make loop again.
        {
            var sound = audContent.GetSFX(Patch);

            if (sound != null)
            {
                ResolveVolumeGroup(sound);
                RecalculateVolume();

                var instance = sound.CreateInstance();
                instance.Volume = Volume;
                instance.Pitch = GetPitch(); //AUD-05: native reg-21 pitch law
                if (Emitter3D == null) instance.Pan = Pan;
                else Apply3D(instance);
                instance.IsLooped = true;
                //instance.Play();

                var entry = new HITNoteEntry(sound, instance, Patch, TickN);
                entry.Source = this; // AUD-13: marker-stamp attribution (NoteQueued fires at PLAY time; handle identity breaks across retire-and-refire)
                VM.QueuePlay(entry);
                Notes.Add(entry);
                NotesByChannel.Add(instance, entry);
                // AUD-19: as-played note trace (volume is post group-master + duck).
                if (HITTrace.Enabled) HITTrace.Note(HITTrace.KIND_NOTE_LOOP, Name,
                    Patch?.Name, Patch != null ? Patch.FileID : 0,
                    ActiveTrack != null ? ActiveTrack.TrackID : 0,
                    instance.Volume, InstVolume, Pan, instance.Pitch, (byte)VolGroup, LastMainOwner);
                return Notes.Count - 1;
            }
            else
            {
                Debug.WriteLine("HITThread: Couldn't find sound");
            }
            return -1;
        }

        /// <summary>
        /// AUD-03 (D1): the original routes a note by the track's DECLARED control
        /// group (SimsSound.hot [Track] column 6, kGroupSFX=1/kGroupMusic=2/
        /// kGroupVox=3 — verified in the original binary at cTrackPlayer::
        /// UpdateVolPan 0x30F900), not by the sample content sniff. Tracks without
        /// a declared group (0/undefined) keep the historic content-sniff routing
        /// so TSO content is unaffected.
        /// </summary>
        private void ResolveVolumeGroup(SoundEffect sound)
        {
            switch (ActiveTrack?.ControlGroup ?? 0)
            {
                case HITControlGroups.kGroupVox:
                    VolGroup = Model.HITVolumeGroup.VOX; break;
                case HITControlGroups.kGroupMusic:
                    VolGroup = Model.HITVolumeGroup.MUSIC; break;
                case HITControlGroups.kGroupSFX:
                    VolGroup = Model.HITVolumeGroup.FX; break;
                default:
                    switch (sound.Name)
                    {
                        case "FX":
                            VolGroup = Model.HITVolumeGroup.FX; break;
                        case "MUSIC":
                            VolGroup = Model.HITVolumeGroup.MUSIC; break;
                        case "VOX":
                            VolGroup = Model.HITVolumeGroup.VOX; break;
                    }
                    break;
            }
        }

        /// <summary>
        /// Is a note active?
        /// </summary>
        /// <param name="note">The note to check.</param>
        /// <returns>True if active, false if not.</returns>
        public bool NoteActive(int note)
        {
            if (note == -1 || note >= Notes.Count) return false;
            return !Notes[note].started || (Notes[note].instance.State != SoundState.Stopped);
        }

        /// <summary>
        /// Signals the VM to duck all threads with a higher ducking priority than this one.
        /// </summary>
        public void Duck()
        {
            //VM.Duck(this.DuckPriority);
        }

        /// <summary>
        /// Signals to the VM to unduck all threads that are currently ducked.
        /// </summary>
        public void Unduck()
        {
            //VM.Unduck();
        }

        private void LocalVarSet(int location, int value)
        {
            switch (location)
            {
                case 0x12: //patch, switch active track
                case 50:
                    var replacePatch = audContent.GetPatch((uint)value, ResGroup);
                    if (replacePatch != null) Patch = replacePatch; //river loop seems to try to load an invalid patch?
                    break;
            }
        }

        public void SetFlags(int value)
        {
            ZeroFlag = (value == 0);
            SignFlag = (value < 0);
        }

        public void WriteVar(int location, int value)
        {
            if (location < 0x10)
            {
                Registers[location] = value;
            }
            else if (location < 0x46)
            {
                if (location == 0x19) DuckPri = value; // AUD-06: duckpri (reg 25) VM write
                LocalVarSet(location, value); //invoke any special behaviours, like track switch for setting patch
                LocalVar[location - 0x10] = value;
            }
            else if (location < 0x64)
            {
                return; //not mapped
            }
            else if (location < 0x88)
            {
                if (location == 0x7b)
                {
                    // AUD-06: `set main_duckpri, X` — announce X (X!=0) or
                    // deregister (X==0); republish max + respray all live volumes.
                    MainDuckPri = value;
                    VM?.DuckAnnounce(this, value);
                }
                else
                {
                    VM.WriteGlobal(location - 0x64, value);
                }
            }
            else if (location < 0x271a)
            {
                return; //not mapped
            }
            else if (location < 0x2737)
            {
                ObjectVar[location - 0x271a] = value; //this probably should not be valid... but if it is used it may require some reworking to get this to sync across object threads.
            }
        }

        public int ReadVar(int location)
        {
            if (location < 0x10)
            {
                return Registers[location];
            } 
            else if (location < 0x46) 
            {
                // AUD-07 integration hardening: a thread can reach NoteOn (which now
                // reads reg 21 for the pitch law) before its track allocated LocalVar;
                // report 0 like the other unmapped ranges instead of NRE-ing.
                if (LocalVar == null) return 0;
                return LocalVar[location - 0x10];
            }
            else if (location < 0x64)
            {
                return 0; //not mapped
            }
            else if (location < 0x88)
            {
                return VM.ReadGlobal(location - 0x64);
            }
            else if (location < 0x271a)
            {
                return 0; //not mapped
            }
            else if (location < 0x2737)
            {
                var loc = location - 0x271a;
                return ObjectVar[loc];
            }
            return 0;
        }

        public void JumpToEntryPoint(int TrackID)
        {
            PC = (uint)Src.EntryPointByTrackID[(uint)TrackID];
        }

        public override void Pause()
        {
            Paused = true;
            foreach (var note in Notes)
            {
                if (note.instance.State != SoundState.Stopped) note.instance.Pause();
            }
        }

        public override void Resume()
        {
            Paused = false;
            foreach (var note in Notes)
            {
                if (note.instance.State != SoundState.Stopped) note.instance.Resume();
            }
        }
    }

    public class HITNoteEntry 
    {
        public HITThread Source; // AUD-13: the thread that queued this note (marker-stamp attribution)
        public SoundEffectInstance instance;
        public Patch Sound; //This is for killing specific sounds, see HITInterpreter.SeqGroupKill.
        public bool started;
        public int StartTick;
        public int EndTick;
        public float Duration;

        public HITNoteEntry(SoundEffect sfx, SoundEffectInstance instance, Patch sound, int startTick)
        {
            this.instance = instance;
            this.Sound = sound;
            this.started = false;
            this.EndTick = -1;
            this.Duration = (float)sfx.Duration.TotalSeconds;
            this.StartTick = startTick;
        }
    }
}
