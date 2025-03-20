using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using EchoBot.Authentication;
using Azure.Identity;
using System.Collections.Concurrent;

namespace EchoBot.Bot
{
    /// <summary>
    /// Service for handling chat-related operations.
    /// </summary>
    public class ChatBotService : ActivityHandler, IChatBotService, IBot, IDisposable
    {
        private readonly GraphAuthenticationProvider _authProvider;
        private readonly AppSettings _settings;
        private readonly ILogger<ChatBotService> _logger;

        // Store conversation references for proactive messaging
        private static readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences = new();

        public ChatBotService(
            IOptions<AppSettings> settings,
            ILogger<ChatBotService> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            // Initialize GraphAuthenticationProvider
            _authProvider = new GraphAuthenticationProvider(
                _settings.AadAppId,
                _settings.AadAppSecret,
                _settings.AadAppTenantId
            );
        }

        /// <summary>
        /// Handles incoming messages and responds to the user.
        /// </summary>
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // Capture the user's message
            string userMessage = turnContext.Activity.Text ?? "unknown";
            string botReply = $"You said: {userMessage}";

            // Log the user's message
            _logger.LogInformation($"User message: {userMessage}");

            // Respond to the user
            await turnContext.SendActivityAsync(MessageFactory.Text(botReply), cancellationToken);

            // Store the conversation reference for proactive messaging
            AddConversationReference((Activity)turnContext.Activity);

            // Send two proactive messages after responding
            await Task.Delay(1000); // Optional delay for demonstration
            await SendProactiveMessageAsync("This is the first proactive message!", turnContext.Adapter, cancellationToken);
            await Task.Delay(1000); // Optional delay for demonstration
            await SendProactiveMessageAsync("This is the second proactive message!", turnContext.Adapter, cancellationToken);
        }

        /// <summary>
        /// Adds or updates a conversation reference for proactive messaging.
        /// </summary>
        public void AddConversationReference(Activity activity)
        {
            var conversationReference = activity.GetConversationReference();
            _conversationReferences.AddOrUpdate(conversationReference.Conversation.Id, conversationReference, (key, oldValue) => conversationReference);
        }

        /// <summary>
        /// Sends a proactive message to the user.
        /// </summary>
        public async Task SendProactiveMessageAsync(string message, BotAdapter adapter, CancellationToken cancellationToken)
        {
            foreach (var conversationReference in _conversationReferences.Values)
            {
                await adapter.ContinueConversationAsync(
                    _settings.AadAppId,
                    conversationReference,
                    async (turnContext, token) =>
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(message), token);
                    },
                    cancellationToken);
            }
        }

        /// <summary>
        /// Sends a message to a Teams user using Microsoft Graph.
        /// </summary>
        public async Task PostMessageToUserAsync(string userEmail, string message)
        {
            try
            {
                _logger.LogInformation($"Sending proactive message to {userEmail}");

                // Use Azure.Identity to authenticate
                var clientSecretCredential = new ClientSecretCredential(
                    _settings.AadAppTenantId,
                    _settings.AadAppId,
                    _settings.AadAppSecret
                );

                var graphClient = new GraphServiceClient(clientSecretCredential);

                var chatMessage = new ChatMessage
                {
                    Body = new ItemBody
                    {
                        Content = message
                    }
                };

                // Get the user by email
                var user = await graphClient.Users[userEmail].GetAsync();

                // Get the user's chat
                var chats = await graphClient.Users[user.Id].Chats.GetAsync();
                var generalChat = chats.Value.FirstOrDefault();

                if (generalChat == null)
                {
                    _logger.LogError($"No chat found for user {userEmail}");
                    return;
                }

                // Send the message to the user's chat
                await graphClient.Chats[generalChat.Id].Messages.PostAsync(chatMessage);

                _logger.LogInformation($"Message sent to {userEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send message to {userEmail}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Initializes the chat bot instance.
        /// </summary>
        public void Initialize()
        {
            _logger.LogInformation("Initializing Chat Bot Service");
            // Add any initialization logic here if needed
        }

        /// <summary>
        /// Shuts down the chat bot instance.
        /// </summary>
        public async Task Shutdown()
        {
            _logger.LogInformation("Shutting down Chat Bot Service");
            // Add any cleanup logic here if needed
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            // Dispose resources if needed
        }
    }
}