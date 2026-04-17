using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;

namespace SweepNDodge.DotsBullets.Tests
{
    public class SourceRuntimeTemplateAuthoringEditorSummaryUtilityTests
    {
        [Test]
        public void BuildRows_ComputesNominalRanges()
        {
            var rows = SourceRuntimeTemplateAuthoringEditorSummaryUtility.BuildRows(
                SourceStateId.Normal,
                2000,
                4000,
                null);

            Assert.That(rows.Length, Is.EqualTo(3));
            Assert.That(rows[0].State, Is.EqualTo(SourceStateId.Normal));
            Assert.That(rows[0].MinInclusive, Is.EqualTo(0));
            Assert.That(rows[0].MaxExclusive, Is.EqualTo(2000));
            Assert.That(rows[1].MinInclusive, Is.EqualTo(2000));
            Assert.That(rows[1].MaxExclusive, Is.EqualTo(4000));
            Assert.That(rows[2].MinInclusive, Is.EqualTo(4000));
            Assert.That(rows[2].MaxExclusive, Is.Null);
        }

        [Test]
        public void BuildRows_NormalizesNegativeAndInvertedThresholds()
        {
            var rows = SourceRuntimeTemplateAuthoringEditorSummaryUtility.BuildRows(
                SourceStateId.Weakened,
                -3,
                -7,
                null);

            Assert.That(rows[0].MinInclusive, Is.EqualTo(0));
            Assert.That(rows[0].MaxExclusive, Is.EqualTo(0));
            Assert.That(rows[1].MinInclusive, Is.EqualTo(0));
            Assert.That(rows[1].MaxExclusive, Is.EqualTo(0));
            Assert.That(rows[2].MinInclusive, Is.EqualTo(0));
            Assert.That(rows[2].MaxExclusive, Is.Null);

            rows = SourceRuntimeTemplateAuthoringEditorSummaryUtility.BuildRows(
                SourceStateId.Weakened,
                10,
                4,
                null);

            Assert.That(rows[0].MaxExclusive, Is.EqualTo(10));
            Assert.That(rows[1].MinInclusive, Is.EqualTo(10));
            Assert.That(rows[1].MaxExclusive, Is.EqualTo(10));
            Assert.That(rows[2].MinInclusive, Is.EqualTo(10));
        }

        [Test]
        public void BuildRows_KeepsZeroWidthWeakenedInterval()
        {
            var rows = SourceRuntimeTemplateAuthoringEditorSummaryUtility.BuildRows(
                SourceStateId.Depleted,
                5,
                5,
                null);

            Assert.That(rows[1].State, Is.EqualTo(SourceStateId.Weakened));
            Assert.That(rows[1].MinInclusive, Is.EqualTo(5));
            Assert.That(rows[1].MaxExclusive, Is.EqualTo(5));
            Assert.That(rows[1].RangeLabel, Is.EqualTo("[5, 5)"));
        }

        [Test]
        public void BuildRows_GroupsSlotsByLaneInAuthoredOrder()
        {
            var slots = new[]
            {
                new SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring
                {
                    State = SourceStateId.Weakened,
                    Lane = SourceSpawnLaneId.Hazard,
                },
                new SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring
                {
                    State = SourceStateId.Normal,
                    Lane = SourceSpawnLaneId.Trash,
                },
                new SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring
                {
                    State = SourceStateId.Weakened,
                    Lane = SourceSpawnLaneId.Hazard,
                },
                new SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring
                {
                    State = SourceStateId.Weakened,
                    Lane = SourceSpawnLaneId.Trash,
                },
            };

            var rows = SourceRuntimeTemplateAuthoringEditorSummaryUtility.BuildRows(
                SourceStateId.Normal,
                2,
                4,
                slots);

            Assert.That(rows[0].SlotSummary, Is.EqualTo("Trash: slot1"));
            Assert.That(rows[1].SlotSummary, Is.EqualTo("Hazard: slot0, slot2 | Trash: slot3"));
            Assert.That(rows[2].SlotSummary, Is.EqualTo("none"));
        }

        [Test]
        public void BuildRows_MarksInitialStateWithoutChangingSummary()
        {
            var slots = new[]
            {
                new SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring
                {
                    State = SourceStateId.Depleted,
                    Lane = SourceSpawnLaneId.Special,
                },
            };

            var rows = SourceRuntimeTemplateAuthoringEditorSummaryUtility.BuildRows(
                SourceStateId.Depleted,
                7,
                9,
                slots);

            Assert.That(rows[0].IsInitialState, Is.False);
            Assert.That(rows[1].IsInitialState, Is.False);
            Assert.That(rows[2].IsInitialState, Is.True);
            Assert.That(rows[2].SlotSummary, Is.EqualTo("Special: slot0"));
            Assert.That(rows[2].MinInclusive, Is.EqualTo(9));
        }
    }
}
