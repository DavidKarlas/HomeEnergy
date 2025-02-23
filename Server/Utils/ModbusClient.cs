using NModbus;
using NModbus.Message;
using NuGet.Configuration;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace HomeEnergy.Utils
{
    public class ModbusClient
    {
        private readonly ConcurrentQueue<ModbusRequest> modbusMessages = new();
        private readonly string ipAddress;
        private readonly int port;

        public ModbusClient(string ipAddress, int port)
        {
            this.ipAddress = ipAddress;
            this.port = port;
            Task.Run(LoopAsync);
        }

        public bool Connected { get; private set; }

        private async void LoopAsync()
        {
            while (true)
            {
                try
                {
                    using var client = new TcpClient(ipAddress, port);
                    var factory = new ModbusFactory();
                    using var master = factory.CreateMaster(client);
                    Connected = true;
                    while (true)
                    {
                        if (modbusMessages.TryDequeue(out var request))
                        {
                            try
                            {
                                if (request.Data != null)
                                {
                                    if (request.Data.Length != request.NumberOfRegisters)
                                    {
                                        request.TaskCompletionSource.SetException(new Exception("Data length does not match number of registers"));
                                        continue;
                                    }
                                    await master.WriteMultipleRegistersAsync(request.SlaveId, request.RegisterOffset, request.Data);
                                    request.TaskCompletionSource.SetResult(Array.Empty<ushort>());
                                }
                                else
                                {
                                    if (request.NumberOfRegisters < 1 || request.NumberOfRegisters > 30)
                                    {
                                        request.TaskCompletionSource.SetException(new Exception("Number of register must be at least 1 but not more than 30."));
                                        continue;
                                    }
                                    var response = await master.ReadHoldingRegistersAsync(request.SlaveId, request.RegisterOffset, request.NumberOfRegisters);
                                    request.TaskCompletionSource.SetResult(response);
                                }
                            }
                            catch (Exception e)
                            {
                                request.TaskCompletionSource.SetException(e);
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Connected = false;
                }
                await Task.Delay(1000);
            }
        }

        public Task<ushort[]> ReadRegisters(byte slaveId, ushort registerOffset, ushort numberOfRegisters = 1)
        {
            var queueItem = new ModbusRequest() {
                SlaveId = slaveId,
                RegisterOffset = registerOffset,
                NumberOfRegisters = numberOfRegisters
            };
            modbusMessages.Enqueue(queueItem);
            return queueItem.TaskCompletionSource.Task;
        }

        public Task WriteRegisters(byte slaveId, ushort registerOffset, ushort[] data)
        {
            var queueItem = new ModbusRequest() {
                SlaveId = slaveId,
                RegisterOffset = registerOffset,
                NumberOfRegisters = (ushort)data.Length,
                Data = data
            };
            modbusMessages.Enqueue(queueItem);
            return queueItem.TaskCompletionSource.Task;
        }

        private class ModbusRequest
        {
            public byte SlaveId { get; set; }
            public ushort RegisterOffset { get; set; }
            public ushort NumberOfRegisters { get; set; }
            public ushort[]? Data { get; set; }
            public TaskCompletionSource<ushort[]> TaskCompletionSource { get; } = new();
        }
    }
}
