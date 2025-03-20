using System.Threading.Tasks;
using EchoBot.Models;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Client;
using System.Collections.Concurrent;
using Microsoft.Bot.Schema;

namespace EchoBot.Bot
{
    /// <summary>
    /// Interface for ChatBotService.
    /// </summary>
    public interface IChatBotService
    {
        /// <summary>
        /// Sends a proactive message to a Teams user.
        /// </summary>
        /// <param name="userEmail">The email of the user to send the message to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PostMessageToUserAsync(string userEmail, string message);
        //Task SendMessageToUserAsync(Activity incomingActivity, string message);

        /// <summary>
        /// Initializes the chat bot instance.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shuts down the chat bot instance.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Shutdown();
    }
}