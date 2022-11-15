using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WinLaunchUpdate
{
    public partial class App : Application
    {
        public static bool Silent = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.First() == "-silent")
            {
                Silent = true;
            }
        }
    }
}
