using UnityEngine;

namespace TP.Config
{
    [CreateAssetMenu(menuName = "TP/Presentation Config", fileName = "PresentationConfig")]
    public sealed class PresentationConfig : ScriptableObject
    {
        public Vector3 cameraOffset = new Vector3(0f, 13f, -7.5f);
        public float cameraFollowSharpness = 10f;
        public Texture menuBackground;
        public AudioClip menuMusic;
        public AudioClip gameplayMusic;
        public AudioClip shootClip;
        public AudioClip uiClickClip;
        public AudioClip waveStartClip;
        public AudioClip explosionClip;
        [Range(0f, 1f)] public float musicVolume = 0.35f;
    }

}
