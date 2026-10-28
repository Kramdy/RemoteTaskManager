/*
Домашнее задание. Написать сетевое приложение «Диспетчер задач», позволяющее:
• отобразить список запущенных процессов удаленного хоста;
• завершить на удаленном хосте процесс, выбранный из списка;
• обновить список процессов;
• создать новый процесс на удаленном хосте (путь к исполняемому файлу вводится в текстовое поле ввода).
*/
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace TaskManagerServer;
internal static class Program { [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new ServerForm()); } }
public sealed class ServerForm : Form
{
    private readonly TextBox portBox = new() { Text = "49200", Width = 80 };
    private readonly Button startButton = new() { Text = "Start server", AutoSize = true };
    private readonly ListBox log = new() { Dock = DockStyle.Fill };
    private TcpListener? listener;
    public ServerForm() { Text = "Remote Task Manager — Server"; ClientSize = new Size(520, 360); FlowLayoutPanel top = new() { Dock = DockStyle.Top, Height = 40 }; top.Controls.AddRange([new Label { Text = "Port:", AutoSize = true }, portBox, startButton]); Controls.Add(log); Controls.Add(top); startButton.Click += async (_, _) => await StartAsync(); FormClosing += (_, _) => listener?.Stop(); }
    private async Task StartAsync() { if (!int.TryParse(portBox.Text, out int port)) { Add("Enter a valid port."); return; } listener = new TcpListener(IPAddress.Any, port); listener.Start(); startButton.Enabled = false; Add($"Server started on port {port}."); try { while (listener is not null) _ = HandleClientAsync(await listener.AcceptTcpClientAsync()); } catch (SocketException) { } }
    private async Task HandleClientAsync(TcpClient client) { Add($"Client connected: {client.Client.RemoteEndPoint}"); using (client) { using NetworkStream stream = client.GetStream(); using StreamReader reader = new(stream); using StreamWriter writer = new(stream) { AutoFlush = true }; try { while (await reader.ReadLineAsync() is string line) { Request? request = JsonSerializer.Deserialize<Request>(line); Response response = request is null ? Response.Error("Invalid request") : ProcessRequest(request); await writer.WriteLineAsync(JsonSerializer.Serialize(response)); } } catch (Exception error) { Add("Client error: " + error.Message); } Add("Client disconnected."); } }
    private Response ProcessRequest(Request request) { try { return request.Command switch { "list" => new Response { Success = true, Processes = Process.GetProcesses().OrderBy(p => p.ProcessName).Select(p => new ProcessInfo(p.Id, p.ProcessName)).ToList() }, "kill" => Kill(request.ProcessId), "start" => StartProcess(request.Path), _ => Response.Error("Unknown command") }; } catch (Exception error) { return Response.Error(error.Message); } }
    private static Response Kill(int id) { Process.GetProcessById(id).Kill(); return new Response { Success = true, Message = $"Process {id} was stopped." }; }
    private static Response StartProcess(string? path) { if (string.IsNullOrWhiteSpace(path)) return Response.Error("Enter an executable path."); Process.Start(path); return new Response { Success = true, Message = "Process started." }; }
    private void Add(string message) { if (InvokeRequired) { BeginInvoke(() => Add(message)); return; } log.Items.Add(message); log.TopIndex = log.Items.Count - 1; }
}
public sealed class Request { public string Command { get; set; } = ""; public int ProcessId { get; set; } public string? Path { get; set; } }
public sealed class Response { public bool Success { get; set; } public string? Message { get; set; } public List<ProcessInfo>? Processes { get; set; } public static Response Error(string message) => new() { Success = false, Message = message }; }
public sealed record ProcessInfo(int Id, string Name);
