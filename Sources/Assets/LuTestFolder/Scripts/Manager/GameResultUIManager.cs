using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

[System.Serializable]
public class MonsterPortraitData
{
    public string enemyID;       // 일반 몬스터용 ID (예: "Dokkaebi")
    public BossType bossType;    // 보스용 타입 (예: BossType.Fox)
    public string displayName;   // 출력될 한글 이름 (예: "도깨비", "구미호")
    public GameObject GravePrefab;// 출력될 2D 이미지

    public string GetDisplayName()
    {
        if (displayName == "도깨비")
            return GameText.Get("game.GameResultUIManager.ShowResultOpponent.2d14016e35", displayName);
        var stage = new StageInfo { bossType = bossType, enemyID = enemyID };
        foreach (string alias in StageInfo.GetBossEnemyIdAliases(stage.GetEffectiveBossType()))
            if (displayName == alias) return stage.GetBossDisplayName();
        return displayName;
    }
}

public class GameResultUIManager : MonoBehaviour
{
    private static GameResultUIManager _instance;

    // 누군가 Instance를 불렀을 때, 비어있다면 꺼져있는 오브젝트까지 싹 뒤져서 찾기
    public static GameResultUIManager Instance
    {
        get
        {
            if (_instance == null || !_instance.HasValidSceneReferences())
            {
                // FindObjectsInactive.Include (또는 true)를 넣으면 인스펙터에서 꺼둔(Inactive) 녀석도 찾아냅니다.
                _instance = FindBestInstance();
                
                if (_instance == null)
                {
                    Debug.LogError("GameResultUIManager가 씬에 없습니다!");
                }
            }
            return _instance;
        }
    }

    public static GameResultUIManager Current
    {
        get
        {
            if (_instance == null || !_instance.HasValidSceneReferences())
            {
                _instance = FindBestInstance();
            }

            return _instance;
        }
    }

    [Header("결과창 타이틀")]
    public GameObject GameEndPanel;
    public TextMeshProUGUI resultTitleText;

    [Header("UI 텍스트 연결")]
    public TextMeshProUGUI bestHandScoreText;   // 최고득점 제출
    public TextMeshProUGUI mostPlayedHandText;  // 가장 많이 플레이한 핸드 (족보 이름 & 횟수)
    public TextMeshProUGUI playedCardsText;     // 제출한 패
    public TextMeshProUGUI discardedCardsText;  // 버린 패
    public TextMeshProUGUI moneySpentText;      // 사용한 돈
    public TextMeshProUGUI shopPurchasesText;   // 총 구매 수
    public TextMeshProUGUI rerollsText;         // 새로고침 횟수
    //public TextMeshProUGUI newDiscoveriesText;  // 새로운 발견
    
    public TextMeshProUGUI stageText;           // 스테이지
    public TextMeshProUGUI roundText;           // 라운드
    
    [Header("패배 전용 그룹")]
    public GameObject gameOverGroup;            // 패배 대상 패널 + 가로 배치된 버튼 2개 묶음
    public TextMeshProUGUI deathReasonText;     // 패배한 대상 텍스트
    public Image deathMonsterImage;

    [Header("★ 3D 비석 연출 설정")]
    public Transform GraveAnchor;           // 3D 비석이 생성될 기준 위치 (빈 오브젝트)
    public float GraveScale = 100f;         // UI 위에서 3D 모델의 크기 (Canvas에선 크게 키워야 보임)
    public Vector3 GraveRotation = new Vector3(0, 180f, 0); // 비석이 앞을 보게 돌리는 회전값
    public float GraveZOffset = 0.25f;

    [Header("★ 몬스터 초상화 데이터 설정")]
    [Tooltip("여기에 보스와 도깨비의 이름, 이미지를 등록하세요.")]
    public List<MonsterPortraitData> monsterPortraits = new List<MonsterPortraitData>();
    public GameObject defaultGravePrefab; // 등록되지 않은 적일 때 띄울 기본 땜빵용 이미지

    private GameObject currentSpawnedGrave;

    [Header("클리어(승리) 전용 그룹")]
    public GameObject clearGroup;               // 세로 배치된 버튼 2개 + 무한 모드 버튼 묶음

    [Header("결과창 이동")]
    [SerializeField] private string newRunSceneName = "ChoiceJoker";
    [SerializeField] private string mainMenuSceneName = "TitleScene_Rebuilt";
    [SerializeField] private Button newRunButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private GameObject endlessModePromptGroup;
    [SerializeField] private Button endlessModeButton;
    [SerializeField] private GameObject journeyContinueGroup;

    private GameResultJourneyMapRenderer journeyMapRenderer;
    private GameResultHanjiPresentation hanjiPresentation;
    private bool isLeavingResult;
    private bool isResultInputBlocked;
    private RunStatTracker displayedStats;
    private StageInfo displayedOpponentStage;
    private bool displayedIsClear;

    private void Awake()
    {
        // RunStatTracker lives across scenes, so prefer the manager with live scene UI references.
        if (_instance == null || !_instance.HasValidSceneReferences())
        {
            _instance = this;
            ConfigureResultButtons();
        }
        else if (_instance != this)
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        GameText.Changed -= RefreshResultLanguage;
        SetResultInputBlocked(false);
        hanjiPresentation?.Dispose();

        if (_instance == this)
            _instance = null;
    }

    private void OnEnable()
    {
        ConfigureResultButtons();
        GameText.Changed -= RefreshResultLanguage;
        GameText.Changed += RefreshResultLanguage;
        RefreshResultLanguage();
    }

    private void Update()
    {
        if (GameEndPanel != null && GameEndPanel.activeInHierarchy)
            hanjiPresentation?.Tick(Time.unscaledDeltaTime);
        else
            hanjiPresentation?.CancelButtonClick();
    }

    private void OnDisable()
    {
        GameText.Changed -= RefreshResultLanguage;
        hanjiPresentation?.CancelButtonClick();
    }

    // 결과창이 켜질 때 호출해 주시면 됩니다 (예: OnEnable 에서 호출하거나 매니저가 직접 호출)
    public void ShowResult(bool isClear)
    {
        if (!EnsureSceneReferences())
        {
            Debug.LogError("[GameResultUIManager] 현재 씬의 GameEndPanel을 찾지 못해 결과창을 표시할 수 없습니다.");
            return;
        }

        if (SaveManager.instance != null && SaveManager.instance.userData != null && JokboPlayTracker.Instance != null)
        {
            // 누적 최다 족보는 제출/복원 시 집계합니다. 결과창에서는 기존 옵션 저장만 유지합니다.
            if (OptionWindowManager.Instance != null)
            {
                OptionWindowManager.Instance.SaveOptions(); 
            }
            else
            {
                if (SaveManager.instance != null && SaveManager.instance.userData != null)
                {
                    SaveManager.instance.SaveUserData();
                }
            }
        }
        
        if (UnlockManager.instance != null)
            UnlockManager.instance.CheckUnlockConditions();

        ConfigureResultButtons();
        ApplyResultStaticTextReplacements();
        ClearEncounterPresentationForResult();
        MoveCameraToEncounterView();

        GameEndPanel.SetActive(true);
        gameObject.SetActive(true);
        SetResultInputBlocked(true);

        RunStatTracker stats = RunStatTracker.Instance;
        if (stats == null) return;
        displayedStats = stats;
        displayedIsClear = isClear;
        displayedOpponentStage = MapManager.instance != null ? MapManager.instance.GetCurrentStageInfo() : null;

        if (hanjiPresentation == null)
            hanjiPresentation = GameResultHanjiPresentation.TryCreate(this);
        hanjiPresentation?.Show(isClear);
        RenderJourneyMap(stats, isClear);

        RefreshResultLanguage();

        // 3. 누적 스탯들 표시
        playedCardsText.text = stats.totalPlayedCards.ToString();
        discardedCardsText.text = stats.totalDiscardedCards.ToString();
        moneySpentText.text = stats.totalMoneySpent.ToString();
        shopPurchasesText.text = stats.totalShopPurchases.ToString();
        rerollsText.text = stats.totalRerolls.ToString();
        //newDiscoveriesText.text = stats.newDiscoveries.ToString();

        // 4. 현재 진행도 표시
        if (CombatManager.instance != null)
        {
            stageText.text = (CombatManager.instance.curStageIndex + 1).ToString();
            roundText.text = (CombatManager.instance.curRoundIndex + 1).ToString();
        }

        // 5. 클리어 vs 패배 분기
        if (isClear)
        {
            if (gameOverGroup != null) gameOverGroup.SetActive(false);
            if (clearGroup != null) clearGroup.SetActive(true);
            ShowEndlessModePromptFirst();
            HideBattleResultContinuationPanel();
        }
        else
        {
            if (gameOverGroup != null) gameOverGroup.SetActive(true);
            if (clearGroup != null) clearGroup.SetActive(false);
            SetClearResultSubGroups(false, false);
        }
        ShowResultOpponent(stats, isClear);
    }

