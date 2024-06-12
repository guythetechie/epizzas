[<RequireQualifiedAccess>]
module common.HealthCheck

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Diagnostics.HealthChecks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Diagnostics.HealthChecks
open Microsoft.Extensions.Hosting

let configureServices (services: IServiceCollection) =
    services
        .AddHealthChecks()
        .AddCheck("self", (fun () -> HealthCheckResult.Healthy()), [| "live" |])
    |> ignore

let configureApplication (application: WebApplication) =
    if application.Environment.IsDevelopment() then
        application.MapHealthChecks("/health") |> ignore

        let options = new HealthCheckOptions()
        options.Predicate <- fun registration -> registration.Tags.Contains("live")

        application.MapHealthChecks("/alive", options) |> ignore
