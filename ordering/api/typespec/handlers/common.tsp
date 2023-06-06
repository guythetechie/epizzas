import "@typespec/http";

using TypeSpec.Http;

namespace EPizzas.Ordering.Api.Handlers;

scalar eTag extends string;

scalar ContinuationToken extends string;

@doc("Error code.")
union ErrorCode {
    resourceNotFound: "ResourceNotFound",
    resourceAlreadyExists: "ResourceAlreadyExists",
    invalidConditionalHeader: "InvalidConditionalHeader",
    invalidJsonBody: "InvalidJsonBody",
    invalidId: "InvalidId",
    eTagMismatch: "ETagMismatch",
    internalServerError: "InternalServerError",
    invalidContinuationToken: "InvalidContinuationToken",
}

@doc("{name} was not found.", T)
model ResourceNotFoundErrorModel<T extends object> {
    code: ErrorCode.resourceNotFound;
    message: string;
}

@doc("{name} already exists.", T)
model ResourceAlreadyExistsErrorModel<T extends object> {
    code: ErrorCode.resourceAlreadyExists;
    message: string;
}

@doc("Invalid conditional header.")
model InvalidConditionalHeaderErrorModel {
    code: ErrorCode.invalidConditionalHeader;
    message: string;
}

@doc("Cannot get {name} from JSON body.", T)
model InvalidJsonBodyErrorModel<T extends object> {
    code: ErrorCode.invalidJsonBody;
    message: string;
    details: {
        code: ErrorCode.invalidJsonBody;
        message: string;
    };
}

@doc("{name} ID is invalid.", T)
model InvalidIdErrorModel<T extends object> {
    code: ErrorCode.invalidId;
    message: string;
}

@doc("ETag mismatch.")
model ETagMismatchModel {
    code: ErrorCode.eTagMismatch;
    message: string;
}

@doc("Invalid continuation token.")
model InvalidContinuationTokenModel {
    code: ErrorCode.invalidContinuationToken;
    message: string;
}