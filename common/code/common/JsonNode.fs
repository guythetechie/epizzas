[<RequireQualifiedAccess>]
module common.JsonNode

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

let asJsonObject (node: JsonNode | null) =
    match node with
    | :? JsonObject as jsonObject -> JsonResult.succeed jsonObject
    | Null -> JsonResult.failWithMessage "JSON node is null."
    | _ -> JsonResult.failWithMessage "JSON node is not a JSON object."

let asJsonArray (node: JsonNode | null) =
    match node with
    | :? JsonArray as jsonArray -> JsonResult.succeed jsonArray
    | Null -> JsonResult.failWithMessage "JSON node is null."
    | _ -> JsonResult.failWithMessage "JSON node is not a JSON array."

let asJsonValue (node: JsonNode | null) =
    match node with
    | :? JsonValue as jsonValue -> JsonResult.succeed jsonValue
    | Null -> JsonResult.failWithMessage "JSON node is null."
    | _ -> JsonResult.failWithMessage "JSON node is not a JSON value."

let fromStream (stream: Stream | null) =
    async {
        let! cancellationToken = Async.CancellationToken

        match stream with
        | Null -> return JsonResult.failWithMessage "Stream is null."
        | NonNull stream ->
            try
                match!
                    JsonNode.ParseAsync(stream, cancellationToken = cancellationToken)
                    |> Async.AwaitTask
                with
                | Null -> return JsonResult.failWithMessage "Stream is null or is not a valid JSON."
                | NonNull node -> return JsonResult.succeed node
            with
            | :? JsonException as jsonException -> return JsonResult.failWithMessage jsonException.Message
            | :? AggregateException as aggregateException ->
                return
                    aggregateException.Flatten().InnerExceptions
                    |> Seq.choose (function
                        | :? JsonException as jsonException -> Some jsonException
                        | _ -> None)
                    |> List.ofSeq
                    |> function
                        | [] -> raise aggregateException
                        | jsonExceptions ->
                            jsonExceptions |> Seq.map _.Message |> JsonError.fromMessages |> JsonResult.fail
            | exn -> return raise exn
    }
