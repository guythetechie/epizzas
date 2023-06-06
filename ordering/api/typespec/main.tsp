import "@typespec/http";
import "@typespec/versioning";
import "./models/common.tsp";
import "./services/orders.tsp";

using TypeSpec.Http;

@useAuth(BearerAuth)
@service({
    title: "Ordering API",
})
@doc("Ordering API")
namespace EPizzas.Ordering.Api;
