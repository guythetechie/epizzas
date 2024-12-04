namespace common

open FSharpPlus
open System
open System.Text.Json

type JsonError = private JsonError of JsonException

[<RequireQualifiedAccess>]
module JsonError =
    let fromString message = JsonException(message) |> JsonError

    let internal fromException jsonException = JsonError jsonException

    let getMessage (JsonError error) = error.Message

    let toJsonException (JsonError error) = error

    let addError newError existingError =
        let innerExceptions =
            let newJsonException = toJsonException newError
            let existingJsonException = toJsonException existingError

            match existingJsonException.InnerException with
            | :? AggregateException as aggregateException ->
                Seq.append aggregateException.InnerExceptions [ newJsonException ]
            | _ -> Seq.append [ existingJsonException ] [ newJsonException ]

        let jsonException =
            new JsonException(
                "Multiple errors, see inner exception for details..",
                new AggregateException(innerExceptions)
            )

        fromException jsonException

type JsonError with
    // Semigroup
    static member (+)(x: JsonError, y: JsonError) = x |> JsonError.addError y

type JsonResult<'a> =
    private
    | Success of 'a
    | Failure of JsonError

[<RequireQualifiedAccess>]
module JsonResult =
    let isSuccess jsonResult =
        match jsonResult with
        | Success _ -> true
        | Failure _ -> false

    let isFailure jsonResult =
        match jsonResult with
        | Success _ -> false
        | Failure _ -> true

    let succeed x = Success x

    let fail e = Failure e

    let failWithMessage message = JsonError.fromString message |> fail

    let replaceErrorWith f jsonResult =
        match jsonResult with
        | Failure e -> f e |> fail
        | _ -> jsonResult

    let setErrorMessage message jsonResult =
        replaceErrorWith (fun _ -> JsonError.fromString message) jsonResult

    let map f jsonResult =
        match jsonResult with
        | Success x -> succeed (f x)
        | Failure e -> fail e

    let bind f jsonResult =
        match jsonResult with
        | Success x -> f x
        | Failure e -> fail e

    let apply f x =
        match f, x with
        | Success f, Success x -> succeed (f x)
        | Failure e, Success _ -> fail e
        | Success _, Failure e -> fail e
        | Failure e1, Failure e2 -> fail (JsonError.addError e2 e1)

    let defaultWith f jsonResult =
        match jsonResult with
        | Success x -> x
        | Failure jsonError -> f jsonError

    let throwIfFail jsonResult =
        defaultWith (JsonError.toJsonException >> raise) jsonResult

//[<AutoOpen>]
//module Builder =
//    type JsonResultBuilder() =
//        member _.Bind(x: JsonResult<'a>, f: 'a -> JsonResult<'b>) : JsonResult<'b> =
//            match x with
//            | Success a -> f a
//            | Failure error -> JsonResult.fail error

//        member _.Bind2(x: JsonResult<'a>, y: JsonResult<'b>, f: 'a -> 'b -> JsonResult<'c>) : JsonResult<'c> =
//            match x, y with
//            | Success a, Success b -> f a b
//            | Failure e, Success _ -> JsonResult.fail e
//            | Success _, Failure e -> JsonResult.fail e
//            | Failure e1, Failure e2 -> JsonResult.fail (JsonError.addError e2 e1)

//        //member _.Bind3
//        //    (
//        //        x: JsonResult<'a>,
//        //        y: JsonResult<'b>,
//        //        z: JsonResult<'c>,
//        //        f: 'a -> 'b -> 'c -> JsonResult<'d>
//        //    ) : JsonResult<'d> =
//        //    match x, y, z with
//        //    | Success a, Success b, Success c -> f a b c
//        //    | Failure e1, Success _, Success _ -> JsonResult.fail e1
//        //    | Failure e1, Success _, Failure e3 -> JsonResult.fail (JsonError.addError e3 e1)
//        //    | Failure e1, Failure e2, Success _ -> JsonResult.fail (JsonError.addError e2 e1)
//        //    | Failure e1, Failure e2, Failure e3 ->
//        //        JsonResult.fail (e1 |> JsonError.addError e2 |> JsonError.addError e3)
//        //    | Success _, Failure e2, Success _ -> JsonResult.fail e2
//        //    | Success _, Failure e2, Failure e3 -> JsonResult.fail (JsonError.addError e2 e3)
//        //    | Success _, Success _, Failure e3 -> JsonResult.fail e3

