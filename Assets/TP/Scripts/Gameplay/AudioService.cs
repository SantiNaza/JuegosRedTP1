using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>Música y efectos de sonido con dos AudioSources (sin un AudioSource por entidad).</summary>
    public sealed class AudioService : IAudioService
    {
        private readonly PresentationConfig config;
        private readonly AudioSource music;
        private readonly AudioSource sfx;
        private float lastShootTime = -1f;

        public AudioService(GameObject host, PresentationConfig config)
        {
            this.config = config;

            music = host.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.volume = config.musicVolume;

            sfx = host.AddComponent<AudioSource>();
            sfx.loop = false;
            sfx.playOnAwake = false;
            // Los sonidos siguen sonando aunque el juego esté pausado (clicks de la UI).
            sfx.ignoreListenerPause = true;
        }

        public void PlayMenuMusic() => PlayMusic(config.menuMusic);
        public void PlayGameplayMusic() => PlayMusic(config.gameplayMusic);

        public void Play(SfxType type)
        {
            switch (type)
            {
                case SfxType.Shoot:
                    // Limitamos la frecuencia para no saturar el mixer con disparos superpuestos.
                    if (Time.unscaledTime - lastShootTime < 0.05f) return;
                    lastShootTime = Time.unscaledTime;
                    PlayOneShot(config.shootClip, 0.5f);
                    break;
                case SfxType.UiClick: PlayOneShot(config.uiClickClip, 0.8f); break;
                case SfxType.WaveStart: PlayOneShot(config.waveStartClip, 0.8f); break;
                case SfxType.Explosion: PlayOneShot(config.explosionClip != null ? config.explosionClip : config.shootClip, 1f); break;
            }
        }

        private void PlayMusic(AudioClip clip)
        {
            if (clip == null || (music.clip == clip && music.isPlaying)) return;
            music.clip = clip;
            music.Play();
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null) sfx.PlayOneShot(clip, volume);
        }
    }
}
