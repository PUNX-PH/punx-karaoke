using UnityEngine;
using UnityEngine.UI;

namespace KaraokeGame
{
    /// <summary>
    /// Attach to any Button to play a click sound when pressed.
    /// Shares a single AudioSource if one is assigned; otherwise creates its own.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonSFX : MonoBehaviour
    {
        [SerializeField] private AudioClip  clickSound;
        [SerializeField] private AudioSource audioSource;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            GetComponent<Button>().onClick.AddListener(PlayClick);
        }

        private void PlayClick()
        {
            if (clickSound != null)
                audioSource.PlayOneShot(clickSound, volume);
        }
    }
}