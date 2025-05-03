using UnityEngine;

[CreateAssetMenu(fileName = "ChapterProgressData", menuName = "Story/Chapter Progress Data")]
// ChapterProgressData stores the progress of the story, including the current chapter and any relevant data.
public class ChapterProgressData : ScriptableObject
{
    public string chapterTitle;
    [TextArea(10, 100)]
    public string storyContent;
}