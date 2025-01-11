﻿[<RequireQualifiedAccess>]
module common.JsonObject

open FSharpPlus
open System.Text.Json.Nodes

let getProperty propertyName (jsonObject: JsonObject | null) =
    match jsonObject with
    | null -> JsonResult.failWithMessage "JSON object is null."
    | jsonObject ->
        match jsonObject.TryGetPropertyValue(propertyName) with
        | true, property ->
            match property with
            | null -> JsonResult.failWithMessage $"Property '{propertyName}' is null."
            | property -> JsonResult.succeed property
        | _ -> JsonResult.failWithMessage $"Property '{propertyName}' is missing."

let getOptionalProperty propertyName jsonObject =
    getProperty propertyName jsonObject
    |> map Option.Some
    |> JsonResult.defaultWith (fun _ -> Option.None)

let getPropertyFromResult getPropertyResult propertyName jsonObject =
    let updateErrorMessage error =
        let currentMessage = JsonError.getMessage error
        let newMessage = $"Property '{propertyName}' is invalid. {currentMessage}"
        JsonError.setMessage newMessage error

    getProperty propertyName jsonObject
    |> bind (getPropertyResult >> JsonResult.mapError updateErrorMessage)

let getJsonObjectProperty propertyName jsonObject =
    getPropertyFromResult JsonNode.asJsonObject propertyName jsonObject

let getJsonArrayProperty propertyName jsonObject =
    getPropertyFromResult JsonNode.asJsonArray propertyName jsonObject

let getJsonValueProperty propertyName jsonObject =
    getPropertyFromResult JsonNode.asJsonValue propertyName jsonObject

let getStringProperty propertyName jsonObject =
    let getPropertyResult = JsonNode.asJsonValue >> JsonResult.bind JsonValue.asString
    getPropertyFromResult getPropertyResult propertyName jsonObject

let getAbsoluteUriProperty propertyName jsonObject =
    let getPropertyResult =
        JsonNode.asJsonValue >> JsonResult.bind JsonValue.asAbsoluteUri

    getPropertyFromResult getPropertyResult propertyName jsonObject

let getGuidProperty propertyName jsonObject =
    let getPropertyResult = JsonNode.asJsonValue >> JsonResult.bind JsonValue.asGuid
    getPropertyFromResult getPropertyResult propertyName jsonObject

let getBoolProperty propertyName jsonObject =
    let getPropertyResult = JsonNode.asJsonValue >> JsonResult.bind JsonValue.asBool
    getPropertyFromResult getPropertyResult propertyName jsonObject

let getIntProperty propertyName jsonObject =
    let getPropertyResult = JsonNode.asJsonValue >> JsonResult.bind JsonValue.asInt
    getPropertyFromResult getPropertyResult propertyName jsonObject

let setProperty (propertyName: string) (propertyValue: JsonNode | null) (jsonObject: JsonObject) =
    jsonObject[propertyName] <- propertyValue
    jsonObject

let removeProperty (propertyName: string) (jsonObject: JsonObject) =
    jsonObject.Remove(propertyName) |> ignore
    jsonObject
