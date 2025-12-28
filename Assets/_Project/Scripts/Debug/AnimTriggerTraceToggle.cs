using UnityEngine;

namespace DungeonDeck.Debugging
{
    public class AnimTriggerTraceToggle : MonoBehaviour
    {
        [Header("Master")]
        public bool enable = true;

        [Header("Filter")]
        public bool onlySpecificTrigger = true;
        public string triggerName = "Attack";
        public Animator[] filterAnimators; // 비워두면 전체 Animator 대상

        [Header("Stack Trace")]
        public bool includeStackTrace = true;
        public bool fullStackTrace = false;

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) Apply();
        }

        [ContextMenu("Apply Trace Config")]
        public void Apply()
        {
            AnimTriggerTrace.Configure(
                enable,
                onlySpecificTrigger,
                triggerName,
                includeStackTrace,
                fullStackTrace,
                filterAnimators
            );

            Debug.Log($"[AnimTriggerTraceToggle] enable={enable} trigger={triggerName} onlySpecific={onlySpecificTrigger} fullStack={fullStackTrace}", this);
        }
    }
}