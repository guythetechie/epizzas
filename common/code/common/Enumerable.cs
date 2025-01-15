using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Nito.Comparers;
using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace common;

public static class IEnumerableExtensions
{
    /// <summary>
    /// Performs a side effect on each element of the enumerable.
    /// </summary>
    public static IEnumerable<T> Do<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach (var item in enumerable)
        {
            action(item);
            yield return item;
        }
    }

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed sequentially. The function returns after all actions have executed.
    /// </summary>
    public static void Iter<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach (var t in enumerable)
        {
            action(t);
        }
    }

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed in parallel. The function will wait for all actions to complete before returning.
    /// </summary>
    public static void IterParallel<T>(this IEnumerable<T> enumerable, Action<T> action) =>
        enumerable.IterParallel(action, maxDegreeOfParallelism: -1);

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed in parallel with based on <paramref name="maxDegreeOfParallelism"/>.
    /// The function will wait for all actions to complete before returning.
    /// </summary>
    public static void IterParallel<T>(this IEnumerable<T> enumerable, Action<T> action, int maxDegreeOfParallelism)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism
        };

        Parallel.ForEach(enumerable, parallelOptions, action);
    }

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed sequentially. The function returns after all actions have executed.
    /// </summary>
    public static async ValueTask Iter<T>(this IEnumerable<T> enumerable,
                                          Func<T, ValueTask> action,
                                          CancellationToken cancellationToken) =>
        await enumerable.IterParallel(async (t, _) => await action(t), maxDegreeOfParallelism: 1, cancellationToken);

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed sequentially. The function returns after all actions have executed.
    /// </summary>
    public static async ValueTask Iter<T>(this IEnumerable<T> enumerable,
                                          Func<T, CancellationToken, ValueTask> action,
                                          CancellationToken cancellationToken) =>
        await enumerable.IterParallel(action, maxDegreeOfParallelism: 1, cancellationToken);

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed in parallel. The function will wait for all actions to complete before returning.
    /// </summary>
    public static async ValueTask IterParallel<T>(this IEnumerable<T> enumerable,
                                                  Func<T, ValueTask> action,
                                                  CancellationToken cancellationToken) =>
    await enumerable.IterParallel(async (t, _) => await action(t), maxDegreeOfParallelism: -1, cancellationToken);

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed in parallel. The function will wait for all actions to complete before returning.
    /// </summary>
    public static async ValueTask IterParallel<T>(this IEnumerable<T> enumerable,
                                                  Func<T, CancellationToken, ValueTask> action,
                                                  CancellationToken cancellationToken) =>
        await enumerable.IterParallel(action, maxDegreeOfParallelism: -1, cancellationToken);

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// <paramref name="maxDegreeOfParallelism"/> controls the maximum number of parallel actions.
    /// The function will wait for all actions to complete before returning.
    /// </summary>
    public static async ValueTask IterParallel<T>(this IEnumerable<T> enumerable,
                                                  Func<T, CancellationToken, ValueTask> action,
                                                  int maxDegreeOfParallelism,
                                                  CancellationToken cancellationToken)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(enumerable, parallelOptions: options, action);
    }

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed in parallel. The function will wait for all actions to complete before returning.
    /// </summary>
    public static async ValueTask IterParallel<T1, T2>(this IEnumerable<(T1, T2)> enumerable,
                                                       Func<T1, T2, CancellationToken, ValueTask> action,
                                                       CancellationToken cancellationToken) =>
        await enumerable.IterParallel(async (t, cancellationToken) => await action(t.Item1, t.Item2, cancellationToken),
                                      cancellationToken);

    /// <summary>
    /// Applies <paramref name="f"/> to each element and filters out <seealso cref="Option.None"/> values.
    /// </summary>
    public static IEnumerable<T2> Choose<T, T2>(this IEnumerable<T> enumerable, Func<T, Option<T2>> f) =>
        from t in enumerable
        let option = f(t)
        where option.IsSome
        select option.ValueUnsafe();

    /// <summary>
    /// Applies <paramref name="f"/> to each element of <paramref name="enumerable"/> and
    /// returns the first Option of <typeparamref name="T2"/> that is Some.
    /// If all options are None, returns a None.
    /// </summary>
    public static async ValueTask<Option<T2>> Pick<T, T2>(this IEnumerable<T> enumerable,
                                                          Func<T, CancellationToken, ValueTask<Option<T2>>> f,
                                                          CancellationToken cancellationToken)
    {
        foreach (var item in enumerable)
        {
            var option = await f(item, cancellationToken);

            if (option.IsSome)
            {
                return option;
            }
        }

        return Option<T2>.None;
    }

    /// <summary>
    /// Returns the first item in the enumerable. If the enumerable is empty, returns <seealso cref="Option.None"/>.
    /// </summary>
    public static Option<T> HeadOrNone<T>(this IEnumerable<T> enumerable) =>
        enumerable.FirstOrDefault() ?? Option<T>.None;

    /// <summary>
    /// Returns the last item in the enumerable. If the enumerable is empty, returns <seealso cref="Option.None"/>.
    /// </summary>
    public static Option<T> LastOrNone<T>(this IEnumerable<T> enumerable) =>
        enumerable.LastOrDefault() ?? Option<T>.None;

    public static FrozenSet<T> ToFrozenSet<T, TKey>(this IEnumerable<T> enumerable, Func<T, TKey> keySelector)
    {
        var comparer = EqualityComparerBuilder.For<T>().EquateBy(keySelector);

        return enumerable.ToFrozenSet(comparer);
    }

    public static FrozenDictionary<TKey, TValue> ToFrozenDictionary<TKey, TValue>(
        this IEnumerable<(TKey, TValue)> enumerable,
        IEqualityComparer<TKey>? comparer = default) where TKey : notnull =>
        enumerable.ToFrozenDictionary(kvp => kvp.Item1, kvp => kvp.Item2, comparer);
}

