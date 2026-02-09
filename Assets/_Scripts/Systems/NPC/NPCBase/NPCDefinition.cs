using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liquid.NPC
{
    [CreateAssetMenu(menuName = "Liquid/NPC/NPC Definition", fileName = "NPC_")]
    public sealed class NpcDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string npcId = Guid.Empty.ToString();
        [SerializeField] private string displayName;
        [SerializeField, Min(0)] private int age;

        [Header("Role")]
        [SerializeField] private OccupationType occupation;
        [SerializeField] private OccupationProficiency occupationProficiency;
        [SerializeField] private OpennessLevel openness;

        [Header("Personality Topics")]
        [Tooltip("Topics this NPC likes talking about / values.")]
        [SerializeField] private List<NpcTopicTag> interests = new();
        [Tooltip("Topics that annoy this NPC / they dislike.")]
        [SerializeField] private List<NpcTopicTag> dislikes = new();

        public string NpcId => npcId;
        public string DisplayName => displayName;
        public int Age => age;

        public OccupationType Occupation => occupation;
        public OccupationProficiency Proficiency => occupationProficiency;
        public OpennessLevel Openness => openness;

        public IReadOnlyList<NpcTopicTag> Interests => interests;
        public IReadOnlyList<NpcTopicTag> Dislikes => dislikes;

        /// <summary>
        /// NPCType derives from occupation for dialogue selection rules.
        /// (We keep it as a function so you can later remap without changing serialized data.)
        /// </summary>
        public OccupationType GetNpcType() => occupation;

        public bool LikesTopic(NpcTopicTag topic) => topic != null && interests.Contains(topic);
        public bool DislikesTopic(NpcTopicTag topic) => topic != null && dislikes.Contains(topic);

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(npcId) || npcId == Guid.Empty.ToString())
                npcId = Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;

            interests ??= new List<NpcTopicTag>();
            dislikes ??= new List<NpcTopicTag>();
        }
    }
}