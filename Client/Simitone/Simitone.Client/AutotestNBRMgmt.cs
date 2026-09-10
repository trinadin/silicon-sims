using FSO.Content.TS1;
using FSO.Files.Formats.IFF.Chunks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// NBR-02 opt-in pin ("nbrmgmt"): exercises the neighborhood create/select and
    /// move-out/eviction backend law on a scratch TS1NeighborhoodProvider mounted
    /// over the private userdir, so the live lot VM is never unmounted mid-run.
    /// Assertions mirror NBR-01's decoded contracts: enumeration
    /// (Neighborhood::CheckForNewUserDataDirectories), the live neighborhood number
    /// (GetCurrentNeighborhoodNumber), switch-with-save
    /// (SwitchToNewNeighborhood/Next/Previous), the MoveOut law (-1 when the lot is
    /// vacant, else 1; net-worth liquidation into the family budget; killSims
    /// member/character/inventory removal; membership emptied) and reopen
    /// consistency across a save/remount and a switch round-trip.
    /// Mutations land only in the private userdir; the shared corpus is untouched.
    /// </summary>
    public static class AutotestNBRMgmt
    {
        private static bool All = true;

        private static void Check(bool cond, string what)
        {
            All &= cond;
            Console.WriteLine("AUTOTEST nbrmgmt: " + (cond ? "ok   " : "BAD  ") + what);
        }

        public static bool Run()
        {
            var content = FSO.Content.Content.Get();
            var scratch = new TS1NeighborhoodProvider(content);

            Check(scratch.CurrentNeighborhoodID == 0, "initial mount is neighborhood 0");
            var avail0 = scratch.GetAvailableNeighborhoods();
            Check(avail0.Count > 0 && avail0[0] == 0, "enumeration lists neighborhood 0 (" + avail0.Count + " mounted on this userdir)");

            // --- MoveOut law on the scratch mount ---
            // pick the highest occupied house that is not the live lot house (5),
            // so the VM's own family is never touched
            var house = scratch.FamilyForHouse.Keys.Where(x => x != 5).OrderByDescending(x => x).FirstOrDefault();
            var fam = (house != 0) ? scratch.GetFamilyForHouse(house) : null;
            Check(fam != null, "scratch neighborhood has an occupant to evict (house " + house + ")");

            if (fam != null)
            {
                Check(scratch.MoveOut(9999, true) == -1, "MoveOut on a vacant lot returns -1");

                var archBefore = fam.ValueInArch;
                var budgetBefore = fam.Budget;
                var memberNIDs = new List<short>();
                var memberFiles = new List<string>();
                foreach (var guid in fam.FamilyGUIDs)
                {
                    var nid = scratch.GetNeighborIDForGUID(guid);
                    if (nid != null)
                    {
                        memberNIDs.Add(nid.Value);
                        var entries = ((TS1ObjectProvider)content.WorldObjects).Entries;
                        if (entries.TryGetValue(guid, out var entry) && !string.IsNullOrEmpty(entry.FileName))
                            memberFiles.Add(entry.FileName);
                    }
                }
                Check(memberNIDs.Count > 0, "family has member records to purge (" + memberNIDs.Count + ")");

                Check(scratch.MoveOut(house, true) == 1, "MoveOut on an occupied lot returns 1");
                Check(!scratch.FamilyForHouse.ContainsKey(house), "house binding removed from the family-for-house map");
                Check(fam.HouseNumber == 0, "FAMI house number cleared");
                Check(fam.Budget == budgetBefore + archBefore && fam.ValueInArch == 0, "net worth liquidated into the family budget");
                Check(fam.FamilyGUIDs.Length == 0, "family membership emptied (killSims)");
                Check(memberNIDs.All(nid => scratch.GetNeighborByID(nid) == null), "member neighbour records removed");
                Check(memberNIDs.All(nid => !scratch.Neighborhood.InventoryByID.ContainsKey(nid)), "member inventories removed");
                Check(memberFiles.All(f => !File.Exists(f)), "member character files deleted (" + memberFiles.Count + " files)");
                Check(scratch.MoveOut(house, true) == -1, "re-evicting the now-vacant lot returns -1");

                // reopen consistency: persist, remount from disk, re-assert
                scratch.SaveNeighbourhood(false);
                scratch.InitSpecific(0);
                Check(!scratch.FamilyForHouse.ContainsKey(house), "eviction survives a remount (reopen consistency)");
                Check(memberNIDs.All(nid => scratch.GetNeighborByID(nid) == null), "member removal survives a remount");
            }

            // --- switch/create law: round-trip into the UserData2 template ---
            var famsBefore = scratch.MainResource.List<FAMI>()?.Count ?? -1;
            Check(scratch.SwitchToNeighborhood(1), "switch to neighborhood 1 (materializes UserData2 from the install template)");
            Check(scratch.CurrentNeighborhoodID == 1, "live neighborhood number follows the switch");
            var avail1 = scratch.GetAvailableNeighborhoods();
            Check(avail1.Contains(0) && avail1.Contains(1), "enumeration now lists both mounted neighborhoods");
            Check(scratch.MainResource != null && scratch.MainResource.List<FAMI>() != null, "switched neighborhood parsed (FAMI readable)");
            Check(scratch.SwitchToNeighborhood(999) == false, "switch to a never-materialized neighborhood fails bounded");
            Check(scratch.SwitchToNeighborhood(0), "switch back to neighborhood 0");
            Check(scratch.CurrentNeighborhoodID == 0, "live neighborhood number follows the return");
            Check((scratch.MainResource.List<FAMI>()?.Count ?? -1) == famsBefore, "family count on neighborhood 0 unchanged after the round-trip");
            if (fam != null) Check(!scratch.FamilyForHouse.ContainsKey(house), "eviction state survives the switch round-trip");

            return All;
        }
    }
}
