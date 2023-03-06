namespace Doodaoma.NINA.Doodaoma.Provider {
    internal interface ICameraInfoProvider {
        string GetDeviceId();
        string GetName();
        string GetDescription();
        string GetDriverInfo();
        string GetDriverVersion();
    }
}