"use strict";
// Set up media stream constant and parameters.
const mediaStreamConstraints = {
    video: true,
    audio: true
};

// Set up to exchange only video.
const offerOptions = {
    offerToReceiveVideo: 1,
    offerToReceiveAudio: 1
};

const servers = {
    iceServers: [
        {
            urls: "turn:20.117.201.117:3478",
            username: "turnuser",
            credential: "Eb6fDuzMg^1N0lpP",
        }
    ]
}

let dotNet;
let localStream;
// Replace single remoteStream with a map of remoteStreams for multiple peers
const remoteStreams = {};
// Replace single peerConnection with a map of peerConnections
const peerConnections = {};

export function initialize(dotNetRef) {
    try{
        dotNet = dotNetRef;
    }catch (e) {
        console.error(e);
    }
}

export async function startLocalStream() {
    console.debug("Requesting local stream.");
    localStream = await navigator.mediaDevices.getUserMedia(mediaStreamConstraints);
    return localStream;
}

async function createPeerConnection(peerId) {
    if (peerConnections[peerId] != null) return;
    
    // Create peer connection and add behavior
    const peerConnection = new RTCPeerConnection(servers);
    console.debug(`Created peer connection for peer: ${peerId}`);
    
    // Store the peer connection in our map
    peerConnections[peerId] = peerConnection;
    
    // Set up event handlers with the peer ID context
    peerConnection.addEventListener("icecandidate", (event) => handleConnection(event, peerId));
    peerConnection.addEventListener("iceconnectionstatechange", (event) => handleConnectionChange(event, peerId));
    peerConnection.addEventListener("track", (event) => gotRemoteMediaStream(event, peerId));
    localStream = await navigator.mediaDevices.getUserMedia(mediaStreamConstraints);

    // Add local stream to connection
    localStream.getTracks().forEach(track => {
        peerConnection.addTrack(track, localStream);
    });
    
    console.debug(`Added local stream to peer connection for: ${peerId}`);
    return peerConnection;
}

// First flow: This client initiates call to a specific peer
export async function callAction(peerId) {
    console.log(`Starting call to peer: ${peerId}`);
    const peerConnection = createPeerConnection(peerId);
    console.debug(`Creating offer for peer: ${peerId}`);
    
    try {
        const offerDescription = await peerConnection.createOffer(offerOptions);
        console.debug(`Offer from peerConnection to ${peerId}:\n${offerDescription.sdp}`);
        console.debug(`Setting local description for peer: ${peerId}`);
        await peerConnection.setLocalDescription(offerDescription);
        console.debug(`Local description set successfully for peer: ${peerId}`);
        
        // Return the offer along with the peer ID
        return JSON.stringify({
            peerId: peerId,
            description: offerDescription
        });
    } catch (error) {
        console.error(`Error creating offer for peer ${peerId}:`, error);
        throw error;
    }
}

// Handle incoming answer from a peer
export async function processAnswer(data) {
    const parsedData = JSON.parse(data);
    const peerId = parsedData.peerId;
    const descriptionData = parsedData.description;
    
    console.debug(`Processing answer from peer: ${peerId}`);
    
    const peerConnection = peerConnections[peerId];
    if (!peerConnection) {
        console.error(`No peer connection found for peer: ${peerId}`);
        return;
    }
    
    try {
        console.debug(`Setting remote description for peer: ${peerId}`);
        
        // Ensure we have a proper RTCSessionDescription object
        const description = new RTCSessionDescription({
            type: 'answer',
            sdp: typeof descriptionData.sdp === 'string' ? descriptionData.sdp : descriptionData.sdp?.toString()
        });
        
        console.log("Answer SDP:", description.sdp);
        await peerConnection.setRemoteDescription(description);
        console.debug(`Remote description set successfully for peer: ${peerId}`);
    } catch (error) {
        console.error(`Error setting remote description for peer ${peerId}:`, error);
    }
}

