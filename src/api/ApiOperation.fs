[<AutoOpen>]
module internal ApiOperation

open Microsoft.AspNetCore.Http
open FSharpPlus
open Giraffe

type ApiOperation<'a> = Async<Choice<'a, IResult>>

type ApiOperationBuilder() =
    member _.Bind(operation: ApiOperation<'a>, f: 'a -> ApiOperation<'b>) : ApiOperation<'b> =
        async {
            match! operation with
            | Choice1Of2 value1 -> return! f value1
            | Choice2Of2 value2 -> return Choice2Of2 value2
        }

    member _.Bind(operation: Result<'a, IResult>, f: 'a -> ApiOperation<'b>) : ApiOperation<'b> =
        async {
            match operation with
            | Ok value1 -> return! f value1
            | Error value2 -> return Choice2Of2 value2
        }

    member _.Bind(operation: Async<Result<'a, IResult>>, f: 'a -> ApiOperation<'b>) : ApiOperation<'b> =
        async {
            match! operation with
            | Ok value1 -> return! f value1
            | Error value2 -> return Choice2Of2 value2
        }

    member _.Bind3
        (
            operation1: ApiOperation<'a>,
            operation2: ApiOperation<'b>,
            operation3: ApiOperation<'c>,
            f: ('a * 'b * 'c) -> ApiOperation<'d>
        ) : ApiOperation<'d> =
        async {
            let! result1 = operation1
            let! result2 = operation2
            let! result3 = operation3

            match result1, result2, result3 with
            | Choice1Of2 value1, Choice1Of2 value2, Choice1Of2 value3 -> return! f (value1, value2, value3)
            | Choice2Of2 value2, _, _ -> return Choice2Of2 value2
            | _, Choice2Of2 value2, _ -> return Choice2Of2 value2
            | _, _, Choice2Of2 value3 -> return Choice2Of2 value3
        }

    member _.Return value = Choice1Of2 value |> async.Return

    member _.ReturnFrom(operation: ApiOperation<'a>) = operation

    member _.ReturnFrom(operation: Async<IResult>) =
        Async.map Choice<_, IResult>.Choice1Of2 operation

    member _.BindReturn(operation: ApiOperation<'a>, f: 'a -> 'b) : ApiOperation<'b> =
        async {
            match! operation with
            | Choice1Of2 value -> return Choice1Of2(f value)
            | Choice2Of2 value -> return Choice2Of2 value
        }

    member _.BindReturn(operation: Result<'a, IResult>, f: 'a -> 'b) : ApiOperation<'b> =
        async {
            match operation with
            | Ok value -> return Choice1Of2(f value)
            | Error value -> return Choice2Of2 value
        }

    member _.BindReturn(operation: Async<Result<'a, IResult>>, f: 'a -> 'b) : ApiOperation<'b> =
        async {
            let! result = operation

            match result with
            | Ok value -> return Choice1Of2(f value)
            | Error value -> return Choice2Of2 value
        }

    member _.MergeSources(source1: ApiOperation<'a>, source2: ApiOperation<'b>) : ApiOperation<'a * 'b> =
        async {
            let! result1 = source1
            let! result2 = source2

            return
                match result1, result2 with
                | Choice1Of2 value1, Choice1Of2 value2 -> Choice1Of2(value1, value2)
                | Choice2Of2 value2, _ -> Choice2Of2 value2
                | _, Choice2Of2 value2 -> Choice2Of2 value2
        }

    member _.MergeSources3
        (source1: ApiOperation<'a>, source2: ApiOperation<'b>, source3: ApiOperation<'c>)
        : ApiOperation<'a * 'b * 'c> =
        async {
            let! result1 = source1
            let! result2 = source2
            let! result3 = source3

            return
                match result1, result2, result3 with
                | Choice1Of2 value1, Choice1Of2 value2, Choice1Of2 value3 -> Choice1Of2(value1, value2, value3)
                | Choice2Of2 value2, _, _ -> Choice2Of2 value2
                | _, Choice2Of2 value2, _ -> Choice2Of2 value2
                | _, _, Choice2Of2 value3 -> Choice2Of2 value3
        }

let apiOperation = ApiOperationBuilder()

[<RequireQualifiedAccess>]
module ApiOperation =
    let finalize (operation: ApiOperation<IResult>) =
        let coalesce = Choice.either id id
        Async.map coalesce operation

    let toHttpHandler operation : HttpHandler =
        fun next (context: HttpContext) ->
            task {
                let! result = finalize operation
                do! result.ExecuteAsync(context)
                return! next context
            }
