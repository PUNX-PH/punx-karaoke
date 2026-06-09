using System.Collections;
using UnityEngine;

namespace KaraokeGame
{
    /// <summary>
    /// Attach to any UI GameObject to drive fade, scale, slide, punch, and idle animations.
    /// Requires (or auto-adds) a CanvasGroup for fade support.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIAnimator : MonoBehaviour
    {
        public enum AnimationMode  { Fade, Scale, Slide, Punch }
        public enum IdleMode       { None, Float, Pulse, Glow, Rotate, Shake, Wiggle }
        public enum SlideDirection { Left, Right, Up, Down }
        public enum EaseMode       { Linear, EaseIn, EaseOut, EaseInOut, Bounce, Elastic }

        // ── In / Out ──────────────────────────────────────────────────────────

        [Header("Trigger")]
        [Tooltip("Run the In animation automatically when the GameObject becomes active.")]
        [SerializeField] private bool playOnEnable = true;

        [Header("In Animation")]
        [SerializeField] private AnimationMode  inMode      = AnimationMode.Fade;
        [SerializeField] private SlideDirection inDirection = SlideDirection.Down;
        [SerializeField] private EaseMode       inEase      = EaseMode.EaseOut;
        [SerializeField] private float          inDuration  = 0.35f;
        [SerializeField] private float          inDelay     = 0f;

        [Header("Out Animation")]
        [SerializeField] private AnimationMode  outMode      = AnimationMode.Fade;
        [SerializeField] private SlideDirection outDirection = SlideDirection.Down;
        [SerializeField] private EaseMode       outEase      = EaseMode.EaseIn;
        [SerializeField] private float          outDuration  = 0.25f;
        [SerializeField] private float          outDelay     = 0f;

        [Header("Slide Settings")]
        [Tooltip("How far (pixels) the element slides. 0 = auto (uses RectTransform size).")]
        [SerializeField] private float slideDistance = 0f;

        [Header("Punch Settings")]
        [SerializeField] private float punchScale    = 1.2f;
        [SerializeField] private float punchDuration = 0.3f;

        // ── Idle ──────────────────────────────────────────────────────────────

        [Header("Idle Animation")]
        [Tooltip("Loops continuously. Starts after the In animation finishes (or immediately if Play On Enable is off).")]
        [SerializeField] private IdleMode idleMode       = IdleMode.None;
        [SerializeField] private bool     idleAutoStart  = true;
        [SerializeField] private float    idleSpeed      = 1f;

        [Tooltip("Float / Wiggle / Shake: max pixel offset.")]
        [SerializeField] private float idleDistance = 8f;

        [Tooltip("Pulse / Glow: how much to scale or alpha-shift per cycle (0–1).")]
        [SerializeField] private float idleIntensity = 0.08f;

        [Tooltip("Rotate: degrees per second.")]
        [SerializeField] private float idleRotateSpeed = 45f;

        [Tooltip("Shake: randomises every N seconds. Lower = faster chaos.")]
        [SerializeField] private float idleShakeInterval = 0.05f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private RectTransform rt;
        private CanvasGroup   cg;
        private Vector2       originalPos;
        private Vector3       originalScale;
        private Coroutine     currentRoutine;
        private Coroutine     idleRoutine;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            rt            = GetComponent<RectTransform>();
            originalPos   = rt.anchoredPosition;
            originalScale = rt.localScale;

            cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (playOnEnable)
                currentRoutine = StartCoroutine(InThenIdle());
            else if (idleAutoStart && idleMode != IdleMode.None)
                StartIdle();
        }

