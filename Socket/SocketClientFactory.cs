using Accord.Math;
using Doodaoma.NINA.Doodaoma.Factory;
using Doodaoma.NINA.Doodaoma.Properties;
using Doodaoma.NINA.Doodaoma.Provider;
using System;
using System.Net;
using Websocket.Client;

namespace Doodaoma.NINA.Doodaoma.Socket {
    internal class SocketClientFactory : IFactory<WebsocketClient> {
        private readonly ICameraInfoProvider cameraInfoProvider;

        public SocketClientFactory(ICameraInfoProvider cameraInfoProvider) {
            this.cameraInfoProvider = cameraInfoProvider;
        }

        public WebsocketClient Create() {
            return new WebsocketClient(BuildSocketConnectionUri()) {
                ReconnectTimeout = TimeSpan.FromMinutes(3)
            };
        }

        private Uri BuildSocketConnectionUri() {
            string deviceId = WebUtility.UrlEncode(cameraInfoProvider.GetDeviceId());
            string name = WebUtility.UrlEncode(cameraInfoProvider.GetName());
            string description = WebUtility.UrlEncode(cameraInfoProvider.GetDescription());
            string driverInfo = WebUtility.UrlEncode(cameraInfoProvider.GetDriverInfo());
            string driverVersion = WebUtility.UrlEncode(cameraInfoProvider.GetDriverVersion());
            return new Uri(Settings.Default.ServerSocketUrl +
                           "/nina" +
                           $"?deviceId={deviceId}" +
                           $"&name={name}" +
                           $"&description={description}" +
                           $"&driverInfo={driverInfo}" +
                           $"&driverVersion={driverVersion}");
        }
    }
}