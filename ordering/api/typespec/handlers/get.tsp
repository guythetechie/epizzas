import "@typespec/http";
import "@typespec/rest";
import "./common.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Rest.Resource;

namespace EPizzas.Ordering.Api.Handlers.Get;

model RequestModel<T extends object> {
    ...ResourceParameters<T>;
}

@withDefaultKeyVisibility("")
@doc("{name} was found.", T)
model OkResponseModel<T extends object> {
    ...T;
    eTag: eTag;
}

@doc("{name} was found.", T)
model OkResponse<T extends object> {
    ...Response<200>;
    @body body: OkResponseModel<T>;
}

@doc("The request is invalid.")
model BadRequestResponse<T extends object> {
    ...Http.BadRequestResponse;
    @body body: {
        ...InvalidIdErrorModel<T>;
    };
}

@doc("{name} was not found.", T)
model NotFoundResponse<T extends object> {
    ...Http.NotFoundResponse;
    @body body: ResourceNotFoundErrorModel<T>;
}

alias ResponseModel<T extends object> = OkResponse<T> | BadRequestResponse<T> | NotFoundResponse<T>;

@doc("Get an {name} instance.", T)
@readsResource(T)
op operation<T extends object>(...RequestModel<T>): ResponseModel<T>;