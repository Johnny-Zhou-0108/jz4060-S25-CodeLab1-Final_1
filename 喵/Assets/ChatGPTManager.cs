using System.Collections.Generic;
using UnityEngine;
using OpenAI;
using UnityEngine.Events;
using TMPro;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChatGPTManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField inputField;
    public TMP_Text outputText;

    [Header("GPT Settings")]
    public OnResponseEvent OnResponse;

    [System.Serializable]
    public class OnResponseEvent : UnityEvent<string> { }

    private OpenAIApi openAI = new OpenAIApi();
    private List<ChatMessage> messages = new List<ChatMessage>();

    [Header("Prompt Config")]
    [TextArea(10, 30)]
    public string gptPersonalityDescription;

    [Header("Story Data")]
    public ChapterData chapterData;
    public ChapterProgressData playerProgressData;
    public GameStateData gameStateData;

    [Header("Auto Generated Data")]
    public List<NPCData> allNpcData = new List<NPCData>();
    public List<SceneData> allSceneData = new List<SceneData>();
    public List<ItemData> allItemData = new List<ItemData>();

    [Header("Story Progression")]
    public int totalRounds = 10;
    private List<string> playerInputs = new List<string>();
    private List<string> aiResponses = new List<string>();
    private List<string> playerBehaviorSummaries = new List<string>();

    private bool isStoryInitialized = false;

    void Start()
    {
#if UNITY_EDITOR
        InitializeEditorData();
#endif

        inputField.onSubmit.AddListener(OnInputSubmitted);
        inputField.ActivateInputField();

        InitializeStoryStructure();
    }

#if UNITY_EDITOR
    private void InitializeEditorData()
    {
        // import text from external file
        if (chapterData != null && chapterData.externalTextFile != null)
        {
            string path = AssetDatabase.GetAssetPath(chapterData.externalTextFile);
            chapterData.fullText = File.ReadAllText(path);
            EditorUtility.SetDirty(chapterData);
            AssetDatabase.SaveAssets();
        }
        // clear story content to start fresh
        if (playerProgressData != null)
        {
            playerProgressData.storyContent = "";
            EditorUtility.SetDirty(playerProgressData);
            AssetDatabase.SaveAssets();
        }
    }
