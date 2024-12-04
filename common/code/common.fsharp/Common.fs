namespace common

open System

type ETag =
    private
    | ETag of string

    static member fromString value =
        match String.IsNullOrWhiteSpace value with
        | true -> Error "ETag cannot be null or whitespace."
        | false -> ETag value |> Ok

    static member generate() = Guid.NewGuid().ToString() |> ETag

    static member All = ETag "\"*\""

    static member toString(ETag value) = value

type ContinuationToken =
    private
    | ContinuationToken of string

    static member fromString value =
        match String.IsNullOrWhiteSpace value with
        | true -> Error "Continuation token cannot be null or whitespace."
        | false -> ContinuationToken value |> Ok

    static member toString(ContinuationToken value) = value
