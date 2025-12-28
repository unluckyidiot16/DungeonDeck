// Assets/_Project/Scripts/Battle/BattleController.cs
using System;
using System.Collections;
using System.Linq;
using DungeonDeck.UI.Widgets;
using DungeonDeck.Rewards;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonDeck.Core;
using DungeonDeck.Run;
using DungeonDeck.Config.Cards;
using DungeonDeck.Config.Map;
using Random = UnityEngine.Random;

namespace DungeonDeck.Battle
{
    public class BattleController : MonoBehaviour
    {
        [Header("Debug")]
        public bool autoWinForTest = false;

        private BattleState _state;
        private DeckRuntime _deck;
        
        public event Action StateChanged;
        
        [Header("Enemies")]
        [SerializeField, Range(1, 3)] private int debugEnemyCount = 3;

        [Serializable]
        public class EnemyState
        {
            public int hp;
            public int maxHp;
            public int block;
            public int vulnerableTurns;

            public bool IsAlive => hp > 0;
        }

        private readonly List<EnemyState> _enemies = new();
        private int _selectedEnemyIndex = 0;

        public int EnemyCount => _enemies.Count;
        public int SelectedEnemyIndex => _selectedEnemyIndex;
        
        // -------------------------
        // Player action queue (buffer)
        // -------------------------
        private enum BattleActionKind { PlayCard, EndTurn }
        
        [System.Serializable]
        private struct QueuedAction
        { 
            public BattleActionKind kind;
            public CardDefinition card;
            public int requestedHandIndex;
            public int targetIndex;
            public int seq;
        }
    
        private readonly Queue<QueuedAction> _actionQueue = new();
        private Coroutine _actionRunner = null;
        private int _actionSeq = 0;
        private bool _endTurnQueued = false;
    
        public int PendingActionCount => _actionQueue.Count;
        public bool IsActionQueueRunning => _actionRunner != null;
        public bool HasQueuedEndTurn => _endTurnQueued;
            
            
        private EnemyState GetSelectedEnemy()
        {
            if (_enemies.Count == 0) return null;
            _selectedEnemyIndex = Mathf.Clamp(_selectedEnemyIndex, 0, _enemies.Count - 1);
            return _enemies[_selectedEnemyIndex];
        }

        
        [Header("Reward (Win)")]
        [SerializeField] private CardChoicePanel rewardPanel;   // 배틀 씬 안에 비활성으로 두고 연결
        [SerializeField] private List<CardDefinition> fallbackRewardCandidates = new(); // 비었으면 현재 덱에서 후보를 뽑음
        [SerializeField] private bool useRunCardPools = true; // RunSession 카드풀 기반 후보/가중치 적용
        [SerializeField] private bool allowSkipReward = false;
        
        [Header("FX (optional)")]
        [SerializeField] private DungeonDeck.Battle.View.BattleAnimDirector animDirector;
        [SerializeField] private DungeonDeck.Battle.View.HitPopupSpawner hitPopups;

        private bool _isPlayerTurn = false;
        private bool _resolving = false;

        public bool IsPlayerTurn => _isPlayerTurn;
        public bool IsResolving => _resolving;
        
        private bool _endingFlow = false;
        
        private void AdvanceNodeAndRoute(RunSession run)
        {
            if (run == null) return;

            run.MarkNodeClearedAndAdvance();

            // Save after battle resolution + advance (or clear if run finished)
            if (RunSaveManager.I != null) RunSaveManager.I.SaveCurrentRun();

            SceneManager.LoadScene(run.IsRunFinished() ? SceneRoutes.End : SceneRoutes.Map);
        }

        
// UI/외부 조회용
        public int Energy => _state != null ? _state.energy : 0;
        public int PlayerHP => _state != null ? _state.playerHP : 0;
        public int PlayerMaxHP => _state != null ? _state.playerMaxHP : 0;
        public int PlayerBlock => _state != null ? _state.playerBlock : 0;

        public int EnemyHP => GetSelectedEnemy() != null ? GetSelectedEnemy().hp : 0;
        public int EnemyMaxHP => GetSelectedEnemy() != null ? GetSelectedEnemy().maxHp : 0;
        public int EnemyBlock => GetSelectedEnemy() != null ? GetSelectedEnemy().block : 0;

        
        public int EnemyVulnerableTurns => GetSelectedEnemy() != null ? GetSelectedEnemy().vulnerableTurns : 0;

