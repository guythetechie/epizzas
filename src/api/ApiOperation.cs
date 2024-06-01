using LanguageExt;
using LanguageExt.Traits;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace api;

internal sealed record ApiOperation<A> : K<ApiOperation, A>
{
    private readonly EitherT<IResult, IO, A> value;

    internal ApiOperation(EitherT<IResult, IO, A> value) => this.value = value;

    public EitherT<IResult, IO, A> ToEitherT() => value;
}

internal class ApiOperation : Monad<ApiOperation>, Alternative<ApiOperation>
{
    public static ApiOperation<A> LiftResult<A>(IResult result)
    {
        var transformer = EitherT.Left<IResult, IO, A>(result);
        return new(transformer);
    }

    public static ApiOperation<A> LiftResult<A>(ValueTask<IResult> task) =>
        LiftResult<A>(async () => await task);

    public static ApiOperation<A> LiftResult<A>(Func<ValueTask<IResult>> f)
    {
        var io = from result in Prelude.liftIO(async () => await f())
                 select Prelude.Left<IResult, A>(result);

        return LiftIO(io);
    }

    public static ApiOperation<A> LiftEither<A>(Either<IResult, A> either)
    {
        var transformer = EitherT.lift<IResult, IO, A>(either);
        return new(transformer);
    }

    public static ApiOperation<A> LiftEither<A>(ValueTask<Either<IResult, A>> task) =>
        LiftEither(() => task);

    public static ApiOperation<A> LiftEither<A>(Func<ValueTask<Either<IResult, A>>> f)
    {
        var io = Prelude.liftIO(async () => await f());
        return LiftIO(io);
    }

    public static ApiOperation<A> LiftEither<A>(EitherT<IResult, IO, A> transformer) =>
        new(transformer);

    public static ApiOperation<A> LiftIO<A>(IO<A> io)
    {
        var transformer = EitherT.liftIO<IResult, IO, A>(io);

        return new(transformer);
    }

    public static ApiOperation<A> LiftIO<A>(IO<Either<IResult, A>> IO)
    {
        var transformer = EitherT.lift<IResult, IO, Either<IResult, A>>(IO)
                                 .Bind(Prelude.identity);

        return new(transformer);
    }

    public static K<ApiOperation, B> Bind<A, B>(K<ApiOperation, A> ma, Func<A, K<ApiOperation, B>> f)
    {
        var transformer = ma.As().ToEitherT().Bind(a => f(a).As().ToEitherT());
        return new ApiOperation<B>(transformer);
    }

    public static K<ApiOperation, A> Pure<A>(A value)
    {
        var inner = Prelude.Pure(value);
        var transformer = EitherT.lift<IResult, IO, A>(inner);
        return new ApiOperation<A>(transformer);
    }

    public static K<ApiOperation, B> Apply<A, B>(K<ApiOperation, Func<A, B>> mf, K<ApiOperation, A> ma) =>
        mf.Bind(ma.Map);

    public static K<ApiOperation, B> Map<A, B>(Func<A, B> f, K<ApiOperation, A> ma) =>
        ma.Bind(a => Pure(f(a)));

    public static K<ApiOperation, A> Empty<A>()
    {
        var io = IO<Either<IResult, A>>.Empty;
        return LiftIO(io);
    }

    public static K<ApiOperation, A> Combine<A>(K<ApiOperation, A> lhs, K<ApiOperation, A> rhs)
    {
        var IO = lhs.As().ToEitherT().Run().As() | rhs.As().ToEitherT().Run().As();
        return LiftIO(IO);
    }
}

internal static class ApiResultExtensions
{
    public static ApiOperation<A> As<A>(this K<ApiOperation, A> ma) =>
        (ApiOperation<A>)ma;

    public static IResult Run(this K<ApiOperation, IResult> operation, CancellationToken cancellationToken) =>
        operation.As()
                 .ToEitherT()
                 .Run()
                 .As()
                 .Run(EnvIO.New(token: cancellationToken))
                 .IfLeft(Prelude.identity);
}