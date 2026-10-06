using FSO.SimAntics.Model;
using FSO.Content;
using System.IO;

namespace FSO.SimAntics.Entities
{
    public class VMTS1MotiveDecay : VMIMotiveDecay
    {
        // ============================================================================
        // R251 mood law (tools/iff-dump/r251-mood-cadence/implementation.md):
        // mood = Sum(w_i * m_i + w_room * room) / Sum(w_i + w_room), summed over the
        // 8 mood participants in the native CalcHappy order (Hunger, Energy, Comfort,
        // Fun, Hygiene, Social, Bladder, Room). The w_i are the piecewise STR#502 /
        // STR#504 weight curves (the "Happy Weight" curves), looked up per motive value.
        // There is NO smoothing — the port already recomputes on its 2-game-minute
        // cadence; only the aggregation is changed (the old law was an equal-weight
        // (sum of 7 + room) / 8 average).
        //
        // The native CalcHappy table order (rodata 0x5a45b8) is (7,5,6,15,8,14,9,13)
        // and the STR#502/504 curves fill entries 0..6 in that same order, i.e.
        //   curve[0]=Hunger  curve[1]=Energy  curve[2]=Comfort curve[3]=Fun
        //   curve[4]=Hygiene curve[5]=Social  curve[6]=Bladder
        // (verified against the extracted Global.iff STR# 502 / STR# 504 resources).
        // Room is the 8th participant. STR#502/504 contain exactly 7 curves, so the
        // Room weight is NOT a curve — it is a scalar. I could NOT pin the native room
        // weight from the static binary (the TOC pointer slots are unrelocated
        // placeholders and the saved moods are state-dependent, per R251), so this is a
        // DOCUMENTED BEST-SUPPORTED DEFAULT: RoomWeight = 1 makes Room contribute
        // comparably to a weight-1 motive. R223's least-squares fit of 8 (and a
        // state-dependent-derived ~57) are both NON-authoritative and are not used.
        private static readonly VMMotive[] MoodOrderMotives = new VMMotive[]
        {
            VMMotive.Hunger, VMMotive.Energy, VMMotive.Comfort, VMMotive.Fun,
            VMMotive.Hygiene, VMMotive.Social, VMMotive.Bladder
        };
        private const float RoomWeight = 1f;

        // Table split (person+1536, CalcHappy 0x10b9c8-0x10b9e8, reconciled from raw
        // hex in implementation.md): r5=1 (child table) only when person[1536] is in
        // [1,17]; r5=0 (adult table) when person[1536]==0 or >=18. The port exposes the
        // same field as VMPersonDataVariable.PersonsAge (attr 58), and VMFindBestAction
        // already uses the identical "age>0 && age<0x12" test for isChild.
        private const int ChildAgeMin = 1;
        private const int ChildAgeMax = 0x12; // exclusive; ages 1..17 are children
        public static float[] Constants = new float[]
        {
            180, //energy span 0
            16, //wake hours 1
            7, //wake hour 2
            0.01f, //energy drift 3
            0.3f, //hunger to bladder 4
            0.0021f, //hunger decrement ratio 5
            0.055f, //social decrement base 6
            0.000125f, //social decrement multiplier 7
            0.25f, //ent dec awake 8
            1f, //ent mult asleep 9
            0.17f, //hyg decrement awake 10
            0.08f, //hyg decrement asleep 11
            0.3f, //blad decrement awake 12
            0.15f, //blad decrement asleep 13
            0.4f, //comfort decrement active 14
            0.6f, //comfort decrement lazy 15
            0.5f, //comfort decrement 16
        };

        public static VMMotive[] DecrementMotives = new VMMotive[]
        {
            VMMotive.Hunger,
            VMMotive.Comfort,
            VMMotive.Hygiene,
            VMMotive.Bladder,
            VMMotive.Energy,
            VMMotive.Fun,
            VMMotive.Social
        };

        public short[] MotiveFractions = new short[7]; //in 1/1000ths
        public int LastMinute;

