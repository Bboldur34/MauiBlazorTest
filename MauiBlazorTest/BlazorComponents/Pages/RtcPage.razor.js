const localVideo = document.getElementById('localVideo');
export function setLocalStream(stream) {
    localVideo.srcObject = stream;
}

export function setRemoteStream(stream, elementId) {
    const videoElement = document.getElementById(elementId);
    if (videoElement) {
        videoElement.srcObject = stream;
    } else {
        console.error(`Video element with ID ${elementId} not found.`);
    }
}