public static class IAsyncEnumerableExtensions
{
    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed sequentially. The function returns after all actions have executed.
    /// </summary>
    public static async ValueTask Iter<T>(this IAsyncEnumerable<T> enumerable,
                                          Func<T, ValueTask> action,
                                          CancellationToken cancellationToken) =>
        await enumerable.Iter(async (t, cancellationToken) => await action(t), cancellationToken);

    /// <summary>
    /// Iterates over an <seealso cref="IEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed sequentially. The function returns after all actions have executed.
    /// </summary>
    public static async ValueTask Iter<T>(this IAsyncEnumerable<T> enumerable,
                                          Func<T, CancellationToken, ValueTask> action,
                                          CancellationToken cancellationToken)
    {
        await foreach (var item in enumerable.WithCancellation(cancellationToken))
        {
            await action(item, cancellationToken);
        }
    }

    /// <summary>
    /// Iterates over an <seealso cref="IAsyncEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed in parallel. The function will wait for all actions to complete before returning.
    /// </summary>
    public static async ValueTask IterParallel<T>(this IAsyncEnumerable<T> enumerable,
                                                  Func<T, ValueTask> action,
                                                  CancellationToken cancellationToken) =>
        await enumerable.IterParallel(async (t, _) => await action(t), maxDegreeOfParallelism: -1, cancellationToken);

    /// <summary>
    /// Iterates over an <seealso cref="IAsyncEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed in parallel. The function will wait for all actions to complete before returning.
    /// </summary>
    public static async ValueTask IterParallel<T>(this IAsyncEnumerable<T> enumerable,
                                                  Func<T, CancellationToken, ValueTask> action,
                                                  CancellationToken cancellationToken) =>
        await enumerable.IterParallel(action, maxDegreeOfParallelism: -1, cancellationToken);

