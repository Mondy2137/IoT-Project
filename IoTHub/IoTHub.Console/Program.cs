using System.Net.Sockets;
using IoT_Agent;
using Microsoft.Azure.Devices.Client;
using Opc.UaFx.Client;

Console.WriteLine("Connection string to Azure IoT");
var testCString = Console.ReadLine();

Console.WriteLine("Device name in OPC UA:");
var testDeviceName = Console.ReadLine();



//testCString;

string deviceConnectionStringAzure = "HostName=UL-zajecia-IoT.azure-devices.net;DeviceId=test_device;SharedAccessKey=/yDZTwWCjiKGayFrVZA95jIDZOzSSISY3AIoTI7xejA=";
using var deviceClientAzure = DeviceClient.CreateFromConnectionString(deviceConnectionStringAzure, TransportType.Mqtt);

string deviceConnectionStringOpc = "opc.tcp://localhost:4840/";
using var deviceClientOpcUa = new OpcClient(deviceConnectionStringOpc);
Console.WriteLine("Connection to OPC UA Client successfull!");

await deviceClientAzure.OpenAsync();
var device = new Virtual_device(deviceClientAzure, deviceClientOpcUa, testDeviceName);
Console.WriteLine("Connection to Azure successfull!");

await device.InitializeHandlers();

//await device.D2C_Message();

//await device.UpdateTwinAsync();

//System.Threading.Thread.Sleep(5000);



//await device.D2C_Message();

while (true)
{
    await device.D2C_Message();
    await device.UpdateTwinAsync();
    System.Threading.Thread.Sleep(60000);
}



//await device.testElo();


Console.ReadLine();