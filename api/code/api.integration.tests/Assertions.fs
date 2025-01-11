[<AutoOpen>]
module api.integration.tests.Assertions

open System.Runtime.CompilerServices
open System.Net.Http
open Faqt
open Faqt.AssertionHelpers
open common

[<Extension>]
type HttpResponseMessageAssertions =
    [<Extension>]
    static member BeSuccessful(t: Testable<HttpResponseMessage>, ?because) : And<HttpResponseMessage> =
        let _ = t.Assert()
        if t.Subject.IsSuccessStatusCode then
            And(t)
        else
            t.With("But was", "Failure").With("StatusCode", t.Subject.StatusCode).Fail(because)

[<Extension>]
type JsonResultAssertions =
    [<Extension>]
    static member BeSuccess<'a>(t: Testable<JsonResult<'a>>, ?because) : AndDerived<JsonResult<'a>, 'a> =
        use _ = t.Assert()

        t.Subject
        |> JsonResult.map (fun a -> AndDerived(t, a))
        |> JsonResult.defaultWith (fun error ->
            t.With("But was", "Failure").With("Error message", JsonError.getMessage error).Fail(because))

    [<Extension>]
    static member BeFailure<'a>(t: Testable<JsonResult<'a>>, ?because) : AndDerived<JsonResult<'a>, JsonError> =
        let _ = t.Assert()

        t.Subject
        |> JsonResult.map (fun a -> t.With("But was", "Success").With("Value", a).Fail(because))
        |> JsonResult.defaultWith (fun error -> AndDerived(t, error))
