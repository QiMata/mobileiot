namespace QiMata.MobileIoT.Constants;

public static class AppConstants
{
    public static class Devices
    {
        public const string PiSensor = "PiSensor";
        public const string PiDHTSensor = "PiDHTSensor";
    }

    public static class Hosts
    {
        public const string RaspberryPi = "raspberrypi.local";
        public const int PiCameraHttpPort = 5000;
        public const string PiDataPath = "/home/pi/data";
    }

    public static class Gpio
    {
        public const int Dht22Pin = 4;
        public const int LedPin = 17;
    }
}
