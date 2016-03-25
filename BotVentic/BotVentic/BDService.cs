using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace BotVentic
{
    partial class BDService : ServiceBase
    {
        private Thread PrivateBotThread;
        public BDService()
        {
            InitializeComponent();
            PrivateBotThread = new Thread(new ThreadStart(BotVentic.DiscordBot.Mainloop));
        }

        protected override void OnStart(string[] args)
        {
            // TODO: Add code here to start your service.
            PrivateBotThread.Start();
        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.
            PrivateBotThread.Abort();
        }
    }
}