    private static void ClearEncounterPresentationForResult()
    {
        EncounterPresentationManager encounter = EncounterPresentationManager.Instance;
        if (encounter != null)
        {
            // Cancel delayed actor/camera work before hiding models. Defeat cleanup can
            // restore its target, so it must run before the final immediate actor hide.
            encounter.StopCurrentPresentation();
            encounter.ResetDefeatPresentation();
            encounter.HidePreviewActors();
        }
        // The late-rendered scenery is owned separately from the encounter actors.
        StageLightingDirector.Instance?.Atmosphere?.HideImmediate();
    }

    private void RefreshResultLanguage()
    {
        if (displayedStats == null || GameEndPanel == null || !GameEndPanel.activeInHierarchy)
            return;

        if (bestHandScoreText != null)
            GameText.Bind(bestHandScoreText, "game.GameResultUIManager.ShowResult.f0dff5f49a", "{value0}점",
                new { value0 = displayedStats.bestHandScore });
        if (mostPlayedHandText != null && JokboPlayTracker.Instance != null)
        {
            JokBoType mostPlayed = JokboPlayTracker.Instance.GetMostPlayedJokboTotal();
            int count = JokboPlayTracker.Instance.TotalPlayCount.TryGetValue(mostPlayed, out int total) ? total : 0;
            GameText.Bind(mostPlayedHandText, "game.GameResultUIManager.ShowResult.ffd4bdc47f", "{value0} ({value1}회)",
                new { value0 = ResolveJokboDisplayName(mostPlayed), value1 = count });
        }
        if (resultTitleText != null)
            GameText.Bind(resultTitleText, displayedIsClear
                ? "game.GameResultUIManager.ShowResult.a951ee69c9" : "game.GameResultUIManager.ShowResult.b06527e3fa",
                displayedIsClear ? "승리" : "패배");
        if (deathReasonText != null)
            deathReasonText.text = ResolveResultOpponent(displayedStats, displayedIsClear, displayedOpponentStage, out _);
        hanjiPresentation?.RefreshLanguage(displayedIsClear);
    }

    private void ShowResultOpponent(RunStatTracker stats, bool isClear)
    {
        if (currentSpawnedGrave != null)
        {
            currentSpawnedGrave.SetActive(false);
            Destroy(currentSpawnedGrave);
            currentSpawnedGrave = null;
        }

        string targetName = ResolveResultOpponent(stats, isClear, displayedOpponentStage, out GameObject targetPrefab);
        if (deathReasonText != null) deathReasonText.text = targetName;
        if (targetPrefab != null && GraveAnchor != null)
        {
            currentSpawnedGrave = Instantiate(targetPrefab, GraveAnchor);
            currentSpawnedGrave.transform.localPosition = new Vector3(0, 0, GraveZOffset);
            currentSpawnedGrave.transform.localRotation = Quaternion.Euler(GraveRotation);
            currentSpawnedGrave.transform.localScale = Vector3.one * GraveScale;
            SetLayerRecursively(currentSpawnedGrave, GraveAnchor.gameObject.layer);
            hanjiPresentation?.FitOpponentGrave(currentSpawnedGrave);
        }
    }

    private string ResolveResultOpponent(RunStatTracker stats, bool isClear, StageInfo currentStage, out GameObject targetPrefab)
    {
        targetPrefab = null;
        if (!isClear && stats.deathReason == "포기")
            return GameText.Get("game.GameResultUIManager.ShowResultOpponent.08cfbabceb", "포기함");

        string targetName = GameText.Get("game.GameResultUIManager.ShowResultOpponent.d58a7c9a12", "알 수 없는 적");
        targetPrefab = defaultGravePrefab;
        if (currentStage != null)
        {
            if (currentStage.type == StageType.Boss)
            {
                BossType currentBossType = currentStage.GetEffectiveBossType();
                targetName = currentStage.GetBossDisplayName();
                MonsterPortraitData data = monsterPortraits.Find(x => x.bossType == currentBossType);
                if (data != null)
                {
                    if (!string.IsNullOrEmpty(data.displayName)) targetName = data.GetDisplayName();
                    if (data.GravePrefab != null) targetPrefab = data.GravePrefab;
                }
            }
            else if (currentStage.type == StageType.Battle)
            {
                targetName = GameText.Get("game.GameResultUIManager.ShowResultOpponent.2d14016e35", "도깨비");
                MonsterPortraitData data = monsterPortraits.Find(x => x.enemyID == currentStage.enemyID);
                if (data != null)
                {
                    if (!string.IsNullOrEmpty(data.displayName)) targetName = data.GetDisplayName();
                    if (data.GravePrefab != null) targetPrefab = data.GravePrefab;
                }
            }
        }

        return targetName;
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    public void OnNewRunButtonClicked()
    {
        if (isLeavingResult)
            return;

        if (hanjiPresentation?.TryPlayButtonClick("NewRun", () => BeginRunEndTransition(newRunSceneName, true)) == true)
            return;
        BeginRunEndTransition(newRunSceneName, true);
    }

    public void OnMainMenuButtonClicked()
    {
        if (isLeavingResult)
            return;

        if (hanjiPresentation?.TryPlayButtonClick("MainMenu", () =>
            BeginRunEndTransition(mainMenuSceneName == "TitleScene" ? "TitleScene_Rebuilt" : mainMenuSceneName, true)) == true)
            return;
        // 이전 씬/프리팹에 저장된 타이틀 이름도 현재 빌드의 타이틀로 연결합니다.
        BeginRunEndTransition(mainMenuSceneName == "TitleScene" ? "TitleScene_Rebuilt" : mainMenuSceneName, true);
    }

    public void OnEndlessModeButtonClicked()
    {
        if (isLeavingResult)
            return;

        if (hanjiPresentation?.TryPlayButtonClick("Infinite", ContinueJourneyFromEndlessModePrompt) == true)
            return;
        ContinueJourneyFromEndlessModePrompt();
    }

    public void DebugPlayPendingArtifactUnlockPresentation()
    {
        if (isLeavingResult)
            return;

        isLeavingResult = true;
        SetResultButtonsInteractable(false);

        RunEndTransitionPresenter.GetOrCreate().BeginDebugUnlockPresentation(() =>
        {
            isLeavingResult = false;
            SetResultButtonsInteractable(true);
        });
    }

    private void BeginRunEndTransition(string sceneName, bool resetRunBeforeLoad)
    {
        if (!EnsureSceneReferences())
            return;

        SetResultInputBlocked(false);
        isLeavingResult = true;
        SetResultButtonsInteractable(false);

        bool started = RunEndTransitionPresenter
            .GetOrCreate()
            .BeginSceneLeave(sceneName, resetRunBeforeLoad, GameEndPanel);

        if (started)
            return;

        isLeavingResult = false;
        hanjiPresentation?.CancelButtonClick();
        SetResultInputBlocked(true);
        SetResultButtonsInteractable(true);
    }

    private void ConfigureResultButtons()
    {
        if (!EnsureGameEndPanelReference())
            return;

        if (newRunButton == null)
            newRunButton = FindButtonRecursive(GameEndPanel.transform, "NewRun", "NewRunButton");

        if (mainMenuButton == null)
            mainMenuButton = FindButtonRecursive(GameEndPanel.transform, "MainMenu", "MainMenuButton");

        if (newRunButton != null)
        {
            newRunButton.onClick.RemoveListener(OnNewRunButtonClicked);
            newRunButton.onClick.AddListener(OnNewRunButtonClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuButtonClicked);
            mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);
        }

        if (newRunButton != null)
        {
            TextMeshProUGUI label = newRunButton.GetComponentInChildren<TextMeshProUGUI>(true);
            GameText.Bind(label, "game.GameResultUIManager.ConfigureResultButtons.5c662f5824", "다시하기");
        }

        ResolveClearResultFlowReferences();
        if (endlessModeButton != null)
        {
            endlessModeButton.onClick.RemoveListener(OnEndlessModeButtonClicked);
            endlessModeButton.onClick.AddListener(OnEndlessModeButtonClicked);
        }
    }

