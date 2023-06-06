import "@typespec/http";
import "@typespec/rest";
import "../models/common.tsp";
import "../models/orders.tsp";
import "../handlers/get.tsp";
import "../handlers/list.tsp";
import "../handlers/put.tsp";
import "../handlers/delete.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Rest.Resource;
using EPizzas.Ordering.Api.Handlers;

namespace EPizzas.Ordering.Api;

@tag("Orders")
@route("v1/orders")
interface OrderService {
    @doc("Get an order.")
    getOrder is Handlers.Get.operation<Order>;

    @doc("List orders.")
    listOrders is Handlers.List.operation<Order>;

    @doc("Create or update an order.")
    createOrUpdateOrder is Handlers.Put.operation<Order>;

    @doc("Cancel an order.")
    @deletesResource(Order)
    cancelOrder is Handlers.Delete.operation<Order>;
}
    
@@doc(EPizzas.Ordering.Api.Handlers.Delete.RequestModel.ifMatch, "Cancel order if the server's ETag matches this header.");
@@doc(EPizzas.Ordering.Api.Handlers.Delete.NoContentResponse, "Order was successfully canceled.");