using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;

namespace Scanner.Services
{
    public sealed class SpeechService : IDisposable
    {
        private static readonly Dictionary<char, string> ChineseDigits = new Dictionary<char, string>
        {
            { '0', "零" }, { '1', "一" }, { '2', "二" }, { '3', "三" }, { '4', "四" },
            { '5', "五" }, { '6', "六" }, { '7', "七" }, { '8', "八" }, { '9', "九" }
        };
        private readonly SpeechSynthesizer _speech;
        private readonly string _chineseVoice;
        public SpeechService()
        {
            _speech = new SpeechSynthesizer();
            _speech.SetOutputToDefaultAudioDevice();
            _speech.Volume = 100;
            _speech.Rate = 0;
            InstalledVoice voice = _speech.GetInstalledVoices().FirstOrDefault(item => item.Enabled && item.VoiceInfo.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
            _chineseVoice = voice == null ? null : voice.VoiceInfo.Name;
            if (!string.IsNullOrWhiteSpace(_chineseVoice))
            {
                _speech.SelectVoice(_chineseVoice);
            }
        }

        public void SpeakOid()
        {
            _speech.SpeakAsync("O I D");
        }

        public void SpeakChineseTail(string serialNumber)
        {
            string value = (serialNumber ?? string.Empty).Trim();
            if (value.Length > 5)
            {
                value = value.Substring(value.Length - 5);
            }
            string speechText = string.Concat(value.Select(character => ChineseDigits.ContainsKey(character) ? ChineseDigits[character] : character.ToString()));
            _speech.SpeakAsync(speechText);
        }

        public void Dispose()
        {
            _speech.Dispose();
        }
    }
}
