[<AutoOpen>]
module internal ApiErrorCode

open System

type ApiErrorCode =
    | ResourceNotFound
    | InvalidRequestParameter
    | PreconditionFailed
    | InvalidHeader
    | ResourceAlreadyExists
    | InvalidRequestBody

    static member toString value =
        match value with
        | ResourceNotFound -> nameof ResourceNotFound
        | InvalidRequestParameter -> nameof InvalidRequestParameter
        | PreconditionFailed -> nameof PreconditionFailed
        | InvalidHeader -> nameof InvalidHeader
        | ResourceAlreadyExists -> nameof ResourceAlreadyExists
        | InvalidRequestBody -> nameof InvalidRequestBody

    static member fromString value =
        match value with
        | _ when String.Equals(value, nameof ResourceNotFound, StringComparison.OrdinalIgnoreCase) -> ResourceNotFound
        | _ when String.Equals(value, nameof InvalidRequestParameter, StringComparison.OrdinalIgnoreCase) -> InvalidRequestParameter
        | _ when String.Equals(value, nameof PreconditionFailed, StringComparison.OrdinalIgnoreCase) -> PreconditionFailed
        | _ when String.Equals(value, nameof InvalidHeader, StringComparison.OrdinalIgnoreCase) -> InvalidHeader
        | _ when String.Equals(value, nameof ResourceAlreadyExists, StringComparison.OrdinalIgnoreCase) -> ResourceAlreadyExists
        | _ when String.Equals(value, nameof InvalidRequestBody, StringComparison.OrdinalIgnoreCase) -> InvalidRequestBody
        | _ -> invalidOp ($"'{value}' is not a valid API error code.")