// Handle incoming offer from a peer
export async function processOffer(data) {
    const parsedData = JSON.parse(data);
    const peerId = parsedData.peerId;
    const descriptionData = parsedData.description;
    
    console.debug(`Processing offer from peer: ${peerId}`);
    console.debug("Raw description data:", descriptionData);
    
    // Create peer connection if it doesn't exist
    const peerConnection = createPeerConnection(peerId);
    
    try {
        console.debug(`Setting remote description for peer: ${peerId}`);
        
        // Ensure we have a proper RTCSessionDescription object
        const description = new RTCSessionDescription({
            type: 'offer',
            sdp: typeof descriptionData.sdp === 'string' ? descriptionData.sdp : descriptionData.sdp?.toString()
        });
        
        console.log("Offer SDP:", description.sdp);
        await peerConnection.setRemoteDescription(description);
        
        console.debug(`Creating answer for peer: ${peerId}`);
        const answer = await peerConnection.createAnswer();
        console.debug(`Answer for peer ${peerId}: ${answer.sdp}`);
        
        console.debug(`Setting local description for peer: ${peerId}`);
        await peerConnection.setLocalDescription(answer);
        
        // Send answer back to peer
        console.debug(`Sending answer to peer: ${peerId}`);
        await dotNet.invokeMethodAsync("SendAnswer", JSON.stringify({
            peerId: peerId,
            description: answer
        }));
    } catch (error) {
        console.error(`Error processing offer from peer ${peerId}:`, error);
    }
}

// Handle ICE candidates from peers
export async function processCandidate(data) {
    const parsedData = JSON.parse(data);
    const peerId = parsedData.peerId;
    const candidate = parsedData.candidate;
    
    console.debug(`Processing ICE candidate from peer: ${peerId}`);
    
    const peerConnection = peerConnections[peerId];
    if (!peerConnection) {
        console.error(`No peer connection found for peer: ${peerId}`);
        return;
    }
    
    try {
        await peerConnection.addIceCandidate(candidate);
        console.debug(`Added ICE candidate for peer: ${peerId}`);
    } catch (error) {
        console.error(`Error adding ICE candidate for peer ${peerId}:`, error);
    }
}

// Handles hangup action for a specific peer or all peers
export function hangupAction(peerId = null) {
    if (peerId) {
        // Hang up with a specific peer
        const peerConnection = peerConnections[peerId];
        if (peerConnection) {
            peerConnection.close();
            delete peerConnections[peerId];
            delete remoteStreams[peerId];
            console.debug(`Ended call with peer: ${peerId}`);
        }
    } else {
        // Hang up with all peers
        Object.keys(peerConnections).forEach(id => {
            peerConnections[id].close();
            delete remoteStreams[id];
        });
        // Clear the peerConnections object
        Object.keys(peerConnections).forEach(key => delete peerConnections[key]);
        console.debug("Ending all calls.");
    }
}

// Handles remote MediaStream success for a specific peer
async function gotRemoteMediaStream(event, peerId) {
    console.debug(`Got remote media stream from peer: ${peerId}`);
    
    const mediaRemoteStream = new MediaStream();
    event.streams[0].getTracks().forEach(track => {
        console.log(`Adding track from peer ${peerId} to remote stream:`, track);
        mediaRemoteStream.addTrack(track);
    });
    
    // Store the remote stream
    remoteStreams[peerId] = mediaRemoteStream;
    
    // Notify the C# code about the new remote stream
    await dotNet.invokeMethodAsync("SetRemoteStream", peerId);
    console.debug(`Remote stream from peer ${peerId} ready to display`);
}

// Get a specific remote stream by peer ID
export function getRemoteStream(peerId) {
    return remoteStreams[peerId];
}

// Get all remote stream IDs
export function getRemoteStreamIds() {
    return Object.keys(remoteStreams);
}

// Sends ICE candidates to peer through signaling
async function handleConnection(event, peerId) {
    const iceCandidate = event.candidate;
    
    if (iceCandidate) {
        try {
            await dotNet.invokeMethodAsync("SendCandidate", JSON.stringify({
                peerId: peerId,
                candidate: iceCandidate
            }));
            console.debug(`Sent ICE candidate to peer ${peerId}: ${iceCandidate.candidate}`);
        } catch (error) {
            console.error(`Error sending ICE candidate to peer ${peerId}:`, error);
        }
    }
}

// Logs changes to the connection state
function handleConnectionChange(event, peerId) {
    const peerConnection = event.target;
    console.debug(`ICE state change event for peer ${peerId}:`, event);
    console.debug(`Connection state with peer ${peerId}: ${peerConnection.iceConnectionState}`);
    
    // Handle disconnection
    if (peerConnection.iceConnectionState === 'disconnected' || 
        peerConnection.iceConnectionState === 'failed' ||
        peerConnection.iceConnectionState === 'closed') {
        console.debug(`Peer ${peerId} disconnected, cleaning up resources`);
        dotNet.invokeMethodAsync("PeerDisconnected", peerId);
    }
}


