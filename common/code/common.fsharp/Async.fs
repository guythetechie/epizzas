[<RequireQualifiedAccess>]
module common.Async

let startAsTaskWithToken cancellationToken computation =
    Async.StartAsTask(computation, cancellationToken = cancellationToken)