    private void SetResultButtonsInteractable(bool interactable)
    {
        if (newRunButton != null)
            newRunButton.interactable = interactable;

        if (mainMenuButton != null)
            mainMenuButton.interactable = interactable;
    }

    private void ApplyResultStaticTextReplacements()
    {
        if (GameEndPanel == null)
            return;

        TextMeshProUGUI[] texts = GameEndPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null || string.IsNullOrWhiteSpace(text.text))
                continue;

            text.text = ReplaceResultLabelText(text.text);
        }
    }

    private static string ReplaceResultLabelText(string text)
    {
        return text
            .Replace("게임오버", "패배")
            .Replace("게임 오버", "패배")
            .Replace("최고득점 핸드", "최고득점 제출")
            .Replace("최고 득점 핸드", "최고득점 제출")
            .Replace("최다플레이 핸드", "최다제출 족보")
            .Replace("최다 플레이 핸드", "최다제출 족보")
            .Replace("가장 많이 플레이한 핸드", "최다제출 족보")
            .Replace("플레이한 카드", "제출한 패")
            .Replace("버린 카드", "버린 패")
            .Replace("새로운 런", "다시하기");
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buttonText != null)
            buttonText.text = label;
    }

    private static string ResolveJokboDisplayName(JokBoType jokbo)
    {
        if (jokbo == JokBoType.None)
            return "-";

        if (ScoreManager.Instance != null)
            return ScoreManager.Instance.GetJokboDisplayName(jokbo);

        switch (jokbo)
        {
            case JokBoType.ThreeGwang138: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.37abc76a6d", "1/3/8 광땡");
            case JokBoType.ThreeGwangAny: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.18f267e6b3", "광 3개");
            case JokBoType.SamDong: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.93566c4ea5", "삼동");
            case JokBoType.Yang: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.2ce295bd77", "양");
            case JokBoType.Eum: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.95e3c75cd5", "음");
            case JokBoType.TwoGwang: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.0a3665b9db", "광 2개");
            case JokBoType.BbingPae: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.95d99b0b20", "삥패");
            case JokBoType.SaPae: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.e625ff58b2", "사패");
            case JokBoType.JangDdaeng: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.04fb7c8239", "장땡");
            case JokBoType.Ddaeng: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.630a3abf09", "땡");
            case JokBoType.Mangtong: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.29afba3d00", "망통");
            case JokBoType.Ggeut: return GameText.Get("game.GameResultUIManager.ResolveJokboDisplayName.d4d24f4d1f", "끗");
            default: return jokbo.ToString();
        }
    }

    private bool EnsureSceneReferences()
    {
        if (!EnsureGameEndPanelReference())
            return false;

        if (resultTitleText == null)
            resultTitleText = FindTextRecursive(GameEndPanel.transform, "MainText", "ResultMainText");
        if (bestHandScoreText == null)
            bestHandScoreText = FindTextRecursive(GameEndPanel.transform, "BestScoreJokbo");
        if (mostPlayedHandText == null)
            mostPlayedHandText = FindTextRecursive(GameEndPanel.transform, "MostPlayedJokboNum");
        if (playedCardsText == null)
            playedCardsText = FindTextRecursive(GameEndPanel.transform, "PlayCardNum");
        if (discardedCardsText == null)
            discardedCardsText = FindTextRecursive(GameEndPanel.transform, "DiscardCardNum");
        if (moneySpentText == null)
            moneySpentText = FindTextRecursive(GameEndPanel.transform, "SpentMoneyText");
        if (shopPurchasesText == null)
            shopPurchasesText = FindTextRecursive(GameEndPanel.transform, "TotalPurchaseText");
        if (rerollsText == null)
            rerollsText = FindTextRecursive(GameEndPanel.transform, "TotalRerollText");
        // if (newDiscoveriesText == null)
        //     newDiscoveriesText = FindTextRecursive(GameEndPanel.transform, "FoundNewItemText");
        if (stageText == null)
            stageText = FindTextRecursive(GameEndPanel.transform, "StageText");
        if (roundText == null)
            roundText = FindTextRecursive(GameEndPanel.transform, "RoundText");

        if (gameOverGroup == null)
            gameOverGroup = FindChildGameObjectRecursive(GameEndPanel.transform, "GameOverGroup");
        if (clearGroup == null)
            clearGroup = FindChildGameObjectRecursive(GameEndPanel.transform, "ClearGroup");
        ResolveClearResultFlowReferences();
        if (deathReasonText == null && gameOverGroup != null)
            deathReasonText = FindTextRecursive(gameOverGroup.transform, "MonsterName");

        if (GraveAnchor == null && gameOverGroup != null)
        {
            Transform anchor = FindChildRecursive(gameOverGroup.transform, "TombstoneAnchor");
            if (anchor != null) GraveAnchor = anchor;
        }

        ConfigureResultButtons();
        return true;
    }

    private void ShowEndlessModePromptFirst()
    {
        ResolveClearResultFlowReferences();

        if (endlessModePromptGroup == null)
        {
            SetClearResultSubGroups(false, false);
            return;
        }

        SetClearResultSubGroups(true, true);
    }

    private void SetClearResultSubGroups(bool showEndlessPrompt, bool showJourneyContinue)
    {
        ResolveClearResultFlowReferences();

        if (endlessModePromptGroup != null)
            endlessModePromptGroup.SetActive(showEndlessPrompt);

        if (journeyContinueGroup != null)
            journeyContinueGroup.SetActive(showJourneyContinue);
    }

    private void ResolveClearResultFlowReferences()
    {
        if (GameEndPanel == null)
            return;

        if (clearGroup == null)
            clearGroup = FindChildGameObjectRecursive(GameEndPanel.transform, "ClearGroup");

        Transform clearRoot = clearGroup != null ? clearGroup.transform : GameEndPanel.transform;

        if (endlessModePromptGroup == null)
            endlessModePromptGroup = FindChildGameObjectRecursive(clearRoot, "Infinite");

        if (journeyContinueGroup == null)
            journeyContinueGroup = FindChildGameObjectRecursive(clearRoot, "ClearButton");

        if (endlessModeButton == null && endlessModePromptGroup != null)
            endlessModeButton = endlessModePromptGroup.GetComponent<Button>() ?? endlessModePromptGroup.GetComponentInChildren<Button>(true);
    }

    private void HideBattleResultContinuationPanel()
    {
        if (BattleResultUI.Instance != null)
            BattleResultUI.Instance.gameObject.SetActive(false);

        if (FieldUIManager.Instance != null)
            FieldUIManager.Instance.SetResultCanvasVisible(false);
    }

    private void ContinueJourneyFromEndlessModePrompt()
    {
        SaveManager.instance?.MarkEndlessModeEntered();
        SetClearResultSubGroups(false, false);

        if (GameEndPanel != null)
            GameEndPanel.SetActive(false);

        SetResultInputBlocked(false);

        if (CombatManager.instance != null)
            CombatManager.instance.ReturnToMap();
    }

    private void SetResultInputBlocked(bool blocked)
    {
        if (isResultInputBlocked == blocked)
            return;

        isResultInputBlocked = blocked;
        if (blocked)
            HeadLookInputController.PushExternalInputBlock();
        else
            HeadLookInputController.PopExternalInputBlock();
    }

    private bool EnsureGameEndPanelReference()
    {
        if (GameEndPanel != null)
            return true;

        GameEndPanel = FindSceneGameObject("GameEndPanel");
        if (GameEndPanel == null)
            return false;

        journeyMapRenderer = null;
        hanjiPresentation?.Dispose();
        hanjiPresentation = null;
        newRunButton = null;
        mainMenuButton = null;
        return true;
    }

    private bool HasValidSceneReferences()
    {
        return GameEndPanel != null && GameEndPanel.scene.IsValid() && GameEndPanel.scene.isLoaded;
    }

    private static GameResultUIManager FindBestInstance()
    {
        GameResultUIManager[] managers = Resources.FindObjectsOfTypeAll<GameResultUIManager>();
        GameResultUIManager fallback = null;

        for (int i = 0; i < managers.Length; i++)
        {
            GameResultUIManager manager = managers[i];
            if (manager == null)
                continue;

            if (fallback == null)
                fallback = manager;

            if (manager.HasValidSceneReferences())
                return manager;
        }

        return fallback;
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate == null || candidate.name != objectName)
                continue;

            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
                continue;

            if (candidate.hideFlags != HideFlags.None)
                continue;

            return candidate;
        }

        return null;
    }

    private static TextMeshProUGUI FindTextRecursive(Transform parent, params string[] childNames)
    {
        Transform child = FindChildRecursive(parent, childNames);
        if (child == null)
            return null;

        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        return text != null ? text : child.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private static GameObject FindChildGameObjectRecursive(Transform parent, params string[] childNames)
    {
        Transform child = FindChildRecursive(parent, childNames);
        return child != null ? child.gameObject : null;
    }

    private static Button FindButtonRecursive(Transform parent, params string[] childNames)
    {
        if (childNames == null)
            return null;

        for (int i = 0; i < childNames.Length; i++)
        {
            Transform child = FindChildRecursive(parent, childNames[i]);
            Button button = child != null ? child.GetComponent<Button>() : null;
            if (button != null)
                return button;
        }

        return null;
    }

    private void RenderJourneyMap(RunStatTracker stats, bool isClear)
    {
        GameResultJourneyMapRenderer renderer = ResolveJourneyMapRenderer();
        if (renderer != null)
            renderer.Render(stats, isClear);
    }

    private static void MoveCameraToEncounterView()
    {
        if (TableTransitionManager.instance != null)
            TableTransitionManager.instance.TryMoveToPreviewLookView();
    }

    private GameResultJourneyMapRenderer ResolveJourneyMapRenderer()
    {
        if (journeyMapRenderer != null)
            return journeyMapRenderer;

        Transform goneMap = GameEndPanel != null ? FindChildRecursive(GameEndPanel.transform, "GoneMap") : null;
        if (goneMap == null)
            return null;

        journeyMapRenderer = goneMap.GetComponent<GameResultJourneyMapRenderer>();
        if (journeyMapRenderer == null)
            journeyMapRenderer = goneMap.gameObject.AddComponent<GameResultJourneyMapRenderer>();

        journeyMapRenderer.SetPreferredFontAsset(ResolveJourneyMapFontAsset(goneMap));
        return journeyMapRenderer;
    }

    private TMP_FontAsset ResolveJourneyMapFontAsset(Transform goneMap)
    {
        if (GameEndPanel == null)
            return null;

        TextMeshProUGUI[] textComponents = GameEndPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        TMP_FontAsset fallbackFont = null;

        for (int i = 0; i < textComponents.Length; i++)
        {
            TextMeshProUGUI text = textComponents[i];
            if (text == null || text.font == null)
                continue;

            if (goneMap != null && text.transform.IsChildOf(goneMap))
                continue;

            if (fallbackFont == null)
                fallbackFont = text.font;

            if (text.font.name.Contains("EBS_jusigyeong_M"))
                return text.font;
        }

        return fallbackFont;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, params string[] childNames)
    {
        if (parent == null || childNames == null)
            return null;

        for (int i = 0; i < childNames.Length; i++)
        {
            Transform child = FindChildRecursive(parent, childNames[i]);
            if (child != null)
                return child;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            string trimmedName = child.name.Trim();
            for (int j = 0; j < childNames.Length; j++)
            {
                if (trimmedName == childNames[j])
                    return child;
            }

            Transform nested = FindChildRecursive(child, childNames);
            if (nested != null)
                return nested;
        }

        return null;
    }
}

