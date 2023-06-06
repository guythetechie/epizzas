import "@typespec/http";
import "@typespec/rest";
import "./common.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Rest.Resource;

namespace EPizzas.Ordering.Api.Handlers.Put;

@withDefaultKeyVisibility("")
model RequestModel<T extends object> {
    ...ResourceParameters<T>;
    ...ResourceCreateOrUpdateModel<T>;

    @doc(
        """
        Update an {name} instance if the server's ETag matches this header.

        **NOTE**: Must specify either `If-Match` or `If-None-Match` header.
        """,
        T
    )
    @header("If-Match")
    ifMatch?: eTag;

    @doc(
        """
        Create an {name} instance. Only accepted value is `*`.

        **NOTE**: Must specify either `If-Match` or `If-None-Match` header.
        """,
        T
    )
    @header("If-None-Match")
    ifNoneMatch?: eTag;
}

@doc("The request is invalid.")
model BadRequestResponse<T extends object> {
    ...Http.BadRequestResponse;

    @body
    body: InvalidIdErrorModel<T> | InvalidJsonBodyErrorModel<T> | InvalidConditionalHeaderErrorModel;
}

@doc("{name} was not found.", T)
model NotFoundResponse<T extends object> {
    ...Http.NotFoundResponse;
    @body body: ResourceNotFoundErrorModel<T>;
}

@doc("{name} already exists.", T)
model ConflictResponse<T extends object> {
    ...Http.ConflictResponse;
    @body body: ResourceAlreadyExistsErrorModel<T>;
}

@doc("""
    The specified `If-Match` or `If-None-Match` header doesn't match the server's.
    """)
model PreconditionFailedResponse<T extends object> {
    ...Response<412>;
    @body body: InvalidConditionalHeaderErrorModel;
}

@doc("""
    Must specify `If-Match` or `If-None-Match` header.
    """)
model PreconditionRequiredResponse<T extends object> {
    ...Response<428>;
    @body body: InvalidConditionalHeaderErrorModel;
}

@doc("{name} was successfully created.", T)
@withDefaultKeyVisibility("")
model CreatedResponseModel<T extends object> {
    ...T;
    eTag: eTag;
}

@doc("{name} was successfully created.", T)
model CreatedResponse<T extends object> {
    @header("Location") location: string;
    ...Rest.Resource.ResourceCreatedResponse<CreatedResponseModel<T>>;
}

@doc("{name} was successfully updated.", T)
@withDefaultKeyVisibility("")
model OkResponseModel<T extends object> {
    ...T;
    eTag: eTag;
}

@doc("{name} was successfully updated.", T)
model OkResponse<T extends object> {
    ...Response<200>;
    @body body: OkResponseModel<T>;
}

alias ResponseModel<T extends object> =
    | CreatedResponse<T>
    | OkResponse<T>
    | BadRequestResponse<T>
    | NotFoundResponse<T>
    | ConflictResponse<T>
    | PreconditionFailedResponse<T>
    | PreconditionRequiredResponse<T>;

@doc("Create or update an {name} instance.", T)
@createsOrUpdatesResource(T)
@put
op operation<T extends object>(...RequestModel<T>): ResponseModel<T>;
