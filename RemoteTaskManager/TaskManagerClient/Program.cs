/*
Домашнее задание. Написать сетевое приложение «Диспетчер задач», позволяющее:
• отобразить список запущенных процессов удаленного хоста;
• завершить на удаленном хосте процесс, выбранный из списка;
• обновить список процессов;
• создать новый процесс на удаленном хосте (путь к исполняемому файлу вводится в текстовое поле ввода).
*/
using System.Net.Sockets;
using System.Text.Json;

namespace TaskManagerClient;
internal static class Program { [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new ClientForm()); } }
public sealed class ClientForm : Form
{
    private readonly TextBox ipBox = new() { Text = "127.0.0.1", Width = 110 };
    private readonly TextBox portBox = new() { Text = "49200", Width = 65 };
    private readonly Button connectButton = new() { Text = "Connect", AutoSize = true };
    private readonly Button refreshButton = new() { Text = "Refresh", AutoSize = true, Enabled = false };
    private readonly Button killButton = new() { Text = "Stop selected", AutoSize = true, Enabled = false };
    private readonly TextBox pathBox = new() { PlaceholderText = "Path to executable file", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button startButton = new() { Text = "Start process", Dock = DockStyle.Fill, Enabled = false };
    private readonly ListView processes = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 24, Text = "Not connected" };
    private TcpClient? client; private StreamReader? reader; private StreamWriter? writer;
    public ClientForm()
    {
        Text = "Remote Task Manager — Client"; ClientSize = new Size(650, 450); processes.Columns.Add("ID", 100); processes.Columns.Add("Process name", 500);
        FlowLayoutPanel top = new() { Dock = DockStyle.Top, Height = 40 }; top.Controls.AddRange([new Label { Text = "Server IP:", AutoSize = true }, ipBox, new Label { Text = "Port:", AutoSize = true }, portBox, connectButton, refreshButton, killButton]);
        TableLayoutPanel bottom = new() { Dock = DockStyle.Bottom, Height = 35, ColumnCount = 2 }; bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); bottom.Controls.Add(pathBox, 0, 0); bottom.Controls.Add(startButton, 1, 0);
        Controls.Add(processes); Controls.Add(bottom); Controls.Add(status); Controls.Add(top); connectButton.Click += async (_, _) => await ConnectAsync(); refreshButton.Click += async (_, _) => await RefreshAsync(); killButton.Click += async (_, _) => await KillAsync(); startButton.Click += async (_, _) => await StartProcessAsync(); FormClosing += (_, _) => client?.Close();
    }
    private async Task ConnectAsync() { if (!int.TryParse(portBox.Text, out int port)) { SetStatus("Enter a valid port."); return; } try { client = new TcpClient(); await client.ConnectAsync(ipBox.Text, port); NetworkStream stream = client.GetStream(); reader = new StreamReader(stream); writer = new StreamWriter(stream) { AutoFlush = true }; connectButton.Enabled = false; refreshButton.Enabled = killButton.Enabled = pathBox.Enabled = startButton.Enabled = true; SetStatus("Connected."); await RefreshAsync(); } catch (Exception error) { SetStatus("Connection error: " + error.Message); client?.Close(); } }
    private async Task RefreshAsync() { Response? response = await SendRequestAsync(new Request { Command = "list" }); if (response?.Success != true || response.Processes is null) return; processes.Items.Clear(); foreach (ProcessInfo process in response.Processes) { ListViewItem item = new(process.Id.ToString()); item.SubItems.Add(process.Name); processes.Items.Add(item); } SetStatus($"Processes: {processes.Items.Count}"); }
    private async Task KillAsync() { if (processes.SelectedItems.Count == 0) { SetStatus("Select a process first."); return; } int id = int.Parse(processes.SelectedItems[0].Text); Response? response = await SendRequestAsync(new Request { Command = "kill", ProcessId = id }); SetStatus(response?.Message ?? "No response from server."); if (response?.Success == true) await RefreshAsync(); }
    private async Task StartProcessAsync() { Response? response = await SendRequestAsync(new Request { Command = "start", Path = pathBox.Text }); SetStatus(response?.Message ?? "No response from server."); if (response?.Success == true) { pathBox.Clear(); await RefreshAsync(); } }
    private async Task<Response?> SendRequestAsync(Request request) { if (reader is null || writer is null) return null; try { await writer.WriteLineAsync(JsonSerializer.Serialize(request)); string? line = await reader.ReadLineAsync(); return line is null ? null : JsonSerializer.Deserialize<Response>(line); } catch (Exception error) { SetStatus("Network error: " + error.Message); return null; } }
    private void SetStatus(string text) => status.Text = text;
}
public sealed class Request { public string Command { get; set; } = ""; public int ProcessId { get; set; } public string? Path { get; set; } }
public sealed class Response { public bool Success { get; set; } public string? Message { get; set; } public List<ProcessInfo>? Processes { get; set; } }
public sealed record ProcessInfo(int Id, string Name);
