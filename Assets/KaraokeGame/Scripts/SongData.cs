using AudioTextSynchronizer.Core;
using UnityEngine;

namespace KaraokeGame
{
    [CreateAssetMenu(fileName = "NewSong", menuName = "KaraokeGame/Song Data")]
    public class SongData : ScriptableObject
    {
        public string      title;
        public string      artist;
        public AudioClip   audioClip;
        public PhraseAsset lyricsTimingAsset;

        [Tooltip("Zero-based indices of timing entries to blank out — one per quiz line.")]
        public int[]    missingLineIndices;

        [Tooltip("Correct answer for each missing line — must match missingLineIndices length.")]
        public string[] correctAnswers;

        [Tooltip("The timing index to show alongside each missing line (the 'lead-in' line). " +
                 "Must match missingLineIndices length. Leave element at -1 to use the previous line automatically.")]
        public int[] contextLineIndices;
    }
}