using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XChrome.cs.tools.socks5;

namespace HttpToSocks5Proxy
{
    internal class HttpProxyListener
    {
        private readonly IPEndPoint _endPoint;
        private readonly int _backlog;

        private string? _authorization;
        private string? _username = "";

        public HttpProxyListener(IPEndPoint endPoint, int backlog)
        { 
            _endPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            _backlog = backlog;
        }



        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            if (_endPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                socket.DualMode = true;
            }
            socket.Bind(_endPoint);
            socket.Listen(_backlog);
            using (cancellationToken.UnsafeRegister(s => { ((IDisposable)s!).Dispose(); }, socket))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (Socks5Server._isStop) break;
                    Socket incoming = await socket.AcceptAsync();
                    _ = Task.Run(() => ProcessSocketAsync(incoming, cancellationToken));
                }

            }
        }

        private async Task ProcessSocketAsync(Socket socket, CancellationToken cancellationToken)
        {
            using (var ns = new NetworkStream(socket, ownsSocket: true))
            {
                var processor = new HttpProxyProcessor(ns);
                if (!(_authorization is null))
                {
                    processor.SetCredential(_authorization);
                }
                try
                {
                    await processor.RunAsync(cancellationToken);
                }
                catch (Exception)
                {
                    // Ignore
                }

            }
        }
    }
}
