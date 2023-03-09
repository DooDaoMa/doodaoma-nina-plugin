using Doodaoma.NINA.Doodaoma.Provider;
using Doodaoma.NINA.Doodaoma.Socket;
using Doodaoma.NINA.Doodaoma.Uploader;
using Doodaoma.NINA.Doodaoma.Uploader.Models;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Image.Interfaces;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Websocket.Client;

namespace Doodaoma.NINA.Doodaoma {
    [Export(typeof(IPluginManifest))]
    public class Doodaoma : PluginBase, INotifyPropertyChanged {
        private readonly IDeepSkyObjectSearchVM deepSkyObjectSearchVm;
        private readonly IImageDataFactory imageDataFactory;
        private readonly DefaultFileUploader fileUploader;
        private readonly WebsocketClient socketClient;
        private readonly HttpClient httpClient;
        private event EventHandler<bool> IsConnectedEvent;
        private string currentUserId;

        public event PropertyChangedEventHandler PropertyChanged;
        public IAsyncCommand ConnectToServerCommand { get; }

        private string connectButtonText = "Connect";

        public string ConnectButtonText {
            get => connectButtonText;
            set {
                connectButtonText = value;
                RaisePropertyChanged();
            }
        }

        private bool isConnected;

        public bool IsConnected {
            get => isConnected;
            set {
                isConnected = value;
                ConnectButtonText = value ? "Connected" : "Connect";
                RaisePropertyChanged();
            }
        }

        [ImportingConstructor]
        public Doodaoma(IDeepSkyObjectSearchVM deepSkyObjectSearchVm, IImagingMediator imagingMediator,
            ITelescopeMediator telescopeMediator, IImageSaveMediator imageSaveMediator,
            IProfileService profileService, IImageDataFactory imageDataFactory) {
            this.deepSkyObjectSearchVm = deepSkyObjectSearchVm;
            this.imageDataFactory = imageDataFactory;

            ICameraInfoProvider cameraInfoProvider = new FakeCameraInfoProvider();
            socketClient = new SocketClientFactory(cameraInfoProvider).Create();
            httpClient = new HttpClient();
            fileUploader = new DefaultFileUploader(httpClient);

            IsConnectedEvent += OnIsConnectedEvent;

            SocketHandler handler = new SocketHandler(this.deepSkyObjectSearchVm, telescopeMediator,
                imagingMediator, profileService);
            handler.UserIdChangeEvent += OnUserIdChangeEvent;
            handler.UserDisconnectedEvent += HandlerOnUserDisconnectedEvent;
            handler.UploadFileEvent += HandlerOnUploadFileEvent;
            socketClient.MessageReceived
                .Where(msg => msg.Text != null)
                .Where(msg => msg.Text.StartsWith("{") && msg.Text.EndsWith("}"))
                .Subscribe(msg => handler.HandleMessage(msg.Text));
            ConnectToServerCommand = new AsyncCommand<bool>(ConnectToServer);
        }

        private void OnUserIdChangeEvent(object sender, string userId) {
            currentUserId = userId;
            Notification.ShowInformation("Current user " + currentUserId);
        }

        private void HandlerOnUserDisconnectedEvent(object sender, EventArgs e) {
            currentUserId = null;
            Notification.ShowInformation("User disconnected");
        }

        private async void HandlerOnUploadFileEvent(object sender, UploadFileEventArgs e) {
            if (!isConnected) {
                Notification.ShowError("Not connected to server");
                return;
            }

            if (currentUserId == null) {
                Notification.ShowError("Null user id");
                return;
            }

            try {
                byte[] fileBytes;

                BitmapEncoder encoder = new PngBitmapEncoder();
                using (MemoryStream stream = new MemoryStream()) {
                    encoder.Frames.Add(BitmapFrame.Create(e.ImageData.RenderBitmapSource()));
                    encoder.Save(stream);
                    fileBytes = stream.ToArray();
                }

                UploadFileResponse response = await fileUploader.Upload(
                    new DefaultFileUploader.Params {
                        UserId = currentUserId, Content = fileBytes, Name = Path.GetFileName(e.Path)
                    }
                );
                Notification.ShowInformation(response.Message);
            } catch (Exception exception) {
                Notification.ShowError(exception.ToString());
            }
        }

        private async Task<bool> ConnectToServer() {
            try {
                await socketClient.StartOrFail();
                return true;
            } catch (Exception e) {
                Notification.ShowError(e.ToString());
                return false;
            } finally {
                IsConnectedEvent?.Invoke(this, socketClient.IsRunning);
            }
        }

        public override Task Teardown() {
            deepSkyObjectSearchVm.TargetSearchResult.PropertyChanged -= TargetSearchResultOnPropertyChanged;
            socketClient.Dispose();
            httpClient.Dispose();
            return base.Teardown();
        }

        private void OnIsConnectedEvent(object sender, bool e) {
            IsConnected = e;
            if (e) {
                Notification.ShowInformation("Connected");
                deepSkyObjectSearchVm.TargetSearchResult.PropertyChanged += TargetSearchResultOnPropertyChanged;
            } else {
                Notification.ShowInformation("Not connected");
            }
        }

        private void TargetSearchResultOnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            Notification.ShowInformation(e.PropertyName);
            JObject deepSkyObjectsMessage = JObject.FromObject(new {
                type = "deepSkyObjects", payload = new { results = deepSkyObjectSearchVm.TargetSearchResult.Result }
            });
            socketClient.Send(deepSkyObjectsMessage.ToString());
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}