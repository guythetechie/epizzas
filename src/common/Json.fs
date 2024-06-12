namespace common

open System.Text.Json
open System.Text.Json.Nodes

[<RequireQualifiedAccess>]
module JsonArray =
    let fromSeq nodes =
        nodes |> Seq.map (fun node -> node :> JsonNode) |> Array.ofSeq |> JsonArray

[<RequireQualifiedAccess>]
module JsonValue =
    let tryToString (jsonValue: JsonValue) =
        match jsonValue with
        | null -> Error "JSON value is null."
        | _ ->
            match jsonValue.TryGetValue<string>() with
            | true, value -> Ok value
            | _ -> Error "JSON value is not a string."

[<RequireQualifiedAccess>]
module JsonNode =
    let tryToString (jsonNode: JsonNode) =
        match jsonNode with
        | null -> Error "JSON node is null."
        | :? JsonValue as jsonValue -> JsonValue.tryToString jsonValue
        | _ -> Error "JSON node is not a JSON value."

    let toString jsonNode =
        tryToString jsonNode
        |> Result.defaultWith (fun error -> raise (JsonException error))

    let tryToJsonObject (jsonNode: JsonNode) =
        match jsonNode with
        | null -> Error "JSON node is null."
        | :? JsonObject as jsonObject -> Ok jsonObject
        | _ -> Error "JSON node is not a JSON object."

    let toJsonObject jsonNode =
        tryToJsonObject jsonNode
        |> Result.defaultWith (fun error -> raise (JsonException error))

    let tryToJsonArray (jsonNode: JsonNode) =
        match jsonNode with
        | null -> Error "JSON node is null."
        | :? JsonArray as jsonArray -> Ok jsonArray
        | _ -> Error "JSON node is not a JSON array."

    let toJsonArray jsonNode =
        tryToJsonArray jsonNode
        |> Result.defaultWith (fun error -> raise (JsonException error))

[<RequireQualifiedAccess>]
module JsonObject =
    open System.Collections.Generic

    let tryGetProperty (jsonObject: JsonObject) (propertyName: string) =
        match jsonObject with
        | null -> Error "JSON object is null."
        | _ ->
            match jsonObject.TryGetPropertyValue(propertyName) with
            | true, node ->
                if isNull node then
                    Error $"Property '{propertyName}' is null."
                else
                    Ok node
            | _ -> Error $"Property '{propertyName}' is missing."

    let getProperty jsonObject propertyName =
        tryGetProperty jsonObject propertyName
        |> Result.defaultWith (fun error -> raise (JsonException error))

    let private bindPropertyError propertyName result =
        result
        |> Result.mapError (fun error -> $"Property '{propertyName}' is invalid. {error}")

    let tryGetStringProperty jsonObject propertyName =
        tryGetProperty jsonObject propertyName
        |> Result.bind (JsonNode.tryToString >> bindPropertyError propertyName)

    let getStringProperty jsonObject propertyName =
        tryGetStringProperty jsonObject propertyName
        |> Result.defaultWith (fun error -> raise (JsonException error))

    let tryGetJsonObjectProperty jsonObject propertyName =
        tryGetProperty jsonObject propertyName
        |> Result.bind (JsonNode.tryToJsonObject >> bindPropertyError propertyName)

    let getJsonObjectProperty jsonObject propertyName =
        tryGetJsonObjectProperty jsonObject propertyName
        |> Result.defaultWith (fun error -> raise (JsonException error))

    let tryGetJsonArrayProperty jsonObject propertyName =
        tryGetProperty jsonObject propertyName
        |> Result.bind (JsonNode.tryToJsonArray >> bindPropertyError propertyName)

    let getJsonArrayProperty jsonObject propertyName =
        tryGetJsonArrayProperty jsonObject propertyName
        |> Result.defaultWith (fun error -> raise (JsonException error))

    let fromSeq pairs =
        pairs |> Seq.map KeyValuePair.Create |> JsonObject
