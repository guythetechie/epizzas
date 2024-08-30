using LanguageExt;
using LanguageExt.Traits;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace common;

public sealed record ApiOperation<A> : K<ApiOperation, A>
{
    internal Eff<Either<IResult, A>> Value { get; }

    internal ApiOperation(Eff<Either<IResult, A>> value) => Value = value;
}

internal class ApiOperation : Monad<ApiOperation>, Alternative<ApiOperation>
{
    public static ApiOperation<A> Lift<A>(IResult result) =>
        Lift(Either<IResult, A>.Left(result));

    public static ApiOperation<A> Lift<A>(Func<ValueTask<IResult>> f) =>
        Lift<A>(() => f().AsTask());

    public static ApiOperation<A> Lift<A>(Func<Task<IResult>> f) =>
        new(Eff<Either<IResult, A>>.LiftIO(async () =>
        {
            var result = await f();
            return Either<IResult, A>.Left(result);
        }));

    public static ApiOperation<A> Lift<A>(Either<IResult, A> either) =>
        Lift(Eff<Either<IResult, A>>.Pure(either));

    public static ApiOperation<A> Lift<A>(IO<A> io) =>
        Lift(io.Map(Either<IResult, A>.Right));

    public static ApiOperation<A> Lift<A>(IO<Either<IResult, A>> io) =>
        Lift(Eff<Either<IResult, A>>.LiftIO(io));

    public static ApiOperation<A> Lift<A>(Eff<A> eff) =>
        Lift(eff.Map(Either<IResult, A>.Right));

    public static ApiOperation<A> Lift<A>(Eff<Either<IResult, A>> eff) =>
        new(eff);

    public static K<ApiOperation, B> Bind<A, B>(K<ApiOperation, A> ma, Func<A, K<ApiOperation, B>> f) =>
        Lift(ma.As()
               .Value
               .Bind(either => either.Map(a => f(a).As().Value)
                                     .IfLeft(result => Lift<B>(result).Value)));

    public static K<ApiOperation, A> Pure<A>(A value) =>
        Lift(Either<IResult, A>.Right(value));

    public static K<ApiOperation, B> Apply<A, B>(K<ApiOperation, Func<A, B>> mf, K<ApiOperation, A> ma) =>
        mf.Bind(ma.Map);

    public static K<ApiOperation, B> Map<A, B>(Func<A, B> f, K<ApiOperation, A> ma) =>
        ma.Bind(a => Pure(f(a)));

    public static K<ApiOperation, A> Empty<A>() =>
        Lift(IO<A>.Empty);

    public static K<ApiOperation, A> Combine<A>(K<ApiOperation, A> lhs, K<ApiOperation, A> rhs) =>
        Lift(lhs.As().Value.Combine(rhs.As().Value).As());
}

internal static class ApiOperationExtensions
{
    public static ApiOperation<A> As<A>(this K<ApiOperation, A> ma) =>
        (ApiOperation<A>)ma;

    public static async ValueTask<IResult> Run(this K<ApiOperation, IResult> operation, CancellationToken cancellationToken) =>
        await operation.As()
                       .Value
                       .Map(either => either.IfLeft(Prelude.identity))
                       .RunUnsafeAsync(EnvIO.New(token: cancellationToken));
}