        private void OnDisable()
        {
            StopCurrent();
            StopIdle();
            rt.anchoredPosition = originalPos;
            rt.localScale       = originalScale;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Play the In animation, then start the idle loop.</summary>
        public void AnimateIn()
        {
            StopCurrent();
            StopIdle();
            currentRoutine = StartCoroutine(InThenIdle());
        }

        /// <summary>Play the Out animation (stops idle first).</summary>
        public void AnimateOut()
        {
            StopIdle();
            Run(false);
        }

        /// <summary>Punch-scale the element (good for button feedback).</summary>
        public void Punch() => Run(false, punch: true);

        /// <summary>Play Out animation then deactivate the GameObject.</summary>
        public void AnimateOutAndHide()
        {
            StopIdle();
            StopCurrent();
            currentRoutine = StartCoroutine(OutThenHide());
        }

        /// <summary>Start the idle loop immediately.</summary>
        public void StartIdle()
        {
            StopIdle();
            if (idleMode != IdleMode.None)
                idleRoutine = StartCoroutine(IdleLoop());
        }

        /// <summary>Stop the idle loop and snap back to original transform.</summary>
        public void StopIdle()
        {
            if (idleRoutine != null) { StopCoroutine(idleRoutine); idleRoutine = null; }
            rt.anchoredPosition = originalPos;
            rt.localScale       = originalScale;
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private IEnumerator InThenIdle()
        {
            yield return StartCoroutine(inMode switch
            {
                AnimationMode.Fade  => FadeRoutine(true),
                AnimationMode.Scale => ScaleRoutine(true),
                AnimationMode.Slide => SlideRoutine(true),
                AnimationMode.Punch => PunchRoutine(),
                _                   => FadeRoutine(true)
            });
            currentRoutine = null;
            if (idleAutoStart && idleMode != IdleMode.None) StartIdle();
        }

        private void Run(bool isIn, bool punch = false)
        {
            StopCurrent();
            if (punch) { currentRoutine = StartCoroutine(PunchRoutine()); return; }
            var mode = isIn ? inMode : outMode;
            currentRoutine = mode switch
            {
                AnimationMode.Fade  => StartCoroutine(FadeRoutine(isIn)),
                AnimationMode.Scale => StartCoroutine(ScaleRoutine(isIn)),
                AnimationMode.Slide => StartCoroutine(SlideRoutine(isIn)),
                AnimationMode.Punch => StartCoroutine(PunchRoutine()),
                _                   => null
            };
        }

        private void StopCurrent()
        {
            if (currentRoutine != null) { StopCoroutine(currentRoutine); currentRoutine = null; }
        }

        // ── Idle loops ────────────────────────────────────────────────────────

        private IEnumerator IdleLoop()
        {
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime * idleSpeed;
                switch (idleMode)
                {
                    case IdleMode.Float:
                        rt.anchoredPosition = originalPos +
                            new Vector2(0f, Mathf.Sin(time) * idleDistance);
                        break;

                    case IdleMode.Wiggle:
                        rt.anchoredPosition = originalPos +
                            new Vector2(Mathf.Sin(time) * idleDistance, 0f);
                        break;

                    case IdleMode.Pulse:
                        float ps = 1f + Mathf.Sin(time) * idleIntensity;
                        rt.localScale = originalScale * ps;
                        break;

                    case IdleMode.Glow:
                        float alpha = Mathf.Lerp(1f - idleIntensity, 1f,
                            (Mathf.Sin(time) + 1f) * 0.5f);
                        cg.alpha = alpha;
                        break;

                    case IdleMode.Rotate:
                        rt.Rotate(0f, 0f, idleRotateSpeed * Time.deltaTime);
                        break;

                    case IdleMode.Shake:
                        rt.anchoredPosition = originalPos + new Vector2(
                            Random.Range(-idleDistance, idleDistance),
                            Random.Range(-idleDistance, idleDistance));
                        yield return new WaitForSeconds(idleShakeInterval);
                        continue;
                }
                yield return null;
            }
        }

        // ── Fade ──────────────────────────────────────────────────────────────
        private IEnumerator FadeRoutine(bool isIn)
        {
            float delay = isIn ? inDelay : outDelay;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            float duration = isIn ? inDuration : outDuration;
            var   ease     = isIn ? inEase     : outEase;
            float start    = isIn ? 0f : 1f;
            float end      = isIn ? 1f : 0f;

            cg.alpha = start;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                cg.alpha = Mathf.Lerp(start, end, Evaluate(ease, Mathf.Clamp01(t)));
                yield return null;
            }
            cg.alpha = end;
        }

