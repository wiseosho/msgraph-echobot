// ***********************************************************************
// Assembly         : EchoBot.Services
// Author           : JasonTheDeveloper
// Created          : 09-07-2020
//
// Last Modified By : bcage29
// Last Modified On : 10-17-2023
// ***********************************************************************
// <copyright file="BotMediaStream.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>
// <summary>The bot media stream.</summary>
// ***********************************************************************-
using EchoBot.Media;
using EchoBot.Util;
using EchoBot.SignalR; // Ensure this namespace is included
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Skype.Bots.Media;
using Microsoft.Skype.Internal.Media.Services.Common;
using System.Runtime.InteropServices;

namespace EchoBot.Bot
{
    /// <summary>
    /// Class responsible for streaming audio and video.
    /// </summary>
    public class BotMediaStream : ObjectRootDisposable
    {
        private AppSettings _settings;

        /// <summary>
        /// The participants
        /// </summary>
        internal List<IParticipant> participants;

        /// <summary>
        /// The audio socket
        /// </summary>
        private readonly IAudioSocket _audioSocket;
        /// <summary>
        /// The media stream
        /// </summary>
        private readonly ILogger _logger;
        private AudioVideoFramePlayer audioVideoFramePlayer;
        private readonly TaskCompletionSource<bool> audioSendStatusActive;
        private readonly TaskCompletionSource<bool> startVideoPlayerCompleted;
        private AudioVideoFramePlayerSettings audioVideoFramePlayerSettings;
        private List<AudioMediaBuffer> audioMediaBuffers = new List<AudioMediaBuffer>();
        private int shutdown;
        private readonly SpeechService _languageService;
        private readonly ISignalRService _signalRService;
        private readonly CallHandler _callHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="BotMediaStream" /> class.
        /// </summary>
        /// <param name="mediaSession">The media session.</param>
        /// <param name="callId">The call identity</param>
        /// <param name="graphLogger">The Graph logger.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="settings">Azure settings</param>
        /// <param name="signalRService">SignalR service</param>
        /// <param name="callHandler">Call handler</param>
        /// <exception cref="InvalidOperationException">A mediaSession needs to have at least an audioSocket</exception>
        public BotMediaStream(
            ILocalMediaSession mediaSession,
            string callId,
            IGraphLogger graphLogger,
            ILogger logger,
            AppSettings settings,
            ISignalRService signalRService,
            CallHandler callHandler // Add CallHandler as a parameter
        )
            : base(graphLogger)
        {
            ArgumentVerifier.ThrowOnNullArgument(mediaSession, nameof(mediaSession));
            ArgumentVerifier.ThrowOnNullArgument(logger, nameof(logger));
            ArgumentVerifier.ThrowOnNullArgument(settings, nameof(settings));
            ArgumentVerifier.ThrowOnNullArgument(signalRService, nameof(signalRService)); // Verify the parameter

            _settings = settings;
            _logger = logger;
            _signalRService = signalRService; // Assign the parameter
            _callHandler = callHandler; // Initialize CallHandler

            this.participants = new List<IParticipant>();

            this.audioSendStatusActive = new TaskCompletionSource<bool>();
            this.startVideoPlayerCompleted = new TaskCompletionSource<bool>();

            // Subscribe to the audio media.
            this._audioSocket = mediaSession.AudioSocket;
            if (this._audioSocket == null)
            {
                throw new InvalidOperationException("A mediaSession needs to have at least an audioSocket");
            }

            var ignoreTask = this.StartAudioVideoFramePlayerAsync().ForgetAndLogExceptionAsync(this.GraphLogger, "Failed to start the player");

            this._audioSocket.AudioSendStatusChanged += OnAudioSendStatusChanged;            

            this._audioSocket.AudioMediaReceived += this.OnAudioMediaReceived;

             if (_settings.UseSpeechService)
            {
                // Pass the ISignalRService to the SpeechService constructor
                _languageService = new SpeechService(_settings, _logger, _signalRService);
                _languageService.SendMediaBuffer += this.OnSendMediaBuffer;
            }
        }

        /// <summary>
        /// Gets the participants.
        /// </summary>
        /// <returns>List&lt;IParticipant&gt;.</returns>
        public List<IParticipant> GetParticipants()
        {
            return participants;
        }

        /// <summary>
        /// Shut down.
        /// </summary>
        /// <returns><see cref="Task" />.</returns>
        public async Task ShutdownAsync()
        {
            if (Interlocked.CompareExchange(ref this.shutdown, 1, 1) == 1)
            {
                return;
            }

            await this.startVideoPlayerCompleted.Task.ConfigureAwait(false);

            // unsubscribe
            if (this._audioSocket != null)
            {
                this._audioSocket.AudioSendStatusChanged -= this.OnAudioSendStatusChanged;
            }

            // shutting down the players
            if (this.audioVideoFramePlayer != null)
            {
                await this.audioVideoFramePlayer.ShutdownAsync().ConfigureAwait(false);
            }

            // make sure all the audio and video buffers are disposed, it can happen that,
            // the buffers were not enqueued but the call was disposed if the caller hangs up quickly
            foreach (var audioMediaBuffer in this.audioMediaBuffers)
            {
                audioMediaBuffer.Dispose();
            }

            _logger.LogInformation($"disposed {this.audioMediaBuffers.Count} audioMediaBUffers.");

            this.audioMediaBuffers.Clear();
        }

