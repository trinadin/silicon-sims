using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.Files.Utils;
using FSO.LotView;
using FSO.LotView.Components;
using FSO.LotView.Model;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FSO.SimAntics.Primitives
{
    public class VMGenericTS1Call : VMPrimitiveHandler
    {
        /// <summary>
        /// Reproduces cXObject::TryGenericSimCall mode 12. The original
        /// BuildRotationLookup uses a fixed 64 by 64 tile domain rather than
        /// the current lot dimensions.
        /// </summary>
        public static short GetDistanceToCamera(short tileX, short tileY, WorldRotation rotation)
        {
            int rotatedX;
            int rotatedY;
            switch (rotation)
            {
                case WorldRotation.TopRight:
                    rotatedX = 63 - tileY;
                    rotatedY = tileX;
                    break;
                case WorldRotation.BottomRight:
                    rotatedX = 63 - tileX;
                    rotatedY = 63 - tileY;
                    break;
                case WorldRotation.BottomLeft:
                    rotatedX = tileY;
                    rotatedY = 63 - tileX;
                    break;
                default:
                    rotatedX = tileX;
                    rotatedY = tileY;
                    break;
            }
            return (short)-(rotatedX + rotatedY);
        }

        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            var operand = (VMGenericTS1CallOperand)args;

            var inventoryInd = 10;
            switch (operand.Call)
            {
                case VMGenericTS1CallMode.HouseTutorialComplete: //0
                    TutorialCompleted(context.VM, 0);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.SwapMyAndStackObjectsSlots: //1
                    var cont1 = context.Caller.Container;
                    var cont2 = context.StackObject.Container;
                    var contS1 = context.Caller.ContainerSlot;
                    var contS2 = context.StackObject.ContainerSlot;
                    if (cont1 != null && cont2 != null)
                    {
                        context.Caller.PrePositionChange(context.VM.Context);
                        context.StackObject.PrePositionChange(context.VM.Context);
                        cont1.ClearSlot(contS1);
                        cont2.ClearSlot(contS2);
                        cont1.PlaceInSlot(context.StackObject, contS1, true, context.VM.Context);
                        cont2.PlaceInSlot(context.Caller, contS2, true, context.VM.Context);
                    }
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.SetActionIconToStackObject: //2
                    context.Thread.ActiveAction.IconOwner = context.StackObject;
                    return VMPrimitiveExitCode.GOTO_TRUE;
                // 3. PullDownTaxiDialog
                case VMGenericTS1CallMode.AddToFamily: //4
                    if (context.VM.TS1State.CurrentFamily == null || context.VM.TS1State.CurrentFamily.FamilyGUIDs.Length >= 8)
                        return VMPrimitiveExitCode.GOTO_FALSE;
                    var fneigh = Content.Content.Get().Neighborhood.GetNeighborByID(context.StackObjectID);
                    if (fneigh == null) return VMPrimitiveExitCode.GOTO_FALSE;
                    AddToFamily(context.VM.TS1State.CurrentFamily, fneigh, context.VM);
                    var runtime = context.VM.TS1State.CurrentFamily.RuntimeSubset.ToList();
                    runtime.Add(fneigh.GUID);
                    context.VM.TS1State.CurrentFamily.RuntimeSubset = runtime.ToArray();
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.CombineAssetsOfFamilyInTemp0: //5
                    //adds the family in temp 0's assets to our budget. (for move in)
                    var family = Content.Content.Get().Neighborhood.GetFamily((ushort)context.Thread.TempRegisters[0]);
                    context.VM.TS1State.CurrentFamily.Budget += family.ValueInArch + family.Budget;
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.RemoveFromFamily: //6
                    if (context.VM.TS1State.CurrentFamily == null)
                        return VMPrimitiveExitCode.GOTO_FALSE;
                    fneigh = Content.Content.Get().Neighborhood.GetNeighborByID(context.StackObjectID);
                    if (fneigh == null) return VMPrimitiveExitCode.GOTO_FALSE;
                    fneigh.PersonData[(int)VMPersonDataVariable.TS1FamilyNumber] = 0;
                    var runtimef = GetRuntimeNeigh(context.VM, (ushort)context.StackObjectID);
                    runtimef?.SetPersonData(VMPersonDataVariable.TS1FamilyNumber, 0);

                    var guids = context.VM.TS1State.CurrentFamily.FamilyGUIDs.ToList();
                    guids.Remove(fneigh.GUID);
                    context.VM.TS1State.CurrentFamily.FamilyGUIDs = guids.ToArray();
                    TryDeleteFamily(context.VM.TS1State.CurrentFamily);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                /*
               MakeNewNeighbor = 7, //this one is "depracated". there's a dedicated primitive for this. */

                case VMGenericTS1CallMode.FamilyTutorialComplete: //8
                    TutorialCompleted(context.VM, 1);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.ArchitectureTutorialComplete: //9
                    TutorialCompleted(context.VM, 2);
                    return VMPrimitiveExitCode.GOTO_TRUE;

                case VMGenericTS1CallMode.DisableBuildBuy: //10
                    context.VM.Context.Architecture.BuildBuyEnabled = false;
                    context.VM.SignalGenericVMEvt(VMEventType.TS1BuildBuyChange, 0);
                    break;
                case VMGenericTS1CallMode.EnableBuildBuy: //11
                    context.VM.Context.Architecture.BuildBuyEnabled = true;
                    context.VM.SignalGenericVMEvt(VMEventType.TS1BuildBuyChange, 1);
                    break;
                case VMGenericTS1CallMode.GetDistanceToCameraInTemp0: //12
                    var position = context.StackObject.Position;
                    var rotation = (VM.UseWorld && context.VM.Context.World != null)
                        ? context.VM.Context.World.State.Rotation
                        : WorldRotation.TopLeft;
                    context.Thread.TempRegisters[0] = GetDistanceToCamera(
                        position.TileX, position.TileY, rotation);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.AbortInteractions: //13
                    // TS1 ObjectModule::CleanupPeople calls cXPerson::Cleanup on
                    // every person except the target. Its saved Interaction
                    // TargetID/Icon fields map to Callee/IconOwner in this VM.
                    var abortTarget = context.StackObject;
                    if (abortTarget == null)
                    {
                        // SIM-13 (native law, SIM-12 decode): an unresolved Stack
                        // Object ID reports error code 10 through the original's
                        // non-fatal reporter (PPC 0x590720, returns), then runs
                        // ObjectModule::CleanupPeople(module, null): every person
                        // flushes its ENTIRE action queue, routing each removal
                        // through CancelAction (the existing Queue Skipped /
                        // entry-point-4 contract). No person self-teardown, and
                        // the BHAV continues on the TRUE branch.
                        // Documented divergence (SIM-12 Option A): the base-object
                        // self-teardown half (entry-point-3 tree, slot/array
                        // deregistration, sound quieting, tutorial-pointer clear)
                        // is NOT reproduced, to avoid mid-tick mass entity removal
                        // (netplay/state-corruption risk for behavior the owned
                        // corpus never reaches).
                        System.Console.WriteLine(
                            "[GenericCall13] error 10: unresolved Stack Object ID.");
                        foreach (VMAvatar person in context.VM.Context.ObjectQueries.Avatars.ToList())
                        {
                            if (person.Dead || person.Thread == null) continue;
                            var personThread = person.Thread;
                            var active = personThread.ActiveAction;
                            if (active != null) personThread.CancelAction(active.UID);
                            foreach (var action in personThread.Queue.ToList())
                                personThread.CancelAction(action.UID);
                        }
                        return VMPrimitiveExitCode.GOTO_TRUE;
                    }
                    foreach (VMAvatar person in context.VM.Context.ObjectQueries.Avatars.ToList())
                    {
                        if (person == abortTarget || person.Dead || person.Thread == null) continue;

                        var personThread = person.Thread;
                        var active = personThread.ActiveAction;
                        var activeMatches = active != null
                            && (active.Callee == abortTarget
                                || active.IconOwner == abortTarget
                                || personThread.Stack.Any(frame =>
                                    frame.Callee == abortTarget
                                    || frame.StackObject == abortTarget));

                        // Match the original ordering: inspect current object-use
                        // and stack state before Queue Skipped callbacks can mutate it.
                        if (activeMatches) personThread.CancelAction(active.UID);

                        foreach (var action in personThread.Queue.ToList())
                        {
                            if (activeMatches && action == active) continue;
                            if (action.Callee == abortTarget || action.IconOwner == abortTarget)
                                personThread.CancelAction(action.UID);
                        }
                    }
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.HouseRadioStationEqualsTemp0: //14
                    // Original name says "equals", but the PPC case calls
                    // cSimulator::SetGlobal(31, signed Temp0) unconditionally.
                    context.VM.SetGlobalValue(31, context.Thread.TempRegisters[0]);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.MyRoutingFootprintEqualsTemp0: //15
                    //todo: change the avatar's routing footprint (need to find out how exactly this is changed in the normal game)
                    break;
                // 16. Change Normal Outfit
                case VMGenericTS1CallMode.ChangeToLotInTemp0: //17
                    //-1 is this family's home lot
                    var switchLotId = (uint)context.Thread.TempRegisters[0];
                    var vacation = switchLotId >= 40 && switchLotId < 50;
                    // TRV-02: an invalid destination must refuse travel rather
                    // than signal a lot switch into a missing house file.
                    if (switchLotId != 0xFFFFFFFFu)
                    {
                        var destPath = Content.Content.Get().Neighborhood.GetHousePath((short)switchLotId);
                        if (string.IsNullOrEmpty(destPath) || !File.Exists(destPath))
                            return VMPrimitiveExitCode.GOTO_FALSE;
                    }
                    var crossData = Content.Content.Get().Neighborhood.GameState;
                    crossData.ActiveFamily = context.VM.TS1State.CurrentFamily;
                    crossData.DowntownSimGUID = context.Caller.Object.OBJ.GUID;
                    crossData.LotTransitInfo = (vacation) ? (short)1 : context.VM.GetGlobalValue(34);
                    // TRV-02: mirror the transit state so a save taken on the
                    // destination lot reloads with the return path intact.
                    context.VM.TS1State.LotTransitInfo = crossData.LotTransitInfo;
                    context.VM.TS1State.DowntownSimGUID = crossData.DowntownSimGUID;
                    var people = new List<VMAvatar>();

                    people.Add((VMAvatar)context.Caller);
                    if (crossData.LotTransitInfo >= 1)
                    {
                        foreach (VMAvatar person in context.VM.Context.ObjectQueries.Avatars)
                        {
                            if (person.GetPersonData(VMPersonDataVariable.TS1FamilyNumber) == crossData.ActiveFamily.ChunkID && person != context.Caller)
                                people.Add(person);
                        }
                    }

                    int pi = 0;
                    foreach (var person in people)
                    {
                        var nid = person.GetPersonData(VMPersonDataVariable.NeighborId);
                        var dtInv = InitInventory(nid);

                        SaveIData(dtInv, 0, person.GetMotiveData(VMMotive.Bladder));
                        SaveIData(dtInv, 1, person.GetMotiveData(VMMotive.Comfort));
                        SaveIData(dtInv, 2, person.GetMotiveData(VMMotive.Energy));
                        SaveIData(dtInv, 3, person.GetMotiveData(VMMotive.Fun));
                        SaveIData(dtInv, 4, person.GetMotiveData(VMMotive.Hunger));
                        SaveIData(dtInv, 5, person.GetMotiveData(VMMotive.Hygiene));
                        SaveIData(dtInv, 6, person.GetMotiveData(VMMotive.Social));

                        if (crossData.LotTransitInfo > 1)
                            SaveIData(dtInv, 9, (short)context.VM.TS1State.CurrentFamily.FamilyGUIDs.Length);

                        if (pi++ == 0)
                        {
                            SaveIData(dtInv, 7, (short)context.VM.Context.Clock.Hours);
                            SaveIData(dtInv, 8, (short)context.VM.Context.Clock.Minutes);
                        }
                    }

                    //the original game sends avatar motive data along in their inventory under type 2
                    //this is called "inventory sim data effects"


                    context.VM.SignalLotSwitch(switchLotId);
                    return VMPrimitiveExitCode.GOTO_TRUE_NEXT_TICK;
                case VMGenericTS1CallMode.BuildTheDowntownSimAndPlaceObjIDInTemp0: //18
                    //spawn downtown sim out of world

                    var crossDataDT = Content.Content.Get().Neighborhood.GameState;

                    var control = context.VM.Context.CreateObjectInstance(crossDataDT.DowntownSimGUID, LotTilePos.OUT_OF_WORLD, Direction.NORTH)?.BaseObject;
                    ((VMAvatar)control).AvatarState.Permissions = Model.TSOPlatform.VMTSOAvatarPermissions.Owner;
                    context.VM.SetGlobalValue(3, control.ObjectID);
                    context.VM.SendCommand(new VMNetChangeControlCmd() { TargetID = control.ObjectID });
                    crossDataDT.ActiveFamily.SelectOneMember(crossDataDT.DowntownSimGUID);
                    context.VM.TS1State.ActivateFamily(context.VM, crossDataDT.ActiveFamily);

                    context.Thread.TempRegisters[0] = context.VM.GetGlobalValue(3);
                    if (VM.UseWorld) context.VM.Context.World.CenterTo((AvatarComponent)(context.VM.GetObjectById(context.VM.GetGlobalValue(3))?.WorldUI));
                    break;
                case VMGenericTS1CallMode.SpawnDowntownDateOfPersonInTemp0: //18
                    //spawn our autofollow sim
                    var neighbourhood = Content.Content.Get().Neighborhood;
                    var ntarget = (VMAvatar)context.VM.GetObjectById(context.Thread.TempRegisters[0]);
                    context.Thread.TempRegisters[0] = -1;
                    if (ntarget == null) return VMPrimitiveExitCode.GOTO_FALSE; //vacation?
                    var neighbour = ntarget.GetPersonData(Model.VMPersonDataVariable.NeighborId);
                    var inventory = neighbourhood.GetInventoryByNID(neighbour);
                    if (inventory != null)
                    {
                        var toSpawn = inventory.FirstOrDefault(x => x.Type == 2 && x.GUID == inventoryInd)?.Count;
                        if (toSpawn != null)
                        {
                            var spawntarg = neighbourhood.GetNeighborByID((short)toSpawn);
                            var autofollow = context.VM.Context.CreateObjectInstance(spawntarg.GUID, LotTilePos.OUT_OF_WORLD, Direction.NORTH)?.BaseObject;
                            context.Thread.TempRegisters[0] = autofollow.ObjectID;
                            inventory.RemoveAll(x => x.Type == 2 && x.GUID == inventoryInd);
                        }
                    }
                    break;
                case VMGenericTS1CallMode.SpawnTakeBackHomeDataOfPersonInTemp0:
                    inventoryInd = 11;
                    goto case VMGenericTS1CallMode.SpawnDowntownDateOfPersonInTemp0;
                case VMGenericTS1CallMode.SpawnInventorySimDataEffects:
                    //for the caller? stack object?
                    //do caller for now
                    var eperson = (VMAvatar)context.VM.GetObjectById(context.Thread.TempRegisters[0]);
                    var eInv = InitInventory(eperson.GetPersonData(VMPersonDataVariable.NeighborId));

                    if (eInv.Count(x => x.Type == 2) == 0)
                        return VMPrimitiveExitCode.GOTO_TRUE;

                    eperson.SetMotiveData(VMMotive.Bladder, GetIData(eInv, 0));
                    eperson.SetMotiveData(VMMotive.Comfort, GetIData(eInv, 1));
                    eperson.SetMotiveData(VMMotive.Energy, GetIData(eInv, 2));
                    eperson.SetMotiveData(VMMotive.Fun, GetIData(eInv, 3));
                    eperson.SetMotiveData(VMMotive.Hunger, GetIData(eInv, 4));
                    eperson.SetMotiveData(VMMotive.Hygiene, GetIData(eInv, 5));
                    eperson.SetMotiveData(VMMotive.Social, GetIData(eInv, 6));

                    //remove the effects since we've used em
                    //7 and 8, time, were used to start the lot. they arent really used on return
                    //9 is not used by fso

                    eInv.RemoveAll(x => x.Type == 2 && x.GUID < 7 && x.GUID >= 0);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.SelectDowntownLot: //22
                    // TRV-02/TRV-01: mode 22 is dead in the reference build — the
                    // compiler folded it with deprecated mode 7 and the body only
                    // raises native error 42 before returning true. Kept as a
                    // no-op; the old downtown lot picker is an obsolete dialect.
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.GetDowntownTimeFromSOInventory: //23
                    {
                        // TRV-02 (TRV-01 decode, native case 23): read the
                        // departure hours/minutes (tokens 7/8 of inventory type 2
                        // on the stack object's neighbour inventory) into
                        // Temp0/Temp1 and CONSUME the tokens.
                        var sperson = context.StackObject as VMAvatar;
                        if (sperson == null) return VMPrimitiveExitCode.GOTO_FALSE;
                        var sInv = InitInventory(sperson.GetPersonData(VMPersonDataVariable.NeighborId));
                        context.Thread.TempRegisters[0] = TakeIData(sInv, 7);
                        context.Thread.TempRegisters[1] = TakeIData(sInv, 8);
                        return VMPrimitiveExitCode.GOTO_TRUE;
                    }
                case VMGenericTS1CallMode.HotDateChangeSuitsPermanentlyCall:
                    //temp 0: outfit type
                    //temp 1: outfit index
                    context.Thread.TempRegisters[1] = VMTS1PurchasableOutfitHelper.SetSuit(
                        (VMAvatar)context.Caller, 
                        context.Thread.TempRegisters[0],
                        context.Thread.TempRegisters[1]);

                    return VMPrimitiveExitCode.GOTO_TRUE;

                // 25. SaveSimPersistentData (motives, relationships?)
                case VMGenericTS1CallMode.SaveSimPersistentData:
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.BuildVacationFamilyPutFamilyNumInTemp0: //26
                    //in our implementation, vacation lots build the family in the same way as normal lots.
                    var crossData2 = Content.Content.Get().Neighborhood.GameState;
                    if (crossData2.LotTransitInfo >= 1)
                    {
                        crossData2.ActiveFamily.SelectWholeFamily();
                        context.VM.TS1State.ActivateFamily(context.VM, crossData2.ActiveFamily);
                        context.Thread.TempRegisters[0] = context.VM.GetGlobalValue(9);

                        //set to 1 if we spawned a whole family.
                        //seems to be from globals 34 on the lot we exited. Magic town uses 0 for a single sim, and 1 for whole family 
                        //(blimp, though 1 is still set for whole family when theres only one person in it!)

                        context.VM.TS1State.VerifyFamily(context.VM);
                        context.VM.SendCommand(new VMNetChangeControlCmd() { TargetID = context.VM.Context.ObjectQueries.GetObjectsByGUID(crossData2.DowntownSimGUID).FirstOrDefault()?.ObjectID ?? 0 });
                    }
                    else
                    {
                        var control2 = context.VM.Context.CreateObjectInstance(crossData2.DowntownSimGUID, LotTilePos.OUT_OF_WORLD, Direction.NORTH)?.BaseObject;
                        ((VMAvatar)control2).AvatarState.Permissions = Model.TSOPlatform.VMTSOAvatarPermissions.Owner;
                        context.VM.SetGlobalValue(3, control2.ObjectID);
                        context.VM.SendCommand(new VMNetChangeControlCmd() { TargetID = control2.ObjectID });
                        crossData2.ActiveFamily.SelectOneMember(crossData2.DowntownSimGUID);
                        context.VM.TS1State.ActivateFamily(context.VM, crossData2.ActiveFamily);

                        context.Thread.TempRegisters[0] = context.VM.GetGlobalValue(3);
                    }

                    context.Thread.TempRegisters[1] = (short)((crossData2.LotTransitInfo >= 1) ? 1 : 0);
                    break;
                case VMGenericTS1CallMode.ReturnNumberOfAvaiableVacationLotsInTemp0: //27
                    {
                        // TRV-02 (TRV-01 decode, native case 27): nine rentals
                        // (40..48); one is unavailable while a neighborhood family
                        // references it as its house. Native writes the remainder
                        // to Temp0.
                        var occupied = CountOccupiedVacationLots(
                            Content.Content.Get().Neighborhood.MainResource?.List<FAMI>());
                        context.Thread.TempRegisters[0] = (short)Math.Max(0, 9 - occupied);
                        break;
                    }
                case VMGenericTS1CallMode.ReturnZoningTypeOfLotInTemp0: //28
                    var zones = Content.Content.Get().Neighborhood.ZoningDictionary;
                    short result = 1;
                    if (zones.TryGetValue(context.Thread.TempRegisters[0], out result))
                        context.Thread.TempRegisters[0] = result;
                    else context.Thread.TempRegisters[0] = (short)((context.Thread.TempRegisters[0] >= 81 && context.Thread.TempRegisters[0] <= 89) ? 2 : 1);
                    return VMPrimitiveExitCode.GOTO_TRUE;

                case VMGenericTS1CallMode.SetStackObjectsSuit:
                    context.Thread.TempRegisters[1] = VMTS1PurchasableOutfitHelper.SetSuit(
                        (VMAvatar)context.StackObject, 
                        context.Thread.TempRegisters[0],
                        context.Thread.TempRegisters[1]);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.GetStackObjectsSuit:
                    context.Thread.TempRegisters[1] = VMTS1PurchasableOutfitHelper.GetSuitIndex((VMAvatar)context.StackObject, context.Thread.TempRegisters[0]);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.CountStackObjectSuits:
                    var validSuits = VMTS1PurchasableOutfitHelper.GetValidOutfits((VMAvatar)context.StackObject, context.Thread.TempRegisters[0]);
                    context.Thread.TempRegisters[0] = (short)validSuits.Length;
                    return VMPrimitiveExitCode.GOTO_TRUE;
                // 32. CreatePurchasedPetsNearOwner
                case VMGenericTS1CallMode.CreatePurchasedPetsNearOwner:
                    context.VM.TS1State.CurrentFamily.SelectWholeFamily();
                    context.VM.TS1State.VerifyFamily(context.VM);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                case VMGenericTS1CallMode.AddToFamilyInTemp0:
                    family = Content.Content.Get().Neighborhood.GetFamily((ushort)context.Thread.TempRegisters[0]);
                    if (family == null || family.FamilyGUIDs.Length >= 8)
                        return VMPrimitiveExitCode.GOTO_FALSE;
                    fneigh = Content.Content.Get().Neighborhood.GetNeighborByID(context.StackObjectID);
                    if (fneigh == null) return VMPrimitiveExitCode.GOTO_FALSE;
                    AddToFamily(context.VM.TS1State.CurrentFamily, fneigh, context.VM);
                    return VMPrimitiveExitCode.GOTO_TRUE;
                // 34/36. PromoteFameIfNeeded / DemoteFameIfNeeded (superstar-fame,
                // EXP-11 runtime-leg prep 2026-09-24). Previously silent no-ops
                // (fall-through GOTO_TRUE): person[81] never updated, so fame
                // decay/awards/set-unlocks all stalled (Global.iff #484 ins16/ins29
                // and ControllerStudioLot #4128 ins7 are the only callers).
                //
                // Native law, decoded instruction-level from the owned PPC PEF
                // (sha 33c76da2...; TryGenericSimCall switch table TOC[-0x59a0],
                // case 34 = file 0xf3c18 -> PromoteIfNeeded__10cFameTrackFi
                // 0x58500, case 36 = 0xf3cd8 -> DemoteIfNeeded__10cFameTrackFi
                // 0x582d0, on the GetFameTrackData singleton 0x577c0):
                //
                //   Demote(handle): if handle==-1 or person[81]==0 return false;
                //     if person[80] < fameScoreTable[person[81]-1]:
                //       person[81]--; return true.
                //   Promote(handle): if handle==-1 return false; level=person[81];
                //     if level==10 or level>10 return false;
                //     score=person[80]; if score < fameScoreTable[level] return false;
                //     6-category skill gate (per-level tables, person-data
                //     categories [10,12,11,17,18,15] = Cooking, Mechanical,
                //     Charisma, Body, Logic, Creativity — PEF data 0x59a0c0):
                //     fail if personData[cat] < req*100;
                //     famous-friend gate: promote only when
                //     GetFamousFriendCount(handle) * 0.5 >= friendsNeeded[level]
                //     (float K = 0.5 at PEF 0x59a0d8; fcmpu+cror+bge returns 0
                //     when count*0.5 < needed);
                //     then person[81]++; if person[82] < person[81] person[82]++;
                //     return true.
                //   The tables load natively from GameData/fame.iff STR# keys
                //   (LoadUpFameTrackScoring 0x57f00 -> LoadUIStrings 4/5/6):
                //   STR# 5 "Fame Score Needed" = score threshold per level 0..10
                //   [2,9,21,35,52,78,156,300,550,900,0]; STR# 4 "Friend Star
                //   Power Needed" = famous friends needed per level
                //   [0,0,0,0,2,4,7,11,14,18,0]; STR# 6 "Skills Needed" =
                //   6 comma-separated per-level requirements.
                //   Residuals (named, not invented): (a) the native's exact
                //   famous-friend filter (PersonFinder walk 0xb45c0/0xb4ff0/
                //   0x105310) is unreproduced — this port counts OTHER on-lot
                //   avatars with fame score > 0 (the game's own fame-track
                //   predicate, Global #485); (b) the optional operand param0
                //   (native lha r0,4(r24) → fame-table person lookup when
                //   nonzero) is inert in all game-data call sites (0) and is
                //   not reproduced; (c) the native's find-person failure path
                //   (non-fatal error 28 via PPC 0x590720) is mirrored as a log
                //   line + GOTO_FALSE.
                case VMGenericTS1CallMode.PromoteFameIfNeeded: //34
                case VMGenericTS1CallMode.DemoteFameIfNeeded: //36
                    return FameRecompute(context, operand.Call == VMGenericTS1CallMode.PromoteFameIfNeeded);
                case VMGenericTS1CallMode.TakeTaxiHook: //35
                    //not sure where this one is called, seems to have been added for studiotown
                    break;
                // 36. DemoteFameIfNeeded (implemented above with mode 34)
                // 37. CancelPieMenu
                // 38. GetTokensFromString (MM)
                // 39. ChildToAdult (let's make this at least keep their skin colour, maybe)
                // 40. PetToAdult
                // 41. HeadFlush
                // 42. MakeTemp0SelectedSim,
                case VMGenericTS1CallMode.MakeTemp0SelectedSim:
                    //right now assume there's only one ts1 client, and that's us.
                    var vm = context.VM;
                    var target = vm.GetObjectById(context.Thread.TempRegisters[0]);
                    if (target == null || target is VMGameObject) return VMPrimitiveExitCode.GOTO_FALSE;

                    var caller = vm.GetAvatarByPersist(vm.MyUID);
                    if (caller != null)
                    {
                        //relinquish previous control
                        vm.Context.ObjectQueries.RemoveAvatarPersist(caller.PersistID);
                        caller.PersistID = 0;
                    }

                    target.PersistID = vm.MyUID;
                    vm.Context.ObjectQueries.RegisterAvatarPersist((VMAvatar)target, target.PersistID);
                    vm.SetGlobalValue(3, target.ObjectID);
                    
                    return VMPrimitiveExitCode.GOTO_TRUE;
                // 43. FamilySpellsIntoController
            }
            return VMPrimitiveExitCode.GOTO_TRUE;
        }

        /// <summary>
        /// Port of Neighborhood::TutorialCompleted (0xad9e0; r247 decode.md §C,
        /// skeptic correction 2), the target of generic sim calls 0/8/9 with
        /// arg = 0/1/2. Writes arg+1 to the neighborhood tutorial state (+0x12a)
        /// and clears the tutorial house latch (+0x12c), both live and in the
        /// neighborhood file's NGBH record, persisted by an immediate save. arg 0
        /// additionally clears the "tutorial session active" latch — simulator
        /// global 26 — both on the live simulator and in the current house file's
        /// SIMI chunk (a targeted two-byte patch; a whole-file IffFile.Write
        /// cannot round-trip a lazily opened house). The original opens
        /// Houses/House%02d.iff (house = the neighborhood's current house) for
        /// every arg FIRST and aborts all writes when it cannot, so a missing
        /// house file means nothing is written; for arg 0 the house-file
        /// open+reconstitute+write IS the SIMI patch, so a failed patch (its
        /// bool result) aborts everything too — no NGBH arg+1, no TutorialHouse
        /// clear, no live global-26 clear, no save. The script's primitive exit
        /// code does not depend on this succeeding.
        /// </summary>
        private static void TutorialCompleted(VM vm, int arg)
        {
            if (!vm.TS1) return;
            var nbhd = Content.Content.Get().Neighborhood;
            if (nbhd?.Neighborhood == null) return;

            // House file handled FIRST for every arg (decode.md §C: both opens
            // precede any write; failure destructs both and returns).
            var house = vm.TS1State.CurrentHouse;
            if (house <= 0) return; //no current house: the native's house-file open fails first
            if (!File.Exists(nbhd.GetHousePath(house))) return; //IFFResFile2::Open failure: the original writes nothing

            if (arg == 0)
            {
                // The arg-0 SIMI patch subsumes the native's house-file
                // reconstitute+save: it runs FIRST and its bool result gates
                // every other write (r247 repair F2 — the result was ignored,
                // letting the NGBH/live writes land on a failed house patch).
                if (!nbhd.PatchHouseSimiGlobal(house, 26, 0))
                {
                    System.Console.WriteLine("R247 TutorialCompleted: house " + house
                        + " SIMI global-26 patch failed; no completion writes performed");
                    return;
                }
            }

            nbhd.TutorialState = arg + 1;  //Neighborhood+0x12a (NGBH word 1), live and persisted
            nbhd.TutorialHouse = 0;        //Neighborhood+0x12c (NGBH word 2)
            if (arg == 0) vm.SetGlobalValue(26, 0); //live simulator global 26
            nbhd.SaveNeighbourhood(false); //the NGBH patch reaches the file immediately
        }

        private short GetIData(List<InventoryItem> inventory, uint guid)
        {
            return (short)(inventory.FirstOrDefault(x => x.Type == 2 && x.GUID == guid)?.Count ?? 0);
        }

        /// <summary>
        /// TRV-02 (native GetTokenAtIndex + RemoveTokenByIndex): read a type-2
        /// inventory token's value and consume the token in the same step.
        /// Returns 0 when the token is absent.
        /// </summary>
        public static short TakeIData(List<InventoryItem> inventory, uint guid)
        {
            var token = inventory.FirstOrDefault(x => x.Type == 2 && x.GUID == guid);
            if (token == null) return 0;
            var value = (short)token.Count;
            inventory.RemoveAll(x => x.Type == 2 && x.GUID == guid);
            return value;
        }

        /// <summary>
        /// TRV-02 (native case 27): vacation rentals 40..48 are unavailable while
        /// a neighborhood family's house reference equals them; the remainder of
        /// nine is the bookable count.
        /// </summary>
        public static int CountOccupiedVacationLots(IEnumerable<FAMI> families)
        {
            if (families == null) return 0;
            return families.Count(f => f != null && f.HouseNumber >= 40 && f.HouseNumber <= 48);
        }

        private void SaveIData(List<InventoryItem> inventory, uint guid, short data)
        {
            var replace = inventory.FirstOrDefault(x => x.Type == 2 && x.GUID == guid);
            if (replace == null)
            {
                replace = new InventoryItem() { Type = 2, GUID = guid };
                inventory.Add(replace);
            }
            replace.Count = (ushort)data;
        }

        private List<InventoryItem> InitInventory(short neighbour)
        {
            var neighbourhood = Content.Content.Get().Neighborhood;
            var inventory = neighbourhood.GetInventoryByNID(neighbour);
            if (inventory == null)
            {
                //set up this neighbour's inventory...
                inventory = new List<InventoryItem>();
                neighbourhood.SetInventoryForNID(neighbour, inventory);
            }
            return inventory;
        }

        private void TryDeleteFamily(FAMI family)
        {
            //delete the family if there's no people in it
            if (family.FamilyGUIDs.Length == 0)
            {
                family.ChunkParent.FullRemoveChunk(family);
            }
        }

        private void AddToFamily(FAMI family, Neighbour neigh, VM vm)
        {
            //was the neighbor already in a family?
            if (neigh.PersonData != null) {
                var famID = neigh.PersonData[(int)VMPersonDataVariable.TS1FamilyNumber];
                var oldFam = Content.Content.Get().Neighborhood.GetFamily((ushort)famID);
                if (oldFam != null && oldFam.ChunkID != 0)
                {
                    var oguids = oldFam.FamilyGUIDs.ToList();
                    oguids.Remove(neigh.GUID);
                    oldFam.FamilyGUIDs = oguids.ToArray();
                    TryDeleteFamily(oldFam);
                }
                neigh.PersonData[(int)VMPersonDataVariable.TS1FamilyNumber] = (short)family.ChunkID;
            }

            var guids = family.FamilyGUIDs.ToList();
            guids.Add(neigh.GUID);
            family.FamilyGUIDs = guids.ToArray();

            //if the sim is on the lot, change their runtime person data to reflect the new family id.
            var runtime = (VMAvatar)vm.Context.ObjectQueries.Avatars.FirstOrDefault(x => ((VMAvatar)x).GetPersonData(VMPersonDataVariable.NeighborId) == neigh.NeighbourID);
            runtime?.SetPersonData(VMPersonDataVariable.TS1FamilyNumber, (short)family.ChunkID);
        }

        private VMAvatar GetRuntimeNeigh(VM vm, ushort neighborID)
        {
            return (VMAvatar)vm.Context.ObjectQueries.Avatars.FirstOrDefault(x => ((VMAvatar)x).GetPersonData(VMPersonDataVariable.NeighborId) == neighborID);
        }

        /// <summary>
        /// superstar-fame: the cFameTrack tables, parsed from the same source the
        /// native uses (GameData/fame.iff STR# 4/5/6 KEYS — the entry strings are
        /// "DO NOT TRANSLATE" placeholders; the data rides the null-terminated
        /// key of each format--3 (0xFFFD) entry, which this port's STR parser
        /// exposes as STRItem.Value). Lazy, try-once; null when the file or the
        /// expected tables are missing (the modes then report no-change).
        /// </summary>
        private sealed class FameTrackTables
        {
            public short[] Score;    // STR# 5, per level 0..10 (threshold to hold/reach level+1)
            public short[] Friends;  // STR# 4, per level 0..10 (famous friends needed)
            public short[][] Skills; // STR# 6, [level][6] in FameSkillCats order
        }

        /// <summary>Native category order (PEF data 0x59a0c0): Cooking, Mechanical,
        /// Charisma, Body, Logic, Creativity — matches STR# 6's title order.</summary>
        private static readonly int[] FameSkillCats = { 10, 12, 11, 17, 18, 15 };

        private static FameTrackTables _fameTables;
        private static bool _fameTablesTried;

        private static FameTrackTables GetFameTrackTables()
        {
            if (_fameTablesTried) return _fameTables;
            _fameTablesTried = true;
            try
            {
                // Resolve via the TS1 base path — the established idiom for
                // GameData lookups (HITTVOn.cs, IDETester.cs). A TS1AllFiles
                // scan-list lookup is not viable here: the launcher
                // normalizes -path with a trailing slash, so _ScanFiles
                // stores base-relative entries ('GameData/fame.iff') that
                // can never satisfy an EndsWith("/GameData/fame.iff") test,
                // and _fameTablesTried below would permanently cache any
                // null read before the background scan completes.
                string path = null;
                var content = Content.Content.Get();
                var ts1Base = content?.TS1BasePath;
                if (!string.IsNullOrEmpty(ts1Base))
                {
                    path = Path.Combine(ts1Base, "GameData", "fame.iff");
                    if (!File.Exists(path)) path = null;
                }
                if (path == null) return null;
                var iff = new IffFile(path);
                var score = ParseFameKeyList(iff, 5, 1);
                var friends = ParseFameKeyList(iff, 4, 1);
                if (score == null || friends == null) return null;
                var skills = new short[11][];
                for (ushort level = 0; level < 11; level++)
                {
                    var entry = iff.Get<STR>(6)?.GetStringEntry(level);
                    if (entry == null) return null;
                    var parts = (entry.Value ?? "").Split(',');
                    if (parts.Length != FameSkillCats.Length) return null;
                    var row = new short[FameSkillCats.Length];
                    for (var i = 0; i < parts.Length; i++)
                    {
                        if (!short.TryParse(parts[i].Trim(), out row[i])) return null;
                    }
                    skills[level] = row;
                }
                _fameTables = new FameTrackTables { Score = score, Friends = friends, Skills = skills };
            }
            catch (Exception e)
            {
                System.Console.WriteLine("[FameTrack] fame.iff table load failed: " + e.GetType().Name + " " + e.Message);
            }
            return _fameTables;
        }

        private static short[] ParseFameKeyList(IffFile iff, ushort strId, int partsPerEntry)
        {
            var str = iff.Get<STR>(strId);
            if (str == null) return null;
            var result = new short[11];
            for (ushort i = 0; i < 11; i++)
            {
                var entry = str.GetStringEntry(i);
                if (entry == null) return null;
                var key = entry.Value ?? "";
                if (partsPerEntry != 1) return null;
                if (!short.TryParse(key.Trim(), out result[i])) return null;
            }
            return result;
        }

        /// <summary>
        /// TS1 generic call modes 34/36 — the fame level recompute. See the
        /// case comment above for the decoded native law. Returns true (native
        /// bool) exactly when the level register moved.
        /// </summary>
        private VMPrimitiveExitCode FameRecompute(VMStackFrame context, bool promote)
        {
            var tables = GetFameTrackTables();
            var ava = context.StackObject as VMAvatar;
            if (ava == null)
            {
                // Native find-person failure: non-fatal error 28 (PPC 0x590720),
                // tree continues with a "false" result.
                System.Console.WriteLine("[GenericCall34/36] error 28: Stack Object is not a person ("
                    + VMGenericTSOCall.FrameInfo(context) + ")");
                return VMPrimitiveExitCode.GOTO_FALSE;
            }
            if (tables == null)
            {
                System.Console.WriteLine("[GenericCall34/36] fame.iff tables unavailable; recompute skipped");
                return VMPrimitiveExitCode.GOTO_FALSE;
            }

            var score = ava.GetPersonData(VMPersonDataVariable.TS1FameScore);
            var level = ava.GetPersonData(VMPersonDataVariable.TS1FameStarPower);

            if (promote)
            {
                // native: level==10 -> false; level>10 -> false; (levels 0..9 proceed)
                if (level < 0 || level >= 10) return VMPrimitiveExitCode.GOTO_FALSE;
                if (score < tables.Score[level]) return VMPrimitiveExitCode.GOTO_FALSE;
                // six-category skill gate: personData[cat] must reach req*100
                var reqs = tables.Skills[level];
                for (var i = 0; i < FameSkillCats.Length; i++)
                {
                    var req = reqs[i];
                    if (req > 0)
                    {
                        var points = ava.GetPersonData((VMPersonDataVariable)FameSkillCats[i]);
                        if (points < (short)(req * 100)) return VMPrimitiveExitCode.GOTO_FALSE;
                    }
                }
                // famous-friend gate: count * 0.5 >= friendsNeeded[level]
                if (CountFamousFriends(context, ava) * 0.5f < tables.Friends[level])
                    return VMPrimitiveExitCode.GOTO_FALSE;
                level++;
                ava.SetPersonData(VMPersonDataVariable.TS1FameStarPower, level);
                if (ava.GetPersonData(VMPersonDataVariable.TS1FameStarHighWatermark) < level)
                    ava.SetPersonData(VMPersonDataVariable.TS1FameStarHighWatermark, level);
                return VMPrimitiveExitCode.GOTO_TRUE;
            }
            else
            {
                // native: level==0 -> false; demote one step when score < table[level-1]
                if (level <= 0) return VMPrimitiveExitCode.GOTO_FALSE;
                if (score >= tables.Score[level - 1]) return VMPrimitiveExitCode.GOTO_FALSE;
                level--;
                ava.SetPersonData(VMPersonDataVariable.TS1FameStarPower, level);
                return VMPrimitiveExitCode.GOTO_TRUE;
            }
        }

        /// <summary>
        /// Famous-friend count for the promote gate. Residual (named above): the
        /// native's PersonFinder friendship filter is unreproduced; this counts
        /// OTHER on-lot avatars with fame score > 0 — the game's own fame-track
        /// predicate (Global #485 "on fame track" = person[56]==0 && person[80]>0).
        /// </summary>
        private static int CountFamousFriends(VMStackFrame context, VMAvatar self)
        {
            var count = 0;
            foreach (VMAvatar other in context.VM.Context.ObjectQueries.Avatars.ToList())
            {
                if (other == self || other.Dead) continue;
                if (other.GetPersonData(VMPersonDataVariable.TS1FameScore) > 0) count++;
            }
            return count;
        }
    }

    public class VMGenericTS1CallOperand : VMPrimitiveOperand
    {
        public VMGenericTS1CallMode Call;

        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes)
        {
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN))
            {
                Call = (VMGenericTS1CallMode)io.ReadByte();
            }
        }

        public void Write(byte[] bytes)
        {
            using (var io = new BinaryWriter(new MemoryStream(bytes)))
            {
                io.Write((byte)Call);
            }
        }
        #endregion
    }
}
