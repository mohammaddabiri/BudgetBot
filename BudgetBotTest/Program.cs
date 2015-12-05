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
            s_commandParsers.Add(new AllocateBudgetCommandParser());
            s_commandParsers.Add(new ReportBudgetCommandParser());
            s_commandParsers.Add(new AddTransactionCommandParser());
            
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

            public AllocateBudgetCommand(string category, float amount)
            {
                Category = category;
                Amount = amount;
            }

            public override Task Execute()
            {
                BudgetEndpoints.AddBudgetCategory(Category, Amount);
                return null;
            }
        }

        public class ListCommand : Command
        {
            public override Task Execute()
            {
                return base.Execute();
                throw new NotImplementedException();
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
            public override Task Execute()
            {
                var budgetList = BudgetEndpoints.FetchBudgetList();
                if (budgetList == null || budgetList.Categories == null || budgetList.Categories.Count == 0)
                {
                    Console.WriteLine("No budgets defined.");
                }

                foreach (var budgetCategory in budgetList.Categories)
                {
                    var message = string.Format("{0}: {1}", budgetCategory.Name, budgetCategory.Limit);
                    Console.WriteLine(message);
                }

                return null;
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
                            var allExpenses = allTransactions.Sum(x => x.Cost);
                            var newBalance = category.Limit - allExpenses;
                            var balanceMessage = string.Format("Balance: {0}", newBalance.ToString("C"));
                            Console.WriteLine(balanceMessage);
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
                var withNoteCommand = args.Length == 3 && args[0].IsAlpha && args[1].IsNumeric && args[2].IsAlpha;
                if (withNoteCommand)
                {
                    return new AddTransactionCommand(args[0], args[1].AsFloat, args[2]);
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

                return null;
            }
        }

        public class CommandLineArg
        {
            public string AsString;
            public int AsInt;
            public float AsFloat;
            public bool IsInt;
            public bool IsFloat;
            public bool IsNumeric;
            public bool IsAlpha;

            public static implicit operator string (CommandLineArg arg)
            {
                return arg.AsString;
            }

            public CommandLineArg(string arg)
            {
                AsString = arg;
                IsInt = int.TryParse(arg, out AsInt);
                IsFloat = float.TryParse(arg, out AsFloat);
                IsNumeric = IsInt || IsFloat;
                IsAlpha = !IsNumeric;
            }
        }
        public class AllocateBudgetCommandParser : CommandParser
        {
            public override Command Parse(CommandLineArg[] args)
            {
                if (args.Length == 3 && args[0] == "budget" && !args[1].IsNumeric && args[2].IsFloat)
                {
                    return new AllocateBudgetCommand(args[1], args[2].AsFloat);
                }

                return null;          
            }
        }

        public class ListCommandParser : CommandParser
        {
            public override Command Parse(CommandLineArg[] args)
            {
                if(args.Length != 1 || args[0] != "list")
                {
                    throw new InvalidOperationException();
                }

                return new ListCommand();
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

            public static BudgetList AddBudgetCategory(string newCategory, float budget)
            {
                var budgetList = FetchBudgetList();
                var budgetCategory = budgetList[newCategory];
                if(budgetCategory == null)
                {
                    budgetCategory = new BudgetCategory(newCategory);
                    budgetList.Categories.Add(budgetCategory);
                }

                budgetCategory.Limit = budget;
                
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

        [System.Serializable]
        public class BudgetCategory
        {
            public string Name;
            public float Limit;

            public BudgetCategory()
            {
            }

            public BudgetCategory(string name)
            {
                Name = name;
            }

            public BudgetCategory(string name, float limit)
            {
                Name = name;
                Limit = limit;
            }
        }

        [System.Serializable]
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
