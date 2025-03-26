﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace MauiBlazorTest.WebRtc;

public class WebRtcService
{
    private readonly NavigationManager _nav;
    private readonly IJSRuntime _js;
    private readonly string _signallingServerBaseUrl;

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<WebRtcService>? _jsThis;
    private HubConnection? _hub;
    private string? _signalingChannel;
    public event EventHandler<IJSObjectReference>? OnRemoteStreamAcquired;

    public WebRtcService(IJSRuntime js, NavigationManager nav, IConfiguration configuration)
    {
        _js = js;
        _nav = nav;
        _signallingServerBaseUrl = configuration["SignalingServer:BaseUrl"];
    }

    public async Task Join(string signalingChannel)
    {
        try
        {
            _jsModule = await _js.InvokeAsync<IJSObjectReference>(
                "import", "/js/WebRtcService.cs.js");
            _jsThis = DotNetObjectReference.Create(this);
            
            if (_signalingChannel != null)
            {
                throw new InvalidOperationException();
            }

            _signalingChannel = signalingChannel;
            var hub = await GetHub();
            await hub.SendAsync("join", signalingChannel);
            await _jsModule.InvokeVoidAsync("initialize", _jsThis);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public async Task<IJSObjectReference> StartLocalStream()
    {
        IJSObjectReference stream = null;
        try
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException();
            }

            stream = await _jsModule.InvokeAsync<IJSObjectReference>("startLocalStream");
            return stream;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        return stream;
    }

    public async Task Call()
    {
        try
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException();
            }

            var offerDescription = await _jsModule.InvokeAsync<string>("callAction");
            await SendOffer(offerDescription);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public async Task Hangup()
    {
        try
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException();
            }

            await _jsModule.InvokeVoidAsync("hangupAction");

            var hub = await GetHub();
            await hub.SendAsync("leave", _signalingChannel);

            _signalingChannel = null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private async Task<HubConnection> GetHub()
    {
        try
        {
            if (_hub != null)
            {
                return _hub;
            }

            var hub = new HubConnectionBuilder()
                .WithUrl(_signallingServerBaseUrl + "/messagehub", (opts) =>
                {
                
                })
                .Build();

            hub.On<string, string, string>("SignalWebRtc", async (signalingChannel, type, payload) =>
            {
                if (_jsModule == null)
                {
                    throw new InvalidOperationException();
                }

                if (_signalingChannel != signalingChannel)
                {
                    return;
                }

                switch (type)
                {
                    case "offer":
                        await _jsModule.InvokeVoidAsync("processOffer", payload);
                        break;
                    case "answer":
                        await _jsModule.InvokeVoidAsync("processAnswer", payload);
                        break;
                    case "candidate":
                        await _jsModule.InvokeVoidAsync("processCandidate", payload);
                        break;
                }
            });


            await hub.StartAsync();
            _hub = hub;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        return _hub;
    }

    [JSInvokable]
    public async Task SendOffer(string offer)
    {
        try
        {
            var hub = await GetHub();
            await hub.SendAsync("SignalWebRtc", _signalingChannel, "offer", offer);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    [JSInvokable]
    public async Task SendAnswer(string answer)
    {
        try
        {
            var hub = await GetHub();
            await hub.SendAsync("SignalWebRtc", _signalingChannel, "answer", answer);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    [JSInvokable]
    public async Task SendCandidate(string candidate)
    {
        try
        {
            var hub = await GetHub();
            await hub.SendAsync("SignalWebRtc", _signalingChannel, "candidate", candidate);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    [JSInvokable]
    public async Task SetRemoteStream()
    {
        try
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException();
            }

            var stream = await _jsModule.InvokeAsync<IJSObjectReference>("getRemoteStream");
            OnRemoteStreamAcquired?.Invoke(this, stream);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}