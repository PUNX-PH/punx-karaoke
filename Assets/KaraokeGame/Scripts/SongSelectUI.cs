using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaraokeGame
{
    public class SongSelectUI : MonoBehaviour
    {
        [Header("Song List")]
        [SerializeField] private Transform  songListContainer;  // VerticalLayoutGroup parent
        [SerializeField] private Button     songButtonPrefab;   // Button with child TMP text

        [Header("Selection Info")]
        [SerializeField] private TextMeshProUGUI selectedSongText; // shows chosen song name
        [SerializeField] private Button          startButton;

        [Header("Menu Audio")]
        [SerializeField] private AudioSource bgMusicSource;
        [SerializeField] private AudioClip   bgMusicClip;
        [Range(0f, 1f)]
        [SerializeField] private float bgMusicVolume = 0.5f;

        private SongData   selectedSong;
        private Button     activeButton;  // currently highlighted button

        // highlight colors
        private static readonly Color ColSelected   = new Color(1f,   0.85f, 0f,   1f);
        private static readonly Color ColUnselected = new Color(0.2f, 0.2f,  0.2f, 1f);

        private void Start()
        {
            if (GameManager.Instance == null)
                new GameObject("GameManager").AddComponent<GameManager>();

            startButton.interactable = false;
            startButton.onClick.AddListener(OnStartPressed);

            PopulateSongList();

            if (bgMusicSource != null && bgMusicClip != null)
            {
                bgMusicSource.clip   = bgMusicClip;
                bgMusicSource.volume = bgMusicVolume;
                bgMusicSource.loop   = true;
                bgMusicSource.Play();
            }
        }

        private void PopulateSongList()
        {
            var allSongs = Resources.LoadAll<SongData>("Songs");

            if (allSongs.Length == 0)
            {
                Debug.LogWarning("[KaraokeGame] No SongData assets found in Resources/Songs/.");
                return;
            }

            foreach (Transform child in songListContainer)
                Destroy(child.gameObject);

            foreach (var song in allSongs)
            {
                var btn = Instantiate(songButtonPrefab, songListContainer);
                btn.interactable = true;
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    int blanks = song.missingLineIndices?.Length ?? 0;
                    tmp.text = $"{song.title}  <size=75%>[{song.artist}]</size>" +
                               (blanks > 0 ? $"  <size=65%><color=#AAAAAA>· {blanks} blank{(blanks > 1 ? "s" : "")}</color></size>" : "");
                }

                SetButtonColor(btn, ColUnselected);

                var captured    = song;
                var capturedBtn = btn;
                btn.onClick.AddListener(() => OnSongClicked(captured, capturedBtn));
            }
        }

        private void OnSongClicked(SongData song, Button btn)
        {
            var gm = GameManager.Instance;
            gm.SelectedSongs = new[] { song };
            gm.ResetGame();

            SceneManager.LoadScene("Gameplay");
        }

        private void OnStartPressed()
        {
            // Start button is optional — song buttons load directly.
            // If a song was pre-selected via selectedSong, honour it.
            if (selectedSong == null) return;

            var gm = GameManager.Instance;
            gm.SelectedSongs = new[] { selectedSong };
            gm.ResetGame();

            SceneManager.LoadScene("Gameplay");
        }

        private static void SetButtonColor(Button btn, Color col)
        {
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = col;
        }
    }
}