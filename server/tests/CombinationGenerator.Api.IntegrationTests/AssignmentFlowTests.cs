using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>
/// The end-to-end user flow of docs/assignment-original.txt and the "Paging anchor" / "Back" semantics of the contract.
/// </summary>
public sealed class AssignmentFlowTests : IClassFixture<ApiFactory>
{
    private const string NoMore = "No more combinations";

    private readonly ApiClient _api;

    public AssignmentFlowTests(ApiFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task FullFlow_N3_NextAllBackNextExhaustionReset()
    {
        // Start: total shown.
        var start = await _api.StartAsync(3);
        start.TotalCount.Should().Be("6");
        var id = start.SessionId;

        // Next → #1 [1,2,3]; Next → #2 [1,3,2].
        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult("1", [1, 2, 3], true, null));
        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult("2", [1, 3, 2], true, null));

        // All (pageSize 2): starts right after the displayed #2.
        var page1 = await _api.AllAsync(id, 2, 1);
        page1.FirstIndex.Should().Be("3");
        page1.Items.Should().BeEquivalentTo(
            new[] { new PageItem("3", [2, 1, 3]), new PageItem("4", [2, 3, 1]) }, o => o.WithStrictOrdering());

        // Back: client shows the last item of the viewed page (#4); Next continues after it.
        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult("5", [3, 1, 2], true, null));
        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult("6", [3, 2, 1], false, null));

        // Exhaustion: normal 200 with the message, repeatedly.
        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult(null, null, false, NoMore));
        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult(null, null, false, NoMore));

        // All at the end: empty page with the message.
        var empty = await _api.AllAsync(id, 2, 1);
        empty.Should().BeEquivalentTo(new PageResult([], "1", 2, "0", "0", null, NoMore));

        // Reset = start again: a fresh session from #1, the old one is untouched.
        var reset = await _api.StartAsync(4);
        reset.TotalCount.Should().Be("24");
        (await _api.NextAsync(reset.SessionId)).Should().BeEquivalentTo(new NextResult("1", [1, 2, 3, 4], true, null));
        (await _api.NextAsync(id)).Message.Should().Be(NoMore);
    }

    [Fact]
    public async Task All_PagingKeepsAnchorFixed_FirstIndexAndTotalsStable()
    {
        var id = (await _api.StartAsync(3)).SessionId;
        await _api.NextAsync(id);
        await _api.NextAsync(id);

        var page1 = await _api.AllAsync(id, 2, 1);
        var page2 = await _api.AllAsync(id, 2, 2);
        var page1Again = await _api.AllAsync(id, 2, 1);

        page2.Should().BeEquivalentTo(new PageResult(
            [new PageItem("5", [3, 1, 2]), new PageItem("6", [3, 2, 1])], "2", 2, "2", "4", "3", null));
        page1Again.Should().BeEquivalentTo(page1);
    }

    [Fact]
    public async Task All_ChangingPageSizeWhilePaging_KeepsSameAnchor()
    {
        var id = (await _api.StartAsync(4)).SessionId;
        await _api.NextAsync(id); // #1

        await _api.AllAsync(id, 5, 1);                 // anchor #1, items #2..#6 (position moves to #6)
        var resized = await _api.AllAsync(id, 3, 2);   // same anchor: items #5..#7

        resized.FirstIndex.Should().Be("2");
        resized.TotalItems.Should().Be("23");
        resized.TotalPages.Should().Be("8");
        resized.Items.Select(i => i.Index).Should().Equal("5", "6", "7");
    }

    [Fact]
    public async Task Back_FromLastPage_NextReturnsNoMoreCombinations()
    {
        var id = (await _api.StartAsync(3)).SessionId;
        await _api.NextAsync(id);
        await _api.NextAsync(id);
        await _api.AllAsync(id, 2, 1);

        var page2 = await _api.AllAsync(id, 2, 2); // viewing #5,#6 → Back shows #6

        page2.Items[^1].Index.Should().Be("6");
        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult(null, null, false, NoMore));
    }

    [Fact]
    public async Task Back_JumpBackToEarlierPage_NextContinuesAfterThatPage()
    {
        var id = (await _api.StartAsync(4)).SessionId;

        await _api.AllAsync(id, 5, 3);   // #11..#15
        await _api.AllAsync(id, 5, 1);   // back to #1..#5 → position #5

        (await _api.NextAsync(id)).Index.Should().Be("6");
    }

    [Fact]
    public async Task All_AfterNextEndsAllMode_CapturesNewAnchor()
    {
        var id = (await _api.StartAsync(4)).SessionId;
        await _api.AllAsync(id, 3, 2);           // anchor #0, items #4..#6, position #6

        var next = await _api.NextAsync(id);     // #7, ends All mode
        var page = await _api.AllAsync(id, 3, 1);

        next.Index.Should().Be("7");
        page.FirstIndex.Should().Be("8");
        page.TotalItems.Should().Be("17");
        page.Items.Select(i => i.Index).Should().Equal("8", "9", "10");
    }

    [Fact]
    public async Task All_EnteredRightAfterStartThenNext_NextContinuesAfterPage()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        await _api.AllAsync(id, 2, 1); // #1,#2

        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult("3", [2, 1, 3], true, null));
    }

    [Fact]
    public async Task All_EmptyPageAtEnd_LeavesPositionUnchanged()
    {
        var id = (await _api.StartAsync(2)).SessionId;
        await _api.NextAsync(id);
        await _api.NextAsync(id); // #2 (last)

        var empty = await _api.AllAsync(id, 10, 1);

        empty.Items.Should().BeEmpty();
        empty.Message.Should().Be(NoMore);
        (await _api.NextAsync(id)).Message.Should().Be(NoMore);
    }

    [Fact]
    public async Task Example1000_All_FirstItemIs1001()
    {
        // Assignment: "if combination 1000 is displayed, All starts at 1001".
        var id = (await _api.StartAsync(7)).SessionId;
        await _api.AllAsync(id, 999, 1);                 // position → #999
        var displayed = await _api.NextAsync(id);        // #1000 displayed, All mode ended

        var page = await _api.AllAsync(id, 10, 1);

        displayed.Index.Should().Be("1000");
        page.FirstIndex.Should().Be("1001");
        page.Items[0].Index.Should().Be("1001");
        var reference = Reference.AllPermutations(7);
        displayed.Permutation.Should().Equal(reference[999]);
        page.Items.Select(i => i.Permutation).Should().BeEquivalentTo(
            reference.Skip(1000).Take(10), o => o.WithStrictOrdering());
    }
}
