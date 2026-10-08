using System;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Engine.Utils;

namespace FSO.SimAntics.Primitives
{
    //like tso transfer funds, but really scaled back (only deduct from maxis)
    public class VMTS1Budget : VMPrimitiveHandler
    {
        /// <summary>
        /// Test-only observation point for committed TS1 household-budget mutations. With no
        /// subscriber (normal play) this has no effect; the parity harness uses it to attribute
        /// IFF-authored career transfers without mistaking unrelated household expenses for pay.
        /// </summary>
        public static event Action<VMStackFrame, int, int> BudgetMutated;

        private static void NotifyBudgetMutated(VMStackFrame context, int oldBudget, int newBudget)
        {
            var observers = BudgetMutated;
            if (observers == null) return;
            foreach (Action<VMStackFrame, int, int> observer in observers.GetInvocationList())
            {
                try { observer(context, oldBudget, newBudget); }
                catch (Exception error)
                {
                    Console.WriteLine("[BudgetObserver] " + error.GetType().Name + " " + error.Message);
                }
            }
        }

        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            var operand = (VMTransferFundsOperand)args;
            var amount = VMMemory.GetBigVariable(context, operand.GetAmountOwner(), (short)operand.AmountData);

            if ((operand.Flags & VMTransferFundsFlags.Subtract) > 0) amount = -amount; //instead of subtracting, we're adding
                                                                                       //weird terms for the flags here but ts1 is inverted
                                                                                       //amount contains the amount we are subtracting from the budget.

            var oldBudget = context.VM.TS1State.CurrentFamily?.Budget ?? 0;
            var newBudget = oldBudget - amount;
            if (oldBudget < 0) return VMPrimitiveExitCode.GOTO_FALSE;
            if ((operand.Flags & VMTransferFundsFlags.JustTest) == 0 && context.VM.TS1State.CurrentFamily != null)
            {
                context.VM.TS1State.CurrentFamily.Budget = newBudget;
                NotifyBudgetMutated(context, oldBudget, newBudget);
                // SIM-09: the IFF operand's expense type drives the budget
                // window's category attribution (income kinds → the two income
                // rows, NPC kinds → services, fridge/food → food, etc.).
                context.VM.TS1State.RecordTransaction(newBudget - oldBudget, operand.ExpenseType);
            }
            return VMPrimitiveExitCode.GOTO_TRUE;
        }
    }
}
