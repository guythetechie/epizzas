using common;
using LanguageExt;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace portal.Components.Orders;

public partial class CreateDialog : ComponentBase, IDialogContentComponent<Unit>
{
    private string selectedSize = string.Empty;

    private readonly ImmutableArray<string> availableSizes =
        [
        nameof(PizzaSize.Small),
        nameof(PizzaSize.Medium),
        nameof(PizzaSize.Large)
        ];

    private Dictionary<PizzaToppingKind, string> toppings = new()
    {
        [PizzaToppingKind.Pepperoni] = "None",
        [PizzaToppingKind.Cheese] = "None",
        [PizzaToppingKind.Sausage] = "None"
    };

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

    private async Task OnCreateButtonClicked()
    {
        await Dialog.CloseAsync();
    }

    private async Task OnCancelButtonClicked()
    {
        await Dialog.CloseAsync();
    }
}
