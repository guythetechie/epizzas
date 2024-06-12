namespace common

open System

type OrderId =
    private
    | OrderId of string

    static member fromString value =
        if String.IsNullOrWhiteSpace value then
            invalidOp "Order ID cannot be empty."
        else
            OrderId value

    static member toString(OrderId value) = value

type OrderStatus =
    | Pending
    | Canceled

    static member toString value =
        match value with
        | Pending -> nameof Pending
        | Canceled -> nameof Canceled

    static member fromString value =
        match value with
        | _ when String.Equals(value, nameof Pending, StringComparison.OrdinalIgnoreCase) -> Pending
        | _ when String.Equals(value, nameof Canceled, StringComparison.OrdinalIgnoreCase) -> Canceled
        | _ -> invalidOp ($"'{value}' is not a valid order status.")

type Order =
    { Id: OrderId
      Status: OrderStatus
      Pizzas: Pizza list }
