using common;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace portal.Components.Orders;

public partial class CreateDialog(CreateOrder createOrder) : ComponentBase, IDialogContentComponent<Unit>
{
    private Model formModel = new();
    private EditContext? editContext;
    private ValidationMessageStore? validationMessageStore;

    private readonly ImmutableArray<string> availableSizes =
        [
        nameof(PizzaSize.Small),
        nameof(PizzaSize.Medium),
        nameof(PizzaSize.Large)
        ];

    private readonly ImmutableArray<string> availableToppingAmounts =
        [
        "None",
        nameof(PizzaToppingAmount.Light),
        nameof(PizzaToppingAmount.Normal),
        nameof(PizzaToppingAmount.Extra)
        ];

    [Parameter]
    public Unit Content { get; set; } = Unit.Default;

    [CascadingParameter]
    public FluentDialog Dialog { get; set; } = default!;

    protected override void OnInitialized()
    {
        editContext = new(formModel);
        validationMessageStore = new(editContext);
        base.OnInitialized();
    }

    private async Task OnCreateButtonClicked()
    {
        validationMessageStore?.Clear();

        await ValidatePizza()
                .Match(async pizza =>
                       {
                           await createOrder(pizza, CancellationToken.None);
                           await Dialog.CloseAsync(Unit.Default);
                       },
                       error => Task.CompletedTask);
    }

    private async Task OnCancelButtonClicked()
    {
        await Dialog.CancelAsync();
    }

    private Fin<Pizza> ValidatePizza() =>
        (ValidateSize(),
         ValidateToppings())
        .Apply((size, toppings) => new Pizza
        {
            Size = size,
            Toppings = toppings
        })
        .As();

    private Fin<PizzaSize> ValidateSize()
    {
        var fin = PizzaSize.From(formModel.Size)
                           .MapFail(error => Error.New("Select a pizza size."));

        fin.IfFail(error => validationMessageStore?.Add(() => formModel.Size, error.Message));

        return fin;
    }

    private Fin<FrozenDictionary<PizzaToppingKind, PizzaToppingAmount>> ValidateToppings()
    {
        var fin = formModel.Toppings
                           .WhereValue(amount => amount != "None")
                           .AsIterable()
                           .Traverse(kvp => PizzaToppingAmount.From(kvp.Value)
                                                              .Select(amount => KeyValuePair.Create(kvp.Key, amount)))
                           .Map(kvps => kvps.ToFrozenDictionary())
                           .As();

        fin.IfFail(error => error.AsIterable()
                                 .Iter(error => validationMessageStore?.Add(() => formModel.Size, error.Message)));

        return fin;
    }

    private sealed record Model
    {
        public string Size { get; set; } = string.Empty;
        public Dictionary<PizzaToppingKind, string> Toppings { get; set; } = new()
        {
            [PizzaToppingKind.Pepperoni.Instance] = "None",
            [PizzaToppingKind.Cheese.Instance] = "None",
            [PizzaToppingKind.Sausage.Instance] = "None"
        };
    }
}