#endif

    private async void InitializeStoryStructure()
    {
        if (isStoryInitialized || chapterData == null || string.IsNullOrEmpty(chapterData.fullText))
            return;
        // Initialize the story structure
        await GenerateSceneData();
        await GenerateNPCData();
        await GenerateItemData();
        InitializeGameStateData();

        Debug.Log("Initialize the story");
        isStoryInitialized = true;
    }

    private async Task GenerateSceneData()
    {
        // Generate 8 scenes from the story
        // Format, each line: Scene Name: Description: Available Actions (comma-separated)
        string prompt = @"You are the scene designer for 'The Little Prince'.
Please generate a list of 8 unique scenes from the story. 
Each scene must be written on a single line using this exact format: 

Scene Name: Description: Available Actions (comma-separated)

Make sure:
- Each line is unique and contains exactly two colons.
- Scene Name should reflect a specific moment from the story.
- Do not repeat the placeholder 'Scene Name', 'Description', or 'Available Actions'.
";

        var scenes = await RequestGptList(prompt);
        Debug.Log($"GPT returned SceneData:\n{scenes}");

#if UNITY_EDITOR
        allSceneData.Clear();
        // Format each line and create a SceneData object
        foreach (var line in scenes.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(':');
            if (parts.Length < 3)
            {
                Debug.LogWarning("Invalid scene format, skipping: " + line);
                continue;
            }

            var scene = ScriptableObject.CreateInstance<SceneData>();
            scene.sceneName = parts[0].Trim();
            scene.description = parts[1].Trim();
            scene.availableActions = parts[2].Split(',').Select(x => x.Trim()).ToList();
            scene.connectedScenes = new List<string>();
            scene.name = scene.sceneName;

            SaveAsset(scene, scene.sceneName);
            allSceneData.Add(scene);
        }
#endif
    }


    private async Task GenerateNPCData()
    {
        string prompt = @"You are the character designer for 'The Little Prince'. 
Please list all important characters, one per line, using the format: Name: Role: Initial Attitude: Initial Emotion. 
Use the colon ':' as the separator.
";
        
        var npcs = await RequestGptList(prompt);
        Debug.Log($"GPT returned NPCData:\n{npcs}");

#if UNITY_EDITOR
        allNpcData.Clear();
        foreach (var line in npcs.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(':');
            if (parts.Length < 4) continue;

            var npc = ScriptableObject.CreateInstance<NPCData>();
            npc.npcName = parts[0].Trim();
            npc.role = parts[1].Trim();
            npc.initialAttitude = parts[2].Trim();
            npc.currentEmotion = parts[3].Trim();
            npc.memory = new List<string>();

            SaveAsset(npc, npc.npcName);
            allNpcData.Add(npc);
        }
#endif
    }

    private async Task GenerateItemData()
    {
        string prompt = @"You are the item designer for 'The Little Prince'. 
Please list all important items, one per line, using the format: Item Name: Description: Initial Location. 
Use the colon ':' as the separator.
";

        var items = await RequestGptList(prompt);
        Debug.Log($"GPT returned ItemData；\n{items}");

#if UNITY_EDITOR
        allItemData.Clear();
        foreach (var line in items.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(':');
            if (parts.Length < 3) continue;

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = parts[0].Trim();
            item.description = parts[1].Trim();
            item.location = parts[2].Trim();

            SaveAsset(item, item.itemName);
            allItemData.Add(item);
        }
#endif
    }

    private void InitializeGameStateData()
    {
#if UNITY_EDITOR
        if (gameStateData == null)
        {
            gameStateData = ScriptableObject.CreateInstance<GameStateData>();
            SaveAsset(gameStateData, "GameStateData");
        }

        gameStateData.currentLocation = allSceneData.FirstOrDefault()?.sceneName ?? "Unkown Location";
        gameStateData.visitedLocations = new List<string> { gameStateData.currentLocation };
        gameStateData.npcInteracted = new List<string>();
        gameStateData.itemsCollected = new List<string>();

        EditorUtility.SetDirty(gameStateData);
        AssetDatabase.SaveAssets();
#endif
    }

    private async Task<string> RequestGptList(string prompt)
    {
        var request = new CreateChatCompletionRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = @"You are a game design expert skilled at deconstructing literary works into gameplay elements." },
                new ChatMessage { Role = "user", Content = prompt }
            }
        };

        var response = await openAI.CreateChatCompletion(request);
        return response.Choices?[0].Message.Content.Trim() ?? "";
    }

#if UNITY_EDITOR
    private void SaveAsset(ScriptableObject asset, string assetName = null)
    {
        EnsureSaveFolderExists();
        string folderPath = "Assets/StorySnapshots";
        string path = $"{folderPath}/{(assetName ?? asset.name)}.asset";

        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return;
        }

        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
    }

    private void EnsureSaveFolderExists()
    {
        string folderPath = "Assets/StorySnapshots";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "StorySnapshots");
        }
    }
