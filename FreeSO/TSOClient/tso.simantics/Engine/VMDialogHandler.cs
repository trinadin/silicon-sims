using System;
using System.Linq;
using System.Text;
using FSO.SimAntics.Engine.Utils;
using FSO.SimAntics.Primitives;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics.Model;
using FSO.SimAntics.Model.TSOPlatform;

namespace FSO.SimAntics.Engine
{
    public static class VMDialogHandler
    {
        private sealed class DialogIconSelection
        {
            public bool WasSelected;
            public VMEntity Entity;
            public short NeighborID = -1;

            public void SelectStackObject(VMStackFrame context)
            {
                WasSelected = true;
                Entity = context.StackObject;
                NeighborID = -1;
            }

            public void SelectNeighbor(VMStackFrame context)
            {
                WasSelected = true;
                Entity = null;
                NeighborID = context.StackObjectID;
            }
        }

        //should use a Trie for this in future, for performance reasons
        private static string[] valid = {
            "Object", "Me", "TempXL:", "Temp:", "$", "Attribute:", "DynamicStringLocal:", "Local:", "TimeLocal:", "NameLocal:",
            "FixedLocal:", "DynamicObjectName", "MoneyXL:", "JobOffer:", "Job:", "JobDesc:", "Param:", "Neighbor", "\r\n", "ListObject",
            "CatalogLocal:", "DateLocal:", "ObjectLocal:", "TokenNameLocal:", "\\n"
        };

        public static void ShowDialog(VMStackFrame context, VMDialogOperand operand, STR source)
        {
            context.VM.SignalDialog(BuildDialogInfo(context, operand, source));
        }

        /// <summary>
        /// Expand an ObjectDialog and recover its image-selector provenance
        /// without publishing it to UI listeners. Keeping construction pure
        /// makes the engine parse order testable and keeps ShowDialog's signal
        /// side effect in one place.
        /// </summary>
        public static VMDialogInfo BuildDialogInfo(VMStackFrame context,
            VMDialogOperand operand, STR source)
        {
            // ObjectDialog parses its response labels, title and message in this
            // exact order. In automatic-icon mode the last successfully parsed
            // $Object/$Neighbor token selects the image; $Me does not. If no
            // token selects one, the signed Stack Object ID is the fallback.
            var tracker = operand.IconMode == VMDialogIconMode.Automatic
                ? new DialogIconSelection()
                : null;
            var yes = (operand.YesStringID == 0) ? null : ParseDialogString(context,
                source.GetString(operand.YesStringID - 1), source, 0, tracker);
            var no = (operand.NoStringID == 0) ? null : ParseDialogString(context,
                source.GetString(operand.NoStringID - 1), source, 0, tracker);
            var cancel = (operand.CancelStringID == 0) ? null : ParseDialogString(context,
                source.GetString(operand.CancelStringID - 1), source, 0, tracker);
            var title = (operand.TitleStringID == 0) ? "" : ParseDialogString(context,
                source.GetString(operand.TitleStringID - 1), source, 0, tracker);
            var message = ParseDialogString(context,
                source.GetString(Math.Max(0, operand.MessageStringID - 1)), source, 0, tracker);
            var iconName = "";
            if (operand.IconMode == VMDialogIconMode.Named
                && operand.IconNameStringID != 0)
            {
                // Only named mode interprets operand byte 1 as a 1-based STR
                // index. Indexed mode uses that same byte as BMP_(5000+n), and
                // missing named entries follow the original blank-image path.
                var rawIconName = source.GetString(operand.IconNameStringID - 1);
                if (rawIconName != null)
                    iconName = ParseDialogString(context, rawIconName, source);
            }

            VMEntity icon = null;
            short iconNeighborID = -1;
            if (operand.IconMode == VMDialogIconMode.Automatic)
            {
                if (tracker != null && tracker.WasSelected)
                {
                    icon = tracker.Entity;
                    iconNeighborID = tracker.NeighborID;
                }
                else icon = context.StackObject;
            }
            else if (operand.IconMode == VMDialogIconMode.Neighbor)
            {
                iconNeighborID = context.StackObjectID;
            }

            return new VMDialogInfo
            {
                Block = (operand.Flags & VMDialogFlags.Continue) == 0,
                Caller = context.Caller,
                Icon = icon,
                IconResource = context.ScopeResource,
                IconNeighborID = iconNeighborID,
                Operand = operand,
                Message = message,
                Title = title,
                IconName = iconName,

                Yes = yes,
                No = no,
                Cancel = cancel,
                DialogID = (context.CodeOwner.GUID << 32) | ((ulong)context.Routine.ID << 16) | context.InstructionPointer
            };
        }

