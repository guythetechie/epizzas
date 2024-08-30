using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;
using System;
using System.Threading.Tasks;

namespace common;

public static class OptionTIO
{
    public static OptionT<IO, A> Lift<A>(Func<Task<A>> f) =>
        OptionT<IO, A>.LiftIO(IO.liftAsync(f));

    public static OptionT<IO, A> Lift<A>(Func<EnvIO, Task<A>> f) =>
        OptionT<IO, A>.LiftIO(IO.liftAsync(f));

    public static OptionT<IO, A> Use<A>(A a) where A : IDisposable =>
        OptionT<IO, A>.LiftIO(Prelude.use(() => a));

    public static OptionT<IO, A> Lift<A>(IO<Option<A>> io) =>
        OptionT<IO, A>.LiftIO(io);
}

public static class EitherTIO
{
    public static EitherT<L, IO, R> Lift<L, R>(Func<Task<R>> f) =>
        EitherT<L, IO, R>.LiftIO(IO.liftAsync(f));

    public static EitherT<L, IO, R> Lift<L, R>(Func<EnvIO, Task<R>> f) =>
        EitherT<L, IO, R>.LiftIO(IO.liftAsync(f));

    public static EitherT<L, IO, R> Use<L, R>(R r) where R : IDisposable =>
        EitherT<L, IO, R>.LiftIO(Prelude.use(() => r));

    public static EitherT<L, IO, R> Left<L, R>(L l) =>
        EitherT<L, IO, R>.Left(l);

    public static EitherT<L, IO, R> Right<L, R>(R r) =>
        EitherT<L, IO, R>.Right(r);
}

public static class EitherExtensions
{
    public static Validation<Error, R> ToValidation<R>(this Either<string, R> either) =>
        either.Map(Validation<Error, R>.Success)
              .IfLeft(error => Error.New(error));

    public static EitherT<L2, M, R> MapLeft<L1, L2, M, R>(this EitherT<L1, M, R> transformer, Func<L1, L2> f) where M : Monad<M> =>
        EitherT.lift<L2, M, Either<L2, R>>(from either in transformer.Run()
                                           select either.MapLeft(f))
               .Bind(Prelude.identity);
}

public static class OptionExtensions
{
    public static OptionT<IO, A> Catch<A>(this OptionT<IO, A> transformer, Predicate<Error> predicate, OptionT<IO, A> valueIfError) =>
        transformer.MapT(k => k.As()
                               .Try()
                               .Bind(fin => fin.Map(IO.Pure)
                                               .IfFail(error => IO.lift(() => predicate(error)
                                                                              ? valueIfError.Run().As().Run()
                                                                              : throw error))));
}

public static class IOExtensions
{
    public static IO<T> Use<T>(this IO<T> io) where T : IDisposable =>
        Prelude.use(io).As();
}