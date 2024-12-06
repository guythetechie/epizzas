[<RequireQualifiedAccess>]
module api.integration.tests.Gen

open FsCheck
open FsCheck.FSharp
open System
open System.Text.Json.Nodes
open System.Collections.Generic
open System.Text.Json

let default'<'a> () = ArbMap.defaults |> ArbMap.generate<'a>

let mapFilter mapper predicate gen =
    Arb.fromGen gen |> Arb.mapFilter mapper predicate |> Arb.toGen

let nullable (gen: Gen<'a>): Gen<'a | null> =
    Gen.frequency
        [ 1, gen
          7, Gen.constant(null) ]

let mapOf keyGen valueGen =
    Arb.mapKV ((Arb.fromGen keyGen), (Arb.fromGen valueGen)) |> Arb.toGen

let jsonValue =
    Gen.oneof
        [ default'<int> () |> Gen.map JsonValue.Create
          default'<string> () |> Gen.map JsonValue.Create
          default'<bool> () |> Gen.map JsonValue.Create
          default'<double> ()
          |> Gen.filter (Double.IsInfinity >> not)
          |> Gen.filter (Double.IsNaN >> not)
          |> Gen.map JsonValue.Create
          default'<byte> () |> Gen.map JsonValue.Create
          default'<Guid> () |> Gen.map JsonValue.Create ]

let toJsonNode gen =
    gen |> Gen.map (fun value -> value :> JsonNode)

let private jsonValueAsNode = toJsonNode jsonValue

let generateJsonArray (nodeGen: Gen<JsonNode | null>) =
    Gen.arrayOf nodeGen |> Gen.map JsonArray

let generateJsonObject (nodeGen: Gen<JsonNode | null>) =
    Gen.zip (default'<string> ()) nodeGen
    |> Gen.listOf
    |> Gen.map (Seq.distinctBy (fun (first, second) -> first.ToUpperInvariant()))
    |> Gen.map (Seq.map KeyValuePair.Create)
    |> Gen.map JsonObject

let jsonNode =
    let rec generateJsonNode size =
        if size < 1 then
            jsonValueAsNode
        else
            let reducedSizeGen = generateJsonNode (size / 2)

            Gen.oneof
                [ jsonValueAsNode
                  generateJsonArray reducedSizeGen |> toJsonNode
                  generateJsonObject reducedSizeGen |> toJsonNode ]

    Gen.sized generateJsonNode

let jsonObject = generateJsonObject jsonNode

let jsonArray = generateJsonArray jsonNode

[<RequireQualifiedAccess>]
module JsonValue =
    let string = default'<string>() |> Gen.map JsonValue.Create

    let nonString =
        jsonValue |> Gen.filter (fun value -> value.GetValueKind() <> JsonValueKind.String)

    let integer = default'<int>() |> Gen.map JsonValue.Create

    let nonInteger =
        jsonValue
        |> Gen.filter (fun value ->
            match value.GetValue<obj>() with
            | :? int -> false
            | :? byte -> false
            | _ -> true)

    let absoluteUri =
        default'<NonWhiteSpaceString> ()
        |> Gen.map (fun value -> $"https://{value.Get}")
        |> Gen.filter (fun uri -> Uri.TryCreate(uri, UriKind.Absolute) |> fst)
        |> Gen.map JsonValue.Create

    let nonAbsoluteUri =
        default'<obj>()
        |> Gen.filter (fun value ->
            match Uri.TryCreate(value.ToString(), UriKind.Absolute) with
            | true, _ -> false
            | _ -> true)
        |> Gen.map JsonValue.Create

    let guid =
        default'<Guid> ()
        |> Gen.map (fun value -> value.ToString())
        |> Gen.map JsonValue.Create

    let nonGuid =
        default'<obj>()
        |> Gen.filter (fun value ->
            match Guid.TryParse(value.ToString()) with
            | true, _ -> false
            | _ -> true)
        |> Gen.map JsonValue.Create

    let bool = Gen.elements [true; false]  |> Gen.map JsonValue.Create

    let nonBool =
        default'<obj> ()
        |> Gen.filter (fun value ->
            match value with
            | :? bool -> false
            | _ -> true)
        |> Gen.map JsonValue.Create

[<RequireQualifiedAccess>]
module JsonNode =
    let jsonObject = toJsonNode jsonObject

    let nonJsonObject = Gen.oneof [toJsonNode jsonValue; toJsonNode jsonArray]

    let jsonArray = toJsonNode jsonArray

    let nonJsonArray = Gen.oneof [toJsonNode jsonValue; toJsonNode jsonObject]

    let jsonValue = jsonValueAsNode

    let nonJsonValue = Gen.oneof [toJsonNode jsonArray; toJsonNode jsonObject]