using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.ServiceProcess;
using System.Text;
using System.Timers;

namespace Picker
{
    internal class Report
    {
        private const string wmi_os_query = "select * from Win32_OperatingSystem";
        private const string wmi_cpu_query = "Select * from Win32_Processor";
        private const string wmi_pc_query = "Select * from Win32_ComputerSystem";

        public int ReportId { get; set; }
        public string MemLoadKib { get; set; }
        public string OSManufacturer { get; set; }
        [Key]
        public string CSName { get; set; }
        public string CPUName { get; set; }
        public string CPUManufacturer { get; set; }
        public string CPULoadPercent { get; set; }
        public string LoggedUser { get; set; }

        public Report()
        {
            ManagementObject mo = new ManagementObjectSearcher(wmi_os_query).Get()
                .Cast<ManagementObject>().First();

            string totalMemMib = mo["TotalVisibleMemorySize"].ToString();
            string freeMemMib = mo["FreePhysicalMemory"].ToString();
            MemLoadKib = freeMemMib + "/" + totalMemMib;
            OSManufacturer = mo["Manufacturer"].ToString();
            CSName = mo["CSName"].ToString();

            mo = new ManagementObjectSearcher(wmi_cpu_query).Get()
                .Cast<ManagementObject>().First();

            CPUName = mo["Name"].ToString();
            CPUManufacturer = mo["Manufacturer"].ToString();
            CPULoadPercent = mo["LoadPercentage"].ToString();

            mo = new ManagementObjectSearcher(wmi_pc_query).Get()
                .Cast<ManagementObject>().First();

            LoggedUser = mo["UserName"].ToString();
        }
    }

    public partial class Picker : ServiceBase
    {
        private static readonly ServiceContext _context = new ServiceContext();
        private Timer timer;

        public Picker()
        {
            InitializeComponent();
        }

        private static void LogWrite(string text)
        {
            using (var sw = new StreamWriter(Directory.GetCurrentDirectory() + "\\ServiceLog.txt", true))
            {
                sw.WriteLine($"[{DateTime.Now}] {text}");
            }
        }

        private static void Post()
        {
            try
            {
                var report = new Report();

                HttpClient client = new HttpClient();
                var result = client.PostAsync("https://localhost:44311/api/values", new StringContent(JsonConvert.SerializeObject(report).ToString(),
                    Encoding.UTF8, "application/json"));

                var previous = _context.Reports.Where(r => r.CSName == report.CSName).FirstOrDefault();

                if (previous == null)
                {
                    _context.Reports.Add(report);
                }
                else
                {
                    previous = report;
                    _context.Entry(previous).State = EntityState.Modified;
                }

                _context.SaveChanges();

                LogWrite("Report sent");
            }
            catch (Exception ex)
            {
                LogWrite(ex.ToString());
            }
        }

        protected override void OnStart(string[] args)
        {
            timer = new Timer(1800000);
            timer.AutoReset = true;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();

            LogWrite("Service started...");

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            
            try
            {
                Post();
            }
            catch (Exception ex)
            {
                LogWrite(ex.ToString());
            }
        }

        protected override void OnStop()
        {
            timer.Stop();
            timer = null;

            LogWrite("Service stopped.");
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Post();
        }
    }

    internal class ServiceContext : DbContext
    {
        public ServiceContext() : base("Default")
        {

        }

        public DbSet<Report> Reports { get; set; }
    }
}