//        member _.Return(x: 'a) : JsonResult<'a> = JsonResult.succeed x

//        member _.ReturnFrom(x: JsonResult<'a>) : JsonResult<'a> = x

//        member _.BindReturn(x: JsonResult<'a>, f: 'a -> 'b) : JsonResult<'b> = JsonResult.map f x

//        member _.Bind2Return(x: JsonResult<'a>, y: JsonResult<'b>, f: 'a -> 'b -> JsonResult<'c>) : JsonResult<'c> =
//            match x, y with
//            | Success a, Success b -> f a b
//            | Failure e, Success _ -> JsonResult.fail e
//            | Success _, Failure e -> JsonResult.fail e
//            | Failure e1, Failure e2 -> JsonResult.fail (JsonError.addError e2 e1)

//        member _.Bind3Return
//            (
//                x: JsonResult<'a>,
//                y: JsonResult<'b>,
//                z: JsonResult<'c>,
//                f: 'a -> 'b -> 'c -> JsonResult<'d>
//            ) : JsonResult<'d> =
//            match x, y, z with
//            | Success a, Success b, Success c -> f a b c
//            | Failure e1, Success _, Success _ -> JsonResult.fail e1
//            | Failure e1, Success _, Failure e3 -> JsonResult.fail (JsonError.addError e3 e1)
//            | Failure e1, Failure e2, Success _ -> JsonResult.fail (JsonError.addError e2 e1)
//            | Failure e1, Failure e2, Failure e3 ->
//                JsonResult.fail (e1 |> JsonError.addError e2 |> JsonError.addError e3)
//            | Success _, Failure e2, Success _ -> JsonResult.fail e2
//            | Success _, Failure e2, Failure e3 -> JsonResult.fail (JsonError.addError e2 e3)
//            | Success _, Success _, Failure e3 -> JsonResult.fail e3

//        member _.MergeSources(x: JsonResult<'a>, y: JsonResult<'b>) : JsonResult<'a * 'b> =
//            match x, y with
//            | Success x, Success y -> JsonResult.succeed (x, y)
//            | Failure e1, Success _ -> JsonResult.fail e1
//            | Success _, Failure e2 -> JsonResult.fail e2
//            | Failure e1, Failure e2 -> JsonResult.fail (JsonError.addError e2 e1)

//        member _.MergeSources3(x: JsonResult<'a>, y: JsonResult<'b>, z: JsonResult<'c>) : JsonResult<'a * 'b * 'c> =
//            match x, y, z with
//            | Success x, Success y, Success z -> JsonResult.succeed (x, y, z)
//            | Failure e1, Success _, Success _ -> JsonResult.fail e1
//            | Failure e1, Success _, Failure e3 -> JsonResult.fail (JsonError.addError e3 e1)
//            | Failure e1, Failure e2, Success _ -> JsonResult.fail (JsonError.addError e2 e1)
//            | Failure e1, Failure e2, Failure e3 ->
//                JsonResult.fail (e1 |> JsonError.addError e2 |> JsonError.addError e3)
//            | Success _, Failure e2, Success _ -> JsonResult.fail e2
//            | Success _, Failure e2, Failure e3 -> JsonResult.fail (JsonError.addError e2 e3)
//            | Success _, Success _, Failure e3 -> JsonResult.fail e3

//        member _.Using(x: 'a :> IDisposable, f: 'a -> JsonResult<'b>) : JsonResult<'b> =
//            try
//                f x
//            finally
//                x.Dispose()

//    let jsonResult = JsonResultBuilder()

