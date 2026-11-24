using System;
using System.Collections.Generic;
using UnityEngine;
using TP.Core;

namespace TP.Config
{
    [Serializable]
    public struct VfxEntry
    {
        public VfxType type;
        public GameObject prefab;
        public float lifetime;
        public int prewarm;
    }

    [CreateAssetMenu(menuName = "TP/Vfx Config", fileName = "VfxConfig")]
    public sealed class VfxConfig : ScriptableObject
    {
        public List<VfxEntry> effects = new List<VfxEntry>();
    }
}
