using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Newtonsoft.Json;

namespace Rubban.Core
{
    public sealed class VizTcpClient : IDisposable
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private CancellationTokenSource _cts;

        public string Host { get; private set; } = "127.0.0.1";
        public int Port { get; private set; } = 6800;
        public string Terminator { get; private set; } = "\0";
        public event Action<bool, string> ConnectionStateChanged;

        public void Start(string host, int port, string terminator = "\0")
        {
            Stop();
            Host = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : host;
            Port = port;
            Terminator = string.IsNullOrEmpty(terminator) ? "\0" : terminator;
            _cts = new CancellationTokenSource();
            Task.Run(() => LoopAsync(_cts.Token));
        }

        public void EnqueueCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return;
            _queue.Enqueue(cmd);
            _signal.Release();
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _client = new TcpClient { NoDelay = true };
                    var connect = _client.ConnectAsync(Host, Port);
                    if (await Task.WhenAny(connect, Task.Delay(3000, ct)) != connect)
                        throw new TimeoutException($"Timeout {Host}:{Port}");
                    await connect;

                    _stream = _client.GetStream();
                    ConnectionStateChanged?.Invoke(true, $"{Host}:{Port}");
                    await Task.WhenAny(ReadAsync(_stream, ct), WriteAsync(_stream, ct));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { ConnectionStateChanged?.Invoke(false, ex.Message); }
                finally { Cleanup(); }

                if (!ct.IsCancellationRequested)
                    try { await Task.Delay(2500, ct); } catch (OperationCanceledException) { break; }
            }
        }

        private async Task WriteAsync(NetworkStream s, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await _signal.WaitAsync(ct);
                while (_queue.TryDequeue(out string cmd))
                {
                    byte[] data = Encoding.UTF8.GetBytes(cmd + Terminator);
                    await s.WriteAsync(data, 0, data.Length, ct);
                    await s.FlushAsync(ct);
                }
            }
        }

        private async Task ReadAsync(NetworkStream s, CancellationToken ct)
        {
            byte[] buf = new byte[4096];
            while (!ct.IsCancellationRequested)
            {
                int n = await s.ReadAsync(buf, 0, buf.Length, ct);
                if (n == 0) break;
            }
        }

        private void Cleanup()
        {
            try { _stream?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null; _client = null;
        }

        public void Stop()
        {
            if (_cts != null) { _cts.Cancel(); Cleanup(); _cts.Dispose(); _cts = null; }
        }

        public void Dispose() => Stop();
    }

    public class TemplateEngine
    {
        private readonly Dictionary<string, List<string>> _templates;
        public TemplateEngine(string path)
        {
            _templates = File.Exists(path)
                ? JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(File.ReadAllText(path))
                : new Dictionary<string, List<string>>();
        }

        public IEnumerable<string> Render(string key, Dictionary<string, string> tokens)
        {
            if (!_templates.TryGetValue(key, out var lines)) yield break;
            foreach (var line in lines)
            {
                string r = line;
                foreach (var kv in tokens) r = r.Replace("{" + kv.Key + "}", kv.Value ?? "");
                yield return r;
            }
        }
    }

    public static class ExcelRosterImporter
    {
        public static void CreateBlankTemplate(string xlsxPath)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("TEAM_ROSTER");
                ws.Cell("A1").Value = "TEAM NAME"; ws.Cell("B1").Value = "اسم الفريق";
                ws.Cell("A2").Value = "COACH";     ws.Cell("B2").Value = "اسم المدرب";
                ws.Cell("A4").Value = "NO";        ws.Cell("B4").Value = "PLAYER NAME"; ws.Cell("C4").Value = "ROLE (Main/Sub/GK)";
                ws.Range("A1:A2").Style.Font.Bold = true;
                ws.Range("A4:C4").Style.Font.Bold = true;
                ws.Range("A4:C4").Style.Fill.BackgroundColor = XLColor.LightGray;
                ws.Columns().AdjustToContents();
                wb.SaveAs(xlsxPath);
            }
        }

        public static TeamRoster ImportRoster(string xlsxPath)
        {
            var roster = new TeamRoster();
            using (var wb = new XLWorkbook(xlsxPath))
            {
                var ws = wb.Worksheet(1);
                roster.TeamName = ws.Cell("B1").GetString().Trim();
                roster.Coach    = ws.Cell("B2").GetString().Trim();
                int row = 5;
                while (!ws.Cell(row, 1).IsEmpty() || !ws.Cell(row, 2).IsEmpty())
                {
                    roster.Players.Add(new PlayerEntry
                    {
                        Number = ws.Cell(row, 1).GetString().Trim(),
                        Name   = ws.Cell(row, 2).GetString().Trim(),
                        Role   = string.IsNullOrWhiteSpace(ws.Cell(row, 3).GetString()) ? "Main" : ws.Cell(row, 3).GetString().Trim()
                    });
                    row++;
                }
            }
            return roster;
        }
    }
}

