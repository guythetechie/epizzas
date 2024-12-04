module api.Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open common

let private configureBuilder builder =
    OpenTelemetry.configureBuilder builder
    HealthCheck.configureBuilder builder
    Oxpecker.configureBuilder builder

let private configureApplication (application: WebApplication) =
    let _ = application.UseHttpsRedirection()
    Oxpecker.configureApplication application

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureBuilder builder

    let application = builder.Build()
    let _ = configureApplication application

    application.Run()

    0 // Exit code
