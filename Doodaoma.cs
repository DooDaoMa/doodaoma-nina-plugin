using Doodaoma.NINA.Doodaoma.Manager;
using Doodaoma.NINA.Doodaoma.Provider;
using Doodaoma.NINA.Doodaoma.Socket;
using Doodaoma.NINA.Doodaoma.Uploader;
using Doodaoma.NINA.Doodaoma.Uploader.Models;
using Newtonsoft.Json.Linq;
using NINA.Astrometry.Interfaces;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.PlateSolving.Interfaces;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Utility.DateTimeProvider;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Websocket.Client;

namespace Doodaoma.NINA.Doodaoma {
    [Export(typeof(IPluginManifest))]
    public class Doodaoma : PluginBase, INotifyPropertyChanged {
        private readonly IImageSaveMediator imageSaveMediator;
        private readonly IProfileService profileService;
        private readonly DefaultFileUploader fileUploader;
        private readonly SocketHandler handler;
        private readonly WebsocketClient socketClient;
        private readonly HttpClient httpClient;
        private event EventHandler<bool> IsConnectedEvent;
        public event PropertyChangedEventHandler PropertyChanged;
        public IAsyncCommand ConnectToServerCommand { get; }

        private string currentUserId;

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
        public Doodaoma(IList<IDateTimeProvider> dateTimeProviders, ITelescopeMediator telescopeMediator,
            ICameraMediator cameraMediator, IProfileService profileService, IFilterWheelMediator filterWheelMediator,
            IGuiderMediator guiderMediator, IImageHistoryVM imageHistoryVm, IFocuserMediator focuserMediator,
            IAutoFocusVMFactory autoFocusVmFactory, IRotatorMediator rotatorMediator, IImagingMediator imagingMediator,
            IPlateSolverFactory plateSolverFactory, IWindowServiceFactory windowServiceFactory,
            INighttimeCalculator nighttimeCalculator, IFramingAssistantVM framingAssistantVm,
            IApplicationMediator applicationMediator, IPlanetariumFactory planetariumFactory,
            IImageSaveMediator imageSaveMediator, IMeridianFlipVMFactory meridianFlipVmFactory,
            IApplicationStatusMediator applicationStatusMediator, IDomeMediator domeMediator,
            IDomeFollower domeFollower) {
            this.imageSaveMediator = imageSaveMediator;
            this.imageSaveMediator.ImageSaved += ImageSaveMediatorOnImageSaved;
            this.profileService = profileService;
            this.profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.CollectionChanged +=
                HandlerOnGetFilterWheelOptionsEvent;

            ICameraInfoProvider cameraInfoProvider = new FakeCameraInfoProvider();
            socketClient = new SocketClientFactory(cameraInfoProvider).Create();
            httpClient = new HttpClient();
            fileUploader = new DefaultFileUploader(httpClient);
            SequenceManager sequenceManager = new SequenceManager(dateTimeProviders, telescopeMediator, cameraMediator,
                profileService, filterWheelMediator, guiderMediator, imageHistoryVm, focuserMediator,
                autoFocusVmFactory, rotatorMediator, imagingMediator, plateSolverFactory, windowServiceFactory,
                nighttimeCalculator, framingAssistantVm, applicationMediator, planetariumFactory,
                meridianFlipVmFactory, applicationStatusMediator, domeMediator, domeFollower, imageSaveMediator);

            IsConnectedEvent += OnIsConnectedEvent;

            handler = new SocketHandler(sequenceManager);
            handler.UserIdChangeEvent += HandlerOnUserIdChangeEvent;
            handler.UserDisconnectedEvent += HandlerOnUserDisconnectedEvent;
            handler.CapturingEvent += HandlerOnCapturingEvent;
            handler.GetFilterWheelOptionsEvent += HandlerOnGetFilterWheelOptionsEvent;
            socketClient.MessageReceived
                .Where(msg => msg.Text != null)
                .Where(msg => msg.Text.StartsWith("{") && msg.Text.EndsWith("}"))
                .Subscribe(msg => handler.HandleMessage(msg.Text));
            ConnectToServerCommand = new AsyncCommand<bool>(ConnectToServer);
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
            imageSaveMediator.ImageSaved -= ImageSaveMediatorOnImageSaved;
            profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.CollectionChanged -=
                HandlerOnGetFilterWheelOptionsEvent;
            handler.UserIdChangeEvent -= HandlerOnUserIdChangeEvent;
            handler.UserDisconnectedEvent -= HandlerOnUserDisconnectedEvent;
            handler.CapturingEvent -= HandlerOnCapturingEvent;
            handler.ClearCaptureCancelTokenSource();
            socketClient.Dispose();
            httpClient.Dispose();
            return base.Teardown();
        }

        private void OnIsConnectedEvent(object sender, bool e) {
            IsConnected = e;
            Notification.ShowInformation(e ? "Connected" : "Not connected");
        }

        private async void ImageSaveMediatorOnImageSaved(object sender, ImageSavedEventArgs e) {
            if (!isConnected) {
                return;
            }

            if (currentUserId == null) {
                return;
            }

            try {
                byte[] fileBytes;

                BitmapEncoder encoder = new PngBitmapEncoder();
                using (MemoryStream stream = new MemoryStream()) {
                    encoder.Frames.Add(BitmapFrame.Create(e.Image));
                    encoder.Save(stream);
                    fileBytes = stream.ToArray();
                }

                UploadFileResponse response = await fileUploader.Upload(
                    new DefaultFileUploader.Params {
                        UserId = currentUserId, Content = fileBytes, Name = Path.GetFileName(e.PathToImage.AbsolutePath)
                    }
                );
                Notification.ShowInformation(response.Message);
            } catch (Exception exception) {
                Notification.ShowError(exception.ToString());
            }
        }

        private void HandlerOnGetFilterWheelOptionsEvent(object sender, EventArgs e) {
            if (e is NotifyCollectionChangedEventArgs args) {
                JObject filterWheels = JObject.FromObject(new {
                    type = "updateFilterWheelOptions",
                    payload = new { options = args.NewItems }
                });
                socketClient.Send(filterWheels.ToString());
            } else {
                JObject filterWheels = JObject.FromObject(new {
                    type = "updateFilterWheelOptions",
                    payload = new {
                        options = profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.ToList()
                    }
                });
                socketClient.Send(filterWheels.ToString());
            }
        }

        private void HandlerOnCapturingEvent(object sender, EventArgs e) {
            JObject message = JObject.FromObject(new {
                type = "sendMessage", payload = new { message = "Camera is busy" }
            });
            socketClient.Send(message.ToString());
        }

        private void HandlerOnUserIdChangeEvent(object sender, string userId) {
            currentUserId = userId;
            Notification.ShowInformation("Current user " + currentUserId);
        }

        private void HandlerOnUserDisconnectedEvent(object sender, EventArgs e) {
            currentUserId = null;
            Notification.ShowInformation("User disconnected");
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}