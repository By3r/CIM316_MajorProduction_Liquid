using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liquid.NPC
{
    [CreateAssetMenu(menuName = "Liquid/NPC/NPC Social Config", fileName = "NPCSocial_")]
    public sealed class NpcSocialConfig : ScriptableObject
    {
        [Serializable]
        public sealed class NpcOpinion
        {
            [SerializeField] private NpcDefinition otherNpc;
            [Tooltip("Positive = likes, Negative = dislikes, 0 = neutral.")]
            [SerializeField] private int affinity;
            [TextArea(2, 5)]
            [SerializeField] private string notes;

            public NpcDefinition OtherNpc => otherNpc;
            public int Affinity => affinity;
            public string Notes => notes;

            public bool IsValid => otherNpc != null;
        }

        [Serializable]
        public sealed class TopicFriendshipRule
        {
            [SerializeField] private NpcTopicTag topic;
            [Tooltip("How many points gained if the player chooses a 'good' choice about this topic.")]
            [SerializeField] private int pointsGain = 5;
            [Tooltip("How many points lost if the player chooses a 'bad' choice about this topic.")]
            [SerializeField] private int pointsLoss = 5;

            public NpcTopicTag Topic => topic;
            public int PointsGain => pointsGain;
            public int PointsLoss => pointsLoss;

            public bool IsValid => topic != null;
        }

        [Header("Owner")]
        [SerializeField] private NpcDefinition owner;

        [Header("NPC -> NPC Opinions")]
        [SerializeField] private List<NpcOpinion> opinions = new();

        [Header("Player Friendship Tuning")]
        [Tooltip("Override how many points this NPC usually gives/loses for specific topics.")]
        [SerializeField] private List<TopicFriendshipRule> topicRules = new();

        [Header("Fallback Values")]
        [SerializeField] private int defaultGain = 3;
        [SerializeField] private int defaultLoss = 3;

        public NpcDefinition Owner => owner;
        public IReadOnlyList<NpcOpinion> Opinions => opinions;

        public int DefaultGain => defaultGain;
        public int DefaultLoss => defaultLoss;

        public bool TryGetOpinion(NpcDefinition otherNpc, out int affinity)
        {
            affinity = 0;
            if (otherNpc == null) return false;

            for (int i = 0; i < opinions.Count; i++)
            {
                var op = opinions[i];
                if (op != null && op.IsValid && op.OtherNpc == otherNpc)
                {
                    affinity = op.Affinity;
                    return true;
                }
            }
            return false;
        }

        public int GetGainForTopic(NpcTopicTag topic)
        {
            if (topic == null) return defaultGain;

            for (int i = 0; i < topicRules.Count; i++)
            {
                var r = topicRules[i];
                if (r != null && r.IsValid && r.Topic == topic)
                    return r.PointsGain;
            }

            return defaultGain;
        }

        public int GetLossForTopic(NpcTopicTag topic)
        {
            if (topic == null) return defaultLoss;

            for (int i = 0; i < topicRules.Count; i++)
            {
                var r = topicRules[i];
                if (r != null && r.IsValid && r.Topic == topic)
                    return r.PointsLoss;
            }

            return defaultLoss;
        }

        private void OnValidate()
        {
            opinions ??= new List<NpcOpinion>();
            topicRules ??= new List<TopicFriendshipRule>();
            if (defaultGain < 0) defaultGain = 0;
            if (defaultLoss < 0) defaultLoss = 0;
        }
    }
}