[<RequireQualifiedAccess>]
module common.JsonArray

open FSharpPlus
open System.Text.Json.Nodes

let fromSeq items =
    items |> Seq.map (fun x -> x :> JsonNode) |> Array.ofSeq |> JsonArray

let private getResultSeq toJsonResult toErrorMessage (jsonArray: JsonArray) =
    jsonArray
    |> traversei (fun index node -> toJsonResult node |> JsonResult.setErrorMessage (toErrorMessage index))

let getJsonObjects jsonArray =
    getResultSeq JsonNode.asJsonObject (fun index -> $"Element at index {index} is not a JSON object.") jsonArray

let getJsonArrays jsonArray =
    getResultSeq JsonNode.asJsonArray (fun index -> $"Element at index {index} is not a JSON array.") jsonArray

let getJsonValues jsonArray =
    getResultSeq JsonNode.asJsonValue (fun index -> $"Element at index {index} is not a JSON value.") jsonArray
