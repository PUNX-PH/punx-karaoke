# Karaoke Quiz Game — Scene Setup Guide

All five C# scripts are in `Assets/KaraokeGame/Scripts/`.  
Follow the steps below to wire up the two scenes in Unity.

---

## 0. Project settings

1. **Build Settings → Scenes In Build**:
   - Index 0 → `SongSelect`
   - Index 1 → `GameScreen`

2. **AudioSource** on the GameScreen sync object: set **Play On Awake = false**.

---

## 1. Create Song Data assets

For each song:

1. **Right-click → Create → KaraokeGame → Song Data**
2. Fill in: `title`, `artist`, `audioClip`, `lyricsTimingAsset` (a `PhraseAsset`), `missingLineIndex` (0-based), `correctAnswer`
3. Save the asset inside **`Assets/Resources/Songs/`** (create the folder if needed)

> The `PhraseAsset` is created via **Right-click → Create → Audio Text Synchronizer → Audio Timings**.  
> Each entry in its `Timings` list is one lyric line: set `StartPosition`, `EndPosition`, and `Text`.  
> Populate the `Text` field at the top of the PhraseAsset with all lines joined by `\n` (the game rebuilds this at runtime, but the asset needs a non-empty value for the Editor inspector).

---

## 2. Scene 1 — SongSelect

### Hierarchy

```
SongSelect (scene)
├── EventSystem
├── GameManager          ← empty GameObject
└── Canvas (Screen Space – Overlay)
    └── ScrollView
        ├── Viewport
        │   └── Content           ← VerticalLayoutGroup, ContentSizeFitter (Vertical = Preferred)
        └── Scrollbar Vertical
```

### Components

| Object | Component | Setting |
|--------|-----------|---------|
| GameManager | `GameManager` script | — |
| Canvas root or a child | `SongSelectUI` script | `listContent` → Content transform; `listItemPrefab` → SongListItem prefab |

### SongListItem prefab

Create a prefab under `Assets/KaraokeGame/Prefabs/SongListItem.prefab`:

```
SongListItem (prefab root)
├── TitleText    (TextMeshProUGUI)
├── ArtistText   (TextMeshProUGUI)
└── PlayButton   (Button → TextMeshProUGUI label "Play")
```

Add `SongListItem` script to the root. Wire:
- `titleText` → TitleText
- `artistText` → ArtistText
- `playButton` → PlayButton

---

## 3. Scene 2 — GameScreen

### Hierarchy overview

```
GameScreen (scene)
├── EventSystem
├── SyncManager          ← TextSynchronizer + AudioSource live here
└── Canvas (Screen Space – Overlay)
    ├── SongTitleText       (TextMeshProUGUI)
    ├── TimerText           (TextMeshProUGUI, shown when timer runs — managed by script)
    │
    ├── LyricsScrollView    (ScrollRect – Vertical only, no Horizontal)
    │   └── Viewport        (Image + Mask)
    │       └── Content     (ContentSizeFitter: Vertical = Preferred Size)
    │           └── LyricsText   (TextMeshProUGUI – word wrap ON, overflow = Overflow)
    │
    ├── PlayerLayer         (Panel / CanvasGroup)
    │   ├── GuessLabel      (TextMeshProUGUI  "Your answer:")
    │   ├── GuessInputField (TMP_InputField)
    │   ├── PlayerTimerText (TextMeshProUGUI – starts hidden, shows when timer runs)
    │   └── AdminToggleBtn  (Button – make Image alpha 0, place 60×60 in top-right corner)
    │
    ├── AdminLayer          (Panel / CanvasGroup – starts inactive)
    │   ├── LiveGuessLabel  (TextMeshProUGUI  "Player's answer:")
    │   ├── AdminLiveGuessText (TextMeshProUGUI)
    │   ├── AdminTimerText  (TextMeshProUGUI)
    │   ├── CorrectButton   (Button, green background)
    │   ├── WrongButton     (Button, red background)
    │   └── AdminCloseBtn   (Button  "Close Admin")
    │
    └── ResultPanel         (Panel – starts inactive)
        ├── ResultText      (TextMeshProUGUI – large, centred)
        ├── CorrectAnswerText (TextMeshProUGUI)
        └── ResultCountdownText (TextMeshProUGUI)
```

### SyncManager object

Add to the `SyncManager` GameObject:
- **AudioSource** — Play On Awake: **false**, Loop: false
- **TextSynchronizer** — leave `Timings` empty (the script assigns it at runtime)

> The `GameScreenManager` sets `GameObjectWithTextComponent`, `TextComponent`, `Property`, `Source`, `TextEffect`, and `Timings` at runtime, so you do **not** need to fill those in the inspector.

### Canvas GameScreenManager object

Add `GameScreenManager` to any persistent GameObject (e.g. the Canvas root) and wire every serialised field:

| Field | Object |
|-------|--------|
| `textSynchronizer` | SyncManager → TextSynchronizer |
| `audioSource` | SyncManager → AudioSource |
| `lyricsScrollRect` | LyricsScrollView |
| `lyricsText` | LyricsScrollView/Viewport/Content/LyricsText |
| `playerLayer` | PlayerLayer |
| `guessInputField` | PlayerLayer/GuessInputField |
| `adminToggleButton` | PlayerLayer/AdminToggleBtn |
| `playerTimerText` | PlayerLayer/PlayerTimerText |
| `adminLayer` | AdminLayer |
| `adminLiveGuessText` | AdminLayer/AdminLiveGuessText |
| `adminTimerText` | AdminLayer/AdminTimerText |
| `correctButton` | AdminLayer/CorrectButton |
| `wrongButton` | AdminLayer/WrongButton |
| `adminCloseButton` | AdminLayer/AdminCloseBtn |
| `resultPanel` | ResultPanel |
| `resultText` | ResultPanel/ResultText |
| `correctAnswerText` | ResultPanel/CorrectAnswerText |
| `resultCountdownText` | ResultPanel/ResultCountdownText |
| `songTitleText` | SongTitleText |

---

## 4. Hidden admin button

The admin toggle button should be **invisible but tappable**:
- Select `AdminToggleBtn`
- Set its `Image` component **Color alpha → 0**
- Ensure **Raycast Target = true** on the Image
- Size: 60 × 60 px, anchored to the top-right corner

The admin can tap the invisible area to open the admin overlay.

---

## 5. LyricsText setup

For correct auto-scroll:
- `TextMeshProUGUI` settings: **Word Wrapping = On**, **Overflow = Overflow**
- The `Content` parent needs **ContentSizeFitter → Vertical Fit = Preferred Size**
- The `ScrollRect` needs **Vertical = true**, **Horizontal = false**

---

## 6. Flow summary

| Event | What happens |
|-------|-------------|
| SongSelect → Play | `GameManager.SelectedSong` set; load GameScreen |
| GameScreen Start | Deep-copy PhraseAsset, replace line `missingLineIndex` with `___________`, begin playback, unlock input |
| Each timing line starts | `LyricsText` highlights current line (golden yellow); ScrollRect auto-scrolls |
| `OnSyncFinished` | 30-second countdown begins; Correct / Wrong buttons enabled |
| Correct or Wrong pressed | Result panel shown, 5-second countdown, then SongSelect loads |
| Timer reaches 0 | Auto-triggers Wrong |