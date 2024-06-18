namespace V1

open Microsoft.AspNetCore.Routing
open Microsoft.AspNetCore.Builder

[<RequireQualifiedAccess>]
module Services =
    let configure services = services |> Orders.Services.configure

[<RequireQualifiedAccess>]
module Endpoints =
    let configure (builder: IEndpointRouteBuilder) =
        builder.MapGroup("/v1") |> Orders.Endpoints.configure
