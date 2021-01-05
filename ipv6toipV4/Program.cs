using System;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace PortForwarding
{
    class Program
    {

        static void Main(string[] args)
        {
            var closeRequested = new CancellationTokenSource();
            TcpListener server=null;
            var port = 25565;

            if (args.Length == 2 && args[0] == "port" && int.TryParse(args[1], out var portValue))
            {
                port = portValue;
            }

            AppDomain.CurrentDomain.ProcessExit += (e, args) => closeRequested.Cancel();

            try
            {
                // Set the TcpListener on port 13000.
                server = new TcpListener(IPAddress.IPv6Any, port);

                // Start listening for client requests.
                server.Start();

                // Enter the listening loop.
                while(true)
                {
                    Console.WriteLine("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine($"Connected from {client.Client.RemoteEndPoint}");

                    RunSocketCopy(client, port, closeRequested.Token);
                }
            }
            catch(SocketException e)
            {
               Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }
        }

        static async void RunSocketCopy(TcpClient v6Con, int port, CancellationToken exitService)
        {
            using(var socketCopy = new SocketCopy(v6Con, port))
            {
                await socketCopy.RunAsync(exitService);
            }
        }
    }
}
