import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "./common.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;

@route("/orders")
@tag("Orders")
namespace api.Orders;

@doc("Order ID")
scalar OrderId extends NonEmptyString;

@route("/{orderId}")
namespace GetById {
    model SuccessfulResponse {
        eTag: ETag;
        pizzas: Pizza[];
    }

    @doc("Order not found")
    model NotFoundResponse {
        ...Http.NotFoundResponse;
        ...Error<ErrorCode.ResourceNotFound>;
    }

    @doc("Get order by ID")
    op get(@path orderId: string): SuccessfulResponse | NotFoundResponse;
}

@route("/")
namespace List {
    model Response
        is PaginatedResponse<{
            id: OrderId;
            eTag: ETag;
            pizzas: Pizza[];
        }>;

    @doc("List orders")
    op list(): Response;
}

@route("/{orderId}")
namespace Cancel {
    @doc("Cancel order")
    @delete
    op cancel(@path orderId: string): void;
}

@route("/")
namespace Create {
    model Request {
        id: OrderId;
        pizzas: Pizza[];
    }

    model Response {
        id: OrderId;
        eTag: ETag;
    }

    @doc("Create order")
    op post(@body request: Request): Response;
}