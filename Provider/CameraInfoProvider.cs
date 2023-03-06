using NINA.Equipment.Interfaces.Mediator;

namespace Doodaoma.NINA.Doodaoma.Provider {
    internal class CameraInfoProvider : ICameraInfoProvider {
        private readonly ICameraMediator cameraMediator;

        public CameraInfoProvider(ICameraMediator cameraMediator) {
            this.cameraMediator = cameraMediator;
        }

        public string GetDeviceId() {
            return cameraMediator.GetInfo().DeviceId;
        }

        public string GetName() {
            return cameraMediator.GetInfo().Name;
        }

        public string GetDescription() {
            return cameraMediator.GetInfo().Description;
        }

        public string GetDriverInfo() {
            return cameraMediator.GetInfo().DriverInfo;
        }

        public string GetDriverVersion() {
            return cameraMediator.GetInfo().DriverVersion;
        }
    }
}