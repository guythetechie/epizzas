namespace common

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.DependencyInjection.Extensions
open System

[<RequireQualifiedAccess>]
module ServiceCollection =
    let tryAddKeyedSingleton<'a when 'a :> obj> (key: string) (services: IServiceCollection) (f: IServiceProvider -> 'a) =
        services.TryAddKeyedSingleton(typeof<'a>, key, Func<IServiceProvider, obj, obj>(fun provider _ -> f provider))

    let tryAddSingleton<'a when 'a :> obj> (services: IServiceCollection) (f: IServiceProvider -> 'a) =
        services.TryAddSingleton(typeof<'a>, Func<IServiceProvider, obj>(fun provider -> f provider))

[<RequireQualifiedAccess>]
module ServiceProvider =
    let getService<'a when 'a : not null> (provider: IServiceProvider) =
        match provider.GetService(typeof<'a>) with
        | :? 'a as value-> Some value
        | _ -> None

    let getServiceOrThrow<'a when 'a: not null> (provider: IServiceProvider) =
        provider.GetRequiredService<'a>()