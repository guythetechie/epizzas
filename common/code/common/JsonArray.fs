[<RequireQualifiedAccess>]
module common.JsonArray

open FSharpPlus
open System.Text.Json.Nodes

let fromSeq items =
    items |> Seq.map (fun x -> x :> JsonNode | null) |> Array.ofSeq |> JsonArray

let private getResultSeq toJsonResult toError (jsonArray: JsonArray) =
    jsonArray
    |> traversei (fun index node -> toJsonResult node |> JsonResult.mapError (fun _ -> toError index))

let getJsonObjects jsonArray =
    getResultSeq JsonNode.asJsonObject (fun index -> JsonError.fromMessage $"Element at index {index} is not a JSON object.") jsonArray

let getJsonArrays jsonArray =
    getResultSeq JsonNode.asJsonArray (fun index -> JsonError.fromMessage $"Element at index {index} is not a JSON array.") jsonArray

let getJsonValues jsonArray =
    getResultSeq JsonNode.asJsonValue (fun index -> JsonError.fromMessage $"Element at index {index} is not a JSON value.") jsonArray
