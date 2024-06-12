namespace V1.Orders

open Giraffe.EndpointRouting

[<RequireQualifiedAccess>]
module Services =
    let configure services =
        Common.Services.configure services
        GetById.Services.configure services
        Cancel.Services.configure services
        List.Services.configure services
        Create.Services.configure services

[<RequireQualifiedAccess>]
module Endpoints =
    let list =
        [ subRoute
              "/orders"
              (GetById.Endpoints.list
               @ Cancel.Endpoints.list
               @ List.Endpoints.list
               @ Create.Endpoints.list) ]
