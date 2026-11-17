using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>
/// Contract: sessions are fully isolated; concurrent requests on the same session are serialized.
/// Assignment question: "3 different users at the same time".
/// </summary>
public sealed class ConcurrencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly ApiClient _api;

    public ConcurrencyTests(ApiFactory factory)
    {
        _factory = factory;
        _api = new ApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task Next_20ParallelCallsOnOneSession_ReturnDistinctConsecutiveIndices()
    {
        var id = (await _api.StartAsync(5)).SessionId;
        var reference = Reference.AllPermutations(5);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => _api.NextAsync(id))));

        var byIndex = results.OrderBy(r => int.Parse(r.Index!)).ToList();
        byIndex.Select(r => r.Index).Should().Equal(Enumerable.Range(1, 20).Select(i => i.ToString()));
        for (var i = 0; i < byIndex.Count; i++)
        {
            byIndex[i].Permutation.Should().Equal(reference[i], "the permutation must match its index #{0}", i + 1);
        }

        (await _api.NextAsync(id)).Index.Should().Be("21");
    }

    [Fact]
    public async Task Next_200ParallelCallsOverIndependentClients_NoDuplicatesNoGaps()
    {
        var id = (await _api.StartAsync(6)).SessionId;
        var clients = Enumerable.Range(0, 8).Select(_ => new ApiClient(_factory.CreateClient())).ToArray();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 200).Select(i => Task.Run(() => clients[i % clients.Length].NextAsync(id))));

        results.Select(r => int.Parse(r.Index!)).Should().BeEquivalentTo(Enumerable.Range(1, 200));
        results.Select(r => string.Join(',', r.Permutation!)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Next_ParallelCallsPastTheEnd_ExactlyTotalCountPermutationsReturned()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => _api.NextAsync(id))));

        results.Where(r => r.Index is not null).Select(r => r.Index).Should().BeEquivalentTo("1", "2", "3", "4", "5", "6");
        results.Count(r => r.Message == "No more combinations").Should().Be(14);
        results.Single(r => r.Index == "6").HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task ThreeSessionsInterleaved_DifferentN_ResultsAreIsolated()
    {
        var s3 = (await _api.StartAsync(3)).SessionId;
        var s4 = (await _api.StartAsync(4)).SessionId;
        var s5 = (await _api.StartAsync(5)).SessionId;
        var ref3 = Reference.AllPermutations(3);
        var ref4 = Reference.AllPermutations(4);
        var ref5 = Reference.AllPermutations(5);

        // Sequential interleaving.
        for (var i = 0; i < 6; i++)
        {
            (await _api.NextAsync(s3)).Permutation.Should().Equal(ref3[i]);
            (await _api.NextAsync(s4)).Permutation.Should().Equal(ref4[i]);
            (await _api.NextAsync(s5)).Permutation.Should().Equal(ref5[i]);
        }

        // Each enters All mode independently with its own anchor.
        var p3 = await _api.AllAsync(s3, 2, 1);
        var p4 = await _api.AllAsync(s4, 2, 1);
        var p5 = await _api.AllAsync(s5, 2, 1);
        p3.Message.Should().Be("No more combinations");
        p4.FirstIndex.Should().Be("7");
        p4.TotalItems.Should().Be("18");
        p5.FirstIndex.Should().Be("7");
        p5.TotalItems.Should().Be("114");
        p4.Items[0].Permutation.Should().Equal(ref4[6]);
        p5.Items[0].Permutation.Should().Equal(ref5[6]);
    }

    [Fact]
    public async Task ThreeSessionsInParallel_EachWalksItsOwnFullSequence()
    {
        var sessions = new[] { 3, 4, 5 };
        var ids = await Task.WhenAll(sessions.Select(n => _api.StartAsync(n)));

        var walks = await Task.WhenAll(ids.Select(start => Task.Run(async () =>
        {
            var collected = new List<NextResult>();
            NextResult next;
            do
            {
                next = await _api.NextAsync(start.SessionId);
                collected.Add(next);
            }
            while (next.Index is not null);

            return (start.N, collected);
        })));

        foreach (var (n, collected) in walks)
        {
            var reference = Reference.AllPermutations(n);
            collected.Should().HaveCount(reference.Count + 1);
            collected.Take(reference.Count).Select(r => r.Permutation)
                .Should().BeEquivalentTo(reference, o => o.WithStrictOrdering());
            collected[^1].Message.Should().Be("No more combinations");
        }
    }

    [Fact]
    public async Task MixedParallelNextAndAll_OnOneSession_ServerStateStaysConsistent()
    {
        var id = (await _api.StartAsync(6)).SessionId;

        var tasks = Enumerable.Range(0, 40).Select(i => Task.Run<object>(async () =>
            i % 4 == 0 ? await _api.AllAsync(id, 3, 1) : await _api.NextAsync(id)));
        var results = await Task.WhenAll(tasks);

        // Every page returned must be internally consistent (contiguous indices starting at its firstIndex).
        foreach (var page in results.OfType<PageResult>())
        {
            var first = long.Parse(page.FirstIndex!);
            page.Items.Select(i => long.Parse(i.Index)).Should().Equal(first, first + 1, first + 2);
        }

        // Every next must return a valid lexicographic permutation for its index.
        var reference = Reference.AllPermutations(6);
        foreach (var next in results.OfType<NextResult>())
        {
            next.Permutation.Should().Equal(reference[int.Parse(next.Index!) - 1]);
        }

        // No two nexts may return the same index.
        results.OfType<NextResult>().Select(r => r.Index).Should().OnlyHaveUniqueItems();
    }
}
