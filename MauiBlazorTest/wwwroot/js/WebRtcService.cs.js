"use strict";
// Set up media stream constant and parameters.
const mediaStreamConstraints = {
    video: true,
   // audio: true
};

// Set up to exchange only video.
const offerOptions = {
    offerToReceiveVideo: 1,
   // offerToReceiveAudio: 1
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
let remoteStream;
let peerConnection;

let isOffering;
let isOffered;

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

function createPeerConnection() {
    if (peerConnection != null) return;
    // Create peer connections and add behavior.
    peerConnection = new RTCPeerConnection(servers);
    console.debug("Created local peer connection object peerConnection.");

    peerConnection.addEventListener("icecandidate", handleConnection);
    peerConnection.addEventListener("iceconnectionstatechange", handleConnectionChange);
    peerConnection.addEventListener("track", gotRemoteMediaStream);

    // Add local stream to connection and create offer to connect.
    localStream.getTracks().forEach(track => {
        peerConnection.addTrack(track, localStream);
    });
    console.debug("Added local stream to peerConnection.");
}

// first flow: This client initiates call. Sequence is:
// Create offer - send to peer - receive answer - set stream
// Handles call button action: creates peer connection.
export async function callAction() {
    if (isOffered) return Promise.resolve();

    isOffering = true;
    console.log("Starting call.");
    createPeerConnection();
    console.debug("Starting call.");
    console.debug("peerConnection createOffer start.");
    let offerDescription = await peerConnection.createOffer(offerOptions);
    console.debug(`Offer from peerConnection:\n${offerDescription.sdp}`);
    console.debug("peerConnection setLocalDescription start.");
    await peerConnection.setLocalDescription(offerDescription);
    console.debug("peerConnection.setLocalDescription(offerDescription) success");
    return JSON.stringify(offerDescription);
}

// Signaling calls this once an answer has arrived from other peer. Once
// setRemoteDescription is called, the addStream event trigger on the connection.
export async function processAnswer(descriptionText) {
    let description = JSON.parse(descriptionText);
    console.debug("processAnswer: peerConnection setRemoteDescription start.");
    await peerConnection.setRemoteDescription(description);
    console.debug("peerConnection.setRemoteDescription(description) success");
}

// In this flow, the user gets an offer from signaling from a peer.
// In this case, we setRemoteDescription similar to when we got the answer
// in the flow above. srd triggers addStream.
export async function processOffer(descriptionText) {
    console.log("processOffer");
    if (isOffering) return;

    createPeerConnection();
    let description = JSON.parse(descriptionText);
    console.debug("peerConnection setRemoteDescription start.");
    await peerConnection.setRemoteDescription(description);

    console.debug("peerConnection createAnswer start.");
    let answer = await peerConnection.createAnswer();
    console.debug(`Answer: ${answer.sdp}.`);
    console.debug("peerConnection setLocalDescription start.");
    await peerConnection.setLocalDescription(answer);

    console.debug("dotNet SendAnswer.");
    await dotNet.invokeMethodAsync("SendAnswer", JSON.stringify(answer));
}

export async function processCandidate(candidateText) {
    let candidate = JSON.parse(candidateText);
    console.debug("processCandidate: peerConnection addIceCandidate start.");
    await peerConnection.addIceCandidate(candidateText);
    console.debug("addIceCandidate added.");
}

// Handles hangup action: ends up call, closes connections and resets peers.
export function hangupAction() {
    peerConnection.close();
    peerConnection = null;
    console.debug("Ending call.");
}

// Handles remote MediaStream success by handing the stream to the blazor component.
async function gotRemoteMediaStream(event) {
    const [mediaRemoteStream] = event.streams;
    console.debug(mediaRemoteStream);
    remoteStream = mediaRemoteStream;
    await dotNet.invokeMethodAsync("SetRemoteStream");
    console.debug("Remote peer connection received remote stream.");
}
export function getRemoteStream() {
    return remoteStream;
}

// Sends candidates to peer through signaling.
async function handleConnection(event) {
    const iceCandidate = event.candidate;

    if (iceCandidate) {
        await dotNet.invokeMethodAsync("SendCandidate", JSON.stringify(iceCandidate));

        console.debug(`peerConnection ICE candidate:${event.candidate.candidate}.`);
    }
}

// Logs changes to the connection state.
function handleConnectionChange(event) {
    const peerConnection = event.target;
    console.debug("ICE state change event: ", event);
    console.debug(`peerConnection ICE state: ${peerConnection.iceConnectionState}.`);
}


