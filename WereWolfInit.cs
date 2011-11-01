using System;
using System.ComponentModel;
using System.ServiceProcess;
using Squishy.Irc;
namespace WerewolvesBot
{
    public partial class WereWolfInit : ServiceBase
    {
        public static IrcConnection Irc = new IrcConnection
        {
            UserName = Properties.Settings.Default.IrcUsername,
            ServerPassword = Properties.Settings.Default.IrcServerPassword,
            Nicks = new[] {Properties.Settings.Default.IrcNick}
        };
        public WereWolfInit()
        {
            InitializeComponent();
        }
        public static void Init()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                Irc.BeginConnect(Properties.Settings.Default.IrcServer, Convert.ToInt32(Properties.Settings.Default.IrcPort), null);
            };
            worker.RunWorkerAsync();
        }
        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