        public void Tick(VMAvatar avatar, VMContext context)
        {
            var roomScore = context.GetRoomScore(context.GetRoomAt(avatar.Position));
            avatar.SetMotiveData(VMMotive.Room, roomScore);
            var minutes = context.Clock.Minutes;
            if (minutes/2 == LastMinute || avatar.GetValue(VMStackObjectVariable.Hidden) > 0) return;
            var hours = context.Clock.Hours;
            LastMinute = minutes/2;
            var sleeping = (avatar.GetMotiveData(VMMotive.SleepState) != 0);

            for (int i = 0; i < 7; i++)
            {
                int frac = 0;
                var dm = DecrementMotives[i];
                if (avatar.HasMotiveChange(dm) && dm != VMMotive.Energy) continue;
                var motive = avatar.GetMotiveData(dm);
                var r_Hunger = ToFixed1000(Constants[5]) * (100 + avatar.GetMotiveData(VMMotive.Hunger));
                switch (i)
                {
                    case 0:
                        frac = r_Hunger; break;
                    case 1:
                        var active = avatar.GetPersonData(VMPersonDataVariable.ActivePersonality);
                        if (active > 666)
                            frac = ToFixed1000(Constants[14]);
                        else if (active < 666)
                            frac = ToFixed1000(Constants[15]);
                        else
                            frac = ToFixed1000(Constants[16]);
                        break;
                    case 2:
                        frac = ToFixed1000(Constants[sleeping ? 11 : 10]); break;
                    case 3:
                        frac = ToFixed1000(Constants[sleeping ? 13 : 12]) + FracMul(r_Hunger, ToFixed1000(Constants[4])); break;
                    case 4:
                        //try to wake the sim up if they're asleep on the wake hour, and they're well rested.
                        if (sleeping && hours == Constants[2] && motive >= Constants[0] - 100)
                        {
                            avatar.SetMotiveData(VMMotive.SleepState, 0);
                        }

                        if (sleeping)
                        {
                            if (avatar.GetMotiveData(VMMotive.SleepState) == -1)
                            {
                                frac = -643 * 2; 
                            }
                            else
                            {
                                frac = (context.Clock.Hours >= Constants[2]) ? ToFixed1000(Constants[3]) : 0;
                            }
                        } else
                        {
                            frac = (ToFixed1000(Constants[0]) / (30 * (int)Constants[1]));
                        }
                        //energy span over wake hours. small energy drift applied if asleep during the day.
                       
                        break;
                    case 5:
                        frac = (sleeping)?0:ToFixed1000(Constants[8]);
                        break;
                    case 6:
                        frac = ToFixed1000(Constants[6]) +
                            ToFixed1000(Constants[7]) * avatar.GetPersonData(VMPersonDataVariable.OutgoingPersonality);
                        break;
                }

                MotiveFractions[i] += (short)frac;
                if (MotiveFractions[i] >= 1000)
                {
                    motive -= (short)(MotiveFractions[i] / 1000);
                    MotiveFractions[i] %= 1000;
                    if (motive < -100) motive = -100;
                    avatar.SetMotiveData(DecrementMotives[i], motive);
                }
                if (MotiveFractions[i] < 0)
                {
                    motive += (short)(1 - MotiveFractions[i] / 1000);
                    MotiveFractions[i] += (short)((1 - MotiveFractions[i]/1000) * 1000);
                    avatar.SetMotiveData(DecrementMotives[i], motive);
                }
            }

            // R251: recompute mood as the curve-weighted average (no smoothing; the
            // 2-game-minute cadence gate above is unchanged). Decay is untouched.
            avatar.SetMotiveData(VMMotive.Mood, ComputeMood(avatar, roomScore));
        }

        /// <summary>
        /// Computes the R251 mood target from the current motives:
        /// mood = (Sum_i w_i(m_i)·m_i + RoomWeight·room) / (Sum_i w_i(m_i) + RoomWeight)
        /// over the 8 participants, with w_i from the STR#502 (adult) or STR#504 (child)
        /// Happy Weight curves. The Room enters with a constant weight (see the class
        /// doc for the RoomWeight disclosure). Mirrors the native CalcHappy pure-recompute
        /// law; no smoothing is applied.
        /// </summary>
        private short ComputeMood(VMAvatar avatar, int roomScore)
        {
            // Table split: age in [1,17] -> child curves (STR#504), else adult (STR#502).
            var global = Content.Content.Get().WorldObjectGlobals;
            if (global == null) return 0;
            var age = avatar.GetPersonData(VMPersonDataVariable.PersonsAge);
            var child = (age >= ChildAgeMin && age < ChildAgeMax);
            var curves = child ? global.HappyWeightChild : global.HappyWeight;

            double num = 0, den = 0;
            for (int i = 0; i < MoodOrderMotives.Length; i++)
            {
                var m = (double)avatar.GetMotiveData(MoodOrderMotives[i]);
                double w = 1.0;
                if (curves != null && i < curves.Length)
                    w = curves[i].GetPoint((float)m);
                num += w * m;
                den += w;
            }
            var room = (double)Math.Max(-100, Math.Min(100, roomScore));
            // ORIG-02: the Room weight is NOT flat — it is the same
            // HappyWeightCurves table's entry 5 (builder index array
            // {7,6,8,9,15,13,14} -> motive 13 = Room), i.e. the curve
            // (-100;2) (0;1) (100;2): weight 2 at the extremes, 1 at zero.
            // The old flat RoomWeight (r223's fit) is retired.
            double roomW = 1.0;
            if (curves != null && curves.Length > 5)
                roomW = curves[5].GetPoint((float)room);
            num += roomW * room;
            den += roomW;
            if (den <= 0) return 0;
            return (short)(num / den);
        }

        public int ToFixed1000(float input)
        {
            return (int)(input * 1000);
        }

        public int FracMul(int input, int frac)
        {
            return (int)((long)input * frac) / 1000;
        }

        public void SerializeInto(BinaryWriter writer)
        {
            writer.Write(LastMinute);
            for (int i = 0; i < 7; i++)
                writer.Write(MotiveFractions[i]);
        }

        public void Deserialize(BinaryReader reader)
        {
            LastMinute = reader.ReadInt32();
            for (int i = 0; i < 7; i++)
                MotiveFractions[i] = reader.ReadInt16();
        }
    }
}