    /// <summary>
    /// Iterates over an <seealso cref="IAsyncEnumerable{T}"/> and executes <paramref name="action"/> for each element.
    /// <paramref name="maxDegreeOfParallelism"/> controls the maximum number of parallel actions.
    /// The function will wait for all actions to complete before returning.
    /// </summary>
    public static async ValueTask IterParallel<T>(this IAsyncEnumerable<T> enumerable,
                                                  Func<T, CancellationToken, ValueTask> action,
                                                  int maxDegreeOfParallelism,
                                                  CancellationToken cancellationToken)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(enumerable, parallelOptions: options, action);
    }

    /// <summary>
    /// Iterates over an <seealso cref="IAsyncEnumerable"/> and executes <paramref name="action"/> for each element.
    /// Each action is executed in parallel. The function will wait for all actions to complete before returning.
    /// </summary>
    public static async ValueTask IterParallel<T1, T2>(this IAsyncEnumerable<(T1, T2)> enumerable,
                                                       Func<T1, T2, CancellationToken, ValueTask> action,
                                                       CancellationToken cancellationToken) =>
        await enumerable.IterParallel(async (t, cancellationToken) => await action(t.Item1, t.Item2, cancellationToken),
                                      cancellationToken);

    public static async ValueTask<FrozenSet<T>> ToFrozenSet<T>(this IAsyncEnumerable<T> enumerable,
                                                               CancellationToken cancellationToken,
                                                               IEqualityComparer<T>? comparer = default)
    {
        var items = await enumerable.ToListAsync(cancellationToken);

        return items.ToFrozenSet(comparer);
    }

    /// <summary>
    /// Applies <paramref name="f"/> to each element and filters out <seealso cref="Option.None"/> values.
    /// </summary>
    public static IAsyncEnumerable<T2> Choose<T, T2>(this IAsyncEnumerable<T> enumerable, Func<T, Option<T2>> f) =>
        enumerable.Choose(async (t, cancellationToken) => await ValueTask.FromResult(f(t)));

    /// <summary>
    /// Applies <paramref name="f"/> to each element and filters out <seealso cref="Option.None"/> values.
    /// </summary>
    public static IAsyncEnumerable<T2> Choose<T, T2>(this IAsyncEnumerable<T> enumerable,
                                                     Func<T, ValueTask<Option<T2>>> f) =>
        enumerable.Choose(async (t, cancellationToken) => await f(t));

    /// <summary>
    /// Applies <paramref name="f"/> to each element and filters out <seealso cref="Option.None"/> values.
    /// </summary>
    public static IAsyncEnumerable<T2> Choose<T, T2>(this IAsyncEnumerable<T> enumerable,
                                                     Func<T, CancellationToken, ValueTask<Option<T2>>> f) =>
        enumerable.SelectAwaitWithCancellation(f)
                  .Where(option => option.IsSome)
                  .Select(option => option.ValueUnsafe()!);

    /// <summary>
    /// 
    /// </summary>
    public static async ValueTask<Option<T>> FirstOrNone<T>(this IAsyncEnumerable<T> enumerable,
                                                            CancellationToken cancellationToken) =>
        await enumerable.Select(Option<T>.Some)
                        .DefaultIfEmpty(Option<T>.None)
                        .FirstAsync(cancellationToken);

    /// <summary>
    /// Applies <paramref name="f"/> to each element of <paramref name="enumerable"/> and
    /// returns the first Option of <typeparamref name="T2"/> that is Some.
    /// If all options are None, returns a None.
    /// </summary>
    public static async ValueTask<Option<T2>> Pick<T, T2>(this IAsyncEnumerable<T> enumerable,
                                                          Func<T, CancellationToken, ValueTask<Option<T2>>> f,
                                                          CancellationToken cancellationToken) =>
        await enumerable.Choose(f)
                        .FirstOrNone(cancellationToken);

    public static async ValueTask<FrozenDictionary<TKey, T>> ToFrozenDictionary<TKey, T>(
        this IAsyncEnumerable<T> enumerable,
        Func<T, TKey> keySelector,
        CancellationToken cancellationToken,
        IEqualityComparer<TKey>? comparer = default) where TKey : notnull =>
        await enumerable.Select(item => (keySelector(item), item))
                        .ToFrozenDictionary(cancellationToken, comparer);

    public static async ValueTask<FrozenDictionary<TKey, TValue>> ToFrozenDictionary<TKey, TValue>(
        this IAsyncEnumerable<(TKey, TValue)> enumerable,
        CancellationToken cancellationToken,
        IEqualityComparer<TKey>? comparer = default) where TKey : notnull
    {
        var array = await enumerable.ToArrayAsync(cancellationToken);

        return array.ToFrozenDictionary(comparer);
    }

    public static async ValueTask<ImmutableArray<T>> ToImmutableArray<T>(this IAsyncEnumerable<T> enumerable,
                                                                         CancellationToken cancellationToken)
    {
        var result = await enumerable.ToArrayAsync(cancellationToken);

        return [.. result];
    }

    /// <summary>
    /// Takes each element in <paramref name="enumerable"/>,
    /// runs <paramref name="action"/> on it, then returns the element.
    /// </summary>
    public static IAsyncEnumerable<T> Tap<T>(this IAsyncEnumerable<T> enumerable, Action<T> action) =>
        enumerable.Select(t =>
        {
            action(t);
            return t;
        });

    public static IAsyncEnumerable<T[]> Chunk<T>(this IAsyncEnumerable<T> source,
                                                 int size,
                                                 CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        return source.GetChunkIterator(size, cancellationToken);
    }

    private static async IAsyncEnumerable<T[]> GetChunkIterator<T>(
        this IAsyncEnumerable<T> source,
        int size,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        // Implementation based on https://github.com/dotnet/runtime/blob/c417e3b74ab0b3fd55a0cc43723f75c6df9118d3/src/libraries/System.Linq/src/System/Linq/Chunk.cs#L72
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        // Before allocating anything, make sure there's at least one element.
        if (await enumerator.MoveNextAsync(cancellationToken))
        {
            // Now that we know we have at least one item, allocate an initial storage array. This is not
            // the array we'll yield.  It starts out small in order to avoid significantly overallocating
            // when the source has many fewer elements than the chunk size.
            var arraySize = Math.Min(size, 4);
            int i;

            do
            {
                var array = new T[arraySize];

                // Store the first item
                array[0] = enumerator.Current;
                i = 1;

                if (size != array.Length)
                {
                    // This is the first chunk. As we fill the array, grow it as needed.
                    for (; i < size && await enumerator.MoveNextAsync(cancellationToken); i++)
                    {
                        if (i >= array.Length)
                        {
                            arraySize = (int)Math.Min((uint)size, 2 * (uint)array.Length);
                            Array.Resize(ref array, arraySize);
                        }

                        array[i] = enumerator.Current;
                    }
                }
                else
                {
                    // For all but the first chunk, the array will already be correctly sized.
                    // We can just store into it until either it's full or MoveNext returns false.
                    var local = array; // avoid bounds checks by using cached local (`array` is lifted to iterator object as a field)
                    Debug.Assert(local.Length == size);
                    for (; (uint)i < (uint)local.Length && await enumerator.MoveNextAsync(cancellationToken); i++)
                    {
                        local[i] = enumerator.Current;
                    }
                }

                if (i != array.Length)
                {
                    Array.Resize(ref array, i);
                }

                yield return array;
            }
            while (i >= size && await enumerator.MoveNextAsync(cancellationToken));
        }
    }

    public static async IAsyncEnumerable<TResult> FullJoin<T1, T2, TKey, TResult>(
        this IAsyncEnumerable<T1> first,
        IAsyncEnumerable<T2> second,
        Func<T1, ValueTask<TKey>> firstKeySelector,
        Func<T2, ValueTask<TKey>> secondKeySelector,
        Func<T1, ValueTask<TResult>> firstResultSelector,
        Func<T2, ValueTask<TResult>> secondResultSelector,
        Func<T1, T2, ValueTask<TResult>> bothResultSelector,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var secondLookup = await second.ToLookupAwaitAsync(secondKeySelector, cancellationToken);
        var keys = new System.Collections.Generic.HashSet<TKey>();

        await foreach (var firstItem in first.WithCancellation(cancellationToken))
        {
            var firstKey = await firstKeySelector(firstItem);
            keys.Add(firstKey);

            if (secondLookup.Contains(firstKey))
            {
                var secondItems = secondLookup[firstKey];

                foreach (var secondItem in secondItems)
                {
                    yield return await bothResultSelector(firstItem, secondItem);
                }
            }
            else
            {
                yield return await firstResultSelector(firstItem);
            }
        }

        foreach (var group in secondLookup)
        {
            if (keys.Contains(group.Key) is false)
            {
                foreach (var secondItem in group)
                {
                    yield return await secondResultSelector(secondItem);
                }
            }
        }
    }
}

