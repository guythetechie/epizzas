enum Versions {
    v1,
}

@minLength(1)
scalar NonEmptyString extends string;

@doc("The ETag.")
scalar ETag extends NonEmptyString;

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
