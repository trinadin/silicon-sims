using System;
using System.Collections.Generic;
using FSO.SimAntics.Model;

namespace Simitone.Client.Autotest
{
    /// <summary>
    /// UI-27 (r261-relgrade law) additive gate pins. Kept in this separate
    /// file so sibling worktrees' AutotestRunner.cs edits cannot collide; the
    /// runner hooks are one-line delegations inside CheckUIRel/CheckUIJob.
    ///
    /// (a) Relationship filter truth table (GetRelatedPeople 0x242360):
    /// mode 1 Friends membership is GetRelation's COMPUTED mutual RelMatrix
    /// slot-0 >= 50 law (forward AND reverse, raw pre-clamp values), mode 2
    /// Famous membership is the persisted neighbor word 81
    /// (TS1FameStarPower) != 0 test with NO in-world requirement, mode 0
    /// Family is the word-61 equality pair, mode >= 3 accepts all.
    ///
    /// (b) Report-card grade pins (cWinSubpanelReportCard::TSPaint 0x2a63d0):
    /// value >= 10 flashes the grade button at the engine's 333 ms tick, the
    /// GetCareer == 0 path blanks, and letter-at-value&lt;10 remains the
    /// work.iff STR#4097 table (pinned live by the pre-existing uijob
    /// gradeCanon block, which must hold alongside these rows).
    /// </summary>
    public static class RelGrade27Gates
    {
        private static List<short> Row(params short[] values)
        {
            return new List<short>(values);
        }

        /// <summary>GetRelatedPeople mode law truth table (r261 §1.2/§1.5).</summary>
        public static bool RelFilterTruthRows()
        {
            var t = Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.FriendThreshold;

            // Mode 1 — the synthetic mutual rows the law doc pins: (0,49),
            // (50,50), (60,40), (100,100) classify exactly; one-directional
            // >= 50 must NOT pass; null/empty rows never pass.
            bool friend = t == 50
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(Row(0), Row(49))
                && Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(Row(50), Row(50))
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(Row(60), Row(40))
                && Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(Row(100), Row(100))
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(Row(49), Row(100))
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(Row(100), Row(49))
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(Row(100), Row(-100))
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(null, Row(50))
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(Row(50), null)
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFriend(new List<short>(), Row(50));

            // Marker law: the smiley (bit 0) is the same mutual class — it
            // starves without the reverse row — while heart (bit 1, forward
            // slot 1) and deep-heart (bit 2, forward slot 3) stay forward-only.
            bool markers = Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.MarkerFlagsFor(Row(75), Row(75)) == 1
                && Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.MarkerFlagsFor(Row(75, 1), Row(75)) == 3
                && Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.MarkerFlagsFor(Row(75, 1, 0, 1), Row(75)) == 7
                && Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.MarkerFlagsFor(Row(49, 1, 0, 1), Row(100)) == 6
                && Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.MarkerFlagsFor(Row(100, 1, 0, 1), Row(49)) == 6;

            // Mode 2 — persisted PD81 alone; there is no resolved/in-world
            // parameter left to fail (the old (false, 10) row is unrepresentable).
            bool famous = Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFamous(1)
                && Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFamous(-1)
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsNativeFamous(0);

            // Mode 0 — family word-61 equality pair (mismatch and missing
            // word rows reject).
            int familyWord = (int)VMPersonDataVariable.TS1FamilyNumber;
            var sourceFamily = new List<short>(new short[familyWord + 1]);
            var sameFamily = new List<short>(new short[familyWord + 1]);
            var otherFamily = new List<short>(new short[familyWord + 1]);
            sourceFamily[familyWord] = sameFamily[familyWord] = 12;
            otherFamily[familyWord] = 13;
            bool family = Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.SameFamily(sourceFamily, sameFamily)
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.SameFamily(sourceFamily, otherFamily)
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.SameFamily(sourceFamily, new List<short>());

            // IsMutualFriend stays the compatible alias of the mutual law.
            bool alias = Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsMutualFriend(Row(50), Row(50))
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIRelationshipSubpanel.IsMutualFriend(Row(50), Row(49));

            return friend && markers && famous && family && alias;
        }

        /// <summary>Report-card flash + career-blank pins (r261 §2.1/§2.4).</summary>
        public static bool GradeLetterPins()
        {
            bool consts = Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashThreshold == 10
                && Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashPeriodMs == 333;

            // cmpwi r30,10 / blt around the flash branch: value >= 10 flashes.
            bool flash = !Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashes(-1)
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashes(0)
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashes(9)
                && Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashes(10)
                && Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashes(15);

            // 333 ms on / 333 ms off phase boundaries from the engine literal
            // (333, 333, 0); not-flashing is always visible (clear 0x51c160).
            bool phase = Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashVisible(false, 0)
                && Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashVisible(false, 999999)
                && Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashVisible(true, 0)
                && Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashVisible(true, 332)
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashVisible(true, 333)
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashVisible(true, 665)
                && Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashVisible(true, 666)
                && !Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.GradeFlashVisible(true, 999);

            // GetCareer == 0 draws nothing: blank at career 0 for every grade
            // value (the letter-at-value<10 rows are pinned live against
            // STR#4097 by uijob's gradeCanon).
            bool blank = Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.ReportGradeText(null, 0) == ""
                && Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.ReportGradeText(null, 9) == ""
                && Simitone.Client.UI.Panels.LiveSubpanels.UIJobSubpanel.ReportGradeText(null, 15) == "";

            return consts && flash && phase && blank;
        }
    }
}
