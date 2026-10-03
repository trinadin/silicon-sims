namespace FSO.SimAntics.Model
{
    public enum VMGenericTS1CallMode
    {
        HouseTutorialComplete = 0,
        SwapMyAndStackObjectsSlots = 1,
        SetActionIconToStackObject = 2,
        PullDownTaxiDialog = 3, // ENG-05: native = cSimsApp::RemoveTaxiDialog (0x24bdc0); no-window path returns FALSE (the addendum-corrected law)
        AddToFamily = 4,
        CombineAssetsOfFamilyInTemp0 = 5,
        RemoveFromFamily = 6,
        MakeNewNeighbor = 7, //this one is "depracated"
        FamilyTutorialComplete = 8,
        ArchitectureTutorialComplete = 9,
        DisableBuildBuy = 10,
        EnableBuildBuy = 11,
        GetDistanceToCameraInTemp0 = 12,
        AbortInteractions = 13, //abort all interactions associated with the stack object
        HouseRadioStationEqualsTemp0 = 14,
        MyRoutingFootprintEqualsTemp0 = 15, // ENG-05 (addendum-corrected): misnomer — native writes the STACK OBJECT's footprint-type field (+1564:=Temp0, +1566:=0) ALWAYS; recompute happens when Temp0 == 0 (not nonzero)
        ChangeNormalOutfit = 16, //changes the normal outfit of the sim to the next available suit — ENG-05: DEAD in the shipped corpus (0 real callers; law decoded 0x0f27b8)
        ChangeToLotInTemp0 = 17,
        BuildTheDowntownSimAndPlaceObjIDInTemp0 = 18, 
        SpawnDowntownDateOfPersonInTemp0 = 19, //temp0 is replaced with autofollow sim in temp0
        SpawnTakeBackHomeDataOfPersonInTemp0 = 20, //same side effect as above
        SpawnInventorySimDataEffects = 21,
        SelectDowntownLot = 22, //displays the selection screen and returns a lot in temp0 (simulation paused)
        GetDowntownTimeFromSOInventory = 23, //hours in temp0, minutes in temp1
        HotDateChangeSuitsPermanentlyCall = 24,
        SaveSimPersistentData = 25, //motives, relationships...
        BuildVacationFamilyPutFamilyNumInTemp0 = 26,
        ReturnNumberOfAvaiableVacationLotsInTemp0 = 27,
        ReturnZoningTypeOfLotInTemp0 = 28,
        SetStackObjectsSuit = 29, //suit type in temp0, suit index in temp1. Returns old index in temp1.
        GetStackObjectsSuit = 30, //suit type in temp0, suit index in temp1.
        CountStackObjectSuits = 31, //suit type in temp0, suit count returned in temp1.
        CreatePurchasedPetsNearOwner = 32, 
        AddToFamilyInTemp0 = 33,
        PromoteFameIfNeeded = 34,
        TakeTaxiHook = 35,
        DemoteFameIfNeeded = 36,
        CancelPieMenu = 37, // ENG-05: DEAD in the shipped corpus (vestigial existence-check body, 0 real callers)
        GetTokensFromString = 38,
        ChildToAdult = 39,
        PetToAdult = 40, // ENG-05 (addendum-corrected): NOT a stub — the pet-variant (flag 1, AnyoneToAdult) two-phase transform sharing mode 39's law
        HeadFlush = 41, // ENG-05: native no-body — table entry IS the return tail; r3=this!=0 → no-op TRUE
        MakeTemp0SelectedSim = 42,
        FamilySpellsIntoController = 43 // ENG-05: Family::LoadSpellsForFamily (0x75b70), current-family-gated
    }
}
