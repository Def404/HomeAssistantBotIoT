using System.Net;
using System.Net.Http.Headers;
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

		_receiverOptions = new ReceiverOptions{
			AllowedUpdates = new[]{
				UpdateType.Message,
				UpdateType.CallbackQuery,
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

										keyboardButtonsList.Add(new InlineKeyboardButton[]{
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

							if (message.Text == "/menu")
							{
								WebAppInfo webAppInfo = new WebAppInfo(){
									Url = "https://192.168.1.6:5001/telegram"
								};

								var inlineKeyboard = new InlineKeyboardMarkup(
									new List<InlineKeyboardButton[]>(){
										new InlineKeyboardButton[]{
											InlineKeyboardButton.WithWebApp("Это кнопка с сайтом", webAppInfo),
										}
									});


								await botClient.SendTextMessageAsync(
									chat.Id,
									"Меню",
									replyMarkup: inlineKeyboard);

								return;
							}

							return;
						}
						case MessageType.Voice:
						{
							var a = message.Voice;

							using (FileStream fstream = new FileStream($"{a.FileId}.{a.MimeType.Split("/")[1]}",
								       FileMode.OpenOrCreate))
							{
								var file_info = await botClient.GetInfoAndDownloadFileAsync(a.FileId, fstream);
							}

							var FOLDER_ID = "";

							var IAM_TOKEN =
								"";

							using (FileStream fileStream = new FileStream($"{a.FileId}.{a.MimeType.Split("/")[1]}",
								       FileMode.OpenOrCreate))
							{

								var param = $"topic=general&folderId={FOLDER_ID}&lang=ru-RU";

								HttpClient client = new HttpClient();
								//client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IAM_TOKEN);
								client.DefaultRequestHeaders.Add("Authorization", $"Bearer {IAM_TOKEN}");
								HttpContent content = new StreamContent(fileStream);

								var response =
									await client.PostAsync(
										$"https://stt.api.cloud.yandex.net/speech/v1/stt:recognize?{param}", content);

								var jsonResponse = await response.Content.ReadAsStringAsync();
								Console.WriteLine($"{jsonResponse}\n");

								var audioTest = JsonSerializer.Deserialize<AudioMessageResult>(jsonResponse);
								string? deviceName = null;
								int? value = null;

								if (audioTest == null)
									return;

								if (audioTest.result.ToLower().Contains("включи") ||
								    audioTest.result.ToLower().Contains("включить"))
									value = 1;

								if (audioTest.result.ToLower().Contains("выключи") ||
								    audioTest.result.ToLower().Contains("выключить"))
									value = 0;

								if (audioTest.result.ToLower().Contains("устройство 1"))
									deviceName = "device1";

								if (audioTest.result.ToLower().Contains("устройство 2"))
									deviceName = "device2";

								if (audioTest.result.ToLower().Contains("устройство 3"))
									deviceName = "device3";

								if (value == null || deviceName == null)
								{
									await botClient.SendTextMessageAsync(
										chat.Id,
										$"Ошибка распознания голосового сообщения",
										replyToMessageId: message.MessageId);

									return;
								}

								HttpClientHandler clientHandler = new HttpClientHandler();

								clientHandler.ServerCertificateCustomValidationCallback =
									(sender, cert, chain, sslPolicyErrors) => { return true; };

								HttpClient client2 = new HttpClient(clientHandler);

								var response2 = await client2.PostAsync(
									$"https://192.168.1.3:44323/Heads/PostChangeStatus?deviceName={deviceName}&value={value}",
									null);

								string valueTxt = "";

								if (value == 1)
									valueTxt = "включили";
								else if (value == 0)
									valueTxt = "выключили";

								await botClient.SendTextMessageAsync(
									chat.Id,
									$"Вы {valueTxt} устройство {deviceName}",
									replyToMessageId: message.MessageId);

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

								keyboardButtonsList.Add(new InlineKeyboardButton[]{
									InlineKeyboardButton.WithCallbackData($"Выключить", data)
								});
							}
							else if (deviceStatus.status == Status.STOP)
							{
								var data = $"run {deviceStatus.deviceName}";

								keyboardButtonsList.Add(new InlineKeyboardButton[]{
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
						var deviceName = callbackQuery.Data.Split(" ")[1];
						HttpClientHandler clientHandler = new HttpClientHandler();

						clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
						{
							return true;
						};

						HttpClient client = new HttpClient(clientHandler);

						var response = await client.PostAsync(
							$"https://192.168.1.3:44323/Heads/PostChangeStatus?deviceName={deviceName}&value={1}", null);

						await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

						await botClient.SendTextMessageAsync(
							chat.Id,
							$"Вы включили устройство {deviceName}");

						return;
					}
					else if (callbackQuery.Data.Split(" ")[0].Equals("stop"))
					{
						var deviceName = callbackQuery.Data.Split(" ")[1];
						HttpClientHandler clientHandler = new HttpClientHandler();

						clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
						{
							return true;
						};

						HttpClient client = new HttpClient(clientHandler);

						var response = await client.PostAsync(
							$"https://192.168.1.3:44323/Heads/PostChangeStatus?deviceName={deviceName}&value={0}", null);

						await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

						await botClient.SendTextMessageAsync(
							chat.Id,
							$"Вы выключили устройство {deviceName}");

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
		var ErrorMessage = error switch{
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

	private class AudioMessageResult
	{
		public string result { get; set; }
	}
}