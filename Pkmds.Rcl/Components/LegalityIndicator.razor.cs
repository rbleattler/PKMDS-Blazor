using PKHexSeverity = PKHeX.Core.Severity;

namespace Pkmds.Rcl.Components;

public partial class LegalityIndicator
{
    [Parameter]
    public PKHexSeverity Severity { get; set; } = PKHexSeverity.Valid;

    [Parameter]
    public string Message { get; set; } = string.Empty;

    private string GetIcon() => Severity switch
    {
        PKHexSeverity.Fishy => Icons.Material.Filled.Warning,
        _ => Icons.Material.Filled.Cancel
    };

    private Color GetColor() => Severity switch
    {
        PKHexSeverity.Fishy => Color.Warning,
        _ => Color.Error
    };
}
