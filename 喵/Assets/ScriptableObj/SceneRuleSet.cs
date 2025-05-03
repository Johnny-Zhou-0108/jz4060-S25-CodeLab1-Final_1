using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SceneRuleSet", menuName = "Story/Scene RuleSet")]
// SceneRuleSet defines the rules and restrictions for a specific scene.
// Not in use right now.
// Could be a future improvement to add more depth to scene interactions.
// With this and NPCRuleSet. Not relying on hardcoded rules.
// The game could be more dynamic and adaptable to player choices.
public class SceneRuleSet : ScriptableObject
{
    public string sceneId;
    public List<string> forbiddenActions = new List<string>();
}