#endif

    private async void OnInputSubmitted(string userInput)
    {
        // Format the input
        userInput = userInput.Trim();
        if (string.IsNullOrEmpty(userInput)) return;

        // Add the input to the list
        playerInputs.Add(userInput);
        
        if (playerProgressData != null)
        {
            // Update the story scriptable obj with the player's input
            playerProgressData.storyContent += "\nLittle Prince:" + userInput;
#if UNITY_EDITOR
            EditorUtility.SetDirty(playerProgressData);
            AssetDatabase.SaveAssets();
#endif
        }
        //reset the input field
        await ProcessPlayerInput(userInput);

        inputField.text = "";
        inputField.ActivateInputField();
    }

    private async Task ProcessPlayerInput(string userInput)
    {
        string npcIntent = await AnalyzePlayerIntentForNPC(userInput);
        bool isNpcInteracted = false;
        // Player input is an interaction with the NPC
        if (npcIntent != "No Interaction")
        {
            await EngageNPCDialogue(npcIntent, userInput);
            isNpcInteracted = true;
        }

        if (!isNpcInteracted)
        {
            string behaviorSummary = await SummarizePlayerBehaviorGPT();
            playerBehaviorSummaries.Add(behaviorSummary);

            string dynamicPrompt = BuildDynamicPrompt(userInput, behaviorSummary);
            var aiReply = await GetGPTResponse(dynamicPrompt, userInput);
            HandleAIResponse(aiReply);
        }

    }

    private async Task<string> GetGPTResponse(string prompt, string userInput)
    {
        if (messages.Count == 0)
        {
            messages.Add(new ChatMessage
            {
                Role = "system",
                // this not matters, will be overridden by the dynamic prompt
                Content = @"
You are an interactive storytelling AI guiding the player through an immersive experience based on 'The Little Prince'.

- The player is 'The Little Prince'. You should respond as a narrator or NPC to his actions and dialogue.
- Do NOT generate any lines or inner monologues for the Little Prince.
- Always reply in the voice of the environment or other characters to move the story forward.
- Avoid reiterating character roles; jump directly into storytelling.
- Every reply must progress the story, e.g., new characters, changes in environment, plot events.

Style requirements:
- Language should be poetic and reflective of the fairytale tone of 'The Little Prince'.
- Avoid overly long explanations; keep replies natural and fluid.
- Limit response length to within 150 words.
"
            });
        }
        
        messages.Add(new ChatMessage { Role = "user", Content = userInput });

        var request = new CreateChatCompletionRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = messages
        };

        var response = await openAI.CreateChatCompletion(request);
        var reply = response.Choices?[0].Message.Content.Trim() ?? "No reply";
        
        messages.Add(new ChatMessage { Role = "assistant", Content = reply });
        // clear the messages to avoid exceeding the token limit
        if (messages.Count > 40)
        {
            messages.RemoveAt(1); 
            messages.RemoveAt(1); 
        }

        return reply;
    }

    private void HandleAIResponse(string aiReply)
    {
        if (string.IsNullOrWhiteSpace(aiReply) || aiReply == "No reply")
        {
            outputText.text += "\n\n[AI is not responding correctly. Please try again.]";
            return;
        }
        // Not sure how to end the story
        // There should be a flag in the AI response, in this case, END
        // But I have never reached the ending lol
        // LLMs are good for opened-ended stories, but not for closed ones
        // especially in novels-like narratives
        bool isStoryEnding = aiReply.Contains("[END]");
        aiReply = aiReply.Replace("[END]", "").Trim();

        aiResponses.Add(aiReply);
        outputText.text = aiReply;

        // Check if the story is ending
        if (isStoryEnding)
        {
            outputText.text += "\n\n[This is all about your unique Little Prince story.]";
            inputField.interactable = false;
        }

        UpdateStoryProgress(aiReply);
    }


    private void UpdateStoryProgress(string aiReply)
    {
        // Update the story scriptable obj with the AI's response
        if (playerProgressData != null)
        {
            playerProgressData.storyContent += "\nAI:" + aiReply;
#if UNITY_EDITOR
            EditorUtility.SetDirty(playerProgressData);
            AssetDatabase.SaveAssets();
#endif
        }

        var currentNpc = allNpcData.FirstOrDefault();
        if (currentNpc != null)
        {
            currentNpc.memory.Add($"AI:「{aiReply}」");
#if UNITY_EDITOR
            EditorUtility.SetDirty(currentNpc);
            AssetDatabase.SaveAssets();
#endif
        }
    }

    private async Task<string> AnalyzePlayerIntentForScene(string userInput)
    {
        // Not is use right now
        // The prompt is not accurate enough
        // The AI is not able to understand, sometimes will throw an exception back
        var request = new CreateChatCompletionRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = new List<ChatMessage>
            {
                new ChatMessage { 
                    Role = "system", 
                    Content = $"Analyze player input's intention，" +
                              $"Only return the scene name or 'No Switch'。" +
                              $"Available Scenes:{string.Join(",", allSceneData.Select(s => s.sceneName))}" 
                },
                new ChatMessage { Role = "user", Content = userInput }
            }
        };

        var response = await openAI.CreateChatCompletion(request);
        return response.Choices?[0].Message.Content.Trim() ?? "No Switch";
    }

    private bool IsSceneConnected(string targetScene)
    {
        var currentScene = allSceneData.FirstOrDefault(s => s.sceneName == gameStateData.currentLocation);
        return currentScene?.connectedScenes.Contains(targetScene) ?? false;
    }

    private void MovePlayerToScene(string sceneName)
    {
        gameStateData.currentLocation = sceneName;
        if (!gameStateData.visitedLocations.Contains(sceneName))
        {
            gameStateData.visitedLocations.Add(sceneName);
#if UNITY_EDITOR
            EditorUtility.SetDirty(gameStateData);
            AssetDatabase.SaveAssets();
#endif
        }
    }

    private string GetSceneDescription(string sceneName)
    {
        var scene = allSceneData.FirstOrDefault(s => s.sceneName == sceneName);
        return scene?.description ?? "This scene has no more description.";
    }

    private async Task<string> AnalyzePlayerIntentForNPC(string userInput)
    {
        var request = new CreateChatCompletionRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = new List<ChatMessage>
            //To see if it's an interaction with the NPC
            {
                new ChatMessage { 
                    Role = "system", 
                    Content = $"Analyze player input's intention，" +
                              $"Only return the NPC Name or 'No Interaction'。" +
                              $"Available NPC:{string.Join(",", allNpcData.Select(n => n.npcName))}" 
                },
                new ChatMessage { Role = "user", Content = userInput }
            }
        };

        var response = await openAI.CreateChatCompletion(request);
        return response.Choices?[0].Message.Content.Trim() ?? "No Interaction";
    }

    private async Task EngageNPCDialogue(string npcName, string userInput)
    {
        // get all NPC data
        var npc = allNpcData.FirstOrDefault(n => n.npcName == npcName);
        if (npc == null) return;

        // let GPT roleplay the NPC
        string prompt = $"You are {npc.npcName}，{npc.role}. " +
                        $"Your emotional state is:{npc.currentEmotion}，" +
                        $"your attitude towards the little prince is:{npc.initialAttitude}. " +
                        $"Memory:{string.Join(";", npc.memory)}";
        
        var aiReply = await GetGPTResponse(prompt, userInput);
        HandleAIResponse(aiReply);

        if (!gameStateData.npcInteracted.Contains(npcName))
        {
            gameStateData.npcInteracted.Add(npcName);
#if UNITY_EDITOR
            EditorUtility.SetDirty(gameStateData);
            AssetDatabase.SaveAssets();
#endif
        }
    }

    private async Task<string> SummarizePlayerBehaviorGPT()
    {
        // Summarize the player's behavior tendency
        var request = new CreateChatCompletionRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "Summarize the player's behavior tendency in less than 20 words." },
                new ChatMessage { Role = "user", Content = string.Join("\n", playerInputs) }
            }
        };

        var response = await openAI.CreateChatCompletion(request);
        return response.Choices?[0].Message.Content.Trim() ?? "Unknown behavior tendency.";
    }

    private string BuildDynamicPrompt(string userInput, string behaviorSummary)
    {
        var currentScene = allSceneData.FirstOrDefault(s => s.sceneName == gameStateData.currentLocation);
        var currentNpc = allNpcData.FirstOrDefault();
        // if it's not a scene switch or NPC interaction, build a robust dynamic prompt to handle the response
        // not using hardcoded responses, use rules to regulate and limit the AI
        // limit the response to 150 words
        return $@"
Story Background:
- Current Scene: {currentScene?.sceneName}, Description: {currentScene?.description}
- Available Actions: {string.Join(", ", currentScene?.availableActions ?? new List<string>())}

NPC Info:
- {currentNpc?.npcName}: {currentNpc?.role}, Current Emotion: {currentNpc?.currentEmotion}
- NPC Memory Snippets: {string.Join(";", currentNpc?.memory.TakeLast(3) ?? new List<string>())}

Player Input:
- The Little Prince’s action or dialogue: {userInput}

Player Behavior Summary:
- {behaviorSummary}

Your response guidelines:
- You are the narrator or an NPC in the current scene.
- Respond to the Little Prince's input and move the story forward.
- You may introduce scene changes, new characters, or events.
- Do NOT speak on behalf of the Little Prince.
- Keep the response in the poetic tone of 'The Little Prince', under 150 words.
";
    }

}