// Assets/_Project/Scripts/Battle/View/BattleStageSpawner.cs
// v2: 슬롯 인덱스 보존 바인딩 수정
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DungeonDeck.Run;
using DungeonDeck.Config.Encounters;
using DungeonDeck.Config.Map;
using DungeonDeck.Config.Enemies;

namespace DungeonDeck.Battle.View
{
    /// <summary>
    /// 씬에 미리 배치된 플레이어/적 오브젝트를 Encounter 데이터에 따라 초기화합니다.
    /// </summary>
    public class BattleStageSpawner : MonoBehaviour
    {
        [Header("Player")]
        public BattleActorView playerView;

        [Header("Enemies (씬에 미리 배치)")]
        [Tooltip("슬롯 0~2에 해당하는 적 오브젝트. 인스펙터에서 직접 할당.")]
        public BattleEnemy[] enemies = new BattleEnemy[3];
        
        [Header("Encounter Source")]
        [Tooltip("일반 전투에서 사용할 Encounter Table (RunSession.PendingBattleType이 Boss가 아니면 사용)")]
        public BattleEncounterTable encounterTable;
            
        [Tooltip("보스 전투에서 사용할 Encounter Table (없으면 encounterTable을 사용)")]
        public BattleEncounterTable bossEncounterTable;
            
        [Tooltip("(디버그) 지정 시, 테이블/런 상태 무시하고 항상 이 Encounter를 사용")]
        public BattleEncounterDefinition debugEncounterOverride;
        
        [Header("Refs")]
        public BattleController battle;
        public BattleAnimDirector animDirector;
        public HitPopupSpawner hitPopups;

        private IEnumerator Start()
        {
            CacheRefs();
            
            // ✅ PendingEncounter 우선 → (없을 때만) 테이블 롤백
            var encounter = ResolveEncounter();
            
            Debug.Log($"[BattleStageSpawner] Resolved encounter: {(encounter != null ? encounter.id : "NULL")}");
            
            InitializeEnemiesFromEncounter(encounter);
            
            // 한 프레임 대기 후 바인딩 (다른 컴포넌트들 초기화 대기)
            yield return null;
            
            BindToSystems();
        }
        
        private void CacheRefs()
        {
            if (battle == null) battle = FindObjectOfType<BattleController>(true);
            if (animDirector == null) animDirector = FindObjectOfType<BattleAnimDirector>(true);
            if (hitPopups == null) hitPopups = FindObjectOfType<HitPopupSpawner>(true);
        }

        /// <summary>
        /// ✅ 이번 전투 Encounter 결정 우선순위
        /// 1) debugEncounterOverride
        /// 2) RunSession.I.PendingEncounter (맵 노드에서 미리 결정된 Encounter)
        /// 3) (fallback) 이 씬 단독 실행 시 encounterTable/bossEncounterTable 롤
        /// </summary>
        private BattleEncounterDefinition ResolveEncounter()
        {
            if (debugEncounterOverride != null) 
                return debugEncounterOverride;
            
            var run = RunSession.I;
            if (run != null)
            { 
                // ✅ 핵심: 맵에서 세팅해둔 PendingEncounter를 그대로 사용
                var pending = run.PendingEncounter;
                if (pending != null)
                {
                    Debug.Log($"[BattleStageSpawner] Using PendingEncounter: {pending.id}");
                    return pending;
                }
            }
            
            // ── fallback: Battle 씬 단독 실행(맵 진입 없이) 시에만 테이블로 굴린다
            var type = run != null ? run.PendingBattleType : MapNodeType.Battle;
            
            BattleEncounterTable table = null;
            if (type == MapNodeType.Boss)
                table = bossEncounterTable != null ? bossEncounterTable : encounterTable;
            else
                table = encounterTable;
            
            if (table == null)
            {
                Debug.LogWarning("[BattleStageSpawner] No encounter table assigned!");
                return null;
            }
            
            int seed = ComputeEncounterSeed(run, type);
            return table.Roll(seed);
        }
        
        private int ComputeEncounterSeed(RunSession run, MapNodeType type)
        {
            unchecked
            {
                int seed = 17;
                seed = seed * 31 + (run != null ? run.MapSeed : 0);
                seed = seed * 31 + (run != null && run.State != null ? run.State.nodeIndex : 0);
                seed = seed * 31 + (int)type;
                seed = seed * 31 + (run != null && run.State != null ? run.State.rewardRollCount : 0);
                return seed;
            }
        }
                
        /// <summary>
        /// Encounter 데이터를 읽어 각 슬롯의 적을 초기화/활성화합니다.
        /// </summary>
        private void InitializeEnemiesFromEncounter(BattleEncounterDefinition encounter)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                var enemy = enemies[slot];
                if (enemy == null) continue;
                
                // 1) Encounter 우선, 2) 없으면 씬에 박아둔 EnemyDefinition(폴백)
                var enemyDef = encounter != null ? encounter.GetEnemyAt(slot) : enemy.Definition;
                
