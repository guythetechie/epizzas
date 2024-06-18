namespace V1.Orders

open Microsoft.AspNetCore.Routing
open Microsoft.AspNetCore.Builder

[<RequireQualifiedAccess>]
module Services =
    let configure services =
        services |> Common.Services.configure
        GetById.Services.configure services
        Cancel.Services.configure services
        List.Services.configure services
        Create.Services.configure services
        services

[<RequireQualifiedAccess>]
module Endpoints =
    let configure (builder: IEndpointRouteBuilder) = builder.MapGroup("/orders")
