using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using DG.Tweening;
using TMPro;

public class OptionWindowManager : MonoBehaviour
{
    //원본
    private const int MinimumOptionCanvasSortingOrder = 9000;

    public static OptionWindowManager Instance { get; private set; }

    [Header("전체 옵션 창 오브젝트")]
    [SerializeField] private GameObject totalWindowObj; // 최상위 전체 Window 오브젝트

    [Header("메인 1단계 / 2단계 패널")]
    [SerializeField] private GameObject mainPanel;       // 옵션/통계/메인메뉴 버튼이 있는 첫 패널
    [SerializeField] private GameObject mainOptionPanel; // 사운드/게임/비디오 상세 패널

    [Header("상세 옵션 내 서브 패널 UI")]
    [SerializeField] private GameObject soundPanelObj; // 사운드(오디오) 내용 패널
    [SerializeField] private GameObject gamePanelObj;  // 게임 내용 패널
    [SerializeField] private GameObject videoPanelObj; // 비디오 내용 패널
    [SerializeField] private GameObject statPanelObj;  // 통계 패널 (필요시 MainPanel에서 바로 띄우거나 이동)

    [Header("서브 패널 스크립트 참조")]
    [SerializeField] private GameOptionPanel gamePanel;
    [SerializeField] private VideoOptionPanel videoPanel;


    [Header("★ 1단계 일반 메뉴 버튼들 (OptionTabButton 컴포넌트)")]
    [SerializeField] private OptionTabButton mainOptionButton;
    [SerializeField] private OptionTabButton mainStatButton;
    [SerializeField] private OptionTabButton mainMenuButton;
    [SerializeField] private OptionTabButton mainTutorialButton;
    [SerializeField] private OptionTabButton mainExitButton;

    [Header("★ 2단계 상세 메뉴 탭 버튼들 (OptionTabButton 컴포넌트)")]
    [SerializeField] private OptionTabButton soundTabButton;
    [SerializeField] private OptionTabButton gameTabButton;
    [SerializeField] private OptionTabButton videoTabButton;
    [SerializeField] private OptionTabButton exitTabButton;

    [Header("★ 원신 도감형 튜토리얼 컴포넌트 추가")]
    [SerializeField] private GameObject tutorialPanelObj; // 하이어라키의 TutorialPanel 연결
    [SerializeField] private TutorialBookView tutorialBookView;
    [SerializeField] private OptionTabButton tutorialBackButton;

    [Header("타이틀 직접 튜토리얼 프리팹")]
    [SerializeField] private GameObject tutorialPanelPrefab;
    [SerializeField] private GameObject tutorialManagerPrefab;

    [Header("외부 UI 참조 (ESC 충돌 방지용)")]
    [SerializeField] private GameObject jokboCheckPanel; // 하이어라키의 JokboCheckPanel 연결

    [Header("런타임/타이틀 보조 설정")]
    [SerializeField] private bool registerRuntimeButtonListeners;
    [SerializeField] private bool directOptionWindowMode;

    private GameObject currentActiveSubPanel;
    private bool gameplayInputBlockActive;
    private bool cardInputBlockActive;
    private CardManager_2 cardInputBlockOwner;
    private Button dataManagementButton;
    private SettingsBookPresentation bookPresentation;
    private HelpGuidePresentation helpGuidePresentation;
    private bool helpOpenedDirectly;

    public static bool IsDirectOptionWindowOpen()
    {
        OptionWindowManager manager = Instance;
        return manager != null &&
            manager.directOptionWindowMode &&
            manager.totalWindowObj != null &&
            manager.totalWindowObj.activeInHierarchy;
    }

    public static bool IsAnyOptionWindowOpen()
    {
        OptionWindowManager manager = Instance;
        return manager != null &&
            manager.totalWindowObj != null &&
            manager.totalWindowObj.activeInHierarchy;
    }

