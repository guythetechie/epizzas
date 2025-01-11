using common;
using LanguageExt;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace portal;

internal static class FSharpModule
{
    public static Option<T> ToOption<T>(this FSharpOption<T>? option) =>
        option is not null && FSharpOption<T>.get_IsSome(option)
            ? option.Value
            : Option<T>.None;

    public static Task<T> ToTask<T>(this FSharpAsync<T> async, CancellationToken cancellationToken) =>
        FSharpAsync.StartImmediateAsTask(async, cancellationToken);
}

internal static class JsonResultExtensions
{
    public static JsonResult<T2> Select<T1, T2>(this JsonResult<T1> jsonResult, Func<T1, T2> selector) =>
        JsonResult.map(FuncConvert.FromFunc(selector), jsonResult);

    public static JsonResult<TResult> SelectMany<T1, T2, TResult>(
        this JsonResult<T1> jsonResult,
        Func<T1, JsonResult<T2>> f,
        Func<T1, T2, TResult> selector
    )
    {
        Func<T1, JsonResult<TResult>> bindF = x => f(x).Select(t2 => selector(x, t2));
        return JsonResult.bind(FuncConvert.FromFunc(bindF), jsonResult);
    }

    public static JsonResult<ImmutableArray<T2>> Traverse<T, T2>(
        this IEnumerable<T?> enumerable,
        Func<T?, JsonResult<T2>> f
    ) where T : System.Text.Json.Nodes.JsonNode
    {
        var items = new List<T2>();

        foreach (var item in enumerable)
        {
            switch (f(item))
            {
                case JsonResult<T2>.Success success:
                    items.Add(success.Item);
                    break;
                case JsonResult<T2>.Failure failure:
                    return JsonResult.fail<ImmutableArray<T2>>(failure.Item);
            }
        }

        return JsonResult.succeed(items.ToImmutableArray());
    }

    public static T ThrowIfFail<T>(this JsonResult<T> jsonResult) =>
        JsonResult.throwIfFail(jsonResult);
}