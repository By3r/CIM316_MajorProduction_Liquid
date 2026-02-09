using UnityEngine;

namespace Liquid.NPC
{
    [CreateAssetMenu(menuName = "Liquid/NPC/Topic Tag", fileName = "TopicTag_")]
    public sealed class NpcTopicTag : ScriptableObject
    {
        [SerializeField] private string displayName;

        public string DisplayName => displayName;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
        }
    }
}