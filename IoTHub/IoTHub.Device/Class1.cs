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
        private readonly string device_name;

        public Virtual_device(DeviceClient azureClient, OpcClient opcClient, string device_name)
        {
            this.azure_client = azureClient;
            this.opc_client = opcClient;
            this.device_name = device_name;
        }

        #region D2C MESSAGE

        public async Task D2C_Message()
        {
            opc_client.Connect();

            OpcReadNode productionStatusNode = new OpcReadNode($"ns=2;s={device_name}/ProductionStatus");
            OpcReadNode workerIdNode = new OpcReadNode($"ns=2;s={device_name}/WorkorderId");
            OpcReadNode goodCountNode = new OpcReadNode($"ns=2;s={device_name}/GoodCount");
            OpcReadNode badCountNode = new OpcReadNode($"ns=2;s={device_name}/BadCount");
            OpcReadNode temperatureNode = new OpcReadNode($"ns=2;s={device_name}/Temperature");

            OpcValue productionStatusRead = opc_client.ReadNode(productionStatusNode);
            OpcValue workerIdNodeRead = opc_client.ReadNode(workerIdNode);
            OpcValue goodCountRead = opc_client.ReadNode(goodCountNode);
            OpcValue badCountRead = opc_client.ReadNode(badCountNode);
            OpcValue temperatureRead = opc_client.ReadNode(temperatureNode);

            var productionStatusValue = productionStatusRead.Value;
            var workerIdNodeValue = workerIdNodeRead.Value;
            if (productionStatusValue.ToString() == "0")
            {
                workerIdNodeValue = "";
            }
            var goodCountValue = goodCountRead.Value;
            var badCountValue = badCountRead.Value;
            var temperatureValue = temperatureRead.Value;

            var data = new
            {
                device = device_name,
                temperature = temperatureValue,
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

            //await UpdateTwinAsync();

            //opc_client.Disconnect();
        }

        #endregion

        #region DEVICE TWIN
        public async Task UpdateTwinAsync()
        {
            opc_client.Connect();

            OpcReadNode productionRateNode = new OpcReadNode($"ns=2;s={device_name}/ProductionRate");
            OpcValue productionRateRead = opc_client.ReadNode(productionRateNode);

            OpcReadNode deviceErrorsNode = new OpcReadNode($"ns=2;s={device_name}/DeviceError");
            int deviceErrorsRead = opc_client.ReadNode(deviceErrorsNode).As<int>();

            var errorString = "";
            if(deviceErrorsRead == 0){
                errorString = "None";
            }
            else{
                if(deviceErrorsRead - 8 >=0){
                    errorString += "[Unknown Error] ";
                    deviceErrorsRead -= 8;
                }
                if(deviceErrorsRead - 4 >= 0){
                    errorString += "[Sensor Failure] ";
                    deviceErrorsRead -= 4;
                }
                if(deviceErrorsRead - 2 >= 0){
                    errorString += "[Power Failure] ";
                    deviceErrorsRead -= 2;
                }
                if(deviceErrorsRead - 1 >= 0){
                    errorString += "[Emergency Stop] ";
                    deviceErrorsRead -= 1;
                }
            }

            var twin = await azure_client.GetTwinAsync();

            var reportedProperties = new TwinCollection();
            reportedProperties["ProductionRate"] = productionRateRead.Value;


            if (!twin.Properties.Reported.Contains("deviceErrors") || twin.Properties.Reported["deviceErrors"] != errorString){
                reportedProperties["deviceErrors"] = errorString;

                var data = new
                {
                    device_errors = errorString
                };

                var dataString = JsonConvert.SerializeObject(data);
                Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
                eventMessage.ContentType = MediaTypeNames.Application.Json;
                eventMessage.ContentEncoding = "utf-8";

                Console.WriteLine(errorString);

                await azure_client.SendEventAsync(eventMessage);
            }

            await azure_client.UpdateReportedPropertiesAsync(reportedProperties);


            //opc_client.Disconnect();
        }

        private async Task OnDesirePropertyChange(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now;
            await azure_client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);

            int desiredProductionRate = desiredProperties["ProductionRate"];

            opc_client.WriteNode($"ns=2;s={device_name}/ProductionRate", desiredProductionRate); // nie wiem czy nie bedzie trzeba tez ustawić reported, ale to troche bez sensu, bo juz odczytujemy to UpdateTwin()
        }

        #endregion

        #region DIRECT METHODS

        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\t DEFAULT METHOD EXECUTED: {methodRequest.Name}");
            await Task.Delay(1000);
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> EmergencyStopHandler(MethodRequest methodRequest, object userContext)
        {
            opc_client.Connect();

            opc_client.CallMethod($"ns=2;s={device_name}", $"ns=2;s={device_name}/EmergencyStop");

            Console.WriteLine("EMERGENCY STOP!");

            opc_client.Disconnect();

            await Task.Delay(1000);
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> ResetErrorStatus(MethodRequest methodRequest, object userContext)
        {
            opc_client.Connect();

            opc_client.CallMethod($"ns=2;s={device_name}", $"ns=2;s={device_name}/ResetErrorStatus");

            Console.WriteLine("RESSETING ERRORS");

            opc_client.Disconnect();

            await Task.Delay(1000);
            return new MethodResponse(0);
        }

        #endregion

        public async Task InitializeHandlers()
        {
            await azure_client.SetDesiredPropertyUpdateCallbackAsync(OnDesirePropertyChange, azure_client);

            await azure_client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, azure_client);

            await azure_client.SetMethodHandlerAsync("EmergencyStop", EmergencyStopHandler, azure_client);
            await azure_client.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatus, azure_client);
        }

        public async Task testElo()
        {
            var twin = await azure_client.GetTwinAsync();

            var reportedProperties = new TwinCollection();
            reportedProperties["deviceErrors"] = null;
            await azure_client.UpdateReportedPropertiesAsync(reportedProperties);
            Console.WriteLine("zrobioned");
        }
    }
}