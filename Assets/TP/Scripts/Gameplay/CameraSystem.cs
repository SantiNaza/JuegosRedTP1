using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>Cámara top-down que sigue al jugador (LateTick). En el menú muestra el mapa desde arriba.</summary>
    public sealed class CameraSystem : ILateUpdatable
    {
        private readonly Camera camera;
        private readonly Transform cameraTr;
        private readonly PresentationConfig config;
        private readonly IPlayerService player;
        private readonly Vector3 overviewPoint;

        public CameraSystem(Camera camera, PresentationConfig config, IPlayerService player, Vector3 overviewPoint)
        {
            this.camera = camera;
            cameraTr = camera != null ? camera.transform : null;
            this.config = config;
            this.player = player;
            this.overviewPoint = overviewPoint;
            SnapTo(overviewPoint, 1.6f);
        }

        public void LateTick(float deltaTime)
        {
            if (cameraTr == null) return;

            bool following = player.IsSpawned;
            Vector3 focus = following ? player.Position : overviewPoint;
            float zoom = following ? 1f : 1.6f;
            Vector3 desired = focus + config.cameraOffset * zoom;

            // Suavizado independiente del framerate.
            float t = 1f - Mathf.Exp(-config.cameraFollowSharpness * deltaTime);
            cameraTr.position = Vector3.Lerp(cameraTr.position, desired, t);
            cameraTr.rotation = Quaternion.LookRotation(-config.cameraOffset);
        }

        public void SnapTo(Vector3 focus, float zoom)
        {
            if (cameraTr == null) return;
            cameraTr.position = focus + config.cameraOffset * zoom;
            cameraTr.rotation = Quaternion.LookRotation(-config.cameraOffset);
        }

        public Camera Camera => camera;
    }
}
