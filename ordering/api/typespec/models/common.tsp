import "@typespec/versioning";

using TypeSpec.Versioning;

@versioned(Versions)
namespace EPizzas.Ordering.Api;

enum Versions {
    v1,
}
