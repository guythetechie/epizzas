[<RequireQualifiedAccess>]
module common.JsonValue

open System
open System.Text.Json.Nodes
open System.Text.Json
open FSharpPlus

let private getString (jsonValue: JsonValue) = jsonValue.GetValue<obj>() |> string

let asString (jsonValue: JsonValue) =
    match jsonValue.GetValueKind() with
    | JsonValueKind.String -> getString jsonValue |> JsonResult.succeed
    | _ -> JsonResult.failWithMessage "JSON value is not a string"

let asInt (jsonValue: JsonValue) =
    match jsonValue.GetValueKind() with
    | JsonValueKind.Number ->
        match getString jsonValue |> Int32.TryParse with
        | true, x -> JsonResult.succeed x
        | _ -> JsonResult.failWithMessage "JSON value is not an integer"
    | _ -> JsonResult.failWithMessage "JSON value is not a number"

let asAbsoluteUri jsonValue =
    let errorMessage = "JSON value is not an absolute URI."

    monad {
        let! stringValue = asString jsonValue |> JsonResult.setErrorMessage errorMessage

        match Uri.TryCreate(stringValue, UriKind.Absolute) with
        | true, uri when
            (match uri with
             | Null -> false
             | NonNull nonNullUri -> nonNullUri.HostNameType <> UriHostNameType.Unknown)
            ->
            return uri
        | _ -> return! JsonResult.failWithMessage errorMessage
    }

let asGuid jsonValue =
    let errorMessage = "JSON value is not a GUID."

    monad {
        let! stringValue = asString jsonValue |> JsonResult.setErrorMessage errorMessage

        match Guid.TryParse(stringValue) with
        | true, guid -> return guid
        | _ -> return! JsonResult.failWithMessage errorMessage
    }

let asBool (jsonValue: JsonValue) =
    match jsonValue.GetValueKind() with
    | JsonValueKind.True -> JsonResult.succeed true
    | JsonValueKind.False -> JsonResult.succeed false
    | _ -> JsonResult.failWithMessage "JSON value is not a boolean."

let asDateTimeOffset (jsonValue: JsonValue) =
    let errorMessage = "JSON value is not a date time offset."

    match jsonValue.TryGetValue<DateTimeOffset>() with
    | true, dateTimeOffset -> JsonResult.succeed dateTimeOffset
    | _ ->
        monad {
            let! stringValue = asString jsonValue |> JsonResult.setErrorMessage errorMessage

            match DateTimeOffset.TryParse(stringValue) with
            | true, dateTimeOffset -> return dateTimeOffset
            | _ -> return! JsonResult.failWithMessage errorMessage
        }
