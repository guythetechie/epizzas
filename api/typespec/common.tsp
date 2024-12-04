enum Versions {
    v1,
}

@minLength(1)
scalar NonEmptyString extends string;

@doc("The ETag.")
scalar ETag extends NonEmptyString;

@doc("User ID")
scalar UserId extends NonEmptyString;

enum ErrorCode {
    InvalidRequestParameter,
    ResourceNotFound,
}

model Error<T extends ErrorCode> {
    code: T;
    message: string;
    details?: [];
}

model PaginatedResponse<T> {
    @doc("Current page of results.")
    value: T[];

    @doc("URL to get the next page of results.")
    nextPageLink?: url;
}

@doc("Pizza topping kind")
enum PizzaToppingKind {
    Cheese,
    Pepperoni,
    Sausage,
}

@doc("Pizza topping amount")
enum PizzaToppingAmount {
    Light,
    Normal,
    Extra,
}

@doc("Pizza size")
enum PizzaSize {
    Small,
    Medium,
    Large,
}

@doc("Pizza")
model Pizza {
    size: PizzaSize;
    toppings: {
        kind: PizzaToppingKind;
        amount: PizzaToppingAmount;
    }[];
}

@doc("Order ID")
scalar OrderId extends NonEmptyString;

model OrderStatusBase {
    name: NonEmptyString;
    date: utcDateTime;
    by: UserId;
}

model OrderStatusCreated extends OrderStatusBase {
    name: "Created";
}

model OrderStatusCancelled extends OrderStatusBase {
    name: "Cancelled";
}

@doc("Order status")
union OrderStatus {
    created: OrderStatusCreated,
    cancelled: OrderStatusCancelled,
}

@doc("Order")
model Order {
    orderId: OrderId;
    status: OrderStatus;

    @minItems(1)
    pizzas: Pizza[];
}
