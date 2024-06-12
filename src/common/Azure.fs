namespace common

open Azure

[<RequireQualifiedAccess>]
module AzureETag =
    let fromString value = ETag value

    let toString (value: ETag) = value.ToString()

    let fromCosmosJsonObject jsonObject =
        JsonObject.getStringProperty jsonObject "_etag" |> ETag
