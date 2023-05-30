using CommunityToolkit.Diagnostics;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace EPizzas.Common;

public static class OptionExtensions
{
    public static T IfNoneThrow<T>(this Option<T> option, string errorMessage)
    {
        return option.IfNone(() => throw new InvalidOperationException(errorMessage));
    }

    public static async ValueTask Iter<T>(this Option<T> option, Func<T, ValueTask> action)
    {
        await option.Match(async t => await action(t), async () => await ValueTask.CompletedTask);
    }

    /// <summary>
    /// If <paramref name="option"/> has a value, apply <paramref name="f"/> to the value.
    /// If the result is null, return an empty option; otherwise, return an option with it.
    /// </summary>
    public static Option<T2> BindNullable<T, T2>(this Option<T> option, Func<T, T2?> f) =>
        option.Bind(t => f(t) switch
        {
            null => Option<T2>.None,
            var t2 => Option<T2>.Some(t2) // Not sure if necessary, but we use a cast to ensure that nullable value types are converted to their inner value.
        });
}

public static class EitherExtensions
{
    public static EitherAsync<TLeft, TRight> ToAsync<TLeft, TRight>(this ValueTask<Either<TLeft, TRight>> task)
    {
        return task.AsTask().ToAsync();
    }
}

public static class IEnumerableExtensions
{
    public static async ValueTask Iter<T>(this IEnumerable<T> enumerable, Func<T, ValueTask> action, CancellationToken cancellationToken)
    {
        await enumerable.IterParallel(action, maxDegreeOfParallelism: 1, cancellationToken);
    }

    public static async ValueTask IterParallel<T>(this IEnumerable<T> enumerable, Func<T, ValueTask> action, CancellationToken cancellationToken)
    {
        await enumerable.IterParallel(action, maxDegreeOfParallelism: -1, cancellationToken);
    }

    public static async ValueTask IterParallel<T>(this IEnumerable<T> enumerable, Func<T, ValueTask> action, int maxDegreeOfParallelism, CancellationToken cancellationToken)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(enumerable, options, async (t, _) => await action(t));
    }
}

public static class IAsyncEnumerableExtensions
{
    public static async ValueTask<Seq<T>> ToSeq<T>(this IAsyncEnumerable<T> enumerable, CancellationToken cancellationToken)
    {
        return await enumerable.ToListAsync(cancellationToken)
                               .Map(items => items.ToSeq());
    }

    public static IAsyncEnumerable<T> Tap<T>(this IAsyncEnumerable<T> enumerable, Action<T> action)
    {
        return enumerable.Select(t =>
        {
            action(t);
            return t;
        });
    }

    public static async ValueTask Iter<T>(this IAsyncEnumerable<T> enumerable, Func<T, ValueTask> action, CancellationToken cancellationToken)
    {
        await enumerable.IterParallel(action, maxDegreeOfParallelism: 1, cancellationToken);
    }

    public static async ValueTask IterParallel<T>(this IAsyncEnumerable<T> enumerable, Func<T, ValueTask> action, CancellationToken cancellationToken)
    {
        await enumerable.IterParallel(action, maxDegreeOfParallelism: -1, cancellationToken);
    }

    public static async ValueTask IterParallel<T>(this IAsyncEnumerable<T> enumerable, Func<T, ValueTask> action, int maxDegreeOfParallelism, CancellationToken cancellationToken)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(enumerable, options, async (t, _) => await action(t));
    }

    public static async ValueTask<Option<T>> HeadOrNone<T>(this IAsyncEnumerable<T> enumerable, CancellationToken cancellationToken)
    {
        var predicate = (T t) => true;

        return await enumerable.HeadOrNone(predicate, cancellationToken);
    }

    public static async ValueTask<Option<T>> HeadOrNone<T>(this IAsyncEnumerable<T> enumerable, Func<T, bool> predicate, CancellationToken cancellationToken)
    {
        Guard.IsNotNull(predicate);

        await using (var enumerator = enumerable.GetCancelableEnumerator(cancellationToken))
        {
            while (await enumerator.MoveNextAsync())
            {
                var value = enumerator.Current;

                if (predicate(value))
                {
                    return Option<T>.Some(value);
                }
            }
        }

        return Option<T>.None;
    }

    public static IAsyncEnumerable<T2> Choose<T1, T2>(this IAsyncEnumerable<T1> enumerable, Func<T1, ValueTask<Option<T2>>> selector)
    {
        return enumerable.SelectAwait(selector)
                         .Where(option => option.IsSome)
                         .Select(option => option.ValueUnsafe());
    }

    private static ConfiguredCancelableAsyncEnumerable<T>.Enumerator GetCancelableEnumerator<T>(this IAsyncEnumerable<T> enumerable, CancellationToken cancellationToken)
    {
        return enumerable.WithCancellation(cancellationToken).GetAsyncEnumerator();
    }
}

public static class IDictionaryExtensions
{
    public static Option<TValue> Find<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    {
        Guard.IsNotNull(dictionary);

        return dictionary.TryGetValue(key, out var value)
                ? value
                : Option<TValue>.None;
    }
}

public static class AffExtensions
{
    public static async ValueTask<T> RunAndThrowIfFail<T>(this Aff<T> aff)
    {
        var result = await aff.Run();

        return result.ThrowIfFail();
    }

    public static Aff<T> Catch<T, TError>(this Aff<T> aff, Func<TError, T> f) where TError : Error
    {
        return aff.Catch(error => error is TError tError
                                    ? Prelude.SuccessAff(f(tError))
                                    : Prelude.FailAff<T>(error));
    }
}