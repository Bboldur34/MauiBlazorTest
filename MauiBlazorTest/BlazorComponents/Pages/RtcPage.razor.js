const localVideo = document.getElementById('localVideo');
// Store multiple remote streams and video elements
const remoteVideos = {};

export function setLocalStream(stream) {
    localVideo.srcObject = stream;
}

export function setRemoteStream(stream, elementId) {
    // Check if video element exists, if not create it
    let videoElement = document.getElementById(elementId);
    if (!videoElement) {
        // Create a container for remote videos if it doesn't exist
        let remoteVideoContainer = document.getElementById('remoteVideoContainer');
        if (!remoteVideoContainer) {
            remoteVideoContainer = document.createElement('div');
            remoteVideoContainer.id = 'remoteVideoContainer';
            document.body.appendChild(remoteVideoContainer);
        }
        
        // Create new video element for this remote peer
        videoElement = document.createElement('video');
        videoElement.id = elementId;
        videoElement.autoplay = true;
        videoElement.playsInline = true;
        videoElement.className = 'remote-video';
        remoteVideoContainer.appendChild(videoElement);
        
        // Store reference to the video element
        remoteVideos[elementId] = videoElement;
    }
    
    videoElement.srcObject = stream;
}

// Function to remove a remote stream when a peer disconnects
export function removeRemoteStream(elementId) {
    const videoElement = document.getElementById(elementId);
    if (videoElement) {
        if (videoElement.srcObject) {
            videoElement.srcObject.getTracks().forEach(track => track.stop());
        }
        videoElement.srcObject = null;
        videoElement.parentNode.removeChild(videoElement);
        delete remoteVideos[elementId];
    }
}

// Helper function to get all remote video elements
export function getRemoteVideoIds() {
    return Object.keys(remoteVideos);
}