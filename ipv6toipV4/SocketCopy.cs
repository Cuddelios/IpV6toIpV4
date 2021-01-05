using System;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace PortForwarding
{
    public class SocketCopy : IDisposable
    {
        static volatile int count = 0;

        TcpClient tcpClientV6;
        TcpClient tcpClientV4;

        NetworkStream streamIpV4;
        NetworkStream streamIpV6;

        public SocketCopy(TcpClient v6Con, int port)
        {
            this.tcpClientV4 = new TcpClient("127.0.0.1", port);
            this.tcpClientV6 = v6Con;

            this.streamIpV4 = this.tcpClientV4.GetStream();
            this.streamIpV6 = this.tcpClientV6.GetStream();

            Console.WriteLine($"{++count}. connection to local Server.");
        }


        public void Dispose()
        {
            try
            {
                count--;
                Console.WriteLine($"Disconnect from {this.tcpClientV6.Client.RemoteEndPoint}.");

                // close everything
                streamIpV4.Close();
                streamIpV6.Close();

                this.tcpClientV4?.Close();
                this.tcpClientV6?.Close();
            }
            catch{/*ignore*/}
        }


        public async Task RunAsync(CancellationToken exitService)
        {
            try 
            {
                var exit = new CancellationTokenSource();
                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(exitService, exit.Token);

                var tasks  = new [] {
                    CopyStreamDataAsync(streamIpV6, streamIpV4, linkedToken.Token), 
                    CopyStreamDataAsync(streamIpV4, streamIpV6, linkedToken.Token)
                    };

                // Waiting until any socket has closed or timed out
                await Task.WhenAny(tasks);

                // canceling the pending read requests
                exit.Cancel();

                // Waiting all operation are finished
                await Task.WhenAll(tasks);
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine("ArgumentNullException: {0}", e);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e);
            }
        }

        static async Task CopyStreamDataAsync(NetworkStream reciever, NetworkStream sender, CancellationToken cancel, int readTimeout = 10000)
        {
            await Task.Run(async ()=> 
            {
                var memory = new Byte[1024];
                var addressFamiliy = reciever.Socket.AddressFamily;

                Console.WriteLine($"Begin copy data from {reciever.Socket.RemoteEndPoint}({reciever.Socket.ProtocolType})/{addressFamiliy} to {sender.Socket.RemoteEndPoint}({sender.Socket.ProtocolType})");

                reciever.ReadTimeout = readTimeout;

                while(reciever.Socket.Connected && sender.Socket.Connected && !cancel.IsCancellationRequested )
                {
                    try 
                    {
                        var length = await reciever.ReadAsync(memory, 0, memory.Length, cancel);
                        if(length > 0)
                        {
                            await sender.WriteAsync(memory, 0, length);
                        }
                        else 
                        {                        
                            Console.WriteLine($"Exit copy data: No more Data from {reciever.Socket.RemoteEndPoint}");
                            return;
                        }
                    }
                    catch (IOException io)
                    {
                        if(io.InnerException is SocketException socketException)
                        {
                            if(socketException.SocketErrorCode == SocketError.TimedOut)
                            {
                                Console.WriteLine($"Socket timeout");
                                continue;
                            }
                            Console.WriteLine($"Exit copy data: {reciever.Socket.RemoteEndPoint} socket error - {socketException.SocketErrorCode}");
                            return;
                        }
                        
                        Console.WriteLine($"Exit copy data: Error from {reciever.Socket.RemoteEndPoint}");
                        return;
                    }
                    catch 
                    {
                        Console.WriteLine($"Exit copy data: Error from {reciever.Socket.RemoteEndPoint}");
                        return;
                    }
                }
            });
        }
    }
}
