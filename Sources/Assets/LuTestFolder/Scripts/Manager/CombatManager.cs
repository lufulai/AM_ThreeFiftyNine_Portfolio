using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CombatManager : MonoBehaviour
{
    [Serializable]
    private class BossDebuffSet
    {
        public BossType bossType = BossType.None;
        public BossDebuffSO[] debuffs = Array.Empty<BossDebuffSO>();
    }

    public static CombatManager instance { get; private set; }

    [Header("Generated Stage Data")]
    [SerializeField] private int[] generatedBaseTargetScores = { 300, 800, 2000, 5000, 11000, 20000 };
    [SerializeField] private float generatedFirstRoundMultiplier = 1f;
    [SerializeField] private float generatedSecondRoundMultiplier = 1.5f;
    [SerializeField] private float generatedBossRoundMultiplier = 2f;
    [SerializeField] private float generatedEndlessExponentGrowth = 0.2f;
    [SerializeField] private int generatedScoreRoundingUnit = 100;
    [SerializeField] private int generatedDefaultMaxHands = 5;
    [SerializeField] private int generatedDefaultMaxDrops = 3;

    [Header("Run Clear")]
    [SerializeField, Min(1)] private int runClearStageNumber = 6;

    private const int ConfigurableBaseScoreStageCount = 6;
    private const double EndlessBaseScore = 20000d;
    private const double EndlessBaseGrowth = 1.6d;
    private const double EndlessStepGrowth = 0.75d;
    private const string RustyPliersArtifactID = "L_Artifact_21";
    private const double RustyPliersTargetMultiplier = 0.8d;

    [Header("Cur Stage Data")]
    public int curStageIndex = 0;
    public int curRoundIndex = 0; // 0이면 1라운드 . . .
    public long curRoundScore = 0; // 현 라운드 점수
    public long targetScore; // 현 라운드 목표 점수
    private long cheongaOriginalTargetScore;
    private int cheongaTargetGoCount;
    // Other reductions keep the original cross; a different GO phase or round hides it.
    public long CheongaOriginalTargetScore => targetScore > 0L && targetScore < cheongaOriginalTargetScore
        && cheongaTargetGoCount == (GoStopManager.Instance != null ? GoStopManager.Instance.currentGoCount : 0)
        ? cheongaOriginalTargetScore : 0L;

    private void ClearCheongaTargetHistory()
    {
        cheongaOriginalTargetScore = 0L;
        cheongaTargetGoCount = 0;
    }
    public long LastEarnedScoreForPresentation { get; private set; }
    public int maxHands;
    public int maxDrops;
    public int remaningHands; // 남은 핸드 [플레이] 횟수
    public bool LastSubmissionUsedLastHand { get; private set; }
    public bool ClearedUsingLastHand { get; private set; }

    /// <summary>처용탈 등이 핸드를 회복하기 전, 이번 제출이 마지막 기회인지 기록합니다.</summary>
    public void CaptureSubmissionUnlockState()
    {
        LastSubmissionUsedLastHand = remaningHands == 1;
    }
    public int remaningDrops; // 남은 드랍 횟수
    public int maxHandCard = 5;
    public double roundDiscardBonusMultiplier = 0.0; // 저승에 있는 조커로 인한 라운드 보너스 배수

    public int currentRoundArtifactBonusMoney = 0; // 기물 보너스
    public int currentRoundSynergyBonusMoney = 0;  // 시너지 보너스
    public int currentRoundInterestMoney = 0;      // 이자 보너스

    public bool isRoundCleared = false;
    public bool isInCombat = false;
    public bool IsCurrentRoundBoss { get; private set; }
    public bool IsBattleResultOpen { get; private set; }
    public bool HasPendingPostPlayOutcome { get; private set; }
    public bool CanAcceptCardActions => isInCombat && !isRoundCleared && !IsBattleResultOpen && !isReturningToMap
        && !(GoStopManager.Instance != null && GoStopManager.Instance.IsChoiceResolving);
    public int SubmittedHandsThisRound { get; private set; }
    public int SubmittedCardCountThisRound { get; private set; }

    [Header("Boss Debuff")]
    [SerializeField] private BossDebuffSO[] availableDebuffs; // 보스 관에서 사용될 저주 풀
    [SerializeField] private BossDebuffSet[] bossDebuffSets;
    public BossDebuffSO currentDebuff;


    public event Action<int, int> OnChangedHandsDropsCount; // 이벤트 액션 생성  현재 결합도 문제 해결 방안으로 해놈
    private bool isReturningToMap;
    private bool isRoundClearRoutineRunning;
    private Coroutine roundClearRoutine;
    private int battleResultVersion;
    private bool isPreparingBattleResultRewards;
    private bool hasPendingBattleResultSettlement;
    private int pendingBattleResultMoney;
    // Display the queued deposit, which can differ from net income after already-paid fees.
    internal int PendingBattleResultPayout => hasPendingBattleResultSettlement ? pendingBattleResultMoney : 0;
    internal int PendingRewardForEffectCalculation => isPreparingBattleResultRewards ? pendingBattleResultMoney : 0;
    private Coroutine bossDefeatRoutine;
    private Coroutine goFailureEscapeRoutine;
    private bool isResolvingGoFailure;
    private bool hasPendingRunClearResult;
    private bool hasPendingRoundActivation;
    private JokBoType currentRustyPliersTargetJokbo = JokBoType.None;
    private bool rustyPliersPreparedForCurrentHand;
    private StageInfo activeStageInfo;

    public JokBoType CurrentRustyPliersTargetJokbo => currentRustyPliersTargetJokbo;
    public StageInfo CurrentEncounterStageInfo => activeStageInfo;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        CancelGoFailureEscape();
        CancelBattleResultSettlement();
        if (instance == this)
        {
            SoundManager.instance?.EndBossBGM();
            instance = null;
        }
    }

    private void OnDisable()
    {
        CancelGoFailureEscape();
        CancelBattleResultSettlement();
    }

    // private void Start()
    // {
    //     // GameManager가 StartNewGame()에서 첫 라운드를 시작시키므로, 여기서 중복 호출하지 않습니다.
    // }

    // 라운드 시작 함수며 주 기능은 초기화 담당
    public void StartRound(int roundIndex) // 위 주석에 써놓은 것 처럼 0이면 1라운드 1이면 2라운드
    {
        if (PrepareRoundState(roundIndex))
            ActivatePreparedRound();
    }

    private bool PrepareRoundState(int roundIndex)
    {
        hasPendingRoundActivation = false;

        if (!IsCombatStageInfo(activeStageInfo))
        {
            Debug.LogError("[CombatManager] 현재 전투 컨텍스트가 없습니다. 맵 노드의 StageInfo로 전투를 시작해야 합니다.");
            ResetCombatBoardStateForInvalidStage();
            return false;
        }

        CancelPendingRoundCallbacks();
        ResetCurrentDebuff();

        if (JokboPlayTracker.Instance != null)
            JokboPlayTracker.Instance.ResetForNewRound();

        if (!TryResolveRoundSetup(curStageIndex, roundIndex, out int stageNum, out RoundData data, out string roundSource))
        {
            Debug.LogError($"[CombatManager] 라운드 데이터를 만들 수 없습니다. stageIndex={curStageIndex}, roundIndex={roundIndex}");
            ResetCombatBoardStateForInvalidStage();
            return false;
        }

        isReturningToMap = false;
        isInCombat = true;
        SetBattleResultOpen(false);
        IsCurrentRoundBoss = activeStageInfo != null && activeStageInfo.type == StageType.Boss;

        curRoundIndex = roundIndex; // 0 , 1 , 2로 숫자를 받고

        GoStopManager.Instance?.ResetGoStop();

        LastEarnedScoreForPresentation = 0;
        ClearCheongaTargetHistory();
        curRoundScore = 0; // 점수 초기화
        SubmittedHandsThisRound = 0;
        LastSubmissionUsedLastHand = false;
        ClearedUsingLastHand = false;
        SubmittedCardCountThisRound = 0;
        targetScore = data.targetScore;
        currentRustyPliersTargetJokbo = JokBoType.None;
        rustyPliersPreparedForCurrentHand = false;
        maxHands = data.maxHands;
        maxDrops = data.maxDrops;

        remaningHands = data.maxHands; // 남은 핸드 [플레이] 횟수는 정해둔 데이터 횟수 주입
        remaningDrops = data.maxDrops;

        currentRoundArtifactBonusMoney = 0;
        currentRoundSynergyBonusMoney = 0;
        currentRoundInterestMoney = 0;

        if (VoucherManager.Instance != null)
        {
            int handModifier = VoucherManager.Instance.GetTotalEffectValue(VoucherEffectType.HandCountModifier);
            int dropModifier = VoucherManager.Instance.GetTotalEffectValue(VoucherEffectType.DropCountModifier);

            maxHands += handModifier;
            remaningHands += handModifier;
            remaningDrops += dropModifier;
        }

        roundDiscardBonusMultiplier = 0.0; // 새 라운드 시작 시 보너스 배수 초기화
        // 버리기 횟수처럼 전투마다 초기화되는 조커 효과를 기본값 위에 다시 적용합니다.
        if (GameManager.instance != null)
            GameManager.instance.ApplyJokerRoundStartState();
        
        if (CardManager_2.Instance != null)
            CardManager_2.Instance.SetupCardBuffer(); // 라운드 시작 시 덱과 핸드 초기화

        // 기물: 라운드 시작 트리거. 목표 점수는 이 트리거 적용 후 값을 프리뷰에 보여준다.
        if (GameManager.instance != null)
            GameManager.instance.OnCombatRoundStart();

        GameLog.Info($"{stageNum} 스테이지 {roundIndex + 1}관 목표 점수 {targetScore} ({roundSource})");

        if (IsCurrentRoundBoss)
        {
            StartBossRound();
        }
        else
        {
            SoundManager.instance?.PlayDefaultBGM();
            currentDebuff = null; // 보스 관이 아니면 저주 제거
        }

        // 관 시작 시 저주 상태 초기화
        if (currentDebuff != null)
        {
            if (ArtifactManager.Instance != null && ArtifactManager.Instance.HasArtifact("O_Artifact_43"))
            {
                GameLog.Info("[성주단지] 대요괴의 저주를 무시했습니다.");
                currentDebuff = null;
            }
            else
            {
                bool keepPreparedRandomMonth =
                    currentDebuff is UnusableRandomMonth randomMonthDebuff && randomMonthDebuff.HasSelectedMonth;

                if (!keepPreparedRandomMonth)
                    currentDebuff.ResetDebuff();

                currentDebuff.InitDebuff();
                if (!isInCombat)
                    return false;
            }
        }

        ApplyArtifactSynergyTargetScoreModifiers();

        if (FieldUIManager.Instance != null)
        {
            FieldUIManager.Instance.Update_Stage_Round_UI(stageNum, curRoundIndex);
            FieldUIManager.Instance.UpdateFieldUI(remaningHands, remaningDrops);
            FieldUIManager.Instance.UpdateScoreUI(targetScore, curRoundScore, 1, JokBoType.None, false);
            FieldUIManager.Instance.RefreshBossDebuffIndicator();
        }

        hasPendingRoundActivation = true;
        return true;
    }

    private void ActivatePreparedRound()
    {
        if (!hasPendingRoundActivation)
            return;

        hasPendingRoundActivation = false;
        ScoreSwordGaugePresenter.Active?.ResetForRound();

        if (FieldUIManager.Instance != null)
        {
            FieldUIManager.Instance.UpdateScoreUI(targetScore, curRoundScore, 1, JokBoType.None, false);
            FieldUIManager.Instance.RefreshBossDebuffIndicator();
        }

        if (CardManager_2.Instance != null)
            CardManager_2.Instance.DrawAndSpawn(maxHandCard);
    }

    private void StartBossRound()
    {
        StageInfo currentStageInfo = CurrentEncounterStageInfo;
        BossType bossType = currentStageInfo != null ? currentStageInfo.GetEffectiveBossType() : BossType.None;
        // currentDebuff = currentStageInfo != null && currentStageInfo.bossDebuff != null
        //     ? currentStageInfo.bossDebuff
        //     : PickDebuffForBoss(bossType);
        
        if (currentStageInfo != null && currentStageInfo.bossDebuff != null)
        {
            currentDebuff = currentStageInfo.bossDebuff;
        }
        else
        {
            // 2. 지정된 게 없다면, 스테이지 번호를 확인해 저주 선택
            currentDebuff = PickDebuffForBoss(bossType);
        }

        if (ArtifactManager.Instance != null && ArtifactManager.Instance.HasArtifact("O_Artifact_43"))
        {
            GameLog.Info("[성주단지] 대요괴의 저주를 무시했습니다.");
            currentDebuff = null;
            SoundManager.instance?.PlayBossBGM(bossType, curStageIndex);
            return;
        }

        string bossName = currentStageInfo != null ? currentStageInfo.GetBossDisplayName() : bossType.ToString();
        string debuffName = GetDebuffDisplayName(currentDebuff);
        GameLog.Info($"[Boss] {bossName} 저주 선택: {debuffName}");
        SoundManager.instance?.PlayBossBGM(bossType, curStageIndex);

        if (currentDebuff is DebuffDueoksini)
        {
            double multiplier = ArtifactManager.Instance != null
                && ArtifactManager.Instance.HasArtifact("O_Artifact_36")
                ? 2d
                : 3d;
            targetScore = ScoreSafeMath.MultiplyScore(targetScore, multiplier, 1L);
        }
        else if (currentDebuff is DebuffImugi) targetScore /= 2; // 기본 보스 2배 / 2 = 1배

        if (currentDebuff is DebuffVirginGhost ghost)
        {
            // 인벤토리에 기물이 1개도 존재하지 않는다면 즉시 한을 풀어줍니다.
            if (ArtifactManager.Instance == null || 
                ArtifactManager.Instance.inventory == null || 
                ArtifactManager.Instance.inventory.items.Count == 0)
            {
                ghost.OnArtifactSold(); // 기물 판매 시 실행되던 한 풀기 함수 호출!
                GameLog.Info("[Boss] 처녀귀신 방어 코드 작동: 보유 중인 기물이 없어 저주가 즉시 해제됩니다.");
            }
        }
        
        if (FieldUIManager.Instance != null)
            FieldUIManager.Instance.UpdateScoreUI(targetScore, curRoundScore, 1, JokBoType.None, false);
    }

    private void ApplyArtifactSynergyTargetScoreModifiers()
    {
        ArtifactManager artifactManager = ArtifactManager.Instance;
        if (artifactManager == null || targetScore <= 0)
            return;

        float targetMultiplier = 1f;
        string logLabel = null;
        if (artifactManager.HasSynergyTier(ArtifactSynergyType.RoyalInspector, 3))
        {
            float discount = 0.2f * (float)artifactManager.GetSynergyEffectMultiplier();
            targetMultiplier *= Mathf.Clamp01(1f - discount);
            logLabel = "어사출두 3";
        }
        if (artifactManager.HasSynergyTier(ArtifactSynergyType.RoyalInspector, 2))
        {
            float discount = 0.1f * (float)artifactManager.GetSynergyEffectMultiplier();
            targetMultiplier *= Mathf.Clamp01(1f - discount);
            logLabel = logLabel == null ? "어사출두 2" : "어사출두 2+3";
        }

        if (logLabel == null)
            return;

        long beforeTargetScore = targetScore;
        targetScore = ScoreSafeMath.MultiplyScoreCeiling(targetScore, targetMultiplier, 1L);
        GameLog.Info($"[시너지] {logLabel} 요구점수 감소: {beforeTargetScore} -> {targetScore}");
    }

    public BossDebuffSO PickRandomDebuffForBoss(BossType bossType)
    {
        return PickDebuffForBoss(bossType, curStageIndex + 1);
    }

    /// <summary>맵 생성 시 전달받은 실제 스테이지 번호를 기준으로 보스 디버프를 선택합니다.</summary>
    public BossDebuffSO PickRandomDebuffForBoss(BossType bossType, int oneBasedStageNumber)
    {
        return PickDebuffForBoss(bossType, oneBasedStageNumber);
    }

    /// <summary>
    /// 맵 런 시드 전용 선택 경로입니다. UnityEngine.Random 상태를 소비하지 않아 같은 시드의 보스 저주가 고정됩니다.
    /// 반환값은 원본 에셋이며, 상태를 갖는 런타임 복제는 호출자가 만듭니다.
    /// </summary>
    public BossDebuffSO PickDebuffForBossDeterministically(
        BossType bossType,
        int oneBasedStageNumber,
        ulong seed)
    {
        BossDebuffSO[] candidates = ResolveBossDebuffCandidates(bossType);
        if (candidates == null || candidates.Length == 0)
            return null;

        int stageNumber = Mathf.Max(1, oneBasedStageNumber);
        int selectedIndex;
        if (stageNumber % 6 == 0)
        {
            selectedIndex = candidates.Length - 1;
        }
        else
        {
            int candidateCount = Mathf.Min(2, candidates.Length);
            selectedIndex = new MapDeterministicRandom(seed).Range(0, candidateCount);
        }

        BossDebuffSO selectedDebuff = candidates[selectedIndex];
        GameLog.Info(
            $"[BossDebuffSelect] 시드 고정 / 스테이지 {stageNumber}, 보스 {bossType}, "
            + $"후보 {candidates.Length}개 중 {selectedIndex + 1}번 '{GetDebuffDisplayName(selectedDebuff)}' 선택");
        return selectedDebuff;
    }

    private BossDebuffSO PickDebuffForBoss(BossType bossType)
    {
        return PickDebuffForBoss(bossType, curStageIndex + 1);
    }

    /// <summary>보스 후보 배열과 실제 1-based 스테이지 번호로 이번 디버프를 선택합니다.</summary>
    private BossDebuffSO PickDebuffForBoss(BossType bossType, int oneBasedStageNumber)
    {
        BossDebuffSO[] candidates = ResolveBossDebuffCandidates(bossType);

        if (candidates != null && candidates.Length > 0)
        {
            int stageNum = Mathf.Max(1, oneBasedStageNumber);
            int selectedIndex;

            if (stageNum % 6 == 0)
            {
                // 6의 배수 스테이지: 배열의 맨 마지막 칸 확정 (예: 진보스 저주)
                selectedIndex = candidates.Length - 1;
            }
            else
            {
                // 6의 배수가 아님: 앞의 2칸 중에서 랜덤 발동 (배열 크기 방어코드 포함)
                int maxIndex = Mathf.Min(2, candidates.Length);
                selectedIndex = UnityEngine.Random.Range(0, maxIndex);
            }

            BossDebuffSO selectedDebuff = candidates[selectedIndex];
            GameLog.Info(
                $"[BossDebuffSelect] 스테이지 {stageNum}, 보스 {bossType}, "
                + $"후보 {candidates.Length}개 중 {selectedIndex + 1}번 '{GetDebuffDisplayName(selectedDebuff)}' 선택");
            return selectedDebuff;
        }
        // if (candidates != null && candidates.Length > 0)
        //     return candidates[UnityEngine.Random.Range(0, candidates.Length)];

        // if (availableDebuffs != null && availableDebuffs.Length > 0)
        //     return availableDebuffs[UnityEngine.Random.Range(0, availableDebuffs.Length)];

        return null;
    }

    /// <summary>Play Mode 테스트 중 지정한 디버프를 현재 관에 즉시 초기화하여 적용합니다.</summary>
    public void DebugApplyBossDebuff(BossDebuffSO debuff)
    {
        ResetCurrentDebuff();
        currentDebuff = debuff;

        if (currentDebuff != null)
        {
            currentDebuff.InitDebuff();
            GameLog.Info($"[BossDebuffDebug] 현재 관에 '{GetDebuffDisplayName(currentDebuff)}' 디버프를 즉시 적용했습니다.");
        }
        else
        {
            GameLog.Info("[BossDebuffDebug] 현재 관의 디버프를 해제했습니다.");
        }

        FieldUIManager.Instance?.RefreshBossDebuffIndicator();
        CardManager_2.Instance?.RefreshSelectionPreviewNow();
    }

    private BossDebuffSO[] ResolveBossDebuffCandidates(BossType bossType)
    {
        if (bossType == BossType.None || bossDebuffSets == null)
            return null;

        for (int i = 0; i < bossDebuffSets.Length; i++)
        {
            BossDebuffSet set = bossDebuffSets[i];
            if (set == null || set.bossType != bossType)
                continue;

            return BuildValidDebuffList(set.debuffs);
        }

        return null;
    }

    /// <summary>저장된 에셋 이름을 해당 보스의 디버프 풀과 공용 풀에서 찾아 다시 연결합니다.</summary>
    public BossDebuffSO ResolveSavedBossDebuff(BossType bossType, string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        BossDebuffSO[] bossCandidates = ResolveBossDebuffCandidates(bossType);
        BossDebuffSO resolved = FindDebuffByAssetName(bossCandidates, assetName);
        return resolved != null ? resolved : FindDebuffByAssetName(availableDebuffs, assetName);
    }

    private static BossDebuffSO FindDebuffByAssetName(BossDebuffSO[] candidates, string assetName)
    {
        if (candidates == null)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            BossDebuffSO candidate = candidates[i];
            if (candidate != null && string.Equals(candidate.name, assetName, System.StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private static BossDebuffSO[] BuildValidDebuffList(BossDebuffSO[] source)
    {
        if (source == null || source.Length == 0)
            return null;

        List<BossDebuffSO> validDebuffs = new List<BossDebuffSO>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
                validDebuffs.Add(source[i]);
        }

        return validDebuffs.Count > 0 ? validDebuffs.ToArray() : null;
    }

    private static string GetDebuffDisplayName(BossDebuffSO debuff)
    {
        if (debuff == null)
            return "없음";

        return !string.IsNullOrWhiteSpace(debuff.debuffName)
            ? debuff.debuffName.Trim()
            : debuff.name;
    }

    public long ApplyCurrentStageBaseTargetScore()
    {
        targetScore = RoundScoreToUnit(ResolveGeneratedBaseTargetScore(curStageIndex));

        if (FieldUIManager.Instance != null)
        {
            FieldUIManager.Instance.UpdateScoreUI(targetScore, curRoundScore, 1, JokBoType.None);
        }

        return targetScore;
    }

    private void ResetCurrentDebuff()
    {
        if (currentDebuff == null)
            return;

        currentDebuff.ResetDebuff();
        currentDebuff = null;
    }

    private void CancelPendingRoundCallbacks()
    {
        CancelGoFailureEscape();
        CancelBattleResultSettlement();
        CancelInvoke(nameof(TriggerGoStopPhase));
        CancelInvoke(nameof(GameOver));
        CancelInvoke(nameof(ResolvePendingPostPlayOutcome));
        if (bossDefeatRoutine != null)
        {
            StopCoroutine(bossDefeatRoutine);
            bossDefeatRoutine = null;
            EncounterPresentationManager.Instance?.ResetDefeatPresentation();
        }
        HasPendingPostPlayOutcome = false;
    }

    private void CancelGoFailureEscape()
    {
        if (goFailureEscapeRoutine != null)
        {
            StopCoroutine(goFailureEscapeRoutine);
            goFailureEscapeRoutine = null;
            EncounterPresentationManager.Instance?.ResetDefeatPresentation();
        }
        isResolvingGoFailure = false;
    }

    private IEnumerator ResolveGoFailureEscapeRoutine()
    {
        StageInfo encounter = activeStageInfo;
        int stage = curStageIndex;
        int round = curRoundIndex;
        int goCount = GoStopManager.Instance.currentGoCount;
        // StartCoroutine must return its handle before a missing actor can complete immediately.
        yield return null;
        bool completed = true;
        EncounterPresentationManager presentation = EncounterPresentationManager.Instance;
        if (presentation != null && presentation.isActiveAndEnabled)
        {
            completed = false;
            yield return presentation.PlayEnemyEscapeRoutine(() => completed = true);
        }
        goFailureEscapeRoutine = null;
        if (!completed || !isActiveAndEnabled || !isResolvingGoFailure || isReturningToMap
            || IsBattleResultOpen || activeStageInfo != encounter || curStageIndex != stage
            || curRoundIndex != round || GoStopManager.Instance == null
            || GoStopManager.Instance.currentGoCount != goCount)
            yield break;

        // Keep the guard until the next round/reset. Escaping never enters clear/kill rewards.
        ShowGoFailureBattleResult();
    }

    private void ShowGoFailureBattleResult()
    {
        pendingBattleResultMoney = 0;
        hasPendingBattleResultSettlement = true;
        hasPendingRunClearResult = false;
        isRoundCleared = false;
        int feesPaid = GoStopManager.Instance != null ? GoStopManager.Instance.TotalEntryFeesPaid : 0;
        BattleResultUI.Instance?.ShowGoFailureResult(targetScore, feesPaid);
        MoveCameraToEncounterView();
        SetBattleResultOpen(true);
    }

    internal bool TryQueueBattleResultMoney(int amount)
    {
        if (!isPreparingBattleResultRewards)
            return false;
        pendingBattleResultMoney += amount;
        return true;
    }

    private void PrepareBattleResultRewards(Action calculateRewards)
    {
        isPreparingBattleResultRewards = true;
        try { calculateRewards(); }
        finally { isPreparingBattleResultRewards = false; }
    }

    private void CancelBattleResultSettlement()
    {
        ++battleResultVersion;
        if (roundClearRoutine != null)
        {
            StopCoroutine(roundClearRoutine);
            roundClearRoutine = null;
        }
        isRoundClearRoutineRunning = false;
        isPreparingBattleResultRewards = false;
        hasPendingBattleResultSettlement = false;
        pendingBattleResultMoney = 0;
        hasPendingRunClearResult = false;
        if (IsBattleResultOpen)
            SetBattleResultOpen(false);
    }

    private void SetBattleResultOpen(bool isOpen)
    {
        IsBattleResultOpen = isOpen;

        if (FieldUIManager.Instance != null)
            FieldUIManager.Instance.SetResultCanvasVisible(isOpen);
    }

    private static void MoveCameraToEncounterView()
    {
        if (TableTransitionManager.instance != null)
            TableTransitionManager.instance.TryMoveToPreviewLookView();
    }

    public void OnPlayCards(JokBoType jokbo, long earnScore, long multiplier, bool deferPostPlayOutcome = false)
    {
        if (!rustyPliersPreparedForCurrentHand)
            PrepareRustyPliersForHand(jokbo);

        if (currentDebuff is DebuffGumiho)
        {
            ArtifactManager.Instance?.ShuffleArtifacts();
        }

        LastEarnedScoreForPresentation = System.Math.Max(0L, earnScore);
        curRoundScore = ScoreSafeMath.AddScore(curRoundScore, earnScore); // 이게 지금까지 획득한 점수에 점수 더하기
        remaningHands--; // 남은 핸드 횟수 차감
        SubmittedHandsThisRound++;

        JokboPlayTracker.Instance.RecordJokboPlay(jokbo);

        FieldUIManager.Instance.UpdateScoreUI(targetScore, curRoundScore, multiplier, jokbo);

        // 획득한 점수가 저주 등이 적용된 목표 점수 이상인지 체크
        if (curRoundScore >= targetScore)
        {
            ClearedUsingLastHand = LastSubmissionUsedLastHand;
            isRoundCleared = true;
            HasPendingPostPlayOutcome = true;
            if (!deferPostPlayOutcome)
                Invoke(nameof(ResolvePendingPostPlayOutcome), 0.6f);
            return;
        }

        if (remaningHands <= 0 && !isRoundCleared)
        {
            HasPendingPostPlayOutcome = true;
            if (!deferPostPlayOutcome)
                Invoke(nameof(ResolvePendingPostPlayOutcome), 0.6f);
        }

        FieldUIManager.Instance.UpdateFieldUI(remaningHands, remaningDrops);
        rustyPliersPreparedForCurrentHand = false;
    }

    public void RecordSubmittedCards(int cardCount)
    {
        if (cardCount > 0)
            SubmittedCardCountThisRound += cardCount;
    }

    public bool RetryCurrentRoundAfterDefeatProtection()
    {
        if (!IsCombatStageInfo(activeStageInfo))
            return false;

        CancelInvoke();
        isRoundCleared = false;
        HasPendingPostPlayOutcome = false;
        SetBattleResultOpen(false);
        StartRound(curRoundIndex);
        return isInCombat;
    }

    public void PrepareRustyPliersForHand(JokBoType submittedJokbo)
    {
        if (rustyPliersPreparedForCurrentHand)
            return;

        rustyPliersPreparedForCurrentHand = true;
        currentRustyPliersTargetJokbo = JokBoType.None;

        if (ArtifactManager.Instance == null
            || !ArtifactManager.Instance.HasArtifact(RustyPliersArtifactID)
            || submittedJokbo == JokBoType.None)
            return;

        currentRustyPliersTargetJokbo = PickRustyPliersTargetJokbo();
        if (currentRustyPliersTargetJokbo == JokBoType.None)
            return;

        string targetName = ScoreManager.Instance != null
            ? ScoreManager.Instance.GetJokboDisplayName(currentRustyPliersTargetJokbo)
            : currentRustyPliersTargetJokbo.ToString();
        GameLog.Info($"[녹슨 펜치] 이번 대상 족보: {targetName}");

        if (submittedJokbo != currentRustyPliersTargetJokbo)
            return;

        long beforeTargetScore = targetScore;
        targetScore = ScoreSafeMath.MultiplyScoreCeiling(targetScore, RustyPliersTargetMultiplier, 1L);
        GameLog.Info($"[녹슨 펜치] 대상 족보 일치, 요구점수 감소: {beforeTargetScore} -> {targetScore}");
        FieldUIManager.Instance?.UpdateScoreUI(targetScore, curRoundScore, 1, submittedJokbo);
    }

    private static JokBoType PickRustyPliersTargetJokbo()
    {
        if (ScoreManager.Instance == null || ScoreManager.Instance.ActiveJokboRuleProfile == null)
            return JokBoType.None;

        List<JokBoType> candidates = new List<JokBoType>();
        List<JokboRuleDefinition> rules = ScoreManager.Instance.ActiveJokboRuleProfile.Rules;
        for (int i = 0; i < rules.Count; i++)
        {
            JokboRuleDefinition rule = rules[i];
            if (rule == null || !rule.enabled || rule.jokboType == JokBoType.None || candidates.Contains(rule.jokboType))
                continue;

            candidates.Add(rule.jokboType);
        }

        return candidates.Count > 0
            ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
            : JokBoType.None;
    }

    public bool ApplyArtifactTargetReduction(long reduction, string sourceName)
    {
        // OnHandPlay effects finish before the queued clear outcome is resolved.
        if (!isInCombat || (isRoundCleared && !HasPendingPostPlayOutcome)
            || reduction <= 0L || targetScore <= 1L)
            return false;

        long beforeTarget = targetScore;
        long afterTarget = beforeTarget - System.Math.Min(reduction, beforeTarget - 1L);
        if (CheongaOriginalTargetScore <= 0L) cheongaOriginalTargetScore = beforeTarget;
        cheongaTargetGoCount = GoStopManager.Instance != null ? GoStopManager.Instance.currentGoCount : 0;
        targetScore = afterTarget;
        CancelInvoke(nameof(ResolvePendingPostPlayOutcome));
        CancelInvoke(nameof(GameOver));
        FieldUIManager.Instance?.UpdateScoreUI(targetScore, curRoundScore, 1, JokBoType.None);
        if (curRoundScore >= targetScore)
        {
            // The goal reduction belongs to the same submitted hand, including last-hand wins.
            ClearedUsingLastHand = LastSubmissionUsedLastHand;
            isRoundCleared = true;
            HasPendingPostPlayOutcome = true;
        }
        GameLog.Info($"[{sourceName}] 목표점수 {beforeTarget} → {targetScore}. 현재 점수: {curRoundScore}");
        return true;
    }

    public bool ApplyArtifactScoreBonus(long scoreBonus, string sourceName)
    {
        if (!isInCombat || isRoundCleared || scoreBonus <= 0)
            return false;

        CancelInvoke(nameof(ResolvePendingPostPlayOutcome));
        CancelInvoke(nameof(GameOver));
        curRoundScore = ScoreSafeMath.AddScore(curRoundScore, scoreBonus);
        LastEarnedScoreForPresentation = scoreBonus;

        FieldUIManager.Instance?.UpdateScoreUI(targetScore, curRoundScore, 1, JokBoType.None);

        if (curRoundScore >= targetScore)
        {
            // 제출 이후 다른 행동의 기물 점수로 이긴 경우를 마지막 핸드 승리로 잘못 판정하지 않습니다.
            ClearedUsingLastHand = false;
            isRoundCleared = true;
            HasPendingPostPlayOutcome = true;
        }

        GameLog.Info($"[{sourceName}] 점수 {scoreBonus}점을 추가했습니다. 현재 점수: {curRoundScore}/{targetScore}");
        return true;
    }

    public void SchedulePendingPostPlayOutcome(bool deferPostPlayOutcome)
    {
        if (!HasPendingPostPlayOutcome || deferPostPlayOutcome)
            return;

        CancelInvoke(nameof(ResolvePendingPostPlayOutcome));
        Invoke(nameof(ResolvePendingPostPlayOutcome), 0.6f);
    }

    private void TriggerGoStopPhase()
    {
        if (IsCurrentRoundBoss || activeStageInfo?.type != StageType.Battle)
            return;
        HasPendingPostPlayOutcome = false;
        // 보상 계산 (1라 3, 2라 4, 3라 5원 등 기획에 맞게)
        int expectedReward = curRoundIndex + 3; 

        GoStopManager.Instance?.EnterGoStopPhase(curRoundScore, targetScore, expectedReward);
    }

    public void ResolvePendingPostPlayOutcome()
    {
        if (!HasPendingPostPlayOutcome)
            return;

        CancelInvoke(nameof(TriggerGoStopPhase));
        CancelInvoke(nameof(GameOver));
        CancelInvoke(nameof(ResolvePendingPostPlayOutcome));

        HasPendingPostPlayOutcome = false;

        if (isRoundCleared)
        {
            if (IsCurrentRoundBoss)
            {
                if (bossDefeatRoutine == null)
                    bossDefeatRoutine = StartCoroutine(ResolveBossDefeatRoutine());
                return;
            }

            TriggerGoStopPhase();
            return;
        }

        if (remaningHands <= 0)
            GameOver();
    }

    private IEnumerator ResolveBossDefeatRoutine()
    {
        EncounterPresentationManager encounterPresentation = EncounterPresentationManager.Instance;
        if (encounterPresentation != null)
            yield return encounterPresentation.PlayFinalDefeatRoutine(true);

        bossDefeatRoutine = null;
        RoundClear();
    }

    // 카드 버렸을 때 
    public void OnDropCards(int discardedCardCount = 0, List<BaseCardSO> discardedCards = null)
    {
        if (remaningDrops > 0)
        {
            bool freeDiscard = ArtifactManager.Instance != null
                && ArtifactManager.Instance.HasSynergyTier(ArtifactSynergyType.ImmortalPlay, 3)
                && UnityEngine.Random.value < ArtifactManager.Instance.ScaleSynergyChance(0.25f);

            if (!freeDiscard)
                remaningDrops--;
            else
                GameLog.Info("[시너지] 신선놀음 3: 버리기 횟수를 소모하지 않았습니다.");

            if (RunStatTracker.Instance != null)
            {
                RunStatTracker.Instance.AddDiscardedCards(discardedCardCount);
            }

            // 기물: 카드 버리기 트리거
            if (GameManager.instance != null)
            {
                GameManager.instance.OnCardsDiscarded(discardedCardCount, discardedCards);
            }
            FieldUIManager.Instance.UpdateFieldUI(remaningHands, remaningDrops);
        }
    }

    public void RoundClear(int providedReward = -1, int goStopReward = 0)
    {
        if (isRoundClearRoutineRunning || hasPendingBattleResultSettlement || IsBattleResultOpen || isReturningToMap)
            return;

        isRoundClearRoutineRunning = true;
        roundClearRoutine = StartCoroutine(RoundClearRoutine(providedReward, goStopReward));
    }

    private IEnumerator RoundClearRoutine(int providedReward, int goStopReward)
    {
        GameLog.Info("Clear");
        int version = battleResultVersion;
        StageInfo encounter = activeStageInfo;
        int stage = curStageIndex;
        int round = curRoundIndex;
        pendingBattleResultMoney = 0;
        try
        {
            // Keep the owned handle available even if there are no acquisition animations.
            yield return null;
            if (!BattleResultPreparationIsCurrent(version, encounter, stage, round)) yield break;
            if (SubmittedHandsThisRound == 1 && curRoundScore >= targetScore && providedReward != 0)
                ArtifactManager.Instance?.RestoreFirstHandClearUsage();

            int baseReward = providedReward == -1 ? curRoundIndex + 3 : providedReward;
            PrepareBattleResultRewards(() =>
            {
                EconomyManager.instance?.AddMoney(baseReward + goStopReward);
                GameManager.instance?.OnEnemyKilled(IsCurrentRoundBoss);
            });

            if (GameManager.instance != null)
            {
                if (ConsumableManager.Instance != null && ConsumableManager.Instance.HasPendingAcquirePresentation)
                {
                    MoveCameraToEncounterView();
                    yield return new WaitForSecondsRealtime(0.25f);
                    if (!BattleResultPreparationIsCurrent(version, encounter, stage, round)) yield break;
                    yield return ConsumableManager.Instance.PlayPendingAcquirePresentationRoutine();
                }
                if (!BattleResultPreparationIsCurrent(version, encounter, stage, round)) yield break;
                PrepareBattleResultRewards(() => GameManager.instance.OnCombatRoundEnd(remaningHands, remaningDrops));
            }

            isRoundCleared = false;
            hasPendingBattleResultSettlement = true;
            int feesPaid = GoStopManager.Instance != null ? GoStopManager.Instance.TotalEntryFeesPaid : 0;
            BattleResultUI.Instance?.ShowResult(targetScore, baseReward, currentRoundInterestMoney,
                currentRoundArtifactBonusMoney, currentRoundSynergyBonusMoney, goStopReward, feesPaid);

            MoveCameraToEncounterView();
            SetBattleResultOpen(true);
            if (ShouldQueueRunClearResult())
                hasPendingRunClearResult = true;
        }
        finally
        {
            if (version == battleResultVersion)
            {
                isRoundClearRoutineRunning = false;
                roundClearRoutine = null;
                if (!IsBattleResultOpen)
                {
                    pendingBattleResultMoney = 0;
                    hasPendingBattleResultSettlement = false;
                }
            }
        }
    }

    private bool BattleResultPreparationIsCurrent(int version, StageInfo encounter, int stage, int round)
    {
        return version == battleResultVersion && isActiveAndEnabled && !isReturningToMap
            && activeStageInfo == encounter && curStageIndex == stage && curRoundIndex == round;
    }

    internal int BattleResultSettlementVersion => battleResultVersion;

    internal void ContinueFromBattleResult(int expectedVersion)
    {
        if (expectedVersion == battleResultVersion) ContinueFromBattleResult();
    }

    public void ContinueFromBattleResult()
    {
        if (!IsBattleResultOpen || !hasPendingBattleResultSettlement || isRoundClearRoutineRunning)
            return;

        // The receipt includes previously paid fees/in-combat income; only unpaid rewards are deposited.
        int reward = pendingBattleResultMoney;
        hasPendingBattleResultSettlement = false;
        pendingBattleResultMoney = 0;
        if (reward != 0)
            EconomyManager.instance?.AddBattleResultMoney(reward);
        if (hasPendingRunClearResult)
        {
            ShowPendingRunClearResult();
            return;
        }

        ReturnToMap();
    }

    private void ShowPendingRunClearResult()
    {
        hasPendingRunClearResult = false;
        SetBattleResultOpen(false);

        if (GameManager.instance != null)
            GameManager.instance.OnRunCompleted();

        if (SaveManager.instance != null)
            SaveManager.instance.UpdateAndSaveGameResult(true);

        if (GameResultUIManager.Instance != null)
            GameResultUIManager.Instance.ShowResult(true);
    }

    private bool ShouldQueueRunClearResult()
    {
        int finalStageNumber = Mathf.Max(1, runClearStageNumber);
        int finalRoundIndex = Mathf.Max(0, GetRoundCount(curStageIndex) - 1);

        return IsCurrentRoundBoss
            && curStageIndex + 1 >= finalStageNumber
            && curRoundIndex >= finalRoundIndex;
    }

    // 결과창, 상점 확인 후 지도로 되돌아갈 때 호출되는 함수
    public void ReturnToMap()
    {
        if (isReturningToMap)
            return;

        CancelPendingRoundCallbacks();
        isReturningToMap = true;
        SoundManager.instance?.EndBossBGM();
        // Restore temporary combat stats before the next world destroys its debuff instances.
        ResetCurrentDebuff();
        SetBattleResultOpen(false);
        isInCombat = false;
        IsCurrentRoundBoss = false;
        GameLog.Info("[CombatManager] 결과창 종료. 현재 전투를 마치고 맵으로 복귀합니다.");
        FieldUIManager.Instance?.RefreshBossDebuffIndicator();

        bool advancedStage = AdvanceCombatProgress();
        GameLog.Info($"[CombatManager] 맵 복귀 처리. 스테이지 완료 여부: {advancedStage}, 다음 전투 인덱스: {curStageIndex} / {curRoundIndex}");
        activeStageInfo = null;

        if (TableTransitionManager.instance != null)
        {
            TableTransitionManager.instance.StartTransitionToMap();
            return;
        }

        Debug.LogWarning("[CombatManager] TableTransitionManager가 없어 맵 복귀 연출을 실행하지 못했습니다.");
    }

    // 지도에서 전투 비석눌렀을 때 호출해야할 함수
    public void GoToNextCombatNode()
    {
        if (MapManager.instance != null && MapManager.instance.UseGraphMapSystem)
        {
            Debug.LogWarning("[CombatManager] 그래프 맵에서는 다음 전투를 직접 시작하지 않습니다. 맵 노드 진입을 사용하세요.");
            return;
        }

        // 1. 다음 라운드가 있는지 체크
        if (curRoundIndex < GetRoundCount(curStageIndex) - 1)
        {
            StartRound(curRoundIndex + 1);
        }
        else // 2. 마지막 라운드였다면 다음 스테이지로
        {
            curStageIndex++;
            StartRound(0);
        }
    }

    public void GameOver()
    {
        if (isResolvingGoFailure || isRoundClearRoutineRunning || IsBattleResultOpen || isReturningToMap)
            return;
        HasPendingPostPlayOutcome = false;
        bool wasBossDefeat = IsCurrentRoundBoss;
        isInCombat = false;
        IsCurrentRoundBoss = false;
        GameLog.Info("GameOver");
        FieldUIManager.Instance?.RefreshBossDebuffIndicator();

        // 기물: 사망 방어 체크 (수호장승 등)
        if (GameManager.instance != null && GameManager.instance.TryPreventDefeat(wasBossDefeat))
        {
            GameLog.Info("기물 효과로 부활했습니다!");
            return; // 패배 중단
        }

        UnlockManager.instance?.RecordJourneyDefeat();

        if (!wasBossDefeat && GoStopManager.Instance != null && GoStopManager.Instance.currentGoCount > 0)
        {
            GameLog.Info("[CombatManager] Go 진행 중 실패! 보상 없이 제령 비용 손실만 표시하고 관을 종료합니다.");

            // 실제 GO 패배에만 도주를 보여준다. 부활과 수수료 실패는 이 경로에 오지 않는다.
            isResolvingGoFailure = true;
            goFailureEscapeRoutine = StartCoroutine(ResolveGoFailureEscapeRoutine());
            return;
        }

        if(GameResultUIManager.Instance != null)
        {
            MoveCameraToEncounterView();
            RunStatTracker.Instance?.MarkLastJourneyNodeAsGameOver();
            if (UnlockManager.instance != null)
                UnlockManager.instance.CheckUnlockConditions();

            if (SaveManager.instance != null) SaveManager.instance.UpdateAndSaveGameResult(false);

            GameResultUIManager.Instance.ShowResult(false);
        }
    }

    public void Surrender()
    {
        CancelPendingRoundCallbacks();
        isInCombat = false;
        IsCurrentRoundBoss = false;
        SetBattleResultOpen(false);
        GameLog.Info("[CombatManager] 플레이어가 런을 포기했습니다.");
        FieldUIManager.Instance?.RefreshBossDebuffIndicator();

        if (RunStatTracker.Instance != null)
        {
            RunStatTracker.Instance.deathReason = "포기";
            RunStatTracker.Instance.MarkLastJourneyNodeAsGameOver();
        }

        if (UnlockManager.instance != null)
            UnlockManager.instance.CheckUnlockConditions();

        if (SaveManager.instance != null)
            SaveManager.instance.UpdateAndSaveGameResult(false);

        if (GameResultUIManager.Instance != null)
        {
            MoveCameraToEncounterView();
            GameResultUIManager.Instance.ShowResult(false);
        }
    }

    public void ControlHands(int amount)
    {
        remaningHands += amount;
        
        remaningHands = Mathf.Max(0, remaningHands);

        // 데이터가 바뀌었으니 구독자들에게 방송을 켭니다. (?는 null 체크입니다)
        OnChangedHandsDropsCount?.Invoke(remaningHands, remaningDrops);

        GameLog.Info($"[CombatManager] 핸드 횟수가 {amount}만큼 변동되었습니다. 현재 남은 핸드 횟수: {remaningHands}");
    }

    public void ControlDrops(int amount)
    {
        remaningDrops += amount;
        
        remaningDrops = Mathf.Max(0, remaningDrops);
        // 데이터가 바뀌었으니 구독자들에게 방송을 켭니다. (?는 null 체크입니다)
        OnChangedHandsDropsCount?.Invoke(remaningHands, remaningDrops);

        GameLog.Info($"[CombatManager] 드랍 횟수가 {amount}만큼 변동되었습니다. 현재 남은 드랍 횟수: {remaningDrops}");
    }


    /// <summary>클릭한 맵 노드 정보를 이번 전투의 고정 컨텍스트로 저장하고 전투를 시작합니다.</summary>
    public void EnterCombatFromMap(StageInfo stageInfo)
    {
        if (PrepareCombatEntryFromMap(stageInfo))
            ActivatePreparedCombatEntry();
    }

    /// <summary>
    /// 적/보스 프리뷰에서 최종 목표 점수를 보여줄 수 있도록 라운드 수치만 먼저 준비합니다.
    /// 실제 카드 생성은 <see cref="ActivatePreparedCombatEntry"/>까지 지연됩니다.
    /// </summary>
    public bool PrepareCombatEntryFromMap(StageInfo stageInfo)
    {
        if (!IsCombatStageInfo(stageInfo))
        {
            Debug.LogError("[CombatManager] 유효한 전투 StageInfo 없이 전투를 시작할 수 없습니다.");
            ResetCombatBoardStateForInvalidStage();
            return false;
        }

        activeStageInfo = stageInfo;
        SynchronizeProgressWithCurrentMap(stageInfo);
        GameLog.Info(
            $"[BossStageHandoff] 전투 진입 StageInfo: "
            + $"{(stageInfo != null ? stageInfo.GetBossDisplayName() : "없음")} / "
            + $"{(stageInfo != null && stageInfo.bossDebuff != null ? stageInfo.bossDebuff.name : "디버프 없음")}");
        GameLog.Info(
            $"[CombatManager] 그래프 전투 컨텍스트 적용. "
            + $"월드={curStageIndex}, 난이도 단계={curRoundIndex}, 타입={stageInfo.type}");
        return PrepareRoundState(curRoundIndex);
    }

    /// <summary>전투판 등장이 끝난 뒤 준비된 라운드의 카드를 생성합니다.</summary>
    public void ActivatePreparedCombatEntry()
    {
        if (!hasPendingRoundActivation)
        {
            Debug.LogWarning("[CombatManager] 준비된 전투 진입 상태가 없어 카드 생성을 건너뜁니다.");
            return;
        }

        ActivatePreparedRound();
    }

    /// <summary>기존 디버그 진입 경로에서는 현재 맵 노드 정보를 사용해 전투를 시작합니다.</summary>
    public void EnterCombatFromMap()
    {
        EnterCombatFromMap(MapManager.instance != null ? MapManager.instance.GetCurrentStageInfo() : null);
    }

    public bool AdvanceCombatProgress()
    {
        if (MapManager.instance != null && MapManager.instance.UseGraphMapSystem)
        {
            bool completedBossNode = activeStageInfo != null && activeStageInfo.type == StageType.Boss;
            GameLog.Info("[CombatManager] 그래프 맵 진행은 MapManager가 관리하므로 구 3관 인덱스를 증가시키지 않습니다.");
            return completedBossNode;
        }

        int roundCount = GetRoundCount(curStageIndex);
        if (roundCount <= 0)
        {
            Debug.LogError($"[CombatManager] 전투 진행 라운드 수가 올바르지 않습니다. stageIndex={curStageIndex}");
            return true;
        }

        bool advancedStage = false;

        // 1. 다음 라운드가 있는지 체크
        if (curRoundIndex < roundCount - 1)
        {
            curRoundIndex++; // 라운드 인덱스만 증가
        }
        else // 2. 마지막 라운드였다면 다음 스테이지로
        {
            curStageIndex++;
            advancedStage = true;

            curRoundIndex = 0; // 다음 스테이지의 1라운드로 세팅
        }
        GameLog.Info($"[시스템] 장전 완료! 다음 전투는 {curStageIndex + 1}스테이지 {curRoundIndex + 1}관입니다.");
        return advancedStage;
    }

    public void ResetCombatProgressForNewRun()
    {
        SoundManager.instance?.EndBossBGM();
        CancelPendingRoundCallbacks();
        ResetCurrentDebuff();

        curStageIndex = 0;
        curRoundIndex = 0;
        LastEarnedScoreForPresentation = 0;
        ClearCheongaTargetHistory();
        curRoundScore = 0;
        targetScore = 0;
        maxHands = 0;
        maxDrops = 0;
        remaningHands = 0;
        remaningDrops = 0;
        SubmittedHandsThisRound = 0;
        SubmittedCardCountThisRound = 0;
        maxHandCard = 5;
        VoucherManager.Instance?.ResetAppliedMaxHandCardModifierTracking();
        ArtifactManager.Instance?.ResetHandSizeModifierMarkers();
        roundDiscardBonusMultiplier = 0.0;
        isRoundCleared = false;
        isInCombat = false;
        IsCurrentRoundBoss = false;
        isReturningToMap = false;
        hasPendingRunClearResult = false;
        activeStageInfo = null;
        SetBattleResultOpen(false);

        GoStopManager.Instance?.ResetGoStop();

        if (FieldUIManager.Instance != null)
        {
            FieldUIManager.Instance.UpdateFieldUI(remaningHands, remaningDrops);
            FieldUIManager.Instance.UpdateScoreUI(targetScore, curRoundScore, 1, JokBoType.None, false);
            FieldUIManager.Instance.SetResultCanvasVisible(false);
            FieldUIManager.Instance.RefreshBossDebuffIndicator();
        }
    }

    public void DebugSetStageForWorldTest(int zeroBasedStageIndex, int zeroBasedRoundIndex)
    {
        CancelPendingRoundCallbacks();
        CancelInvoke(nameof(RoundClear));
        CancelInvoke(nameof(GameOver));
        ResetCurrentDebuff();

        curStageIndex = Mathf.Max(0, zeroBasedStageIndex);
        curRoundIndex = Mathf.Clamp(zeroBasedRoundIndex, 0, GetRoundCount(curStageIndex) - 1);
        LastEarnedScoreForPresentation = 0;
        ClearCheongaTargetHistory();
        curRoundScore = 0;
        SubmittedHandsThisRound = 0;
        SubmittedCardCountThisRound = 0;
        isRoundCleared = false;
        isInCombat = false;
        isReturningToMap = false;
        hasPendingRunClearResult = false;
        IsCurrentRoundBoss = false;
        SetBattleResultOpen(false);

        if (TryResolveRoundSetup(curStageIndex, curRoundIndex, out int stageNum, out RoundData round, out _))
        {
            targetScore = round.targetScore;
            maxHands = round.maxHands;
            maxDrops = round.maxDrops;
            remaningHands = round.maxHands;
            remaningDrops = round.maxDrops;
        }
        else
        {
            targetScore = 0;
            maxHands = 0;
            maxDrops = 0;
            remaningHands = 0;
            remaningDrops = 0;
            stageNum = curStageIndex + 1;
        }

        currentRoundArtifactBonusMoney = 0;
        currentRoundSynergyBonusMoney = 0;
        currentRoundInterestMoney = 0;
        roundDiscardBonusMultiplier = 0.0;
        GoStopManager.Instance?.ResetGoStop();

        if (FieldUIManager.Instance != null)
        {
            FieldUIManager.Instance.Update_Stage_Round_UI(stageNum, curRoundIndex);
            FieldUIManager.Instance.UpdateFieldUI(remaningHands, remaningDrops);
            FieldUIManager.Instance.UpdateScoreUI(targetScore, curRoundScore, 1, JokBoType.None, false);
            FieldUIManager.Instance.RefreshBossDebuffIndicator();
        }
    }

    private bool TryResolveRoundSetup(int stageIndex, int roundIndex, out int stageNum, out RoundData round, out string sourceName)
    {
        stageNum = Mathf.Max(1, stageIndex + 1);
        round = null;
        sourceName = "Generated";

        return TryCreateGeneratedRound(stageIndex, roundIndex, out round);
    }

    private bool TryCreateGeneratedRound(int stageIndex, int roundIndex, out RoundData round)
    {
        round = null;
        if (roundIndex < 0 || roundIndex >= 3)
            return false;

        round = new RoundData
        {
            targetScore = ResolveGeneratedRequiredScore(stageIndex, roundIndex),
            maxHands = Mathf.Max(0, generatedDefaultMaxHands),
            maxDrops = Mathf.Max(0, generatedDefaultMaxDrops),
            isBoss = activeStageInfo != null && activeStageInfo.type == StageType.Boss
        };

        return true;
    }

    public long ResolveGeneratedRequiredScore(int stageIndex, int roundIndex)
    {
        long baseTargetScore = ResolveGeneratedBaseTargetScore(stageIndex);
        double roundMultiplier = ResolveGeneratedRoundMultiplier(roundIndex);
        return RoundScoreToUnit(baseTargetScore * roundMultiplier);
    }

    private long ResolveGeneratedBaseTargetScore(int stageIndex)
    {
        int safeStageIndex = Mathf.Max(0, stageIndex);
        if (safeStageIndex < ConfigurableBaseScoreStageCount)
        {
            if (generatedBaseTargetScores != null && safeStageIndex < generatedBaseTargetScores.Length)
                return Mathf.Max(1, generatedBaseTargetScores[safeStageIndex]);

            if (generatedBaseTargetScores != null && generatedBaseTargetScores.Length > 0)
                return Mathf.Max(1, generatedBaseTargetScores[generatedBaseTargetScores.Length - 1]);

            return RoundScoreToUnit(EndlessBaseScore);
        }

        int stageNumber = safeStageIndex + 1;
        int endlessStageOffset = stageNumber - ConfigurableBaseScoreStageCount;
        double acceleratedGrowth = Math.Pow(
            EndlessStepGrowth * endlessStageOffset,
            1d + (Math.Max(0d, generatedEndlessExponentGrowth) * endlessStageOffset));
        double growthMultiplier = Math.Pow(EndlessBaseGrowth + acceleratedGrowth, endlessStageOffset);

        return RoundScoreToUnit(EndlessBaseScore * growthMultiplier);
    }

    private double ResolveGeneratedRoundMultiplier(int roundIndex)
    {
        switch (roundIndex)
        {
            case 0: return Math.Max(0d, generatedFirstRoundMultiplier);
            case 1: return Math.Max(0d, generatedSecondRoundMultiplier);
            case 2: return Math.Max(0d, generatedBossRoundMultiplier);
            default: return Math.Max(0d, generatedFirstRoundMultiplier);
        }
    }

    private long RoundScoreToUnit(double rawScore)
    {
        if (double.IsNaN(rawScore) || double.IsInfinity(rawScore) || rawScore >= long.MaxValue)
            return long.MaxValue;

        int roundingUnit = Mathf.Max(1, generatedScoreRoundingUnit);
        double rounded = Math.Round(rawScore / roundingUnit) * roundingUnit;
        if (rounded >= long.MaxValue)
            return long.MaxValue;

        return Math.Max(roundingUnit, (long)rounded);
    }

    private void SynchronizeProgressWithCurrentMap(StageInfo stageInfo)
    {
        MapManager mapManager = MapManager.instance;
        if (mapManager != null)
        {
            curStageIndex = Mathf.Max(0, mapManager.currentWorldIndex);
            curRoundIndex = mapManager.ResolveCurrentCombatRoundIndex(stageInfo);
            return;
        }

        curStageIndex = Mathf.Max(0, curStageIndex);
        curRoundIndex = stageInfo.type == StageType.Boss ? 2 : 0;
    }

    private static bool IsCombatStageInfo(StageInfo stageInfo)
    {
        return stageInfo != null
            && (stageInfo.type == StageType.Battle || stageInfo.type == StageType.Boss);
    }

    private int GetRoundCount(int stageIndex)
    {
        return 3;
    }

    private void ResetCombatBoardStateForInvalidStage()
    {
        CancelPendingRoundCallbacks();
        SoundManager.instance?.EndBossBGM();
        hasPendingRoundActivation = false;
        ResetCurrentDebuff();
        activeStageInfo = null;
        isInCombat = false;
        isRoundCleared = false;
        isReturningToMap = false;
        IsCurrentRoundBoss = false;
        LastEarnedScoreForPresentation = 0;
        ClearCheongaTargetHistory();
        curRoundScore = 0;
        targetScore = 0;
        maxHands = 0;
        maxDrops = 0;
        remaningHands = 0;
        remaningDrops = 0;
        SubmittedHandsThisRound = 0;
        SubmittedCardCountThisRound = 0;
        roundDiscardBonusMultiplier = 0.0;

        GoStopManager.Instance?.ResetGoStop();

        if (CardManager_2.Instance != null)
            CardManager_2.Instance.SetupCardBuffer();

        if (FieldUIManager.Instance != null)
        {
            FieldUIManager.Instance.UpdateFieldUI(remaningHands, remaningDrops);
            FieldUIManager.Instance.UpdateScoreUI(targetScore, curRoundScore, 1, JokBoType.None, false);
            FieldUIManager.Instance.RefreshBossDebuffIndicator();
        }
    }

}
