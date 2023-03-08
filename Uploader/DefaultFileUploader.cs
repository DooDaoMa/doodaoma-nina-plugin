using Doodaoma.NINA.Doodaoma.Properties;
using Doodaoma.NINA.Doodaoma.Uploader.Models;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Doodaoma.NINA.Doodaoma.Uploader {
    public class DefaultFileUploader : IFileUploader<DefaultFileUploader.Params, UploadFileResponse> {
        private const string KeyUserId = "userId";
        private const string KeyImageFile = "imageFile";
        private readonly HttpClient httpClient;

        public DefaultFileUploader(HttpClient httpClient) {
            this.httpClient = httpClient;
        }

        public async Task<UploadFileResponse> Upload(Params fileParams) {
            MultipartFormDataContent form = new MultipartFormDataContent {
                { new StringContent(fileParams.UserId), KeyUserId }, {
                    new ByteArrayContent(
                        fileParams.Content,
                        0,
                        fileParams.Content.Length
                    ),
                    KeyImageFile, fileParams.Name
                }
            };
            HttpResponseMessage response =
                await httpClient.PostAsync(Settings.Default.ServerUrl + "/api/images", form);
            string responseMessage = await response.Content.ReadAsStringAsync();
            UploadFileResponse uploadFileResponse = JsonConvert.DeserializeObject<UploadFileResponse>(responseMessage);

            if (!response.IsSuccessStatusCode) {
                throw new IOException(uploadFileResponse.Message ?? "Unknown error message");
            }

            return uploadFileResponse;
        }

        public struct Params {
            public string UserId { get; }
            public byte[] Content { get; }
            public string Name { get; }

            public Params(string userId, byte[] content, string name) {
                UserId = userId;
                Content = content;
                Name = name;
            }
        }
    }
}