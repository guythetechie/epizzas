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

@route("/{orderId}")
namespace GetById {
    model SuccessfulResponse {
        eTag: ETag;
        status: Order.status;
        pizzas: Order.pizzas;
    }

    @doc("Order not found")
    model NotFoundResponse {
        ...Http.NotFoundResponse;
        ...Error<ErrorCode.ResourceNotFound>;
    }

    @doc("Get order by ID")
    op get(@path orderId: OrderId): SuccessfulResponse | NotFoundResponse;
}

@route("/")
namespace List {
    model Response
        is PaginatedResponse<{
            ...Order;
            eTag: ETag;
        }>;

    @doc("List orders")
    op list(): Response;
}

@route("/{orderId}")
namespace Cancel {
    @doc("Cancel order")
    @delete
    op cancel(@path orderId: OrderId): void;
}

@route("/{orderId}")
namespace Create {
    model Request {
        pizzas: Order.pizzas;
    }

    @doc("Create order")
    @put
    op put(@path orderId: OrderId, @body request: Request): NoContentResponse;
}
