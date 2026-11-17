using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>Boundary values of n (1 and 20) and 64-bit page numbers.</summary>
public sealed class EdgeCaseTests : IClassFixture<ApiFactory>
{
    private const string NoMore = "No more combinations";
    private const string Factorial20 = "2432902008176640000";

    private static readonly int[] Ascending20 = Enumerable.Range(1, 20).ToArray();
    private static readonly int[] Descending20 = Enumerable.Range(1, 20).Reverse().ToArray();

    private readonly ApiClient _api;

    public EdgeCaseTests(ApiFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    // ---- n = 1 ------------------------------------------------------------

    [Fact]
    public async Task N1_Next_SinglePermutationThenNoMore()
    {
        var start = await _api.StartAsync(1);

        var first = await _api.NextAsync(start.SessionId);
        var second = await _api.NextAsync(start.SessionId);

        start.TotalCount.Should().Be("1");
        first.Should().BeEquivalentTo(new NextResult("1", [1], false, null));
        second.Should().BeEquivalentTo(new NextResult(null, null, false, NoMore));
    }

    [Fact]
    public async Task N1_AllRightAfterStart_ReturnsTheSingleItem()
    {
        var start = await _api.StartAsync(1);

        var page = await _api.AllAsync(start.SessionId, 50, 1);

        page.Should().BeEquivalentTo(new PageResult([new PageItem("1", [1])], "1", 50, "1", "1", "1", null));
        (await _api.NextAsync(start.SessionId)).Message.Should().Be(NoMore);
    }

    [Fact]
    public async Task N1_AllAfterNext_ReturnsEmptyPageWithMessage()
    {
        var start = await _api.StartAsync(1);
        await _api.NextAsync(start.SessionId);

        var page = await _api.AllAsync(start.SessionId, 1, 1);

        page.Should().BeEquivalentTo(new PageResult([], "1", 1, "0", "0", null, NoMore));
    }

    // ---- n = 20 -----------------------------------------------------------

    [Fact]
    public async Task N20_FirstTwoNexts_AreIdentityAndSwapOfLastTwo()
    {
        var start = await _api.StartAsync(20);

        var first = await _api.NextAsync(start.SessionId);
        var second = await _api.NextAsync(start.SessionId);

        start.TotalCount.Should().Be(Factorial20);
        first.Should().BeEquivalentTo(new NextResult("1", Ascending20, true, null));
        var swapped = Ascending20.ToArray();
        (swapped[18], swapped[19]) = (swapped[19], swapped[18]);
        second.Should().BeEquivalentTo(new NextResult("2", swapped, true, null));
    }

    [Fact]
    public async Task N20_FirstPage_PageSize1000()
    {
        var start = await _api.StartAsync(20);

        var page = await _api.AllAsync(start.SessionId, 1000, 1);

        page.TotalItems.Should().Be(Factorial20);
        page.TotalPages.Should().Be("2432902008176640");
        page.FirstIndex.Should().Be("1");
        page.Items.Should().HaveCount(1000);
        page.Items[0].Should().BeEquivalentTo(new PageItem("1", Ascending20));
        page.Items[^1].Index.Should().Be("1000");
        page.Items.Select(i => i.Index).Should().Equal(Enumerable.Range(1, 1000).Select(i => i.ToString()));
    }

    [Fact]
    public async Task N20_LastPage_PageSize1000_EndsWithDescendingPermutation()
    {
        var start = await _api.StartAsync(20);

        var page = await _api.AllAsync(start.SessionId, 1000, 2432902008176640);

        page.Page.Should().Be("2432902008176640");
        page.Items.Should().HaveCount(1000);
        page.Items[0].Index.Should().Be("2432902008176639001");
        page.Items[^1].Should().BeEquivalentTo(new PageItem(Factorial20, Descending20));
    }

    [Fact]
    public async Task N20_LastPage_PageSize1_PageBeyond2Pow53IsAddressable()
    {
        var start = await _api.StartAsync(20);

        var page = await _api.AllAsync(start.SessionId, 1, 2432902008176640000);

        page.Page.Should().Be(Factorial20);
        page.TotalPages.Should().Be(Factorial20);
        page.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new PageItem(Factorial20, Descending20));
        (await _api.NextAsync(start.SessionId)).Should().BeEquivalentTo(new NextResult(null, null, false, NoMore));
    }

    [Fact]
    public async Task N20_PageJustAbove2Pow53_HasExactIndex()
    {
        // 2^53 + 1 cannot be represented exactly as a double: catches any double-based arithmetic.
        const long page = 9007199254740993;
        var start = await _api.StartAsync(20);

        var result = await _api.AllAsync(start.SessionId, 1, page);

        result.Page.Should().Be("9007199254740993");
        result.Items.Should().ContainSingle().Which.Index.Should().Be("9007199254740993");
    }

    [Fact]
    public async Task N20_PageBeyondLast_Returns400()
    {
        var start = await _api.StartAsync(20);

        using var response = await _api.GetAllRawAsync(
            start.SessionId.ToString(), "pageSize=1000&page=2432902008176641");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task N20_SecondToLastViaAll_ThenNext_ReturnsLastThenNoMore()
    {
        var start = await _api.StartAsync(20);
        var page = await _api.AllAsync(start.SessionId, 1, 2432902008176639999); // #...639999

        var last = await _api.NextAsync(start.SessionId);
        var none = await _api.NextAsync(start.SessionId);

        page.Items.Single().Index.Should().Be("2432902008176639999");
        var expectedSecondToLast = Descending20.ToArray();
        (expectedSecondToLast[18], expectedSecondToLast[19]) = (expectedSecondToLast[19], expectedSecondToLast[18]);
        page.Items.Single().Permutation.Should().Equal(expectedSecondToLast);
        last.Should().BeEquivalentTo(new NextResult(Factorial20, Descending20, false, null));
        none.Should().BeEquivalentTo(new NextResult(null, null, false, NoMore));
    }

    [Fact]
    public async Task N20_MiddlePage_IsLexicographicSuccessorChain()
    {
        var start = await _api.StartAsync(20);

        // Index 19! + 1 is the first permutation starting with 2: [2,1,3,4,...,20].
        var page = await _api.AllAsync(start.SessionId, 2, 60822550204416001);

        var expectedFirst = new[] { 2, 1 }.Concat(Enumerable.Range(3, 18)).ToArray();
        page.Items[0].Should().BeEquivalentTo(new PageItem("121645100408832001", expectedFirst));
        var expectedLastOfOnes = new[] { 1 }.Concat(Enumerable.Range(2, 19).Reverse()).ToArray();

        // Page before: last item of that page is the last permutation starting with 1.
        var previous = await _api.AllAsync(start.SessionId, 2, 60822550204416000);
        previous.Items[^1].Should().BeEquivalentTo(new PageItem("121645100408832000", expectedLastOfOnes));
    }
}
