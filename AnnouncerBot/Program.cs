using Discord;
using Discord.Audio;
using Discord.Commands;
using NAudio.Wave;
using System.Threading.Tasks;
using System.Speech.Synthesis;
using System.IO;
using System;

namespace AnnouncerBot
{
    class Program
    {
        // This is the "audio client" we'll use it later.
        public static IAudioClient _vClient;

        // Your bots Token.  Change this and the Server/Channel IDs in order to connect.
        private static string botToken = "YOUR BOT KEY HERE";

        // Server and Channel hardcoded IDs
        private static ulong serverID = 000000000000000000;
        private static ulong channelID = 000000000000000000;

        // The discord client.
        public static DiscordClient _client;

        // Text to Speech
        private static SpeechSynthesizer reader;

        static void Main(string[] args)
        {
            Start();
        }
        public static void Start()
        {
            _client = new DiscordClient();

            // Setup Discord Commands
            _client.UsingCommands(x =>
            {
                x.PrefixChar = '!';
                x.HelpMode = HelpMode.Public;
            });

            // Discord.Audio stuff.  Got to set the mode to outgoing.
            _client.UsingAudio(x =>
            {
                x.Mode = AudioMode.Outgoing;
            });

            // Variable to make creating commands look a little more clean.
            // Currently not used
            var cmds = _client.GetService<CommandService>();

            // Connect to the voice channel when available
            _client.ServerAvailable += async (s, e) =>
            {
                var voiceChannel = _client.GetServer(serverID).GetChannel(channelID);
                _vClient = await _client.GetService<AudioService>().Join(voiceChannel);
                Console.WriteLine("Joined server");
            };


            // Catch-all for user update events
            _client.UserUpdated += async (s, e) =>
            {
                bool joined;
                Channel voiceChan = e.Server.GetChannel(channelID);

                // User Joined
                if (e.After.VoiceChannel != null && e.Before.VoiceChannel == null)
                {
                    Console.WriteLine(e.After.Name.ToString() + " has joined the channel.");
                    joined = true;
                    voiceChan = e.After.VoiceChannel;
                }
                // User Left
                else if (e.Before.VoiceChannel != null && e.After.VoiceChannel == null)
                {
                    Console.WriteLine(e.After.Name.ToString() + " has left the channel.");
                    joined = false;
                    voiceChan = e.Before.VoiceChannel;
                }
                else
                {
                    return;
                }

                // Fire up the TTS service
                reader = new SpeechSynthesizer();
                var channelCount = _client.GetService<AudioService>().Config.Channels;
                System.Speech.AudioFormat.SpeechAudioFormatInfo synthFormat = new System.Speech.AudioFormat.SpeechAudioFormatInfo(System.Speech.AudioFormat.EncodingFormat.Pcm, 48000, 16, channelCount, 16000, 2, null);
                MemoryStream stream = new MemoryStream();
                reader.SetOutputToAudioStream(stream, synthFormat);

                // Use their nickname if they have one
                var name = "";
                if (e.After.Nickname != null)
                {
                    name = e.After.Nickname;
                }
                else
                {
                    name = e.After.Name;
                }

                // Joined or Left?
                if (joined)
                {
                    reader.Speak(name + "has joined the channel.");
                }
                else
                {
                    reader.Speak(name + "has left the channel.");
                }
                stream.Position = 0;
                reader.Dispose();

                await SendAudio(stream, voiceChan);

            };

            // Note:  All commands should be ABOVE ExecuteAndWait()
            _client.ExecuteAndWait(async () =>
            {
                while (true)
                {
                    try
                    {
                        await _client.Connect(botToken, TokenType.Bot);
                        break;
                    }
                    catch
                    {
                        System.Console.WriteLine("Could not connect.  Are you using a proper bot token? or maybe services are down?");
                        await Task.Delay(3000);
                    }
                }
            });

        }

        public static async Task SendAudio(MemoryStream stream, Channel voiceChannel)
        {
            try
            {

                var channelCount = _client.GetService<AudioService>().Config.Channels; // Get the number of AudioChannels our AudioService has been configured to use.
                var OutFormat = new WaveFormat(48000, 16, channelCount); // Create a new Output Format, using the spec that Discord will accept, and with the number of channels that our client supports.

                {
                    int blockSize = OutFormat.AverageBytesPerSecond / 50; // Establish the size of our AudioBuffer
                    byte[] buffer = new byte[blockSize];
                    int byteCount;

                    while ((byteCount = stream.Read(buffer, 0, blockSize)) > 0) // Read audio into our buffer, and keep a loop open while data is present
                    {
                        if (byteCount < blockSize)
                        {
                            // Incomplete Frame
                            for (int i = byteCount; i < blockSize; i++)
                                buffer[i] = 0;
                        }
                        _vClient.Send(buffer, 0, blockSize); // Send the buffer to Discord
                    }
                }
            }
            catch
            {
                System.Console.WriteLine("Something went wrong. :(");
            }
        }
    }
}