namespace MauiBlazorTest;
internal static class WebViewSetup
{
    public static void EnableVideoFeatures()
    {
#if ANDROID
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.ModifyMapping(
            nameof(Android.Webkit.WebView.WebChromeClient),
            (handler, _, _) =>
            {
                var settings = handler.PlatformView.Settings;

                settings.MediaPlaybackRequiresUserGesture = false;
                settings.JavaScriptEnabled = true;

                handler.PlatformView.SetWebChromeClient(
                    new CustomWebChromeClient(handler));
            });
#endif
    }
}