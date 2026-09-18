using System;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2ListViewStateTests
    {
        [Test]
        public void FreshStateIsEmptyNotLoadingNoError()
        {
            ListViewState<int> state = new ListViewState<int>();

            Assert.That(state.IsEmpty, Is.True);
            Assert.That(state.IsLoading, Is.False);
            Assert.That(state.ErrorMessage, Is.Null);
            Assert.That(state.HasMore, Is.False);
        }

        [Test]
        public void BeginLoadClearsAPreviousError()
        {
            ListViewState<int> state = new ListViewState<int>();
            state.Fail("network error");

            state.BeginLoad();

            Assert.That(state.IsLoading, Is.True);
            Assert.That(state.ErrorMessage, Is.Null);
        }

        [Test]
        public void ReplaceWithSetsItemsAndCursorAndClearsLoading()
        {
            ListViewState<int> state = new ListViewState<int>();
            state.BeginLoad();

            state.ReplaceWith(new[] { 1, 2, 3 }, "cursor-a");

            Assert.That(state.Items, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(state.NextCursor, Is.EqualTo("cursor-a"));
            Assert.That(state.HasMore, Is.True);
            Assert.That(state.IsLoading, Is.False);
        }

        [Test]
        public void AppendPageAddsToExistingItemsRatherThanReplacing()
        {
            ListViewState<int> state = new ListViewState<int>();
            state.ReplaceWith(new[] { 1, 2 }, "cursor-a");

            state.AppendPage(new[] { 3, 4 }, "cursor-b");

            Assert.That(state.Items, Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(state.NextCursor, Is.EqualTo("cursor-b"));
        }

        [Test]
        public void ANullCursorMeansNoMore()
        {
            ListViewState<int> state = new ListViewState<int>();
            state.ReplaceWith(new[] { 1 }, null);

            Assert.That(state.HasMore, Is.False);
        }

        [Test]
        public void FailSetsAnErrorAndClearsLoadingWithoutTouchingItems()
        {
            ListViewState<int> state = new ListViewState<int>();
            state.ReplaceWith(new[] { 1 }, null);

            state.Fail("could not reach the server");

            Assert.That(state.ErrorMessage, Is.EqualTo("could not reach the server"));
            Assert.That(state.IsLoading, Is.False);
            Assert.That(state.Items, Is.EqualTo(new[] { 1 }), "a failed refresh must not wipe out the last known list");
        }
    }

    public class Level5BackendV2RowCommandStateTests
    {
        [Test]
        public void ASecondClaimForTheSameRowIsRefusedUntilEnded()
        {
            RowCommandState state = new RowCommandState();
            Guid rowId = Guid.NewGuid();

            Assert.That(state.TryBegin(rowId), Is.True);
            Assert.That(state.IsInFlight(rowId), Is.True);
            Assert.That(state.TryBegin(rowId), Is.False, "a row command already in flight must refuse a second concurrent claim");

            state.End(rowId);

            Assert.That(state.IsInFlight(rowId), Is.False);
            Assert.That(state.TryBegin(rowId), Is.True);
        }

        [Test]
        public void DifferentRowsDoNotBlockEachOther()
        {
            RowCommandState state = new RowCommandState();
            Guid rowA = Guid.NewGuid();
            Guid rowB = Guid.NewGuid();

            Assert.That(state.TryBegin(rowA), Is.True);
            Assert.That(state.TryBegin(rowB), Is.True);
        }
    }

    public class Level5BackendV2ChallengeFormStateTests
    {
        [Test]
        public void TheSameClientRequestIdIsReusedAcrossARetryOfTheSameSubmission()
        {
            ChallengeFormState form = new ChallengeFormState();

            Guid first = form.GetOrBeginClientRequestId();
            Guid retried = form.GetOrBeginClientRequestId();

            Assert.That(retried, Is.EqualTo(first), "retrying the same logical create must reuse the same id");
        }

        [Test]
        public void CompletingTheSubmissionClearsTheIdSoTheNextOneIsNew()
        {
            ChallengeFormState form = new ChallengeFormState();
            Guid first = form.GetOrBeginClientRequestId();

            form.CompleteSubmission();
            Guid next = form.GetOrBeginClientRequestId();

            Assert.That(next, Is.Not.EqualTo(first), "a new logical create action must get a new id");
        }

        [Test]
        public void NoIdIsGeneratedUntilFirstRequested()
        {
            ChallengeFormState form = new ChallengeFormState();

            Assert.That(form.ClientRequestId, Is.Null);
        }

        [Test]
        public void DefaultsToBestOfThree()
        {
            ChallengeFormState form = new ChallengeFormState();

            Assert.That(form.TotalGames, Is.EqualTo(3));
        }
    }
}
