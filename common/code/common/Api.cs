using LanguageExt;
using LanguageExt.Traits;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

using static LanguageExt.Prelude;

namespace common;

internal sealed record ApiOperation<T> : K<ApiOperation, T>
{
    internal readonly EitherT<IResult, Eff, T> value;

    internal ApiOperation(EitherT<IResult, Eff, T> value) => this.value = value;

    internal Eff<T2> Match<T2>(Func<IResult, T2> left, Func<T, T2> right) =>
        value.Match(left, right).As();

    internal ApiOperation<T2> Map<T2>(Func<T, T2> map) =>
        new(value.Map(map));

    internal ApiOperation<T2> Bind<T2>(Func<T, ApiOperation<T2>> bind) =>
        new(value.Bind(x => bind(x).As().value));
}

public class ApiOperation :
    Monad<ApiOperation>,
    Choice<ApiOperation>
{
    public static K<ApiOperation, A> Pure<A>(A value) =>
        new ApiOperation<A>(Prelude.Pure(value));

    public static K<ApiOperation, A> Lift<A>(ValueTask<A> value) =>
        new ApiOperation<A>(EitherT<IResult, Eff, A>.LiftIO(liftIO(async () => await value)));

    public static K<ApiOperation, A> Lift<A>(Either<IResult, A> value) =>
        new ApiOperation<A>(EitherT<IResult, Eff, A>.Lift(value));

    public static K<ApiOperation, A> Lift<A>(ValueTask<Either<IResult, A>> value) =>
        new ApiOperation<A>(EitherT<IResult, Eff, A>.LiftIO(liftIO(async () => await value)));

    public static K<ApiOperation, B> Apply<A, B>(K<ApiOperation, Func<A, B>> mf, K<ApiOperation, A> ma) =>
        mf.Bind(ma.Map);

    public static K<ApiOperation, B> Bind<A, B>(K<ApiOperation, A> ma, Func<A, K<ApiOperation, B>> f) =>
        ma.As().Bind(a => f(a).As());

    public static K<ApiOperation, B> Map<A, B>(Func<A, B> f, K<ApiOperation, A> ma) =>
        ma.As().Map(f);

    public static K<ApiOperation, A> Choose<A>(K<ApiOperation, A> fa, K<ApiOperation, A> fb) =>
        new ApiOperation<A>(fa.As().value.Choose(fb.As().value).As());
}

public static class ApiOperationExtensions
{
    internal static ApiOperation<A> As<A>(this K<ApiOperation, A> ma) =>
        (ApiOperation<A>)ma;

    public static async ValueTask<IResult> Run(
        this K<ApiOperation, IResult> operation,
        CancellationToken cancellationToken) =>
        await operation.As()
                       .Match(identity, identity)
                       .RunUnsafeAsync(EnvIO.New(token: cancellationToken));
}