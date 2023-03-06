namespace Doodaoma.NINA.Doodaoma.Provider {
    internal class FakeCameraInfoProvider : ICameraInfoProvider {
        public string GetDescription() {
            return "TestDescription";
        }

        public string GetDeviceId() {
            return "TestDeviceId";
        }

        public string GetDriverInfo() {
            return "TestDriverInfo";
        }

        public string GetDriverVersion() {
            return "TestDriverVersion";
        }

        public string GetName() {
            return "TestName";
        }
    }
}