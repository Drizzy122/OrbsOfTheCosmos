using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Authors the first-mission (KillEnemiesQuest) conversation with full
    /// dialogue: multiple lines per section and multiple player choices.
    /// Re-running resets the asset to this content, discarding inspector edits.
    /// </summary>
    public static class FirstMissionDialogueBuilder
    {
        const string AssetPath = "Assets/_Project/Dialogue/KillEnemiesConversation.asset";
        const string Speaker = "Elder Vesper";

        [MenuItem("Tools/Dialogue/Build First Mission Dialogue")]
        public static void Run()
        {
            DialogueConversationSO convo = AssetDatabase.LoadAssetAtPath<DialogueConversationSO>(AssetPath);
            if (convo == null)
            {
                Debug.LogError($"[FirstMissionDialogue] Asset not found at {AssetPath}");
                return;
            }

            if (convo.quest == null)
            {
                convo.quest = FindQuest("KillEnemiesQuest");
            }

            convo.requirementsNotMet = Section(
                Lines(
                    "Mm? You're eager, little wing — I can hear it in your heartbeat. But eagerness alone won't keep you alive out there.",
                    "Grow a little stronger first. Then we'll talk."));

            convo.canStart = WithRepeat(Section(
                Lines(
                    "Ah... Lyo. Come closer, let these old ears have a look at you.",
                    "You've heard the skittering at night, haven't you? The island across the water — it's crawling with bugs and plague bats now.",
                    "They've already chewed through half our supply caves. If no one thins them out, the village won't last the season.",
                    "You're young, but you fly better than anyone I've seen. Will you help us?"),
                Choice("What exactly am I dealing with?", DialogueQuestAction.None,
                    "Bugs the size of your head, and plague bats — twisted, sickly things. They were ordinary creatures once, before the island changed.",
                    "Some say it started when the old Orb was lost there. But that's a story for another night.",
                    "Come back when you're ready to give me an answer."),
                Choice("Is there anything in it for me?", DialogueQuestAction.None,
                    "Ha! Spoken like a true forager. Twenty-five coins from the village chest, and you'll come back tougher than you left — that I can promise.",
                    "Well? Do we have a deal or not, little wing?"),
                Choice("I'll clear the island.", DialogueQuestAction.StartQuest,
                    "I knew there was steel under those wings.",
                    "Glide across when the water is calm, and trust your ears — your echo will find them before your eyes do.",
                    "Thin them out and come straight back to me. Don't linger after dark."),
                Choice("Not right now.", DialogueQuestAction.None,
                    "Hmph. Honesty is better than bravado — I'll give you that.",
                    "The island isn't going anywhere. Sadly. Come back when you find your nerve.")),
                "Back again, little wing? The island still crawls — and my offer still stands.");

            convo.inProgress = WithRepeat(Section(
                Lines(
                    "Still breathing? Good. The island hasn't beaten you yet.",
                    "Trust your echo, strike before they swarm, and don't let the plague bats corner you against the cliffs."),
                Choice("Tell me about the Orb you mentioned.", DialogueQuestAction.None,
                    "Curious little thing, aren't you. Long ago this valley was watched over by a great Orb of light — until it was carried off and lost on that very island.",
                    "Some nights, when the water is still, you can see a faint glow through the mist. Old eyes playing tricks... or maybe not.",
                    "Finish the hunt first, Lyo. Legends keep better than villages do."),
                Choice("Just checking in.", DialogueQuestAction.None,
                    "Then off with you — every hour we wait, they multiply.")),
                "The hunt's not done, little wing. What do you need?");

            convo.canFinish = WithRepeat(Section(
                Lines(
                    "The night sounds... quiet. Quieter than it's been in months.",
                    "You actually did it, didn't you? The swarm is broken!",
                    "Come here, let me look at you — not a scratch worth crying over. Incredible."),
                Choice("The island is clear.", DialogueQuestAction.FinishQuest,
                    "Wings of my wings, you've saved us all. Take this — twenty-five coins, and worth every one.",
                    "Rest tonight, Lyo. You've earned it.",
                    "And come see me tomorrow. There's something glowing out on that island... something I want you to see for yourself."),
                Choice("Not yet — I want to double-check the island.", DialogueQuestAction.None,
                    "Careful is a fine way to stay alive. Return when you're certain.")),
                "Ready to make it official, little wing?");

            convo.finished = Section(
                Lines(
                    "There's a lightness in the village I haven't felt in years. That's your doing, little wing.",
                    "Now — about those glowing elumins on the island. They won't gather themselves..."),
                Choice("Just passing through.", DialogueQuestAction.None,
                    "Then pass through often. The village sleeps easier with you around."));

            convo.fallback = Section(
                Lines("Hm? Speak up, little wing — these ears are old."),
                Choice("Never mind.", DialogueQuestAction.None));

            EditorUtility.SetDirty(convo);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FirstMissionDialogue] Rebuilt '{AssetPath}' (speaker: {Speaker}).");
        }

        // ── Builders ─────────────────────────────────────────────────────────

        static DialogueLine[] Lines(params string[] texts) =>
            texts.Select(t => new DialogueLine { speaker = Speaker, text = t }).ToArray();

        static DialogueChoice Choice(string text, DialogueQuestAction action, params string[] responses) =>
            new DialogueChoice
            {
                text = text,
                action = action,
                responseLines = responses.Select(t => new DialogueLine { speaker = Speaker, text = t }).ToList()
            };

        static DialogueSection Section(DialogueLine[] lines, params DialogueChoice[] choices) =>
            new DialogueSection
            {
                lines = lines.ToList(),
                choices = choices.ToList()
            };

        static DialogueSection WithRepeat(DialogueSection section, params string[] repeatTexts)
        {
            section.repeatLines = repeatTexts.Select(t => new DialogueLine { speaker = Speaker, text = t }).ToList();
            return section;
        }

        static QuestInfoSO FindQuest(string questId)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:QuestInfoSO"))
            {
                var quest = AssetDatabase.LoadAssetAtPath<QuestInfoSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (quest != null && quest.id == questId) return quest;
            }
            Debug.LogWarning($"[FirstMissionDialogue] QuestInfoSO '{questId}' not found — link it manually.");
            return null;
        }
    }
}
