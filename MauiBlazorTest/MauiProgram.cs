using System.Reflection;
using MauiBlazorTest.WebRtc;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace MauiBlazorTest;

public static class MauiProgram
{
    static MauiProgram()
    {
        AppContext.SetSwitch("BlazorWebView.AndroidFireAndForgetAsync", false);
    }
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        WebViewSetup.EnableVideoFeatures();

        builder
            .UseMauiApp<App>()
           
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddLogging(logging =>
        {
            logging.AddFilter("Microsoft.AspNetCore.Components.WebView", LogLevel.Trace);
            logging.AddDebug();
        });
        var a = Assembly.GetExecutingAssembly();
        using var stream = a.GetManifestResourceStream("MauiBlazorTest.appsettings.json");

        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();


        builder.Configuration.AddConfiguration(config);
#if DEBUG
        
        builder.Services.AddScoped<WebRtcService>();
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}