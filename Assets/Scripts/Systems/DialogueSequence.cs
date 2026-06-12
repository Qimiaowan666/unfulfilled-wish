using System;
using System.Collections.Generic;
using UnityEngine;

// 一段剧情台词数据（ScriptableObject）：在 Project 里右键 Create/Game/Dialogue Sequence 新建，
// 在 Inspector 里逐行填「说话人 + 台词」。供 DialogueUI / BossIntroTrigger 使用。
[Serializable]
public class DialogueLine
{
    public string speaker;
    [TextArea(2, 4)] public string text;
}

[CreateAssetMenu(fileName = "DialogueSequence", menuName = "Game/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    public List<DialogueLine> lines = new List<DialogueLine>();
}
