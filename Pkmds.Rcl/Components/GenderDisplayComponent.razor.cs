namespace Pkmds.Rcl.Components;

public partial class GenderDisplayComponent
{
    [Parameter]
    public Gender Gender { get; set; }

    [Parameter]
    public EventCallback<Gender> OnChange { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public bool IncludeGenderless { get; set; }
}
