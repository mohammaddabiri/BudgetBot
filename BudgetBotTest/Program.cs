using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace BudgetBotTest
{
    class Program
    {
        static List<CommandParser> s_commandParsers = new List<CommandParser>();
        static ServiceStack.Redis.RedisClient s_redis;
        static void Main(string[] args)
        {
            s_commandParsers.Add(new ListCommandParser());
            s_commandParsers.Add(new ClearListCommandParser());            
            s_commandParsers.Add(new AllocateBudgetCommandParser());
            s_commandParsers.Add(new AddTransactionCommandParser());
            s_commandParsers.Add(new ReportBudgetCommandParser());

            s_redis = ServiceStack.Redis.RedisClient.New();
            //Run().Wait();

            while(true)
            {
                var userCommand = Console.ReadLine();

                //CommandLine.Parser.Default.ParseArguments(userCommand, 
                Command command;
                if(FindCommandParser(userCommand, out command))
                {
                    //Console.WriteLine("Command found");
                    InterpretCommand(command);
                    Console.WriteLine("");
                }
                else
                {
                    Console.WriteLine("Command not found");
                }

                //Console.WriteLine(userCommand);
            }
        }

        static void InterpretCommand(Command command)
        {
            // Just implementing execution from within the command for now.
            command.Execute();
        }

        static bool FindCommandParser(string commandString, out Command outCommand)
        {
            commandString = commandString.Trim();
            if (commandString == string.Empty)
            {
                outCommand = null;
                return false;
            }

            commandString = commandString.ToLower();
            var commandArgStrings = commandString.Split(new char[] { ' ' });
            var args = new List<CommandLineArg>(commandArgStrings.Length);
            foreach(var argString in commandArgStrings)
            {
                args.Add(new CommandLineArg(argString));
            }
            foreach (var parser in s_commandParsers)
            {
                try
                {
                    var parsedCommand = parser.Parse(args.ToArray());
                    if(parsedCommand != null)
                    {
                        outCommand = parsedCommand;
                        return true;
                    }
                }
                catch { }
            }

            outCommand = null;
            return false;
        }

        public class AllocateBudgetCommand : Command
        {
            public string Category;
            public float Amount;
            DateTime StartDate;
            BudgetInterval Interval;

            public AllocateBudgetCommand(string category, float amount, DateTime startDate, BudgetInterval interval)
            {
                Category = category;
                Amount = amount;
                StartDate = startDate;
                Interval = interval;            
            }

            public override Task Execute()
            {
                BudgetEndpoints.AddBudgetCategory(Category, Amount, StartDate, Interval);
                return null;
            }
        }

        public class ClearListCommand : Command
        {
            public string Category;

            public ClearListCommand(string category)
            {
                Category = category;
            }

            public override Task Execute()
            {
                var allTransactions = BudgetEndpoints.FetchBudgetTransactions(Category);
                allTransactions.Clear();
                s_redis.Set("budget-" + Category.ToLower(), allTransactions);
                s_redis.Save();
                return base.Execute();
            }

            string FormatTransaction(BudgetTransaction transaction)
            {
                var noteFormatting = !string.IsNullOrEmpty(transaction.Notes) ? "{0}: {1} - {2}" : "{0}: {1}";
                //if (!string.IsNullOrEmpty(transaction.Notes))
                //{
                return string.Format(noteFormatting, transaction.Timestamp.ToShortDateString(), transaction.Cost.ToString("C").PadRight(8), transaction.Notes);
                //}

                //return string.Format(noteFormatting, transaction.Timestamp.ToShortDateString(), transaction.Cost.ToString("C"));
            }
        }
        
        public class ListCommand : Command
        {
            public string Category;

            public ListCommand(string category)
            {
                Category = category;
            }

            public override Task Execute()
            {
                var allTransactions = BudgetEndpoints.FetchBudgetTransactions(Category);
                foreach(var transaction in allTransactions)
                {
                    var transactionMessage = FormatTransaction(transaction);
                    Console.WriteLine(transactionMessage);
                }
                return base.Execute();
            }

            string FormatTransaction(BudgetTransaction transaction)
            {
                var noteFormatting = !string.IsNullOrEmpty(transaction.Notes) ? "{0}: {1} - {2}" : "{0}: {1}";
                //if (!string.IsNullOrEmpty(transaction.Notes))
                //{
                    return string.Format(noteFormatting, transaction.Timestamp.ToShortDateString(), transaction.Cost.ToString("C").PadRight(8), transaction.Notes);
                //}

                //return string.Format(noteFormatting, transaction.Timestamp.ToShortDateString(), transaction.Cost.ToString("C"));
            }
        }

        public abstract class Command
        {
            public virtual async Task Execute() { }
        }

        public interface ICommandParser { }
        public abstract class CommandParser : ICommandParser
        {
            public abstract Command Parse(CommandLineArg[] args);
        }

        public class ReportBudgetCommand : Command
        {
            public string Category = string.Empty;

            public ReportBudgetCommand() { }
            public ReportBudgetCommand(string category) { Category = category; }

            public override Task Execute()
            {
                var budgetList = BudgetEndpoints.FetchBudgetList();
                if (budgetList == null || budgetList.Categories == null || budgetList.Categories.Count == 0)
                {
                    Console.WriteLine("No budgets defined.");
                    return null;
                }


                if (string.IsNullOrEmpty(Category))
                {
                    foreach (var budgetCategory in budgetList.Categories)
                    {
                        var balance = BudgetEndpoints.GetBalance(budgetCategory.Name);
                        var message1 = FormatCategoryReport(budgetCategory, balance);
                        Console.WriteLine(message1);
                    }
                }
                else
                {
                    var category = budgetList[Category];
                    if(category == null)
                    {
                        Console.WriteLine(string.Format("Category \"{0}\" not found.", Category));
                        return null;
                    }

                    var balance = BudgetEndpoints.GetBalance(category.Name);
                    var catMessage1 = FormatCategoryReport(category, balance);
                    Console.WriteLine(catMessage1);
                }
                return null;
            }

            public static string FormatCategoryReport(BudgetCategory category, float balance)
            {
                var spendingPct = balance / category.Limit;
                var remainingDays = category.Period.RemainingDays;
                return string.Format("{0}: {1} / {2} ({3}) {4} days", category.Name.PadRight(16), balance.ToString("C"), category.Limit.ToString("C"), spendingPct.ToString("P0"), remainingDays);
            }
        }
        
        public class AddTransactionCommand : Command
        {
            public string Category;
            public float Cost;
            public string Notes;

            public AddTransactionCommand()
            {
            }

            public AddTransactionCommand(string category, float cost, string notes = "")
            {
                Category = category;
                Cost = cost;
                Notes = notes;
            }

            public override Task Execute()
            {
                var budgetList = BudgetEndpoints.FetchBudgetList();
                if(budgetList == null)
                {
                    Console.WriteLine("No budgets allocated.");
                }
                else
                {
                    var category = budgetList[Category];
                    if(category == null)
                    {
                        var errorMessage = string.Format("No category \"{0}\" defined.", Category);
                        Console.WriteLine(errorMessage);
                    }
                    else
                    {
                        var newTransaction = BudgetEndpoints.AddTransaction(Category, Cost, Notes);
                        if(newTransaction == null)
                        {
                            Console.WriteLine("Failed to log transaction");
                        }
                        else
                        {
                            var allTransactions = BudgetEndpoints.FetchBudgetTransactions(Category);
                            var allExpenses = allTransactions.Where(o=>o.Timestamp.CompareTo(category.Period.StartDate) > 0).Sum(x => x.Cost);
                            var newBalance = category.Limit - allExpenses;
                            var balanceMessage = string.Format("Balance: {0}", newBalance.ToString("C"));
                            //var message2 = string.Format("{0} days remaining", budgetCategory.Days);
                            Console.WriteLine(balanceMessage);
                            s_redis.Save();
                        }
                    }
                }
                return null;
            }
        }

        public class AddTransactionCommandParser : CommandParser
        {
            public override Command Parse(CommandLineArg[] args)
            {
                var notelessCommand = args.Length == 2 && args[0].IsAlpha && args[1].IsNumeric;
                if (notelessCommand)
                {
                    return new AddTransactionCommand(args[0], args[1].AsFloat);
                }
                var withNoteCommand = args.Length > 2 && args[0].IsAlpha && args[1].IsNumeric;
                if (withNoteCommand)
                {
                    var notesBuilder = new StringBuilder();
                    for(var i = 2; i < args.Length; ++i)
                    {
                        notesBuilder.Append(args[i]);
                        notesBuilder.Append(" ");
                    }
                    return new AddTransactionCommand(args[0], args[1].AsFloat, notesBuilder.ToString());
                }

                return null;
            }
        }
        public class ReportBudgetCommandParser : CommandParser
        {
            public override Command Parse(CommandLineArg[] args)
            {
                if(args.Length == 1 && args[0] == "budget")
                {
                    return new ReportBudgetCommand();
                }
                else if(args.Length == 1 && args[0].IsAlpha)
                {
                    return new ReportBudgetCommand(args[0]);
                }
                
                return null;
            }
        }

        public class CommandLineArg
        {
            public string AsString;
            public int AsInt;
            public float AsFloat;
            public DateTime AsDate;
            public bool IsInt;
            public bool IsFloat;
            public bool IsNumeric;
            public bool IsAlpha;
            public bool IsDate;

            public static implicit operator string (CommandLineArg arg)
            {
                return arg.AsString;
            }

            public override bool Equals(object obj)
            {
                var otherArg = obj as CommandLineArg;
                if(otherArg != null)
                {
                    return string.Compare(AsString, otherArg.AsString, true) == 0;
                }

                return base.Equals(obj);
            }
            public CommandLineArg(string arg)
            {
                AsString = arg;
                IsInt = int.TryParse(arg, out AsInt);
                IsFloat = float.TryParse(arg, out AsFloat);
                IsDate = DateTime.TryParse(arg, out AsDate);
                IsNumeric = IsInt || IsFloat;
                IsAlpha = !IsNumeric && !IsDate;
            }
        }
        public class AllocateBudgetCommandParser : CommandParser
        {
            public override Command Parse(CommandLineArg[] args)
            {
                // budget food 200 20/12 1m
                BudgetInterval interval;
                if (args.Length == 5 && args[0] == "budget" && args[1].IsAlpha && args[2].IsFloat && args[3].IsDate && args[4].IsAlpha && BudgetInterval.TryParse(args[4], out interval))
                {                    
                    return new AllocateBudgetCommand(args[1], args[2].AsFloat, args[3].AsDate, interval);
                }

                return null;          
            }
        }


        public class ClearListCommandParser : CommandParser
        {
            public override Command Parse(CommandLineArg[] args)
            {
                if (args.Length == 3 && args[0].IsAlpha && BudgetEndpoints.GetBudgetCategory(args[0]) != null && args[1] == "list" && args[2] == "clear")
                {
                    return new ClearListCommand(args[0]);
                }

                return null;
            }
        }

        public class ListCommandParser : CommandParser
        {
            public override Command Parse(CommandLineArg[] args)
            {
                if(args.Length == 2 && args[0].IsAlpha && args[1] == "list" && BudgetEndpoints.GetBudgetCategory(args[0]) != null)
                {
                    return new ListCommand(args[0]);
                }

                return null;
            }
        }

        public static class BudgetEndpoints
        {
            public static BudgetTransactions FetchBudgetTransactions(string category)
            {
                var transactionJson = s_redis.GetValue("budget-"+category.ToLower());
                var budgetList = BudgetTransactions.FromJson(transactionJson);                
                return budgetList;
            }

            public static BudgetCategory GetBudgetCategory(string category)
            {
                var budgetList = FetchBudgetList();
                return budgetList[category];
            }

            public static float GetBalance(string categoryName)
            {
                var allTransactions = FetchBudgetTransactions(categoryName);
                var category = GetBudgetCategory(categoryName);
                var allExpenses = allTransactions.Where(o => o.Timestamp.CompareTo(category.Period.StartDate) > 0).Sum(x => x.Cost);
                var newBalance = category.Limit - allExpenses;
                return newBalance;
            }

            public static BudgetList FetchBudgetList()
            {
                var budgetJson = s_redis.GetValue("budgets");
                var budgetList = BudgetList.ParseJson(budgetJson);
                if (budgetList == null)
                {
                    budgetList = new BudgetList();
                }

                return budgetList;
            }

            public static BudgetTransaction AddTransaction(string category, float cost, string notes = "")
            {
                var transactions = FetchBudgetTransactions(category);
                var newTransaction = new BudgetTransaction(DateTime.Now, category, cost, notes);
                transactions.Add(newTransaction);

                s_redis.SetValue("budget-" + category.ToLower(), transactions.AsJson());
                return newTransaction;
            }

            public static BudgetList AddBudgetCategory(string newCategory, float budget, DateTime startDate, BudgetInterval interval)
            {
                var budgetList = FetchBudgetList();
                var budgetCategory = budgetList[newCategory];
                if(budgetCategory == null)
                {
                    budgetCategory = new BudgetCategory(newCategory);
                    budgetList.Categories.Add(budgetCategory);
                }

                budgetCategory.Limit = budget;
                budgetCategory.Period = new BudgetPeriod(startDate, interval);
                
                s_redis.SetValue("budgets", budgetList.AsJson());
                s_redis.Save();
                //s_redis.Set("budgets", budgetList);
                //s_redis.Add("budgets", budgetList);
                return budgetList;
            }
        }

        [Serializable]
        public class BudgetTransaction
        {
            public DateTime Timestamp;
            public string Category;
            public float Cost;
            public string Notes;

            public BudgetTransaction()
            {
            }

            public BudgetTransaction(DateTime timestamp, string category, float cost, string notes = "")
            {
                Timestamp = timestamp;
                Category = category;
                Cost = cost;
                Notes = notes;
            }
        }

        [Serializable]
        public class BudgetTransactions : List<BudgetTransaction>
        {
            //public List<BudgetTransaction> Transactions = new List<BudgetTransaction>();

            public static BudgetTransactions FromJson(string json)
            {
                try
                {
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<BudgetTransactions>(json);
                }
                catch { }
                return new BudgetTransactions();
            }

            public string AsJson()
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(this);
            }
        }

        public enum BudgetIntervals
        {
            Days,
            Monthly,
            Quaterly,
            Yearly,
        }

        [Serializable]
        public class BudgetInterval
        {
            public BudgetIntervals Units = BudgetIntervals.Monthly;
            public int Span = 1;

            public BudgetInterval() { }            
            public BudgetInterval(BudgetIntervals units, int span)
            {
                Units = units;
                Span = span;
            }

            public static bool TryParse(string text, out BudgetInterval outInterval)
            {
                outInterval = null;

                if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(text) || text.Length < 2)
                {
                    return false;
                }

                BudgetIntervals interval = BudgetIntervals.Monthly;
                text = text.Trim().ToLower();
                var units = text[text.Length - 1];
                
                switch(units)
                {
                    case 'd':
                        interval = BudgetIntervals.Days;
                        break;
                    case 'm':
                        interval = BudgetIntervals.Monthly;
                        break;
                    case 'q':
                        interval = BudgetIntervals.Quaterly;
                        break;
                    case 'y':
                        interval = BudgetIntervals.Yearly;
                        break;
                }

                var spanText = text.Substring(0, text.Length - 1);
                int span = 0;
                if(!int.TryParse(spanText, out span))
                {
                    return false;
                }
                
                outInterval = new BudgetInterval(interval, span);
                return true;
            }
        }

        [Serializable]
        public class BudgetPeriod
        {
            public DateTime StartDate;
            public BudgetInterval Interval = new BudgetInterval();
            
            public int RemainingDays
            {
                get
                {
                    var remainingDays = (End - DateTime.Now).Days;
                    return remainingDays;
                }
            }

            public DateTime End
            {
                get
                {
                    switch(Interval.Units)
                    {
                        case BudgetIntervals.Monthly:
                            return StartDate.AddMonths(Interval.Span);
                        case BudgetIntervals.Quaterly:
                            return StartDate.AddMonths(3 * Interval.Span);
                        case BudgetIntervals.Days:
                            return StartDate.AddDays(Interval.Span);
                    }

                    return DateTime.Now;
                }
            }

            public BudgetPeriod()
            {
            }

            public BudgetPeriod(DateTime startDate, BudgetInterval interval)
            {
                StartDate = startDate;
                Interval = interval;
            }
        }

        [Serializable]
        public class BudgetCategory
        {
            public string Name;
            public float Limit;
            public BudgetPeriod Period = new BudgetPeriod();

            public BudgetCategory()
            {
            }

            public BudgetCategory(string name)
            {
                Name = name;
            }

            public BudgetCategory(string name, float limit, BudgetPeriod period)
            {
                Name = name;
                Limit = limit;
                Period = period;
            }
        }

        [Serializable]
        public class BudgetList
        {
            public BudgetCategory this[string categoryName]
            {
                get { return Categories.Find(x => string.Compare(x.Name, categoryName, true) == 0); }
            }

            public List<BudgetCategory> Categories = new List<BudgetCategory>();

            public static BudgetList ParseJson(string json)
            {
                try
                {
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<BudgetList>(json);
                }
                catch { }
                return new BudgetList();
            }

            public string AsJson()
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(this);
            }
        }            
    }
}
