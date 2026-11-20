using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Graybox full-screen fade for Layer transitions (#35): fades to black and
    /// back over <see cref="FadeSeconds"/> whenever <see cref="Trigger"/> is called.
    /// Purely local presentation (IMGUI); Phase 6 replaces it.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        /// <summary>Total fade time, half out and half in (PRD #31 tuning).</summary>
        public const float FadeSeconds = 0.3f;

        private static Texture2D _black;
        private float _startTime = float.NegativeInfinity;

        public void Trigger() => _startTime = Time.unscaledTime;

        /// <summary>0 = clear, 1 = black.</summary>
        public float Alpha
        {
            get
            {
                float t = (Time.unscaledTime - _startTime) / FadeSeconds;
                if (t < 0f || t >= 1f)
                    return 0f;
                return 1f - Mathf.Abs(t * 2f - 1f);
            }
        }

        private void OnGUI()
        {
            float alpha = Alpha;
            if (alpha <= 0f)
                return;

            if (_black == null)
            {
                _black = new Texture2D(1, 1);
                _black.SetPixel(0, 0, Color.black);
                _black.Apply();
            }

            /* Lower depth draws on top: the fade must cover the HUD lines too. */
            GUI.depth = -100;
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black);
            GUI.color = previous;
        }
    }
}
