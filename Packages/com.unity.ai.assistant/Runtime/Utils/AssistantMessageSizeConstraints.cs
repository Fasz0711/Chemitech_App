using UnityEngine;

namespace Unity.AI.Assistant.Utils
{
    // This class will be removed in the near future. It existed originally to service a protocol field that was
    // bypassed and never actually used. We will delete this during a larger refactor.
    static class AssistantMessageSizeConstraints
    {
        // This is the maximum amount of characters the text field can currently handle
        // If we increase this further it will glitch out visually
        internal const int PromptLimit = 100000;
            
        // 5% of int.MaxValue is reserved for JSON overhead.
        internal const int MessageLimit = (int)(int.MaxValue * 0.95f);

        // Context gets what's left after the JSON-overhead reservation and the prompt budget.
        internal const int ContextLimit = MessageLimit - PromptLimit;

        // Returns the context space remaining once the prompt is accounted for, capped at
        // ContextLimit.
        public static int GetDynamicContextLimitForPrompt(string prompt)
        {
            return Mathf.Min(MessageLimit - prompt.Length, ContextLimit);
        }
    }
}