public class GameResultJourneyMapRenderer : MonoBehaviour
{
    private const string RuntimeRootName = "JourneyMapRuntime";
    private const string ScrollViewName = "JourneyScrollView";
    private const string ViewportName = "Viewport";
    private const string ContentName = "Content";
    private const float NodeSize = 68f;
    private const float VerticalSpacing = 142f;
    private const float BottomPadding = 97f;
    private const float TopPadding = 100f;
    private const float NextNodeAlpha = 0.35f;
    private const float NodeAppearDuration = 0.18f;
    private const float PathDrawDuration = 0.22f;
    private const float ScrollFollowDuration = 0.20f;
    private const float JourneyMinStepSpeed = 1f;
    private const float JourneyMaxStepSpeed = 2.6f;
    private const int LongJourneyBoostStartCount = 6;
    private const int LongJourneyBoostEndCount = 18;

    private static Sprite solidSprite;
    private static Sprite battleSprite;
    private static Sprite shopSprite;
    private static Sprite bossSprite;

    private RectTransform runtimeRoot;
    private RectTransform viewport;
    private RectTransform content;
    private ScrollRect scrollRect;
    private Coroutine playRoutine;
    private TMP_FontAsset preferredFontAsset;
    private readonly Dictionary<StageType, Sprite> inkSprites = new Dictionary<StageType, Sprite>();
    private readonly Dictionary<BossType, Sprite> bossInkSprites = new Dictionary<BossType, Sprite>();
    private Texture2D paintedNodeTexture;
    private Material nodeInkMaterial;
    private readonly Dictionary<string, Sprite> scenerySprites = new Dictionary<string, Sprite>();
    private RectTransform sceneryRoot;
    private List<JourneyMapStepVisual> currentVisuals;
    private Sprite inkRule;
    private Material inkMaterial;
    private Material landscapeMaterial;
    private Sprite crossBrush;
    private Sprite guideBrush;
    private Sprite journeyHeadingRule;
    private Image guide;

    public void SetPreferredFontAsset(TMP_FontAsset fontAsset)
    {
        if (fontAsset != null)
            preferredFontAsset = fontAsset;
    }

