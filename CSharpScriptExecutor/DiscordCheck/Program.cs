using Discord;
using Discord.WebSocket;
class DiscordCheck
{
    private static DiscordSocketClient _client;

    static async Task Main()
    {
        _client = new DiscordSocketClient();
        _client.Log += Log;

        await _client.LoginAsync(TokenType.Bot, "MTE4OTU5MDY3MTI3MTY2NTc1Nw.G4PKAa.052HkCQu_HSvPu_8SNYmRJklSQVEDBpZnepzWc");
        await _client.StartAsync();

        _client.Ready += async () =>
        {
            Console.WriteLine("Бот подключен и готов. Введите команду:");

            while (true)
            {
                Console.Write("Команда (add/edit/delete/exit): ");
                string command = Console.ReadLine().ToLower();

                switch (command)
                {
                    case "add":
                        await ExecuteAddCommand();
                        break;
                    case "edit":
                        await ExecuteEditCommand();
                        break;
                    case "list":
                        await ExecuteListCommand();
                        break;
                    case "delete":
                        await ExecuteDeleteCommand();
                        break;
                    case "exit":
                        return;
                    default:
                        Console.WriteLine("Неверная команда. Пожалуйста, введите add, edit, delete или exit.");
                        break;
                }
            }
        };

        await Task.Delay(-1);
    }

    private static async Task ExecuteAddCommand()
    {
        Console.Write("Введите текст сообщения: ");
        string messageText = Console.ReadLine();
        await SendMessage(messageText);
        Console.WriteLine("Сообщение успешно добавлено.");
    }

    private static async Task ExecuteEditCommand()
    {
        Console.Write("Введите ID сообщения для редактирования: ");
        if (ulong.TryParse(Console.ReadLine(), out ulong messageId))
        {
            Console.Write("Введите новый текст: ");
            string newText = Console.ReadLine();

            await EditMessage(messageId, newText);
            Console.WriteLine("Сообщение успешно отредактировано.");
        }
        else
        {
            Console.WriteLine("Неверный ID сообщения.");
        }
    }

    private static async Task ExecuteListCommand()
    {
        SocketTextChannel channel = _client.GetGuild(1189588675357593741).GetTextChannel(1189588675823157249);
        var messages = await channel.GetMessagesAsync().FlattenAsync();

        Console.WriteLine("Список сообщений:");

        foreach (var message in messages)
        {
            Console.WriteLine($"ID: {message.Id}, Текст: {message.Content}");
        }
    }


    private static async Task ExecuteDeleteCommand()
    {
        Console.Write("Введите ID сообщения для удаления: ");
        if (ulong.TryParse(Console.ReadLine(), out ulong messageId))
        {
            await DeleteMessage(messageId);
            Console.WriteLine("Сообщение успешно удалено.");
        }
        else
        {
            Console.WriteLine("Неверный ID сообщения.");
        }
    }

    private static async Task SendMessage(string text)
    {
        SocketTextChannel channel = _client.GetGuild(1189588675357593741).GetTextChannel(1189588675823157249);
        await channel.SendMessageAsync(text);
    }

    private static async Task EditMessage(ulong messageId, string newText)
    {
        SocketTextChannel channel = _client.GetGuild(1189588675357593741).GetTextChannel(1189588675823157249);
        IUserMessage originalMessage = await channel.GetMessageAsync(messageId) as IUserMessage;

        if (originalMessage != null)
        {
            await originalMessage.ModifyAsync(msg => msg.Content = newText);
        }
        else
        {
            Console.WriteLine($"Сообщение {messageId} не найдено.");
        }
    }

    private static async Task DeleteMessage(ulong messageId)
    {
        SocketTextChannel channel = _client.GetGuild(1189588675357593741).GetTextChannel(1189588675823157249);
        IUserMessage originalMessage = await channel.GetMessageAsync(messageId) as IUserMessage;

        if (originalMessage != null)
        {
            await originalMessage.DeleteAsync();
        }
        else
        {
            Console.WriteLine($"Сообщение {messageId} не найдено.");
        }
    }

    private static Task Log(LogMessage arg)
    {
        Console.WriteLine(arg);
        return Task.CompletedTask;
    }
}
