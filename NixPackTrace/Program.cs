using System;
using System.Windows.Forms;
using NixPackTrace.UI;

namespace NixPackTrace
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            
            // Simple generic login logic or launch main form
            Application.Run(new LoginForm());
        }
    }
}