    private void Awake()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
        else if (ShouldReplaceSingleton(Instance, this))
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        AutoBindMissingReferences();
        EnsureTitleDataManagementButton();
        if (registerRuntimeButtonListeners)
            RegisterRuntimeButtonListeners();
        EnsureBookPresentation();
    }

    /// <summary>삭제 진입은 프로필 화면이 담당하므로 과거 설정 버튼을 다시 표시하지 않습니다.</summary>
    private void EnsureTitleDataManagementButton()
    {
        if (dataManagementButton == null)
        {
            Transform existing = mainOptionPanel != null ? FindChildTransform(mainOptionPanel.transform, "DataManagementButton") : null;
            if (existing == null && totalWindowObj != null)
                existing = FindChildTransform(totalWindowObj.transform, "DataManagementButton");
            if (existing != null) dataManagementButton = existing.GetComponent<Button>();
        }
        if (dataManagementButton == null) return;
        dataManagementButton.onClick.RemoveListener(OpenProfileDataManagement);
        dataManagementButton.gameObject.SetActive(false);
    }
    /// <summary>옵션 창을 닫고 별도의 프로필 초기화 화면을 엽니다.</summary>
    private void OpenProfileDataManagement()
    {
        // Presenter가 준비되지 않았다면 옵션 창을 닫지 않아 사용자가 빈 화면에 남지 않게 합니다.
        ProfileMenuPresenter presenter = ProfileMenuPresenter.Current;
        if (presenter == null)
            presenter = FindFirstObjectByType<ProfileMenuPresenter>(FindObjectsInactive.Include);

        if (presenter == null)
        {
            Debug.LogError("[OptionWindow] 프로필 초기화 화면을 찾지 못했습니다.");
            return;
        }

        CloseTotalWindow();
        // The next modal opens immediately, so complete the book cleanup before handing off.
        if (bookPresentation != null) FinishClosingBook();
        presenter.OpenDataManagement();
    }

    private void OnDestroy()
    {
        helpGuidePresentation?.Dispose();
        bookPresentation?.HideImmediately();
        bookPresentation?.Dispose();
        ReleaseGameplayInputBlock();

        if (Instance == this)
            Instance = null;
    }

    private static bool ShouldReplaceSingleton(OptionWindowManager current, OptionWindowManager candidate)
    {
        if (current == null || candidate == null)
            return true;

        if (!current.isActiveAndEnabled)
            return true;

        if (current.directOptionWindowMode && !candidate.directOptionWindowMode)
            return true;

        Scene currentScene = current.gameObject.scene;
        Scene candidateScene = candidate.gameObject.scene;
        return candidateScene.IsValid() &&
            candidateScene == SceneManager.GetActiveScene() &&
            candidateScene != currentScene;
    }

    private void Start()
    {
        LoadAndSyncOptions();
    }

    private void OnDisable()
    {
        helpGuidePresentation?.HideImmediately();
        bookPresentation?.HideImmediately();
        if (bookPresentation != null && totalWindowObj != null && totalWindowObj.activeSelf)
            totalWindowObj.SetActive(false);
        ReleaseGameplayInputBlock();
    }

    private void Update()
    {
        if (totalWindowObj != null && totalWindowObj.activeInHierarchy)
            helpGuidePresentation?.Tick(Time.unscaledDeltaTime);
        else
            helpGuidePresentation?.HideImmediately();
        if (bookPresentation != null && totalWindowObj != null)
        {
            if (totalWindowObj.activeInHierarchy) bookPresentation.Tick(Time.unscaledDeltaTime);
            else bookPresentation.HideImmediately();
        }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleEscapeInput();
            return;
        }

        SyncGameplayInputBlockWithWindow();
    }

    public void HandleEscapeInput()
    {
        if (helpGuidePresentation != null && helpGuidePresentation.IsVisible)
        {
            if (!helpGuidePresentation.CloseTermDetail()) CloseHelpGuide();
            return;
        }
        if (jokboCheckPanel != null && jokboCheckPanel.activeSelf)
        {
            jokboCheckPanel.SetActive(false);
            return;
        }

        if (totalWindowObj != null && totalWindowObj.activeSelf)
        {
            if (bookPresentation != null)
            {
                CloseTotalWindow();
                return;
            }
            if (tutorialPanelObj != null && tutorialPanelObj.activeSelf)
            {
                if (directOptionWindowMode)
                {
                    CloseTotalWindow();
                }
                else
                {
                    tutorialPanelObj.SetActive(false);
                    if (mainPanel != null) mainPanel.SetActive(true);
                    ResetMainPanelButtons();
                }
            }
            else if (mainOptionPanel != null && mainOptionPanel.activeSelf)
            {
                if (directOptionWindowMode)
                    CloseTotalWindow();
                else
                    BackToMainPanel();
            }
            else if (statPanelObj != null && statPanelObj.activeSelf)
            {
                statPanelObj.SetActive(false);
                if (mainPanel != null) mainPanel.SetActive(true);
                ResetMainPanelButtons();
            }
            else
            {
                CloseTotalWindow();
            }
        }
        else if (!directOptionWindowMode)
        {
            OpenFirstMenu();
        }
    }

    public void OpenFirstMenu()
    {
        helpGuidePresentation?.HideImmediately();
        SoundManager.PlaySfx(data => data.Common_Button);
        if (totalWindowObj == null) return;
        EnsureBookPresentation();

        EnsureOptionWindowDrawsAboveRuntimeOverlays();
        BringOptionWindowToFront();
        SetOptionWindowInputBlock(true);
        totalWindowObj.SetActive(true);
        
        if (mainPanel != null) mainPanel.SetActive(true);
        if (mainOptionPanel != null) mainOptionPanel.SetActive(false);
        if (statPanelObj != null) statPanelObj.SetActive(false);
        if (tutorialPanelObj != null) tutorialPanelObj.SetActive(false);

        ResetMainPanelButtons();

        if (bookPresentation != null)
        {
            ShowBookPage(mainOptionPanel);
            ForceSwitchPanel(soundPanelObj);
            bookPresentation.Open();
            return;
        }

        RectTransform mainPanelRect = mainPanel != null ? mainPanel.GetComponent<RectTransform>() : null;
        if (mainPanelRect != null)
        {
            mainPanelRect.anchoredPosition = new Vector2(0f, -1080f); // 화면 아래 배치
            mainPanelRect.DOAnchorPosY(0f, 0.35f).SetEase(Ease.OutCubic).SetUpdate(true); // 솟아오르는 연출
        }
    }

    public void OnClickOptionButton()
    {
        SoundManager.PlaySfx(data => data.Common_Button);
        if (bookPresentation != null) ShowBookPage(mainOptionPanel);
        if (mainPanel != null) mainPanel.SetActive(false);
        if (mainOptionPanel != null) mainOptionPanel.SetActive(true);

        // 첫 진입 시 사운드 패널 강제 활성화 및 하이라이트 매핑
        ForceSwitchPanel(soundPanelObj);
    }

    public void OnClickTutorialButton()
    {
        SoundManager.PlaySfx(data => data.Common_Button);
        if (TryOpenHelpGuide(false)) return;
        if (bookPresentation != null) ShowBookPage(tutorialPanelObj);
        // 1. 뒤에 ESC 눌렀을 때 나오는 패널(mainPanel)을 끕니다.
        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

        // 2. 튜토리얼 북 전체 패널을 활성화합니다.
        if (tutorialPanelObj != null)
        {
            tutorialPanelObj.SetActive(true);
            InitializeTutorialBookView();
        }
    }

    public void OpenOptionMenuDirect()
    {
        helpGuidePresentation?.HideImmediately();
        SoundManager.PlaySfx(data => data.Common_Button);
        // 비활성 프리팹의 참조가 Awake에서 늦게 잡힌 경우를 대비해 표시 직전에 한 번 더 보장합니다.
        AutoBindMissingReferences();
        EnsureTitleDataManagementButton();

        if (totalWindowObj == null) return;
        EnsureBookPresentation();

        EnsureOptionWindowDrawsAboveRuntimeOverlays();
        BringOptionWindowToFront();
        SetOptionWindowInputBlock(true);
        DOTween.Kill(totalWindowObj.transform);
        totalWindowObj.SetActive(true);

        if (mainPanel != null) mainPanel.SetActive(false);
        if (statPanelObj != null) statPanelObj.SetActive(false);
        if (mainOptionPanel != null) mainOptionPanel.SetActive(true);

        ForceSwitchPanel(soundPanelObj);
        if (bookPresentation != null)
        {
            ShowBookPage(mainOptionPanel);
            bookPresentation.Open();
        }
    }

    public void OpenTutorialMenuDirect()
    {
        SoundManager.PlaySfx(data => data.Common_Button);
        AutoBindMissingReferences();

        if (totalWindowObj == null) return;
        EnsureBookPresentation();

        EnsureOptionWindowDrawsAboveRuntimeOverlays();
        BringOptionWindowToFront();
        SetOptionWindowInputBlock(true);
        DOTween.Kill(totalWindowObj.transform);
        totalWindowObj.SetActive(true);

        if (TryOpenHelpGuide(true))
        {
            bookPresentation?.HideImmediately();
            return;
        }

        if (mainPanel != null) mainPanel.SetActive(false);
        if (mainOptionPanel != null) mainOptionPanel.SetActive(false);
        if (statPanelObj != null) statPanelObj.SetActive(false);
        if (currentActiveSubPanel != null) currentActiveSubPanel.SetActive(false);
        currentActiveSubPanel = null;

        if (tutorialPanelObj != null)
        {
            tutorialPanelObj.SetActive(true);
            InitializeTutorialBookView();
        }
        if (bookPresentation != null)
        {
            ShowBookPage(tutorialPanelObj);
            bookPresentation.Open();
        }
    }

    private void InitializeTutorialBookView()
    {
        EnsureTutorialManagerInstance();

        if (tutorialBookView == null && tutorialPanelObj != null)
            tutorialBookView = tutorialPanelObj.GetComponent<TutorialBookView>();

        if (tutorialBookView != null && OptionTutorialManager.instance != null)
            tutorialBookView.InitializeBook(OptionTutorialManager.instance.GetTutorialSteps());
    }

    private bool TryOpenHelpGuide(bool directly)
    {
        if (helpGuidePresentation == null)
            helpGuidePresentation = HelpGuidePresentation.TryCreate(totalWindowObj, CloseHelpGuide);
        if (helpGuidePresentation == null) return false;
        helpOpenedDirectly = directly;
        helpGuidePresentation.Open();
        return true;
    }

    private void CloseHelpGuide()
    {
        if (helpGuidePresentation == null || !helpGuidePresentation.IsVisible) return;
        SoundManager.PlaySfx(data => data.UI_Cancel);
        bool closeWindow = helpOpenedDirectly;
        helpGuidePresentation.Close(() =>
        {
            if (!closeWindow) return;
            SaveOptions();
            FinishClosingBook();
        });
    }

    // 예외 필터를 거치지 않고 무조건 이미지 불빛과 패널을 강제 스위칭하는 안전 버스트 함수
    private void ForceSwitchPanel(GameObject targetPanel)
    {
        if (targetPanel == null) return;

        // 1. 내용물 패널 온오프 집행
        if (soundPanelObj != null) soundPanelObj.SetActive(soundPanelObj == targetPanel);
        if (gamePanelObj != null)  gamePanelObj.SetActive(gamePanelObj == targetPanel);
        if (videoPanelObj != null) videoPanelObj.SetActive(videoPanelObj == targetPanel);

        currentActiveSubPanel = targetPanel;

        // 2. 패널별 스크립트 강제 리로드 신호 송신
        if (targetPanel == soundPanelObj)
        {
            VolumeController[] controllers = soundPanelObj.GetComponentsInChildren<VolumeController>(true);
            foreach (var controller in controllers) controller.SetupInitialValue();
        }
        else if (targetPanel == gamePanelObj && gamePanel != null)
        {
            gamePanel.SetupInitialValue();
        }
        else if (targetPanel == videoPanelObj && videoPanel != null)
        {
            videoPanel.InitializeVideoUI();
        }

        // 3. ★ 버튼 하이라이트 강제 주황색 업데이트 실행
        Update2ndTabHighlights(targetPanel);
    }

    // 기존 호환성 유지용 스위치 함수 보완
    public void SwitchSubPanel(GameObject targetSubPanel)
    {
        SoundManager.PlaySfx(data => data.Common_Button);
        ForceSwitchPanel(targetSubPanel);
    }

    private void Update2ndTabHighlights(GameObject activePanel)
    {
        if (soundTabButton != null) soundTabButton.SetSelectState(activePanel == soundPanelObj);
        if (gameTabButton != null)  gameTabButton.SetSelectState(activePanel == gamePanelObj);
        if (videoTabButton != null) videoTabButton.SetSelectState(activePanel == videoPanelObj);
    }

    private void ResetMainPanelButtons()
    {
        if (mainOptionButton != null) mainOptionButton.SetSelectState(false);
        if (mainStatButton != null)   mainStatButton.SetSelectState(false);
        if (mainTutorialButton != null) mainTutorialButton.SetSelectState(false);
        if (mainExitButton != null)   mainExitButton.SetSelectState(false);
    }

    public void BackToMainPanel()
    {
        if (bookPresentation != null)
        {
            CloseTotalWindow();
            return;
        }
        SoundManager.PlaySfx(data => data.UI_Cancel);
        SaveOptions(); 

        if (mainOptionPanel != null) mainOptionPanel.SetActive(false);
        if (currentActiveSubPanel != null) currentActiveSubPanel.SetActive(false);
        currentActiveSubPanel = null;

        if (mainPanel != null) mainPanel.SetActive(true);
        ResetMainPanelButtons();
    }

    public void CloseTotalWindow()
    {
        helpGuidePresentation?.HideImmediately();
        SoundManager.PlaySfx(data => data.UI_Cancel);
        SaveOptions(); 

        if (totalWindowObj == null) return;

        if (bookPresentation != null)
        {
            bookPresentation.Close(FinishClosingBook);
            return;
        }

        if (directOptionWindowMode)
        {
            SetOptionWindowInputBlock(false);
            totalWindowObj.SetActive(false);
            ClearAllPanels();
            return;
        }

        if (mainPanel == null) return;

        RectTransform mainPanelRect = mainPanel.GetComponent<RectTransform>();
        if (mainPanelRect != null)
        {
            // 알맹이가 아래로 내려가는 트윈 연출
            mainPanelRect.DOAnchorPosY(-1080f, 0.25f).SetEase(Ease.InCubic).SetUpdate(true)
                .OnComplete(() => {
                    totalWindowObj.SetActive(false); // 하강이 끝나면 전체 오브젝트 비활성화
                    SetOptionWindowInputBlock(false);
                    ClearAllPanels();
                });
        }
        else
        {
            SetOptionWindowInputBlock(false);
            totalWindowObj.SetActive(false);
            ClearAllPanels();
        }
    }

    private void EnsureOptionWindowDrawsAboveRuntimeOverlays()
    {
        Canvas optionCanvas = totalWindowObj.GetComponentInParent<Canvas>(true);
        if (optionCanvas == null)
            optionCanvas = GetComponentInParent<Canvas>(true);

        if (optionCanvas == null)
            return;

        optionCanvas.overrideSorting = true;
        optionCanvas.sortingOrder = Mathf.Max(optionCanvas.sortingOrder, MinimumOptionCanvasSortingOrder);
    }

    private void BringOptionWindowToFront()
    {
        if (transform != null)
            transform.SetAsLastSibling();

        if (totalWindowObj != null)
            totalWindowObj.transform.SetAsLastSibling();

        if (mainOptionPanel != null)
        {
            BringTabButtonToFront(soundTabButton);
            BringTabButtonToFront(gameTabButton);
            BringTabButtonToFront(videoTabButton);
            BringTabButtonToFront(exitTabButton);
        }
    }

    private static void BringTabButtonToFront(OptionTabButton tabButton)
    {
        if (tabButton != null)
            tabButton.transform.SetAsLastSibling();
    }

    private void SetOptionWindowInputBlock(bool shouldBlock)
    {
        if (totalWindowObj == null)
            return;

        CanvasGroup canvasGroup = totalWindowObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = totalWindowObj.AddComponent<CanvasGroup>();

        canvasGroup.interactable = shouldBlock;
        canvasGroup.blocksRaycasts = shouldBlock;

        if (shouldBlock)
            AcquireGameplayInputBlock();
        else
            ReleaseGameplayInputBlock();
    }

    private void SyncGameplayInputBlockWithWindow()
    {
        bool shouldBlock = totalWindowObj != null && totalWindowObj.activeInHierarchy;
        if (shouldBlock)
            AcquireGameplayInputBlock();
        else
            ReleaseGameplayInputBlock();
    }

    private void AcquireGameplayInputBlock()
    {
        if (!gameplayInputBlockActive)
        {
            gameplayInputBlockActive = true;
            HeadLookInputController.PushExternalInputBlock();
            InteractionManager.PushExternalInteractionBlock();
        }

        AcquireCardInputBlock();
    }

    private void AcquireCardInputBlock()
    {
        if (cardInputBlockActive && cardInputBlockOwner != null)
            return;

        cardInputBlockActive = false;
        cardInputBlockOwner = CardManager_2.Instance;

        if (cardInputBlockOwner == null)
            return;

        cardInputBlockOwner.PushExternalCardInteractionBlock();
        cardInputBlockActive = true;
    }

    private void ReleaseGameplayInputBlock()
    {
        ReleaseCardInputBlock();

        if (gameplayInputBlockActive)
        {
            gameplayInputBlockActive = false;
            HeadLookInputController.PopExternalInputBlock();
            InteractionManager.PopExternalInteractionBlock();
        }
    }

    private void ReleaseCardInputBlock()
    {
        if (!cardInputBlockActive)
            return;

        if (cardInputBlockOwner != null)
            cardInputBlockOwner.PopExternalCardInteractionBlock();

        cardInputBlockOwner = null;
        cardInputBlockActive = false;
    }

    private void ClearAllPanels()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (mainOptionPanel != null) mainOptionPanel.SetActive(false);
        if (tutorialPanelObj != null) tutorialPanelObj.SetActive(false);
        if (statPanelObj != null) statPanelObj.SetActive(false);
        if (currentActiveSubPanel != null) currentActiveSubPanel.SetActive(false);
        currentActiveSubPanel = null;

        ResetMainPanelButtons();
        if (soundTabButton != null) soundTabButton.SetSelectState(false);
        if (gameTabButton != null)  gameTabButton.SetSelectState(false);
        if (videoTabButton != null) videoTabButton.SetSelectState(false);
    }

    // ==========================================
    // ★ [유니티 인스펙터 버튼 매핑 검증용 핵심 함수들]
    // ==========================================
    public void OnClickSoundTab() { SwitchSubPanel(soundPanelObj); }
    public void OnClickGameTab()  { SwitchSubPanel(gamePanelObj);  }
    public void OnClickVideoTab() { SwitchSubPanel(videoPanelObj); }

    public void OnClickStatButton()
    {
        SoundManager.PlaySfx(data => data.Common_Button);
        if (bookPresentation != null) ShowBookPage(statPanelObj);
        if (mainPanel != null) mainPanel.SetActive(false);
        if (statPanelObj != null) statPanelObj.SetActive(true);
    }

    public void OnClickMainMenuButton()
    {
        SoundManager.PlaySfx(data => data.Common_Button);
        bookPresentation?.HideImmediately();
        // 노드 내부 최신값은 일반 런 필드에 기록하되, 이어하기용 안전 체크포인트는 그대로 보존합니다.
        SaveManager.instance?.SaveRunData();
        DOTween.KillAll();
        SceneManager.LoadScene("TitleScene_Rebuilt");
    }

    public void LoadAndSyncOptions()
    {
        if (videoPanel != null) videoPanel.InitializeVideoUI();
    }

    public void SaveOptions()
    {
        PlayerPrefs.Save();
        if (SaveManager.instance != null)
        {
            SaveManager.instance.SaveSettingsData();
        }
    }

    private void EnsureBookPresentation()
    {
        if (bookPresentation != null || totalWindowObj == null) return;
        // Keep the authored window as a fallback if production textures cannot be loaded.
        if (Resources.Load<Sprite>("UI/SettingsBook/settings_book_page") == null ||
            Resources.Load<Sprite>("UI/SettingsBook/settings_book_cover") == null) return;

        if (mainTutorialButton == null && tutorialPanelObj != null)
            mainTutorialButton = CreateBookButton("BookTutorialButton", OnClickTutorialButton);
        if (mainExitButton == null && exitTabButton == null)
            mainExitButton = CreateBookButton("BookCloseButton", CloseTotalWindow);

        bookPresentation = SettingsBookPresentation.TryCreate(totalWindowObj, mainPanel, mainOptionPanel,
            soundPanelObj, gamePanelObj, videoPanelObj, statPanelObj, tutorialPanelObj,
            new[] { mainOptionButton, mainStatButton, mainTutorialButton, mainMenuButton },
            new[] { soundTabButton, gameTabButton, videoTabButton }, mainExitButton != null ? mainExitButton : exitTabButton);
        EnsureTitleDataManagementButton();
        if (bookPresentation != null && mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(!directOptionWindowMode);
    }

    private OptionTabButton CreateBookButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = totalWindowObj.layer;
        buttonObject.transform.SetParent(totalWindowObj.transform, false);
        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = totalWindowObj.layer;
        textObject.transform.SetParent(buttonObject.transform, false);
        TMP_Text source = totalWindowObj.GetComponentInChildren<TMP_Text>(true);
        if (source != null) textObject.GetComponent<TextMeshProUGUI>().font = source.font;
        OptionTabButton tab = buttonObject.AddComponent<OptionTabButton>();
        AddListener(tab, action);
        return tab;
    }

    private void ShowBookPage(GameObject target)
    {
        if (dataManagementButton != null) dataManagementButton.gameObject.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(false);
        if (mainOptionPanel != null) mainOptionPanel.SetActive(target == mainOptionPanel);
        if (statPanelObj != null) statPanelObj.SetActive(target == statPanelObj);
        if (tutorialPanelObj != null) tutorialPanelObj.SetActive(target == tutorialPanelObj);
        if (mainOptionButton != null) mainOptionButton.SetSelectState(target == mainOptionPanel);
        if (mainStatButton != null) mainStatButton.SetSelectState(target == statPanelObj);
        if (mainTutorialButton != null) mainTutorialButton.SetSelectState(target == tutorialPanelObj);
    }

    private void FinishClosingBook()
    {
        bookPresentation?.HideImmediately();
        if (totalWindowObj != null) totalWindowObj.SetActive(false);
        SetOptionWindowInputBlock(false);
        ClearAllPanels();
    }

    private void AutoBindMissingReferences()
    {
        Transform root = totalWindowObj != null ? totalWindowObj.transform : transform;

        if (totalWindowObj == null && root.name == "OptionPanel")
            totalWindowObj = root.gameObject;

        if (totalWindowObj == null)
            totalWindowObj = FindChildTransform(root, "OptionPanel")?.gameObject;

        if (totalWindowObj != null)
            root = totalWindowObj.transform;

        if (mainPanel == null)
            mainPanel = FindChildTransform(root, "MainPanel")?.gameObject;

        if (mainOptionPanel == null)
            mainOptionPanel = FindChildTransform(root, "MainOptionPanel")?.gameObject;

        if (soundPanelObj == null)
            soundPanelObj = FindChildTransform(root, "SoundPanel")?.gameObject;

        if (gamePanelObj == null)
            gamePanelObj = FindChildTransform(root, "GamePanel")?.gameObject;

        if (videoPanelObj == null)
            videoPanelObj = FindChildTransform(root, "VideoPanel")?.gameObject;

        if (statPanelObj == null)
            statPanelObj = FindChildTransform(root, "StatPanel")?.gameObject;

        if (tutorialPanelObj == null)
            tutorialPanelObj = FindChildTransform(root, "TutorialPanel")?.gameObject;

        if (tutorialPanelObj == null && tutorialPanelPrefab != null)
        {
            tutorialPanelObj = Instantiate(tutorialPanelPrefab, root, false);
            tutorialPanelObj.name = tutorialPanelPrefab.name;
        }

        if (tutorialBookView == null && tutorialPanelObj != null)
            tutorialBookView = tutorialPanelObj.GetComponent<TutorialBookView>();

        if (tutorialPanelObj != null)
            tutorialPanelObj.SetActive(false);

        if (gamePanel == null && gamePanelObj != null)
            gamePanel = gamePanelObj.GetComponent<GameOptionPanel>();

        if (videoPanel == null && videoPanelObj != null)
            videoPanel = videoPanelObj.GetComponent<VideoOptionPanel>();

        if (mainOptionButton == null)
            mainOptionButton = FindOptionTabButtonByPersistentMethod(root, nameof(OnClickOptionButton)) ?? FindOptionTabButton(root, "OptionButton", mainPanel);

        if (mainStatButton == null)
            mainStatButton = FindOptionTabButtonByPersistentMethod(root, nameof(OnClickStatButton)) ?? FindOptionTabButton(root, "StatButton", mainPanel);

        if (mainMenuButton == null)
            mainMenuButton = FindOptionTabButtonByPersistentMethod(root, nameof(OnClickMainMenuButton)) ?? FindOptionTabButton(root, "MainmenuButton", mainPanel);

        if (mainExitButton == null)
            mainExitButton = FindOptionTabButtonByPersistentMethod(root, nameof(HandleEscapeInput), mainPanel) ?? FindOptionTabButton(root, "BackButton", mainPanel);

        if (tutorialBackButton == null)
            tutorialBackButton = FindOptionTabButtonByPersistentMethod(root, nameof(HandleEscapeInput), tutorialPanelObj) ?? FindOptionTabButton(root, "BackButton", tutorialPanelObj);

        if (soundTabButton == null)
            soundTabButton = FindOptionTabButtonByPersistentMethod(root, nameof(OnClickSoundTab)) ?? FindOptionTabButtonByLabel(root, "사운드");

        if (gameTabButton == null)
            gameTabButton = FindOptionTabButtonByPersistentMethod(root, nameof(OnClickGameTab)) ?? FindOptionTabButtonByLabel(root, "게임");

        if (videoTabButton == null)
            videoTabButton = FindOptionTabButtonByPersistentMethod(root, nameof(OnClickVideoTab)) ?? FindOptionTabButtonByLabel(root, "비디오");

        if (exitTabButton == null)
            exitTabButton = FindOptionTabButtonByPersistentMethod(root, nameof(HandleEscapeInput), mainOptionPanel) ?? FindOptionTabButtonByLabel(root, "뒤로가기");
    }

    private void RegisterRuntimeButtonListeners()
    {
        AddListener(mainOptionButton, OnClickOptionButton);
        AddListener(mainStatButton, OnClickStatButton);
        AddListener(mainMenuButton, OnClickMainMenuButton);
        AddListener(mainExitButton, CloseTotalWindow);
        AddListener(mainTutorialButton, OnClickTutorialButton);
        AddListener(tutorialBackButton, HandleEscapeInput);
        AddListener(soundTabButton, OnClickSoundTab);
        AddListener(gameTabButton, OnClickGameTab);
        AddListener(videoTabButton, OnClickVideoTab);
        AddListener(exitTabButton, directOptionWindowMode ? CloseTotalWindow : BackToMainPanel);
    }

    private void EnsureTutorialManagerInstance()
    {
        if (OptionTutorialManager.instance != null || tutorialManagerPrefab == null)
            return;

        GameObject managerObject = Instantiate(tutorialManagerPrefab);
        managerObject.name = tutorialManagerPrefab.name;
    }

    private static void AddListener(OptionTabButton tabButton, UnityEngine.Events.UnityAction action)
    {
        if (tabButton == null || action == null)
            return;

        Button button = tabButton.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static OptionTabButton FindOptionTabButton(Transform root, string objectName, GameObject scope = null, bool byPanelName = false)
    {
        Transform searchRoot = scope != null ? scope.transform : root;
        if (searchRoot == null)
            return null;

        if (!byPanelName)
            return FindChildTransform(searchRoot, objectName)?.GetComponent<OptionTabButton>();

        Transform panel = FindChildTransform(searchRoot, objectName);
        return panel != null ? panel.GetComponentInChildren<OptionTabButton>(true) : null;
    }

    private static OptionTabButton FindOptionTabButtonByPersistentMethod(Transform root, string methodName, GameObject scope = null)
    {
        Transform searchRoot = scope != null ? scope.transform : root;
        if (searchRoot == null || string.IsNullOrEmpty(methodName))
            return null;

        OptionTabButton[] tabButtons = searchRoot.GetComponentsInChildren<OptionTabButton>(true);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            OptionTabButton tabButton = tabButtons[i];
            Button button = tabButton != null ? tabButton.GetComponent<Button>() : null;
            if (button == null)
                continue;

            int eventCount = button.onClick.GetPersistentEventCount();
            for (int eventIndex = 0; eventIndex < eventCount; eventIndex++)
            {
                if (button.onClick.GetPersistentMethodName(eventIndex) == methodName)
                    return tabButton;
            }
        }

        return null;
    }

    private static OptionTabButton FindOptionTabButtonByLabel(Transform root, string label)
    {
        if (root == null || string.IsNullOrEmpty(label))
            return null;

        OptionTabButton[] tabButtons = root.GetComponentsInChildren<OptionTabButton>(true);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            OptionTabButton tabButton = tabButtons[i];
            TMPro.TMP_Text text = tabButton != null ? tabButton.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
            if (text != null && text.text.Trim() == label)
                return tabButton;
        }

        return null;
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }
}
