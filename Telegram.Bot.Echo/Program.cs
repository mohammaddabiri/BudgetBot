using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using File = System.IO.File;
using Engine.Storage;

namespace Telegram.Bot.Echo
{
    class Program
    {
        public static string TokenUrl = "177135231:AAFXTuYQGnhy-RVzQj7539wPoUGu-sM3s_Y";
        
        static ServiceStack.Redis.RedisClient redis;
        static LocalFileStore m_localFileStore;
        static string m_fileStoreRoot = @"D:\dev\budgetBot";
        static string m_transactionLogFile = @"";

        static void Main(string[] args)
        {
            m_localFileStore = new LocalFileStore(m_fileStoreRoot);

            //var dbs = DB.GetDatabases("127.0.0.1:6379");
            //Console.WriteLine(dbs.Length);

            redis = ServiceStack.Redis.RedisClient.New();
            Run().Wait();            
        }
        
        static float budget
        {
            get
            {
                var budget = redis.Get<float>("budget");
                return budget;
            }
            set
            {
                redis.Set("budget", value);
            }        
        }

        static List<BudgetItem> s_expenses = new List<BudgetItem>();

        class BudgetItem
        {
            public DateTime Timestamp;
            public string User;
            public string Category;
            public float Cost;

            public static BudgetItem Parse(string user, string message, DateTime timeStamp)
            {
                var split = message.Trim().Split(' ');
                var splitList = split.ToList<string>();

                var costText = split[splitList.Count - 1];
                var cost = float.Parse(costText);
                splitList.RemoveAt(splitList.Count - 1);

                var itemText = message.Replace(costText, string.Empty);                
                BudgetItem item = new BudgetItem();
                item.User = user;
                item.Category = itemText;
                item.Cost = cost;
                item.Timestamp = timeStamp;
                return item;
            }
        }

        static async Task Run()
        {
            try
            {
                var Bot = new Api(TokenUrl);

                var me = await Bot.GetMe();

                Console.WriteLine("Hello my name is {0}", me.Username);

                var offset = 0;

                while (true)
                {
                    var updates = await Bot.GetUpdates(offset);

                    foreach (var update in updates)
                    {
                        try
                        {
                            var messageType = update.Message.Type;
                        }
                        catch
                        {
                            continue;
                        }
                        if (update.Message.Type == MessageType.TextMessage)
                        {
                            await Bot.SendChatAction(update.Message.Chat.Id, ChatAction.Typing);
                            await Task.Delay(2000);

                            var splitMessage = update.Message.Text.Trim().Split(' ');

                            if (splitMessage.Length != 0 && splitMessage[0] == "budget")
                            {
                                try
                                {
                                    float newBudget = float.Parse(splitMessage[1]);
                                    budget = newBudget;
                                    //redis.Set("budget", newBudget);
                                }
                                catch (Exception e)
                                {
                                    Exception ex = e;
                                }
                            }
                            else if (splitMessage.Length != 0 && splitMessage[0] == "list")
                            {
                                var filterUser = splitMessage.Length == 2 ? splitMessage[1].Trim() : string.Empty;

                                var outputString = new System.Text.StringBuilder();
                                if (s_expenses.Count == 0)
                                {
                                    var tEmpty = await Bot.SendTextMessage(update.Message.Chat.Id, "No expenses recorded.");
                                }
                                else
                                {
                                    foreach (var item in s_expenses)
                                    {
                                        if (string.IsNullOrEmpty(filterUser) || item.User == filterUser)
                                        {
                                            var reply = string.Format("{0}: £{1} - ({2})", item.Timestamp.ToShortDateString(), item.Cost, item.Category);
                                            outputString.AppendLine(reply);
                                        }
                                    }

                                    var t = await Bot.SendTextMessage(update.Message.Chat.Id, outputString.ToString());
                                }
                            }
                            else
                            {
                                try
                                {
                                    var item = BudgetItem.Parse(update.Message.From.FirstName, update.Message.Text, DateTime.Now);
                                    s_expenses.Add(item);

                                    budget -= item.Cost;

                                    var reply = string.Format("Budget: £{0}", budget);
                                    var t = await Bot.SendTextMessage(update.Message.Chat.Id, reply);
                                    Console.WriteLine("Echo Message: {0}", update.Message.Text);
                                }
                                catch
                                {
                                    var failResponse = await Bot.SendTextMessage(update.Message.Chat.Id, "Bad entry.  Try again.");
                                    Console.WriteLine("Echo Message: {0}", failResponse);
                                }
                            }
                        }

                        if (update.Message.Type == MessageType.PhotoMessage)
                        {
                            var file = await Bot.GetFile(update.Message.Photo.LastOrDefault()?.FileId);

                            Console.WriteLine("Received Photo: {0}", file.FilePath);

                            var filename = file.FileId + "." + file.FilePath.Split('.').Last();

                            using (var profileImageStream = File.Open(filename, FileMode.Create))
                            {
                                await file.FileStream.CopyToAsync(profileImageStream);
                            }
                        }

                        offset = update.Id + 1;
                    }

                    await Task.Delay(1000);
                }
            }            
            catch(Exception e)
            {
                var exception = e;
            }

        }
    }
}
