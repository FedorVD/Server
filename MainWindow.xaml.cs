using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
        }
        class ServerObject
        {
            Dispatcher main_dispatcher;
            MainWindow main_window;
            public ServerObject(Dispatcher disp, MainWindow m_window) 
            { 
                main_dispatcher = disp; 
                main_window = m_window;
            }
            TcpListener tcplistener = new TcpListener(IPAddress.Any, 8888);
            List<ClientObject> clients = new List<ClientObject>();
            protected internal async void RemoveConnection(string id)
            {
                ClientObject? client = clients.FirstOrDefault(c => c.Id == id);
                if (client != null) { clients.Remove(client); }
                client?.Close();

            }
            protected internal async Task ListenAsync()
            {
                
                try
                {
                    tcplistener.Start();
                    main_dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () { main_window.ChatText.Text += $"Sever started. Waiting for client connection...\n"; });
                    while (true)
                    {
                        TcpClient tcpClient = await tcplistener.AcceptTcpClientAsync();
                        ClientObject clientObject = new ClientObject(tcpClient, this, main_dispatcher, main_window);
                        clients.Add(clientObject);
                        Task.Run(clientObject.ProcessAsync);

                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
                finally { Disconect(); }
            }
            protected internal async Task BroadcastMessageAsync(string message, string id)
            {
                foreach (var client in clients)
                {
                    if (client.Id != id)
                    {
                        await client.Writer.WriteLineAsync(message);
                        await client.Writer.FlushAsync();
                    }
                }
            }
            protected internal void Disconect()
            {
                tcplistener.Stop();
            }
        }

        class ClientObject
        {
            protected internal string Id { get; } = Guid.NewGuid().ToString();
            protected internal StreamWriter Writer { get; }
            protected internal StreamReader Reader { get; }

            TcpClient client;
            ServerObject server;
            Dispatcher main_dispatcher;
            MainWindow main_window;

            public ClientObject(TcpClient tcpclient, ServerObject serverobject, Dispatcher disp, MainWindow m_window)
            {
                client = tcpclient;
                server = serverobject;
                var stream = client.GetStream();
                Reader = new StreamReader(stream);
                Writer = new StreamWriter(stream);
                main_dispatcher = disp;
                main_window = m_window;
            }

            public async Task ProcessAsync()
            {
                try
                {
                    string? userName = await Reader.ReadLineAsync();
                    string? message = $"{userName} connect to chat.";
                    main_dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () { main_window.ChatText.Text += $"{message}\n"; });
                    await server.BroadcastMessageAsync(message, Id);
                    while (true)
                    {
                        try
                        {
                            message = await Reader.ReadLineAsync();
                            if (message == null) continue;
                            message = $"{userName}: {message}";
                            main_dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () { main_window.ChatText.Text += $"{message}\n"; });
                            await server.BroadcastMessageAsync(message, Id);
                        }
                        catch
                        {
                            message = $"{userName} disconnect chat.";
                            main_dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () { main_window.ChatText.Text += $"{message}\n"; });
                            await server.BroadcastMessageAsync(message, Id);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    server.RemoveConnection(Id);
                }
            }
            protected internal void Close()
            {
                Writer.Close();
                Reader.Close();
                client.Close();
            }
        }

        private void Start_ClickAsync(object sender, RoutedEventArgs e)
        {
            BeginChat();
        }

        private async Task BeginChat()
        {
            ServerObject server = new ServerObject(this.Dispatcher, this);
            await server.ListenAsync();
        }
    }

}