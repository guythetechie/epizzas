import "@typespec/http";
import "@typespec/rest";
import "./common.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Rest.Resource;

namespace EPizzas.Ordering.Api.Handlers.Delete;

model RequestModel<T extends object> {
    ...ResourceParameters<T>;

    @doc(
        """
        Delete an {name} instance if the server's ETag matches this header.
        """,
        T
    )
    @header("If-Match")
    ifMatch: eTag;
}

@doc("The request is invalid.")
model BadRequestResponse<T extends object> {
    ...Http.BadRequestResponse;

    @body
    body: InvalidIdErrorModel<T> | InvalidConditionalHeaderErrorModel;
}

@doc("""
    The specified `If-Match` header doesn't match the server's.
    """)
model PreconditionFailedResponse<T extends object> {
    ...Response<412>;
    @body body: InvalidConditionalHeaderErrorModel;
}

@doc("""
    Must specify `If-Match` header.
    """)
model PreconditionRequiredResponse<T extends object> {
    ...Response<428>;
    @body body: InvalidConditionalHeaderErrorModel;
}

@doc("{name} was successfully deleted.", T)
model NoContentResponse<T extends object> is Http.NoContentResponse;

alias ResponseModel<T extends object> =
    | BadRequestResponse<T>
    | PreconditionFailedResponse<T>
    | PreconditionRequiredResponse<T>
    | NoContentResponse<T>;

@doc("Deletes an {name} instance.", T)
@deletesResource(T)
op operation<T extends object>(...RequestModel<T>): ResponseModel<T>;
