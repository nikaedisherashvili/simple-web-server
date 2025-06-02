using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWebServer
{
    internal class Program
    {
        private const int PORT = 8080;
        private static readonly string WEB_ROOT = Path.Combine(AppContext.BaseDirectory, "webroot");
        private static readonly HashSet<string> ALLOWED = new() { ".html", ".css", ".js" };
        private static readonly Dictionary<string, string> MIME = new()
        {
            [".html"] = "text/html",
            [".css"] = "text/css",
            [".js"] = "application/javascript"
        };

        static async Task Main()
        {
            Console.WriteLine($"Serving \"{WEB_ROOT}\" on http://localhost:{PORT}");
            TcpListener listener = new(IPAddress.Any, PORT);
            listener.Start();

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            using var sock = client;
            using var net = sock.GetStream();
            using var rdr = new StreamReader(net);
            using var wrt = new StreamWriter(net) { NewLine = "\r\n", AutoFlush = true };

            try
            {
                string? requestLine = await rdr.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 3) return;

                string method = parts[0];
                string url = parts[1];

                if (method != "GET")
                {
                    await SendError(wrt, 405, "Method Not Allowed");
                    return;
                }

                string rel = url.Split('?', 2)[0].TrimStart('/');
                string full = Path.GetFullPath(Path.Combine(WEB_ROOT, rel == "" ? "index.html" : rel));

                if (!full.StartsWith(WEB_ROOT) || !ALLOWED.Contains(Path.GetExtension(full)))
                {
                    await SendError(wrt, 403, "Forbidden");
                    return;
                }

                if (!File.Exists(full))
                {
                    await SendError(wrt, 404, "Not Found");
                    return;
                }

                byte[] body = await File.ReadAllBytesAsync(full);
                string mime = MIME.GetValueOrDefault(Path.GetExtension(full), "application/octet-stream");

                await wrt.WriteAsync($"HTTP/1.1 200 OK\r\nContent-Type: {mime}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await net.WriteAsync(body);
            }
            catch (Exception) { }
        }

        private static async Task SendError(StreamWriter wrt, int code, string text)
        {
            string html = $"<html><body><h1>Error {code}: {text}</h1></body></html>";
            byte[] body = Encoding.UTF8.GetBytes(html);
            await wrt.WriteAsync($"HTTP/1.1 {code} {text}\r\nContent-Type: text/html\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await wrt.BaseStream.WriteAsync(body);
        }
    }
}
