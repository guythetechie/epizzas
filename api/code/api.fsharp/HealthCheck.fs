[<RequireQualifiedAccess>]
module HealthCheck

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Diagnostics.HealthChecks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Diagnostics.HealthChecks

let configureBuilder (builder: IHostApplicationBuilder) =
    builder.Services
        .AddHealthChecks()
        .AddCheck("self", (fun () -> HealthCheckResult.Healthy()), [ "live" ])
    |> ignore

let configureApplication (application: WebApplication) =
    if application.Environment.IsDevelopment() then
        let _ = application.MapHealthChecks("/health")

        let liveOptions = new HealthCheckOptions()
        liveOptions.Predicate <- fun registration -> registration.Tags.Contains("live")

        let _ = application.MapHealthChecks("/alive", liveOptions)

        ()
