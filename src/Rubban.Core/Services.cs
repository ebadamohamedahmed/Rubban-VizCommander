using System.Drawing;
using System.Drawing.Imaging;
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
        private static string Val(IXLWorksheet ws, int row, int col)
        {
            var cell = ws.Cell(row, col);
            if (cell.IsMerged())
            {
                var range = cell.MergedRange();
                return range.FirstCell().GetFormattedString().Trim();
            }
            string s = cell.GetFormattedString().Trim();
            if (string.IsNullOrWhiteSpace(s))
                s = (cell.Value.ToString() ?? "").Trim();
            return s;
        }

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
                string c1 = Val(ws, 1, 3);
                string c2Header = Val(ws, 2, 3);
                string c3Val = Val(ws, 3, 3);

                bool isListLayout = !string.IsNullOrWhiteSpace(c1) &&
                                    (c2Header.Contains("اسم") || !string.IsNullOrWhiteSpace(c3Val));

                if (isListLayout)
                {
                    roster.TeamName = c1;
                    for (int r = ws.LastRowUsed()?.RowNumber() ?? 27; r >= 1; r--)
                    {
                        string labelColD = Val(ws, r, 4);
                        string valColC = Val(ws, r, 3);
                        if (labelColD.Contains("مدرب") && !string.IsNullOrWhiteSpace(valColC))
                        {
                            roster.Coach = valColC;
                            break;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(roster.Coach))
                        roster.Coach = Val(ws, 27, 3);

                    string currentSection = "Main";
                    int lastRow = Math.Max(30, ws.LastRowUsed()?.RowNumber() ?? 30);
                    for (int r = 3; r <= lastRow; r++)
                    {
                        string secCell = Val(ws, r, 6);
                        if (!string.IsNullOrWhiteSpace(secCell))
                            currentSection = secCell.Contains("البدلاء") ? "Sub" : "Main";

                        string gk = Val(ws, r, 1);
                        string cap = Val(ws, r, 2);
                        string pName = Val(ws, r, 3);
                        string pNum = Val(ws, r, 4);

                        if (string.IsNullOrWhiteSpace(pName) && string.IsNullOrWhiteSpace(pNum))
                            continue;
                        if (pName.Contains("مدرب")) continue;

                        string role = currentSection;
                        if (gk.Equals("G", StringComparison.OrdinalIgnoreCase)) role = "GK";
                        if (cap.Equals("C", StringComparison.OrdinalIgnoreCase)) role += " (C)";

                        roster.Players.Add(new PlayerEntry
                        {
                            Number = pNum,
                            Name   = pName,
                            Role   = role
                        });
                    }
                }
                else
                {
                    roster.TeamName = Val(ws, 1, 2);
                    roster.Coach    = Val(ws, 2, 2);
                    int row = 5;
                    while (!string.IsNullOrWhiteSpace(Val(ws, row, 1)) || !string.IsNullOrWhiteSpace(Val(ws, row, 2)))
                    {
                        string rl = Val(ws, row, 3);
                        roster.Players.Add(new PlayerEntry
                        {
                            Number = Val(ws, row, 1),
                            Name   = Val(ws, row, 2),
                            Role   = string.IsNullOrWhiteSpace(rl) ? "Main" : rl
                        });
                        row++;
                    }
                }
            }
            return roster;
        }
    }

    public static class ColorSwatchGenerator
    {
        public static string CreatePngSwatch(string baseDir, string label, Color color, int size = 256)
        {
            string colorsDir = Path.Combine(baseDir, "Assets", "Colors");
            Directory.CreateDirectory(colorsDir);

            string safeLabel = string.IsNullOrWhiteSpace(label) ? "team" : string.Concat(label.Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(safeLabel)) safeLabel = "team";

            string hex = $"{color.R:X2}{color.G:X2}{color.B:X2}";
            string fileName = $"{safeLabel}_{hex}.png";
            string fullPath = Path.Combine(colorsDir, fileName);

            using (var bmp = new Bitmap(size, size))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(color);
                bmp.Save(fullPath, ImageFormat.Png);
            }

            return fullPath.Replace("\\", "/");
        }
    }
}