        public int HandCount => _deck != null ? _deck.HandCount : 0;

        public CardDefinition GetHandCard(int index)
        {
            if (_deck == null) return null;
            return _deck.PeekHand(index);
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }
        
        public void EnsureEnemyCount(int count)
        {
            count = Mathf.Clamp(count, 1, 3);

            // 보스는 항상 1마리(원하면 여기 규칙 삭제 가능)
            if (RunSession.I != null && RunSession.I.PendingBattleType == MapNodeType.Boss)
                count = 1;

            if (_enemies.Count == count) return;

            var run = RunSession.I;

            // 늘리기
            while (_enemies.Count < count)
                _enemies.Add(CreateDefaultEnemy(run, _enemies.Count));

            // 줄이기
            while (_enemies.Count > count)
                _enemies.RemoveAt(_enemies.Count - 1);

            _selectedEnemyIndex = Mathf.Clamp(_selectedEnemyIndex, 0, _enemies.Count - 1);
            NotifyStateChanged();
        }

        public bool SelectEnemy(int index)
        {
            if (_enemies.Count == 0) return false;
            _selectedEnemyIndex = Mathf.Clamp(index, 0, _enemies.Count - 1);
            NotifyStateChanged();
            return true;
        }

        private EnemyState CreateDefaultEnemy(RunSession run, int i)
        {
            bool boss = (run != null && run.PendingBattleType == MapNodeType.Boss);
            int baseHp = boss ? 60 : 30;

            // 살짝 변주(같은 스탯만 3개면 밋밋해서)
            int hp = boss ? baseHp : Mathf.Max(10, baseHp - i * 5);

            return new EnemyState
            {
                hp = hp,
                maxHp = hp,
                block = 0,
                vulnerableTurns = 0
            };
        }

