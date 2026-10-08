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
        // EXP-13 G6 probe surface (autotest-only reads; inert otherwise)
        public static string G6AddToFamilyGate = "";

        /// <summary>
        /// ENG-05 modes 39/40 in-flight transforms, keyed by the executing
        /// frame (see the Eng05Transform class below for the native law).
        /// </summary>
        public static readonly Dictionary<VMStackFrame, Eng05Transform> Eng05Transforms = new Dictionary<VMStackFrame, Eng05Transform>();

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
                // 3. PullDownTaxiDialog — ENG-05 decode (§mode-3): the native is
                // cSimsApp::RemoveTaxiDialog() (PPC 0x24bdc0), a VOID whose leftover
                // r3 encodes the outcome; with no taxi dialog open the last write is
                // lwz r3,116(this)=0 → FALSE. The 11 real callers (PedMarkers/Vacation/
                // MGBeanstalk/ClownPortal 'pump until gone' loops, all f=253) exit on
                // FALSE. The port has no taxi dialog UI, so the no-window path IS the
                // exact law; if a taxi screen ever mounts, the close-then-TRUE half
                // belongs to its UI controller.
                case VMGenericTS1CallMode.PullDownTaxiDialog: //3
                    return VMPrimitiveExitCode.GOTO_FALSE;
                case VMGenericTS1CallMode.AddToFamily: //4
                    // EXP-13 G6 probe surface: name the failing gate
                    G6AddToFamilyGate = (context.VM.TS1State.CurrentFamily == null) ? "family-null"
                        : (context.VM.TS1State.CurrentFamily.FamilyGUIDs.Length >= 8) ? "family-full" : null;
                    if (G6AddToFamilyGate != null)
                        return VMPrimitiveExitCode.GOTO_FALSE;
                    var fneigh = Content.Content.Get().Neighborhood.GetNeighborByID(context.StackObjectID);
                    if (fneigh == null) { G6AddToFamilyGate = "neighbor-null id=" + context.StackObjectID; return VMPrimitiveExitCode.GOTO_FALSE; }
                    G6AddToFamilyGate = "ok";
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
                    // ENG-05 decode + addendum (PPC 0x0f2754, polarity CORRECTED):
                    // writes the resolved STACK OBJECT's footprint-type field
                    // (+1564 := Temp0, +1566 := 0) ALWAYS; when Temp0 != 0 returns
                    // TRUE (r3 = Temp0) WITHOUT recomputing; when Temp0 == 0
                    // recomputes the tile rect (cXObject::ComputeRect) and returns
                    // its result. NOT the caller — the enum name is a misnomer.
                    // 202 corpus sites (sleep/sit/wash trees changing the object's
                    // blocking footprint). Port: the field is
                    // VMEntity.RoutingFootprintType; the rect recompute + obstacle
                    // re-register ride UpdateFootprint(); the type→mask selection is
                    // the named residual (the port recomputes from the object's
                    // current footprint mask).
                    var fpObj = context.StackObject;
                    if (fpObj == null) return VMPrimitiveExitCode.GOTO_FALSE; // resolver err-28 law
                    var fpType = context.Thread.TempRegisters[0];
                    fpObj.RoutingFootprintType = fpType;
                    if (fpType != 0) return VMPrimitiveExitCode.GOTO_TRUE; // r3 = Temp0 != 0
                    fpObj.UpdateFootprint(); // Temp0 == 0: ComputeRect + re-register
                    // P3-1 disclosure (review): the native returns ComputeRect's r3
                    // (undecoded) on this path; the port assumes success -> TRUE.
                    return VMPrimitiveExitCode.GOTO_TRUE;
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
                    // P3-1 (indep-review-mode26): the mode-26 twin — a null
                    // ActiveFamily or a failed create (unresolvable
                    // DowntownSimGUID) NREs below with the same
                    // entity-deletion consequence; soft-fail like mode 26
                    if (crossDataDT.ActiveFamily == null) return VMPrimitiveExitCode.GOTO_FALSE;

                    var control = context.VM.Context.CreateObjectInstance(crossDataDT.DowntownSimGUID, LotTilePos.OUT_OF_WORLD, Direction.NORTH)?.BaseObject;
                    if (control == null) return VMPrimitiveExitCode.GOTO_FALSE; // P3-1: the create-deref twin
                    ((VMAvatar)control).AvatarState.Permissions = Model.TSOPlatform.VMTSOAvatarPermissions.Owner;
                    context.VM.SetGlobalValue(3, control.ObjectID);
                    context.VM.SendCommand(new VMNetChangeControlCmd() { TargetID = control.ObjectID });
                    crossDataDT.ActiveFamily.SelectOneMember(crossDataDT.DowntownSimGUID);
                    context.VM.TS1State.ActivateFamily(context.VM, crossDataDT.ActiveFamily);

                    context.Thread.TempRegisters[0] = context.VM.GetGlobalValue(3);
                    if (VM.UseWorld) context.VM.Context.World.CenterTo((AvatarComponent)(context.VM.GetObjectById(context.VM.GetGlobalValue(3))?.WorldUI));
                    break;
                case VMGenericTS1CallMode.SpawnDowntownDateOfPersonInTemp0: //19
                    // spawn our autofollow sim. ENG-06: the token consumed is the
                    // NATIVE shape — generic inventory call 5/6 writes
                    // {Type 1|3, GUID = the neighbor's GUID, Count 1}; the reader
                    // takes the FIRST token of that type and spawns its GUID directly
                    // (the old magic-GUID-10/11 + nid-in-Count stand-in is retired
                    // with its writer).
                    var neighbourhood = Content.Content.Get().Neighborhood;
                    var ntarget = (VMAvatar)context.VM.GetObjectById(context.Thread.TempRegisters[0]);
                    context.Thread.TempRegisters[0] = -1;
                    if (ntarget == null) return VMPrimitiveExitCode.GOTO_FALSE; //vacation?
                    var neighbour = ntarget.GetPersonData(Model.VMPersonDataVariable.NeighborId);
                    var inventory = neighbourhood.GetInventoryByNID(neighbour);
                    if (inventory != null)
                    {
                        var invType = (inventoryInd == 11) ? (ushort)3 : (ushort)1;
                        var token = inventory.FirstOrDefault(x => x.Type == invType);
                        if (token != null)
                        {
                            var autofollow = context.VM.Context.CreateObjectInstance(token.GUID, LotTilePos.OUT_OF_WORLD, Direction.NORTH)?.BaseObject;
                            context.Thread.TempRegisters[0] = autofollow.ObjectID;
                            inventory.RemoveAll(x => x.Type == invType);
                        }
                    }
                    break;
                case VMGenericTS1CallMode.SpawnTakeBackHomeDataOfPersonInTemp0: //20
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
                    // EXP-07 spellbound decode: with no active family (direct
                    // lot loads / autotest boots), the PedMarkersMagic mains'
                    // per-tick family bootstrap NRE'd here (null ActiveFamily)
                    // and the suppressed-exception handler DELETED the ped
                    // markers at boot — killing every magic-lot marker before
                    // any content could scan for them. Natively unreachable
                    // (arrivals always carry a family); fail soft like the
                    // other unavailable-state modes.
                    if (crossData2.ActiveFamily == null) return VMPrimitiveExitCode.GOTO_FALSE;
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
                        // P3-4 (indep-review-mode26): an unresolvable DowntownSimGUID
                        // (a save whose sim template no longer resolves) makes the
                        // create return null — the same soft-fail as the family guard
                        if (control2 == null) return VMPrimitiveExitCode.GOTO_FALSE;
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
                        // TRV-02 (TRV-01 decode, native case 27; TRV-05 field
                        // correction): nine rentals (40..48); one is unavailable
                        // while a neighborhood family's VACATION-RENTAL reference
                        // (native Family+0x13C = FAMI.VacationHouseNumber —
                        // written at travel arrival by cSimsApp::LoadGame
                        // 0x1024f7f0, cleared on return by
                        // Neighborhood::RemoveFromVacation 0xa83e0) equals it.
                        // NOT HouseNumber (+0x110): the family's home lot stays
                        // home while it vacations, so counting homes never marks
                        // a rental occupied. Native writes the remainder of nine
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
                    if (context.VM.TS1State.CurrentFamily == null) return VMPrimitiveExitCode.GOTO_FALSE; // P3-2 (indep-review-mode26): nullable invariant (see VMTS1GlobalLinkStub.cs:66-74)
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
                case VMGenericTS1CallMode.GetTokensFromString:
                    return GetTokensFromString(context);
                // 39/40. ChildToAdult / PetToAdult — the shared two-phase transform
                // (ENG-05 decode + the return-contract addendum, PPC 0x0f3fb8 with
                // 0x0f3ff4 = BNE retranscribed): BOTH modes RequestTransform and
                // poll identically; the flag differs (0 = Neighborhood::ChildToAdult
                // = mode 39; 1 = Neighborhood::AnyoneToAdult = the pet variant,
                // mode 40 — NOT a stub; the first edition's FALSE reading was a
                // 0x4082/0x4182 transcription slip). Native contract: first visit
                // enqueues {a := Temp0, b := 0, flag} and returns 2 (yield — the
                // modal cWinTransformMeDlg); every re-entry polls
                // RetrieveDoneTransform: pending/no-match -> return 2; retrieved
                // state 2 (SUCCESS) -> Temp0 := out.a, Temp1 := out.b, return TRUE;
                // retrieved state 3 (DECLINED — e.g. the target is not on the lot)
                // -> Temp0 := out.a (unchanged), Temp1 := 0, ALSO TRUE (the failure
                // signal is Temp1 == 0, not the branch). Entries vanish after 100
                // unretrieved passes (the native wedge edge case — mirrored).
                // PORT timing law (ENG-16 update): the native enqueues the request
                // and converts asynchronously in cSimulator::ExecuteTransforms on a
                // LATER simulator tick — the port's cookie/poll shape IS that law:
                // the first visit enqueues and yields (CONTINUE_NEXT_TICK), the
                // conversion runs on the poll tick. ENG-07 divergence (a) is
                // satisfied (one-tick queue latency); (b) the wizard is UI-35's
                // picker; (c) the instance swap is ENG-14 below; (d) the RNG
                // stand-in (NextRandom for cRZRandom) stands — random is random,
                // no observable contract to mirror (ENG-16 receipt); (e) pets get
                // age 27 (not 30) since ENG-10.
                case VMGenericTS1CallMode.ChildToAdult: //39
                case VMGenericTS1CallMode.PetToAdult: //40
                {
                    Eng05Transform t39;
                    if (!Eng05Transforms.TryGetValue(context, out t39))
                    {
                        // first visit: enqueue + yield
                        t39 = new Eng05Transform
                        {
                            A = context.Thread.TempRegisters[0],
                            B = 0,
                            Flag = (byte)(operand.Call == VMGenericTS1CallMode.PetToAdult ? 1 : 0),
                            State = 0,
                        };
                        Eng05Transforms[context] = t39;
                        return VMPrimitiveExitCode.CONTINUE_NEXT_TICK; // native return 2
                    }
                    // poll: the port converts now (state 0 -> 2 or 3).
                    // ENG-07 decode (childtoadult-decode.md) — the contract CORRECTED:
                    // a (the request Temp0) = the target's VM OBJECT ID (both callers
                    // assign MyObject[ObjectId]/the stack-object id), NOT a neighbor id;
                    // success writes *a = 0 and *b = the replacement avatar's object id;
                    // the adult age word = 27 for person AND pet (the native CAS constant).
                    if (t39.State == 0)
                    {
                        var target39 = context.VM.GetObjectById(t39.A) as VMAvatar;
                        if (target39 == null || target39.Position == FSO.LotView.Model.LotTilePos.OUT_OF_WORLD)
                        {
                            // the not-found path: return 0 without touching a/b -> state 3
                            t39.State = 3;
                        }
                        else
                        {
                            // The native design session (BeginDesignAPerson ->
                            // SetGender/SetColor carry -> SetAge(27) -> RANDOM body+head
                            // suits from the available tables -> SetupNewHDSkins ->
                            // zero pd{10,11,12,15,17,18,56,57} -> SetName(old) ->
                            // EndDesign(commit)) mapped onto the port's surfaces. ENG-10
                            // added the pet leg below (species tables, suit-only writes).
                            // The native's whole-record swap (new character IFF file +
                            // selector GUID hot-swap + relationship carry) stays the named
                            // in-place divergence: the port rewrites the strings and
                            // re-saves the character IFF via AvatarChanged instead.
                            var bodyStr = target39.Object.Resource.Get<STR>(target39.Object.OBJ.BodyStringID);
                            // ENG-20: snapshot the touched slots — a failed create declines
                            // with the target ALIVE, and the writes below would otherwise
                            // leave a kid with an adult's strings (review N3, twice flagged)
                            var snap20 = new Dictionary<int, string>();
                            if (bodyStr != null)
                                foreach (var si20 in new[] { 0, 1, 2, 13, 14, 17, 18, 19, 20, 21, 22, 30, 31, 32, 33, 34 })
                                    snap20[si20] = bodyStr.GetString(si20);
                            if (bodyStr != null)
                            {
                                // [13] = the age word, person AND pet (the native
                                // adult age is 27 for both). [0] is written in the
                                // PERSON branch only: in this port STR#200[0] is the
                                // SPECIES discriminator (SetAvatarType maps
                                // "dog"/"kat" -> Dog/Cat and loads <[0]>.skel; the
                                // world ctor derives IsPet from [0] != adult/child;
                                // the generator writes {0, "dog"/"kat"}), and since
                                // AvatarChanged persists the character IFF, an
                                // "adult" write here would re-type the pet as an
                                // adult PERSON on the next lot load. Native law:
                                // the "adult" age-class write happens only for
                                // species 0 (childtoadult-decode §SetBodyString);
                                // ChangeSimBaseType §13 keeps the pet type codes.
                                bodyStr.SetString(13, "27");   // the age word
                                // ENG-10 pet leg (native ChangeSimBaseType config §13-14):
                                // a pet target rolls a random ADULT body suit from its
                                // species table and writes STR#200[1] ONLY — the head slot,
                                // handgroups and SetupNewHDSkins are person-only design
                                // steps. Species comes from the TARGET's gender bits
                                // (dog=8/cat=16): the native service is type-agnostic —
                                // mode 40's Flag is corroborating, not authoritative.
                                if (target39.IsDog || target39.IsCat)
                                {
                                    var petSuits = VMTS1PurchasableOutfitHelper.GetValidOutfits(null, (short)(target39.IsDog ? -1 : -2));
                                    if (petSuits.Length > 0)
                                    {
                                        var petInd = (int)(context.VM.Context.NextRandom((ulong)petSuits.Length) % (ulong)petSuits.Length);
                                        bodyStr.SetString(1, petSuits[petInd].Item1);
                                    }
                                }
                                else
                                {
                                bodyStr.SetString(0, "adult"); // [0] = the PERSON age class (species-gated — see above)
                                // ENG-07 review P2 fix (person leg): roll from the ADULT
                                // tables — GetValidOutfits/SetSuit key off the CURRENT [1]
                                // suit type, so a child target would roll a child suit. The
                                // roll mirrors make_new_character's creation pipeline with
                                // the ADULT simtype key ("fa"/"ma" — the base BCF roll
                                // table; make_new_character's derivation fixed to match,
                                // ENG-13), skin [14] carried over (the native SetColor
                                // carry), body AND head rolled, handgroups rebuilt
                                // (hand = the gender char for adults).
                                    Func<string, string> rmExt = (it) => { if (it == null) return null; var ix = it.LastIndexOf('.'); return (ix != -1) ? it.Substring(0, ix) : it; };
                                    Func<string, string, string> exID = (it, skn) => { var ix = it.IndexOf('_'); if (ix != -1) it = it.Substring(0, ix); return it + skn; };
                                    Func<string, string> findHG = (it) => { var ix = it.IndexOf(','); return (ix != -1) ? it.Substring(ix + 1) : ""; };
                                    var gender39 = target39.GetPersonData(VMPersonDataVariable.Gender) & 1;
                                    // the ADULT keys per the actual BCF tables ("fa"/"ma"; body
                                    // build variants fafit/faskn/fafat exist as sibling keys —
                                    // the base key is the roll table). make_new_character's
                                    // adult branch derived "fm"/"mm" until ENG-13 fixed it to
                                    // these same keys.
                                    var key39 = (gender39 > 0) ? "fa" : "ma";
                                    var skin39 = bodyStr.GetString(14);
                                    System.Collections.Generic.List<string> heads39, bodies39;
                                    var colC39 = Content.Content.Get().BCFGlobal.CollectionsByName["c"].ClothesByAvatarType;
                                    var colB39 = Content.Content.Get().BCFGlobal.CollectionsByName["b"].ClothesByAvatarType;
                                    if (!colC39.TryGetValue(key39, out heads39))
                                    {
                                        G6AddToFamilyGate = "eng07-headkey-miss:" + key39 + " avail=" + string.Join("|", colC39.Keys.Take(12));
                                        heads39 = null;
                                    }
                                    if (!colB39.TryGetValue(key39, out bodies39))
                                    {
                                        if (G6AddToFamilyGate == null || G6AddToFamilyGate.Length == 0) G6AddToFamilyGate = "";
                                        G6AddToFamilyGate = G6AddToFamilyGate + " eng07-bodykey-miss:" + key39 + " avail=" + string.Join("|", colB39.Keys.Take(12));
                                        bodies39 = null;
                                    }
                                    var texnames39 = ((FSO.Content.TS1.TS1AvatarTextureProvider)Content.Content.Get().AvatarTextures).GetAllNames();
                                    if (heads39 != null && bodies39 != null && heads39.Count > 0 && bodies39.Count > 0)
                                    {
                                        var headTex39 = heads39.Select(x => rmExt(texnames39.FirstOrDefault(y => y.StartsWith(exID(x, skin39))))).ToList();
                                        var bodyTex39 = bodies39.Select(x => rmExt(texnames39.FirstOrDefault(y => y.StartsWith(exID(x, skin39))))).ToList();
                                        var hgTex39 = bodies39.Select(x => (rmExt(texnames39.FirstOrDefault(y => y == "huao" + findHG(x))) ?? "huao" + skin39).Substring(4)).ToList();
                                        for (int i39 = headTex39.Count - 1; i39 >= 0; i39--) if (headTex39[i39] == null) { headTex39.RemoveAt(i39); heads39.RemoveAt(i39); }
                                        for (int i39 = bodyTex39.Count - 1; i39 >= 0; i39--) if (bodyTex39[i39] == null) { bodyTex39.RemoveAt(i39); bodies39.RemoveAt(i39); hgTex39.RemoveAt(i39); }
                                        if (bodies39.Count > 0 && heads39.Count > 0)
                                        {
                                            var bodyInd39 = (int)(context.VM.Context.NextRandom((ulong)bodies39.Count) % (ulong)bodies39.Count);
                                            var headInd39 = (int)(context.VM.Context.NextRandom((ulong)heads39.Count) % (ulong)heads39.Count);
                                            bodyStr.SetString(1, bodies39[bodyInd39] + ",BODY=" + bodyTex39[bodyInd39]);
                                            bodyStr.SetString(2, heads39[headInd39] + ",HEAD-HEAD=" + headTex39[headInd39]);
                                            var hand39 = key39[0];
                                            var hg39 = hgTex39[bodyInd39];
                                            bodyStr.SetString(17, "H" + hand39 + "LO,HAND=huao" + hg39);
                                            bodyStr.SetString(18, "H" + hand39 + "RO,HAND=huao" + hg39);
                                            bodyStr.SetString(19, "H" + hand39 + "LP,HAND=huao" + hg39);
                                            bodyStr.SetString(20, "H" + hand39 + "RP,HAND=huao" + hg39);
                                            bodyStr.SetString(21, "H" + hand39 + "LO,HAND=huao" + hg39);
                                            bodyStr.SetString(22, "H" + hand39 + "RC,HAND=huao" + hg39);
                                        }
                                    }
                                    // ENG-17 (wave 12): SetupNewHDSkins — the native's
                                    // post-roll skin-setup family (EditPerson::SetupNewSkins
                                    // + SetupFashionSkins + SetupVacationSkins, ENG-17 §4)
                                    // assigns best-fit AVAILABLE suits to the five expanded
                                    // outfit slots STR#200[30..34]. Port hookup: a table-backed
                                    // pick per outfit type from the same texture-filtered
                                    // lists the buy surface uses (GetValidOutfits t=1..5 —
                                    // the h table is the HD SkinsBuy.far set). The RNG-stand-in
                                    // law applies as for the daywear roll (the native's
                                    // RegExpBestFit scan has no port equivalent, disclosed).
                                    // Without this a grown adult keeps childhood slot values
                                    // (30/34 = the invalid "ADDED" literal; 31-33 = child-code
                                    // suits on an adult skeleton). Person targets only.
                                    for (short t17 = 1; t17 <= 5; t17++)
                                    {
                                        var slotSuits17 = VMTS1PurchasableOutfitHelper.GetValidOutfits(target39, t17);
                                        if (slotSuits17.Length > 0)
                                        {
                                            var slotInd17 = (int)(context.VM.Context.NextRandom((ulong)slotSuits17.Length) % (ulong)slotSuits17.Length);
                                            bodyStr.SetStringForce(VMTS1PurchasableOutfitHelper.OutfitTypeToInd[t17], slotSuits17[slotInd17].Item1);
                                        }
                                    }
                                }
                                target39.SetAvatarType(bodyStr);
                                target39.SetAvatarBodyStrings(bodyStr, context.VM.Context);
                                Content.Content.Get().Neighborhood.AvatarChanged(target39.Object.OBJ.GUID); // SetSuit's persist path no longer fires — call it directly
                            }
                            target39.SetPersonData(VMPersonDataVariable.PersonsAge, 27);
                            foreach (var z in new[] { 10, 11, 12, 15, 17, 18, 56, 57 })
                                target39.SetPersonData((VMPersonDataVariable)z, 0);
                            var nid39 = target39.GetPersonData(VMPersonDataVariable.NeighborId);
                            var rec39 = Content.Content.Get().Neighborhood.GetNeighborByID(nid39);
                            if (rec39 != null && rec39.PersonData != null && rec39.PersonData.Length > (int)VMPersonDataVariable.PersonsAge)
                            {
                                // ENG-14 (wave 12): carry the LIVE person data into the
                                // record first (native ChangeSimBaseType step 1 — the
                                // persistent-field snapshot; the avatar writes above have
                                // already landed age 27 + the zero-set on the live array),
                                // so the replacement instance inherits the full set.
                                for (int i39 = 0; i39 < rec39.PersonData.Length; i39++)
                                    rec39.PersonData[i39] = target39.GetPersonData((VMPersonDataVariable)i39);
                                rec39.PersonData[(int)VMPersonDataVariable.PersonsAge] = 27;
                                foreach (var z in new[] { 10, 11, 12, 15, 17, 18, 56, 57 })
                                    if (rec39.PersonData.Length > z) rec39.PersonData[z] = 0;
                            }
                            // ENG-14 (wave 12): the NATIVE INSTANCE SWAP (ENG-07
                            // §ChangeSimBaseType steps 9/16 + §divergence (c) — now
                            // resolved): snapshot the placement, kill the old instance,
                            // realize the replacement from the same (already-updated)
                            // character resource, inherit the record, re-home the thread
                            // on a self-cast (the native trees re-bind StackObjectID :=
                            // Temp[1] themselves, e.g. CharmsKid 4111 ins7), snap to the
                            // saved placement (the TrySnap law), and report the NEW
                            // object id as *b. The pet's native new-character-IFF FILE
                            // maps to the port's AvatarChanged character-IFF re-save
                            // above (same persisted state; id/GUID/relationships already
                            // preserved in place). The native's SetSimFlag(obj,1,false)
                            // has no port consumer (ENG-17 verification).
                            var oldPos39 = target39.Position;
                            var oldRadDir39 = target39.RadianDirection;

                            // collect the live pd BEFORE the kill (native step 7's
                            // vector) — for record-less targets (no NBR record: casual
                            // instances) the carry applies directly; the native would
                            // decline those (its SEARCH 2 requires a record), the port's
                            // acceptance predates — keep it, disclose the carry.
                            var livePd39 = new short[rec39?.PersonData.Length ?? 80];
                            for (int i39 = 0; i39 < livePd39.Length; i39++)
                                livePd39[i39] = target39.GetPersonData((VMPersonDataVariable)i39);
                            livePd39[(int)VMPersonDataVariable.PersonsAge] = 27;
                            foreach (var z in new[] { 10, 11, 12, 15, 17, 18, 56, 57 })
                                if (livePd39.Length > z) livePd39[z] = 0;
                            // wave-12 review P1-2/P2-1 CORRECTED: create the replacement
                            // FIRST (the native order is kill-then-realize, but the port
                            // defers mid-frame kills — and a kill-then-failed-create
                            // stranded the executing tree on a dead entity with no
                            // re-bind path). Create-first eliminates the decline-after-
                            // kill state: a failed create declines BEFORE the kill, the
                            // native's own decline shape.
                            VMAvatar new39 = null;
                            try
                            {
                                var grp39 = context.VM.Context.CreateObjectInstance(target39.Object.OBJ.GUID,
                                    FSO.LotView.Model.LotTilePos.OUT_OF_WORLD, FSO.LotView.Model.Direction.NORTH);
                                new39 = grp39?.Objects?.FirstOrDefault() as VMAvatar;
                            }
                            catch { }
                            if (new39 == null)
                            {
                                // the native's engine-failed path (AnyoneToAdult zeroes
                                // *b and returns 0) — state 3 declines the same way,
                                // with the target STILL ALIVE (pre-kill, native shape).
                                // ENG-20: restore the snapshot so the living target keeps
                                // its own strings (the writes above must not persist)
                                if (bodyStr != null)
                                {
                                    foreach (var sv20 in snap20)
                                        if (sv20.Value != null) bodyStr.SetString(sv20.Key, sv20.Value);
                                    target39.SetAvatarType(bodyStr);
                                    target39.SetAvatarBodyStrings(bodyStr, context.VM.Context);
                                    Content.Content.Get().Neighborhood.AvatarChanged(target39.Object.OBJ.GUID);
                                }
                                t39.State = 3;
                            }
                            else
                            {
                                target39.Delete(false, context.VM.Context); // native step 9: kill the old instance (full cleanup; mid-frame it queues)
                                if (rec39 != null) new39.InheritNeighbor(rec39, context.VM.TS1State.CurrentFamily);
                                else for (int i39 = 0; i39 < livePd39.Length; i39++)
                                    new39.SetPersonData((VMPersonDataVariable)i39, livePd39[i39]);
                                new39.RadianDirection = oldRadDir39;
                                // the TrySnap law: the exact saved spot, else the nearest
                                // adjacent (the native's TrySnap falls back the same way —
                                // and mid-frame kills free the tile only at frame end)
                                var pr39 = new FSO.SimAntics.VMPlacementResult(FSO.SimAntics.Model.VMPlacementError.LocationOutOfBounds);
                                try { pr39 = new39.SetPosition(oldPos39, new39.Direction, context.VM.Context, FSO.SimAntics.Model.VMPlaceRequestFlags.Default); } catch { }
                                if (pr39.Status != FSO.SimAntics.Model.VMPlacementError.Success)
                                {
                                    try { VMFindLocationFor.FindLocationFor(new39, target39, context.VM.Context, FSO.SimAntics.Model.VMPlaceRequestFlags.Default); } catch { }
                                }
                                // ENG-19 (wave 13): the SELF-CAST re-home, now REAL —
                                // VMThread.RebindEntity swaps the private binding (the
                                // scheduler keys on entities, so the binding IS the
                                // mechanism): the caller's thread — this tree, its queue,
                                // its Main — continues on the replacement, and a yield
                                // AFTER the swap resumes there (the 4211 dialog casualty
                                // closed). The replacement's own init thread is dropped
                                // (unreferenced; never ticks). Non-self casts never touch
                                // threads (the target's tree is not executing here).
                                var oldThread19 = target39.Thread;
                                if (context.Caller != null && context.Caller == target39 && oldThread19 != null)
                                {
                                    oldThread19.RebindEntity(new39);
                                }
                                t39.B = (short)new39.ObjectID; // *b = the NEW object id (the native's fresh instance)
                                t39.A2 = 0; // *a = 0 on success
                                t39.State = 2;
                            }
                        }
                    }
                    if (++t39.Polls >= 100) { Eng05Transforms.Remove(context); return VMPrimitiveExitCode.CONTINUE_NEXT_TICK; } // the native's stale-entry erase -> poll-no-match -> yield forever
                    if (t39.State == 0) return VMPrimitiveExitCode.CONTINUE_NEXT_TICK; // still busy (not reachable port-side; kept for law-shape)
                    context.Thread.TempRegisters[0] = (t39.State == 3) ? t39.A : t39.A2; // declined: a untouched; success: *a = 0
                    context.Thread.TempRegisters[1] = (t39.State == 3) ? (short)0 : t39.B;
                    Eng05Transforms.Remove(context); // the native's retrieve erases the entry
                    return VMPrimitiveExitCode.GOTO_TRUE; // state 2 AND state 3 both take the TRUE branch
                }
                // 41. HeadFlush — ENG-05 decode (§mode-41): the native table entry
                // IS the shared return tail (0x0f411c); r3 = this != 0 → a no-op
                // TRUE. All 10 real callers (Karaoke/OpenMic 'Watch',
                // SpellGhostMe/SpellTransform) are satisfied by the port's existing
                // fall-through GOTO_TRUE — already exact, comment-only change.
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
                case VMGenericTS1CallMode.FamilySpellsIntoController: //43
                    // ENG-05 decode (§mode-43) + ENG-08's full decode
                    // (familyspells-decode.md): native =
                    // Family::LoadSpellsForFamily() (0x75b70), gated on the CURRENT
                    // family — a 6-word one-way max-merge of the family's learned
                    // spell bitmaps (Family+334..344) into the magic controller's
                    // attrs 0-5. There is NO spell list and the spellbook lists all
                    // rows natively (learned bits gate CASTING via tree 4101, which
                    // runs in the port). ENG-12 added the port's FAMI spell block:
                    // when the block exists (latch set), mirror §1 exactly —
                    // max-merge into the live 0xB6C90029 controller's attrs 0-5.
                    // Families without a block (every original-format FAMI and
                    // spell-less port families) keep the latch-only no-op law, which
                    // is byte-faithful (native latch=0 ⇒ return TRUE).
                    // ENG-15 (wave-12 review P1-1 CORRECTED): the native law is the
                    // CONVERSE of my first landing — Family::LoadSpellsForFamily
                    // 0x6cd2c-34: `GetZoningType(lot); cmpwi 1; bne epilogue` — the
                    // six-word merge runs ONLY ON COMMUNITY LOTS (zoning == 1; the
                    // magic-town lots 93-98/54 are community — the merge fires exactly
                    // when the family ENTERS the magic lot, where the controller
                    // lives). Missing key ⇒ GetZoningType 0 ⇒ skip (both sides).
                    // The latch+TRUE law holds on the skip either way.
                    var spellsFam = context.VM.TS1State.CurrentFamily;
                    if (spellsFam == null) return VMPrimitiveExitCode.GOTO_FALSE;
                    var zoning43 = Content.Content.Get().Neighborhood.GetZoningType(context.VM.TS1State.CurrentHouse);
                    if (spellsFam.SpellWords != null && zoning43 == 1)
                    {
                        var spellsCtl = context.VM.Entities.FirstOrDefault(e => e.Object?.OBJ?.GUID == 0xB6C90029u);
                        if (spellsCtl != null)
                        {
                            for (short k = 0; k < 6; k++)
                            {
                                var live43 = spellsCtl.GetAttribute(k);
                                var blk43 = (spellsFam.SpellWords.Length > k) ? spellsFam.SpellWords[k] : (short)0;
                                if (blk43 > live43) spellsCtl.SetAttribute(k, blk43);
                            }
                        }
                    }
                    context.VM.TS1State.FamilySpellsLoadedFor = spellsFam.ChunkID;
                    return VMPrimitiveExitCode.GOTO_TRUE;
            }
            return VMPrimitiveExitCode.GOTO_TRUE;
        }

        /// <summary>
        /// EXP-07 (MM recipe scan): generic TS1 call 38 GetTokensFromString.
        /// Contract CORRECTED per the independent review
        /// (indep-review-mode38-20260927.md — REJECT of the first attempt,
        /// which mis-decoded a PersonData surface): every consumer
        /// (MagicMasterSpells 4104 'Get Recipe From Ingredients', MMS 4103
        /// 'Get Ingredients for PressOven', MMS 4098 'Get Spell From
        /// Ingredients', MMS 4099 'Get Ingredients for Spell' (fourth
        /// consumer, per the v2 review); press/oven tables STR#5/STR#6,
        /// spell tables STR#3/STR#4) drives a STATELESS Temp protocol:
        ///   - inputs: Temp[0] = STR# table id, Temp[1] = entry index
        ///     (direct array index — entry 0 is a 'not used' sentinel; the
        ///     tree owns the counter and re-feeds it each iteration),
        ///   - outputs: Temp[1..n] = comma-split tokens, Temp[0] = token
        ///     count on success, -1 when the index is past the table
        ///     (the trees test Temp[0] == -1 to end the scan, and
        ///     Temp[0] != 3 to skip short entries).
        /// No PersonData, no scope-19 — the stack object is the Magic
        /// Controller, a VMGameObject (the review's finding 2: the cast
        /// throws and RunInMyStack's empty catch converts it into a stale
        /// TRUE exit — the first implementation's runs were a false green).
        /// The tree reads the matched entry index from Temp[0] after TRUE
        /// (4104 @15) and maps it to the product (NectarPress 4103
        /// @71-@73/@40: entries 1..3 -> Tuning[512..514], else Tuning[515]).
        /// Runtime probe: unl-magic5 run 12 stocks (2,2,25) expecting
        /// matched entry 1 -> product 1 (Tuning[512]) — a case the stale-
        /// TRUE artifact path cannot produce (it always defaults to 4).
        /// </summary>
        private static VMPrimitiveExitCode GetTokensFromString(VMStackFrame context)
        {
            var so = context.StackObject;
            if (so == null) return VMPrimitiveExitCode.GOTO_FALSE;
            var res = so.Object?.Resource;
            if (res == null) return VMPrimitiveExitCode.GOTO_FALSE;
            var temps = context.Thread.TempRegisters;
            var table = res.Get<FSO.Files.Formats.IFF.Chunks.STR>((ushort)temps[0]);
            if (table == null) return VMPrimitiveExitCode.GOTO_FALSE;
            var str = table.GetString(temps[1]);
            if (str == null)
            {
                temps[0] = -1; // past the table: the caller's scan-exit marker
                return VMPrimitiveExitCode.GOTO_TRUE;
            }
            var parts = str.Split(',');
            short n = 0;
            for (int i = 0; i < parts.Length && i + 1 < temps.Length; i++)
            {
                short v;
                if (short.TryParse(parts[i].Trim(), out v))
                {
                    temps[i + 1] = v;
                    n++;
                }
            }
            temps[0] = n;
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
        /// TRV-02 (native case 27; TRV-05 field correction): vacation rentals
        /// 40..48 are unavailable while a neighborhood family's vacation-rental
        /// reference (native Family+0x13C = FAMI.VacationHouseNumber) equals
        /// them; the remainder of nine is the bookable count.
        /// </summary>
        public static int CountOccupiedVacationLots(IEnumerable<FAMI> families)
        {
            if (families == null) return 0;
            return families.Count(f => f != null && f.VacationHouseNumber >= 40 && f.VacationHouseNumber <= 48);
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

    /// <summary>
    /// ENG-05 modes 39/40: the in-flight transform request keyed by the
    /// executing frame (the native queues it on the simulator keyed by the
    /// StackElem request id; entries are erased on retrieval or after 100
    /// unretrieved passes). Static per process — TS1 runs one VM.
    /// </summary>
    public class Eng05Transform
    {
        public short A, B;
        public short A2;    // ENG-07: the SUCCESS-path *a out (the native zeroes it)
        public byte Flag;   // 0 = ChildToAdult (mode 39), 1 = AnyoneToAdult/pet (mode 40)
        public byte State;  // 0 pending, 2 success, 3 declined
        public int Polls;
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
