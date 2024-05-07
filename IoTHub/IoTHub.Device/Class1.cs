using System.Net.Mime;
using System.Text;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;


namespace IoT_Agent
{
    public class Virtual_device
    {

        private readonly DeviceClient azure_client;
        private readonly OpcClient opc_client;

        public Virtual_device(DeviceClient azureClient, OpcClient opcClient)
        {
            this.azure_client = azureClient;
            this.opc_client = opcClient;
        }

        public async Task D2C_Message()
        {
            opc_client.Connect();

            OpcReadNode productionStatusNode = new OpcReadNode("ns=2;s=Device 1/ProductionStatus");
            OpcReadNode workerIdNode = new OpcReadNode("ns=2;s=Device 1/WorkorderId");
            OpcReadNode goodCountNode = new OpcReadNode("ns=2;s=Device 1/GoodCount");
            OpcReadNode badCountNode = new OpcReadNode("ns=2;s=Device 1/BadCount");

            OpcValue productionStatusRead = opc_client.ReadNode(productionStatusNode);
            OpcValue workerIdNodeRead = opc_client.ReadNode(workerIdNode);
            OpcValue goodCountRead = opc_client.ReadNode(goodCountNode);
            OpcValue badCountRead = opc_client.ReadNode(badCountNode);

            var productionStatusValue = productionStatusRead.Value;
            var workerIdNodeValue = workerIdNodeRead.Value;
            if (productionStatusValue.ToString() == "0")
            {
                workerIdNodeValue = "";
            }
            var goodCountValue = goodCountRead.Value;
            var badCountValue = badCountRead.Value;

            var data = new
            {
                production_status = productionStatusValue,
                worker_id = workerIdNodeValue,
                good_count = goodCountValue,
                bad_count = badCountValue
            };

            var dataString = JsonConvert.SerializeObject(data);
            Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
            eventMessage.ContentType = MediaTypeNames.Application.Json;
            eventMessage.ContentEncoding = "utf-8";

            Console.WriteLine("D2C message sent!");

            await azure_client.SendEventAsync(eventMessage);

            //opc_client.Disconnect();
        }

        public async Task UpdateTwinAsync()
        {
            opc_client.Connect();

            OpcReadNode productionRateNode = new OpcReadNode("ns=2;s=Device 1/ProductionRate");
            OpcValue productionRateRead = opc_client.ReadNode(productionRateNode);

            var twin = await azure_client.GetTwinAsync();
            Console.WriteLine($"\t Initial twin value recived: \n {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
            Console.WriteLine();

            var reportedProperties = new TwinCollection();
            reportedProperties["ProductionRate"] = productionRateRead.Value;
            await azure_client.UpdateReportedPropertiesAsync(reportedProperties);

            twin = await azure_client.GetTwinAsync();
            Console.WriteLine($"\t After update twin value recived: \n {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
            Console.WriteLine();

            opc_client.Disconnect();
        }

        private async Task OnDesirePropertyChange(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now;
            await azure_client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);

            int desiredProductionRate = desiredProperties["ProductionRate"];

            Console.WriteLine(desiredProductionRate.GetType());

            opc_client.WriteNode("ns=2;s=Device 1/ProductionRate", desiredProductionRate);
        }

        public async Task InitializeHandlers()
        {
            await azure_client.SetDesiredPropertyUpdateCallbackAsync(OnDesirePropertyChange, azure_client);
        }
    }
}