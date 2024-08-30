using LanguageExt;
using System;
using System.Threading.Tasks;

namespace common;

public static class Utilities
{
    public static Eff<A> UseEff<A>(Func<Eff<A>> f) where A : IDisposable =>
        Prelude.use(Prelude.liftEff(f).Flatten()).As();

    public static Eff<A> UseEff<A>(Func<Task<A>> f) where A : IDisposable =>
        UseEff(() => Prelude.liftEff(async () => await f()));
}