using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Criptomoeda
{

    class MoedaConfig
    {
        public string Symbol { get; set; }
        public decimal AlertaMin { get; set; }
        public decimal AlertaMax { get; set; }
        public int IntervaloSegundos { get; set; }
        public DateTime UltimaExecucao { get; set; } = DateTime.MinValue;
    }

    class Program : Form
    {

        private static List<MoedaConfig> Configs = new List<MoedaConfig>();
        private static HttpClient client = new HttpClient();
        private static Timer timer;
        private static NotifyIcon notifyIcon;
        private static readonly string logFile = "log.txt";

        /// <summary>
        /// Ponto de entrada principal para o aplicativo.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Information;
            notifyIcon.Visible = true;
            Application.Run(new Program());
        }

        public Program()
        {
            LoadConfig("config.txt");
            
            notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Information,
                Visible = true,
                Text = "Monitor Cripto"
            };

            // Menu no ícone da bandeja
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Abrir log", null, (s, e) => System.Diagnostics.Process.Start("notepad.exe", logFile));
            contextMenu.Items.Add("Sair", null, (s, e) => Application.Exit());
            notifyIcon.ContextMenuStrip = contextMenu;

            // Timer para monitorar
            timer = new Timer { Interval = 1000 };
            timer.Tick += async (s, e) => await MonitorarAsync();
            timer.Start();

            // Oculta a janela
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
        }

        private void LoadConfig(string filePath)
        {

            try
            {
                
                foreach (var line in File.ReadAllLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var parts = line.Split(',');
                    if (parts.Length == 4)
                    {
                        Configs.Add(new MoedaConfig
                        {
                            Symbol = parts[0].Trim(),
                            AlertaMin = decimal.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                            AlertaMax = decimal.Parse(parts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                            IntervaloSegundos = int.Parse(parts[3].Trim())
                        });
                    }
                }

            }
            catch (Exception)
            {
                MostrarAlerta($"Erro", $"config.txt não localizado.");
                throw;
            }
                        
        }

        private async Task MonitorarAsync()
        {
            foreach (var cfg in Configs)
            {
                if ((DateTime.Now - cfg.UltimaExecucao).TotalSeconds >= cfg.IntervaloSegundos)
                {
                    try
                    {
                        decimal price = await GetPriceAsync(cfg.Symbol);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {cfg.Symbol} = {price}");

                        if (price <= cfg.AlertaMin)
                            MostrarAlerta(cfg.Symbol, $"caiu abaixo de {cfg.AlertaMin}! Preço: {price}");

                        else if (price >= cfg.AlertaMax)
                            MostrarAlerta(cfg.Symbol, $"ultrapassou {cfg.AlertaMax}! Preço: {price}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao buscar {cfg.Symbol}: {ex.Message}");
                    }

                    cfg.UltimaExecucao = DateTime.Now;
                }
            }
        }

        private async Task<decimal> GetPriceAsync(string symbol)
        {
            string url = $"https://api.binance.com/api/v3/ticker/price?symbol={symbol}";
            var response = await client.GetStringAsync(url);
            using (JsonDocument doc = JsonDocument.Parse(response))
            {
                string priceStr = doc.RootElement.GetProperty("price").GetString();
                return decimal.Parse(priceStr, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private void MostrarAlerta(string symbol, string mensagem)
        {
            string texto = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {symbol} {mensagem}";

            // Notificação pop-up
            notifyIcon.BalloonTipTitle = $"🚨 ALERTA: {symbol}";
            notifyIcon.BalloonTipText = mensagem;
            notifyIcon.ShowBalloonTip(5000);

            // Salvar no log
            File.AppendAllText(logFile, texto + Environment.NewLine);
        }


    }
}
