namespace Doodaoma.NINA.Doodaoma.Uploader.Models {
    public struct UploadFileResponse {
        public string Message { get; }

        public UploadFileResponse(string message) {
            Message = message;
        }
    }
}