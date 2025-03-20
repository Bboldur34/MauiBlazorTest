using Android.Webkit;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace MauiBlazorTest;

public class CustomWebChromeClient : MauiWebChromeClient
{
    public CustomWebChromeClient(IWebViewHandler handler) : base(handler)
    {
    }

    public override void OnPermissionRequest(PermissionRequest? request)
    {
        request?.Grant(request.GetResources());
    }
}