        private bool AreAllEnemiesDefeated()
        {
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i] != null && _enemies[i].IsAlive)
                    return false;
            return true;
        }

        private int CountAliveEnemies()
        {
            int n = 0;
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i] != null && _enemies[i].IsAlive)
                    n++;
            return n;
        }

        private void AutoSelectNextAliveIfNeeded()
        {
            var sel = GetSelectedEnemy();
            if (sel != null && sel.IsAlive) return;

            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null && _enemies[i].IsAlive)
                {
                    _selectedEnemyIndex = i;
                    return;
                }
            }

            _selectedEnemyIndex = 0;
        }

        private EnemyState GetEnemy(int index)
        {
            if (_enemies.Count == 0) return null;
            index = Mathf.Clamp(index, 0, _enemies.Count - 1);
            return _enemies[index];
        }

        private void ApplyVulnerableToEnemy(int enemyIndex, int turns)
        {
            turns = Mathf.Max(0, turns);
            if (turns <= 0) return;

            var e = GetEnemy(enemyIndex);
            if (e == null) return;

            e.vulnerableTurns = Mathf.Clamp(e.vulnerableTurns + turns, 0, 99);
        }

        private int DealDamageToEnemy_ReturnHpLoss(int enemyIndex, int amount)
        {
            amount = Mathf.Max(0, amount);

            var e = GetEnemy(enemyIndex);
            if (e == null) return 0;

            if (e.vulnerableTurns > 0 && amount > 0)
                amount = Mathf.CeilToInt(amount * 1.5f);

            int hpBefore = e.hp;

            int remain = amount;
            if (e.block > 0)
            {
                int used = Mathf.Min(e.block, remain);
                e.block -= used;
                remain -= used;
            }

            if (remain > 0) e.hp -= remain;
            if (e.hp < 0) e.hp = 0;

            // ✅ 선택 중인 적이 죽었으면 다음 살아있는 적 자동 선택
            if (enemyIndex == _selectedEnemyIndex)
                AutoSelectNextAliveIfNeeded();

            return Mathf.Max(0, hpBefore - e.hp);
        }

        

        /// <summary>
        /// 손패 인덱스의 카드를 사용 시도. 성공/실패 반환.
        /// </summary>
        /// <summary>
        /// 손패 인덱스의 카드를 사용(큐에 적재) 시도. 성공/실패 반환.
        /// </summary>
        public bool TryPlayCardAt(int handIndex)
        {
            if (_endingFlow || !_isPlayerTurn) return false;
            if (_deck == null || _state == null) return false;
            if (_endTurnQueued) return false;
            if (handIndex < 0 || handIndex >= _deck.HandCount) return false;

            var card = _deck.PeekHand(handIndex);
            if (card == null) return false;

            // ✅ 입력 버퍼: 지금 바로 처리하지 않고 큐에 쌓는다.
            //    (큐의 순서대로 실행했을 때 에너지가 모자라면 거절: 플레이어가 "예상 가능한" 동작)
            int energyAfterQueue = SimulateEnergyAfterQueuedActions();
            if (energyAfterQueue < card.cost)
            {
                Debug.Log("[Battle] Not enough energy (queued).");
                return false;
            }

            var action = new QueuedAction
            {
                kind = BattleActionKind.PlayCard,
                card = card,
                requestedHandIndex = handIndex,
                targetIndex = SelectedEnemyIndex,
                seq = ++_actionSeq
            };

            _actionQueue.Enqueue(action);
            NotifyStateChanged();

            EnsureActionRunner();
            return true;
        }

        private void EnsureActionRunner()
        {
            if (_actionRunner != null) return;
            _actionRunner = StartCoroutine(RunActionQueueCo());
        }

        private int SimulateEnergyAfterQueuedActions()
        {
            if (_state == null) return 0;

            int energy = _state.energy;

            foreach (var a in _actionQueue)
            {
                if (a.kind != BattleActionKind.PlayCard) continue;
                if (a.card == null) continue;

                energy -= a.card.cost;

                // GainEnergy가 큐에서 먼저 들어오면, 이후 카드 큐잉이 가능해짐
                if (a.card.effectKind == CardEffectKind.GainEnergy)
                    energy += Mathf.Max(0, a.card.value);
            }

            return energy;
        }

        private IEnumerator RunActionQueueCo()
        {
            try
            {
                while (!_endingFlow)
                {
                    // 플레이어 턴이 아니면 큐는 버림(적 턴/종료 중)
                    if (!_isPlayerTurn)
                    {
                        _actionQueue.Clear();
                        _endTurnQueued = false;
                        yield break;
                    }

                    if (_actionQueue.Count == 0)
                        yield break;

                    var a = _actionQueue.Dequeue();

                    if (a.kind == BattleActionKind.EndTurn)
                    {
                        // 턴 종료는 항상 큐를 비우고 진행(다음 턴 carry-over 금지)
                        _actionQueue.Clear();
                        _endTurnQueued = false;

                        yield return EndTurnFlowCo();
                        yield break;
                    }

                    yield return ExecuteQueuedCardCo(a);

                    if (_endingFlow)
                        yield break;
                }
            }
            finally
            {
                _actionRunner = null;
                NotifyStateChanged();
            }
        }

        private int ResolveTargetIndexForQueuedAction(int requested)
        {
            if (_enemies.Count == 0) return 0;

            requested = Mathf.Clamp(requested, 0, _enemies.Count - 1);
            var e = GetEnemy(requested);
            if (e != null && e.IsAlive) return requested;

            AutoSelectNextAliveIfNeeded();
            return _selectedEnemyIndex;
        }

        private IEnumerator ExecuteQueuedCardCo(QueuedAction a)
        {
            if (_endingFlow || !_isPlayerTurn) yield break;
            if (_deck == null || _state == null) yield break;
            if (a.card == null) yield break;

            // ✅ 큐잉 중 인덱스 변화/중복 클릭 대응: "현재 손패에서 해당 카드 찾기"
            int handIndexNow = -1;
            if (a.requestedHandIndex >= 0 && a.requestedHandIndex < _deck.HandCount && _deck.PeekHand(a.requestedHandIndex) == a.card)
                handIndexNow = a.requestedHandIndex;
            else
                handIndexNow = _deck.FindHandIndex(a.card);

            if (handIndexNow < 0)
                yield break; // 이미 소비/이동된 카드면 스킵

            if (_state.energy < a.card.cost)
            {
                Debug.Log("[Battle] Not enough energy (exec).");
                yield break;
            }

            int targetIndex = ResolveTargetIndexForQueuedAction(a.targetIndex);

            // 1) 에너지 선차감
            _state.energy -= a.card.cost;
            NotifyStateChanged();

            // 2) 연출
            if (animDirector != null)
            {
                if (a.card.effectKind == CardEffectKind.Attack)
                {
                    // ✅ 공격: 적 앞으로 접근 유지 + 콤보 펀치
                    yield return animDirector.PlayPlayerAttackComboCo(targetIndex);
                }
                else
                {
                    // ✅ 비공격: BattleAnimDirector.Co_Cast가 "근접이면 자동 복귀 → 시전"을 처리한다.
                    // (여기서 ReturnToBase를 또 호출하면 트리거/이동이 중복될 수 있음)
                    yield return animDirector.PlayPlayerCardCo(a.card, targetIndex);
                }
            }

            // 3) 효과 적용
            ApplyCard(a.card, targetIndex);

            // 4) 카드 이동
            if (a.card.exhaustOnPlay) _deck.ExhaustFromHand(handIndexNow);
            else _deck.PlayFromHand(handIndexNow);

            // 5) 승리 체크
            if (AreAllEnemiesDefeated())
            {
                NotifyStateChanged();
                EndBattle(true);
                yield break;
            }
            
            // ✅ 하이브리드 규칙:
            //    "에너지 0 + (액션 큐 비었음) + (EndTurn 예약 없음) + (근접 상태)"
            //    => 턴을 강제로 끝내지는 않되, 할 거 없으니 '연출용' 자동 복귀(=forced:false)
            if (ShouldAutoReturnToBaseNow())
            {
                yield return animDirector.ReturnToBaseCo(false);
            }

            NotifyStateChanged();
        }
        
        private bool ShouldAutoReturnToBaseNow()
        {
            if (_endingFlow || !_isPlayerTurn) return false;
            if (_state == null) return false;
            if (_state.energy > 0) return false;
            
            // EndTurn이 큐에 들어가 있으면 EndTurnFlow에서 forced=true 복귀가 처리되므로 여기선 스킵
            if (_endTurnQueued) return false;
            if (_actionQueue != null && _actionQueue.Count > 0) return false;
            
            if (animDirector == null) return false;
            if (!animDirector.IsMelee) return false;
            
            return true;
        }
        
        /// <summary>
        /// 턴 종료 (UI 버튼 연결용)
        /// </summary>
        public void EndTurn()
        {
            if (_deck == null || _state == null) return;
            if (_endingFlow || _resolving || !_isPlayerTurn) return;

            // ✅ 카드 처리 중이면(또는 이미 큐가 있으면) "턴 종료"도 큐에 적재해서 순차 처리
            if (_actionRunner != null || _actionQueue.Count > 0)
            {
                if (_endTurnQueued) return;
                _endTurnQueued = true;
                _actionQueue.Enqueue(new QueuedAction
                {
                    kind = BattleActionKind.EndTurn,
                    card = null,
                    requestedHandIndex = -1,
                    targetIndex = SelectedEnemyIndex,
                    seq = ++_actionSeq
                });
                
                NotifyStateChanged();
                EnsureActionRunner();
                return;
            }
            
            StartCoroutine(EndTurnFlowCo());
        }

        private IEnumerator EndTurnFlowCo()
        {
            _resolving = true;
            _isPlayerTurn = false;
            
            _actionQueue.Clear();
            _endTurnQueued = false;
            
            // ✅ 근접 상태로 턴 종료했으면, 적 턴 전에 원위치 복귀
            if (animDirector != null)
                yield return animDirector.ReturnToBaseCo(true); // forced=true => BackDash 고정

            // 1) 손패 버림
            _deck.DiscardHand();
            NotifyStateChanged();

            // 2) 적 공격 “연출” 먼저
            int raw = (RunSession.I.PendingBattleType == MapNodeType.Boss) ? 12 : 8;

            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e == null || !e.IsAlive) continue;

                if (animDirector != null)
                    yield return animDirector.PlayEnemyAttackCo(i);

                if (animDirector != null) animDirector.PlayPlayerHitFx();
                
                int hpLoss = DealDamageToPlayer_ReturnHpLoss(raw);
                if (hitPopups != null && hpLoss > 0) hitPopups.SpawnPlayer(hpLoss);

                NotifyStateChanged();

                if (_state.playerHP <= 0)
                {
                    EndBattle(false);
                    yield break;
                }
            }

            // ✅ 적 취약 턴 감소(전체)
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e == null) continue;
                if (e.vulnerableTurns > 0) e.vulnerableTurns -= 1;
            }


            NotifyStateChanged();

            if (_state.playerHP <= 0)
            {
                EndBattle(false);
                yield break;
            }

            // 4) 플레이어 턴 시작
            BeginPlayerTurn();
            NotifyStateChanged();
        }


        private void Start()
        {
            if (RunSession.I == null || RunSession.I.State == null)
            {
                Debug.LogError("[Battle] RunSession missing. Start from Boot.");
                SceneManager.LoadScene(SceneRoutes.Boot);
                return;
            }
            
            SetupBattle();
            
            if (animDirector == null) animDirector = FindObjectOfType<DungeonDeck.Battle.View.BattleAnimDirector>(true);
            if (hitPopups == null) hitPopups = FindObjectOfType<DungeonDeck.Battle.View.HitPopupSpawner>(true);
            
            BeginPlayerTurn();
            NotifyStateChanged();

            if (autoWinForTest)
                EndBattle(true);
        }

        private void SetupBattle()
        {
            var run = RunSession.I;

            _state = new BattleState
            {
                playerHP = run.State.hp,
                playerMaxHP = run.State.maxHP,
                playerBlock = 0,
                energy = run.Balance != null ? run.Balance.startEnergyPerTurn : 3,
                drawPerTurn = run.Balance != null ? run.Balance.startDrawPerTurn : 5,
            };

            _deck = new DeckRuntime(run.State.deck);

            // ✅ 적 리스트 초기화 (일반은 debugEnemyCount, 보스는 1)
            EnsureEnemyCount(debugEnemyCount);

            // 기본 선택
            SelectEnemy(0);
        }


        private void BeginPlayerTurn()
        {
            _state.playerBlock = 0;
            _state.energy = RunSession.I.Balance != null ? RunSession.I.Balance.startEnergyPerTurn : 3;

            _deck.Draw(_state.drawPerTurn);

            _isPlayerTurn = true;
            _resolving = false;
        }

        // Hook this to UI button later
        public void DebugPlayFirstCard()
        {
            TryPlayCardAt(0);
        }

        // Hook this to End Turn button later
        public void DebugEndTurn()
        {
            EndTurn();
        }

        private void ApplyCard(CardDefinition card)
        { 
            ApplyCard(card, SelectedEnemyIndex);
        }
        
        private void ApplyCard(CardDefinition card, int targetIndex)
        {
            switch (card.effectKind)
            {
                case CardEffectKind.Attack:
                {
                    int hpLoss = DealDamageToEnemy_ReturnHpLoss(targetIndex, card.value);
                    if (hitPopups != null && hpLoss > 0) hitPopups.SpawnEnemy(hpLoss, targetIndex);
                    break;
                }
                case CardEffectKind.Block:
                    _state.playerBlock += Mathf.Max(0, card.value);
                    Debug.Log($"[Battle] Play {card.id}: Block +{card.value}. Block={_state.playerBlock}");
                    break;

                case CardEffectKind.Draw:
                    _deck.Draw(Mathf.Max(0, card.value));
                    Debug.Log($"[Battle] Play {card.id}: Draw {card.value}. Hand={_deck.HandCount}");
                    break;

                case CardEffectKind.GainEnergy:
                    _state.energy += Mathf.Max(0, card.value);
                    Debug.Log($"[Battle] Play {card.id}: Energy +{card.value}. Energy={_state.energy}");
                    break;
                case CardEffectKind.ApplyVulnerable:
                {
                    ApplyVulnerableToEnemy(targetIndex, card.value);
                    var e = GetEnemy(targetIndex);
                    Debug.Log($"[Battle] Play {card.id}: Apply Vulnerable +{card.value}. TargetVuln={(e != null ? e.vulnerableTurns : 0)}");
                    break;
                }
            }
        }

        private void ApplyVulnerableToSelectedEnemy(int turns)
        {
            turns = Mathf.Max(0, turns);
            if (turns <= 0) return;

            var e = GetSelectedEnemy();
            if (e == null) return;

            e.vulnerableTurns = Mathf.Clamp(e.vulnerableTurns + turns, 0, 99);
        }

        private int DealDamageToSelectedEnemy_ReturnHpLoss(int amount)
        {
            amount = Mathf.Max(0, amount);

            var e = GetSelectedEnemy();
            if (e == null) return 0;

            if (e.vulnerableTurns > 0 && amount > 0)
                amount = Mathf.CeilToInt(amount * 1.5f);

            int hpBefore = e.hp;

            int remain = amount;
            if (e.block > 0)
            {
                int used = Mathf.Min(e.block, remain);
                e.block -= used;
                remain -= used;
            }

            if (remain > 0)
                e.hp -= remain;

            if (e.hp < 0) e.hp = 0;

            AutoSelectNextAliveIfNeeded();

            return Mathf.Max(0, hpBefore - e.hp);
        }

        private int DealDamageToPlayer_ReturnHpLoss(int amount)
        {
            amount = Mathf.Max(0, amount);

            int hpBefore = _state.playerHP;

            int remain = amount;
            if (_state.playerBlock > 0)
            {
                int used = Mathf.Min(_state.playerBlock, remain);
                _state.playerBlock -= used;
                remain -= used;
            }

            if (remain > 0)
                _state.playerHP -= remain;

            if (_state.playerHP < 0) _state.playerHP = 0;
            return Mathf.Max(0, hpBefore - _state.playerHP);
        }


        private void EndBattle(bool win)
        {
            if (_endingFlow) return;
            _endingFlow = true;

            // ✅ UI/입력 잠금 (BattleHandUI가 IsPlayerTurn/IsResolving로 버튼을 끄게)
            _isPlayerTurn = false;
            _resolving = true;
            NotifyStateChanged();

            var run = RunSession.I;

            // sync player hp back to run
            run.State.hp = _state.playerHP;

            if (!win)
            {
                Debug.Log("[Battle] LOSE");
                run.EndRun(RunEndOutcome.Defeat);

                // Ensure save is cleared on run end
                RunSaveManager.ClearSave();
                PlayerPrefs.Save();

                // ✅ End 씬으로 라우팅
                SceneManager.LoadScene(SceneRoutes.End);
                return;
            }

            Debug.Log("[Battle] WIN");

            // 골드 먼저 지급
            if (run.Balance != null) run.State.gold += run.Balance.winGold;

            // ✅ 승리 보상 선택 → 덱 추가 → 노드 클리어/진행 → 맵/엔드 라우팅
            StartCoroutine(WinRewardFlowCo());
        }

        
        private IEnumerator WinRewardFlowCo()
        {
            var run = RunSession.I;
            int battleNodeIndex = (run != null && run.State != null) ? run.State.nodeIndex : 0;

            // 패널 자동 탐색(인스펙터 연결 권장)
            var panel = rewardPanel != null ? rewardPanel : FindObjectOfType<CardChoicePanel>(true);

            // 패널이 없으면 그냥 진행(크래시 방지)
            if (panel == null)
            {
                Debug.LogWarning("[Battle] Reward panel not found. Skipping reward.");
                AdvanceNodeAndRoute(run);
                yield break;
            }

            // 1) 후보 리스트 구성
            var candidates = BuildRewardCandidates(run);
            if (candidates == null || candidates.Count == 0)
            {
                Debug.LogWarning("[Battle] No reward candidates. Advancing without reward.");
                AdvanceNodeAndRoute(run);
                yield break;
            }

            // 2) 3장 뽑기 (가중치 + 중복 패널티)
            var cfg = DungeonDeck.Rewards.CardRewardRollerCards.RollConfig.Default;
            cfg.duplicateWeightMultiplier = 0.35f;
            cfg.scaleByCopies = true;
            cfg.maxCopyExponent = 3;

            List<CardDefinition> options;
            int baseSeed = (run.State.seed != 0) ? run.State.seed : run.State.shopSeed; // fallback
            int salt = (run.State.rewardRollCount + 1) * 1009;
            int seed = unchecked(baseSeed * 10007 + battleNodeIndex * 97 + run.State.runClearedBattles * 13 + salt);
            if (seed == 0) seed = 1;
            List<CardPoolDefinition> pools = null;
            if (useRunCardPools && run != null)
            {
                var ro = run.GetActiveCardPools(RunSession.CardPoolContext.Reward);
                if (ro != null && ro.Count > 0) pools = new List<CardPoolDefinition>(ro);
            }
            if (pools != null && pools.Count > 0)
            {
                options = DungeonDeck.Rewards.CardRewardRollerCards.RollFromPools(
                    pools: pools,
                    ownedDeck: run.State.deck,
                    count: 3,
                    unique: true,
                    seed: seed,
                    configOpt: cfg
                    );
            }
            else
            {
                options = DungeonDeck.Rewards.CardRewardRollerCards.RollWeighted(
                    candidates: candidates,
                    ownedDeck: run.State.deck,
                    count: 3,
                    unique: true,
                    seed: seed,
                    configOpt: cfg
                    );
            }


            if (options == null || options.Count == 0)
            {
                Debug.LogWarning("[Battle] No reward options. Advancing without reward.");
                AdvanceNodeAndRoute(run);
                yield break;
            }

            // 3) UI Show & 선택 대기
            bool done = false;
            CardDefinition chosen = null;

            panel.Show(
                options,
                onChosen: c => { chosen = c; done = true; },
                onSkipped: allowSkipReward ? (() => { done = true; }) : null,
                title: "Reward: Choose 1 Card"
            );

            while (!done) yield return null;

            // ✅ 이번 승리 보상 롤 카운트 증가(세이브/재진입 시 같은 보상 반복 방지)
            run.State.rewardRollCount += 1;
            
            // 4) 선택 카드 덱에 추가
            if (chosen != null)
            {
                // run.State.deck는 List<CardDefinition> 가정 (현재 BattleController가 그렇게 쓰고 있음)
                if (run.State.deck == null) run.State.deck = new List<CardDefinition>();
                run.State.deck.Add(chosen);
                Debug.Log($"[Battle] Reward chosen: {chosen.id}");
            }

            AdvanceNodeAndRoute(run);
        }

