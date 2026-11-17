using CombinationGenerator.Application.Dtos;
using CombinationGenerator.Application.Exceptions;
using CombinationGenerator.Application.Permutations;
using CombinationGenerator.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CombinationGenerator.Application.Tests.Permutations;

public class PermutationServiceTests
{
    private const string NoMore = "No more combinations";

    private readonly FakeSessionStore _store = new();
    private readonly PermutationService _sut;

    public PermutationServiceTests()
    {
        _sut = new PermutationService(_store, NullLogger<PermutationService>.Instance);
    }

    // ---- StartAsync -----------------------------------------------------

    [Fact]
    public async Task StartAsync_N3_ReturnsSessionWithTotalCountAsString()
    {
        var response = await _sut.StartAsync(3);

        response.SessionId.Should().NotBeEmpty();
        response.N.Should().Be(3);
        response.TotalCount.Should().Be("6");
        _store.Sessions.Should().ContainKey(response.SessionId);
    }

    [Fact]
    public async Task StartAsync_N20_TotalCountIsExact20Factorial()
    {
        var response = await _sut.StartAsync(20);

        response.TotalCount.Should().Be("2432902008176640000");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(21)]
    public async Task StartAsync_InvalidN_ThrowsInvalidNException(int n)
    {
        var act = () => _sut.StartAsync(n);

        var ex = await act.Should().ThrowAsync<InvalidNException>();
        ex.Which.Field.Should().Be("n");
        ex.Which.Message.Should().Be("n must be between 1 and 20.");
        _store.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_CalledTwice_CreatesIndependentSessions()
    {
        var first = await _sut.StartAsync(3);
        await _sut.GetNextAsync(first.SessionId);

        var second = await _sut.StartAsync(3);
        var next = await _sut.GetNextAsync(second.SessionId);

        second.SessionId.Should().NotBe(first.SessionId);
        next.Index.Should().Be("1");
    }

    // ---- GetNextAsync ---------------------------------------------------

    [Fact]
    public async Task GetNextAsync_FirstCall_ReturnsIndex1Identity()
    {
        var sessionId = (await _sut.StartAsync(3)).SessionId;

        var response = await _sut.GetNextAsync(sessionId);

        response.Should().BeEquivalentTo(new NextPermutationResponse("1", [1, 2, 3], true, null));
    }

    [Fact]
    public async Task GetNextAsync_SixCallsForN3_ReturnsLexicographicSequenceEndingWithoutMore()
    {
        var sessionId = (await _sut.StartAsync(3)).SessionId;
        int[][] expected = [[1, 2, 3], [1, 3, 2], [2, 1, 3], [2, 3, 1], [3, 1, 2], [3, 2, 1]];

        var responses = new List<NextPermutationResponse>();
        for (var i = 0; i < 6; i++)
        {
            responses.Add(await _sut.GetNextAsync(sessionId));
        }

        responses.Select(r => r.Index).Should().Equal("1", "2", "3", "4", "5", "6");
        responses.Select(r => r.Permutation!.ToArray()).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        responses.Take(5).Should().OnlyContain(r => r.HasMore && r.Message == null);
        responses[5].HasMore.Should().BeFalse();
        responses[5].Message.Should().BeNull();
    }

    [Fact]
    public async Task GetNextAsync_AfterLast_ReturnsNoMoreAndKeepsPosition()
    {
        var sessionId = (await _sut.StartAsync(2)).SessionId;
        await _sut.GetNextAsync(sessionId);
        await _sut.GetNextAsync(sessionId);

        var response = await _sut.GetNextAsync(sessionId);
        var again = await _sut.GetNextAsync(sessionId);

        response.Should().BeEquivalentTo(new NextPermutationResponse(null, null, false, NoMore));
        again.Should().BeEquivalentTo(response);
        _store.Sessions[sessionId].CurrentIndex.Should().Be(1);
    }

    [Fact]
    public async Task GetNextAsync_N1_ReturnsSinglePermutationWithoutMore()
    {
        var sessionId = (await _sut.StartAsync(1)).SessionId;

        var response = await _sut.GetNextAsync(sessionId);

        response.Should().BeEquivalentTo(new NextPermutationResponse("1", [1], false, null));
    }

    [Fact]
    public async Task GetNextAsync_UnknownSession_ThrowsSessionNotFound()
    {
        var id = Guid.NewGuid();

        var act = () => _sut.GetNextAsync(id);

        (await act.Should().ThrowAsync<SessionNotFoundException>()).Which.SessionId.Should().Be(id);
    }

    // ---- GetPageAsync: contract examples (n = 3) ------------------------

    [Fact]
    public async Task GetPageAsync_ViewingIndex2PageSize2_ReturnsContractPage1()
    {
        var sessionId = await StartAndAdvanceAsync(3, 2);

        var response = await _sut.GetPageAsync(sessionId, 1, 2);

        response.Should().BeEquivalentTo(
            new PermutationPageResponse(
                [new PermutationItemDto("3", [2, 1, 3]), new PermutationItemDto("4", [2, 3, 1])],
                "1",
                2,
                "2",
                "4",
                "3",
                null),
            o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task GetPageAsync_SecondPageSameAnchor_ReturnsContractPage2()
    {
        var sessionId = await StartAndAdvanceAsync(3, 2);
        await _sut.GetPageAsync(sessionId, 1, 2);

        var response = await _sut.GetPageAsync(sessionId, 2, 2);

        response.Should().BeEquivalentTo(
            new PermutationPageResponse(
                [new PermutationItemDto("5", [3, 1, 2]), new PermutationItemDto("6", [3, 2, 1])],
                "2",
                2,
                "2",
                "4",
                "3",
                null),
            o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task GetPageAsync_ViewingLast_ReturnsEmptyPageWithMessage()
    {
        var sessionId = await StartAndAdvanceAsync(3, 6);

        var response = await _sut.GetPageAsync(sessionId, 1, 2);

        response.Should().BeEquivalentTo(
            new PermutationPageResponse([], "1", 2, "0", "0", null, NoMore));
        _store.Sessions[sessionId].CurrentIndex.Should().Be(5);
    }

    [Fact]
    public async Task GetPageAsync_RightAfterStart_FirstItemIsIndex1()
    {
        var sessionId = (await _sut.StartAsync(3)).SessionId;

        var response = await _sut.GetPageAsync(sessionId, 1, 50);

        response.FirstIndex.Should().Be("1");
        response.TotalItems.Should().Be("6");
        response.TotalPages.Should().Be("1");
        response.Items.Select(i => i.Index).Should().Equal("1", "2", "3", "4", "5", "6");
    }

    // ---- GetPageAsync: anchor and Back semantics ------------------------

    [Fact]
    public async Task GetPageAsync_AnchorFixedAcrossPagesAndPageSizes()
    {
        var sessionId = await StartAndAdvanceAsync(4, 3);
        var first = await _sut.GetPageAsync(sessionId, 1, 5);

        // After page 1 the current position moved to #8, but the anchor must stay at #3.
        var second = await _sut.GetPageAsync(sessionId, 2, 5);
        var resized = await _sut.GetPageAsync(sessionId, 1, 7);

        first.FirstIndex.Should().Be("4");
        second.FirstIndex.Should().Be("4");
        resized.FirstIndex.Should().Be("4");
        first.TotalItems.Should().Be("21");
        resized.TotalItems.Should().Be("21");
        second.Items[0].Index.Should().Be("9");
        resized.Items.Select(i => i.Index).Should().Equal("4", "5", "6", "7", "8", "9", "10");
    }

    [Fact]
    public async Task GetPageAsync_ThenNext_ContinuesAfterLastItemOfPage()
    {
        var sessionId = await StartAndAdvanceAsync(3, 2);
        await _sut.GetPageAsync(sessionId, 1, 2);

        var next = await _sut.GetNextAsync(sessionId);

        next.Index.Should().Be("5");
        next.Permutation.Should().Equal(3, 1, 2);
    }

    [Fact]
    public async Task GetPageAsync_LastPageThenNext_ReturnsNoMore()
    {
        var sessionId = await StartAndAdvanceAsync(3, 2);
        await _sut.GetPageAsync(sessionId, 1, 2);
        await _sut.GetPageAsync(sessionId, 2, 2);

        var next = await _sut.GetNextAsync(sessionId);

        next.Message.Should().Be(NoMore);
        next.Index.Should().BeNull();
    }

    [Fact]
    public async Task GetPageAsync_AfterNext_CapturesNewAnchor()
    {
        var sessionId = await StartAndAdvanceAsync(3, 1);
        await _sut.GetPageAsync(sessionId, 1, 2); // anchor #1, position -> #3
        await _sut.GetNextAsync(sessionId);       // position -> #4, ends "All" mode

        var response = await _sut.GetPageAsync(sessionId, 1, 2);

        response.FirstIndex.Should().Be("5");
        response.TotalItems.Should().Be("2");
        response.Items.Select(i => i.Index).Should().Equal("5", "6");
    }

    [Fact]
    public async Task GetPageAsync_N20LastPage_ReturnsLastPermutationWithExactIndices()
    {
        var sessionId = (await _sut.StartAsync(20)).SessionId;

        var response = await _sut.GetPageAsync(sessionId, 2_432_902_008_176_640_000L, 1);

        response.Page.Should().Be("2432902008176640000");
        response.TotalPages.Should().Be("2432902008176640000");
        response.TotalItems.Should().Be("2432902008176640000");
        response.FirstIndex.Should().Be("1");
        response.Items.Should().ContainSingle();
        response.Items[0].Index.Should().Be("2432902008176640000");
        response.Items[0].Permutation.Should().Equal(Enumerable.Range(1, 20).Reverse());
    }

    [Fact]
    public async Task GetPageAsync_N20PartialLastPage_ReturnsRemainingItems()
    {
        var sessionId = (await _sut.StartAsync(20)).SessionId;
        // 23 is a prime > 20, so it does not divide 20!: the last page is partial (20! mod 23 = 11).
        const int pageSize = 23;
        var totalPages = (2_432_902_008_176_640_000L + pageSize - 1) / pageSize;

        var response = await _sut.GetPageAsync(sessionId, totalPages, pageSize);

        response.TotalPages.Should().Be(totalPages.ToString(System.Globalization.CultureInfo.InvariantCulture));
        response.Items.Should().HaveCount(11);
        response.Items[^1].Index.Should().Be("2432902008176640000");
    }

    // ---- GetPageAsync: validation ---------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task GetPageAsync_InvalidPageSize_ThrowsInvalidPageSize(int pageSize)
    {
        var sessionId = (await _sut.StartAsync(3)).SessionId;

        var act = () => _sut.GetPageAsync(sessionId, 1, pageSize);

        var ex = await act.Should().ThrowAsync<InvalidPageSizeException>();
        ex.Which.Field.Should().Be("pageSize");
        ex.Which.Message.Should().Be("pageSize must be between 1 and 1000.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task GetPageAsync_PageBelowOne_ThrowsInvalidPageWithoutTouchingStore(long page)
    {
        var sessionId = (await _sut.StartAsync(3)).SessionId;

        var act = () => _sut.GetPageAsync(sessionId, page, 2);

        var ex = await act.Should().ThrowAsync<InvalidPageException>();
        ex.Which.Field.Should().Be("page");
        ex.Which.Message.Should().Be("page must be greater than or equal to 1.");
        _store.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPageAsync_PageBeyondLast_ThrowsInvalidPageAndKeepsState()
    {
        var sessionId = await StartAndAdvanceAsync(3, 2);

        var act = () => _sut.GetPageAsync(sessionId, 3, 2);

        var ex = await act.Should().ThrowAsync<InvalidPageException>();
        ex.Which.Message.Should().Be("page must be between 1 and 2.");
        _store.Sessions[sessionId].CurrentIndex.Should().Be(1);
        _store.Sessions[sessionId].PagingAnchor.Should().BeNull();
    }

    [Fact]
    public async Task GetPageAsync_Page2WhenNothingRemains_ThrowsInvalidPage()
    {
        var sessionId = await StartAndAdvanceAsync(3, 6);

        var act = () => _sut.GetPageAsync(sessionId, 2, 2);

        (await act.Should().ThrowAsync<InvalidPageException>()).Which.Message
            .Should().Be("page must be between 1 and 1.");
    }

    [Fact]
    public async Task GetPageAsync_UnknownSession_ThrowsSessionNotFound()
    {
        var act = () => _sut.GetPageAsync(Guid.NewGuid(), 1, 2);

        await act.Should().ThrowAsync<SessionNotFoundException>();
    }

    // ---- ExitAllModeAsync ("Back") ---------------------------------------

    [Fact]
    public async Task ExitAllModeAsync_ContractScenario_NextAllPage2BackAll_StartsAfterLastShownItem()
    {
        // Contract (n = 3, size 2): Next #1, All #2,#3, page 2 #4,#5, Back, All -> starts at #6.
        var sessionId = await StartAndAdvanceAsync(3, 1);
        (await _sut.GetPageAsync(sessionId, 1, 2)).Items.Select(i => i.Index).Should().Equal("2", "3");
        (await _sut.GetPageAsync(sessionId, 2, 2)).Items.Select(i => i.Index).Should().Equal("4", "5");

        await _sut.ExitAllModeAsync(sessionId);
        var response = await _sut.GetPageAsync(sessionId, 1, 2);

        response.Should().BeEquivalentTo(
            new PermutationPageResponse([new PermutationItemDto("6", [3, 2, 1])], "1", 2, "1", "1", "6", null),
            o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task ExitAllModeAsync_WithoutBack_AllReusesStaleAnchor()
    {
        // Documents why Back must reach the server: without it the anchor of the previous "All" visit is reused.
        var sessionId = await StartAndAdvanceAsync(3, 1);
        await _sut.GetPageAsync(sessionId, 1, 2);
        await _sut.GetPageAsync(sessionId, 2, 2);

        var response = await _sut.GetPageAsync(sessionId, 1, 2);

        response.FirstIndex.Should().Be("2");
    }

    [Fact]
    public async Task ExitAllModeAsync_ClearsAnchorAndKeepsPosition()
    {
        var sessionId = await StartAndAdvanceAsync(3, 1);
        await _sut.GetPageAsync(sessionId, 1, 2);

        await _sut.ExitAllModeAsync(sessionId);

        _store.Sessions[sessionId].PagingAnchor.Should().BeNull();
        _store.Sessions[sessionId].CurrentIndex.Should().Be(2); // #3, the last item of the page shown
        _store.ExecuteCount.Should().Be(3);                     // went through the store (under its lock)
    }

    [Fact]
    public async Task ExitAllModeAsync_ThenNext_ContinuesAfterLastShownItem()
    {
        var sessionId = await StartAndAdvanceAsync(3, 1);
        await _sut.GetPageAsync(sessionId, 1, 2);
        await _sut.ExitAllModeAsync(sessionId);

        var next = await _sut.GetNextAsync(sessionId);

        next.Index.Should().Be("4");
        next.Permutation.Should().Equal(2, 3, 1);
    }

    [Fact]
    public async Task ExitAllModeAsync_OutsideAllMode_IsNoOpAndIdempotent()
    {
        var sessionId = await StartAndAdvanceAsync(3, 2);

        await _sut.ExitAllModeAsync(sessionId);
        await _sut.ExitAllModeAsync(sessionId);

        _store.Sessions[sessionId].CurrentIndex.Should().Be(1);
        _store.Sessions[sessionId].PagingAnchor.Should().BeNull();
        (await _sut.GetNextAsync(sessionId)).Index.Should().Be("3");
    }

    [Fact]
    public async Task ExitAllModeAsync_RightAfterStart_AllStartsAtIndex1()
    {
        var sessionId = (await _sut.StartAsync(3)).SessionId;

        await _sut.ExitAllModeAsync(sessionId);
        var response = await _sut.GetPageAsync(sessionId, 1, 2);

        response.FirstIndex.Should().Be("1");
    }

    [Fact]
    public async Task ExitAllModeAsync_AfterEmptyPage_KeepsPositionOnLast()
    {
        var sessionId = await StartAndAdvanceAsync(3, 6);
        await _sut.GetPageAsync(sessionId, 1, 2);

        await _sut.ExitAllModeAsync(sessionId);

        _store.Sessions[sessionId].CurrentIndex.Should().Be(5);
        (await _sut.GetNextAsync(sessionId)).Message.Should().Be(NoMore);
    }

    [Fact]
    public async Task ExitAllModeAsync_UnknownSession_ThrowsSessionNotFound()
    {
        var id = Guid.NewGuid();

        var act = () => _sut.ExitAllModeAsync(id);

        (await act.Should().ThrowAsync<SessionNotFoundException>()).Which.SessionId.Should().Be(id);
    }

    // ---- Capacity --------------------------------------------------------

    [Fact]
    public async Task StartAsync_StoreFull_PropagatesSessionCapacityExceeded()
    {
        _store.Capacity = 1;
        await _sut.StartAsync(3);

        var act = () => _sut.StartAsync(3);

        (await act.Should().ThrowAsync<SessionCapacityExceededException>()).Which.MaxSessions.Should().Be(1);
        _store.Sessions.Should().HaveCount(1);
    }

    private async Task<Guid> StartAndAdvanceAsync(int n, int steps)
    {
        var sessionId = (await _sut.StartAsync(n)).SessionId;
        for (var i = 0; i < steps; i++)
        {
            await _sut.GetNextAsync(sessionId);
        }

        return sessionId;
    }
}
