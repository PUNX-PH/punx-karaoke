using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AudioTextSynchronizer;
using AudioTextSynchronizer.Core;
using AudioTextSynchronizer.TextEffects;
using AudioTextSynchronizer.TextHighlighters;
using AudioTextSynchronizer.TextSplitters;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaraokeGame
{
    [DefaultExecutionOrder(-100)]
    public class GameScreenManager : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private TextSynchronizer textSynchronizer;
        [SerializeField] private AudioSource audioSource;

        // ── Player Canvas (Display 0) ─────────────────────────────────────────
        [Header("Player Canvas — Display 0")]
        [SerializeField] private Canvas playerCanvas;

        [SerializeField] private GameObject playerCountdownPanel;
        [SerializeField] private TextMeshProUGUI playerCountdownTitleText;
        [SerializeField] private Image playerCountdown3Image;
        [SerializeField] private Image playerCountdown2Image;
        [SerializeField] private Image playerCountdown1Image;
        [SerializeField] private Image playerCountdownGoImage;

        [SerializeField] private GameObject playerGamePanel;
        [SerializeField] private TextMeshProUGUI playerSongInfoText;
        [SerializeField] private ScrollRect lyricsScrollRect;
        [SerializeField] private TextMeshProUGUI lyricsText;
        [SerializeField] private TMP_InputField guessInputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private TextMeshProUGUI playerTimerText;
        [SerializeField] private Slider playerMusicSlider;

        [SerializeField] private GameObject playerAnswerPanel;

        [Header("Player VFX")]
        [SerializeField] private ParticleSystem correctVFX;
        [SerializeField] private ParticleSystem wrongVFX;
        [SerializeField] private Image          playerVFXFlash;

        [Header("Player SFX")]
        [SerializeField] private AudioSource playerSFXSource;
        [SerializeField] private AudioClip   correctSFX;
        [SerializeField] private AudioClip   wrongSFX;
        [SerializeField] private AudioClip   finalResultSFX;

        [SerializeField] private GameObject playerResultPanel;
        [SerializeField] private Image playerCorrectImage;
        [SerializeField] private Image playerWrongImage;
        [SerializeField] private TextMeshProUGUI playerCorrectAnswerText;

        [SerializeField] private GameObject playerFinalPanel;
        [SerializeField] private TextMeshProUGUI playerFinalScoreText;
        [SerializeField] private TextMeshProUGUI playerBreakdownText;

        // ── Admin Canvas (Display 1) ──────────────────────────────────────────
        [Header("Admin Canvas — Display 1")]
        [SerializeField] private Canvas adminCanvas;

        [SerializeField] private GameObject adminCountdownPanel;
        [SerializeField] private TextMeshProUGUI adminCountdownText;
        [SerializeField] private TextMeshProUGUI adminCountdownTitleText;

        [SerializeField] private GameObject adminGamePanel;
        [SerializeField] private TextMeshProUGUI adminSongInfoText;
        [SerializeField] private ScrollRect adminLyricsScrollRect;
        [SerializeField] private TextMeshProUGUI adminLyricsText;
        [SerializeField] private TextMeshProUGUI adminGuessText;
        [SerializeField] private TextMeshProUGUI adminCorrectLineText;
        [SerializeField] private TextMeshProUGUI adminTimerText;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Button correctButton;
        [SerializeField] private Button wrongButton;

        [SerializeField] private GameObject adminResultPanel;
        [SerializeField] private TextMeshProUGUI adminResultText;
        [SerializeField] private TextMeshProUGUI adminCorrectAnswerText;

        [SerializeField] private GameObject adminFinalPanel;
        [SerializeField] private TextMeshProUGUI adminFinalText;

        // ── Admin Song Select Panel ───────────────────────────────────────────
        [Header("Admin Song Select Panel")]
        [SerializeField] private GameObject adminSongSelectPanel;
        [SerializeField] private Transform  songListContainer;   // parent (VerticalLayoutGroup)
        [SerializeField] private Button     songButtonPrefab;    // Button with child TMP text

        [Header("Player Waiting Panel")]
        [SerializeField] private GameObject playerWaitingPanel;

        // ── Runtime state ─────────────────────────────────────────────────────
        private SongData[]  songs;
        private int         songIndex;
        private SongData    currentSong;
        private PhraseAsset runtimePhraseAsset;

        private RichTextEffect            richTextEffect;
        private DefaultTextSplitConfig    splitConfig;
        private TimingTextHighlightConfig highlightConfig;

        // Hidden TextMeshProUGUI that the TextSynchronizer writes to.
        // We use OnTimingStarted to push just the current line to the visible lyricsText.
        private TextMeshProUGUI hiddenSyncText;

        // ── Karaoke line styles (TMP rich text) ───────────────────────────────
        private const string StylePrev    = "<color=#6666AA><size=72%>";
        private const string StylePrevEnd = "</size></color>";
        private const string StyleCurr    = "<color=#FFD200><b><size=115%>";
        private const string StyleCurrEnd = "</size></b></color>";
        private const string StyleNext    = "<color=#9999CC><size=72%>";
        private const string StyleNextEnd = "</size></color>";

        [Header("Display")]
        [Tooltip("How many timings to show in the prev/next slots (fallback when no line mapping exists).")]
        [SerializeField] private int contextWordsPerSlot = 1;

        [Tooltip("Controls how word highlights are distributed across a timing's duration.\n" +
                 "X = raw progress through the timing (0 = start, 1 = end).\n" +
                 "Y = fill position across the words (0 = none highlighted, 1 = all highlighted).\n\n" +
                 "Linear    → uniform speed\n" +
                 "Ease-out  → fast start, slow end\n" +
                 "Ease-in   → slow start, fast end\n" +
                 "S-curve   → slow → fast → slow")]
        [SerializeField] private AnimationCurve wordFillCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // ── Karaoke fill state ────────────────────────────────────────────────
        private int    currentTimingIdx = -1; // which timing is active right now
        private int[]  timingToLine;          // timingIndex  → display line index
        private int[][] lineTimings;          // lineIndex    → timing indices on that line

        private bool  timerRunning;
        private bool  judgeDecisionMade;
        private bool  guessSent;
        private float timer;

        // Per-song result tracking
        private bool songAllCorrect;

        // Missing-line queue
        private readonly List<int> pendingMissingIndices = new List<int>();
        private int   currentMissingIdx  = -1;
        private float missingLinePauseAt = -1f; // audio time to pause (StartPosition of next blank)
        private float missingLineSeekTo  = -1f; // seek here after reveal (StartPosition of current blank)
        private bool  pausedAtMissingLine;

        private const int   SongsPerGame         = 1;
        private const float JudgeTimerDuration   = 30f;
        private const float ResultDisplaySeconds = 3f;
        private const float FinalScreenSeconds   = 8f;
        private const int   PointsPerCorrect     = 100;

        // ── Awake ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            splitConfig    = ScriptableObject.CreateInstance<DefaultTextSplitConfig>();
            highlightConfig = ScriptableObject.CreateInstance<TimingTextHighlightConfig>();
            richTextEffect  = ScriptableObject.CreateInstance<RichTextEffect>();
            richTextEffect.TextSplitConfig        = splitConfig;
            richTextEffect.TextHighlightConfig    = highlightConfig;
            richTextEffect.HighlightColor         = new Color32(255, 210, 0, 255);
            richTextEffect.TextPartFinishedAction = OnTextPartAction.SetCurrentPartText;
            richTextEffect.EffectFinishedAction   = OnTextAction.SetCurrentPartText;

            audioSource.playOnAwake = false;

            // Create a hidden off-screen TextMeshProUGUI for the TextSynchronizer
            // to write to. OnTimingStarted pushes only the current line to lyricsText,
            // giving a "one line at a time" display without fighting the synchronizer.
            var hiddenGo = new GameObject("_SyncTarget");
            hiddenGo.transform.SetParent(lyricsText.transform.parent, false);
            hiddenSyncText = hiddenGo.AddComponent<TextMeshProUGUI>();
            ((RectTransform)hiddenGo.transform).anchoredPosition = new Vector2(-9999f, -9999f);

            textSynchronizer.GameObjectWithTextComponent = hiddenGo;
            textSynchronizer.TextComponent = hiddenSyncText;
            textSynchronizer.Property      = "text";
            textSynchronizer.Source        = audioSource;
            textSynchronizer.TextEffect    = richTextEffect;

            var gm = GameManager.Instance;
            if (gm?.SelectedSongs?.Length >= SongsPerGame)
            {
                songs       = gm.SelectedSongs;
                songIndex   = gm.CurrentSongIndex;
                currentSong = songs[songIndex];
                runtimePhraseAsset = BuildRuntimePhraseAsset(currentSong);
                textSynchronizer.Timings = runtimePhraseAsset;
            }
            else
            {
                var dummy = ScriptableObject.CreateInstance<PhraseAsset>();
                dummy.Text = "...";
                dummy.Timings.Add(new Timing(0f, 1f, Color.white, "", "..."));
                textSynchronizer.Timings = dummy;
            }
        }

        // ── Start ─────────────────────────────────────────────────────────────
        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm?.SelectedSongs?.Length < SongsPerGame)
            {
                Debug.LogError("[KaraokeGame] Not enough songs selected.");
                SceneManager.LoadScene("Song Select");
                return;
            }

            if (Display.displays.Length > 1) Display.displays[1].Activate();
            playerCanvas.targetDisplay = 1;
            adminCanvas.targetDisplay  = 0;

            sendButton.onClick.AddListener(OnSendGuess);
            correctButton.onClick.AddListener(OnCorrect);
            wrongButton.onClick.AddListener(OnWrong);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnSliderSeeked);

            HideAllPanels();
            ShowAdminSongSelect();
        }

        // ── Per-song setup ────────────────────────────────────────────────────
        private void SetupCurrentSong()
        {
            currentSong = songs[songIndex];

            if (runtimePhraseAsset != null) Destroy(runtimePhraseAsset);
            runtimePhraseAsset = BuildRuntimePhraseAsset(currentSong);
            currentTimingIdx = -1;
            BuildLineMapping();

            textSynchronizer.TextEffect = richTextEffect;
            textSynchronizer.Timings    = runtimePhraseAsset;

            audioSource.clip = currentSong.audioClip;
            audioSource.Stop();
            audioSource.time = runtimePhraseAsset.Timings.Count > 0
                ? runtimePhraseAsset.Timings[0].StartPosition : 0f;
            textSynchronizer.IsRunning = false;

            string info = $"{currentSong.title}\n[{currentSong.artist}]";
            playerSongInfoText.text = info;
            adminSongInfoText.text  = info;

            if (playerCountdownTitleText != null) playerCountdownTitleText.text = info;
            if (adminCountdownTitleText  != null) adminCountdownTitleText.text  = info;
        }

        // ── 3-2-1-GO countdown ────────────────────────────────────────────────
        private IEnumerator CountdownThenPlay()
        {
            yield return null;
            audioSource.Stop();
            textSynchronizer.IsRunning = false;

            SetupCurrentSong();

            // Reset per-song state
            judgeDecisionMade  = false;
            guessSent          = false;
            timerRunning       = false;
            pausedAtMissingLine = false;
            missingLinePauseAt  = -1f;
            missingLineSeekTo   = -1f;
            currentMissingIdx   = -1;
            currentTimingIdx    = -1;
            songAllCorrect      = true;

            guessInputField.text         = string.Empty;
            guessInputField.interactable = false;
            sendButton.interactable      = false;
            correctButton.interactable   = false;
            wrongButton.interactable     = false;
            adminGuessText.text          = string.Empty;
            if (adminCorrectLineText != null) adminCorrectLineText.text = string.Empty;
            UpdateTimerDisplay(JudgeTimerDuration);
            if (lyricsText != null) lyricsText.text = string.Empty;

            // Build the pending missing-line queue (sorted ascending by timing order)
            pendingMissingIndices.Clear();
            if (currentSong.missingLineIndices != null)
            {
                foreach (int idx in currentSong.missingLineIndices)
                    if (idx >= 0 && idx < runtimePhraseAsset.Timings.Count)
                        pendingMissingIndices.Add(idx);
                pendingMissingIndices.Sort();
            }

            HideAllPanels();
            playerCountdownPanel.SetActive(true);
            adminCountdownPanel.SetActive(true);

            for (int i = 3; i >= 1; i--)
            {
                SetPlayerCountdownImage(i);
                adminCountdownText.text = i.ToString();
                yield return new WaitForSeconds(1f);
            }

            SetPlayerCountdownImage(0); // GO!
            adminCountdownText.text = "GO!";
            yield return new WaitForSeconds(0.6f);

            playerCountdownPanel.SetActive(false);
            adminCountdownPanel.SetActive(false);
            playerGamePanel.SetActive(true);
            adminGamePanel.SetActive(true);

            textSynchronizer.OnSyncFinished += OnSongEnd;
            richTextEffect.OnTimingStart    += OnTimingStarted;

            // Arm the first pause point
            SetNextMissingLinePause();

            textSynchronizer.Play(true);
        }

        // ── Queue management ─────────────────────────────────────────────────
        // Pops the next missing-line index from the queue and sets the pause time.
        private void SetNextMissingLinePause()
        {
            if (pendingMissingIndices.Count > 0)
            {
                currentMissingIdx = pendingMissingIndices[0];
                pendingMissingIndices.RemoveAt(0);
                missingLineSeekTo  = runtimePhraseAsset.Timings[currentMissingIdx].StartPosition;
                missingLinePauseAt = missingLineSeekTo; // pause BEFORE the line plays
            }
            else
            {
                currentMissingIdx  = -1;
                missingLinePauseAt = -1f;
                missingLineSeekTo  = -1f;
            }
        }

        // ── Player presses Send ───────────────────────────────────────────────
        private void OnSendGuess()
        {
            if (guessSent) return;
            guessSent = true;
            adminGuessText.text          = guessInputField.text;
            guessInputField.interactable = false;
            sendButton.interactable      = false;
        }

        // ── Track the active timing; Update() drives the fill display ───────
        private void OnTimingStarted(Timing timing)
        {
            int idx = FindTimingIndex(timing);
            if (idx < 0) return;
            currentTimingIdx = idx;
            // Push display immediately so the first frame isn't blank
            if (lyricsText != null && lineTimings != null)
                lyricsText.text = BuildFullKaraokeDisplay(idx);
        }

        // Builds a 3-line karaoke display: previous / highlighted current / next.
        // When an explicit inline context is set the "current" is the merged pair.
        private string BuildLyricsDisplay(int idx)
        {
            int pairedMissing  = GetMissingIndexForContext(idx);   // idx is a context line
            int ctxForCurrent  = GetContextIndex(idx);             // idx is a missing line

            string currentText;
            int prevSrc, nextSrc;

            if (pairedMissing >= 0)                                // ── inline context ──
            {
                currentText = $"{StyleCurr}{runtimePhraseAsset.Timings[idx].Text} {runtimePhraseAsset.Timings[pairedMissing].Text}{StyleCurrEnd}";
                prevSrc  = idx - 1;
                nextSrc  = pairedMissing + 1;
            }
            else if (ctxForCurrent >= 0)                           // ── missing line replay ──
            {
                currentText = $"{StyleCurr}{runtimePhraseAsset.Timings[ctxForCurrent].Text} {runtimePhraseAsset.Timings[idx].Text}{StyleCurrEnd}";
                prevSrc  = ctxForCurrent - 1;
                nextSrc  = idx + 1;
            }
            else                                                   // ── normal line ──
            {
                currentText = $"{StyleCurr}{runtimePhraseAsset.Timings[idx].Text}{StyleCurrEnd}";
                prevSrc  = idx - 1;
                nextSrc  = idx + 1;
            }

            return Build3LineDisplay(currentText, prevSrc, nextSrc);
        }

        // Assembles prev + current + next with karaoke-style hierarchy.
        // Gathers up to contextWordsPerSlot timings for each context slot,
        // joining them with spaces — works for both line-by-line and word-by-word setups.
        private string Build3LineDisplay(string currentText, int prevSrc, int nextSrc)
        {
            var timings = runtimePhraseAsset.Timings;
            int limit   = Mathf.Max(1, contextWordsPerSlot);

            // ── Prev slot: walk backwards from prevSrc, insert at front ──────
            string prev = "";
            if (prevSrc >= 0 && prevSrc < timings.Count)
            {
                var parts = new System.Collections.Generic.List<string>();
                for (int i = prevSrc; i >= 0 && parts.Count < limit; i--)
                {
                    int paired = GetMissingIndexForContext(i);
                    string pt  = paired >= 0 && paired < timings.Count
                        ? timings[i].Text + " " + timings[paired].Text
                        : timings[i].Text;
                    parts.Insert(0, pt);
                }
                prev = $"{StylePrev}{string.Join(" ", parts)}{StylePrevEnd}\n";
            }

            // ── Next slot: walk forwards from nextSrc ─────────────────────────
            string next = "";
            if (nextSrc >= 0 && nextSrc < timings.Count)
            {
                var parts = new System.Collections.Generic.List<string>();
                for (int i = nextSrc; i < timings.Count && parts.Count < limit; i++)
                {
                    int paired = GetMissingIndexForContext(i);
                    string nt;
                    if (paired >= 0 && paired < timings.Count)
                        nt = timings[i].Text + " " + timings[paired].Text;
                    else
                    {
                        nt = timings[i].Text;
                        if (IsUpcomingMissingLine(i) && GetContextIndex(i) < 0)
                            nt = "? " + nt;
                    }
                    parts.Add(nt);
                }
                next = $"\n{StyleNext}{string.Join(" ", parts)}{StyleNextEnd}";
            }

            return prev + currentText + next;
        }

        // True while a missing line hasn't been judged yet.
        private bool IsUpcomingMissingLine(int idx)
            => idx == currentMissingIdx || pendingMissingIndices.Contains(idx);

        // Returns the explicit context timing index for a given missing-line index.
        // Returns -1 when contextLineIndices is not set or element is -1 (separate-line mode).
        private int GetContextIndex(int missingIdx)
        {
            var indices  = currentSong.missingLineIndices;
            var contexts = currentSong.contextLineIndices;
            if (indices == null || contexts == null) return -1;
            for (int i = 0; i < indices.Length; i++)
                if (indices[i] == missingIdx)
                    return (i < contexts.Length && contexts[i] >= 0) ? contexts[i] : -1;
            return -1;
        }

        // Returns the missing-line index whose explicit context is 'contextIdx' (-1 if none).
        private int GetMissingIndexForContext(int contextIdx)
        {
            var indices  = currentSong.missingLineIndices;
            var contexts = currentSong.contextLineIndices;
            if (indices == null || contexts == null) return -1;
            for (int i = 0; i < indices.Length; i++)
                if (i < contexts.Length && contexts[i] == contextIdx) return indices[i];
            return -1;
        }

        // Finds a timing's index by matching StartPosition (avoids reference-equality issues).
        private int FindTimingIndex(Timing timing)
        {
            var list = runtimePhraseAsset.Timings;
            for (int i = 0; i < list.Count; i++)
                if (Mathf.Approximately(list[i].StartPosition, timing.StartPosition))
                    return i;
            return -1;
        }

        // ── Update ────────────────────────────────────────────────────────────
        private void Update()
        {
            // Pause just before the next missing line starts
            if (!pausedAtMissingLine && missingLinePauseAt >= 0f &&
                audioSource.isPlaying && audioSource.time >= missingLinePauseAt)
            {
                PauseAtMissingLine();
                return;
            }

            // Karaoke fill animation — rebuild every frame while song is playing
            if (!pausedAtMissingLine && audioSource.isPlaying &&
                currentTimingIdx >= 0 && lineTimings != null && lyricsText != null)
            {
                lyricsText.text = BuildFullKaraokeDisplay(currentTimingIdx);
            }

            // Slider sync
            if (audioSource.clip != null && audioSource.clip.length > 0f)
            {
                float p = audioSource.time / audioSource.clip.length;
                if (musicSlider       != null) musicSlider.SetValueWithoutNotify(p);
                if (playerMusicSlider != null) playerMusicSlider.SetValueWithoutNotify(p);
            }

            if (!timerRunning || judgeDecisionMade) return;
            timer -= Time.deltaTime;
            UpdateTimerDisplay(Mathf.Max(0f, timer));
            if (timer <= 0f) { timerRunning = false; RevealAndResume(false); }
        }

        private void OnSliderSeeked(float value)
        {
            if (audioSource.clip == null) return;
            audioSource.time = value * audioSource.clip.length;
        }

        private void UpdateTimerDisplay(float seconds)
        {
            string s = Mathf.CeilToInt(seconds).ToString();
            if (playerTimerText != null) playerTimerText.text = s;
            if (adminTimerText  != null) adminTimerText.text  = s;
        }

        // ── Pause before missing line; show blank and await admin ─────────────
        private void PauseAtMissingLine()
        {
            pausedAtMissingLine = true;
            missingLinePauseAt  = -1f;
            judgeDecisionMade   = false;

            audioSource.Pause();

            // Pause display — show full line with the missing word as ___________
            if (lyricsText != null)
                lyricsText.text = lineTimings != null
                    ? BuildLineDisplay(currentMissingIdx, "___________")
                    : $"{StyleCurr}___________{StyleCurrEnd}";

            // Show the correct answer on the admin screen for this line
            if (adminCorrectLineText != null)
                adminCorrectLineText.text = $"Answer: \"{GetCorrectAnswerForIndex(currentMissingIdx)}\"";

            // Fresh guess for this line
            guessSent = false;
            guessInputField.text         = string.Empty;
            guessInputField.interactable = true;
            sendButton.interactable      = true;
            adminGuessText.text          = string.Empty;

            if (playerAnswerPanel != null) playerAnswerPanel.SetActive(true);

            correctButton.interactable = true;
            wrongButton.interactable   = true;
            timer        = JudgeTimerDuration;
            timerRunning = true;
        }

        // ── Judge buttons ─────────────────────────────────────────────────────
        private void OnCorrect() { if (!judgeDecisionMade) RevealAndResume(true);  }
        private void OnWrong()   { if (!judgeDecisionMade) RevealAndResume(false); }

        // ── Reveal coloured answer, then resume song ──────────────────────────
        private void RevealAndResume(bool correct)
        {
            judgeDecisionMade          = true;
            timerRunning               = false;
            correctButton.interactable = false;
            wrongButton.interactable   = false;

            if (!correct) songAllCorrect = false;

            // Score per line
            if (correct) GameManager.Instance.CorrectCount++;
            else         GameManager.Instance.WrongCount++;

            // Auto-send if the player hadn't pressed Send
            if (!guessSent)
            {
                guessSent = true;
                adminGuessText.text          = guessInputField.text;
                guessInputField.interactable = false;
                sendButton.interactable      = false;
            }

            // Correct / wrong image — show immediately, auto-hide after 1 s
            if (playerCorrectImage != null) playerCorrectImage.gameObject.SetActive( correct);
            if (playerWrongImage   != null) playerWrongImage.gameObject.SetActive(!correct);
            StartCoroutine(HideResultImageAfterDelay(1f));

            // SFX
            PlaySFX(correct ? correctSFX : wrongSFX);

            // VFX
            ParticleSystem vfx = correct ? correctVFX : wrongVFX;
            if (vfx != null)
            {
                vfx.gameObject.SetActive(true);
                vfx.Play();
                StartCoroutine(DeactivateAfterVFX(vfx));
            }
            if (playerVFXFlash != null)
                StartCoroutine(FlashScreen(correct ? new Color(0f, 1f, 0f, 0.35f)
                                                   : new Color(1f, 0f, 0f, 0.35f)));

            // Colour the blank line in the phrase asset
            string answer = GetCorrectAnswerForIndex(currentMissingIdx);
            string tag    = correct ? "green" : "red";
            if (currentMissingIdx >= 0 && currentMissingIdx < runtimePhraseAsset.Timings.Count)
            {
                runtimePhraseAsset.Timings[currentMissingIdx].Text =
                    $"<color={tag}>{answer}</color>";
                runtimePhraseAsset.Text =
                    string.Join("\n", runtimePhraseAsset.Timings.Select(t => t.Text));
            }

            // Reveal display — show full line with the coloured answer in place
            if (lyricsText != null)
                lyricsText.text = lineTimings != null
                    ? BuildLineDisplay(currentMissingIdx, $"<color={tag}>{answer}</color>")
                    : $"{StyleCurr}<color={tag}>{answer}</color>{StyleCurrEnd}";

            if (playerAnswerPanel != null) playerAnswerPanel.SetActive(false);

            // Save seek position before advancing the queue
            float seekTime = missingLineSeekTo;

            // Advance to the next missing line (or disarm if none left)
            SetNextMissingLinePause();
            pausedAtMissingLine = false;
            judgeDecisionMade   = false;

            // Re-initialise the TextSynchronizer so RichTextEffect rebuilds its
            // cached character offsets from the updated text — prevents the
            // ArgumentOutOfRangeException in String.Insert at song end.
            textSynchronizer.TextEffect = richTextEffect;
            textSynchronizer.Timings    = runtimePhraseAsset;
            if (seekTime >= 0f) audioSource.time = seekTime;
            textSynchronizer.Play(true);
        }

        // ── Song fully ends ───────────────────────────────────────────────────
        private void OnSongEnd()
        {
            textSynchronizer.OnSyncFinished -= OnSongEnd;
            richTextEffect.OnTimingStart    -= OnTimingStarted;

            // Edge case: song ended with unanswered lines (invalid indices, etc.)
            if (pendingMissingIndices.Count > 0 || pausedAtMissingLine)
            {
                songAllCorrect = false;
                timerRunning   = false;
                correctButton.interactable = false;
                wrongButton.interactable   = false;
                if (playerAnswerPanel != null) playerAnswerPanel.SetActive(false);
            }

            GameManager.Instance.CurrentSongIndex = songIndex + 1;

            playerGamePanel.SetActive(false);
            adminGamePanel.SetActive(false);

            songIndex++;
            if (songIndex >= SongsPerGame)
                ShowFinalScore();
            else
                StartCoroutine(CountdownThenPlay());
        }

        // ── Final score ───────────────────────────────────────────────────────
        private void ShowFinalScore()
        {
            int correct    = GameManager.Instance.CorrectCount;
            int wrong      = GameManager.Instance.WrongCount;
            int totalLines = correct + wrong;
            int score      = correct * PointsPerCorrect;

            PlaySFX(finalResultSFX);

            playerFinalPanel.SetActive(true);
            playerFinalScoreText.text = $"You got {correct} out of {totalLines}!\nScore: {score} pts";
            playerBreakdownText.text  = $"Correct: {correct}     Wrong: {wrong}";

            adminFinalPanel.SetActive(true);
            adminFinalText.text = $"Final Score\nCorrect: {correct} / {totalLines}  ·  Score: {score} pts";

            StartCoroutine(ReturnToMenu());
        }

        private IEnumerator ReturnToMenu()
        {
            yield return new WaitForSeconds(FinalScreenSeconds);
            SceneManager.LoadScene("Song Select");
        }

        // ── Admin mirrors player lyrics ───────────────────────────────────────
        private void LateUpdate()
        {
            if (adminLyricsText == null || lyricsText == null) return;
            adminLyricsText.text = lyricsText.text;
            if (adminLyricsScrollRect != null)
                adminLyricsScrollRect.verticalNormalizedPosition =
                    lyricsScrollRect.verticalNormalizedPosition;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private string GetCorrectAnswerForIndex(int missingIdx)
        {
            var indices = currentSong.missingLineIndices;
            var answers = currentSong.correctAnswers;
            if (indices != null && answers != null)
            {
                for (int i = 0; i < indices.Length; i++)
                    if (indices[i] == missingIdx && i < answers.Length)
                        return answers[i];
            }
            // Fallback: use original timing text from the source asset
            var src = currentSong.lyricsTimingAsset.Timings;
            return (missingIdx >= 0 && missingIdx < src.Count) ? src[missingIdx].Text : "?";
        }

        private string BuildAnswerSummary()
        {
            var indices = currentSong.missingLineIndices;
            if (indices == null || indices.Length == 0) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < indices.Length; i++)
            {
                string ans = GetCorrectAnswerForIndex(indices[i]);
                sb.AppendLine(indices.Length > 1 ? $"Line {i + 1}: \"{ans}\"" : $"The answer was:\n\"{ans}\"");
            }
            return sb.ToString().TrimEnd();
        }

        // ── Karaoke fill display ──────────────────────────────────────────────

        // Build the line mapping: source text newlines define display lines.
        // Falls back to one-timing-per-line when the source text has no newlines
        // (e.g. line-by-line timing setup, or Text field not set).
        private void BuildLineMapping()
        {
            int count = runtimePhraseAsset.Timings.Count;
            timingToLine = new int[count];

            var src = currentSong?.lyricsTimingAsset?.Text;
            var srcLines = string.IsNullOrEmpty(src)
                ? null
                : src.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (srcLines != null && srcLines.Length > 1)
            {
                // Word-by-word: group timings by lyric line from the source text
                var lines = new List<int[]>();
                int t = 0;
                foreach (var line in srcLines)
                {
                    var words = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    var list  = new List<int>();
                    foreach (var _ in words)
                    {
                        if (t >= count) break;
                        timingToLine[t] = lines.Count;
                        list.Add(t++);
                    }
                    if (list.Count > 0) lines.Add(list.ToArray());
                }
                // Any remaining timings (count mismatch) get their own lines
                while (t < count)
                {
                    timingToLine[t] = lines.Count;
                    lines.Add(new[] { t++ });
                }
                lineTimings = lines.ToArray();
            }
            else
            {
                // Fallback: each timing is its own display line
                lineTimings = new int[count][];
                for (int i = 0; i < count; i++)
                {
                    timingToLine[i] = i;
                    lineTimings[i]  = new[] { i };
                }
            }
        }

        // Three-row karaoke display: previous line (sung/yellow) / current (fill) / next (dim).
        private string BuildFullKaraokeDisplay(int activeIdx)
        {
            if (activeIdx < 0 || activeIdx >= timingToLine.Length) return "";
            int line = timingToLine[activeIdx];

            string prev = line > 0
                ? $"<size=72%><color=#FFD200>{LineText(line - 1)}</color></size>\n" : "";
            string curr = $"<b><size=115%>{FillLineText(activeIdx, line)}</size></b>";
            string next = line + 1 < lineTimings.Length
                ? $"\n<size=72%><color=#FFFFFF55>{LineText(line + 1)}</color></size>" : "";

            return prev + curr + next;
        }

        // Pause / reveal state: full line with one word replaced by a custom string.
        private string BuildLineDisplay(int replacedTimingIdx, string replacement)
        {
            int line = timingToLine[replacedTimingIdx];
            var sb = new System.Text.StringBuilder();
            foreach (int t in lineTimings[line])
            {
                if (t < replacedTimingIdx)
                    sb.Append($"<color=#FFD200>{runtimePhraseAsset.Timings[t].Text}</color> ");
                else if (t == replacedTimingIdx)
                    sb.Append($"<color=#FFD200>{replacement}</color> ");
                else
                    sb.Append($"<color=#FFFFFF55>{runtimePhraseAsset.Timings[t].Text}</color> ");
            }
            string curr = $"<b><size=115%>{sb.ToString().TrimEnd()}</size></b>";

            int lineIdx = timingToLine[replacedTimingIdx];
            string prev = lineIdx > 0
                ? $"<size=72%><color=#FFD200>{LineText(lineIdx - 1)}</color></size>\n" : "";
            string next = lineIdx + 1 < lineTimings.Length
                ? $"\n<size=72%><color=#FFFFFF55>{LineText(lineIdx + 1)}</color></size>" : "";

            return prev + curr + next;
        }

        // Build the fill-animated text for the active timing's line.
        // Works for both setups:
        //   Word-by-word timings → each timing has 1 word, uses real timing duration.
        //   Line-by-line timings → one timing has multiple words, duration is distributed evenly.
        private string FillLineText(int activeIdx, int lineIdx)
        {
            var sb = new System.Text.StringBuilder();
            foreach (int t in lineTimings[lineIdx])
            {
                string text = runtimePhraseAsset.Timings[t].Text;
                if (t < activeIdx)
                    AppendWords(sb, text, WordState.Sung);
                else if (t == activeIdx)
                    AppendActiveWords(sb, t, text);
                else
                    AppendWords(sb, text, WordState.Future);
            }
            return sb.ToString().TrimEnd();
        }

        private enum WordState { Sung, Future }

        private static void AppendWords(System.Text.StringBuilder sb, string text, WordState state)
        {
            // Already contains a color tag (e.g. revealed answer) — output as-is to
            // avoid broken nested tags like <color=gold><color=green>x</color></color>.
            if (text.Contains("<color=")) { sb.Append(text + " "); return; }

            string col = state == WordState.Sung ? "#FFD200" : "#FFFFFF55";
            foreach (var w in text.Split(new[]{' ','\t'}, System.StringSplitOptions.RemoveEmptyEntries))
                sb.Append($"<color={col}>{w}</color> ");
        }

        // Animate words inside a single timing left-to-right.
        // Single word  → use the timing's real duration for the fill progress.
        // Multiple words → evenly distribute the timing duration across each word.
        private void AppendActiveWords(System.Text.StringBuilder sb, int timingIdx, string text)
        {
            // Already-tagged text (revealed answer) — output as-is.
            if (text.Contains("<color=")) { sb.Append(text + " "); return; }

            var words = text.Split(new[]{' ','\t'}, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return;

            var   t0  = runtimePhraseAsset.Timings[timingIdx];
            float dur = t0.EndPosition - t0.StartPosition;
            float raw = dur > 0f ? Mathf.Clamp01((audioSource.time - t0.StartPosition) / dur) : 1f;

            // Apply the animation curve so the user can shape pacing per line
            float p = wordFillCurve != null ? Mathf.Clamp01(wordFillCurve.Evaluate(raw)) : raw;

            for (int i = 0; i < words.Length; i++)
            {
                float wStart = (float)i       / words.Length;
                float wEnd   = (float)(i + 1) / words.Length;

                if (p >= wEnd)
                    sb.Append($"<color=#FFD200>{words[i]}</color> ");
                else if (p > wStart)
                    sb.Append(FilledWord(words[i], (p - wStart) / (wEnd - wStart)) + " ");
                else
                    sb.Append($"<color=#FFFFFF55>{words[i]}</color> ");
            }
        }

        // Fill a word gold from left to right (p = 0–1).
        // Uses a 3-character SmoothStep blend zone at the boundary for a smooth edge.
        private static string FilledWord(string word, float p)
        {
            if (p <= 0f) return $"<color=#FFFFFF55>{word}</color>";
            if (p >= 1f) return $"<color=#FFD200>{word}</color>";

            int   len      = word.Length;
            float fillPos  = p * len;
            int   solidEnd = Mathf.Max(0, Mathf.FloorToInt(fillPos) - 1);
            int   blendEnd = Mathf.Min(len, Mathf.CeilToInt(fillPos) + 2);

            var sb = new System.Text.StringBuilder();

            // Solid yellow section (single tag — fast)
            if (solidEnd > 0)
                sb.Append($"<color=#FFD200>{word.Substring(0, solidEnd)}</color>");

            // Blend zone: per-character tags with SmoothStep-eased colour
            Color dimCol  = new Color(1f, 1f, 1f, 0.33f);
            Color goldCol = new Color(1f, 0.824f, 0f, 1f);
            for (int i = solidEnd; i < blendEnd; i++)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fillPos - i));
                string hex = ColorUtility.ToHtmlStringRGBA(Color.Lerp(dimCol, goldCol, t));
                sb.Append($"<color=#{hex}>{word[i]}</color>");
            }

            // Solid dim section (single tag — fast)
            if (blendEnd < len)
                sb.Append($"<color=#FFFFFF55>{word.Substring(blendEnd)}</color>");

            return sb.ToString();
        }

        // Playback progress within a single timing (0–1).
        private float WordProgress(int timingIdx)
        {
            var t   = runtimePhraseAsset.Timings[timingIdx];
            float d = t.EndPosition - t.StartPosition;
            return d <= 0f ? 1f : Mathf.Clamp01((audioSource.time - t.StartPosition) / d);
        }

        // All words on a line joined by spaces.
        // Already-tagged text (e.g. revealed answer) is kept intact.
        private string LineText(int lineIdx)
        {
            var parts = new List<string>();
            foreach (int i in lineTimings[lineIdx])
            {
                var t = runtimePhraseAsset.Timings[i].Text;
                if (t.Contains("<color="))
                    parts.Add(t);  // keep rich text intact
                else
                    parts.AddRange(t.Split(new[]{' ','\t'}, System.StringSplitOptions.RemoveEmptyEntries));
            }
            return string.Join(" ", parts);
        }

        // 3/2/1 = show that number image; 0 = show GO!; anything else = hide all
        private void SetPlayerCountdownImage(int step)
        {
            if (playerCountdown3Image  != null) playerCountdown3Image.gameObject.SetActive(step == 3);
            if (playerCountdown2Image  != null) playerCountdown2Image.gameObject.SetActive(step == 2);
            if (playerCountdown1Image  != null) playerCountdown1Image.gameObject.SetActive(step == 1);
            if (playerCountdownGoImage != null) playerCountdownGoImage.gameObject.SetActive(step == 0);
        }

        private void HideAllPanels()
        {
            playerCountdownPanel.SetActive(false);
            playerGamePanel.SetActive(false);
            if (playerAnswerPanel   != null) playerAnswerPanel.SetActive(false);
            if (playerCorrectImage  != null) playerCorrectImage.gameObject.SetActive(false);
            if (playerWrongImage    != null) playerWrongImage.gameObject.SetActive(false);
            if (playerWaitingPanel  != null) playerWaitingPanel.SetActive(false);
            playerFinalPanel.SetActive(false);
            adminCountdownPanel.SetActive(false);
            adminGamePanel.SetActive(false);
            adminFinalPanel.SetActive(false);
            if (adminSongSelectPanel != null) adminSongSelectPanel.SetActive(false);
        }

        // ── Admin song selection ──────────────────────────────────────────────
        private void ShowAdminSongSelect()
        {
            if (adminSongSelectPanel == null || songListContainer == null || songButtonPrefab == null)
            {
                // No song-select panel wired up — go straight to countdown
                StartCoroutine(CountdownThenPlay());
                return;
            }

            if (playerWaitingPanel != null) playerWaitingPanel.SetActive(true);
            adminSongSelectPanel.SetActive(true);

            // Clear any old buttons
            foreach (Transform child in songListContainer)
                Destroy(child.gameObject);

            var allSongs = Resources.LoadAll<SongData>("Songs");
            foreach (var song in allSongs)
            {
                var btn = Instantiate(songButtonPrefab, songListContainer);
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                    tmp.text = $"{song.title}\n<size=80%>[{song.artist}]</size>";

                var captured = song;
                btn.onClick.AddListener(() => OnAdminSongSelected(captured));
            }
        }

        private void OnAdminSongSelected(SongData song)
        {
            var gm = GameManager.Instance;
            gm.SelectedSongs = new[] { song };
            gm.ResetGame();

            songs       = gm.SelectedSongs;
            songIndex   = 0;

            // Rebuild runtime asset for the chosen song
            if (runtimePhraseAsset != null) Destroy(runtimePhraseAsset);
            runtimePhraseAsset = BuildRuntimePhraseAsset(song);
            textSynchronizer.Timings = runtimePhraseAsset;

            if (adminSongSelectPanel != null) adminSongSelectPanel.SetActive(false);
            if (playerWaitingPanel   != null) playerWaitingPanel.SetActive(false);

            StartCoroutine(CountdownThenPlay());
        }

        // ── SFX ───────────────────────────────────────────────────────────────
        private void PlaySFX(AudioClip clip)
        {
            if (playerSFXSource != null && clip != null)
                playerSFXSource.PlayOneShot(clip);
        }

        // ── Hide correct/wrong image after a short delay ─────────────────────
        private IEnumerator HideResultImageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (playerCorrectImage != null) playerCorrectImage.gameObject.SetActive(false);
            if (playerWrongImage   != null) playerWrongImage.gameObject.SetActive(false);
        }

        // ── Deactivate VFX once particles finish ──────────────────────────────
        private IEnumerator DeactivateAfterVFX(ParticleSystem vfx)
        {
            yield return new WaitUntil(() => !vfx.IsAlive(true));
            vfx.gameObject.SetActive(false);
        }

        // ── Screen flash ──────────────────────────────────────────────────────
        private IEnumerator FlashScreen(Color colour)
        {
            playerVFXFlash.color = colour;
            playerVFXFlash.gameObject.SetActive(true);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.1f;
                playerVFXFlash.color = new Color(colour.r, colour.g, colour.b, Mathf.Lerp(0f, colour.a, t));
                yield return null;
            }

            yield return new WaitForSeconds(0.3f);

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.5f;
                playerVFXFlash.color = new Color(colour.r, colour.g, colour.b, Mathf.Lerp(colour.a, 0f, t));
                yield return null;
            }

            playerVFXFlash.gameObject.SetActive(false);
        }

        // ── Build runtime PhraseAsset with blanks ─────────────────────────────
        private static PhraseAsset BuildRuntimePhraseAsset(SongData data)
        {
            var src   = data.lyricsTimingAsset;
            var asset = ScriptableObject.CreateInstance<PhraseAsset>();
            asset.Clip = src.Clip;

            foreach (var t in src.Timings)
                asset.Timings.Add(new Timing(t.StartPosition, t.EndPosition, t.Color, t.Name, t.Text));

            if (data.missingLineIndices != null)
            {
                foreach (int idx in data.missingLineIndices)
                    if (idx >= 0 && idx < asset.Timings.Count)
                        asset.Timings[idx].Text = "___________";
            }

            asset.Text = string.Join("\n", asset.Timings.Select(t => t.Text));
            return asset;
        }

        private void OnDestroy()
        {
            textSynchronizer.OnSyncFinished -= OnSongEnd;
            if (richTextEffect != null)
            {
                richTextEffect.OnTimingStart -= OnTimingStarted;
                Destroy(richTextEffect);
            }
            if (splitConfig        != null) Destroy(splitConfig);
            if (highlightConfig    != null) Destroy(highlightConfig);
            if (runtimePhraseAsset != null) Destroy(runtimePhraseAsset);
        }
    }
}