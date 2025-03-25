using Microsoft.AspNetCore.SignalR;

namespace EchoBot.SignalR
{
    public interface ISignalRService
    {
        void BroadcastSpeakerStarted(string speakerName, DateTime timestamp);
        void BroadcastSpeakerInfo(string speakerName, DateTime timestamp);
        void BroadcastSpeakerFinished(string speakerName, DateTime timestamp);
        void BroadcastTranscription(string speakerName, string text, DateTime timestamp);
    }

    public class SignalRService : ISignalRService
    {
        private readonly IHubContext<TranscriptionHub> _hubContext;

        public SignalRService(IHubContext<TranscriptionHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void BroadcastSpeakerStarted(string speakerName, DateTime timestamp)
        {
            _hubContext.Clients.All.SendAsync("SpeakerStarted", new
            {
                SpeakerName = speakerName,
                Timestamp = timestamp
            });
        }

        public void BroadcastSpeakerInfo(string speakerName, DateTime timestamp)
        {
            _hubContext.Clients.All.SendAsync("SpeakerInfo", new
            {
                SpeakerName = speakerName,
                Timestamp = timestamp
            });
        }

        public void BroadcastSpeakerFinished(string speakerName, DateTime timestamp)
        {
            _hubContext.Clients.All.SendAsync("SpeakerFinished", new
            {
                SpeakerName = speakerName,
                Timestamp = timestamp
            });
        }

        public void BroadcastTranscription(string speakerName, string text, DateTime timestamp)
        {
            _hubContext.Clients.All.SendAsync("Transcription", new
            {
                SpeakerName = speakerName,
                Text = text,
                Timestamp = timestamp
            });
        }
    }

    // Add TranscriptionHub class here
    public class TranscriptionHub : Hub
    {
        // You can add methods here if you want to handle client-to-server communication.
    }
}