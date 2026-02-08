namespace QiMata.MobileIoT.Models;

public record TelemetryData(
    double Temp,
    double Hum,
    double CpuTemp,
    double Uptime,
    double Timestamp);

public record RemoteFileInfo(string Name, long Size);