        // ── Scale ─────────────────────────────────────────────────────────────
        private IEnumerator ScaleRoutine(bool isIn)
        {
            float delay = isIn ? inDelay : outDelay;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            float   duration = isIn ? inDuration : outDuration;
            var     ease     = isIn ? inEase     : outEase;
            Vector3 start    = isIn ? Vector3.zero  : originalScale;
            Vector3 end      = isIn ? originalScale : Vector3.zero;

            rt.localScale = start;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                rt.localScale = Vector3.Lerp(start, end, Evaluate(ease, Mathf.Clamp01(t)));
                yield return null;
            }
            rt.localScale = end;
        }

        // ── Slide ─────────────────────────────────────────────────────────────
        private IEnumerator SlideRoutine(bool isIn)
        {
            float delay = isIn ? inDelay : outDelay;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            float   duration  = isIn ? inDuration  : outDuration;
            var     ease      = isIn ? inEase      : outEase;
            var     direction = isIn ? inDirection : outDirection;
            float   dist      = slideDistance > 0f ? slideDistance : SlideAutoDistance(direction);
            Vector2 offset    = DirectionOffset(direction, dist);
            Vector2 start     = isIn ? originalPos + offset : originalPos;
            Vector2 end       = isIn ? originalPos          : originalPos + offset;

            rt.anchoredPosition = start;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                rt.anchoredPosition = Vector2.Lerp(start, end, Evaluate(ease, Mathf.Clamp01(t)));
                yield return null;
            }
            rt.anchoredPosition = end;
        }

        // ── Punch ─────────────────────────────────────────────────────────────
        private IEnumerator PunchRoutine()
        {
            float half = punchDuration * 0.5f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / half;
                rt.localScale = originalScale * Mathf.Lerp(1f, punchScale, Evaluate(EaseMode.EaseOut, Mathf.Clamp01(t)));
                yield return null;
            }
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / half;
                rt.localScale = originalScale * Mathf.Lerp(punchScale, 1f, Evaluate(EaseMode.EaseOut, Mathf.Clamp01(t)));
                yield return null;
            }
            rt.localScale = originalScale;
        }

        // ── Out then hide ─────────────────────────────────────────────────────
        private IEnumerator OutThenHide()
        {
            yield return StartCoroutine(outMode switch
            {
                AnimationMode.Fade  => FadeRoutine(false),
                AnimationMode.Scale => ScaleRoutine(false),
                AnimationMode.Slide => SlideRoutine(false),
                _                   => FadeRoutine(false)
            });
            gameObject.SetActive(false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private float SlideAutoDistance(SlideDirection dir) =>
            dir == SlideDirection.Left || dir == SlideDirection.Right
                ? rt.rect.width  + 50f
                : rt.rect.height + 50f;

        private static Vector2 DirectionOffset(SlideDirection dir, float dist) => dir switch
        {
            SlideDirection.Left  => new Vector2(-dist,  0f),
            SlideDirection.Right => new Vector2( dist,  0f),
            SlideDirection.Up    => new Vector2( 0f,  dist),
            SlideDirection.Down  => new Vector2( 0f, -dist),
            _                    => Vector2.zero
        };

        private static float Evaluate(EaseMode ease, float t) => ease switch
        {
            EaseMode.Linear    => t,
            EaseMode.EaseIn    => t * t,
            EaseMode.EaseOut   => 1f - (1f - t) * (1f - t),
            EaseMode.EaseInOut => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f,
            EaseMode.Bounce    => EvaluateBounce(t),
            EaseMode.Elastic   => EvaluateElastic(t),
            _                  => t
        };

        private static float EvaluateBounce(float t)
        {
            if (t < 1f / 2.75f)    return 7.5625f * t * t;
            if (t < 2f / 2.75f)  { t -= 1.5f   / 2.75f; return 7.5625f * t * t + 0.75f; }
            if (t < 2.5f / 2.75f){ t -= 2.25f  / 2.75f; return 7.5625f * t * t + 0.9375f; }
            t -= 2.625f / 2.75f;
            return 7.5625f * t * t + 0.984375f;
        }

        private static float EvaluateElastic(float t)
        {
            if (t == 0f || t == 1f) return t;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI) / 3f) + 1f;
        }
    }
}