type JsonResult<'a> with
    // Functor
    static member Map(x, f) =
        match x with
        | Success x -> JsonResult.succeed (f x)
        | Failure e -> JsonResult.fail e

    static member Unzip x =
        match x with
        | Success(x, y) -> JsonResult.succeed x, JsonResult.succeed y
        | Failure e -> JsonResult.fail e, JsonResult.fail e

    // Applicative
    static member Return x = JsonResult.succeed x

    static member (<*>)(f, x) = JsonResult.apply f x

    static member Lift2(f, x1, x2) =
        match x1, x2 with
        | Success x1, Success x2 -> f x1 x2 |> JsonResult.succeed
        | Failure e, Success _ -> JsonResult.fail e
        | Success _, Failure e -> JsonResult.fail e
        | Failure e1, Failure e2 -> JsonResult.fail (JsonError.addError e2 e1)

    static member Lift3(f, x1, x2, x3) =
        match x1, x2, x3 with
        | Success x1, Success x2, Success x3 -> f x1 x2 x3 |> JsonResult.succeed
        | Failure e1, Success _, Success _ -> JsonResult.fail e1
        | Failure e1, Success _, Failure e3 -> JsonResult.fail (JsonError.addError e3 e1)
        | Failure e1, Failure e2, Success _ -> JsonResult.fail (JsonError.addError e2 e1)
        | Failure e1, Failure e2, Failure e3 -> JsonResult.fail (e1 |> JsonError.addError e2 |> JsonError.addError e3)
        | Success _, Failure e2, Success _ -> JsonResult.fail e2
        | Success _, Failure e2, Failure e3 -> JsonResult.fail (JsonError.addError e2 e3)
        | Success _, Success _, Failure e3 -> JsonResult.fail e3

    // Zip applicative
    static member Pure x = JsonResult.succeed x

    static member (<.>)(f, x) = JsonResult.apply f x

    static member Zip(x1, x2) =
        match (x1, x2) with
        | Success x1, Success x2 -> JsonResult.succeed (x1, x2)
        | Failure e1, Success _ -> JsonResult.fail e1
        | Success _, Failure e2 -> JsonResult.fail e2
        | Failure e1, Failure e2 -> JsonResult.fail (JsonError.addError e2 e1)

    static member Map2(f, x1, x2) =
        match x1, x2 with
        | Success x1, Success x2 -> f x1 x2 |> JsonResult.succeed
        | Failure e, Success _ -> JsonResult.fail e
        | Success _, Failure e -> JsonResult.fail e
        | Failure e1, Failure e2 -> JsonResult.fail (JsonError.addError e2 e1)

    static member Map3(f, x1, x2, x3) =
        match x1, x2, x3 with
        | Success x1, Success x2, Success x3 -> f x1 x2 x3 |> JsonResult.succeed
        | Failure e1, Success _, Success _ -> JsonResult.fail e1
        | Failure e1, Success _, Failure e3 -> JsonResult.fail (JsonError.addError e3 e1)
        | Failure e1, Failure e2, Success _ -> JsonResult.fail (JsonError.addError e2 e1)
        | Failure e1, Failure e2, Failure e3 -> JsonResult.fail (e1 |> JsonError.addError e2 |> JsonError.addError e3)
        | Success _, Failure e2, Success _ -> JsonResult.fail e2
        | Success _, Failure e2, Failure e3 -> JsonResult.fail (JsonError.addError e2 e3)
        | Success _, Success _, Failure e3 -> JsonResult.fail e3

    // Monad
    static member (>>=)(x, f) =
        match x with
        | Success x -> f x
        | Failure e -> JsonResult.fail e

    static member Join x =
        match x with
        | Success(Success x) -> JsonResult.succeed x
        | Success(Failure e) -> JsonResult.fail e
        | Failure e -> JsonResult.fail e

    // Foldable
    static member ToSeq x =
        match x with
        | Success x -> Seq.singleton x
        | Failure _ -> Seq.empty

    //// Traversable
    //static member inline Traverse(x, f) =
    //    match x with
    //    | Success x -> f x |> map JsonResult.succeed
    //    | Failure e -> result (Failure e)

    //static member inline Sequence x =
    //    match x with
    //    | Success x -> x |> map JsonResult.succeed
    //    | Failure e -> result (Failure e)
