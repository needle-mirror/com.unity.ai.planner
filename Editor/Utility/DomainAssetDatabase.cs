using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.Utility
{
    static class DomainAssetDatabase
    {
        static IEnumerable<TraitDefinition> s_TraitDefinitions = null;
        static IEnumerable<ActionDefinition> s_ActionDefinitions = null;
        static IEnumerable<EnumDefinition> s_EnumDefinitions = null;
        static IEnumerable<AgentDefinition> s_AgentDefinitions = null;
        static IEnumerable<StateTerminationDefinition> s_StateTerminationDefinitions = null;

        public static IEnumerable<TraitDefinition> TraitDefinitions
        {
            get
            {
                if (s_TraitDefinitions == null)
                {
                    UpdateTraitDefinitions();
                }

                return s_TraitDefinitions;
            }
        }

        public static IEnumerable<ActionDefinition> ActionDefinitions
        {
            get
            {
                if (s_ActionDefinitions == null)
                {
                    UpdateActionDefinitions();
                }

                return s_ActionDefinitions;
            }
        }

        public static IEnumerable<EnumDefinition> EnumDefinitions
        {
            get
            {
                if (s_EnumDefinitions == null)
                {
                    UpdateEnumDefinitions();
                }

                return s_EnumDefinitions;
            }
        }

        public static IEnumerable<StateTerminationDefinition> StateTerminationDefinitions
        {
            get
            {
                if (s_StateTerminationDefinitions == null)
                {
                    UpdateTerminationDefinitions();
                }

                return s_StateTerminationDefinitions;
            }
        }

        public static IEnumerable<AgentDefinition> AgentDefinitions
        {
            get
            {
                if (s_AgentDefinitions == null)
                {
                    UpdateAgentDefinitions();
                }

                return s_AgentDefinitions;
            }
        }

        public static void Refresh()
        {
            UpdateEnumDefinitions();
            UpdateTraitDefinitions();
            UpdateActionDefinitions();
            UpdateAgentDefinitions();
            UpdateTerminationDefinitions();
        }

        private static void UpdateEnumDefinitions()
        {
            s_EnumDefinitions = AssetDatabase.FindAssets($"t: {nameof(EnumDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<EnumDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        private static void UpdateActionDefinitions()
        {
            s_ActionDefinitions = AssetDatabase.FindAssets($"t: {nameof(ActionDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<ActionDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        private static void UpdateTraitDefinitions()
        {
            s_TraitDefinitions = AssetDatabase.FindAssets($"t: {nameof(TraitDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<TraitDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        private static void UpdateAgentDefinitions()
        {
            s_AgentDefinitions = AssetDatabase.FindAssets($"t: {nameof(AgentDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<AgentDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        private static void UpdateTerminationDefinitions()
        {
            s_StateTerminationDefinitions = AssetDatabase.FindAssets($"t: {nameof(StateTerminationDefinition)}").Select(guid =>
                AssetDatabase.LoadAssetAtPath<StateTerminationDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }
    }
}
