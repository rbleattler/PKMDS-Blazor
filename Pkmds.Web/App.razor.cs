namespace Pkmds.Web;

public partial class App
{
    private ErrorBoundary? errorBoundary;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await JsRuntime.InvokeVoidAsync("addUpdateListener");
    }
}
