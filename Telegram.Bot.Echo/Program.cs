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
    public class Program
    {
        public static string TokenUrl = "177135231:AAFXTuYQGnhy-RVzQj7539wPoUGu-sM3s_Y";
        
        static void Main(string[] args)
        {
            Program newProgram = new Program();
            newProgram.Start();
        }
        
        public void Start()
        {
            Run().Wait();
        }

        public Program()
        {
        }
        
        async Task Run()
        {
            try
            {
                var Bot = new Api(TokenUrl);
                Service = new BudgetBotService();
                Service.OnMessage += Service_OnMessage;
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

                            if (update.Message.Type == MessageType.TextMessage)
                            {
                                //await Bot.SendChatAction(update.Message.Chat.Id, ChatAction.Typing);
                                //await Task.Delay(2000);

                                Service.ProcessCommand(update.Message.Text.Trim(), update.Message.Date);

                                while (m_cachedOutputs.Count > 0)
                                {
                                    var cachedMessage = m_cachedOutputs.Dequeue();
                                    if (cachedMessage.StartsWith("image://"))
                                    {
                                        var imageUrl = cachedMessage.Replace("image://", "");
                                        //var fileStream = new FileStream(imageUrl, FileMode.Open);
                                        

                                        using (var stream = File.Open(imageUrl, FileMode.Open))
                                        {
                                            var fileToSend = new FileToSend(imageUrl, stream);
                                            ThrowIt();
                                            var rep = await Bot.SendPhoto(update.Message.Chat.Id, fileToSend, "category");
                                            //var rep = await Bot.SendPhoto(update.Message.Chat.Id, fileToSend);
                                            //var rep = await Bot.SendPhoto(Convert.ToInt32(item), stream, txtMessage.Text);
                                        }

                                        int bpp = 3;
                                        //int timeout = 300;
                                        //var task = Bot.SendPhoto(update.Message.Chat.Id, fileToSend).Result;                                        
                                        //if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                                        //{
                                        //    // task completed within timeout
                                        //    fileStream.Close();
                                        //}
                                        //else
                                        //{
                                        //    // timeout logic
                                        //    fileStream.Close();
                                        //}
                                    }
                                    else if(!string.IsNullOrWhiteSpace(cachedMessage))
                                    {
                                        Bot.SendTextMessage(update.Message.Chat.Id, cachedMessage);
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
                        }
                        catch
                        {
                            continue;
                        }
                        offset = update.Id + 1;
                    }
                }
            }            
            catch(Exception e)
            {
                var exception = e;
            }

        }

        async Task ThrowIt()
        {
            Task.Delay(TimeSpan.FromSeconds(1.5)).Wait();
            throw new Exception();
        }

        private void Service_OnMessage(string msg)
        {
            m_cachedOutputs.Enqueue(msg);
        }

        private BudgetBotService Service;
        private Queue<string> m_cachedOutputs = new Queue<string>();
    }
}
