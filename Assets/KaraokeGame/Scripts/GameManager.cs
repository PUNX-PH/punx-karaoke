using UnityEngine;

namespace KaraokeGame
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public SongData[] SelectedSongs { get; set; }
        public int CurrentSongIndex     { get; set; }
        public int CorrectCount         { get; set; }
        public int WrongCount           { get; set; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ResetGame()
        {
            CurrentSongIndex = 0;
            CorrectCount     = 0;
            WrongCount       = 0;
        }
    }
}