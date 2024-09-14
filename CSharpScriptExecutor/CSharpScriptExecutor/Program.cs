using System.Net.Http.Json;

//библиотека с описанием классов(в даном случае одного)
using CSharpScriptExecutor.Clases;

//библиотеки для работы с ботом в дискорде
using Discord;
using Discord.WebSocket;

//библиотеки для работы с общим документом с данными
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

using Newtonsoft.Json;
class Program
{
    //объявление переменных для работы с ботом дискорда
    private static DiscordSocketClient? _client;
    private static Dictionary<ulong, MessageData>? messageDataMap;
    private static ulong messageId = 0;

    //объявление переменных для работы с Excel Online
    private static SheetsService? sheetsService;
    private static string spreadsheetId = "1UESfNI8ZP0b_seP483qAAwfvEgyi_HBh4Qdeg4wqKWU";
    

    static async Task Main()
    {
        try 
        {
            //получаем данные по созданным ботом сообщениям
            messageDataMap = LoadData();

            //если messageDataMap - null - приравняем к new Dictionary<ulong, MessageData>() для избежания неопределённости null
            messageDataMap ??= [];

            //создаём соединение с ботом дискорда
            _client = new DiscordSocketClient();
            _client.Log += Log;

            await _client.LoginAsync(TokenType.Bot, "MTE4OTU5MDY3MTI3MTY2NTc1Nw.G4PKAa.052HkCQu_HSvPu_8SNYmRJklSQVEDBpZnepzWc");
            await _client.StartAsync();

            //это часть попытки интеграции ИИ CHATGPT. Неудачно из-за ограниченности знаний относительно работы с исскуственным интеллектом
            _client.MessageReceived += HandleMessage;


            _client.Ready += async () =>
            {
                Console.WriteLine("Bot is connected and ready.");

                //данные для соединения с Excel
                string credentialsPath = "minecraftserverproject-407408-cdc59b23c3ce.json";
                string[] sheetNames = { "Mods(рассмотрение)" };
                GoogleCredential credential;

                //соединяемся
                using (var stream = new System.IO.FileStream(credentialsPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(SheetsService.Scope.Spreadsheets);
                }
                sheetsService = new SheetsService(new Google.Apis.Services.BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "YourAppName"
                });

                //на данный момент сервер по майнкрафту, для чего и создавался данный код, заброшен. Изначально планировалось развивать код дальше для возможности удаления модов.
                //Из-за этого всё выполнено для возможных дополнительных таблиц с данными
                foreach (var sheetName in sheetNames)
                {
                    //Переменная для получения данных из области
                    string range = $"{sheetName}!B2:R11";
                    try
                    {
                        //пытаемся выполнить запрос на получение данных из области
                        SpreadsheetsResource.ValuesResource.GetRequest request =
                            sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);

                        ValueRange response = request.Execute();

                        //если данные есть впринципе, то обрабатываем полученные данные для более удобной работы
                        if (response.Values != null && response.Values.Count > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"Data from the sheet {sheetName}:");

                            await CheckAndUpdateMessages(sheetName, response);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"No data on the sheet {sheetName}.");
                        }

                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error querying data from the sheet {sheetName}: {ex.Message}");
                        Console.ResetColor();
                    }
                }
            };
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Ready handler: {ex}");
        }
    }
    private static async Task HandleMessage(SocketMessage message)
    {
        //проверяем, что это писал не сам бот, сообщение находиться в нужном канале и то, что это написал пользователь
        if (message.Author.IsBot || !(message is IUserMessage userMessage) || userMessage.Channel.Id != 1191707523548450836)
            return;

        //обрабатываем сообщение
        string userText = userMessage.Content;
        string botResponse = await GetBotResponse(userText);

        //ответ бота после api запроса
        await userMessage.Channel.SendMessageAsync(botResponse);
    }

    private static async Task CheckAndUpdateMessages(string sheetName, ValueRange response)
    {
        //R - right
        string statusColumn = "R";

        //Проверяем правильность ссылок
        for (int i = 1; i < response.Values.Count; i++)
        {
            var row = response.Values[i];
            string? linkColumnValue = row.ElementAtOrDefault(1)?.ToString();
            Uri uriResult;

            //проверяем правильность ссылки по общим правилам
            bool isLinkValid = Uri.TryCreate(linkColumnValue, UriKind.Absolute, out uriResult) &&
                               (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            //проверяем соответсвие требуемуму сайту(curseforge)
            bool isCurseForgeLink = isLinkValid && uriResult != null && uriResult.Host == "www.curseforge.com";

            string? modName = null;

            //если всё нормально - обрабатываем
            if (isCurseForgeLink)
            {
                if (!string.IsNullOrEmpty(linkColumnValue))
                {
                    try
                    {
                        Uri modUri = new(linkColumnValue);

                        //проверяем, что есть хотя бы один сегмент
                        if (modUri.Segments.Length > 0)
                        {
                            string[] pathSegments = modUri.Segments.Last().TrimEnd('/').Split('-');

                            for (int j = 0; j < pathSegments.Length; j++)
                            {
                                if (!string.IsNullOrEmpty(pathSegments[j]))
                                {
                                    //проверяем, что сегмент не пуст, прежде чем изменять его
                                    pathSegments[j] = char.ToUpper(pathSegments[j][0]) + pathSegments[j].Substring(1);
                                }
                            }

                            modName = string.Join(" ", pathSegments);
                        }
                    }
                    catch (UriFormatException)
                    {
                        Console.WriteLine("Invalid URL format");
                    }
                }
                else
                {
                    Console.WriteLine("Link value is null or empty");
                }
            }

            //переменная для информирования пользователей, что ссылка принята(Send), ничего нет в поле(Nothing) или при вводе есть ошибка(Invalid link (only curseforge.com please))
            string status = !string.IsNullOrEmpty(row.ElementAtOrDefault(0)?.ToString()) &&
                             !string.IsNullOrEmpty(row.ElementAtOrDefault(1)?.ToString()) ? "Send" : "Nothing";
            if(string.IsNullOrEmpty(row.ElementAtOrDefault(1)?.ToString()) && !string.IsNullOrEmpty(row.ElementAtOrDefault(2)?.ToString())) status = "Invalid link (only curseforge.com please)";
            string? statusReal = row.ElementAtOrDefault(16)?.ToString();

            //если всё правильно
            if (status == "Send" && (isCurseForgeLink || !string.IsNullOrEmpty(modName)))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("All OK!");

                if (int.TryParse(row.ElementAtOrDefault(0)?.ToString(), out int id))
                {
                    //пытаемся получить айди для сообщения в дискорде
                    var messageIdResult = await GetMessageIdByDocID(id);

                    //если есть, то работаем с ним
                    if (messageIdResult.HasValue && messageDataMap != null)
                    {
                        Console.WriteLine("Exist");
                        messageId = messageIdResult.Value;
                        //пытаемся получить данные сообщения из словаря messageDataMap по ключу messageId
                        messageDataMap.TryGetValue(messageId, out var messageData);

                        //для проверки
                        //if (messageData != null) Console.WriteLine(messageData.LinkColumnValue + " =? " + linkColumnValue);

                        //если данные сообщения не равны null и сообщение из памяти совпадает с сообщением из документа excel
                        if (messageData != null && messageData.LinkColumnValue == linkColumnValue)
                        {
                            if (messageData.Info != row.ElementAtOrDefault(2)?.ToString())
                            {
                                Console.WriteLine("Updating...");
                                //обновляем это сообщение в дискорде через бота
                                await UpdateExistingMessage(linkColumnValue, modName, row.ElementAtOrDefault(2)?.ToString(), status);
                                Console.WriteLine("OK!");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("NonExist");
                        Console.WriteLine("Sending...");
                        //в случае отсутсвия сообщения создаём новое и отправляем в канал
                        await SendNewMessage(id, linkColumnValue, modName, row.ElementAtOrDefault(2)?.ToString(), status);
                        Console.WriteLine("Send!");
                    }
                }
            }
            //если в статусе стоит, что ранее сообщение было написано и в месте для данных ничего нет
            else if (statusReal == "Send")
            {
                if (string.IsNullOrEmpty(linkColumnValue))
                {
                    if (int.TryParse(row.ElementAtOrDefault(0)?.ToString(), out var id))
                    {
                        if (await GetMessageIdByDocID(id) != null)
                        {
                            Console.WriteLine("Deleting...");
                            await DeleteMessage(id);
                            Console.WriteLine("Deleted!");
                        }
                        else
                        {
                            Console.WriteLine("Message not found for ID: " + id);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid ID format");
                    }
                }
                else
                {
                    Console.WriteLine("Error");
                }
            }
            //случай, если вдруг произошла ошибка в файле или на сервере.
            //Просто удалить сообщение(иначе потенциально могут появиться проблемы из-за непредсказуемости работы генерирования айди сообщений)
            else if (status == "Nothing" && string.IsNullOrEmpty(modName))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                if (int.TryParse(row.ElementAtOrDefault(0)?.ToString(), out var id))
                {
                    if (await GetMessageIdByDocID(id) != null)
                    {
                        Console.WriteLine("Deleting...");
                        await DeleteMessage(id);
                        Console.WriteLine("Deleted!");
                    }
                }
            }
            //если неверно ввели ссылку
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                status = "Invalid link (only curseforge.com please)";
            }
            //обновляем статус мода в excel относительно сервера
            await UpdateStatusInSheet(spreadsheetId, sheetName, statusColumn, i, status);

            //для проверки данных
            //Console.WriteLine($"{row.ElementAtOrDefault(0)}, {row.ElementAtOrDefault(1)}, {row.ElementAtOrDefault(2)}");

            Console.ForegroundColor = ConsoleColor.Red;
        }
    }

    private static async Task DeleteMessage(int ID)
    {
        if(messageDataMap != null) {
            //ищем сообщение на удаление в памяти
            var foundItems = messageDataMap.Where(pair => pair.Value.SheetRow == ID).ToList();

            if (foundItems.Count > 0)
            {
                var firstItem = foundItems.First();
                var messageID = firstItem.Key;

                if (_client != null)
                {
                    //подсоединяемся к каналу
                    SocketTextChannel channel = _client.GetGuild(1189588675357593741).GetTextChannel(1189588675823157249);

                    //ищем сообщение
                    var message = await channel.GetMessageAsync(messageID) as IUserMessage;
                    //если такое есть - удаляем
                    if (message != null)
                    {
                        await message.DeleteAsync();
                        Console.WriteLine("Message deleted.");
                    }
                }
                //удаляем из памяти и обновляем json
                messageDataMap.Remove(messageID);
                SaveData();
            }
        }
    }

    private static async Task<ulong?> GetMessageIdByDocID(int ID)
    {
        //ищем потенциальное сообщение в ранее полученных данных из json
        var foundItems = messageDataMap?
            .Where(pair => pair.Value != null && pair.Value.SheetRow == ID)
            .ToList() ?? [];

        //если нашли, то используем
        if (foundItems.Count > 0)
        {
            var firstItem = foundItems.First();
            var messageID = firstItem.Key;
            if (_client != null)
            {
                //бот заходит в отведённый ему для сообщений канал
                SocketTextChannel channel = _client.GetGuild(1189588675357593741).GetTextChannel(1189588675823157249);
            

                //получаем список сообщений из текстового канала и "упрощаем" его (объединяем в один список)
                var messages = await channel.GetMessagesAsync().FlattenAsync();
                ulong? targetMessageId = null;

                //проходим по каждому сообщению в списке сообщений
                foreach (var message in messages)
                {
                    //если ID сообщения совпадает с искомым ID, сохраняем ID сообщения и выходим из цикла
                    if (message.Id == messageID)
                    {
                        Console.WriteLine("Get it!");
                        targetMessageId = message.Id;
                        break;
                    }
                }
                Console.WriteLine("??? " + messageID);

                // Возвращаем ID найденного сообщения или null, если сообщение не найдено
                return targetMessageId;
            }
        }

        return null;
    }

    private static async Task UpdateExistingMessage(string linkColumnValue, string? modName, string? info, string status)
    {
        if(_client != null) {
            //подключаемся к каналу для бота
            var newModsRole = _client.GetGuild(1189588675357593741).Roles.FirstOrDefault(role => role.Name == "NewMods");
            SocketTextChannel channel = _client.GetGuild(1189588675357593741).GetTextChannel(1189588675823157249);

            //получаем сообщение из дискорда
            IUserMessage? message = await channel.GetMessageAsync(messageId) as IUserMessage;

            if(message != null && newModsRole!= null)
            {
                //обновляем данные по моду в сообщении дискорд
                await message.ModifyAsync(msg => msg.Content = $"<@&{newModsRole.Id}> \nMod - {modName} \nInfo - {info} \nMod link - {linkColumnValue} \nSent at - {DateTime.Now}");
                //сохраняем всё в messageDataMap для сохранения в памяти
                if (messageDataMap != null)
                    messageDataMap[message.Id].Info = info;
            }
        }
        SaveData();
    }

    private static async Task SendNewMessage(int ID, string? linkColumnValue, string? modName, string? info, string status)
    {
        if (_client != null)
        {
            var newModsRole = _client.GetGuild(1189588675357593741).Roles.FirstOrDefault(role => role.Name == "NewMods");

            if(newModsRole != null) { 
                //заходим в канал для отправки нового сообщения
                SocketTextChannel channel = _client.GetGuild(1189588675357593741).GetTextChannel(1189588675823157249);
                //отправляем сообщение с данными про мод через бота
                IUserMessage message = await channel.SendMessageAsync($"<@&{newModsRole.Id}> \nMod - {modName} \nInfo - {info} \nMod link - {linkColumnValue} \nSent at - {DateTime.Now}");
            
                if (linkColumnValue != null)
                {
                    //генерируем формат для хранения данных в памяти
                    MessageData newData = new()
                    {
                        LinkColumnValue = linkColumnValue,
                        Info = info,
                        SheetRow = ID
                    };
            
                    //сохраняем в messageDataMap для обновления памяти в будущем
                    if (messageDataMap != null && newData != null) {
                        messageDataMap[message.Id] = newData;

                        SaveData();

                        await UpdateStatusInSheet(spreadsheetId, "Mods(рассмотрение)", "R", newData.SheetRow, status);
                    }
                }
            }
        }
    }

    private static async Task<string> GetBotResponse(string userMessage)
    {
        try
        {
            //данные для связи с openai
            string apiKey = "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";
            string endpoint = "https://api.openai.com/v1/engines/davinci/completions";
            string prompt = "User: Hello\nChatGPT:";

            //запрос к ИИ
            using (HttpClient client = new ())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                //генерируем структуру запроса
                var requestData = new
                {
                    engine = "text-embedding-ada-002",
                    prompt,
                    temperature = 0.7,
                    max_tokens = 50
                };

                //отправляем запрос
                var response = await client.PostAsJsonAsync(endpoint, requestData);

                //если всё было верно
                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadFromJsonAsync<dynamic>();
                    string botResponse = "";
                    if (responseData != null)
                    {
                        botResponse = responseData.choices[0].text;
                    }
                    return botResponse?.Trim() ?? "Пустой ответ от бота.";
                }
                //если произошла ошибка(на данный момент всегда отправляет сюда)
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Ошибка при запросе к API OpenAI: {response.StatusCode}\n{errorContent}");
                    return $"Ошибка при запросе к API OpenAI: {response.StatusCode}";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке запроса: {ex.Message}");
            return $"Ошибка при обработке запроса: {ex.Message}";
        }
    }

    private static async Task UpdateStatusInSheet(string spreadsheetId, string sheetName, string statusColumn, int rowIndex, string status)
    {
        //переменные для обновления статуса мода в канале
        var statusUpdate = new List<object> { status };
        var updateRange = $"{sheetName}!{statusColumn}{rowIndex + 1}:{statusColumn}{rowIndex + 1}";
        var updateRequest = new ValueRange { Values = new List<IList<object>> { statusUpdate } };
        if (spreadsheetId != null && sheetsService != null)
        {
            //в excel информируем пользователя - всё ли прошло успешно при отправке на сервер Discord или есть какие-то проблемы
            var updateUpdate = sheetsService.Spreadsheets.Values.Update(updateRequest, spreadsheetId, updateRange);
        
            updateUpdate.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await updateUpdate.ExecuteAsync();
        }
    }

    private static void SaveData()
    {
        //обновляем json
        File.WriteAllText("messageDataMap.json", JsonConvert.SerializeObject(messageDataMap));
    }

    private static Dictionary<ulong, MessageData> LoadData()
    {
        try
        {
            //проверяем существование "памяти" бота. Если нет, то создаём. Если есть, то сохраняем в памяти для будущего использования
            if (File.Exists("messageDataMap.json"))
            {
                string json = File.ReadAllText("messageDataMap.json");
                var data = JsonConvert.DeserializeObject<Dictionary<ulong, MessageData>>(json);
                //messageDataMap.json может быть пуст, поэтому data ?? [] для избежания неопределённости действий в случае пустоты string json
                return data ?? [];
            }
        }
        catch (Exception ex)
        {
            // Логируем ошибку
            Console.WriteLine($"Ошибка при чтении или десериализации файла: {ex.Message}");
        }

        // Возвращаем пустой словарь, если файл не найден или произошла ошибка
        return [];
    }

    private static Task Log(LogMessage arg)
    {
        Console.WriteLine(arg);
        return Task.CompletedTask;
    }
}