public static class KeyValuePairExtensions
{
    /// <summary>
    /// Applies <paramref name="f"/> to each value and filters out <seealso cref="Option.None"/> values.
    /// </summary>
    public static IEnumerable<KeyValuePair<TKey, TValue2>> ChooseValues<TKey, TValue, TValue2>(
        this IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs,
        Func<TValue, Option<TValue2>> f) =>
        keyValuePairs.Choose(kvp => from value2 in f(kvp.Value)
                                    select KeyValuePair.Create(kvp.Key, value2));

    /// <summary>
    /// Applies <paramref name="f"/> to each key and filters out <seealso cref="Option.None"/> keys.
    /// </summary>
    public static IEnumerable<KeyValuePair<TKey2, TValue>> ChooseKeys<TKey, TKey2, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs,
        Func<TKey, Option<TKey2>> f) =>
        keyValuePairs.Choose(kvp => from key2 in f(kvp.Key)
                                    select KeyValuePair.Create(key2, kvp.Value));

    /// <summary>
    /// Creates a new key value pair whose value is <paramref name="f"/>(<paramref name="keyValuePair"/>.Value).
    /// </summary>
    public static KeyValuePair<TKey, TValue2> MapValue<TKey, TValue, TValue2>(
        this KeyValuePair<TKey, TValue> keyValuePair,
        Func<TValue, TValue2> f) =>
        KeyValuePair.Create(keyValuePair.Key, f(keyValuePair.Value));

    /// <summary>
    /// Creates a new key value pair whose key is <paramref name="f"/>(<paramref name="keyValuePair"/>.Key).
    /// </summary>
    public static KeyValuePair<TKey2, TValue> MapKey<TKey, TKey2, TValue>(
        this KeyValuePair<TKey, TValue> keyValuePair,
        Func<TKey, TKey2> f) =>
        KeyValuePair.Create(f(keyValuePair.Key), keyValuePair.Value);

    /// <summary>
    /// Applies <paramref name="f"/> to each key
    /// </summary>
    public static IEnumerable<KeyValuePair<TKey2, TValue>> MapKey<TKey, TKey2, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> enumerable,
        Func<TKey, TKey2> f) =>
        enumerable.Select(kvp => kvp.MapKey(f));

    /// <summary>
    /// Applies <paramref name="f"/> to each value
    /// </summary>
    public static IEnumerable<KeyValuePair<TKey, TValue2>> MapValue<TKey, TValue, TValue2>(
        this IEnumerable<KeyValuePair<TKey, TValue>> enumerable,
        Func<TValue, TValue2> f) =>
        enumerable.Select(kvp => kvp.MapValue(f));

    /// <summary>
    /// Removes keys where the predicate is false
    /// </summary>
    public static IEnumerable<KeyValuePair<TKey, TValue>> WhereKey<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> enumerable,
        Func<TKey, bool> predicate) =>
        enumerable.Where(kvp => predicate(kvp.Key));

    /// <summary>
    /// Removes values where the predicate is false
    /// </summary>
    public static IEnumerable<KeyValuePair<TKey, TValue>> WhereValue<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> enumerable,
        Func<TValue, bool> predicate) =>
        enumerable.Where(kvp => predicate(kvp.Value));
}

public static class DictionaryExtensions
{
    public static Option<TValue> Find<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) =>
        dictionary.TryGetValue(key, out var value)
            ? Option<TValue>.Some(value)
            : Option<TValue>.None;
}