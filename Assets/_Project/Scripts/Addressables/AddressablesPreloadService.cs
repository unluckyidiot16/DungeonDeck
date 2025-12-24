using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public static class AddressablesPreloadService
{
    
    private static void SafeRelease<T>(AsyncOperationHandle<T> h)
    {
        if (h.IsValid()) Addressables.Release(h);
    }

    // ✅ non-generic handle용 SafeRelease 추가
    private static void SafeRelease(AsyncOperationHandle h)
    {
        if (h.IsValid()) Addressables.Release(h);
    }
    
    public static IEnumerator GetDownloadSizeCo(
        IList<object> keys,
        Addressables.MergeMode mergeMode,
        Action<long> onSizeBytes,
        Action<Exception> onError = null)
    {
        // 1) mergeMode를 적용해 실제 대상 로케이션을 만든다
        var locH = Addressables.LoadResourceLocationsAsync(keys, mergeMode);
        yield return locH;

        if (locH.Status != AsyncOperationStatus.Succeeded)
        {
            onError?.Invoke(locH.OperationException ?? new Exception("LoadResourceLocationsAsync failed."));
            Addressables.Release(locH);
            yield break;
        }

        // 2) 로케이션 목록으로 다운로드 사이즈 계산 (이 오버로드는 거의 모든 버전에 존재)
        var sizeH = Addressables.GetDownloadSizeAsync(locH.Result);
        yield return sizeH;

        if (sizeH.Status == AsyncOperationStatus.Succeeded)
            onSizeBytes?.Invoke(sizeH.Result);
        else
            onError?.Invoke(sizeH.OperationException ?? new Exception("GetDownloadSizeAsync(locations) failed."));
        
        SafeRelease(sizeH);
        SafeRelease(locH);
    }

    public static IEnumerator DownloadDependenciesCo(
        IList<object> keys,
        Addressables.MergeMode mergeMode,
        Action<float> onProgress01 = null,
        Action onDone = null,
        Action<Exception> onError = null,
        bool autoReleaseHandle = true // 호출부 호환용(내부에선 무시하고 수동 릴리즈)
    )
    {
        // 1) mergeMode 적용 로케이션 생성
        var locH = Addressables.LoadResourceLocationsAsync(keys, mergeMode);
        yield return locH;

        if (locH.Status != AsyncOperationStatus.Succeeded)
        {
            onError?.Invoke(locH.OperationException ?? new Exception("LoadResourceLocationsAsync failed."));
            SafeRelease(locH);
            yield break;
        }

        // ✅ 로케이션이 비어있으면 다운로드할 게 없음(바로 완료 처리)
        if (locH.Result == null || locH.Result.Count == 0)
        {
            onProgress01?.Invoke(1f);
            onDone?.Invoke();
            SafeRelease(locH);
            yield break;
        }

        // 2) 다운로드: ✅ autoRelease는 끄고(항상 false) 우리가 상태 확인 후 릴리즈
        var dlH = Addressables.DownloadDependenciesAsync(locH.Result, autoReleaseHandle: false);

        while (!dlH.IsDone)
        {
            onProgress01?.Invoke(dlH.PercentComplete);
            yield return null;
        }

        // ✅ 이제 dlH는 여전히 유효하므로 Status/Exception 접근 안전
        if (dlH.Status == AsyncOperationStatus.Succeeded)
        {
            onProgress01?.Invoke(1f);
            onDone?.Invoke();
        }
        else
        {
            onError?.Invoke(dlH.OperationException ?? new Exception("DownloadDependenciesAsync(locations) failed."));
        }

        SafeRelease(dlH);
        SafeRelease(locH);
    }
}
