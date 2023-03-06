using Accord.Math;
using Doodaoma.NINA.Doodaoma.Factory;
using Doodaoma.NINA.Doodaoma.Properties;
using Doodaoma.NINA.Doodaoma.Provider;
using System;
using Websocket.Client;

namespace Doodaoma.NINA.Doodaoma.Socket {
    internal class SocketClientFactory : IFactory<WebsocketClient> {
        private readonly ICameraInfoProvider cameraInfoProvider;

        public SocketClientFactory(ICameraInfoProvider cameraInfoProvider) {
            this.cameraInfoProvider = cameraInfoProvider;
        }

        public WebsocketClient Create() {
            return new WebsocketClient(BuildSocketConnectionUri());
        }

        private Uri BuildSocketConnectionUri() {
            return new Uri(Settings.Default.ServerSocketUrl +
                           "/nina" +
                           $"?deviceId={cameraInfoProvider.GetDeviceId()}" +
                           $"&name={cameraInfoProvider.GetName()}" +
                           $"&description={cameraInfoProvider.GetDescription()}" +
                           $"&driverInfo={cameraInfoProvider.GetDriverInfo()}" +
                           $"&driverVersion={cameraInfoProvider.GetDriverVersion()}");
        }
    }
}