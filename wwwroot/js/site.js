function downloadFileFromBase64(fileName, mimeType, base64Data) {
    const link = document.createElement('a');
    link.href = `data:${mimeType};base64,${base64Data}`;
    link.download = fileName;
    link.click();
}