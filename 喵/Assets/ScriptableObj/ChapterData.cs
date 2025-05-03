using UnityEngine;

[CreateAssetMenu(fileName = "ChapterData", menuName = "Story/Chapter Data")]
public class ChapterData : ScriptableObject
{
    public string chapterTitle;
    [TextArea(10, 100)]
    public string fullText;

    [Header("Read from disk file")]
    public TextAsset externalTextFile;

#if UNITY_EDITOR
    [ContextMenu("Txt")]
    public void ImportTextFromExternalFile()
    {
        if (externalTextFile != null)
        {
            fullText = externalTextFile.text;
            // Tell the Unity editor that this object has been modified
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"Successful {externalTextFile.name} Import");
        }
        else
        {
            Debug.LogWarning("No externalTextFile");
        }
    }
#endif
}