using FSO.HIT.Model;
using System;
using System.Collections.Generic;

namespace FSO.HIT
{
    public abstract class HITSound : IDisposable
    {
        protected bool VolumeSet;
        protected float Volume = 1;
        protected float InstVolume = 1;
        protected float Pan;
        protected Microsoft.Xna.Framework.Audio.AudioEmitter Emitter3D;

        protected bool EverHadOwners; //if we never had owners, don't kill the thread. (ui sounds)
        public int LastMainOwner = -1;
        protected List<int> Owners;

        public bool Dead;
        public HITVM VM;
        public HITVolumeGroup VolGroup;
        public string Name;

        // AUD-06 native ducking law. A sound's suffered duck priority (reg 25
        // `duckpri`) and its announced main_duckpri (reg 123, 0 = none). The
        // law: a sound is attenuated to 50% iff it announced a non-zero
        // main_duckpri AND that equals its own duckpri (equality, not <).
        public int DuckPri;            // suffered duck priority (reg 25 equivalent)
        public int MainDuckPri;        // announced main_duckpri (reg 123), 0 = none

        public HITSound()
        {
            Owners = new List<int>();
        }

        // AUD-07 integration hardening (coordinator): externally disposed sounds
        // (probe/teardown paths that dispose without going through HITVM.Tick)
        // must be reaped, not ticked. Set by Dispose implementations.
        public bool IsDisposed;

        public abstract bool Tick();

        public bool SetVolume(float volume, float pan, int ownerID)
        {
            bool ownerChange = false;
            if (VolumeSet)
            {
                if (volume > InstVolume)
                {
                    if (LastMainOwner != ownerID) { LastMainOwner = ownerID; ownerChange = true; }
                    InstVolume = volume;
                    RecalculateVolume();
                    Pan = pan;
                    return true;
                }
                return false;
            }
            else
            {
                if (LastMainOwner != ownerID) { LastMainOwner = ownerID; ownerChange = true; }
                InstVolume = volume;
                RecalculateVolume();
                Pan = pan;
                return true;
            }
        }

        public void Mute()
        {
            InstVolume = 0;
            Volume = 0;
            VolumeSet = true;
        }
        
        public void Set3D(Microsoft.Xna.Framework.Vector3 Position)
        {
            Microsoft.Xna.Framework.Audio.SoundEffect.DistanceScale = 100f;
            if (Emitter3D == null) Emitter3D = new Microsoft.Xna.Framework.Audio.AudioEmitter();
            Emitter3D.Position = Position;
        }

        public void Apply3D(Microsoft.Xna.Framework.Audio.SoundEffectInstance inst)
        {
            Emitter3D.Forward = VM.Listener.Forward;
            inst.Volume = 1f;
            inst.Apply3D(VM.Listener, Emitter3D);
        }

        public void RecalculateVolume()
        {
            VolumeSet = true;
            Volume = InstVolume * GetVolFactor() * GetDuckFactor();
        }

        public float GetVolFactor()
        {
            return VM?.GetMasterVolume(VolGroup) ?? 1f;
        }

        /// <summary>
        /// AUD-06 native ducking law: 0.5 iff this sound announced a non-zero
        /// main_duckpri (reg 123) that equals its own suffered duckpri (reg 25).
        /// Equality, not less-than (corrects a would-be '<' misread).
        /// </summary>
        public float GetDuckFactor()
        {
            if (MainDuckPri != 0 && MainDuckPri == DuckPri) return 0.5f;
            return 1.0f;
        }

        public void AddOwner(int id)
        {
            EverHadOwners = true;
            Owners.Add(id);
        }

        public void RemoveOwner(int id)
        {
            Owners.Remove(id);
        }

        public bool AlreadyOwns(int id)
        {
            return Owners.Contains(id);
        }

        public float GetVolume()
        {
            return Volume;
        }

        public abstract void Pause();

        public abstract void Resume();
        public abstract void Dispose();
    }
}
