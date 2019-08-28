using UnityEditor.AI.Planner.Utility;
using UnityEngine.AI.Planner.DomainLanguage.TraitBased;

namespace UnityEditor.AI.Planner.CodeGen
{
    class DomainAssetPostProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths)
            {
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);

                if (typeof(TraitDefinition) == type
                    || typeof(EnumDefinition) == type
                    || typeof(ActionDefinition) == type
                    || typeof(StateTerminationDefinition) == type
                    || typeof(AgentDefinition) == type
                    )
                {
                    // TODO: Rebuild Domain or set Assembly dirty
                    DomainAssetDatabase.Refresh();
                }
            }
            return paths;
        }

        static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions option)
        {
            // TODO: Rebuild Domain or set Assembly dirty

            return AssetDeleteResult.DidNotDelete;
        }
    }
}