        private static bool CommandSubstrValid(string command)
        {
            for (int i = 0; i < valid.Length; i++)
            {
                if (command.Length <= valid[i].Length && command.Equals(valid[i].Substring(0, command.Length))) return true;
            }
            return false;
        }

        public static string ParseDialogString(VMStackFrame context, string input, STR source)
        {
            return ParseDialogString(context, input, source, 0);
        }

        public static string ParseDialogString(VMStackFrame context, string input, STR source, int depth)
        {
            return ParseDialogString(context, input, source, depth, null);
        }

        private static string ParseDialogString(VMStackFrame context, string input, STR source,
            int depth, DialogIconSelection iconSelection)
        {
            if (depth > 10) return input;
            int state = 0;
            StringBuilder command = new StringBuilder();
            StringBuilder output = new StringBuilder();

            if (input == null) return "Missing String!!!";

            for (int i = 0; i < input.Length; i++)
            {
                if (state == 0)
                {
                    if (input[i] == '$')
                    {
                        state = 1; //start parsing string
                        command.Clear();
                    } else {
                        output.Append(input[i]);
                    }
                }
                else
                {
                    command.Append(input[i]);
                    var invalid = !CommandSubstrValid(command.ToString());
                    if (i == input.Length - 1 || invalid)
                    {
                        if (invalid || char.IsDigit(input[i]))
                        {
                            command.Remove(command.Length - 1, 1);
                            i--;
                        }

                        var cmdString = command.ToString();
                        short[] values = new short[3];
                        if (cmdString.Length > 1 && cmdString[cmdString.Length - 1] == ':')
                        {
                            try
                            {
                                if (cmdString == "DynamicStringLocal:" || cmdString == "TimeLocal:" || cmdString == "JobOffer:" || cmdString == "Job:" || cmdString == "JobDesc:" || cmdString == "DateLocal:")
                                {
                                    values[1] = -1;
                                    values[2] = -1;
                                    for (int j=0; j<3; j++)
                                    {
                                        char next = input[++i];
                                        string num = "";
                                        while (char.IsDigit(next))
                                        {
                                            num += next;
                                            next = (++i == input.Length) ? '!': input[i];
                                        }
                                        if (num == "")
                                        {
                                            values[j] = -1;
                                            if (j == 1) values[2] = -1;
                                            break;
                                        }
                                        values[j] = short.Parse(num);
                                        if (i == input.Length || next != ':') break;
                                    }
                                }
                                else
                                {
                                    char next = input[++i];
                                    string num = "";
                                    while (char.IsDigit(next))
                                    {
                                        num += next;
                                        next = (++i == input.Length) ? '!' : input[i];
                                    }
                                    values[0] = short.Parse(num);
                                }
                                i--;
                            }
                            catch (FormatException)
                            {

                            }
                        }
                        try
                        {
                            switch (cmdString)
                            {
                                case "Object":
                                    if (iconSelection != null) iconSelection.SelectStackObject(context);
                                    goto case "DynamicObjectName";
                                case "DynamicObjectName":
                                    //hack: if stack object doesn't exist and should contain owner's id,
                                    //try output the callee's owner id instead for tip jar.
                                    //special id for this is -1.
                                    if (context.StackObjectID == -1 && !context.VM.TS1)
                                    {
                                        //StackObjectOwnerID call sets the id to -1 if no owner found. (null is usually 0)
                                        output.Append(context.VM.TSOState.Names.GetNameForID(
                                            context.VM, 
                                            (context.Callee.TSOState as VMTSOObjectState)?.OwnerID ?? 0
                                            ));
                                    } else
                                    {
                                        var stackObj = context.StackObject;
                                        if (stackObj.MultitileGroup.Name != "")
                                        {
                                            output.Append(stackObj.MultitileGroup.Name);
                                        }
                                        else
                                        {
                                            var obj = stackObj.MasterDefinition ?? stackObj.Object.OBJ;
                                            var strings = stackObj.Object.Resource.Get<CTSS>(obj.CatalogStringsID);
                                            if (strings != null)
                                            {
                                                output.Append(strings.GetString(0));
                                            }
                                            else
                                            {
                                                output.Append(stackObj.ToString());
                                            }
                                        }
                                    }
                                    break;
                                case "Me":
                                    output.Append(context.Caller.ToString()); break;
                                case "TempXL:":
                                    output.Append(VMMemory.GetBigVariable(context, Scopes.VMVariableScope.TempXL, values[0]).ToString()); break;
                                case "MoneyXL:":
                                    output.Append("$" + VMMemory.GetBigVariable(context, Scopes.VMVariableScope.TempXL, values[0]).ToString("##,#0")); break;
                                case "Temp:":
                                    output.Append(VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Temps, values[0]).ToString()); break;
                                case "$":
                                    output.Append("$"); i--; break;
                                case "Attribute:":
                                    output.Append(VMMemory.GetBigVariable(context, Scopes.VMVariableScope.StackObjectAttributes, values[0]).ToString()); break;
                                case "DynamicStringLocal:":
                                    STR res = null;
                                    if (values[2] != -1 && values[1] != -1)
                                    {
                                        VMEntity obj = context.VM.GetObjectById((short)context.Locals[values[2]]);
                                        if (obj == null) break;
                                        ushort tableID = (ushort)context.Locals[values[1]];

                                        {//local
                                            if (obj.SemiGlobal != null) res = obj.SemiGlobal.Get<STR>(tableID);
                                            if (res == null) res = obj.Object.Resource.Get<STR>(tableID);
                                            if (res == null) res = context.Global.Resource.Get<STR>(tableID);
                                        }
                                    } else if (values[1] != -1)
                                    {
                                        //global table
                                        ushort tableID = (ushort)context.Locals[values[1]];
                                        res = context.Global.Resource.Get<STR>(tableID);

                                    } else
                                    {
                                        res = source;
                                    }

                                    ushort index = (ushort)context.Locals[values[0]];
                                    if (res != null)
                                    {
                                        var str = res.GetString(index);
                                        output.Append(ParseDialogString(context, str, res,
                                            depth + 1, iconSelection)); // recursive command parsing!
                                        // this is needed for the crafting table.
                                        // though it is also, completely insane?
                                    }
                                    break;
                                case "Local:":
                                    output.Append(VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Local, values[0]).ToString()); break;
                                case "FixedLocal:":
                                    output.Append((VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Local, values[0])/100f).ToString("F2")); break;
                                case "TimeLocal:":
                                    var hours = VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Local, values[0]);
                                    var mins = (values[1] == -1)?0:VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Local, values[1]);
                                    var suffix = (hours > 11) ? "pm" : "am";
                                    if (hours > 12) hours -= 12;
                                    output.Append(hours.ToString());
                                    output.Append(":");
                                    output.Append(mins.ToString().PadLeft(2, '0'));
                                    output.Append(suffix);
                                    break;
                                case "ObjectLocal:":
                                    output.Append(context.VM.GetObjectById(VMMemory.GetVariable(context, Scopes.VMVariableScope.Local, values[0]))?.ToString() ?? ""); break;
                                case "JobOffer:":
                                    output.Append(Content.Content.Get().Jobs.JobOffer(
                                        (short)VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Local, values[0]),
                                        VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Local, values[1])));
                                    break;
                                case "Job:":
                                case "JobDesc:":
                                    var level = VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Local, values[1]);
                                    var jobStr = Content.Content.Get().Jobs.JobStrings(
                                        (short)VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Local, values[0]));
                                    if (jobStr != null) output.Append(jobStr.GetString(level*3+((cmdString=="JobDesc:")?3:4)));
                                    break;
                                case "Param:":
                                    output.Append(VMMemory.GetBigVariable(context, Scopes.VMVariableScope.Parameters, values[0]).ToString()); break;
                                case "NameLocal:":
                                    output.Append(context.VM.GetObjectById(VMMemory.GetVariable(context, Scopes.VMVariableScope.Local, values[0])).ToString()); break;
                                case "Neighbor":
                                    //neighbour in stack object id
                                    if (!context.VM.TS1) break;
                                    if (iconSelection != null) iconSelection.SelectNeighbor(context);
                                    var guid = Content.Content.Get().Neighborhood.GetNeighborByID(context.StackObjectID)?.GUID ?? 0;
                                    var gobj = Content.Content.Get().WorldObjects.Get(guid);
                                    if (gobj == null) output.Append("Unknown");
                                    else output.Append(gobj.Resource.Get<FSO.Files.Formats.IFF.Chunks.CTSS>(gobj.OBJ.CatalogStringsID)?.GetString(0) ?? "Unknown");
                                    break;
                                case "ListObject":
                                    output.Append(new string(context.StackObject.MyList.Select(x => (char)x).ToArray()));
                                    break;
                                case "CatalogLocal:":
                                    var catObj = context.VM.GetObjectById(VMMemory.GetVariable(context, Scopes.VMVariableScope.Local, values[0]));
                                    var cat = catObj.Object.Resource.Get<CTSS>(catObj.Object.OBJ.CatalogStringsID)?.GetString(1);
                                    output.Append(cat ?? "");
                                    break;
                                case "TokenNameLocal:":
                                    // EXP-17: the Magic Town quest-line substitution. Native table
                                    // entry '$TokenNameLocal' in the PPC (image 0x10601F98, the
                                    // 18-command dialog table at 0x10601EF0..0x10601FA8; raw-file
                                    // form 0x6018c2..0x60198f). Callers: SocialsMagic STR#301
                                    // strings 4/13/17/19/22/23/24/39/41 — the quest reward/
                                    // delivery dialogs. The index operand reads the token's
                                    // INVENTORY INDEX from Local[n]: 'Quest - Review Quest'
                                    // (SocialsMagic 4124) ins99-103 chains FindToken '03 08 11
                                    // 0d' -> local[8]=temp[1] -> dialog, and primitive-51 mode-3
                                    // writes that temp via the native selector
                                    // Temp[(op3.0x18)>>3] (PPC 0x100e31ec + store 0x100e3248).
                                    // Lookup law mirrors $Neighbor/$Object (definition GUID ->
                                    // CTSS 0 catalog name); the exact native string index for
                                    // tokens is undecoded (no parser code path found through the
                                    // TOC) — DISCLOSED inference, bounded to the name source.
                                    {
                                        var tnCaller = context.Caller as VMAvatar;
                                        List<FSO.Files.Formats.IFF.Chunks.InventoryItem> tnInv = null;
                                        if (tnCaller != null && context.VM.TS1)
                                        {
                                            var tnNid = tnCaller.GetPersonData(Model.VMPersonDataVariable.NeighborId);
                                            tnInv = Content.Content.Get().Neighborhood.GetInventoryByNID(tnNid);
                                        }
                                        if (tnInv != null)
                                        {
                                            var tnIdx = (context.Locals != null && values[0] < (context.Locals?.Length ?? 0)) ? context.Locals[values[0]] : (short)-1;
                                            if (tnIdx >= 0 && tnIdx < tnInv.Count)
                                            {
                                                var tnGuid = tnInv[tnIdx].GUID;
                                                var tnObj = Content.Content.Get().WorldObjects.Get(tnGuid);
                                                if (tnObj?.OBJ != null)
                                                {
                                                    var tnName = tnObj.Resource.Get<CTSS>(tnObj.OBJ.CatalogStringsID)?.GetString(0);
                                                    if (tnName != null) output.Append(tnName);
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case "DateLocal:":
                                    var date = new DateTime(context.Locals[values[2]], context.Locals[values[1]], context.Locals[values[0]]);
                                    output.Append(date.ToLongDateString());
                                    break;
                                case "\\n":
                                    output.Append("\n");
                                    break;
                                default:
                                    output.Append(cmdString);
                                    break;
                            }
                        } catch (Exception)
                        {
                            //something went wrong. just skip command
                        }
                        state = 0;
                    }
                }
            }
            // r253: the native text pipeline breaks lines on LF ONLY
            // (cTSFont::IsCharReturnChar @0x4ace80 compares 0x0A; the whole
            // code section has zero 0x0D compares) and game-data templates
            // author one blank paragraph line as a single \r\n\r\n pair (CR
            // is glyph-less filler). Doubling every \r\n turned that into
            // three blank lines in every TS1 VM dialog. TSO keeps the
            // upstream doubling (tools/iff-dump/r253-dialog-newline-law.md).
            if (context.Thread != null && !context.VM.TS1) output.Replace("\r\n", "\r\n\r\n");
            return output.ToString();
        }
    }
}
