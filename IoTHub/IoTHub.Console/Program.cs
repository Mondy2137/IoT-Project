using System.Net.Sockets;
using IoT_Agent;
using Microsoft.Azure.Devices.Client;
using Opc.Ua;
using Opc.UaFx;
using Opc.UaFx.Client;

#region Azure connection string validation
DeviceClient deviceClientAzure = null;
while (true)
{
    Console.WriteLine("\nConnection string to Azure IoT:");
    var azureCString = Console.ReadLine();
   
    try
    {
        string deviceConnectionStringAzure = azureCString; //"HostName=UL-zajecia-IoT.azure-devices.net;DeviceId=test_device;SharedAccessKey=/yDZTwWCjiKGayFrVZA95jIDZOzSSISY3AIoTI7xejA=";
        deviceClientAzure = DeviceClient.CreateFromConnectionString(deviceConnectionStringAzure, TransportType.Mqtt);
        break;
    }
    catch
    {
        Console.WriteLine("Connection failed. Please check connection string to Azure IoT or resolve other problems");
    }
}
#endregion

#region OPC UA connection string validation
OpcClient deviceClientOpcUa = null;
while (true)
{
    Console.WriteLine("\nConnection string to OPC UA:");
    var opcCString = Console.ReadLine();

    try
    {
        deviceClientOpcUa = new OpcClient(opcCString);
        deviceClientOpcUa.Connect();
        break;
    }
    catch
    {
        Console.WriteLine("Connection failed. Please check connection string to OPC UA server or resolve other problems");
    }
}
#endregion

#region Device name validation
var opcDeviceName = "";
while (true)
{
    Console.WriteLine("\nDevice name in OPC UA:");
    opcDeviceName = Console.ReadLine();

    OpcReadNode deviceNameTest = new OpcReadNode($"ns=2;s={opcDeviceName}/ProductionStatus");
    OpcValue testRead = deviceClientOpcUa.ReadNode(deviceNameTest);
    if (testRead.ToString() == "null")
    {
        Console.WriteLine("There is no such device, please provide correct device name");
    }
    else
    {
        break;
    }
}
#endregion


await deviceClientAzure.OpenAsync();
var device = new Virtual_device(deviceClientAzure, deviceClientOpcUa, opcDeviceName);
Console.WriteLine("\nConnection successfull!\n");

await device.InitializeHandlers();
while (true)
{
    await device.UpdateTwinAsync();
    await device.D2C_Message();
    System.Threading.Thread.Sleep(10000);
}