namespace common

open Microsoft.Azure.Cosmos
open System
open System.IO
open System.Net
open System.Text.Json
open System.Text.Json.Nodes
open Azure
open FSharp.Control

type CosmosId =
    private
    | CosmosId of string

    static member fromString value =
        if String.IsNullOrWhiteSpace value then
            invalidOp "Cosmos ID cannot be empty."
        else
            CosmosId value

    static member toString(CosmosId value) = value

type CosmosRecord<'a> =
    { Id: CosmosId
      PartitionKey: PartitionKey
      Record: 'a
      ETag: ETag }

type ContinuationToken =
    private
    | ContinuationToken of string

    static member fromString value =
        if String.IsNullOrWhiteSpace value then
            invalidOp "Continuation token cannot be empty."
        else
            ContinuationToken value

    static member toString(ContinuationToken value) = value

type CosmosQueryOptions =
    { Query: QueryDefinition
      ContinuationToken: ContinuationToken option
      PartitionKey: PartitionKey option }

type CosmosError =
    | ResourceAlreadyExists
    | ResourceNotFound
    | PreconditionFailed

[<RequireQualifiedAccess>]
module Cosmos =
    let serializer =
        { new CosmosSerializer() with
            member _.FromStream<'a>(stream) =
                match JsonSerializer.Deserialize<'a>(stream) |> box with
                | null -> JsonException("Failed to deserialize JSON.") |> raise
                | value -> value :?> 'a

            member _.ToStream(value) =
                let stream = new MemoryStream()
                JsonSerializer.Serialize(stream, value)
                stream.Position <- 0
                stream }

    let private serializerOptions = JsonSerializerOptions.Web

    let private getJsonObject (response: ResponseMessage) =
        async {
            let! cancellationToken = Async.CancellationToken
            use stream = response.Content

            let! node =
                JsonSerializer
                    .DeserializeAsync<JsonNode>(stream, serializerOptions, cancellationToken)
                    .AsTask()
                |> Async.AwaitTask

            return
                JsonNode.tryToJsonObject node
                |> Result.defaultWith (fun error ->
                    JsonException("Cosmos response message does not contain a JSON object") |> raise)
        }

    let tryReadJsonObject (container: Container) id partitionKey =
        async {
            let idString = CosmosId.toString id
            let! cancellationToken = Async.CancellationToken

            use! response =
                container.ReadItemStreamAsync(idString, partitionKey, cancellationToken = cancellationToken)
                |> Async.AwaitTask

            if response.StatusCode = HttpStatusCode.NotFound then
                return None
            else
                use response = response.EnsureSuccessStatusCode()
                let! jsonObject = getJsonObject response
                return Some jsonObject
        }

    let tryReadRecord container id partitionKey deserializer =
        async {
            match! tryReadJsonObject container id partitionKey with
            | Some jsonObject ->
                let record = deserializer jsonObject

                return
                    Some
                        { Id = id
                          PartitionKey = partitionKey
                          Record = record
                          ETag = AzureETag.fromCosmosJsonObject jsonObject }
            | None -> return None
        }

    let patchRecord (container: Container) id partitionKey eTag operations =
        async {
            let idString = CosmosId.toString id

            let operationList = List.ofSeq operations

            let options = new PatchItemRequestOptions()
            options.IfMatchEtag <- AzureETag.toString eTag

            let! cancellationToken = Async.CancellationToken

            use! response =
                container.PatchItemStreamAsync(idString, partitionKey, operationList, options, cancellationToken)
                |> Async.AwaitTask

            if response.StatusCode = HttpStatusCode.PreconditionFailed then
                return Error CosmosError.PreconditionFailed
            else if response.StatusCode = HttpStatusCode.NotFound then
                return Error CosmosError.ResourceNotFound
            else
                use _ = response.EnsureSuccessStatusCode()
                return Ok()
        }

    let private getCurrentPageResults (iterator: FeedIterator) =
        async {
            let! cancellationToken = Async.CancellationToken
            use! response = iterator.ReadNextAsync(cancellationToken) |> Async.AwaitTask
            response.EnsureSuccessStatusCode() |> ignore
            let! responseJson = getJsonObject response

            let documents =
                JsonObject.getJsonArrayProperty responseJson "Documents"
                |> Seq.map JsonNode.toJsonObject

            let continuationToken =
                if String.IsNullOrWhiteSpace response.ContinuationToken then
                    None
                else
                    Some(ContinuationToken.fromString response.ContinuationToken)

            return documents, continuationToken
        }

    let private getQueryResultsFromIterator (iterator: FeedIterator) =
        taskSeq {
            while iterator.HasMoreResults do
                let! documents, _ = getCurrentPageResults iterator

                for document in documents do
                    yield document
        }

    let private getFeedIterator (container: Container) (options: CosmosQueryOptions) =
        let queryDefinition = options.Query

        let continuationToken =
            options.ContinuationToken
            |> Option.map ContinuationToken.toString
            |> Option.toObj

        let queryRequestOptions = QueryRequestOptions()

        options.PartitionKey
        |> Option.iter (fun partitionKey -> queryRequestOptions.PartitionKey <- partitionKey)

        container.GetItemQueryStreamIterator(queryDefinition, continuationToken, queryRequestOptions)

    let getQueryResults container options =
        taskSeq {
            use iterator = getFeedIterator container options

            for results in getQueryResultsFromIterator iterator do
                yield results
        }

    let createRecord (container: Container) json partitionKey =
        async {
            use stream =
                BinaryData.FromObjectAsJson<JsonObject>(json, serializerOptions).ToStream()

            let options = ItemRequestOptions()
            options.IfNoneMatchEtag <- ETag.All.ToString()
            let! cancellationToken = Async.CancellationToken

            use! response =
                container.CreateItemStreamAsync(stream, partitionKey, options, cancellationToken)
                |> Async.AwaitTask

            match response.StatusCode with
            | HttpStatusCode.Conflict
            | HttpStatusCode.PreconditionFailed -> return Error CosmosError.ResourceAlreadyExists
            | _ ->
                use _ = response.EnsureSuccessStatusCode()
                return Ok()
        }
