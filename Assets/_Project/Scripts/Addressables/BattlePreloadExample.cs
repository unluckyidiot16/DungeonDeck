using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class BattlePreloadWeightedExample : MonoBehaviour
{
    [Header("Labels")]
    public string chapterLabel = "chapter_01";
    public string oathLabel = "oath_a";

    [Header("UI")]
    public UnityEngine.UI.Slider progressBar;
    public TMPro.TextMeshProUGUI progressText;

    [Header("Tuning")]
    [Range(0f, 0.2f)]
    public float sizingPhasePortion = 0.05f; // 사이즈 계산 단계가 바를 조금 채우도록 (0이면 다운로드부터 0~1)

    private class Step
    {
        public string tag;
        public List<object> keys;
        public Addressables.MergeMode mergeMode;
        public long sizeBytes;
    }

    private void Start()
    {
        Caching.ClearCache();
        
        StartCoroutine(PreloadBattleWeighted());
    }

    private IEnumerator PreloadBattleWeighted()
    {
        // 0) Init
        SetOverall(0f, "Init");
        var init = Addressables.InitializeAsync();
        yield return init;
        
        if (init.IsValid())
            Addressables.Release(init);

        // 1) Steps 정의
        var steps = new List<Step>
        {
            new Step
            {
                tag = "Player",
                keys = new List<object> { "type_player" },
                mergeMode = Addressables.MergeMode.Union
            },
            new Step
            {
                tag = $"Enemies({chapterLabel})",
                keys = new List<object> { "type_enemy", chapterLabel },
                mergeMode = Addressables.MergeMode.Intersection
            },
            new Step
            {
                tag = $"CardArt({oathLabel})",
                keys = new List<object> { "type_cardart", oathLabel },
                mergeMode = Addressables.MergeMode.Intersection
            }
        };

        // 2) Size 계산 (가중치 산출)
        float sizingBase = 0f;
        float sizingTarget = Mathf.Clamp01(sizingPhasePortion);

        for (int i = 0; i < steps.Count; i++)
        {
            int idx = i;

            // ✅ DownloadDependenciesCo -> GetDownloadSizeCo 로 교체
            yield return AddressablesPreloadService.GetDownloadSizeCo(
                keys: steps[idx].keys,
                mergeMode: steps[idx].mergeMode,
                onSizeBytes: s => steps[idx].sizeBytes = (s < 0) ? 0 : s,
                onError: e => Debug.LogError(e)
            );

            float p = (idx + 1) / (float)steps.Count;
            SetOverall(Mathf.Lerp(sizingBase, sizingTarget, p), $"Sizing: {steps[idx].tag}");
        }


        long totalBytes = 0;
        for (int i = 0; i < steps.Count; i++) totalBytes += steps[i].sizeBytes;

        // 모든게 이미 캐시에 있어서 totalBytes=0이면, "균등 가중치"로 진행률 계산
        bool useEqualWeights = totalBytes <= 0;
        float downloadBase = sizingTarget;
        float downloadRange = 1f - downloadBase;

        if (useEqualWeights)
        {
            Debug.Log("[Preload] All cached (totalBytes=0). Using equal-weight progress.");
        }
        else
        {
            Debug.Log($"[Preload] Total download size: {FormatBytes(totalBytes)}");
            for (int i = 0; i < steps.Count; i++)
            {
                Debug.Log($"[Preload]  - {steps[i].tag}: {FormatBytes(steps[i].sizeBytes)}");
            }
        }

        // 3) 다운로드(캐시 저장) + “누적 프로그레스” 업데이트
        long doneBytes = 0;

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            float stepWeight01 = useEqualWeights ? (1f / steps.Count) : (step.sizeBytes / (float)totalBytes);

            // stepSize가 0인 경우도 있을 수 있음 (이미 캐시됨)
            // 이 경우 진행률은 즉시 다음 단계로 넘어가며 전체 바도 자연스럽게 넘어간다.
            float stepStartOverall = useEqualWeights
                ? (i / (float)steps.Count)
                : (doneBytes / (float)totalBytes);

            yield return AddressablesPreloadService.DownloadDependenciesCo(
                keys: step.keys,
                mergeMode: step.mergeMode,
                onProgress01: pStep =>
                {
                    float overall01;

                    if (useEqualWeights)
                    {
                        // 균등 가중치: 이전 단계까지 + 현재 단계 진행률 * stepWeight
                        overall01 = (i + Mathf.Clamp01(pStep)) / steps.Count;
                    }
                    else
                    {
                        // 용량 가중치: doneBytes + pStep * stepBytes
                        float numer = doneBytes + Mathf.Clamp01(pStep) * step.sizeBytes;
                        overall01 = (totalBytes <= 0) ? 1f : (numer / totalBytes);
                    }

                    float ui = downloadBase + overall01 * downloadRange;
                    SetOverall(ui, $"{step.tag}: {(int)(Mathf.Clamp01(pStep) * 100f)}%");
                },
                onDone: () =>
                {
                    Debug.Log($"[Preload] Download done: {step.tag}");
                },
                onError: e =>
                {
                    Debug.LogError($"[Preload] Download failed: {step.tag}\n{e}");
                },
                autoReleaseHandle: true
            );

            // 단계 완료 후 doneBytes 누적
            if (!useEqualWeights) doneBytes += step.sizeBytes;

            // 단계 끝났을 때 바를 한 번 더 정리
            float doneOverall = useEqualWeights ? ((i + 1) / (float)steps.Count) : (doneBytes / (float)totalBytes);
            SetOverall(downloadBase + doneOverall * downloadRange, $"Done: {step.tag}");
        }

        SetOverall(1f, "Preload Done");
        Debug.Log("[Preload] ALL done.");
    }

    private void SetOverall(float p01, string msg)
    {
        p01 = Mathf.Clamp01(p01);
        if (progressBar) progressBar.value = p01;
        if (progressText) progressText.text = $"{msg}\n{(int)(p01 * 100f)}%";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.0} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.00} MB";
        double gb = mb / 1024.0;
        return $"{gb:0.00} GB";
    }
}
