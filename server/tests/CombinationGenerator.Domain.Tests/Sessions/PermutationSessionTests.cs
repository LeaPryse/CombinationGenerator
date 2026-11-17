using System.Diagnostics;
using CombinationGenerator.Domain.Exceptions;
using CombinationGenerator.Domain.Permutations;
using CombinationGenerator.Domain.Sessions;

namespace CombinationGenerator.Domain.Tests.Sessions;

public class PermutationSessionTests
{
    private static readonly int[][] N3Sequence =
    [
        [1, 2, 3],
        [1, 3, 2],
        [2, 1, 3],
        [2, 3, 1],
        [3, 1, 2],
        [3, 2, 1],
    ];

    // ---- Creation -------------------------------------------------------

    [Fact]
    public void Create_ValidN_StartsBeforeFirstPermutation()
    {
        var session = PermutationSession.Create(3);

        session.Id.Should().NotBeEmpty();
        session.N.Should().Be(3);
        session.TotalCount.Should().Be(6);
        session.CurrentIndex.Should().Be(-1);
        session.NextIndex.Should().Be(0);
        session.HasMore.Should().BeTrue();
        session.Current.Should().BeNull();
        session.PagingAnchor.Should().BeNull();
    }

    [Fact]
    public void Create_N20_TotalCountIs20Factorial()
    {
        var session = PermutationSession.Create(20);

        session.TotalCount.Should().Be(2_432_902_008_176_640_000L);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Create_InvalidN_ThrowsArgumentOutOfRange(int n)
    {
        var act = () => PermutationSession.Create(n);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("n");
    }

    [Fact]
    public void Constructor_EmptyId_ThrowsArgumentException()
    {
        var act = () => new PermutationSession(Guid.Empty, 3);

        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void Constructor_GivenId_KeepsId()
    {
        var id = Guid.NewGuid();

        var session = new PermutationSession(id, 4);

        session.Id.Should().Be(id);
    }

    // ---- MoveNext -------------------------------------------------------

    [Fact]
    public void MoveNext_FirstCall_ReturnsIndexZeroIdentity()
    {
        var session = PermutationSession.Create(3);

        var result = session.MoveNext();

        result.IsExhausted.Should().BeFalse();
        result.Item!.Index.Should().Be(0);
        result.Item.Values.Should().Equal(1, 2, 3);
        result.HasMore.Should().BeTrue();
        session.CurrentIndex.Should().Be(0);
        session.Current.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void MoveNext_N3_ReturnsFullLexicographicSequenceWithHasMoreFalseOnLast()
    {
        var session = PermutationSession.Create(3);

        var results = Enumerable.Range(0, 6).Select(_ => session.MoveNext()).ToList();

        results.Select(r => r.Item!.Values).Should().BeEquivalentTo(N3Sequence, o => o.WithStrictOrdering());
        results.Select(r => r.Item!.Index).Should().Equal(0, 1, 2, 3, 4, 5);
        results.Take(5).Should().OnlyContain(r => r.HasMore);
        results[5].HasMore.Should().BeFalse();
    }

    [Fact]
    public void MoveNext_AfterLast_ReturnsExhaustedAndKeepsPosition()
    {
        var session = PermutationSession.Create(3);
        for (var i = 0; i < 6; i++)
        {
            session.MoveNext();
        }

        var first = session.MoveNext();
        var second = session.MoveNext();

        first.IsExhausted.Should().BeTrue();
        first.Item.Should().BeNull();
        first.HasMore.Should().BeFalse();
        second.IsExhausted.Should().BeTrue();
        session.CurrentIndex.Should().Be(5);
        session.Current.Should().Equal(3, 2, 1);
    }

    [Fact]
    public void MoveNext_N1_ReturnsSinglePermutationThenExhausted()
    {
        var session = PermutationSession.Create(1);

        var first = session.MoveNext();
        var second = session.MoveNext();

        first.Item!.Values.Should().Equal(1);
        first.HasMore.Should().BeFalse();
        second.IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void MoveNext_ReturnedValues_AreIndependentCopies()
    {
        var session = PermutationSession.Create(3);
        var first = session.MoveNext();

        first.Item!.Values[0] = 99;
        var second = session.MoveNext();

        second.Item!.Values.Should().Equal(1, 3, 2);
        session.Current.Should().Equal(1, 3, 2);
    }

    // ---- GetPage: anchoring ---------------------------------------------

    [Fact]
    public void GetPage_AfterTwoNexts_Page1StartsAtIndexTwo()
    {
        var session = PermutationSession.Create(3);
        session.MoveNext();
        session.MoveNext();

        var page = session.GetPage(1, 2);

        page.Items.Select(i => i.Index).Should().Equal(2, 3);
        page.Items.Select(i => i.Values).Should().BeEquivalentTo(new[] { N3Sequence[2], N3Sequence[3] }, o => o.WithStrictOrdering());
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(2);
        page.TotalItems.Should().Be(4);
        page.TotalPages.Should().Be(2);
        page.FirstIndex.Should().Be(2);
        session.PagingAnchor.Should().Be(2);
    }

    [Fact]
    public void GetPage_RightAfterCreate_Page1StartsAtIndexZero()
    {
        var session = PermutationSession.Create(3);

        var page = session.GetPage(1, 4);

        page.Items.Select(i => i.Index).Should().Equal(0, 1, 2, 3);
        page.Items[0].Values.Should().Equal(1, 2, 3);
        page.TotalItems.Should().Be(6);
        page.TotalPages.Should().Be(2);
        page.FirstIndex.Should().Be(0);
    }

    [Fact]
    public void GetPage_SubsequentPages_KeepAnchorFixed()
    {
        var session = PermutationSession.Create(3);
        session.MoveNext();
        session.MoveNext();
        session.GetPage(1, 2);

        var page2 = session.GetPage(2, 2);
        var page1Again = session.GetPage(1, 2);

        page2.Items.Select(i => i.Index).Should().Equal(4, 5);
        page2.Items.Select(i => i.Values).Should().BeEquivalentTo(new[] { N3Sequence[4], N3Sequence[5] }, o => o.WithStrictOrdering());
        page2.FirstIndex.Should().Be(2);
        page1Again.Items.Select(i => i.Index).Should().Equal(2, 3);
        session.PagingAnchor.Should().Be(2);
    }

    [Fact]
    public void GetPage_PageSizeChanged_KeepsAnchorFixed()
    {
        var session = PermutationSession.Create(4); // 24 permutations
        session.MoveNext(); // index 0
        session.GetPage(1, 5);

        var page = session.GetPage(2, 3);

        session.PagingAnchor.Should().Be(1);
        page.TotalItems.Should().Be(23);
        page.TotalPages.Should().Be(8);
        page.FirstIndex.Should().Be(1);
        page.Items.Select(i => i.Index).Should().Equal(4, 5, 6);
        page.Items.Select(i => i.Values).Should().BeEquivalentTo(
            new[] { PermutationAlgorithms.Unrank(4, 4), PermutationAlgorithms.Unrank(4, 5), PermutationAlgorithms.Unrank(4, 6) },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void MoveNext_AfterGetPage_ClearsAnchor()
    {
        var session = PermutationSession.Create(4);
        session.GetPage(1, 2); // anchor 0, current -> 1

        session.MoveNext(); // current -> 2

        session.PagingAnchor.Should().BeNull();
        var page = session.GetPage(1, 2);
        session.PagingAnchor.Should().Be(3);
        page.FirstIndex.Should().Be(3);
        page.Items[0].Index.Should().Be(3);
    }

    [Fact]
    public void MoveNext_WhenExhausted_StillClearsAnchor()
    {
        var session = PermutationSession.Create(2);
        session.GetPage(1, 2); // current -> 1 (last)

        var result = session.MoveNext();

        result.IsExhausted.Should().BeTrue();
        session.PagingAnchor.Should().BeNull();
    }

    // ---- GetPage: position update ("Back") ------------------------------

    [Fact]
    public void GetPage_ReturnsItems_MovesCurrentToLastItemAndNextContinuesAfterIt()
    {
        var session = PermutationSession.Create(3);
        session.MoveNext();
        session.MoveNext();
        session.GetPage(1, 2); // items 2,3

        session.CurrentIndex.Should().Be(3);
        session.Current.Should().Equal(2, 3, 1);

        var next = session.MoveNext();

        next.Item!.Index.Should().Be(4);
        next.Item.Values.Should().Equal(3, 1, 2);
        next.HasMore.Should().BeTrue();
    }

    [Fact]
    public void GetPage_LastPageReturned_NextIsExhausted()
    {
        var session = PermutationSession.Create(3);
        session.MoveNext();
        session.MoveNext();
        session.GetPage(1, 2);
        session.GetPage(2, 2); // items 4,5

        var next = session.MoveNext();

        session.CurrentIndex.Should().Be(5);
        next.IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void GetPage_ReturnedValues_AreIndependentOfSessionState()
    {
        var session = PermutationSession.Create(3);
        var page = session.GetPage(1, 3);

        page.Items[2].Values[0] = 99;
        session.MoveNext();

        page.Items.Select(i => i.Values).Should().BeEquivalentTo(
            new[] { N3Sequence[0], N3Sequence[1], new[] { 99, 1, 3 } }, o => o.WithStrictOrdering());
        session.Current.Should().Equal(2, 3, 1);
    }

    // ---- ExitAllMode ----------------------------------------------------

    [Fact]
    public void ExitAllMode_BackScenario_NextAllStartsRightAfterLastDisplayedItem()
    {
        // n=3, size 2: Next #1, All #2,#3, page 2 #4,#5, Back shows #5, All again must start at #6.
        var session = PermutationSession.Create(3);
        session.MoveNext();             // index 0 (#1)
        session.GetPage(1, 2);          // indices 1,2
        session.GetPage(2, 2);          // indices 3,4 -> current 4 (#5)

        session.ExitAllMode();

        session.PagingAnchor.Should().BeNull();
        session.CurrentIndex.Should().Be(4);
        session.Current.Should().Equal(N3Sequence[4]);

        var page = session.GetPage(1, 2);

        session.PagingAnchor.Should().Be(5);
        page.FirstIndex.Should().Be(5);
        page.TotalItems.Should().Be(1);
        page.TotalPages.Should().Be(1);
        page.Items.Select(i => i.Index).Should().Equal(5);
        page.Items[0].Values.Should().Equal(N3Sequence[5]);
    }

    [Fact]
    public void ExitAllMode_AfterGetPage_NextContinuesAfterLastDisplayedItem()
    {
        var session = PermutationSession.Create(3);
        session.GetPage(1, 2); // indices 0,1

        session.ExitAllMode();
        var next = session.MoveNext();

        next.Item!.Index.Should().Be(2);
        next.Item.Values.Should().Equal(N3Sequence[2]);
    }

    [Fact]
    public void ExitAllMode_NotInAllMode_IsNoOp()
    {
        var session = PermutationSession.Create(3);
        session.MoveNext();

        session.ExitAllMode();
        session.ExitAllMode();

        session.PagingAnchor.Should().BeNull();
        session.CurrentIndex.Should().Be(0);
        session.Current.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ExitAllMode_BeforeAnythingShown_NextAllStartsAtIndexZero()
    {
        var session = PermutationSession.Create(3);

        session.ExitAllMode();
        var page = session.GetPage(1, 2);

        page.FirstIndex.Should().Be(0);
        page.Items.Select(i => i.Index).Should().Equal(0, 1);
    }

    // ---- Restore --------------------------------------------------------

    [Fact]
    public void Restore_BeforeAnythingShown_EqualsFreshSession()
    {
        var id = Guid.NewGuid();

        var session = PermutationSession.Restore(id, 3, -1, null);

        session.Id.Should().Be(id);
        session.N.Should().Be(3);
        session.TotalCount.Should().Be(6);
        session.CurrentIndex.Should().Be(-1);
        session.Current.Should().BeNull();
        session.PagingAnchor.Should().BeNull();
        session.MoveNext().Item!.Values.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Restore_MidSequence_RebuildsCurrentAndContinues()
    {
        var session = PermutationSession.Restore(Guid.NewGuid(), 3, 3, null);

        session.CurrentIndex.Should().Be(3);
        session.Current.Should().Equal(N3Sequence[3]);
        var next = session.MoveNext();
        next.Item!.Index.Should().Be(4);
        next.Item.Values.Should().Equal(N3Sequence[4]);
    }

    [Fact]
    public void Restore_InAllMode_ReusesAnchor()
    {
        var session = PermutationSession.Restore(Guid.NewGuid(), 3, 4, 1);

        var page = session.GetPage(1, 2);

        session.PagingAnchor.Should().Be(1);
        page.FirstIndex.Should().Be(1);
        page.Items.Select(i => i.Index).Should().Equal(1, 2);
        session.CurrentIndex.Should().Be(2);
    }

    [Fact]
    public void Restore_RoundTrip_BehavesLikeOriginal()
    {
        var original = PermutationSession.Create(5);
        original.MoveNext();
        original.GetPage(3, 7);

        var restored = PermutationSession.Restore(original.Id, original.N, original.CurrentIndex, original.PagingAnchor);

        restored.Current.Should().Equal(original.Current!);
        restored.GetPage(4, 7).Items.Select(i => i.Values)
            .Should().BeEquivalentTo(original.GetPage(4, 7).Items.Select(i => i.Values), o => o.WithStrictOrdering());
        restored.MoveNext().Item!.Values.Should().Equal(original.MoveNext().Item!.Values);
    }

    [Fact]
    public void Restore_ExhaustedN20_RebuildsLastPermutation()
    {
        var last = PermutationAlgorithms.Factorial(20) - 1;

        var session = PermutationSession.Restore(Guid.NewGuid(), 20, last, last + 1);

        session.Current.Should().Equal(Enumerable.Range(1, 20).Reverse());
        session.HasMore.Should().BeFalse();
        session.GetPage(1, 10).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Restore_EmptyId_ThrowsArgumentException()
    {
        var act = () => PermutationSession.Restore(Guid.Empty, 3, 0, null);

        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Restore_InvalidN_ThrowsArgumentOutOfRange(int n)
    {
        var act = () => PermutationSession.Restore(Guid.NewGuid(), n, 0, null);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("n");
    }

    [Theory]
    [InlineData(-2L)]
    [InlineData(6L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void Restore_CurrentIndexOutOfRange_ThrowsArgumentOutOfRange(long currentIndex)
    {
        var act = () => PermutationSession.Restore(Guid.NewGuid(), 3, currentIndex, null);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("currentIndex");
    }

    [Theory]
    [InlineData(2L, -1L)]
    [InlineData(2L, 4L)]
    [InlineData(-1L, 1L)]
    [InlineData(5L, 7L)]
    public void Restore_PagingAnchorOutOfRange_ThrowsArgumentOutOfRange(long currentIndex, long anchor)
    {
        var act = () => PermutationSession.Restore(Guid.NewGuid(), 3, currentIndex, anchor);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pagingAnchor");
    }

    [Theory]
    [InlineData(-1L, 0L)]
    [InlineData(2L, 0L)]
    [InlineData(2L, 3L)]
    [InlineData(5L, 6L)]
    public void Restore_PagingAnchorAtBounds_IsAccepted(long currentIndex, long anchor)
    {
        var session = PermutationSession.Restore(Guid.NewGuid(), 3, currentIndex, anchor);

        session.PagingAnchor.Should().Be(anchor);
    }

    // ---- GetPage: page math ----------------------------------------------

    [Fact]
    public void GetPage_LastPage_IsShorter()
    {
        var session = PermutationSession.Create(3);

        var page = session.GetPage(2, 4);

        page.TotalPages.Should().Be(2);
        page.Items.Select(i => i.Index).Should().Equal(4, 5);
        page.Items.Select(i => i.Values).Should().BeEquivalentTo(new[] { N3Sequence[4], N3Sequence[5] }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void GetPage_NothingAfterAnchor_ReturnsEmptyPageAndKeepsPosition()
    {
        var session = PermutationSession.Create(3);
        for (var i = 0; i < 6; i++)
        {
            session.MoveNext();
        }

        var page = session.GetPage(1, 2);

        page.IsEmpty.Should().BeTrue();
        page.Items.Should().BeEmpty();
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(2);
        page.TotalItems.Should().Be(0);
        page.TotalPages.Should().Be(0);
        page.FirstIndex.Should().BeNull();
        session.PagingAnchor.Should().Be(6);
        session.CurrentIndex.Should().Be(5);
        session.Current.Should().Equal(3, 2, 1);
    }

    [Fact]
    public void GetPage_NothingAfterAnchorAndPageTwo_ThrowsPageOutOfRange()
    {
        var session = PermutationSession.Create(1);
        session.MoveNext();

        var act = () => session.GetPage(2, 2);

        act.Should().Throw<PageOutOfRangeException>()
            .Where(e => e.Page == 2 && e.TotalPages == 0);
    }

    [Fact]
    public void GetPage_PageBeyondTotalPages_ThrowsPageOutOfRangeWithMessage()
    {
        var session = PermutationSession.Create(3);
        session.MoveNext();
        session.MoveNext();

        var act = () => session.GetPage(3, 2);

        act.Should().Throw<PageOutOfRangeException>()
            .WithMessage("page must be between 1 and 2.")
            .Where(e => e.Page == 3 && e.TotalPages == 2);
    }

    [Fact]
    public void GetPage_Throws_LeavesAnchorAndPositionUnchanged()
    {
        var session = PermutationSession.Create(3);
        session.MoveNext();

        var act = () => session.GetPage(10, 2);

        act.Should().Throw<PageOutOfRangeException>();
        session.PagingAnchor.Should().BeNull();
        session.CurrentIndex.Should().Be(0);
        session.Current.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void GetPage_HugePageForN20_ThrowsPageOutOfRangeWithoutOverflow()
    {
        var session = PermutationSession.Create(20);

        var act = () => session.GetPage(long.MaxValue, PermutationSession.MaxPageSize);

        act.Should().Throw<PageOutOfRangeException>()
            .Where(e => e.TotalPages == 2_432_902_008_176_640L);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void GetPage_PageLessThanOne_ThrowsArgumentOutOfRange(long page)
    {
        var session = PermutationSession.Create(3);

        var act = () => session.GetPage(page, 2);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("page");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    [InlineData(int.MaxValue)]
    public void GetPage_PageSizeOutOfRange_ThrowsArgumentOutOfRange(int pageSize)
    {
        var session = PermutationSession.Create(3);

        var act = () => session.GetPage(1, pageSize);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageSize");
    }

    [Fact]
    public void GetPage_N20LastPage_IsFastAndCorrect()
    {
        var session = PermutationSession.Create(20);
        const int pageSize = 1000;
        var totalPages = PermutationAlgorithms.Factorial(20) / pageSize;

        var stopwatch = Stopwatch.StartNew();
        var page = session.GetPage(totalPages, pageSize);
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
        page.TotalPages.Should().Be(totalPages);
        page.Items.Should().HaveCount(pageSize);
        page.Items[^1].Index.Should().Be(PermutationAlgorithms.Factorial(20) - 1);
        page.Items[^1].Values.Should().Equal(Enumerable.Range(1, 20).Reverse());
        page.Items[0].Values.Should().Equal(PermutationAlgorithms.Unrank(20, page.Items[0].Index));
        session.HasMore.Should().BeFalse();
        session.MoveNext().IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void GetPage_N20PageInTheMiddle_ItemsAreConsecutive()
    {
        var session = PermutationSession.Create(20);

        var page = session.GetPage(1_000_000_000, 50);

        var expectedFirst = 999_999_999L * 50;
        page.Items.Select(i => i.Index).Should().Equal(Enumerable.Range(0, 50).Select(i => expectedFirst + i));
        foreach (var item in page.Items)
        {
            PermutationAlgorithms.Rank(item.Values).Should().Be(item.Index);
        }
    }
}
