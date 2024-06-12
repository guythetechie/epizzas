[<RequireQualifiedAccess>]
module internal HttpContext

open System
open System.Text.Json
open System.Text.Json.Nodes
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.AspNetCore.Http.Features
open FSharpPlus
open common

let tryGetRouteValue (context: HttpContext) key =
    match context.Request.RouteValues.TryGetValue key with
    | true, value -> Some value
    | _ -> None

let getRequestUri (context: HttpContext) =
    UriHelper.BuildAbsolute(context.Request.Scheme, context.Request.Host, context.Request.Path)
    |> Uri

let getIfMatchHeaders (context: HttpContext) =
    context.Request.Headers.IfMatch |> Seq.filter String.isNotNullOrWhiteSpace

let tryGetJsonObject (context: HttpContext) =
    let bodyDetectionFeatureOption =
        context.Features.Get<IHttpRequestBodyDetectionFeature>() |> Option.ofObj

    match bodyDetectionFeatureOption with
    | Some bodyDetectionFeature ->
        match bodyDetectionFeature.CanHaveBody with
        | true ->
            if context.Request.HasJsonContentType() then
                async {
                    let! cancellationToken = Async.CancellationToken

                    try
                        match!
                            context.Request.ReadFromJsonAsync<JsonNode>(cancellationToken).AsTask()
                            |> Async.AwaitTask
                        with
                        | null ->
                            return
                                TypedResults.BadRequest(
                                    {| code = ApiErrorCode.toString ApiErrorCode.InvalidRequestBody
                                       message = "Request body cannot be null." |}
                                )
                                :> IResult
                                |> Error
                        | :? JsonObject as jsonObject -> return Ok jsonObject
                        | _ ->
                            return
                                TypedResults.BadRequest(
                                    {| code = ApiErrorCode.toString ApiErrorCode.InvalidRequestBody
                                       message = "Request body must be a JSON object." |}
                                )
                                :> IResult
                                |> Error
                    with :? JsonException as jsonException ->
                        return
                            TypedResults.BadRequest(
                                {| code = ApiErrorCode.toString ApiErrorCode.InvalidRequestBody
                                   message = jsonException.Message |}
                            )
                            :> IResult
                            |> Error
                }
            else
                TypedResults.Json(
                    {| code = ApiErrorCode.toString ApiErrorCode.InvalidHeader
                       message = $"Expected a supported JSON media type but got '{context.Request.ContentType}'." |},
                    statusCode = StatusCodes.Status415UnsupportedMediaType
                )
                :> IResult
                |> Error
                |> async.Return
        | false ->
            TypedResults.BadRequest(
                {| code = ApiErrorCode.toString ApiErrorCode.InvalidRequestBody
                   message = "Request cannot have a body." |}
            )
            :> IResult
            |> Error
            |> async.Return
    | None ->
        TypedResults.BadRequest(
            {| code = ApiErrorCode.toString ApiErrorCode.InvalidRequestBody
               message = "Request cannot have a body." |}
        )
        :> IResult
        |> Error
        |> async.Return
