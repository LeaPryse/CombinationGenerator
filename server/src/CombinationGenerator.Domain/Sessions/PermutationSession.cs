using CombinationGenerator.Domain.Exceptions;
using CombinationGenerator.Domain.Permutations;

namespace CombinationGenerator.Domain.Sessions;

/// <summary>
/// The per-user generation state: which permutation of <c>[1..n]</c> is currently displayed and,
/// while the user is in "All" mode, the fixed paging anchor.
/// </summary>
/// <remarks>
/// <para>
/// All indices exposed by this type are <b>0-based</b> (index 0 = identity permutation, displayed as <c>#1</c>).
/// The Application layer converts them to the 1-based display numbers of the API contract.
/// </para>
/// <para>
/// This entity performs no I/O and is <b>not thread-safe</b>: callers (Infrastructure) must serialize
/// all access to a single instance.
/// </para>
/// <para>
/// Memory is O(n): only the current permutation is stored, never the sequence.
/// </para>
/// </remarks>
public sealed class PermutationSession
{
    /// <summary>Smallest accepted page size.</summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// Largest accepted page size. Bounds the memory and time of a single <see cref="GetPage"/> call
    /// to O(<see cref="MaxPageSize"/> · n).
    /// </summary>
    public const int MaxPageSize = 1000;

    private int[] _current;

