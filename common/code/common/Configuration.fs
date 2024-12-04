[<RequireQualifiedAccess>]
module common.Configuration

open Microsoft.Extensions.Configuration

let private getSection (configuration: IConfiguration) (key: string) =
    let section = configuration.GetSection(key)
    if section.Exists() then Some section else None

let getValue configuration key =
    getSection configuration key
    |> Option.bind (fun section ->
        match section.Value with
        | Null -> None
        | NonNull value -> Some value)

let getValueOrThrow configuration key =
    getValue configuration key
    |> Option.defaultWith (fun () -> failwithf $"Could not find value for key '{key}'.")
