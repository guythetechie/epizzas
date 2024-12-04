namespace common

open System

type PizzaToppingKind =
    | Cheese
    | Pepperoni
    | Sausage

    static member fromString value =
        match value with
        | _ when nameof(Cheese).Equals(value, StringComparison.OrdinalIgnoreCase) -> Ok Cheese
        | _ when nameof(Pepperoni).Equals(value, StringComparison.OrdinalIgnoreCase) -> Ok Pepperoni
        | _ when nameof(Sausage).Equals(value, StringComparison.OrdinalIgnoreCase) -> Ok Sausage
        | _ -> Error $"'{value}' is not a valid pizza topping kind."

    static member toString value =
        match value with
        | Cheese -> nameof (Cheese)
        | Pepperoni -> nameof (Pepperoni)
        | Sausage -> nameof (Sausage)

type PizzaToppingAmount =
    | Light
    | Normal
    | Extra

    static member fromString value =
        match value with
        | _ when nameof(Light).Equals(value, StringComparison.OrdinalIgnoreCase) -> Ok Light
        | _ when nameof(Normal).Equals(value, StringComparison.OrdinalIgnoreCase) -> Ok Normal
        | _ when nameof(Extra).Equals(value, StringComparison.OrdinalIgnoreCase) -> Ok Extra
        | _ -> Error $"'{value}' is not a valid pizza topping amount."

    static member toString value =
        match value with
        | Light -> nameof (Light)
        | Normal -> nameof (Normal)
        | Extra -> nameof (Extra)

type PizzaSize =
    | Small
    | Medium
    | Large

    static member fromString value =
        match value with
        | _ when nameof(Small).Equals(value, StringComparison.OrdinalIgnoreCase) -> Ok Small
        | _ when nameof(Medium).Equals(value, StringComparison.OrdinalIgnoreCase) -> Ok Medium
        | _ when nameof(Large).Equals(value, StringComparison.OrdinalIgnoreCase) -> Ok Large
        | _ -> Error $"'{value}' is not a valid pizza size."

    static member toString value =
        match value with
        | Small -> nameof (Small)
        | Medium -> nameof (Medium)
        | Large -> nameof (Large)

type Pizza =
    { Size: PizzaSize
      Toppings: Map<PizzaToppingKind, PizzaToppingAmount> }

type OrderId =
    private
    | OrderId of string

    static member fromString value =
        if String.IsNullOrWhiteSpace value then
            Error "Order ID cannot be null or whitespace."
        else
            OrderId value |> Ok

    static member toString(OrderId value) = value

type OrderStatus =
    | Created of {| By: string; Date: DateTimeOffset |}
    | Cancelled of {| By: string; Date: DateTimeOffset |}

type Order =
    { Id: OrderId
      Status: OrderStatus
      Pizzas: Pizza seq }
