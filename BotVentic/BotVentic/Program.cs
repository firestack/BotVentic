using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Configuration.Install;
using System.Configuration.Assemblies;

namespace BotVenticService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                string parameter = string.Concat(args);
                switch (parameter)
                {
                    case "--install":
                        ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                        Console.WriteLine("Installing service...");
                        break;
                    case "--uninstall":
                        ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
                        Console.WriteLine("Uninstalling service...");
                        break;

                    default:
                        BotVentic.DiscordBot.Mainloop();
                        break;
                }
            }
            else
            {

                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]

                {
                    new BotVentic.BDService()
                };
                ServiceBase.Run(ServicesToRun);
            }

        }
    }
}
