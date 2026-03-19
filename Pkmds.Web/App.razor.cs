namespace Pkmds.Web;

public partial class App
{
    private ErrorBoundary? errorBoundary;
    private Exception? lastCapturedException;

    private void CaptureExceptionOnce(Exception exception)
    {
        if (ReferenceEquals(exception, lastCapturedException))
        {
            return;
        }

        lastCapturedException = exception;
        SentrySdk.CaptureException(exception);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await JsRuntime.InvokeVoidAsync("addUpdateListener");
    }
}
