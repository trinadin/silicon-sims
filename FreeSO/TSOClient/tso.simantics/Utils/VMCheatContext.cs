using FSO.Content;
using System.Collections.Generic;
using System.IO;

namespace FSO.SimAntics.Utils
{
    /// <summary>
    /// Defines the context of a cheat
    /// </summary>
    public class VMCheatContext
    {
        delegate void ExecuteHandler(VM vm, VMAvatar caller, VMCheatContext context, out bool result);
        private static Dictionary<VMCheatType, ExecuteHandler> predefinedBehavior = new Dictionary<VMCheatType, ExecuteHandler>()
            {
                { VMCheatType.Budget, new ExecuteHandler(ExecuteBudget) },
                { VMCheatType.MoveObjects, new ExecuteHandler(ExecuteMoveObjects) },
                { VMCheatType.Tutorial, new ExecuteHandler(ExecuteTutorial) },
                { VMCheatType.RestoreTut, new ExecuteHandler(ExecuteRestoreTut) }
            };

        public enum VMCheatParameterType
        {
            NO_PARAMS = 0,
            INT = 1,
            BOOL = 2
        }
        public enum BudgetCheatPresetAmount : int
        {
            /// <summary>
            /// Has no value and will not affect the budget of the family
            /// </summary>
            NO_CHEAT = 0,
            /// <summary>
            /// Has a value of $1000 (rosebud cheat in later versions)
            /// </summary>
            KLAPAUCIUS = 1000,
            /// <summary>
            /// Has a value of $1,000,000
            /// </summary>
            MOTHERLODE = 1000000
        }
        /// <summary>
        /// Defines the behavior of the cheat
        /// </summary>
        public enum VMCheatType : byte
        {
            InvalidCheat = 0,
            /// <summary>
            /// For transaction cheats it adds the amount to the user's budget
            /// </summary>
            Budget = 1,
            MoveObjects = 2,
            /// <summary>
            /// The Sims' `tutorial` cheat (id 0x3d, type 2; names "on"/"off").
            /// Modifier true ("on"/1) clears the tutorial object spawner's inhibit
            /// flag — spawning allowed; false ("off"/0) sets it — spawning
            /// disabled. The original stores flag = (param == 0) (r247 skeptic
            /// correction 1; decode.md §F had the direction backwards).
            /// </summary>
            Tutorial = 3,
            /// <summary>
            /// The Sims' `restore_tut` cheat (id 0x2d, type 0): performs the
            /// ResetTutorial file action — stage the pristine Tutorial.FAM into
            /// UserData/Import, fail-if-exists, read-only cleared. The confirm
            /// dialog and save gate around it are the client dialog flow's job.
            /// </summary>
            RestoreTut = 4
        }
        /// <summary>
        /// The selected cheat to run
        /// </summary>
        public VMCheatType CheatBehavior;
        /// <summary>
        /// The numeric parameter submitted with the cheat
        /// </summary>
        public int Amount; // used by budget cheats
        public byte Repetitions; // defined by how many ";!"s there are
        /// <summary>
        /// The boolean value submitted with the cheat
        /// </summary>
        public bool Modifier; // some cheats may require a "on" or "off" modifier
        public bool Executed
        {
            get;
            private set;
        } = false;
        public bool Execute(VM vm, VMAvatar caller)
        {
            if (CheatBehavior == VMCheatType.InvalidCheat)
                return false;
            predefinedBehavior[CheatBehavior].Invoke(vm, caller, this, out bool result); // careful! make sure all cheats are defined in predefinedBehaviors!
            Executed = true;
            return result;
        }
        private static void ExecuteBudget(VM vm, VMAvatar caller, VMCheatContext context, out bool result)
        {
            var amount = context.Amount * (context.Repetitions + 1);
            vm.GlobalLink.PerformTransaction(vm, false, uint.MaxValue, caller.PersistID, amount,
                (bool success, int transferAmount, uint uid1, uint budget1, uint uid2, uint budget2) =>
                {
                        //check if we got the money? why should I? i mean cheating is bad after all
                });
            result = true;
        }

        private static void ExecuteMoveObjects(VM vm, VMAvatar caller, VMCheatContext context, out bool result)
        {
            vm.Context.Cheats.MoveObjects = context.Modifier;
            result = true;
        }

        private static void ExecuteTutorial(VM vm, VMAvatar caller, VMCheatContext context, out bool result)
        {
            //AppCheatCallback case 0x250128 computes flag = (param == 0) from
            //the parsed parameter r24: "on" → 1, "off" → 0, numerics → their
            //value, missing → 0 (skeptic correction 1). The client parse
            //carries on/off in Modifier and numerics in Amount, so
            //param = Modifier ? 1 : Amount reconstructs r24 exactly; spawn is
            //inhibited iff param == 0 ("tutorial 5" therefore ALLOWS spawning
            //just like "on" — the numeric law, not a bool NOT of Modifier).
            var param = context.Modifier ? 1 : context.Amount;
            VMTS1TutorialSpawner.InhibitSpawn = param == 0;
            result = true;
        }

        private static void ExecuteRestoreTut(VM vm, VMAvatar caller, VMCheatContext context, out bool result)
        {
            //AppCheatCallback case 0x2d calls ResetTutorial directly; its dialog
            //and save gate are the client dialog flow's responsibility here, so
            //this runs only the file action. Same neighborhood guard as
            //TutorialCompleted: no provider/runtime neighborhood, no action.
            var nbhd = Content.Content.Get()?.Neighborhood;
            if (nbhd?.Neighborhood == null) { result = false; return; }
            result = nbhd.StageTutorialReset();
        }

        #region VMSerializable Members
        public void SerializeInto(BinaryWriter writer)
        {
            writer.Write((byte)CheatBehavior);
            writer.Write(Amount);
            writer.Write(Modifier);
            writer.Write(Repetitions);           
        }

        public void Deserialize(BinaryReader reader)
        {
            CheatBehavior = (VMCheatType)reader.ReadByte();
            Amount = reader.ReadInt32();
            Modifier = reader.ReadBoolean();
            Repetitions = reader.ReadByte();
        }
        #endregion
    }
}
