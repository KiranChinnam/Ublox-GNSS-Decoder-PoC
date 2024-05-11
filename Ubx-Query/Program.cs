// See https://aka.ms/new-console-template for more information

using System.IO.Ports;

namespace Ubx_Query;

internal class Program
{
   
    static void Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        var decoder = new UbloxDecoder();

        Console.CancelKeyPress += delegate(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var serialPort = new SerialPort();

        serialPort.PortName = "COM4";
        serialPort.BaudRate = 115200;
        serialPort.DataBits = 8;
        serialPort.Parity = Parity.None;
        serialPort.StopBits = StopBits.One;
        serialPort.Handshake = Handshake.None;
        serialPort.Open();


        while (!cts.IsCancellationRequested)
        {
            //Is there data ready to RX?
            if (serialPort.BytesToRead > 0)
            {
                var buffer = new byte[serialPort.BytesToRead];
                serialPort.Read(buffer, 0, buffer.Length);
                decoder.IngestBytes(buffer);
                Console.WriteLine($"Rlen: {buffer.Length}");
                
            }

            while (decoder.MessagesAvailable())
            {
                var message = decoder.GetOneMessage();
                if (message is { MessageClass: 1, MessageId: 7 })
                {
                    var pvtSolution = decoder.DecodeNavPvt(message.Payload);
                    if (pvtSolution is not null)
                    {
                        var v = pvtSolution.Value;
                        Console.WriteLine($"{v.UtcTime} - {v.Latitude:F5} - {v.Longitude:F5} - {Math.Max(v.HorizontalAccuracyMm,v.VerticalAccuracyMm)/1000} - {v.FixType}");
                    }
                }
            }

            Console.WriteLine("Sleeping for 400 ms");
            Thread.Sleep(400);
        }

        serialPort.Close();
    }
}