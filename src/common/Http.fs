namespace common

open System
open Flurl

[<RequireQualifiedAccess>]
module Uri =
    let appendPathToUri (uri: Uri) path =
        uri.AppendPathSegment(path).ToUri()