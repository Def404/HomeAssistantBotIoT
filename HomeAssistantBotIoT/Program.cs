using System.Net;
using System.Text.Json;
using ExecutingDevice;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

internal class Program
{
    private static ITelegramBotClient _botClient;
    private static ReceiverOptions _receiverOptions;

    private const string TOKEN = "";

    public static async Task Main()
    {
        _botClient = new TelegramBotClient(TOKEN);
        _receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message,
                UpdateType.CallbackQuery
            },
            ThrowPendingUpdates = true,
        };

        using var cts = new CancellationTokenSource();

        _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);

        var bot = await _botClient.GetMeAsync();
        Console.WriteLine($"{bot.FirstName} запущен!");

        await Task.Delay(-1);
    }

    private static async Task UpdateHandler(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                {
                    var message = update.Message;
                    var user = message.From;
                    Console.WriteLine($"{user.FirstName} ({user.Id}) написал сообщение: {message.Text}");
                    var chat = message.Chat;

                    switch (message.Type)
                    {
                        case MessageType.Text:
                        {
                            if (message.Text == "/run")
                            {
                                List<InlineKeyboardButton[]> keyboardButtonsList = new List<InlineKeyboardButton[]>();

                                HttpClientHandler clientHandler = new HttpClientHandler();
                                clientHandler.ServerCertificateCustomValidationCallback =
                                    (sender, cert, chain, sslPolicyErrors) => { return true; };

                                HttpClient client = new HttpClient(clientHandler);
                                var response =
                                    await client.GetAsync($"https://192.168.1.3:44323/Heads/GetAllDeviceStatus");

                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    var content = await response.Content.ReadAsStringAsync();

                                    var devices = JsonSerializer.Deserialize<AllDevices>(content);
                                    foreach (var device in devices.resultAllDevices)
                                    {
                                        string statusSticker;
                                        if (device.status == Status.RUN)
                                            statusSticker = "✅";
                                        else if (device.status == Status.STOP)
                                            statusSticker = "❌";
                                        else
                                            statusSticker = "";

                                        keyboardButtonsList.Add(new InlineKeyboardButton[]
                                        {
                                            InlineKeyboardButton.WithCallbackData(
                                                $"{device.deviceName} {statusSticker}", device.deviceName)
                                        });
                                    }
                                }

                                var inlineKeyboard = new InlineKeyboardMarkup(keyboardButtonsList);
                                await botClient.SendTextMessageAsync(
                                    chat.Id,
                                    "Выберите устройство",
                                    replyMarkup: inlineKeyboard);

                                return;
                            }

                            if (message.Text == "/status")
                            {
                                string resultText = $"*Устройства:*\n\n"; 

                                HttpClientHandler clientHandler = new HttpClientHandler();
                                clientHandler.ServerCertificateCustomValidationCallback =
                                    (sender, cert, chain, sslPolicyErrors) => { return true; };

                                HttpClient client = new HttpClient(clientHandler);
                                var response =
                                    await client.GetAsync($"https://192.168.1.3:44323/Heads/GetAllDeviceStatus");

                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    var content = await response.Content.ReadAsStringAsync();

                                    var devices = JsonSerializer.Deserialize<AllDevices>(content);
                                    foreach (var device in devices.resultAllDevices)
                                    {
                                        string statusSticker;
                                        if (device.status == Status.RUN)
                                            statusSticker = "✅";
                                        else if (device.status == Status.STOP)
                                            statusSticker = "❌";
                                        else
                                            statusSticker = "";

                                        resultText += $"{device.deviceName} {statusSticker} \n";
                                    }
                                }
                                
                                await botClient.SendTextMessageAsync(
                                    chat.Id,
                                    resultText,
                                    parseMode: ParseMode.MarkdownV2);

                                return;
                            }

                            return;
                        }
                    }

                    return;
                }
                case UpdateType.CallbackQuery:
                {
                    var callbackQuery = update.CallbackQuery; // btn 
                    var user = callbackQuery.From;
                    Console.WriteLine($"{user.FirstName} ({user.Id}) нажал на кнопку: {callbackQuery.Data}");

                    var chat = callbackQuery.Message.Chat;


                    if (callbackQuery.Data.Contains("device") && callbackQuery.Data.Split().Length <= 1)
                    {
                        
                        HttpClientHandler clientHandler = new HttpClientHandler();
                        clientHandler.ServerCertificateCustomValidationCallback =
                            (sender, cert, chain, sslPolicyErrors) => { return true; };

                        HttpClient client = new HttpClient(clientHandler);
                        var response =
                            await client.GetAsync(
                                $"https://192.168.1.3:44323/Heads/GetStatus?deviceName={callbackQuery.Data}");

                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var deviceStatus = JsonSerializer.Deserialize<DeviceStatus>(content);

                            List<InlineKeyboardButton[]> keyboardButtonsList = new List<InlineKeyboardButton[]>();

                            if (deviceStatus.status == Status.RUN)
                            {
                                var data = $"stop {deviceStatus.deviceName}";
                                keyboardButtonsList.Add(new InlineKeyboardButton[]
                                {
                                    InlineKeyboardButton.WithCallbackData($"Выключить", data)
                                });
                            }
                            else if (deviceStatus.status == Status.STOP)
                            {
                                var data = $"run {deviceStatus.deviceName}";
                                keyboardButtonsList.Add(new InlineKeyboardButton[]
                                {
                                    InlineKeyboardButton.WithCallbackData($"Включить", data)
                                });
                            }


                            var inlineKeyboard = new InlineKeyboardMarkup(keyboardButtonsList);
                            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

                            await botClient.SendTextMessageAsync(
                                chat.Id,
                                $"Устройсво: {deviceStatus.deviceName}",
                                replyMarkup: inlineKeyboard);

                            return;
                        }
                    }
                    else if (callbackQuery.Data.Split(" ")[0].Equals("run"))
                    {
                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

                        await botClient.SendTextMessageAsync(
                            chat.Id,
                            $"Вы нажали на {callbackQuery.Data}");
                        return;
                    }
                    else if (callbackQuery.Data.Split(" ")[0].Equals("stop"))
                    {
                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

                        await botClient.SendTextMessageAsync(
                            chat.Id,
                            $"Вы нажали на {callbackQuery.Data}");
                        return;
                    }

                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
    {
        var ErrorMessage = error switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => error.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    private class AllDevices
    {
        public List<DeviceStatus> resultAllDevices { get; set; }
    }

    private class DeviceStatus
    {
        public string deviceName { get; set; }
        public string status { get; set; }
    }
}