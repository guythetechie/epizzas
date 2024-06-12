[<RequireQualifiedAccess>]
module Configuration

open Microsoft.Extensions.Configuration
open System
open System.Collections.Generic

let tryGetSection (configuration: IConfiguration) key =
    let section = configuration.GetSection key

    if section.Exists() then Some section else None

let tryGetValue configuration key =
    match tryGetSection configuration key with
    | Some section when String.IsNullOrWhiteSpace(section.Value) = false -> Some section.Value
    | _ -> None

let getValue configuration key =
    tryGetValue configuration key
    |> Option.defaultWith (fun () -> raise (KeyNotFoundException($"Could not find key '{key}' in configuration.")))