    public void Render(RunStatTracker stats, bool isClear)
    {
        RectTransform container = transform as RectTransform;
        if (container == null)
            return;

        EnsureSprites();
        LoadInkResources();
        EnsureRuntimeRoot(container);
        ClearRuntimeRoot();

        List<RunJourneyNode> nodes = BuildDisplayNodes(stats, out int nextNodeIndex);
        if (nodes.Count == 0)
            return;

        Vector2 size = container.rect.size;
        float width = Mathf.Max(1f, size.x);
        float height = Mathf.Max(1f, size.y);

        EnsureScrollView(width, height);

        float viewportWidth = Mathf.Max(1f, width - 28f);
        float viewportHeight = Mathf.Max(1f, height - 32f);
        float contentHeight = Mathf.Max(viewportHeight, BottomPadding + TopPadding + Mathf.Max(0, nodes.Count - 1) * VerticalSpacing);
        content.sizeDelta = new Vector2(0f, contentHeight);
        content.anchoredPosition = Vector2.zero;
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;

        List<Vector2> positions = BuildNodePositions(nodes, viewportWidth, contentHeight);
        DrawJourneyScenery(positions, viewportWidth, contentHeight);
        List<JourneyMapStepVisual> visuals = BuildVisuals(nodes, positions, nextNodeIndex, contentHeight);
        currentVisuals = visuals;
        CreateGuide();
        // Heading stays above the scrolling ink and remains readable when a ridge passes behind it.
        DrawTitle(width);

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayJourneyRoutine(visuals, nextNodeIndex, viewportHeight, contentHeight));
    }

    private List<RunJourneyNode> BuildDisplayNodes(RunStatTracker stats, out int nextNodeIndex)
    {
        nextNodeIndex = -1;
        List<RunJourneyNode> nodes = new List<RunJourneyNode>();

        if (stats != null && stats.journeyNodes != null)
        {
            for (int i = 0; i < stats.journeyNodes.Count; i++)
            {
                if (stats.journeyNodes[i] != null)
                    nodes.Add(stats.journeyNodes[i]);
            }
        }

        if (nodes.Count == 0 && TryCreateCurrentNode(out RunJourneyNode currentNode))
        {
            currentNode.isGameOverNode = true;
            nodes.Add(currentNode);
        }

        if (TryCreateNextNode(nodes, out RunJourneyNode nextNode))
        {
            nextNodeIndex = nodes.Count;
            nodes.Add(nextNode);
        }

        return nodes;
    }

    private bool TryCreateCurrentNode(out RunJourneyNode node)
    {
        node = null;
        if (MapManager.instance == null)
            return false;

        bool useGraphMap = MapBoardManager.instance != null && MapBoardManager.instance.IsUsingGraphMap;
        MapGraphNode graphNode = useGraphMap
            ? MapManager.instance.CurrentMapGraph?.FindNode(MapManager.instance.CurrentMapProgress?.currentNodeId)
            : null;
        StageInfo stageInfo = graphNode?.stage ?? MapManager.instance.GetCurrentStageInfo();
        if (stageInfo == null)
            return false;

        int stageIndex = graphNode != null
            ? MapManager.instance.CurrentMapGraph.ResolveStageIndex(graphNode)
            : MapManager.instance.currentStageIndex;
        Vector3 position = Vector3.zero;
        if (MapBoardManager.instance != null)
        {
            if (graphNode != null)
                MapBoardManager.instance.TryGetGraphNodePosition(graphNode.id, out position);
            else
                MapBoardManager.instance.TryGetNodePosition(stageIndex, out position);
        }

        node = new RunJourneyNode(MapManager.instance.currentWorldIndex, stageIndex, stageInfo, position);
        return true;
    }

    private bool TryCreateNextNode(List<RunJourneyNode> existingNodes, out RunJourneyNode nextNode)
    {
        nextNode = null;
        if (MapManager.instance == null)
            return false;
        if (MapBoardManager.instance != null && MapBoardManager.instance.IsUsingGraphMap)
            return false;

        WorldData worldData = MapManager.instance.GetCurrentWorldData();
        if (worldData == null || worldData.stages == null)
            return false;

        int nextStageIndex = MapManager.instance.currentStageIndex + 1;
        if (nextStageIndex < 0 || nextStageIndex >= worldData.stages.Count)
            return false;

        StageInfo nextStageInfo = worldData.stages[nextStageIndex];
        if (nextStageInfo == null)
            return false;

        Vector3 nextPosition;
        if (MapBoardManager.instance == null || !MapBoardManager.instance.TryGetNodePosition(nextStageIndex, out nextPosition))
        {
            Vector3 lastPosition = existingNodes != null && existingNodes.Count > 0
                ? existingNodes[existingNodes.Count - 1].mapPosition
                : Vector3.zero;
            nextPosition = lastPosition + Vector3.forward * 4f;
        }

        nextNode = new RunJourneyNode(MapManager.instance.currentWorldIndex, nextStageIndex, nextStageInfo, nextPosition);
        return true;
    }

    private static List<Vector2> BuildNodePositions(List<RunJourneyNode> nodes, float viewportWidth, float contentHeight)
    {
        List<Vector2> positions = new List<Vector2>(nodes.Count);
        float maxAbsX = 1f;

        for (int i = 0; i < nodes.Count; i++)
            maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(nodes[i].mapPosition.x));

        float spacing = nodes.Count > 1
            ? Mathf.Max(VerticalSpacing, (contentHeight - BottomPadding - TopPadding) / (nodes.Count - 1))
            : VerticalSpacing;
        for (int i = 0; i < nodes.Count; i++)
        {
            float normalizedX = Mathf.Clamp(nodes[i].mapPosition.x / maxAbsX, -1f, 1f);
            // Leave a forest margin at the left while keeping the recorded lateral route order.
            float x = (normalizedX * 0.22f + 0.14f) * viewportWidth;
            float yFromBottom = BottomPadding + spacing * i;
            if (nodes.Count > 1 && nodes.Count <= 4)
            {
                float progress = Mathf.Pow(i / (float)(nodes.Count - 1), 1.17f);
                yFromBottom = BottomPadding + progress * (contentHeight - BottomPadding - TopPadding);
            }
            float y = -(contentHeight - yFromBottom);
            positions.Add(new Vector2(x, y));
        }

        return positions;
    }

    private void DrawTitle(float width)
    {
        TextMeshProUGUI title = CreateText("Title", GameText.Get("game.GameResultJourneyMapRenderer.DrawTitle.be36248386", "여정 지도"), 36, new Color(0.12f, 0.11f, 0.10f, 0.95f));
        GameText.Bind(title, "game.GameResultJourneyMapRenderer.DrawTitle.be36248386", "여정 지도");
        title.alignment = TextAlignmentOptions.Left;
        title.characterSpacing = -5f;
        title.wordSpacing = -18f;
        RectTransform rect = title.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width - 220f, 48f);
        rect.anchoredPosition = new Vector2(53f, -9f);
        title.ForceMeshUpdate(true, true);
        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue;
        for (int i = 0; i < title.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = title.textInfo.characterInfo[i];
            if (!character.isVisible) continue;
            // Glyph metrics exclude the transparent SDF padding around the mesh quad.
            var metrics = character.textElement.glyph.metrics;
            float glyphLeft = character.origin + metrics.horizontalBearingX * character.scale;
            left = Mathf.Min(left, glyphLeft);
            right = Mathf.Max(right, glyphLeft + metrics.width * character.scale);
            bottom = Mathf.Min(bottom, character.baseLine + (metrics.horizontalBearingY - metrics.height) * character.scale);
        }
        bool hasGlyphs = left < right;
        if (journeyHeadingRule != null || inkRule != null)
        {
            GameObject stroke = new GameObject("JourneyTitleInkRule", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            stroke.transform.SetParent(runtimeRoot, false);
            stroke.layer = runtimeRoot.gameObject.layer;
            Image image = stroke.GetComponent<Image>();
            image.sprite = journeyHeadingRule != null ? journeyHeadingRule : inkRule;
            image.raycastTarget = false; image.color = new Color(1f, 1f, 1f, 0.90f);
            RectTransform underline = image.rectTransform;
            underline.anchorMin = underline.anchorMax = new Vector2(0.5f, 1f);
            float lineWidth = hasGlyphs ? Mathf.Min(width - 120f, right - left + 28f) : 168f;
            underline.sizeDelta = new Vector2(lineWidth, 8f);
            underline.anchoredPosition = hasGlyphs
                ? rect.anchoredPosition + new Vector2((left + right) * 0.5f, bottom - 9.5f)
                : new Vector2(-39f, -61f);
        }
        // Keep the heading above its decoration even when a replacement string has deeper glyphs.
        rect.SetAsLastSibling();
    }

    private void DrawJourneyScenery(List<Vector2> positions, float width, float height)
    {
        if (scenerySprites.Count == 0) return;
        GameObject root = new GameObject("JourneyScenery", typeof(RectTransform));
        root.transform.SetParent(content, false);
        root.layer = content.gameObject.layer;
        sceneryRoot = root.GetComponent<RectTransform>();
        sceneryRoot.anchorMin = sceneryRoot.anchorMax = sceneryRoot.pivot = new Vector2(0.5f, 1f);
        sceneryRoot.sizeDelta = new Vector2(width, height);
        sceneryRoot.anchoredPosition = Vector2.zero;
        // Scenery shares Content's coordinates and mask. No fixed scenery is attached to the paper.
        float spacing = positions.Count > 1 ? Mathf.Abs(positions[1].y - positions[0].y) : VerticalSpacing;
        RectTransform distant = CreateSceneryLayer("DistantMountains", width, height);
        RectTransform woods = CreateSceneryLayer("MiddleWoods", width, height);
        RectTransform ground = CreateSceneryLayer("ForegroundGround", width, height);
        // Complete each depth layer before the next. Later nodes cannot paint a distant ridge over trees.
        for (int i = 0; i < positions.Count; i++)
        {
            Vector2 point = positions[i];
            float side = i % 2 == 0 ? -1f : 1f;
            AddScenery(i % 2 == 0 ? "Mountains" : "Mist", new Vector2(side * width * 0.18f, point.y + spacing * 0.33f),
                new Vector2(width * 0.81f, Mathf.Min(290f, spacing * 1.55f)), 0.36f, i % 2 == 1, distant);
        }
        for (int i = 0; i < positions.Count; i++)
        {
            Vector2 point = positions[i];
            float side = i % 2 == 0 ? -1f : 1f;
            Vector2 size = new Vector2(width * 0.61f, Mathf.Clamp(spacing * 1.13f, 164f, 245f));
            Vector2 preferred = new Vector2(side * width * 0.23f, point.y + spacing * 0.29f);
            bool quietSummit = i == positions.Count - 1;
            AddScenery(quietSummit ? "Rock" : i % 3 == 0 ? "PinesLow" : i % 3 == 1 ? "PinesTall" : "Bush",
                FindSceneryPosition(preferred, size, positions, width, height), size,
                quietSummit ? 0.36f : 0.53f, i % 2 == 1, woods);
        }
        for (int i = 0; i < positions.Count; i++)
        {
            Vector2 point = positions[i];
            AddScenery(i % 2 == 0 ? "Grass" : "Stones", point + new Vector2(-12f, -37f),
                new Vector2(210f, 108f), 0.54f, i % 2 == 1, ground);
            if (i % 2 == 0)
                AddScenery("Rock", new Vector2(-width * 0.19f, point.y - spacing * 0.30f),
                    new Vector2(width * 0.67f, 138f), 0.44f, i % 4 == 0, ground);
        }
    }

    private RectTransform CreateSceneryLayer(string name, float width, float height)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform));
        layer.transform.SetParent(sceneryRoot, false);
        layer.layer = sceneryRoot.gameObject.layer;
        RectTransform rect = layer.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Vector2 FindSceneryPosition(Vector2 preferred, Vector2 size, List<Vector2> positions, float width, float height)
    {
        // Pick among a few nearby compositions without consuming gameplay RNG or obscuring a node.
        Vector2[] offsets = { Vector2.zero, new Vector2(0f, 48f), new Vector2(0f, -48f),
            new Vector2(-42f, 24f), new Vector2(42f, 24f) };
        Vector2 best = preferred;
        float bestClearance = float.MinValue;
        foreach (Vector2 offset in offsets)
        {
            Vector2 candidate = preferred + offset;
            candidate = ClampSceneryPosition(candidate, size, width, height);
            float clearance = float.MaxValue;
            foreach (Vector2 node in positions)
            {
                Vector2 gap = candidate - node;
                float x = gap.x / (size.x * 0.44f + NodeSize * 0.65f);
                float y = gap.y / (size.y * 0.42f + NodeSize * 0.65f);
                clearance = Mathf.Min(clearance, x * x + y * y);
            }
            if (clearance <= bestClearance) continue;
            bestClearance = clearance; best = candidate;
            if (clearance >= 1.5f) break;
        }
        return best;
    }

    private static Vector2 ClampSceneryPosition(Vector2 point, Vector2 size, float width, float height)
    {
        float marginX = size.x * 0.5f + 6f;
        float marginY = size.y * 0.5f + 6f;
        point.x = Mathf.Clamp(point.x, -width * 0.5f + marginX, width * 0.5f - marginX);
        point.y = Mathf.Clamp(point.y, -height + marginY, -marginY);
        return point;
    }

    private void AddScenery(string kind, Vector2 position, Vector2 size, float alpha, bool mirror, RectTransform layer)
    {
        if (!scenerySprites.TryGetValue("journey_scenery_" + kind, out Sprite sprite)) return;
        GameObject art = new GameObject("Scenery_" + kind, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        art.transform.SetParent(layer, false);
        art.layer = sceneryRoot.gameObject.layer;
        Image image = art.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        // Keep the neutral pigment's internal values; alpha-only icon ink would flatten the landscape.
        image.material = landscapeMaterial;
        image.color = new Color(1f, 1f, 1f, alpha);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = ClampSceneryPosition(position, size, sceneryRoot.rect.width, sceneryRoot.rect.height);
        if (mirror) rect.localScale = new Vector3(-1f, 1f, 1f);
    }

    private List<JourneyMapStepVisual> BuildVisuals(List<RunJourneyNode> nodes, List<Vector2> positions, int nextNodeIndex, float contentHeight)
    {
        List<JourneyMapStepVisual> visuals = new List<JourneyMapStepVisual>(nodes.Count);

        for (int i = 0; i < nodes.Count; i++)
        {
            bool isNextNode = i == nextNodeIndex;
            RectTransform line = i > 0 ? DrawLine(positions[i - 1], positions[i], i == nextNodeIndex) : null;
            RectTransform node = DrawNode(nodes[i], positions[i], isNextNode);
            CanvasGroup nodeGroup = EnsureCanvasGroup(node.gameObject);
            CanvasGroup lineGroup = line != null ? EnsureCanvasGroup(line.gameObject) : null;
            float lineLength = line != null ? line.sizeDelta.x : 0f;

            nodeGroup.alpha = 0f;
            node.localScale = Vector3.zero;

            if (line != null)
            {
                lineGroup.alpha = 0f;
                line.sizeDelta = new Vector2(0f, line.sizeDelta.y);
            }

            float distanceFromBottom = contentHeight + positions[i].y;
            visuals.Add(new JourneyMapStepVisual(node, nodeGroup, line, lineGroup, lineLength, distanceFromBottom));
        }

        return visuals;
    }

    private RectTransform DrawNode(RunJourneyNode node, Vector2 position, bool isNextNode)
    {
        GameObject nodeObject = new GameObject(isNextNode ? "NextNode" : "JourneyNode", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        nodeObject.transform.SetParent(content, false);
        nodeObject.layer = content.gameObject.layer;

        RectTransform rect = nodeObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * (isNextNode ? NodeSize * 0.9f : NodeSize);
        rect.anchoredPosition = position;
        if (node.stageType == StageType.Battle)
            rect.localRotation = Quaternion.Euler(0f, 0f, (node.worldIndex + node.stageIndex) % 2 == 0 ? -6f : 4f);

        Image image = nodeObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = node.stageType == StageType.Boss && bossInkSprites.TryGetValue(node.bossType, out Sprite bossInk)
            ? bossInk : ResolveNodeSprite(node.stageType);
        image.color = inkSprites.ContainsKey(node.stageType)
            ? new Color(1f, 1f, 1f, isNextNode ? NextNodeAlpha : 1f)
            : ResolveNodeColor(node.stageType, isNextNode);
        image.preserveAspect = true;
        if (inkSprites.ContainsKey(node.stageType)) image.material = inkMaterial;
        if (image.sprite != null && image.sprite.texture == paintedNodeTexture) image.material = nodeInkMaterial;

        if (isNextNode)
            DrawNodeLabel(GameText.Get("game.GameResultJourneyMapRenderer.DrawNode.aef613c661", "다음"), rect, new Color(0.38f, 0.35f, 0.32f, 0.75f));

        if (node.isGameOverNode)
            DrawGameOverMark(rect);

        return rect;
    }

    private void DrawNodeLabel(string text, RectTransform parent, Color color)
    {
        TextMeshProUGUI label = CreateText("NodeLabel", text, 14, color);
        GameText.Bind(label, "game.GameResultJourneyMapRenderer.DrawNode.aef613c661", "다음");
        RectTransform rect = label.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(60f, 20f);
        rect.anchoredPosition = new Vector2(0f, -NodeSize * 0.82f);
    }

    private void DrawGameOverMark(RectTransform nodeRect)
    {
        if (crossBrush != null)
        {
            GameObject mark = new GameObject("GameOverX", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            mark.transform.SetParent(nodeRect, false);
            mark.layer = nodeRect.gameObject.layer;
            Image image = mark.GetComponent<Image>();
            image.sprite = crossBrush; image.color = Color.white; image.raycastTarget = false;
            image.rectTransform.sizeDelta = Vector2.one * NodeSize * 1.06f;
            return;
        }
        DrawCrossLine(nodeRect, 45f);
        DrawCrossLine(nodeRect, -45f);
    }

    private void DrawCrossLine(RectTransform parent, float angle)
    {
        GameObject lineObject = new GameObject("GameOverX", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lineObject.transform.SetParent(parent, false);
        lineObject.layer = parent.gameObject.layer;

        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(NodeSize * 1.22f, 4f);
        rect.anchoredPosition = Vector2.zero;
        rect.localEulerAngles = new Vector3(0f, 0f, angle);

        Image image = lineObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = inkRule != null ? inkRule : solidSprite;
        image.material = inkRule != null ? inkMaterial : null;
        image.color = new Color(0.49f, 0.085f, 0.06f, 1f);
    }

    private RectTransform DrawLine(Vector2 from, Vector2 to, bool isNextLine)
    {
        // End the painted trail before the stamps so each arrival has a visible pause in the ink.
        Vector2 directionToNode = (to - from).normalized;
        float gap = Mathf.Min(NodeSize * 0.56f, Vector2.Distance(from, to) * 0.22f);
        from += directionToNode * gap;
        to -= directionToNode * gap;
        Vector2 delta = to - from;
        if (delta.sqrMagnitude <= 0.01f)
            return null;

        GameObject lineObject = new GameObject(isNextLine ? "NextPathLine" : "PathLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(GameResultInkPathGraphic));
        lineObject.transform.SetParent(content, false);
        lineObject.layer = content.gameObject.layer;
        lineObject.transform.SetSiblingIndex(sceneryRoot != null ? sceneryRoot.GetSiblingIndex() + 1 : 0);

        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(delta.magnitude, isNextLine ? 4f : 12f);
        rect.anchoredPosition = from;
        rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        GameResultInkPathGraphic stroke = lineObject.GetComponent<GameResultInkPathGraphic>();
        float direction = from.x > to.x ? -1f : 1f;
        stroke.Configure(delta.magnitude, rect.sizeDelta.y, Mathf.Min(62f, delta.magnitude * 0.39f) * direction, inkRule);
        stroke.material = inkMaterial;
        stroke.color = isNextLine
            ? new Color(1f, 1f, 1f, 0.28f)
            : new Color(1f, 1f, 1f, 0.78f);

        return rect;
    }

    private TextMeshProUGUI CreateText(string objectName, string text, int fontSize, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(runtimeRoot, false);
        textObject.layer = runtimeRoot.gameObject.layer;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = color;
        TMP_FontAsset readingFont = Resources.Load<TMP_FontAsset>("UI/Typography/EBS_jusigyeong_M SDF");
        TMP_FontAsset font = readingFont != null ? readingFont : preferredFontAsset;
        if (font != null) { label.font = font; label.fontSharedMaterial = font.material; }
        label.fontStyle = FontStyles.Normal;
        label.fontWeight = FontWeight.Regular;
        {GameText.Style(label);return label;}
    }

    private Sprite ResolveNodeSprite(StageType stageType)
    {
        if (inkSprites.TryGetValue(stageType, out Sprite ink)) return ink;
        switch (stageType)
        {
            case StageType.Shop:
                return shopSprite;
            case StageType.Boss:
                return bossSprite;
            case StageType.Battle:
            default:
                return battleSprite;
        }
    }

    private static Color ResolveNodeColor(StageType stageType, bool isNextNode)
    {
        Color color;
        switch (stageType)
        {
            case StageType.Shop:
                color = new Color(0.42f, 0.32f, 0.20f, 1f);
                break;
            case StageType.Boss:
                color = new Color(0.43f, 0.20f, 0.21f, 1f);
                break;
            case StageType.CardRemoval:
                color = new Color(0.35f, 0.28f, 0.38f, 1f);
                break;
            case StageType.CardUpgrade:
                color = new Color(0.43f, 0.27f, 0.19f, 1f);
                break;
            case StageType.ArtifactExchange:
                color = new Color(0.24f, 0.35f, 0.29f, 1f);
                break;
            case StageType.Observatory:
                color = new Color(0.24f, 0.34f, 0.43f, 1f);
                break;
            case StageType.Event:
                color = new Color(0.36f, 0.30f, 0.24f, 1f);
                break;
            case StageType.Battle:
            default:
                color = new Color(0.23f, 0.29f, 0.34f, 1f);
                break;
        }

        if (isNextNode)
            color.a = NextNodeAlpha;

        return color;
    }

    private void LoadInkResources()
    {
        if (scenerySprites.Count == 0)
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("UI/GameResult/journey_ink_scenery_v2"))
                scenerySprites[sprite.name] = sprite;
        if (scenerySprites.Count == 0)
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("UI/GameResult/journey_ink_scenery"))
                scenerySprites[sprite.name] = sprite;
        if (inkSprites.Count > 0) return;
        foreach (Sprite decoration in Resources.LoadAll<Sprite>("UI/GameResult/game_result_text_decorations"))
            if (decoration.name == "result_decoration_JourneyRule") journeyHeadingRule = decoration;
        foreach (Sprite brush in Resources.LoadAll<Sprite>("UI/GameResult/game_result_brush_atlas"))
        {
            if (brush.name == "result_brush_Rule") inkRule = brush;
            else if (brush.name == "result_brush_Cross") crossBrush = brush;
            else if (brush.name == "result_brush_Guide") guideBrush = brush;
        }
        if (inkRule == null) inkRule = Resources.Load<Sprite>("UI/SettingsBook/settings_book_ink_rule");
        inkMaterial = null;
        if (landscapeMaterial == null)
        {
            Material source = Resources.Load<Material>("UI/GameResult/GameResultLandscapeInk");
            if (source != null)
                landscapeMaterial = new Material(source) { name = "JourneyLandscapeInk", hideFlags = HideFlags.DontSave };
        }
        Sprite[] sprites = Resources.LoadAll<Sprite>("UI/GameResult/journey_ink_icons_v2");
        if (sprites.Length == 0) sprites = Resources.LoadAll<Sprite>("UI/GameResult/journey_ink_icons");
        foreach (StageType type in System.Enum.GetValues(typeof(StageType)))
        {
            string spriteName = "journey_ink_" + type;
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i].name == spriteName) { inkSprites[type] = sprites[i]; break; }
        }
        // Keep unmatched events/unknown bosses and missing resources on the existing ink fallback.
        Sprite[] paintedSprites = Resources.LoadAll<Sprite>("UI/GameResult/journey_painted_node_icons");
        foreach (Sprite sprite in paintedSprites)
        {
            foreach (StageType type in System.Enum.GetValues(typeof(StageType)))
                if (sprite.name == "journey_painted_" + type) inkSprites[type] = sprite;
            foreach (BossType type in System.Enum.GetValues(typeof(BossType)))
                if (type != BossType.None && sprite.name == "journey_painted_Boss_" + type) bossInkSprites[type] = sprite;
        }
        if (paintedSprites.Length > 0)
        {
            paintedNodeTexture = paintedSprites[0].texture;
            Material source = Resources.Load<Material>("UI/SettingsBook/SettingsBookInk");
            if (source != null)
            {
                // Own the correction material; paths, X marks and shared UI materials retain their original ink.
                nodeInkMaterial = new Material(source) { name = "JourneyPaintedNodeInk", hideFlags = HideFlags.DontSave };
                nodeInkMaterial.SetFloat("_MaskOnly", 0f);
                nodeInkMaterial.SetFloat("_Exposure", 0.94f);
                nodeInkMaterial.SetColor("_CoolLight", new Color(1f, 0.98f, 0.95f, 1f));
                nodeInkMaterial.SetColor("_WarmLight", new Color(1f, 0.98f, 0.95f, 1f));
                nodeInkMaterial.SetFloat("_TextureContrast", 0.94f);
                nodeInkMaterial.SetFloat("_SurfaceSaturation", 0.55f);
                nodeInkMaterial.SetFloat("_DetailSoftness", 0.2f);
                nodeInkMaterial.SetFloat("_DetailMipBias", 0.35f);
            }
        }
    }

    private Sprite ownedGuideSprite;

    private void CreateGuide()
    {
        Sprite sprite = guideBrush != null ? guideBrush : Resources.Load<Sprite>("UI/MapTooltip/map_tooltip_spirit_neutral_v3");
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/MapTooltip/map_tooltip_spirit_neutral_v3");
            if (texture == null) return;
            if (ownedGuideSprite == null)
                ownedGuideSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprite = ownedGuideSprite;
        }
        GameObject mark = new GameObject("JourneyInkGuide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        mark.transform.SetParent(content, false);
        mark.layer = content.gameObject.layer;
        guide = mark.GetComponent<Image>();
        guide.sprite = sprite;
        guide.material = inkMaterial;
        guide.preserveAspect = true;
        guide.raycastTarget = false;
        guide.color = new Color(1f, 1f, 1f, 0f);
        guide.rectTransform.anchorMin = guide.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        guide.rectTransform.sizeDelta = new Vector2(43f, 64f);
    }

    private void OnDisable()
    {
        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = null;
        if (currentVisuals != null)
        {
            foreach (JourneyMapStepVisual visual in currentVisuals)
            {
                if (visual.Node != null) visual.Node.localScale = Vector3.one;
                if (visual.NodeGroup != null) visual.NodeGroup.alpha = 1f;
                if (visual.Line != null)
                {
                    visual.Line.sizeDelta = new Vector2(visual.LineLength, visual.Line.sizeDelta.y);
                    visual.Line.GetComponent<GameResultInkPathGraphic>()?.SetReveal(1f);
                }
                if (visual.LineGroup != null) visual.LineGroup.alpha = 1f;
            }
        }
        if (scrollRect != null) { scrollRect.enabled = true; scrollRect.StopMovement(); }
        if (guide != null && content != null && currentVisuals != null && currentVisuals.Count > 0)
        {
            RectTransform lastNode = currentVisuals[currentVisuals.Count - 1].Node;
            if (lastNode != null)
                guide.rectTransform.position = lastNode.position + content.TransformVector(new Vector3(48f, NodeSize * 1.06f, 0f));
            guide.color = new Color(1f, 1f, 1f, 0.94f);
        }
    }

    private void OnDestroy()
    {
        if (nodeInkMaterial != null)
        {
            if (Application.isPlaying) Destroy(nodeInkMaterial);
            else DestroyImmediate(nodeInkMaterial);
            nodeInkMaterial = null;
        }
        if (landscapeMaterial != null)
        {
            if (Application.isPlaying) Destroy(landscapeMaterial);
            else DestroyImmediate(landscapeMaterial);
            landscapeMaterial = null;
        }
        if (ownedGuideSprite == null) return;
        if (Application.isPlaying) Destroy(ownedGuideSprite);
        else DestroyImmediate(ownedGuideSprite);
    }

    private void EnsureRuntimeRoot(RectTransform container)
    {
        if (runtimeRoot != null)
            return;

        Transform existing = container.Find(RuntimeRootName);
        if (existing != null)
        {
            runtimeRoot = existing as RectTransform;
            return;
        }

        GameObject rootObject = new GameObject(RuntimeRootName, typeof(RectTransform));
        rootObject.transform.SetParent(container, false);
        rootObject.layer = container.gameObject.layer;
        runtimeRoot = rootObject.GetComponent<RectTransform>();
        runtimeRoot.anchorMin = Vector2.zero;
        runtimeRoot.anchorMax = Vector2.one;
        runtimeRoot.offsetMin = Vector2.zero;
        runtimeRoot.offsetMax = Vector2.zero;
    }

    private void EnsureScrollView(float width, float height)
    {
        GameObject scrollObject = new GameObject(ScrollViewName, typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(runtimeRoot, false);
        scrollObject.layer = runtimeRoot.gameObject.layer;

        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(14f, 12f);
        scrollRectTransform.offsetMax = new Vector2(-14f, -20f);

        GameObject viewportObject = new GameObject(ViewportName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollRectTransform, false);
        viewportObject.layer = runtimeRoot.gameObject.layer;
        viewport = viewportObject.GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.sprite = solidSprite;
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;
        viewportImage.canvasRenderer.cullTransparentMesh = false;
        UpdateViewportSoftness();

        GameObject contentObject = new GameObject(ContentName, typeof(RectTransform));
        contentObject.transform.SetParent(viewport, false);
        contentObject.layer = runtimeRoot.gameObject.layer;
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 28f;
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void LateUpdate()
    {
        UpdateViewportSoftness();
    }

    private void UpdateViewportSoftness()
    {
        if (viewport == null) return;
        RectMask2D mask = viewport.GetComponent<RectMask2D>();
        if (mask == null) return;
        // Match default UI/TMP clipping in root-canvas space, including a scaled or resized paper.
        // Unity's softness spans twice the distance from the edge to full opacity.
        float feather = landscapeMaterial != null ? landscapeMaterial.GetFloat("_BottomFeather") : 0.16f;
        Vector2Int softness = new Vector2Int(0, Mathf.RoundToInt(mask.canvasRect.height * feather * 2f));
        if (mask.softness != softness) mask.softness = softness;
    }

    private void ClearRuntimeRoot()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (runtimeRoot == null)
            return;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = runtimeRoot.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        viewport = null;
        content = null;
        sceneryRoot = null;
        scrollRect = null;
        currentVisuals = null;
        guide = null;
    }

    private IEnumerator PlayJourneyRoutine(List<JourneyMapStepVisual> visuals, int nextNodeIndex, float viewportHeight, float contentHeight)
    {
        if (scrollRect != null)
            scrollRect.enabled = false;
        yield return new WaitForSecondsRealtime(0.18f);

        for (int i = 0; i < visuals.Count; i++)
        {
            JourneyMapStepVisual visual = visuals[i];
            float stepSpeed = ResolveJourneyStepSpeed(i, visuals.Count);
            if (visual.Line != null)
                yield return AnimateLine(visual.Line, visual.LineGroup, visual.LineLength, PathDrawDuration / stepSpeed);

            yield return AnimateNode(visual.Node, visual.NodeGroup, NodeAppearDuration / stepSpeed);

            if (guide != null && visual.Node != null)
                guide.rectTransform.position = visual.Node.position + content.TransformVector(new Vector3(48f, NodeSize * 1.06f, 0f));

            if (visual.Node != null && i != nextNodeIndex)
                yield return FollowNode(visual.DistanceFromBottom, viewportHeight, contentHeight, ScrollFollowDuration / stepSpeed);
        }

        if (scrollRect != null)
        {
            scrollRect.enabled = true;
            scrollRect.StopMovement();
        }

        playRoutine = null;
        if (guide != null) guide.color = new Color(1f, 1f, 1f, 0.94f);
    }

    private static IEnumerator AnimateNode(RectTransform node, CanvasGroup group, float duration)
    {
        if (node == null || group == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.Lerp(0.86f, 1f, Mathf.SmoothStep(0f, 1f, t));
            group.alpha = t;
            node.localScale = Vector3.one * eased;
            yield return null;
        }

        group.alpha = 1f;
        node.localScale = Vector3.one;
    }

    private IEnumerator AnimateLine(RectTransform line, CanvasGroup group, float targetWidth, float duration)
    {
        if (line == null || group == null)
            yield break;

        float height = line.sizeDelta.y;
        float elapsed = 0f;
        GameResultInkPathGraphic stroke = line.GetComponent<GameResultInkPathGraphic>();
        line.sizeDelta = new Vector2(targetWidth, height);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            group.alpha = t;
            if (stroke != null)
            {
                stroke.SetReveal(eased);
                if (guide != null)
                {
                    guide.rectTransform.position = stroke.RevealWorldPosition + content.TransformVector(new Vector3(48f, NodeSize * 1.06f, 0f));
                    guide.color = new Color(1f, 1f, 1f, 0.94f);
                }
            }
            yield return null;
        }

        group.alpha = 1f;
        line.sizeDelta = new Vector2(targetWidth, height);
        if (stroke != null) stroke.SetReveal(1f);
    }

    private IEnumerator FollowNode(float nodeDistanceFromBottom, float viewportHeight, float contentHeight, float duration)
    {
        if (scrollRect == null || content == null || contentHeight <= viewportHeight)
            yield break;

        float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);
        float targetNormalizedPosition = Mathf.Clamp01((nodeDistanceFromBottom - viewportHeight * 0.62f) / maxScroll);
        float startNormalizedPosition = scrollRect.verticalNormalizedPosition;

        if (Mathf.Abs(targetNormalizedPosition - startNormalizedPosition) <= 0.001f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(startNormalizedPosition, targetNormalizedPosition, t);
            yield return null;
        }

        scrollRect.verticalNormalizedPosition = targetNormalizedPosition;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        return group;
    }

    private static float ResolveJourneyStepSpeed(int stepIndex, int totalSteps)
    {
        if (totalSteps <= 1)
            return JourneyMinStepSpeed;

        float progress = Mathf.Clamp01(stepIndex / (float)(totalSteps - 1));
        float routeLengthBoost = Mathf.InverseLerp(LongJourneyBoostStartCount, LongJourneyBoostEndCount, totalSteps) * 0.35f;
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        return Mathf.Lerp(JourneyMinStepSpeed, JourneyMaxStepSpeed + routeLengthBoost, easedProgress);
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = value - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static void EnsureSprites()
    {
        if (solidSprite == null)
            solidSprite = CreateSprite(8, ShapeKind.Square);
        if (battleSprite == null)
            battleSprite = CreateSprite(64, ShapeKind.Circle);
        if (shopSprite == null)
            shopSprite = CreateSprite(64, ShapeKind.Diamond);
        if (bossSprite == null)
            bossSprite = CreateSprite(64, ShapeKind.Hexagon);
    }

    private enum ShapeKind
    {
        Square,
        Circle,
        Diamond,
        Hexagon
    }

    private static Sprite CreateSprite(int size, ShapeKind shape)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.44f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool filled = IsFilled(shape, new Vector2(x, y), center, radius);
                texture.SetPixel(x, y, filled ? white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static bool IsFilled(ShapeKind shape, Vector2 point, Vector2 center, float radius)
    {
        Vector2 local = point - center;
        switch (shape)
        {
            case ShapeKind.Square:
                return true;
            case ShapeKind.Circle:
                return local.sqrMagnitude <= radius * radius;
            case ShapeKind.Diamond:
                return Mathf.Abs(local.x) + Mathf.Abs(local.y) <= radius;
            case ShapeKind.Hexagon:
                float x = Mathf.Abs(local.x);
                float y = Mathf.Abs(local.y);
                return x <= radius * 0.86f && y <= radius * 0.5f + (radius * 0.86f - x) * 0.58f;
            default:
                return true;
        }
    }

    private readonly struct JourneyMapStepVisual
    {
        public readonly RectTransform Node;
        public readonly CanvasGroup NodeGroup;
        public readonly RectTransform Line;
        public readonly CanvasGroup LineGroup;
        public readonly float LineLength;
        public readonly float DistanceFromBottom;

        public JourneyMapStepVisual(RectTransform node, CanvasGroup nodeGroup, RectTransform line, CanvasGroup lineGroup, float lineLength, float distanceFromBottom)
        {
            Node = node;
            NodeGroup = nodeGroup;
            Line = line;
            LineGroup = lineGroup;
            LineLength = lineLength;
            DistanceFromBottom = distanceFromBottom;
        }
    }
}
