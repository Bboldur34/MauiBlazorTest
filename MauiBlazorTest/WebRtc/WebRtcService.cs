using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Text.Json;

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
    private string? _userId;
    
    // Dictionary to store remote streams by peer ID
    private Dictionary<string, IJSObjectReference> _remoteStreams = new Dictionary<string, IJSObjectReference>();
    private Dictionary<string, string> _peerIdToConnectionId = new Dictionary<string, string>();
    
    // Event that fires when a new remote stream is added
    public event EventHandler<RemoteStreamEventArgs>? OnRemoteStreamAcquired;
    
    // Event that fires when a peer disconnects
    public event EventHandler<string>? OnPeerDisconnected;

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
            
            // Generate a unique user ID for this session
            _userId = Guid.NewGuid().ToString();
            
            var hub = await GetHub();
            await hub.SendAsync("Join", signalingChannel);
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

    // Call a specific peer
    public async Task Call(string connectionId)
    {
        try
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException();
            }

            var offerData = await _jsModule.InvokeAsync<string>("callAction", connectionId);
            await SendOffer(offerData);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    // Call all participants in the current channel - note that we don't have a direct API for this in the hub
    // so our implementation will just wait for the "Join" event from the hub to notify us of new peers
    public async Task CallAllParticipants()
    {
        // This is a placeholder as the API doesn't have a way to get all participants
        // We'll rely on the "Join" events to track connections
        Console.WriteLine("Waiting for peers to join the channel");
    }

    // Hangup with a specific peer or with all peers if peerId is null
    public async Task Hangup(string peerId = null)
    {
        try
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException();
            }

            await _jsModule.InvokeVoidAsync("hangupAction", peerId);
            
            if (peerId == null)
            {
                // Clear all remote streams
                _remoteStreams.Clear();
                _peerIdToConnectionId.Clear();
            }
            else if (_remoteStreams.ContainsKey(peerId))
            {
                // Remove specific remote stream
                _remoteStreams.Remove(peerId);
                _peerIdToConnectionId.Remove(peerId);
            }
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

            // When a new connection joins the group
            hub.On<string>("Join", async (connectionId) => 
            {
                // Store the connection ID as a peer ID for simplicity
                string peerId = connectionId;
                _peerIdToConnectionId[peerId] = connectionId;
                
                // Make a call to this new peer
                await Call(peerId);
            });
            
            // When a connection leaves the group
            hub.On<string>("Leave", (connectionId) => 
            {
                // Find the peer ID for this connection ID
                var peerId = _peerIdToConnectionId.FirstOrDefault(x => x.Value == connectionId).Key;
                if (!string.IsNullOrEmpty(peerId))
                {
                    OnPeerDisconnected?.Invoke(this, peerId);
                    _peerIdToConnectionId.Remove(peerId);
                }
            });

            // Handle signaling messages
            hub.On<string, string, string>("SignalWebRtc", async (channel, type, payload) =>
            {
                if (_jsModule == null)
                {
                    throw new InvalidOperationException();
                }

                if (_signalingChannel != channel)
                {
                    return;
                }
                
                // Extract connection ID/peer ID from the payload
                // The payload format will depend on how your app is structured
                try
                {
                    // Use System.Text.Json for parsing
                    using JsonDocument doc = JsonDocument.Parse(payload);
                    JsonElement root = doc.RootElement;
                    
                    // Try to extract senderId - this is our convention for identifying peers
                    string senderId = "";
                    if (root.TryGetProperty("senderId", out JsonElement senderIdElement))
                    {
                        senderId = senderIdElement.GetString();
                    }
                    else
                    {
                        // Fallback - try to find any identifier property
                        Console.WriteLine("Warning: Could not find senderId in payload");
                        senderId = Guid.NewGuid().ToString();
                    }
                    
                    switch (type)
                    {
                        case "offer":
                            // Store mapping between this connection ID and the peer ID
                            _peerIdToConnectionId[senderId] = senderId;
                            
                            // Create a proper SDP object structure for the JavaScript side
                            JsonElement offerElement;
                            if (root.TryGetProperty("offer", out offerElement))
                            {
                                var offerData = new
                                {
                                    peerId = senderId,
                                    description = new
                                    {
                                        type = "offer",
                                        sdp = offerElement.ToString()
                                    }
                                };
                                var offerJson = System.Text.Json.JsonSerializer.Serialize(offerData);
                                Console.WriteLine($"Processing offer from {senderId}, SDP: {offerJson}");
                                await _jsModule.InvokeVoidAsync("processOffer", offerJson);
                            }
                            else
                            {
                                Console.WriteLine("Error: Offer property not found in payload");
                            }
                            break;
                            
                        case "answer":
                            JsonElement answerElement;
                            if (root.TryGetProperty("answer", out answerElement))
                            {
                                var answerData = new
                                {
                                    peerId = senderId,
                                    description = new
                                    {
                                        type = "answer",
                                        sdp = answerElement.ToString()
                                    }
                                };
                                var answerJson = System.Text.Json.JsonSerializer.Serialize(answerData);
                                Console.WriteLine($"Processing answer from {senderId}, SDP: {answerJson}");
                                await _jsModule.InvokeVoidAsync("processAnswer", answerJson);
                            }
                            else
                            {
                                Console.WriteLine("Error: Answer property not found in payload");
                            }
                            break;
                            
                        case "candidate":
                            JsonElement candidateElement;
                            if (root.TryGetProperty("candidate", out candidateElement))
                            {
                                var candidateData = new
                                {
                                    peerId = senderId,
                                    candidate = candidateElement
                                };
                                var candidateJson = System.Text.Json.JsonSerializer.Serialize(candidateData);
                                await _jsModule.InvokeVoidAsync("processCandidate", candidateJson);
                            }
                            else
                            {
                                Console.WriteLine("Error: Candidate property not found in payload");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing signaling message: {ex.Message}");
                    Console.WriteLine($"Payload: {payload}");
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
    public async Task SendOffer(string offerData)
    {
        try
        {
            var hub = await GetHub();
            Console.WriteLine($"Sending offer data: {offerData}");
            
            // Parse offer data
            var offerObj = System.Text.Json.JsonSerializer.Deserialize<OfferData>(offerData);
            
            // Ensure we're sending the correct format to match what the server expects
            var sdpData = offerObj.Description;
            
            // Send offer using the SignalWebRtc method with proper parameters
            string payload = System.Text.Json.JsonSerializer.Serialize(new {
                senderId = _userId,
                offer = sdpData
            });
            
            Console.WriteLine($"Sending offer payload: {payload}");
            await hub.SendAsync("SignalWebRtc", _signalingChannel, "offer", payload);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error sending offer: {e.Message}");
            Console.WriteLine($"Stack trace: {e.StackTrace}");
        }
    }

    [JSInvokable]
    public async Task SendAnswer(string answerData)
    {
        try
        {
            var hub = await GetHub();
            Console.WriteLine($"Sending answer data: {answerData}");
            
            // Parse answer data
            var answerObj = System.Text.Json.JsonSerializer.Deserialize<AnswerData>(answerData);
            
            // Ensure we're sending the correct format
            var sdpData = answerObj.Description;
            
            // Send answer using the SignalWebRtc method with proper parameters
            string payload = System.Text.Json.JsonSerializer.Serialize(new {
                senderId = _userId,
                answer = sdpData
            });
            
            Console.WriteLine($"Sending answer payload: {payload}");
            await hub.SendAsync("SignalWebRtc", _signalingChannel, "answer", payload);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error sending answer: {e.Message}");
            Console.WriteLine($"Stack trace: {e.StackTrace}");
        }
    }

    [JSInvokable]
    public async Task SendCandidate(string candidateData)
    {
        try
        {
            var hub = await GetHub();
            Console.WriteLine($"Sending candidate data: {candidateData}");
            
            // Parse candidate data
            var candidateObj = System.Text.Json.JsonSerializer.Deserialize<CandidateData>(candidateData);
            
            // Send ICE candidate using the SignalWebRtc method with proper parameters
            string payload = System.Text.Json.JsonSerializer.Serialize(new {
                senderId = _userId,
                candidate = candidateObj.Candidate
            });
            
            await hub.SendAsync("SignalWebRtc", _signalingChannel, "candidate", payload);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error sending candidate: {e.Message}");
            Console.WriteLine($"Stack trace: {e.StackTrace}");
        }
    }

    [JSInvokable]
    public async Task SetRemoteStream(string peerId)
    {
        try
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException();
            }

            var stream = await _jsModule.InvokeAsync<IJSObjectReference>("getRemoteStream", peerId);
            _remoteStreams[peerId] = stream;
            OnRemoteStreamAcquired?.Invoke(this, new RemoteStreamEventArgs(peerId, stream));
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
    
    [JSInvokable]
    public void PeerDisconnected(string peerId)
    {
        if (_remoteStreams.ContainsKey(peerId))
        {
            _remoteStreams.Remove(peerId);
        }
        
        // Remove the peer ID to connection ID mapping
        if (_peerIdToConnectionId.ContainsKey(peerId))
        {
            _peerIdToConnectionId.Remove(peerId);
        }
        
        // Notify listeners about peer disconnection
        OnPeerDisconnected?.Invoke(this, peerId);
    }
    
    // Helper class for JSON deserialization of offer data
    private class OfferData
    {
        public string PeerId { get; set; }
        public object Description { get; set; }
    }
    
    // Helper class for JSON deserialization of answer data
    private class AnswerData
    {
        public string PeerId { get; set; }
        public object Description { get; set; }
    }
    
    // Helper class for JSON deserialization of candidate data
    private class CandidateData
    {
        public string PeerId { get; set; }
        public object Candidate { get; set; }
    }
}

// Event arguments for remote stream events
public class RemoteStreamEventArgs : EventArgs
{
    public string PeerId { get; }
    public IJSObjectReference Stream { get; }
    
    public RemoteStreamEventArgs(string peerId, IJSObjectReference stream)
    {
        PeerId = peerId;
        Stream = stream;
    }
}