        /// <summary>
        /// Initialize AV frame player.
        /// </summary>
        /// <returns>Task denoting creation of the player with initial frames enqueued.</returns>
        private async Task StartAudioVideoFramePlayerAsync()
        {
            try
            {
                _logger.LogInformation("Send status active for audio and video Creating the audio video player");
                this.audioVideoFramePlayerSettings =
                    new AudioVideoFramePlayerSettings(new AudioSettings(20), new VideoSettings(), 1000);
                this.audioVideoFramePlayer = new AudioVideoFramePlayer(
                    (AudioSocket)_audioSocket,
                    null,
                    this.audioVideoFramePlayerSettings);

                _logger.LogInformation("created the audio video player");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create the audioVideoFramePlayer with exception");
            }
            finally
            {
                this.startVideoPlayerCompleted.TrySetResult(true);
            }
        }

        /// <summary>
        /// Callback for informational updates from the media plaform about audio status changes.
        /// Once the status becomes active, audio can be loopbacked.
        /// </summary>
        /// <param name="sender">The audio socket.</param>
        /// <param name="e">Event arguments.</param>
        private void OnAudioSendStatusChanged(object? sender, AudioSendStatusChangedEventArgs e)
        {
            _logger.LogTrace($"[AudioSendStatusChangedEventArgs(MediaSendStatus={e.MediaSendStatus})]");

            if (e.MediaSendStatus == MediaSendStatus.Active)
            {
                this.audioSendStatusActive.TrySetResult(true);
            }
        }

        /// <summary>
        /// Receive audio from subscribed participant.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The audio media received arguments.</param>
        private async void OnAudioMediaReceived(object? sender, AudioMediaReceivedEventArgs e)
        {
            if (e.Buffer == null)
            {
                _logger.LogWarning("AudioMediaReceivedEventArgs.Buffer is null. Skipping processing.");
                return;
            }

            // Check if UnmixedAudioBuffers is null or empty
            if (e.Buffer.UnmixedAudioBuffers == null || !e.Buffer.UnmixedAudioBuffers.Any())
            {
                _logger.LogWarning("UnmixedAudioBuffers is null or empty. Unable to determine the speakers.");
                return;
            }

            // Process each UnmixedAudioBuffer concurrently
            var tasks = e.Buffer.UnmixedAudioBuffers.Select(async unmixedAudioBuffer =>
            {
                if (unmixedAudioBuffer.Equals(default(UnmixedAudioBuffer)))
                {
                    _logger.LogWarning("UnmixedAudioBuffer is default. Skipping this buffer.");
                    return;
                }

                // Retrieve the ActiveSpeakerId from the UnmixedAudioBuffer
                var activeSpeakerId = unmixedAudioBuffer.ActiveSpeakerId;

                if (activeSpeakerId == null)
                {
                    _logger.LogWarning("ActiveSpeakerId is null. Skipping this buffer.");
                    return;
                }

                // Map ActiveSpeakerId to the speaker name using CallHandler
                //var speakerName = _callHandler.GetSpeakerName(activeSpeakerId.ToString());
                var speakerName = activeSpeakerId.ToString();

                try
                {
                    if (!startVideoPlayerCompleted.Task.IsCompleted) { return; }

                    if (_languageService != null)
                    {
                        // Pass the speaker name and buffer to the SpeechService
                        await _languageService.AppendAudioBuffer(unmixedAudioBuffer, speakerName);
                    }
                    else
                    {
                        // Handle audio loopback if SpeechService is not enabled
                        var length = unmixedAudioBuffer.Length;
                        if (length > 0)
                        {
                            var buffer = new byte[length];
                            Marshal.Copy(unmixedAudioBuffer.Data, buffer, 0, (int)length);

                            var currentTick = DateTime.Now.Ticks;
                            this.audioMediaBuffers = Util.Utilities.CreateAudioMediaBuffers(buffer, currentTick, _logger);
                            await this.audioVideoFramePlayer.EnqueueBuffersAsync(this.audioMediaBuffers, new List<VideoMediaBuffer>());
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.GraphLogger.Error(ex);
                    _logger.LogError(ex, "Error processing UnmixedAudioBuffer.");
                }
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);
        }

        private void OnSendMediaBuffer(object? sender, Media.MediaStreamEventArgs e)
        {
            this.audioMediaBuffers = e.AudioMediaBuffers;
            var result = Task.Run(async () => await this.audioVideoFramePlayer.EnqueueBuffersAsync(this.audioMediaBuffers, new List<VideoMediaBuffer>())).GetAwaiter();
        }
    }
}

