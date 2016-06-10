using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using NetworkFlooder;

namespace ONT
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// On startup of the application, instantiates dependency objects (like MainWindowVM for MainWindow.xaml.cs).
        /// </summary>
        /// <param name="e">Not used</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            IUnityContainer container = new UnityContainer();
            var MainWindow = container.Resolve<MainWindow>();
            MainWindow.Show();
        }
    }
}