                if (enemyDef != null)
                {
                    Debug.Log($"[BattleStageSpawner] Slot {slot}: Initializing with {enemyDef.id}");
                    
                    enemy.Init(enemyDef, slot);
                    ApplyVisualOverrides(enemy, encounter, slot);
                    
                    // 이 슬롯은 사용 → GO + 주요 컴포넌트가 꺼져있어도 강제로 켠다
                    enemy.gameObject.SetActive(true);
                    EnsureCoreComponentsEnabled(enemy.gameObject);
                }
                else
                {
                    Debug.Log($"[BattleStageSpawner] Slot {slot}: No enemy, disabling");
                    // 이 슬롯에 적 없음 → 비활성화
                    enemy.gameObject.SetActive(false);
                }
            }
        }
    
        /// <summary>
        /// Encounter/EnemyDefinition에 설정된 AnimatorOverrideController를 적용합니다.
        /// 우선순위: Encounter.slotAnimationOverrides[slot] > EnemyDefinition.animationOverride > (기존 AnimatorController 유지)
        /// </summary>
        private void ApplyVisualOverrides(BattleEnemy enemy, BattleEncounterDefinition encounter, int slot)
        {
            if (enemy == null) return;
            
            var animator = enemy.GetComponentInChildren<Animator>(true);
            if (animator == null) return;
            
            AnimatorOverrideController ctrl = null;
            if (encounter != null)
                ctrl = encounter.GetAnimationOverrideAt(slot);
            
            if (ctrl == null && enemy.Definition != null)
                ctrl = enemy.Definition.animationOverride;
            
            if (ctrl != null && animator.runtimeAnimatorController != ctrl)
            {
                animator.runtimeAnimatorController = ctrl;
                // 씬에서 Animator가 꺼져있어도 안전하게 켠다
                animator.enabled = true;
            }
        }
        
        private void EnsureCoreComponentsEnabled(GameObject enemyGo)
        {
            if (enemyGo == null) return;
            
            var be = enemyGo.GetComponent<BattleEnemy>();
            if (be != null) be.enabled = true;
            
            var etv = enemyGo.GetComponent<EnemyTargetView>();
            if (etv != null) etv.enabled = true;
            
            var bav = enemyGo.GetComponent<BattleActorView>();
            if (bav != null) bav.enabled = true;
            
            var anim = enemyGo.GetComponentInChildren<Animator>(true);
            if (anim != null) anim.enabled = true;
            
            var srs = enemyGo.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)
                if (srs[i] != null) srs[i].enabled = true;
        }

        /// <summary>
        /// AnimDirector, HitPopups 등에 View 참조를 연결합니다.
        /// ✅ 슬롯 인덱스를 보존하며 바인딩
        /// </summary>
        private void BindToSystems()
        {
            CacheRefs();

            // ✅ 슬롯 인덱스를 유지하는 배열 생성 (null 허용)
            var enemyViewsArray = new BattleActorView[3];
            int activeCount = 0;
            
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && enemies[i].gameObject.activeInHierarchy)
                {
                    var view = enemies[i].GetComponent<BattleActorView>();
                    enemyViewsArray[i] = view;  // ✅ 슬롯 인덱스 유지
                    if (view != null) activeCount++;
                    
                    Debug.Log($"[BattleStageSpawner] BindToSystems: Slot {i} = {(view != null ? view.name : "NULL")}");
                }
            }
            
            Debug.Log($"[BattleStageSpawner] Active enemy count: {activeCount}");

            // AnimDirector 바인딩 - 슬롯 인덱스 유지
            if (animDirector != null)
            {
                if (animDirector.targetManager == null)
                    animDirector.targetManager = FindObjectOfType<BattleTargetManager>(true);

                // ✅ 슬롯 인덱스를 유지하는 새 Bind 메서드 사용
                animDirector.BindWithSlots(playerView, enemyViewsArray);
                
                // 첫 번째 활성화된 슬롯 찾기
                int firstActiveSlot = FindFirstActiveSlot();
                Debug.Log($"[BattleStageSpawner] First active slot: {firstActiveSlot}");
                
                animDirector.OnTargetChanged(firstActiveSlot);
            }

            // HitPopup 타겟 바인딩
            if (hitPopups != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    Transform popupTarget = null;
                    
                    if (enemies[i] != null && enemies[i].gameObject.activeInHierarchy)
                    {
                        popupTarget = enemies[i].PopupTarget;
                    }
                    
                    hitPopups.RegisterEnemyTarget(i, popupTarget, null);
                }
            }
        }
        
        /// <summary>
        /// 첫 번째 활성화된 슬롯 인덱스 찾기
        /// </summary>
        private int FindFirstActiveSlot()
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && enemies[i].gameObject.activeInHierarchy && enemies[i].IsAlive)
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// 외부에서 강제 리바인딩이 필요할 때 호출
        /// </summary>
        public void RebindFromBattle()
        {
            BindToSystems();
        }
        
        // ─────────────────────────────────────────────────
        // Helper: 슬롯별 적/뷰 접근
        // ─────────────────────────────────────────────────
        public BattleEnemy GetEnemy(int slot)
        {
            if (slot < 0 || slot >= enemies.Length) return null;
            return enemies[slot];
        }
        
        public BattleActorView GetEnemyView(int slot)
        {
            var enemy = GetEnemy(slot);
            return enemy != null ? enemy.GetComponent<BattleActorView>() : null;
        }
    }
}
