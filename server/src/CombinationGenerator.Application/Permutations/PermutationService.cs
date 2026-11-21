using System.Globalization;
using CombinationGenerator.Application.Abstractions;
using CombinationGenerator.Application.Dtos;
using CombinationGenerator.Application.Exceptions;
using CombinationGenerator.Domain.Exceptions;
using CombinationGenerator.Domain.Permutations;
using CombinationGenerator.Domain.Sessions;
using Microsoft.Extensions.Logging;

namespace CombinationGenerator.Application.Permutations;

/// <summary>
/// Orchestrates the use cases: validate input → load the session under its lock → run the domain operation →
/// map the result to a DTO (0-based domain indices become 1-based display numbers, large numbers become strings).
/// </summary>
/// <remarks>
/// <para><b>Paging anchor.</b> The anchor is stored on the session (<see cref="PermutationSession.PagingAnchor"/>).
/// It is <c>null</c> after <c>start</c>, after every <c>next</c> (<see cref="PermutationSession.MoveNext"/> clears it)
/// and after every <c>back</c> (<see cref="PermutationSession.ExitAllMode"/> clears it).
/// The first <c>all</c> request finds it <c>null</c> and captures it as the then-current position
/// (<see cref="PermutationSession.NextIndex"/>, i.e. "the first permutation after the displayed one");
/// every further <c>all</c> request reuses it unchanged, whatever its page or page size, so <c>firstIndex</c>,
/// <c>totalItems</c> and page boundaries stay stable while the user pages.</para>
/// <para><b>Back.</b> Every non-empty page moves the session's current position to the last item of that page,
/// so a following <c>next</c> continues right after it. An empty page leaves the position unchanged. When the client
/// goes Back it calls <see cref="ExitAllModeAsync"/>, which only clears the anchor: without it a later "All" would
/// reuse the stale anchor of the previous "All" visit instead of starting after the displayed permutation.</para>
/// <para>Both rules are implemented atomically by <see cref="PermutationSession.GetPage"/>; this service only
/// serializes access through <see cref="ISessionStore.ExecuteAsync{TResult}"/>.</para>
/// </remarks>
public sealed class PermutationService : IPermutationService
{
    /// <summary>Message returned when no permutation is left.</summary>
    public const string NoMoreCombinationsMessage = "No more combinations";

    private readonly ISessionStore _store;
    private readonly ILogger<PermutationService> _logger;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="store">The session store.</param>
    /// <param name="logger">The logger.</param>
    public PermutationService(ISessionStore store, ILogger<PermutationService> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StartSessionResponse> StartAsync(int n, CancellationToken cancellationToken = default)
    {
        if (n < PermutationAlgorithms.MinN || n > PermutationAlgorithms.MaxN)
        {
            throw new InvalidNException(n);
        }

        var session = PermutationSession.Create(n);
        await _store.AddAsync(session, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Session {SessionId} started with n={N}", session.Id, n);
        return new StartSessionResponse(session.Id, session.N, Format(session.TotalCount));
    }

    /// <inheritdoc />
    public async Task<NextPermutationResponse> GetNextAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var result = await _store
            .ExecuteAsync(sessionId, session => session.MoveNext(), cancellationToken)
            .ConfigureAwait(false);

        if (result.Item is null)
        {
            _logger.LogDebug("Session {SessionId} has no more permutations", sessionId);
            return new NextPermutationResponse(null, null, false, NoMoreCombinationsMessage);
        }

        return new NextPermutationResponse(
            FormatDisplayIndex(result.Item.Index),
            result.Item.Values,
            result.HasMore,
            null);
    }

    /// <inheritdoc />
    public async Task<PermutationPageResponse> GetAllAsync(
        Guid sessionId,
        long page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Both session-independent checks run before the session is touched; all failures are reported together.
        PagingValidation.EnsureValid(pageSize, page);

        PageResult result;
        try
        {
            result = await _store
                .ExecuteAsync(sessionId, session => session.GetPage(page, pageSize), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PageOutOfRangeException ex)
        {
            throw InvalidPageException.TooLarge(ex.Page, ex.TotalPages);
        }

        _logger.LogDebug(
            "Session {SessionId} served page {Page} (pageSize={PageSize}) of {TotalPages}",
            sessionId,
            page,
            pageSize,
            result.TotalPages);

        var items = result.Items
            .Select(item => new PermutationItemDto(FormatDisplayIndex(item.Index), item.Values))
            .ToArray();

        // Domain FirstIndex is the 0-based index of the first item after the anchor; +1 gives its display number.
        return new PermutationPageResponse(
            items,
            Format(result.Page),
            result.PageSize,
            Format(result.TotalPages),
            Format(result.TotalItems),
            result.FirstIndex is { } firstIndex ? FormatDisplayIndex(firstIndex) : null,
            result.IsEmpty ? NoMoreCombinationsMessage : null);
    }

    /// <inheritdoc />
    public async Task ExitAllModeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        // ExecuteAsync needs a result; the operation has none, so it returns a dummy value.
        await _store
            .ExecuteAsync(
                sessionId,
                session =>
                {
                    session.ExitAllMode();
                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("Session {SessionId} left \"All\" mode", sessionId);
    }

    private static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormatDisplayIndex(long zeroBasedIndex) => Format(zeroBasedIndex + 1);
}
