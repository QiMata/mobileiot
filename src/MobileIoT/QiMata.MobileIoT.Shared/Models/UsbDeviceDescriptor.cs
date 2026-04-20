namespace QiMata.MobileIoT.Models;

public sealed record UsbDeviceDescriptor(string Identifier, ushort VendorId, ushort ProductId, string? Name = null);
