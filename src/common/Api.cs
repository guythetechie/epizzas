using LanguageExt;
using LanguageExt.Traits;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace common;

internal sealed record ApiOperation<A>(EitherT<IResult, Eff, A> Value) : K<ApiOperation, A>
{
}

public class ApiOperation : Monad<ApiOperation>
{
    public static K<ApiOperation, A> Pure<A>(A value) =>
        new ApiOperation<A>(EitherT<IResult, Eff, A>.Lift(Prelude.Right(value)));

    public static K<ApiOperation, A> Lift<A>(IResult value) =>
        new ApiOperation<A>(EitherT<IResult, Eff, A>.Lift(Prelude.Left(value)));

    public static K<ApiOperation, A> Lift<A>(Either<IResult, A> value) =>
        new ApiOperation<A>(EitherT<IResult, Eff, A>.Lift(value));

    public static K<ApiOperation, A> Lift<A>(EitherT<IResult, Eff, A> value) =>
        new ApiOperation<A>(value);

    public static K<ApiOperation, B> Apply<A, B>(K<ApiOperation, Func<A, B>> mf, K<ApiOperation, A> ma) =>
        mf.Bind(ma.Map);

    public static K<ApiOperation, B> Bind<A, B>(K<ApiOperation, A> ma, Func<A, K<ApiOperation, B>> f) =>
        new ApiOperation<B>(ma.As().Value.Bind(a => f(a).As().Value));

    public static K<ApiOperation, B> Map<A, B>(Func<A, B> f, K<ApiOperation, A> ma) =>
        new ApiOperation<B>(ma.As().Value.Map(f));
}

public static class ApiOperationExtensions
{
    internal static ApiOperation<A> As<A>(this K<ApiOperation, A> ma) =>
        (ApiOperation<A>)ma;

    public static async ValueTask<IResult> Run(this K<ApiOperation, IResult> operation, CancellationToken cancellationToken) =>
        await operation.As()
                       .Value
                       .Run()
                       .As()
                       .Map(either => either.IfLeft(Prelude.identity))
                       .RunUnsafeAsync(EnvIO.New(token: cancellationToken));
}