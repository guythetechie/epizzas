namespace common

open System

[<RequireQualifiedAccess>]
module String =
    let isNotNullOrWhiteSpace = String.IsNullOrWhiteSpace >> not