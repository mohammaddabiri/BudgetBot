using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace BudgetBotService
{

    public partial class BudgetBotService : ServiceBase
    {
        //static BudgetBotService Service;
        Telegram.Bot.Echo.Program Service;

        public BudgetBotService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Service.Start();
        }

        protected override void OnStop()
        {
        }
    }
}
