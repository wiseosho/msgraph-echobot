using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using System.Threading.Tasks;

namespace EchoBot.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly ILogger<MessagesController> _logger;
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly IBot _bot;

        public MessagesController(
            ILogger<MessagesController> logger,
            IBotFrameworkHttpAdapter adapter,
            IBot bot)
        {
            _logger = logger;
            _adapter = adapter;
            _bot = bot;
        }

        [HttpPost]
        public async Task PostAsync()
        {
            // Use the Bot Framework adapter to process the incoming request
            await _adapter.ProcessAsync(Request, Response, _bot);
        }
    }
}
