namespace common

open System

type ToppingKind =
    | Cheese
    | Pepperoni
    | Sausage

    static member toString value =
        match value with
        | Cheese -> nameof Cheese
        | Pepperoni -> nameof Pepperoni
        | Sausage -> nameof Sausage

    static member fromString value =
        match value with
        | _ when String.Equals(value, nameof Cheese, StringComparison.OrdinalIgnoreCase) -> Cheese
        | _ when String.Equals(value, nameof Pepperoni, StringComparison.OrdinalIgnoreCase) -> Pepperoni
        | _ when String.Equals(value, nameof Sausage, StringComparison.OrdinalIgnoreCase) -> Sausage
        | _ -> invalidOp ($"'{value}' is not a valid topping kind.")

type ToppingAmount =
    | Light
    | Normal
    | Extra

    static member toString value =
        match value with
        | Light -> nameof Light
        | Normal -> nameof Normal
        | Extra -> nameof Extra

    static member fromString value =
        match value with
        | _ when String.Equals(value, nameof Light, StringComparison.OrdinalIgnoreCase) -> Light
        | _ when String.Equals(value, nameof Normal, StringComparison.OrdinalIgnoreCase) -> Normal
        | _ when String.Equals(value, nameof Extra, StringComparison.OrdinalIgnoreCase) -> Extra
        | _ -> invalidOp ($"'{value}' is not a valid topping amount.")

type PizzaSize =
    | Small
    | Medium
    | Large

    static member toString value =
        match value with
        | Small -> nameof Small
        | Medium -> nameof Medium
        | Large -> nameof Large

    static member fromString value =
        match value with
        | _ when String.Equals(value, nameof Small, StringComparison.OrdinalIgnoreCase) -> Small
        | _ when String.Equals(value, nameof Medium, StringComparison.OrdinalIgnoreCase) -> Medium
        | _ when String.Equals(value, nameof Large, StringComparison.OrdinalIgnoreCase) -> Large
        | _ -> invalidOp ($"'{value}' is not a valid pizza size.")

type Pizza =
    { Size: PizzaSize
      Toppings: Map<ToppingKind, ToppingAmount> }