    /// <summary>
    /// Creates a session positioned before the first permutation (<see cref="CurrentIndex"/> = −1). O(n).
    /// </summary>
    /// <param name="id">The session identifier; must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="n">Permutation size in <c><see cref="PermutationAlgorithms.MinN"/>..<see cref="PermutationAlgorithms.MaxN"/></c>.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is out of range.</exception>
    public PermutationSession(Guid id, int n)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The session id must not be empty.", nameof(id));
        }

        PermutationAlgorithms.ValidateN(n);

        Id = id;
        N = n;
        TotalCount = PermutationAlgorithms.Factorial(n);
        CurrentIndex = -1;
        _current = PermutationAlgorithms.Identity(n);
    }

    /// <summary>
    /// Creates a session with a fresh random identifier. O(n).
    /// </summary>
    /// <param name="n">Permutation size in <c><see cref="PermutationAlgorithms.MinN"/>..<see cref="PermutationAlgorithms.MaxN"/></c>.</param>
    /// <returns>The new session.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is out of range.</exception>
    public static PermutationSession Create(int n) => new(Guid.NewGuid(), n);

    /// <summary>The session identifier.</summary>
    public Guid Id { get; }

    /// <summary>The permutation size.</summary>
    public int N { get; }

    /// <summary>Total number of permutations, <c>n!</c>.</summary>
    public long TotalCount { get; }

    /// <summary>
    /// <b>0-based</b> index of the currently displayed permutation, or −1 when nothing has been displayed yet.
    /// (The API's 1-based "current position" equals <c>CurrentIndex + 1</c>.)
    /// </summary>
    public long CurrentIndex { get; private set; }

    /// <summary>
    /// <b>0-based</b> index of the permutation that a call to <see cref="MoveNext"/> would return
    /// (<c>CurrentIndex + 1</c>); equals <see cref="TotalCount"/> when the sequence is exhausted.
    /// </summary>
    public long NextIndex => CurrentIndex + 1;

    /// <summary><c>true</c> if <see cref="MoveNext"/> would return a permutation.</summary>
    public bool HasMore => NextIndex < TotalCount;

    /// <summary>
    /// <b>0-based</b> index of the first item of "All" mode (page 1, item 1), or <c>null</c> when the session is not in
    /// "All" mode. Captured by the first <see cref="GetPage"/> after creation or after <see cref="MoveNext"/>,
    /// as <see cref="NextIndex"/> at that moment; it then stays fixed until <see cref="MoveNext"/> clears it.
    /// Numerically it equals the 1-based display number of the anchor permutation (the one shown when entering "All" mode).
    /// </summary>
    public long? PagingAnchor { get; private set; }

    /// <summary>
    /// A read-only view of the currently displayed permutation, or <c>null</c> when nothing has been displayed yet.
    /// The view reflects later moves of the session; copy it if a snapshot is needed. O(1).
    /// </summary>
    public IReadOnlyList<int>? Current => CurrentIndex < 0 ? null : Array.AsReadOnly(_current);

    /// <summary>
    /// Advances to the next permutation and ends "All" mode (clears <see cref="PagingAnchor"/>).
    /// O(n) worst case (amortized O(1) step plus an O(n) copy of the returned permutation).
    /// </summary>
    /// <returns>
    /// The new current permutation and whether another one follows; or <see cref="NextResult.Exhausted"/> when the
    /// session is already on the last permutation, in which case the position is unchanged.
    /// </returns>
    public NextResult MoveNext()
    {
        PagingAnchor = null;

        if (!HasMore)
        {
            return NextResult.Exhausted;
        }

        if (CurrentIndex >= 0)
        {
            PermutationAlgorithms.NextPermutation(_current);
        }

        // When CurrentIndex is -1, _current already holds the identity permutation (index 0).
        CurrentIndex++;
        return new NextResult(new PermutationItem(CurrentIndex, (int[])_current.Clone()), HasMore);
    }

    /// <summary>
    /// Returns one page of the permutations that follow the paging anchor and moves the current position to the
    /// last item returned (an empty page leaves the position unchanged).
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <see cref="PagingAnchor"/> is <c>null</c>, it is captured as <see cref="NextIndex"/>; otherwise the existing
    /// anchor is reused, so page boundaries stay stable across calls with any page or page size. The call is atomic:
    /// if it throws, neither the anchor nor the position is changed.
    /// </para>
    /// <para>
    /// The first item is computed with <see cref="PermutationAlgorithms.Unrank"/> (O(n²)), the rest with
    /// <see cref="PermutationAlgorithms.NextPermutation"/>: O(n² + pageSize · n) time and memory, independent of <c>n!</c>.
    /// </para>
    /// </remarks>
    /// <param name="page">1-based page number; page 1 is always accepted (an empty page when nothing follows the anchor).</param>
    /// <param name="pageSize">Items per page, in <c><see cref="MinPageSize"/>..<see cref="MaxPageSize"/></c>.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is less than 1, or <paramref name="pageSize"/> is out of range.</exception>
    /// <exception cref="PageOutOfRangeException"><paramref name="page"/> is greater than the number of pages (and not 1).</exception>
    public PageResult GetPage(long page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, MinPageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);

        var anchor = PagingAnchor ?? NextIndex;
        var totalItems = TotalCount - anchor;
        var totalPages = (totalItems / pageSize) + (totalItems % pageSize == 0 ? 0 : 1);

        if (totalItems == 0)
        {
            if (page != 1)
            {
                throw new PageOutOfRangeException(page, totalPages);
            }

            PagingAnchor = anchor;
            return new PageResult(Array.Empty<PermutationItem>(), page, pageSize, 0, 0, null);
        }

        if (page > totalPages)
        {
            throw new PageOutOfRangeException(page, totalPages);
        }

        // page <= totalPages guarantees (page - 1) * pageSize < totalItems <= 20!, so this cannot overflow.
        var firstIndex = anchor + ((page - 1) * pageSize);
        var count = (int)Math.Min(pageSize, TotalCount - firstIndex);

        var items = new PermutationItem[count];
        var buffer = PermutationAlgorithms.Unrank(N, firstIndex);
        items[0] = new PermutationItem(firstIndex, (int[])buffer.Clone());
        for (var i = 1; i < count; i++)
        {
            PermutationAlgorithms.NextPermutation(buffer);
            items[i] = new PermutationItem(firstIndex + i, (int[])buffer.Clone());
        }

        PagingAnchor = anchor;
        CurrentIndex = firstIndex + count - 1;
        _current = buffer;

        return new PageResult(items, page, pageSize, totalItems, totalPages, anchor);
    }
}
