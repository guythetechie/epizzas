namespace V1

open Giraffe.EndpointRouting
open Microsoft.AspNetCore.Routing
open Microsoft.AspNetCore.Builder

[<RequireQualifiedAccess>]
module Services =
    let configure services = Orders.Services.configure services

[<RequireQualifiedAccess>]
module Endpoints =
    let list = [ subRoute "/v1" Orders.Endpoints.list ]
    let configure (builder: IEndpointRouteBuilder) =
        builder.MapGroup("/v1")
