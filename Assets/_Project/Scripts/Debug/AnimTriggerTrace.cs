using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DungeonDeck.Debugging
{
    public static class AnimTriggerTrace
    {
        public static bool Enabled = false;

        // Trigger name filter
        public static bool OnlySpecificTrigger = true;
        public static string TriggerName = "Attack";

        // Animator filter (optional)
        public static Animator[] FilterAnimators = null; // null => all

        // Stack trace
        public static bool IncludeStackTrace = true;
        public static bool FullStackTrace = false;

        // Internal counter so logs don’t collapse too aggressively
        private static int _seq = 0;

        public static void Configure(
            bool enabled,
            bool onlySpecificTrigger,
            string triggerName,
            bool includeStackTrace,
            bool fullStackTrace,
            Animator[] filterAnimators)
        {
            Enabled = enabled;
            OnlySpecificTrigger = onlySpecificTrigger;
            TriggerName = string.IsNullOrEmpty(triggerName) ? "Attack" : triggerName;
            IncludeStackTrace = includeStackTrace;
            FullStackTrace = fullStackTrace;
            FilterAnimators = filterAnimators;
        }

        public static void ResetAndSetTrigger(Animator anim, string trigger, Object ctx = null)
        {
            if (anim == null || string.IsNullOrEmpty(trigger)) return;

            // Reset is often used; we don’t log reset by default (noise),
            // but we DO log the SetTrigger with stack trace.
            anim.ResetTrigger(trigger);
            SetTrigger(anim, trigger, ctx);
        }

        public static void SetTrigger(Animator anim, string trigger, Object ctx = null)
        {
            if (anim == null || string.IsNullOrEmpty(trigger)) return;

            if (Enabled && PassFilter(anim, trigger))
                Log(anim, trigger, ctx);

            anim.SetTrigger(trigger);
        }

        private static bool PassFilter(Animator anim, string trigger)
        {
            if (!Enabled) return false;

            if (OnlySpecificTrigger && !string.Equals(trigger, TriggerName, StringComparison.Ordinal))
                return false;

            if (FilterAnimators != null && FilterAnimators.Length > 0)
            {
                bool ok = false;
                for (int i = 0; i < FilterAnimators.Length; i++)
                {
                    if (FilterAnimators[i] == anim) { ok = true; break; }
                }
                if (!ok) return false;
            }

            return true;
        }

        private static void Log(Animator anim, string trigger, Object ctx)
        {
            int id = ++_seq;
            string animName = anim != null ? anim.gameObject.name : "(null)";
            string ctxName = ctx != null ? ctx.name : "(no ctx)";

            var sb = new StringBuilder(512);
            sb.Append($"[AnimTriggerTrace#{id}] frame={Time.frameCount} t={Time.time:F3} ");
            sb.Append($"anim={animName} trig={trigger} ctx={ctxName}");

            if (IncludeStackTrace)
            {
                sb.AppendLine();
                sb.Append(BuildStack());
            }

            UnityEngine.Debug.Log(sb.ToString(), ctx);
        }

        private static string BuildStack()
        {
            // skipFrames:
            // 0 = this method
            // 1 = Log
            // 2 = SetTrigger
            // 3 = ResetAndSetTrigger (optional)
            // so 3~4 정도부터가 “진짜 호출자”
            var st = new StackTrace(4, true);

            if (FullStackTrace)
                return st.ToString();

            // Compact stack: show only frames that look like project code (Assets/)
            var sb = new StringBuilder(1024);
            int shown = 0;

            for (int i = 0; i < st.FrameCount; i++)
            {
                var f = st.GetFrame(i);
                var m = f.GetMethod();
                if (m == null) continue;

                string file = f.GetFileName();
                // file이 null이면 (IL2CPP/설정)이라도 메서드명은 찍힘
                bool isProject = !string.IsNullOrEmpty(file) && file.Contains("Assets");

                if (!isProject)
                {
                    // 파일 경로가 없더라도 DungeonDeck 네임스페이스면 보여주기
                    string decl = m.DeclaringType != null ? m.DeclaringType.FullName : "";
                    if (decl.Contains("DungeonDeck")) isProject = true;
                }

                if (!isProject) continue;

                string typeName = m.DeclaringType != null ? m.DeclaringType.FullName : "(no type)";
                string methodName = m.Name;

                int line = f.GetFileLineNumber();
                if (!string.IsNullOrEmpty(file))
                {
                    int idx = file.IndexOf("Assets", StringComparison.Ordinal);
                    if (idx >= 0) file = file.Substring(idx);
                }

                sb.Append("  at ").Append(typeName).Append(".").Append(methodName).Append("(");

                // 파라미터는 너무 길어져서 생략
                sb.Append(")");

                if (!string.IsNullOrEmpty(file))
                    sb.Append("  (").Append(file).Append(":").Append(line).Append(")");

                sb.AppendLine();

                shown++;
                if (shown >= 12) break;
            }

            if (shown == 0)
                sb.Append("  (no project frames; enable FullStackTrace or Script Debugging)");

            return sb.ToString();
        }
    }
}
