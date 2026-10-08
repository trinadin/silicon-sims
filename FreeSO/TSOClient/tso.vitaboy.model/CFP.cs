using FSO.Files.Utils;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FSO.Vitaboy
{
    /// <summary>
    /// Compressed Floating Point
    /// 
    /// Used to contain the animation and translation vectors for animations
    /// separately from their "head" information. This makes it easier for the
    /// TS1 content system to scan them without loading all of the animation data.
    /// </summary>
    public class CFP
    {
        public static float[] Delta = Enumerable.Range(0, 0xFD).Select(x => (float)(3.9676e-10 * (Math.Pow((double)x - 126, 3) * Math.Abs(x - 126)))).ToArray();

        public byte[] Data;

        public void Read(Stream stream)
        {
            using (var dest = new MemoryStream())
            {
                stream.CopyTo(dest);
                Data = dest.ToArray();
            }
        }

        public byte FindBestDelta(float diff)
        {
            byte closestDelta = 0;
            float closestDeltaDiff = float.PositiveInfinity;
            for (int i=0; i<256; i++)
            {
                var dd = Math.Abs(Delta[i] - diff);
                if (dd < closestDeltaDiff)
                {
                    closestDeltaDiff = dd;
                    closestDelta = (byte)i;
                }
            }
            return closestDelta;
        }

        /// <summary>
        /// Inject this CFP's translation and rotation data into the target animation. 
        /// It should contain a reference for how many translations and rotations it expects.
        /// </summary>
        /// <param name="anim">The animation to enrich.</param>
        public void EnrichAnim(Animation anim)
        {
            using (var stream = new MemoryStream(Data))
            {
                using (var io = IoBuffer.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
                {

                    anim.Translations = new Vector3[anim.TranslationCount];
                    anim.Rotations = new Quaternion[anim.RotationCount];
                    //read the floating point values and give them to the anim.
                    //the anim supplies us with the count for the translation and rotation vector arrays.
                    ReadNFloats(io, anim.Translations.Count(), (i, x) => anim.Translations[i].X = -x);
                    ReadNFloats(io, anim.Translations.Count(), (i, y) => anim.Translations[i].Y = y);
                    ReadNFloats(io, anim.Translations.Count(), (i, z) => anim.Translations[i].Z = z);

                    ReadNFloats(io, anim.Rotations.Count(), (i, x) => anim.Rotations[i].X = x);
                    ReadNFloats(io, anim.Rotations.Count(), (i, y) => anim.Rotations[i].Y = -y);
                    ReadNFloats(io, anim.Rotations.Count(), (i, z) => anim.Rotations[i].Z = -z);
                    ReadNFloats(io, anim.Rotations.Count(), (i, w) => anim.Rotations[i].W = -w);
                }
            }
            //Data = null;
        }

        /// <summary>
        /// Builds data for the given animation.
        /// </summary>
        /// <param name="anim"></param>
        public void CompressAnim(Animation anim)
        {
            using (var stream = new MemoryStream())
            {
                using (var io = IoWriter.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
                {
                    var values = new List<float>();
                    values.AddRange(anim.Translations.Select(trans => -trans.X));
                    values.AddRange(anim.Translations.Select(trans => trans.Y));
                    values.AddRange(anim.Translations.Select(trans => trans.Z));

                    values.AddRange(anim.Rotations.Select(trans => trans.X));
                    values.AddRange(anim.Rotations.Select(trans => -trans.Y));
                    values.AddRange(anim.Rotations.Select(trans => -trans.Z));
                    values.AddRange(anim.Rotations.Select(trans => -trans.W));

                    float lastValue = 0;
                    int repeatCount = 0;
                    foreach (var value in values)
                    {
                        var diff = value - lastValue;
                        var bestMatch = FindBestDelta(diff);
                        var delta = Delta[bestMatch];
                        var error = Math.Abs(diff - delta);

                        if (bestMatch == 126 && repeatCount != 65535)
                        {
                            repeatCount++;
                        }
                        else
                        {
                            if (repeatCount > 0)
                            {
                                io.WriteByte(0xFE);
                                io.WriteUInt16((ushort)repeatCount);
                            }

                            if (error > 0.0032126708614f / 2)
                            {
                                //encode as literal float
                                io.WriteByte(0xFF);
                                io.WriteFloat(value);
                                lastValue = value;
                            }
                            else
                            {
                                //encode using delta
                                io.WriteByte(bestMatch);
                                lastValue += delta;
                            }
                        }
                    }
                    if (repeatCount > 0)
                    {
                        io.WriteByte(0xFE);
                        io.WriteUInt16((ushort)repeatCount);
                    }
                }
                Data = stream.ToArray();
            }
        }

        public static void ReadNFloats(IoBuffer io, int floats, Action<int, float> output)
        {
            // CC-06: restructured to mirror the native decoder 1:1
            // (ReadCompressedFloats @0x10364450). Native keeps a pending-repeat
            // counter consumed one slot per iteration, bounded by the outer
            // value-count loops — so an overshooting 0xFE block is naturally
            // CLAMPED (surplus repeats are discarded, never written past the
            // destination), and a code above the 0xFD-entry delta table
            // (InitFloatCompression @0x10364330 fills exactly 0xFD entries,
            // 0x00-0xFC) reads the zeroed static memory just past the table
            // end — effectively delta 0 — instead of faulting. A 0xFE count of
            // N still covers N+1 slots total (current slot + N reuses), exactly
            // as before for well-formed data.
            float lastValue = 0;
            int pendingRepeats = 0;
            for (int i = 0; i < floats; i++)
            {
                if (pendingRepeats < 1)
                {
                    var code = io.ReadByte();
                    switch (code)
                    {
                        case 0xFF:
                            lastValue = io.ReadFloat();
                            break;
                        case 0xFE:
                            //repeat count
                            pendingRepeats = io.ReadUInt16();
                            break;
                        default:
                            if (code <= 0xFC) lastValue += Delta[code];
                            break;
                    }
                }
                else
                {
                    pendingRepeats--;
                }

                output(i, lastValue);
            }
        }
    }
}
