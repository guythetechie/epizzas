import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "./common.tsp";
import "./orders.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;

@useAuth(BearerAuth)
@versioned(Versions)
@service({
    title: "EPizzas API",
})
namespace api;
