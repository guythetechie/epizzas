namespace common

open Microsoft.AspNetCore.Http
open FSharpPlus

//type ApiOperation<'a> = private ApiOperation of Async<Result<'a, IResult>>

type ApiOperationBuilder() =
    member _.Bind(x: Async<Result<'a, IResult>>, f: 'a -> Async<Result<'b, IResult>>) : Async<Result<'b, IResult>> =
        async {
            match! x with
            | Ok a -> return! f a
            | Error error -> return Error error
        }

    member _.Bind(x: Async<'a>, f: 'a -> Async<Result<'b, IResult>>) : Async<Result<'b, IResult>> = x |> bind f

    member _.Bind(x: Result<'a, IResult>, f: 'a -> Async<Result<'b, IResult>>) : Async<Result<'b, IResult>> =
        match x with
        | Ok a -> f a
        | Error error -> Error error |> async.Return

    member _.BindReturn(x: Async<Result<'a, IResult>>, f: 'a -> 'b) : Async<Result<'b, IResult>> =
        async {
            match! x with
            | Ok a -> return Ok(f a)
            | Error error -> return Error error
        }

    member _.BindReturn(x: Async<'a>, f: 'a -> 'b) : Async<Result<'b, IResult>> = x |> map (f >> Ok)

    member _.BindReturn(x: Result<'a, IResult>, f: 'a -> 'b) : Async<Result<'b, IResult>> =
        async {
            match x with
            | Ok a -> return Ok(f a)
            | Error error -> return Error error
        }

    member _.MergeSources(x: Result<'a, IResult>, y: Result<'b, IResult>) : Async<Result<'a * 'b, IResult>> =
        async {
            match x, y with
            | Ok x, Ok y -> return Ok(x, y)
            | Error error, _ -> return Error error
            | _, Error error -> return Error error
        }

    member _.MergeSources
        (
            x: Async<Result<'a, IResult>>,
            y: Async<Result<'b, IResult>>
        ) : Async<Result<'a * 'b, IResult>> =
        async {
            let! x = x
            let! y = y

            match x, y with
            | Ok x, Ok y -> return Ok(x, y)
            | Error error, _ -> return Error error
            | _, Error error -> return Error error
        }

    member _.Return(x: IResult) : Async<Result<IResult, IResult>> = Ok x |> async.Return

    member _.ReturnFrom(x: Async<IResult>) : Async<Result<IResult, IResult>> = x |> map Ok

    member _.ReturnFrom(x: Result<IResult, IResult>) : Async<Result<IResult, IResult>> = x |> async.Return

    member _.ReturnFrom(x: Async<Result<IResult, IResult>>) : Async<Result<IResult, IResult>> = x

    member this.TryWith(x: Async<IResult>, handler) : Async<Result<IResult, IResult>> =
        async {
            try
                return! this.ReturnFrom(x)
            with e ->
                return handler e
        }

    member this.TryWith(x: Result<IResult, IResult>, handler) : Async<Result<IResult, IResult>> =
        async {
            try
                return! this.ReturnFrom(x)
            with e ->
                return handler e
        }

    member this.TryWith(x: Async<Result<IResult, IResult>>, handler) : Async<Result<IResult, IResult>> =
        async {
            try
                return! this.ReturnFrom(x)
            with e ->
                return handler e
        }

    member _.Run(x: Async<Result<IResult, IResult>>) : Async<IResult> = x |> map (either id id)

[<AutoOpen>]
module Builder =
    let apiOperation = ApiOperationBuilder()

//[<RequireQualifiedAccess>]
//module ApiOperation =
//    let toAsyncResult (ApiOperation operation) = operation

//    let fromAsyncResult result = result |> ApiOperation

//    let fromResult result =
//        result |> async.Return |> fromAsyncResult

//    let fromAsyncValue value =
//        value |> Async.map Ok |> fromAsyncResult

//    let fromValue value = Ok value |> fromResult

//    let run operation =
//        operation |> toAsyncResult |> map (either id id)

//type ApiOperation<'a> with
//    // Functor
//    static member Map(ApiOperation operation: ApiOperation<'a>, f: 'a -> 'b) : ApiOperation<'b> =
//        operation |> map (Result.map f) |> ApiOperation.fromAsyncResult

//    static member Unzip(operation: ApiOperation<'a * 'b>) : ApiOperation<'a> * ApiOperation<'b> =
//        let asyncResult = ApiOperation.toAsyncResult operation

//        let first =
//            asyncResult |> Async.map (Result.map fst) |> ApiOperation.fromAsyncResult

//        let second =
//            asyncResult |> Async.map (Result.map snd) |> ApiOperation.fromAsyncResult

//        first, second

//    // Monad
//    static member Return(x: 'a) : ApiOperation<'a> = ApiOperation.fromValue x

//    static member (>>=)(x: ApiOperation<'a>, f: 'a -> ApiOperation<'b>) : ApiOperation<'b> =
//        async {
//            match! ApiOperation.toAsyncResult x with
//            | Ok a -> return! f a |> ApiOperation.toAsyncResult
//            | Error error -> return Error error
//        }
//        |> ApiOperation.fromAsyncResult

//    static member Join(x: ApiOperation<ApiOperation<'a>>) : ApiOperation<'a> =
//        async {
//            match! ApiOperation.toAsyncResult x with
//            | Ok y -> return! ApiOperation.toAsyncResult y
//            | Error error -> return Error error
//        }
//        |> ApiOperation.fromAsyncResult

//    // Applicative
//    static member (<*)(f: ApiOperation<'a -> 'b>, x: ApiOperation<'a>) : ApiOperation<'b> =
//        async {
//            let! f = ApiOperation.toAsyncResult f
//            let! x = ApiOperation.toAsyncResult x
//            return Result.apply f x
//        }
//        |> ApiOperation.fromAsyncResult

//    static member Lift2(f: 'a -> 'b -> 'c, x: ApiOperation<'a>, y: ApiOperation<'b>) : ApiOperation<'c> =
//        async {
//            let! x = ApiOperation.toAsyncResult x
//            let! y = ApiOperation.toAsyncResult y

//            match x, y with
//            | Ok x, Ok y -> return f x y |> Ok
//            | Error error, _ -> return Error error
//            | _, Error error -> return Error error
//        }
//        |> ApiOperation.fromAsyncResult

//    static member Lift3
//        (
//            f: 'a -> 'b -> 'c -> 'd,
//            x: ApiOperation<'a>,
//            y: ApiOperation<'b>,
//            z: ApiOperation<'c>
//        ) : ApiOperation<'d> =
//        async {
//            let! x = ApiOperation.toAsyncResult x
//            let! y = ApiOperation.toAsyncResult y
//            let! z = ApiOperation.toAsyncResult z

//            match x, y, z with
//            | Ok x, Ok y, Ok z -> return f x y z |> Ok
//            | Error error, _, _ -> return Error error
//            | _, Error error, _ -> return Error error
//            | _, _, Error error -> return Error error
//        }
//        |> ApiOperation.fromAsyncResult
