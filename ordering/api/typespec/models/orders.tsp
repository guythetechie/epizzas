import "@typespec/rest";
import "@typespec/versioning";
import "./common.tsp";

namespace EPizzas.Ordering.Api;

using TypeSpec.Rest;
using TypeSpec.Versioning;

@doc("The topping type.")
union ToppingType {
    pepperoni: "Pepperoni",
    ham: "Ham",
    cheese: "Cheese",
}

@doc("The topping amount.")
union ToppingAmount {
    light: "Light",
    medium: "Medium",
    extra: "Extra",
}

@doc("The pizza topping.")
model Topping {
    type: ToppingType;
    amount: ToppingAmount;
}

@doc("The pizza size.")
union PizzaSize {
    small: "Small",
    medium: "Medium",
    Large: "Large",
}

@doc("The pizza.")
model Pizza {
    size: PizzaSize;
    toppings: Topping[];
}

@doc("The order status.")
union OrderStatus {
    new: "New",
    canceled: "Canceled",
}

@doc("A pizza order.")
@resource("Order")
model Order {
    @doc("The order ID.")
    @key("orderId")
    id: string;
    pizzas: Pizza[];

    @visibility("read")
    status: OrderStatus;
}
