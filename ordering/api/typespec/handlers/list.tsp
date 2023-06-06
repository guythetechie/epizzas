import "@typespec/http";
import "@typespec/rest";
import "./common.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Rest.Resource;

namespace EPizzas.Ordering.Api.Handlers.List;

@doc("Parameters for shaping list results.")
model RequestModel {
    @doc("Continuation token.")
    @query("continuationToken")
    continationToken?: ContinuationToken;
}

@doc("The request is invalid.")
model BadRequestResponse<T extends object> {
    ...Http.BadRequestResponse;
    @body body: {
        ...InvalidContinuationTokenModel;
    };
}

@doc("{name} instances were listed successfully.", T)
model OkResponseModel<T extends object> {
    ...CollectionWithNextLink<{
        ...T;
        eTag: eTag;
    }>;
}

@doc("{name} instances were listed successfully.", T)
model OkResponse<T extends object> {
    ...Response<200>;
    @body body: OkResponseModel<T>;
}

alias ResponseModel<T extends object> = OkResponse<T> | BadRequestResponse<T> | OkResponse<T>;

@doc("List {name} instances.", T)
@listsResource(T)
op operation<T extends object>(...RequestModel): ResponseModel<T>;
