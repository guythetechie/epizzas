using EPizzas.Common;
using FsCheck;
using FsCheck.Fluent;
using System;

namespace EPizzas.Ordering.Api.Tests;

public static class CommonGenerator
{
    public static Gen<ETag> ETag { get; } =
        from eTag in Generator.GenerateDefault<Guid>()
        select new ETag(eTag.ToString());

    public static Gen<ContinuationToken> ContinuationToken { get; } =
        from token in Generator.GenerateDefault<Guid>()
        select new ContinuationToken(token.ToString());
}
