using UnityEngine;

namespace TP.Config
{
    [CreateAssetMenu(menuName = "TP/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public PlayerConfig player;
        public WeaponConfig weapon;
        public GrenadeConfig grenade;
        public WaveConfig waves;
        public StatsConfig stats;
        public PickupConfig pickups;
        public VfxConfig vfx;
        public PresentationConfig presentation;
        public MapData map;
        public PerformanceConfig performance;
    }
}