private List<CardDefinition> BuildRewardCandidates(RunSession run)
{
    // 1) 인스펙터에 지정한 후보가 있으면 우선 사용
    if (fallbackRewardCandidates != null && fallbackRewardCandidates.Count > 0)
    {
        // null 제거 + 중복 제거(레퍼런스 기준)
        return fallbackRewardCandidates
                   .Where(c => c != null)
                   .GroupBy(c => c.id)
                   .Select(g => g.First())
                   .ToList();
    }

    // 2) 카드 풀 기반 후보(서약/메타 해금 포함)
    if (useRunCardPools && run != null)
    { 
        var poolCandidates = run.GetActiveCardCandidatesUnique(RunSession.CardPoolContext.Reward);
        if (poolCandidates != null && poolCandidates.Count > 0) 
            return poolCandidates;
    }
    
    // 3) 없으면 현재 덱 기반으로 후보 구성(최소 동작 보장)
    if (run != null && run.State != null && run.State.deck != null && run.State.deck.Count > 0)
    {
        return run.State.deck.Where(c => c != null).Distinct().ToList();
    }

    return new List<CardDefinition>();
}

        
    }
    
    // ----------------------------
    // Minimal battle model/runtime
    // ----------------------------
    public class BattleState
    {
        public int playerHP;
        public int playerMaxHP;
        public int playerBlock;

        public int enemyHP;
        public int enemyMaxHP;
        public int enemyBlock;
        public int enemyVulnerableTurns;

        public int energy;
        public int drawPerTurn;
    }

    public class DeckRuntime
    {
        private readonly List<CardDefinition> _draw = new();
        private readonly List<CardDefinition> _discard = new();
        private readonly List<CardDefinition> _hand = new();
        private readonly List<CardDefinition> _exhaust = new();

        public int HandCount => _hand.Count;

        public DeckRuntime(List<CardDefinition> sourceDeck)
        {
            if (sourceDeck != null) _draw.AddRange(sourceDeck);
            Shuffle(_draw);
        }

        public void Draw(int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (_draw.Count == 0)
                    Reshuffle();

                if (_draw.Count == 0)
                    return;

                var c = _draw[0];
                _draw.RemoveAt(0);
                _hand.Add(c);
            }
        }

        public CardDefinition PeekHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return null;
            return _hand[index];
        }

        public int FindHandIndex(CardDefinition card)
        {
            if (card == null) return -1;
            for (int i = 0; i < _hand.Count; i++)
            {
                if (_hand[i] == card) return i;
            }
            return -1;
        }
        
        public void PlayFromHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return;

            var c = _hand[index];
            _hand.RemoveAt(index);
            _discard.Add(c);
        }

        public void ExhaustFromHand(int index)
        {
            if (index < 0 || index >= _hand.Count) return;
            
            var c = _hand[index];
            _hand.RemoveAt(index);
            _exhaust.Add(c);
        }
        
        public void DiscardHand()
        {
            if (_hand.Count == 0) return;
            _discard.AddRange(_hand);
            _hand.Clear();
        }

        private void Reshuffle()
        {
            if (_discard.Count == 0) return;
            _draw.AddRange(_discard);
            _discard.Clear();
            Shuffle(_draw);
        }

        private static void Shuffle(List<CardDefinition> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int r = Random.Range(i, list.Count);
                (list[i], list[r]) = (list[r], list[i]);
            }
        }
    }
}
