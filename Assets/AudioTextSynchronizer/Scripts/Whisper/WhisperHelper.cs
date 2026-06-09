#if WHISPER_UNITY
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AudioTextSynchronizer.Core;
using UnityEngine;
using Whisper;
using Random = UnityEngine.Random;

namespace AudioTextSynchronizer.Whisper
{
    public class WhisperHelper
    {
        private WhisperWrapper whisper;
        private WhisperParams defaultParameters;
        private const float ColorRangeFrom = 0.15f;
        private const float ColorRangeTo = 0.85f;

        public WhisperHelper(string language = "auto")
        {
            defaultParameters = WhisperParams.GetDefaultParams();
            defaultParameters.Language = language;
            defaultParameters.Translate = false;
            defaultParameters.NoContext = true;
            defaultParameters.SingleSegment = false;
            defaultParameters.SpeedUp = false;
            defaultParameters.AudioCtx = 0;
            defaultParameters.EnableTokens = false;
            defaultParameters.TokenTimestamps = false;
            defaultParameters.InitialPrompt = string.Empty;
        }

        public async Task<WhisperWrapper> LoadModel()
        {
            var modelPath = Resources.Load<WhisperSettings>("WhisperSettings").FullModelPath;
            whisper = await WhisperWrapper.InitFromFileAsync(modelPath);
            return whisper;
        }
        
        public async Task<WhisperWrapper> LoadModel(string modelPath)
        {
            whisper = await WhisperWrapper.InitFromFileAsync(modelPath);
            return whisper;
        }
        
        public async Task<PhraseAsset> GenerateTimings(AudioClip clip, string language = "auto", int maxTextLength = 0)
        { 
            var trimChars = new[] {' ', '\t', '\n', '\r'};
            defaultParameters.Language = language;
            var result = await whisper.GetTextAsync(clip, defaultParameters);
            WhisperResult whisperResult;
            if (maxTextLength > 0)
            {
                var segments = new List<WhisperSegment>();
                foreach (var segment in result.Segments)
                {
                    var newSegments = SplitSegment(segment, maxTextLength, trimChars, true);
                    segments.AddRange(newSegments);
                }
                
                whisperResult = new WhisperResult(segments, result.LanguageId);
            }
            else
            {
                whisperResult = new WhisperResult(result.Segments, result.LanguageId);
            }

            var asset = CreatePhraseAsset(clip, whisperResult);
            return asset;
        }

        public async Task<PhraseAsset> GenerateTimings(AudioClip clip, WhisperParams parameters)
        {
            var result = await whisper.GetTextAsync(clip, parameters);
            var asset = CreatePhraseAsset(clip, result);
            return asset;
        }
        
        public async Task<PhraseAsset> GenerateTimings(float[] data, int frequency, int channels)
        {
            var result = await whisper.GetTextAsync(data, frequency, channels, defaultParameters);
            var clip = AudioClip.Create("SomeName", data.Length, channels, frequency, false);
            clip.SetData(data, 1);
            var asset = CreatePhraseAsset(clip, result);
            return asset;
        }

        private PhraseAsset CreatePhraseAsset(AudioClip clip, WhisperResult whisperResult)
        {
            var phraseAsset = ScriptableObject.CreateInstance<PhraseAsset>();
            phraseAsset.Clip = clip;
            var text = new string[whisperResult.Segments.Count];
            for (var i = 0; i < whisperResult.Segments.Count; i++)
            {
                var subtitle = whisperResult.Segments[i];
                if (subtitle.Text == "[BLANK_AUDIO]")
                    continue;

                var startTime = (float)subtitle.Start.TotalMilliseconds / 1000f;
                startTime = Mathf.Max(0, startTime);
                var endTime = (float)subtitle.End.TotalMilliseconds / 1000f;
                endTime = Mathf.Min(endTime, clip.length);
                var timingText = subtitle.Text.Trim();
                var color = new Color(Random.Range(ColorRangeFrom, ColorRangeTo), Random.Range(ColorRangeFrom, ColorRangeTo), Random.Range(ColorRangeFrom, ColorRangeTo), 41f / 255f);
                var name = string.Join(" ", timingText.Split(' ').Take(3));
                var newTiming = new Timing(startTime, endTime, color, name, timingText);
                phraseAsset.Timings.Add(newTiming);
                text[i] = timingText;
            }
            phraseAsset.Text = string.Join(" ", text);
            return phraseAsset;
        }

        private List<WhisperSegment> SplitSegment(WhisperSegment segment, int maxLength, char[] trimChars, bool allowLongWords)
        {
            if (segment == null)
                throw new ArgumentNullException(nameof(segment));
            
            if (segment.Text == null)
                throw new ArgumentException("Segment text cannot be null.", nameof(segment));
            
            if (maxLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "maxLength must be positive.");

            if (segment.Text.Length <= maxLength)
            {
                return new List<WhisperSegment> { segment };
            }

            var parts = new List<(int startIndex, int endIndex, string partText)>();
            var text = segment.Text;
            var currentIndex = 0;
            while (currentIndex < text.Length)
            {
                var remaining = text.Length - currentIndex;
                if (remaining <= maxLength)
                {
                    var partEnd = text.Length;
                    var partText = text.Substring(currentIndex, partEnd - currentIndex);
                    parts.Add((currentIndex, partEnd, partText));
                    break;
                }

                var candidateEnd = currentIndex + maxLength;
                var splitIndex = candidateEnd;
                var wordEnd = candidateEnd;
                while (wordEnd < text.Length && !trimChars.Contains(text[wordEnd]))
                {
                    wordEnd++;
                }

                var wordLength = wordEnd - currentIndex;
                if (allowLongWords && wordLength > maxLength)
                {
                    splitIndex = wordEnd;
                }
                else
                {
                    var foundDelimiter = false;
                    var lastDelimiter = -1;
                    for (var i = currentIndex; i < candidateEnd; i++)
                    {
                        if (trimChars.Contains(text[i]))
                        {
                            foundDelimiter = true;
                            lastDelimiter = i;
                        }
                    }

                    if (foundDelimiter && candidateEnd < text.Length && !trimChars.Contains(text[candidateEnd]) &&
                        lastDelimiter > currentIndex)
                    {
                        splitIndex = lastDelimiter;
                    }
                }

                var part = text.Substring(currentIndex, splitIndex - currentIndex);
                parts.Add((currentIndex, splitIndex, part));
                currentIndex = splitIndex;
                while (currentIndex < text.Length && trimChars.Contains(text[currentIndex]))
                {
                    currentIndex++;
                }
            }

            var newSegments = new List<WhisperSegment>();
            var totalDurationMs = (segment.End - segment.Start).TotalMilliseconds;
            var totalTextLength = text.Length;
            for (var i = 0; i < parts.Count; i++)
            {
                var (startIdx, endIdx, partText) = parts[i];
                var fractionStart = (double)startIdx / totalTextLength;
                var fractionEnd = (double)endIdx / totalTextLength;
                var newStartMs = segment.Start.TotalMilliseconds + fractionStart * totalDurationMs;
                var newEndMs = segment.Start.TotalMilliseconds + fractionEnd * totalDurationMs;
                var newStartDeci = (ulong)(newStartMs / 10);
                var newEndDeci = (ulong)(newEndMs / 10);
                var newSegment = new WhisperSegment(i, partText, newStartDeci, newEndDeci);
                newSegments.Add(newSegment);
            }

            return newSegments;
        }
    }
}
#endif