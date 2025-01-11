namespace api

open Oxpecker
open FSharp.Control
open common

[<RequireQualifiedAccess>]
module internal EndpointHandler =
    let fromResult result : EndpointHandler =
        fun context ->
            task {
                let! result = Async.startAsTaskWithToken context.RequestAborted result
                return! context.Write(result)
            }

    let fromContext f : EndpointHandler =
        fun context ->
            task {
                let! result = f context |> Async.startAsTaskWithToken context.RequestAborted
                return! context.Write(result)
            }
