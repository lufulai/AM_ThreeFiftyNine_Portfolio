using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PackOpeningManager : MonoBehaviour
{
    private const string CardRemovalRuntimeContentName = "CardRemovalRuntimeContent";
    private const string CardRemovalSkyDimBackgroundName = "SkyDimBackground";
    private const string CardPackChoiceOverlayCanvasName = "CardPackChoiceOverlayCanvas";
    private const string CardPackChoiceRootName = "CardPackChoiceSkyRoot";
    private const int CardRemovalOverlaySortingOrder = 1000;
    private const int CardPackChoiceOverlaySortingOrder = 1005;
    private const int WorldPackHoverTagCapacity = 6;
    private const float SceneRemovalJokerTopOffset = 44f;
    private const float SceneRemovalMonthTopPaddingWithJoker = 138f;
    private const float RemovalJokerRowHeight = 156f;
    private static readonly Vector2 RemovalCardPrefabFallbackSize = new Vector2(250f, 350f);

    public static PackOpeningManager Instance { get; private set; }
    public bool IsBrowsingPackInventory { get; private set; }
    private bool isPackInventoryTransition;
    private CanvasGroup inventoryBrowsePanelGroup;
    private float inventoryBrowsePanelAlpha;
    private bool inventoryBrowsePanelInteractable;
    private bool inventoryBrowsePanelBlocksRaycasts;
    private GameObject inventoryBrowseOverlayRoot;
    private bool packChoiceIntroComplete;
    private Button pressedPackButton;
    private bool inventoryBrowseOverlayEnabled;
    private readonly List<CardSO> generatedPackCards = new List<CardSO>();

    [Header("UI 참조")]
    public GameObject packOpeningPanel;      // 팩 개봉 화면 전체 패널
    public Transform cardSpawnParent;        // 생성된 카드들이 나열될 부모 (Horizontal Layout Group 추천)
    public GameObject packItemPrefab;        // 방금 만든 PackItemUI가 붙은 프리팹
    public TextMeshProUGUI infoText;         // 예: "최대 2장 선택하세요! (남은 횟수: 2)"

    [Header("카드 제거 하늘 연출")]
    [SerializeField] private GameObject removalCardItemPrefab;
    [SerializeField] private bool useRemovalSkyPresentation = true;
    [SerializeField] [Min(0f)] private float removalCardCascadeDelay = 0.035f;
    [SerializeField] [Min(0.01f)] private float removalCardRevealDuration = 0.22f;
    [SerializeField] [Min(0.01f)] private float removalSelectedCardDuration = 0.36f;
    [SerializeField] private Color removalSkyTint = new Color(0.08f, 0.18f, 0.32f, 0.86f);
    [SerializeField] private Color removalStarTint = new Color(0.82f, 0.94f, 1f, 0.68f);
    [SerializeField] private Color removalMonthPanelTint = new Color(0.72f, 0.86f, 1f, 0.08f);
    [SerializeField] private Color removalMonthLabelColor = new Color(0.78f, 0.9f, 1f, 0.92f);
    [SerializeField] private Color removalInactiveCardTint = new Color(1f, 1f, 1f, 0.24f);
    [SerializeField] private Vector2 removalCardCellSize = new Vector2(82f, 118f);
    [SerializeField] [Min(1f)] private float removalCardOverlapStep = 26f;
    [SerializeField] [Min(1f)] private float removalCardStackMaxSpread = 92f;
    [SerializeField] [Min(0f)] private float removalCardStackTilt = 4.5f;
    [SerializeField] [Min(0)] private int removalSkyStarCount = 44;
    [Tooltip("새 패 제거 연출을 준비하는 동안 기존 카드 제거 UI를 표시하지 않습니다.")]
    [SerializeField] private bool suppressCardRemovalUi = true;

    [Header("카드팩 선택 연출")]
    [SerializeField] private bool useCardPackSkyPresentation = true;
    [SerializeField] private Color cardPackPanelTint = new Color(0.055f, 0.18f, 0.29f, 0.92f);
    [SerializeField] private Color cardPackAccentColor = new Color(0.36f, 0.74f, 1f, 0.96f);
    [SerializeField] [Min(0f)] private float cardPackChoiceCascadeDelay = 0.055f;
    [SerializeField] [Min(0.01f)] private float cardPackChoiceRevealDuration = 0.24f;
    [SerializeField] [Min(0.01f)] private float cardPackChoiceResolveDuration = 0.3f;
    [SerializeField] private Vector2 cardPackChoiceItemSize = new Vector2(190f, 270f);

    [Header("카드 제거 카메라 연출")]
    [SerializeField] private bool moveCameraForCardRemoval = true;
    [SerializeField] private Vector3 removalSkyCameraEulerOffset = new Vector3(-58f, 0f, 0f);
    [SerializeField] [Min(0.01f)] private float removalCameraTiltDuration = 0.65f;
    [SerializeField] [Min(0.01f)] private float removalCameraReturnDuration = 0.35f;

    [Header("카드 제거 씬 배치 UI")]
    [SerializeField] private GameObject cardRemovalSkyViewRoot;
    [SerializeField] private CanvasGroup cardRemovalSkyCanvasGroup;
    [SerializeField] private TextMeshProUGUI cardRemovalTitleText;
    [SerializeField] private Button cardRemovalCancelButton;
    [SerializeField] private GameObject cardRemovalJokerSection;
    [SerializeField] private Transform cardRemovalJokerGrid;
    [SerializeField] private Transform[] cardRemovalMonthGrids = new Transform[10];

    private int remainingPicks = 0;
    private bool hasInputBlock;
    private bool isCardRemovalMode;
    private bool isCardUpgradeMode;
    private bool isCardPackChoiceMode;
    private bool isRemovalResolving;
    private bool isPackChoiceResolving;
    private bool isPackCloseResolving;
    private GameObject removalSkyRoot;
    private GameObject cardPackChoiceRoot;
    private GameObject cardPackChoiceOverlayCanvasRoot;
    private TextMeshProUGUI cardPackChoiceTitleText;
    private TextMeshProUGUI cardPackChoiceDetailText;
    private TextMeshProUGUI worldPackChoiceCaptionText;
    private TextMeshProUGUI worldPackRemainingText;
    private TextMeshProUGUI worldPackWarningText;
    private RectTransform worldPackHoverRoot;
    private TextMeshProUGUI worldPackHoverTitleText;
    private TextMeshProUGUI worldPackHoverBodyText;
    private RectTransform worldPackHoverTagsRoot;
    private readonly List<RuntimeItemInfoTag> worldPackHoverTags = new List<RuntimeItemInfoTag>(WorldPackHoverTagCapacity);
    private PackItemUI worldPackHoveredItem;
    private Rect worldPackHoverPlacement;
    private Vector2 worldPackHoverPageSize;
    private float worldPackHoverPixelScale;
    private Button cardPackChoiceConfirmButton;
    private Button cardPackChoiceSkipButton;
    private Action packClosedCallback;
    private Action<bool> cardRemovalClosedCallback;
    private Action<bool> cardUpgradeClosedCallback;
    private readonly List<PackItemUI> activeRemovalItems = new List<PackItemUI>();
    private readonly List<PackItemUI> activeCardPackChoiceItems = new List<PackItemUI>();
    private readonly List<PackItemUI> selectedWorldPackChoices = new List<PackItemUI>();
    // Display slots stay stable; chronology decides which occupied slot gets replaced next.
    private readonly List<PackItemUI> worldPackSelectionOrder = new List<PackItemUI>();
    private PackItemUI selectedCardPackChoiceItem;
    private bool cardRemovalWasApplied;
    private bool cardUpgradeWasAttempted;
    private bool cardUpgradeIsEnding;
    private CardSO selectedUpgradeCard;
    private GameObject cardUpgradePanel;
    private TextMeshProUGUI cardUpgradeTitleText;
    private TextMeshProUGUI cardUpgradeDetailText;
    private TextMeshProUGUI cardUpgradeResultText;
    private Button cardUpgradeAttemptButton;
    private CardStrengthenPresentationController cardStrengthenPresentationController;
    private Image packOpeningPanelImage;
    private CanvasGroup packOpeningPanelCanvasGroup;
    private bool hasPackOpeningPanelImageState;
    private bool originalPackOpeningPanelImageEnabled;
    private Color originalPackOpeningPanelImageColor;
    private bool hasPackOpeningPanelCanvasGroupState;
    private float originalPackOpeningPanelCanvasAlpha = 1f;
    private bool originalPackOpeningPanelCanvasInteractable = true;
    private bool originalPackOpeningPanelCanvasBlocksRaycasts = true;
    private bool isUsingSceneRemovalSkyView;
    private Transform removalCameraTransform;
    private Quaternion removalOriginalCameraRotation;
    private bool hasRemovalCameraPose;
    private GameObject cardRemovalOverlayCanvasRoot;
    private Transform cardRemovalOriginalParent;
    private int cardRemovalOriginalSiblingIndex;
    private bool hasCardRemovalOriginalParent;
    private Transform cardRemovalSkyBackgroundTransform;
    private Transform cardRemovalSkyBackgroundOriginalParent;
    private int cardRemovalSkyBackgroundOriginalSiblingIndex;
    private CanvasGroup cardRemovalSkyBackgroundCanvasGroup;
    private bool hasCardRemovalSkyBackgroundOriginalParent;
    private MapDeterministicRandom activePackContentRandom;
    private long packAcquirePresentationSequence;
    private bool hasPackAcquirePresentationSequence;
    private readonly List<ArtifactObject> pendingWorldPackArtifacts = new List<ArtifactObject>();
    private readonly List<CardSO> pendingWorldPackCards = new List<CardSO>();
    private TableInCardDisplay worldPackDeckDisplay;
    private GameObject worldPackUpgradePresentationRoot;
    private readonly Stack<IEnumerator> worldPackAcquisitionRoutines = new Stack<IEnumerator>();
    private Vector3? shopPackPurchasePosition;
    private float shopPackTableHeight;
    private ShopPackWorldPresentation shopPackPresentation;
    private PackItemUI pressedWorldPackItem;
    private Action shopPackClosedCallback;

    private sealed class PackCandidate
    {
        public object Data;
        public PackType Type;
        public Sprite Icon;
        public GameObject ModelPrefab;
        public string ItemName;
        public string Description;
    }

    private void Awake()
    {
        Instance = this;
        EnsureCardRemovalCancelButtonBinding();
    }

    private void OnDestroy()
    {
        ReleaseWorldPackAcquisitionVisuals();
        shopPackPresentation?.Dispose();
        shopPackPresentation = null;
        RestorePackInventoryVisibility();
        ReleaseGeneratedPackCards();
        if (hasPackAcquirePresentationSequence && ConsumableManager.Instance != null)
        {
            ConsumableManager.Instance.ReleasePendingAcquirePresentationAfter(
                packAcquirePresentationSequence);
        }

        if (hasRemovalCameraPose && removalCameraTransform != null)
        {
            removalCameraTransform.DOKill();
            removalCameraTransform.rotation = removalOriginalCameraRotation;
        }

        ReleaseInputBlock();
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        // A disabled coroutine owner cannot finish the asynchronous pack close path.
        if (shopPackPresentation == null && !shopPackPurchasePosition.HasValue) return;
        Action closed = shopPackClosedCallback ?? packClosedCallback;
        shopPackClosedCallback = null;
        packClosedCallback = null;
        StopAllCoroutines();
        ReleaseWorldPackAcquisitionVisuals();
        RestorePackInventoryVisibility();
        ClearCardPackChoiceRoot();
        ReleaseGeneratedPackCards();
        if (hasPackAcquirePresentationSequence && ConsumableManager.Instance != null)
            ConsumableManager.Instance.ReleasePendingAcquirePresentationAfter(packAcquirePresentationSequence);
        hasPackAcquirePresentationSequence = false;
        packAcquirePresentationSequence = 0;
        shopPackPurchasePosition = null;
        isCardPackChoiceMode = false;
        isPackChoiceResolving = false;
        isPackCloseResolving = false;
        isPackInventoryTransition = false;
        IsBrowsingPackInventory = false;
        packChoiceIntroComplete = false;
        if (hasRemovalCameraPose && removalCameraTransform != null)
        {
            removalCameraTransform.DOKill();
            removalCameraTransform.rotation = removalOriginalCameraRotation;
            hasRemovalCameraPose = false;
            removalCameraTransform = null;
        }
        SetPackOpeningPanelShellForCardRemoval(false);
        if (packOpeningPanel != null) packOpeningPanel.SetActive(false);
        ReleaseInputBlock();
        closed?.Invoke();
    }

    private void Update()
    {
        if (isCardUpgradeMode && cardStrengthenPresentationController != null
            && cardStrengthenPresentationController.IsRunning)
        {
            if (cardStrengthenPresentationController.CanBrowseInventory) ReleaseInputBlock();
            else AcquireInputBlock();
            return;
        }
        shopPackPresentation?.Tick(CanUseWorldPackInput());
        UpdatePackButtonInputFallback();
        if (!isCardPackChoiceMode || !packChoiceIntroComplete || isCardRemovalMode || isCardUpgradeMode || isPackCloseResolving
            || isPackChoiceResolving || isPackInventoryTransition || InteractionManager.IsExternallyBlocked)
            return;
        CameraHeadLookController view = Camera.main != null ? Camera.main.GetComponent<CameraHeadLookController>() : null;
        if (view == null) return;
        if (IsBrowsingPackInventory)
        {
            // Nodes identify the destination before the camera tween finishes. Restore the
            // fixed world row while it pans back, rather than making it pop in on arrival.
            if (shopPackPresentation != null)
            {
                if (view.IsAtStageDefaultView) shopPackPresentation.SetVisible(true);
                else if (!view.IsCameraMoving) shopPackPresentation.SetVisible(false);
            }
            if (view.IsCameraMoving) return;
            if (view.IsAtStageDefaultView)
            {
                IsBrowsingPackInventory = false;
                AcquireInputBlock();
                RestorePackInventoryVisibility(true);
                if (shopPackPresentation == null) TiltCameraForCardRemoval();
            }
            return;
        }
        if (view.IsCameraMoving) return;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || packOpeningPanel == null || !packOpeningPanel.activeInHierarchy) return;
        Vector2Int direction = keyboard.aKey.wasPressedThisFrame ? Vector2Int.left
            : keyboard.dKey.wasPressedThisFrame ? Vector2Int.right
            : keyboard.sKey.wasPressedThisFrame ? Vector2Int.down : Vector2Int.zero;
        if (direction == Vector2Int.zero) return;
        isPackInventoryTransition = true;
        HidePackForInventory();
        RestoreCameraAfterCardRemoval(() =>
        {
            if (this == null) return;
            isPackInventoryTransition = false;
            if (!isCardPackChoiceMode || isPackCloseResolving) return;
            ReleaseInputBlock();
            IsBrowsingPackInventory = view != null && view.MoveInDirection(direction);
            if (!IsBrowsingPackInventory)
            {
                AcquireInputBlock();
                RestorePackInventoryVisibility();
                if (shopPackPresentation == null) TiltCameraForCardRemoval();
            }
        });
    }

    private void LateUpdate()
    {
        shopPackPresentation?.RefreshChoiceEffects();
        if (shopPackPresentation != null)
        {
            RefreshWorldPackActionColors();
            RefreshWorldPackHover();
        }
    }

    private void RefreshWorldPackActionColors()
    {
        bool canUse = CanUseWorldPackInput();
        var mouse = UnityEngine.InputSystem.Mouse.current;
        Vector2 pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        var system = UnityEngine.EventSystems.EventSystem.current;
        GameObject focused = canUse && system != null && system.isActiveAndEnabled
            && system.currentInputModule != null && system.currentInputModule.isActiveAndEnabled
            ? system.currentSelectedGameObject : null;
        ApplyWorldPackActionColor(cardPackChoiceConfirmButton, canUse, mouse, pointer, focused);
        ApplyWorldPackActionColor(cardPackChoiceSkipButton, canUse, mouse, pointer, focused);
    }

    private void ApplyWorldPackActionColor(Button button, bool canUse,
        UnityEngine.InputSystem.Mouse mouse, Vector2 pointer, GameObject focused)
    {
        if (button == null || button.targetGraphic == null) return;
        ColorBlock colors = button.colors;
        bool over = canUse && mouse != null && IsPackButtonAtPointer(button, pointer);
        bool pressed = over && button == pressedPackButton && mouse.leftButton.isPressed;
        Color color = !button.IsInteractable() ? colors.disabledColor
            : pressed ? colors.pressedColor : over ? colors.highlightedColor
            : button.gameObject == focused ? colors.selectedColor : colors.normalColor;
        // World-input scenes also run without an EventSystem; use the same tint for both paths.
        button.targetGraphic.CrossFadeColor(color, 0f, true, true);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = !button.IsInteractable() ? new Color(0.28f, 0.25f, 0.22f, 0.6f)
                : pressed || over || button.gameObject == focused ? new Color(1f, 0.95f, 0.82f)
                : new Color(0.07f, 0.055f, 0.04f);
    }

    private void UpdatePackButtonInputFallback()
    {
        if (shopPackPresentation != null)
        {
            UpdateWorldPackInput();
            return;
        }
        // Match the strengthen UI: world-input scenes may have no active EventSystem module.
        var system = UnityEngine.EventSystems.EventSystem.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (!isCardPackChoiceMode || !packChoiceIntroComplete || IsBrowsingPackInventory
            || isPackInventoryTransition || isPackChoiceResolving || isPackCloseResolving
            || cardPackChoiceOverlayCanvasRoot == null || !cardPackChoiceOverlayCanvasRoot.activeInHierarchy
            || mouse == null || OptionWindowManager.IsAnyOptionWindowOpen()
            || PresentationInputBlocker.IsBlocked || InteractionManager.IsExternallyBlocked
            || (TableTransitionManager.instance != null && TableTransitionManager.instance.IsTransitionInProgress)
            || (system != null && system.isActiveAndEnabled && system.currentInputModule != null
                && system.currentInputModule.isActiveAndEnabled))
        {
            pressedPackButton = null;
            return;
        }
        Vector2 pointer = mouse.position.ReadValue();
        Button hit = IsPackButtonAtPointer(cardPackChoiceSkipButton, pointer) ? cardPackChoiceSkipButton
            : IsPackButtonAtPointer(cardPackChoiceConfirmButton, pointer) ? cardPackChoiceConfirmButton : null;
        if (hit == null)
            foreach (PackItemUI item in activeCardPackChoiceItems)
                if (item != null && IsPackButtonAtPointer(item.selectButton, pointer)) { hit = item.selectButton; break; }
        if (mouse.leftButton.wasPressedThisFrame) pressedPackButton = hit;
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            Button clicked = pressedPackButton;
            pressedPackButton = null;
            if (clicked != null && clicked == hit) clicked.onClick.Invoke();
        }
    }

    private bool CanUseWorldPackInput()
    {
        return shopPackPresentation != null && shopPackPresentation.IsVisible
            && isCardPackChoiceMode && packChoiceIntroComplete && !IsBrowsingPackInventory
            && !isPackInventoryTransition && !isPackChoiceResolving && !isPackCloseResolving
            && !OptionWindowManager.IsAnyOptionWindowOpen() && !PresentationInputBlocker.IsBlocked
            && !InteractionManager.IsExternallyBlocked
            && (TableTransitionManager.instance == null || !TableTransitionManager.instance.IsTransitionInProgress);
    }

    private void UpdateWorldPackInput()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (!CanUseWorldPackInput() || mouse == null)
        {
            pressedWorldPackItem = null;
            pressedPackButton = null;
            shopPackPresentation?.SetHover(null);
            return;
        }
        Vector2 pointer = mouse.position.ReadValue();
        Button button = IsPackButtonAtPointer(cardPackChoiceSkipButton, pointer) ? cardPackChoiceSkipButton
            : IsPackButtonAtPointer(cardPackChoiceConfirmButton, pointer) ? cardPackChoiceConfirmButton : null;
        PackItemUI hit = button == null ? shopPackPresentation.HitTest(pointer) : null;
        shopPackPresentation.SetHover(hit);
        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressedWorldPackItem = hit;
            pressedPackButton = button;
        }
        if (!mouse.leftButton.wasReleasedThisFrame) return;
        PackItemUI picked = pressedWorldPackItem;
        Button clicked = pressedPackButton;
        pressedWorldPackItem = null;
        pressedPackButton = null;
        if (picked != null && picked == hit) SelectCardPackChoice(picked);
        var system = UnityEngine.EventSystems.EventSystem.current;
        bool hasUiInput = system != null && system.isActiveAndEnabled && system.currentInputModule != null
            && system.currentInputModule.isActiveAndEnabled;
        if (!hasUiInput && clicked != null && clicked == button) clicked.onClick.Invoke();
    }

    private static bool IsPackButtonAtPointer(Button button, Vector2 pointer)
    {
        return button != null && button.isActiveAndEnabled && button.IsInteractable()
            && RectTransformUtility.RectangleContainsScreenPoint(button.transform as RectTransform, pointer, null);
    }

    private void HidePackForInventory()
    {
        // Keep world poses visible during the outgoing pan; hide them at the side view.
        shopPackPresentation?.SetHover(null);
        inventoryBrowsePanelGroup = packOpeningPanel.GetComponent<CanvasGroup>();
        if (inventoryBrowsePanelGroup == null) inventoryBrowsePanelGroup = packOpeningPanel.AddComponent<CanvasGroup>();
        inventoryBrowsePanelAlpha = inventoryBrowsePanelGroup.alpha;
        inventoryBrowsePanelInteractable = inventoryBrowsePanelGroup.interactable;
        inventoryBrowsePanelBlocksRaycasts = inventoryBrowsePanelGroup.blocksRaycasts;
        inventoryBrowsePanelGroup.alpha = 0f;
        inventoryBrowsePanelGroup.interactable = false;
        inventoryBrowsePanelGroup.blocksRaycasts = false;
        // Items own nested canvases; disabling only the outer Canvas leaves them visible.
        inventoryBrowseOverlayRoot = cardPackChoiceOverlayCanvasRoot;
        if (inventoryBrowseOverlayRoot != null)
        {
            inventoryBrowseOverlayEnabled = inventoryBrowseOverlayRoot.activeSelf;
            inventoryBrowseOverlayRoot.SetActive(false);
        }
        pressedPackButton = null;
    }

    private void RestorePackInventoryVisibility(bool animate = false)
    {
        shopPackPresentation?.SetVisible(true);
        if (inventoryBrowsePanelGroup != null)
        {
            inventoryBrowsePanelGroup.alpha = inventoryBrowsePanelAlpha;
            inventoryBrowsePanelGroup.interactable = inventoryBrowsePanelInteractable;
            inventoryBrowsePanelGroup.blocksRaycasts = inventoryBrowsePanelBlocksRaycasts;
            inventoryBrowsePanelGroup = null;
        }
        if (inventoryBrowseOverlayRoot != null)
        {
            inventoryBrowseOverlayRoot.SetActive(inventoryBrowseOverlayEnabled);
            inventoryBrowseOverlayRoot = null;
            if (shopPackPresentation != null && cardPackChoiceRoot != null)
            {
                CanvasGroup group = EnsureCanvasGroup(cardPackChoiceRoot);
                group.DOKill();
                group.alpha = animate ? 0f : 1f;
                if (animate) group.DOFade(1f, ScalePresentationDuration(0.18f)).SetEase(Ease.OutSine).SetUpdate(true);
            }
        }
    }

    private void ReleaseGeneratedPackCards()
    {
        foreach (CardSO card in generatedPackCards)
            if (card != null && (GameManager.instance == null || !GameManager.instance.masterDeck.Contains(card)))
                Destroy(card);
        generatedPackCards.Clear();
    }

    private float NextPackValue() => activePackContentRandom != null ? activePackContentRandom.Value() : UnityEngine.Random.value;
    private int NextPackRange(int min, int max) => activePackContentRandom != null
        ? activePackContentRandom.Range(min, max) : UnityEngine.Random.Range(min, max);

    public void OpenPack(CardpackSO packData, Action onClosed = null)
        => OpenPackInternal(packData, null, 0f, onClosed);

    public void OpenPack(CardpackSO packData, Vector3 purchasePosition, float tableHeight, Action onClosed = null)
        => OpenPackInternal(packData, purchasePosition, tableHeight, onClosed);

    private void OpenPackInternal(CardpackSO packData, Vector3? purchasePosition, float tableHeight, Action onClosed)
    {
        if (packData == null)
        {
            onClosed?.Invoke();
            return;
        }

        isCardRemovalMode = false;
        isCardPackChoiceMode = useCardPackSkyPresentation;
        isRemovalResolving = false;
        isPackChoiceResolving = false;
        isPackCloseResolving = false;
        cardRemovalWasApplied = false;
        selectedCardPackChoiceItem = null;
        shopPackClosedCallback = null;
        if (purchasePosition.HasValue)
        {
            bool callbackInvoked = false;
            shopPackClosedCallback = () =>
            {
                if (callbackInvoked) return;
                callbackInvoked = true;
                onClosed?.Invoke();
            };
        }
        packClosedCallback = shopPackClosedCallback ?? onClosed;
        cardRemovalClosedCallback = null;
        activeCardPackChoiceItems.Clear();
        packOpeningPanel.SetActive(true);
        // 상점 밖 경로를 포함해 실제로 열린 주머니만 발견으로 기록합니다.
        CollectionDiscovery.RecordBag(packData);
        AcquireInputBlock();
        remainingPicks = packData.pickCount;

        UpdateInfoText();

        ClearSpawnedPackItems();
        shopPackPurchasePosition = purchasePosition;
        shopPackTableHeight = tableHeight;

        activePackContentRandom = MapManager.instance != null
            && MapManager.instance.TryGetCurrentNodeContentSeed(
                MapRandomStream.CardPackOpen,
                true,
                out ulong packContentSeed)
                ? new MapDeterministicRandom(packContentSeed)
                : null;
        List<PackCandidate> candidates;
        try
        {
            candidates = GenerateRandomPackCandidates(packData.packType, packData.generateCount);
        }
        finally
        {
            activePackContentRandom = null;
        }
        // 팩 후보를 준비한 직후 한 번만 처리합니다. 선택형 개봉도 이 지점을 공유하므로
        // 후보를 고르거나 팩을 닫을 때가 아니라 실제 주머니를 열 때 발동합니다.
        ConsumableManager consumableManager = ConsumableManager.Instance;
        hasPackAcquirePresentationSequence = consumableManager != null;
        packAcquirePresentationSequence = consumableManager != null
            ? consumableManager.CaptureAcquirePresentationSequence()
            : 0;
        ArtifactManager.Instance?.ProcessArtifactTrigger(ArtifactTriggerType.OnPackOpen);
        if (isCardPackChoiceMode)
        {
            SetPackOpeningPanelShellForCardRemoval(true);
            BuildCardPackChoiceLayout(packData, candidates);
            if (shopPackPresentation == null)
            {
                SoundManager.PlaySfx(data => data.Shop_CardPackOpen);
                TiltCameraForCardRemoval();
            }
            StartCoroutine(PlayCardPackChoiceIntroRoutine());
            return;
        }

        SoundManager.PlaySfx(data => data.Shop_CardPackOpen);
        GenerateRandomItems(candidates);
    }

    private void GenerateRandomItems(PackType type, int count)
    {
        GenerateRandomItems(GenerateRandomPackCandidates(type, count));
    }

    private void GenerateRandomItems(List<PackCandidate> candidates)
    {
        if (candidates == null)
            return;

        for (int i = 0; i < candidates.Count; i++)
        {
            PackCandidate candidate = candidates[i];
            if (candidate == null)
                continue;

            SpawnPackItem(
                candidate.Data,
                candidate.Type,
                candidate.Icon,
                candidate.ModelPrefab,
                candidate.ItemName,
                candidate.Description);
        }
    }

    private List<PackCandidate> GenerateRandomPackCandidates(PackType type, int count)
    {
        List<PackCandidate> candidates = new List<PackCandidate>();

        // UnlockManager의 데이터베이스에 접근할 수 있는지 확인
        if (UnlockManager.instance == null || UnlockManager.instance.itemDatabase == null)
        {
            Debug.LogError("[PackOpeningManager] UnlockManager 또는 itemDatabase가 없습니다!");
            return candidates;
        }

        switch (type)
        {
            case PackType.Artifact:
                List<ArtifactData> validArtifacts = new List<ArtifactData>();
                if (UnlockManager.instance != null)
                {
                    foreach (var a in UnlockManager.instance.unlockedArtifacts) 
                    {
                        if (a == null)
                            continue;

                        bool isOwned = ArtifactManager.Instance.HasArtifact(a.artifactID);

                        bool isInShop = ShopManager.instance != null && ShopManager.instance.IsArtifactInShop(a.artifactID);

                        // 둘 다 해당하지 않을 때만 팩 등장 목록(validArtifacts)에 추가!
                        if (!isOwned && !isInShop)
                        {
                            validArtifacts.Add(a);
                        }
                    }
                }
                for (int i = 0; i < count && validArtifacts.Count > 0; i++)
                {
                    ArtifactData artifact = ItemOfferRules.PickArtifact(validArtifacts, NextPackValue, NextPackRange);
                    if (artifact == null) break;
                    validArtifacts.Remove(artifact);
                    Sprite icon = artifact.artifactIcon;
                    GameObject modelPrefab = icon != null ? null : artifact.artifactModelPrefab;
                    AddPackCandidate(candidates, artifact, type, icon, modelPrefab, GameContent.Get(artifact, "artifactName", artifact.artifactName), GameContent.Get(artifact, "artifactDescription", artifact.artifactDescription));
                }
                break;

            case PackType.Planet:
                List<PlanetSO> allPlanets = new List<PlanetSO>();
                allPlanets.AddRange(Resources.LoadAll<PlanetSO>("Planets")); // Resources 폴더에 PlanetSO들이 있어야 합니다!

                ShuffleList(allPlanets);
                MoveGuaranteedPlanetToFront(allPlanets);
                for (int i = 0; i < Mathf.Min(count, allPlanets.Count); i++)
                {
                    string description = GameContent.Get(allPlanets[i], "amuletDescription", allPlanets[i].amuletDescription);
        
                    // 만약 예외 상황으로 인스펙터 설명 칸이 비어있다면, 꼬이지 않게 방어용 텍스트 백업 처리
                    if (string.IsNullOrWhiteSpace(description))
                    {
                        description = GameText.Get("game.PackOpeningManager.GenerateRandomPackCandidates.8e85b4447f", "[{value0}] 족보 레벨 업!", new { value0 = allPlanets[i].jokboType });
                    }

                    AddPackCandidate(candidates, allPlanets[i], type, allPlanets[i].planetSprite, null, GameContent.Get(allPlanets[i], "cardName", allPlanets[i].cardName), description);
                }
                break;

            case PackType.Amulet: // 부적(Amulet)
                List<AmuletSO> allAmulets = new List<AmuletSO>();
                allAmulets.AddRange(Resources.LoadAll<AmuletSO>("Amulets")); // Resources 폴더에 AmuletSO들이 있어야 합니다!

                ShuffleList(allAmulets);
                for (int i = 0; i < Mathf.Min(count, allAmulets.Count); i++)
                {
                    AddPackCandidate(candidates, allAmulets[i], type, allAmulets[i].amuletSprite, null, GameContent.Get(allAmulets[i], "cardName", allAmulets[i].cardName), GameContent.Get(allAmulets[i], "amuletDescription", allAmulets[i].amuletDescription));
                }
                break;

            case PackType.Normal:
                List<CardSO> allCards = new List<CardSO>();
                allCards.AddRange(Resources.LoadAll<CardSO>("Cards")); // Resources

                ShuffleList(allCards);
                for (int i = 0; i < Mathf.Min(count, allCards.Count); i++)
                {
                    CardSO candidate = Instantiate(allCards[i]);
                    generatedPackCards.Add(candidate);
                    float chanceMultiplier = VoucherManager.Instance != null
                        ? Mathf.Max(1, VoucherManager.Instance.GetTotalEffectValue(VoucherEffectType.EnhancedCardChanceMultiplier)) : 1;
                    ItemOfferRules.RollCardEnhancements(candidate, NextPackValue, NextPackRange, 0.5f * chanceMultiplier);
                    AddPackCandidate(candidates, candidate, type, candidate.normalSprite, null, GameContent.Get(candidate, "cardName", candidate.cardName), GameContent.Get(candidate, "cardDescription", candidate.cardDescription));
                }
                break;
        }

        return candidates;
    }

    private static void AddPackCandidate(
        List<PackCandidate> candidates,
        object data,
        PackType type,
        Sprite icon,
        GameObject modelPrefab,
        string itemName,
        string description)
    {
        if (candidates == null || data == null)
            return;

        candidates.Add(new PackCandidate
        {
            Data = data,
            Type = type,
            Icon = icon,
            ModelPrefab = modelPrefab,
            ItemName = itemName,
            Description = description
        });
    }

    private void MoveGuaranteedPlanetToFront(List<PlanetSO> planets)
    {
        if (planets == null
            || planets.Count == 0
            || VoucherManager.Instance == null
            || VoucherManager.Instance.GetTotalEffectValue(VoucherEffectType.GuaranteedPlanetForMostPlayedMonth) <= 0
            || !VoucherManager.Instance.TryGetMostPlayedJokbo(out JokBoType mostPlayedJokbo))
        {
            return;
        }

        for (int i = 0; i < planets.Count; i++)
        {
            PlanetSO planet = planets[i];
            if (planet == null || planet.jokboType != mostPlayedJokbo)
                continue;

            planets.RemoveAt(i);
            planets.Insert(0, planet);
            return;
        }
    }

    private PackItemUI SpawnPackItem(object data, PackType type, Sprite icon, GameObject modelPrefab, string itemName, string desc)
    {
        GameObject itemObj = Instantiate(packItemPrefab, cardSpawnParent);
        PackItemUI itemUI = itemObj.GetComponent<PackItemUI>();
        itemUI.Setup(data, type, icon, modelPrefab, itemName, desc);
        return itemUI;
    }

    // PackItemUI에서 클릭 시 호출됨
    public void OnItemPicked(object itemData, PackType type, GameObject clickedUIObject)
    {
        if (remainingPicks <= 0 || isRemovalResolving || isPackChoiceResolving) return;

        if (isCardPackChoiceMode && type != PackType.Removal)
        {
            SelectCardPackChoice(clickedUIObject != null ? clickedUIObject.GetComponent<PackItemUI>() : null);
            return;
        }

        if (isCardUpgradeMode && type == PackType.Removal && itemData is CardSO card)
        {
            SelectCardForUpgrade(card);
            return;
        }

        bool isSuccess = TryApplyPickedItem(itemData, type);
        if (!isSuccess) return;

        if (type == PackType.Removal && isCardRemovalMode)
        {
            remainingPicks--;
            cardRemovalWasApplied = true;
            UpdateCardRemovalInfoText();
            StartCoroutine(PlayCardRemovalSelectionRoutine(clickedUIObject));
            return;
        }

        // 2. 고른 UI는 화면에서 없애거나(Destroy), 클릭 못 하게 비활성화
        Destroy(clickedUIObject);

        // 3. 횟수 차감 및 텍스트 갱신
        remainingPicks--;
        UpdateInfoText();

        // 4. 고를 수 있는 횟수를 다 썼다면 팩 개봉 종료
        if (remainingPicks <= 0)
        {
            Invoke(nameof(ClosePackAfterSelection), 0.5f); // 선택한 거 볼 수 있게 0.5초 뒤에 닫기
        }
    }

    private bool TryApplyPickedItem(object itemData, PackType type)
    {
        bool isSuccess = false;

        switch (type)
        {
            case PackType.Artifact:
                ArtifactData artifact = (ArtifactData)itemData;
                isSuccess = ArtifactManager.Instance != null
                    && ArtifactManager.Instance.TryAddArtifact(artifact);
                break;

            case PackType.Planet:
                PlanetSO planet = (PlanetSO)itemData;
                if (PlanetCardLevelManager.Instance != null)
                {
                    // 올려주신 PlanetCardLevelManager의 함수로 연결!
                    PlanetCardLevelManager.Instance.UpgradeJokboLevel(planet); 
                    UnlockManager.RecordItemAcquired(planet);
                    isSuccess = true;
                }
                break;
                
            case PackType.Amulet: // Amulet
                AmuletSO amulet = (AmuletSO)itemData;
                if (ConsumableManager.Instance != null)
                {
                    isSuccess = ConsumableManager.Instance.TryAddConsumable(amulet);
                }
                break;
                
            case PackType.Normal:
                CardSO card = (CardSO)itemData;
                if (CombatManager.instance != null)
                {
                    GameManager.instance.masterDeck.Add(card);
                    isSuccess = true;
                }
                break;
            
            case PackType.Removal:
                isSuccess = TryRemoveCardForPresentation(itemData as BaseCardSO);
                SoundManager.PlaySfx(data => isSuccess ? data.Remove_Burn : data.Common_Fail);
                break;
        }

        if (!isSuccess && (type == PackType.Artifact || type == PackType.Amulet))
            ShowPackAcquisitionFailure(type, itemData);
        return isSuccess;
    }

    private static string GetPackCapacityWarning(PackType type, object itemData = null)
    {
        if (type == PackType.Artifact)
        {
            ArtifactManager manager = ArtifactManager.Instance;
            ArtifactInventory inventory = manager != null ? manager.inventory : null;
            bool hasSpace = itemData is ArtifactData artifact
                ? manager != null && manager.CanAddArtifact(artifact)
                : manager != null && manager.HasSpaceForAnyArtifact();
            if (inventory?.items != null && !hasSpace)
                return GameText.Get("game.PackOpeningManager.GetPackCapacityWarning.8bcee3b6e6", "기물 슬롯이 가득 찼습니다. ({value0}/{value1})", new { value0 = inventory.items.Count, value1 = manager.GetCurrentArtifactCapacity() });
        }
        else if (type == PackType.Amulet && ConsumableManager.Instance != null)
        {
            ConsumableManager manager = ConsumableManager.Instance;
            int count = 0;
            if (manager.inventory != null)
            {
                foreach (AmuletSO amulet in manager.inventory)
                    if (amulet != null) count++;
            }
            int capacity = manager.CurrentConsumableCapacity;
            if (count >= capacity)
                return GameText.Get("game.PackOpeningManager.GetPackCapacityWarning.d922e2eab0", "소모품 슬롯이 가득 찼습니다. ({value0}/{value1})", new { value0 = count, value1 = capacity });
        }
        return null;
    }

    private void ShowPackAcquisitionFailure(PackType type, object itemData = null)
    {
        string reason = GetPackCapacityWarning(type, itemData);
        string message = !string.IsNullOrEmpty(reason)
            ? GameText.Get("game.PackOpeningManager.ShowPackAcquisitionFailure.71284a9841", "<color=#FFB090>획득 실패: {value0}</color>\n빈 슬롯이 1칸 필요합니다. A/D로 보유 기물/부적을 확인하고 판매할 수 있습니다.\n선택 횟수는 유지됩니다. 건너뛰면 남은 보상은 포기합니다.", new { value0 = reason })
            : GameText.Get("game.PackOpeningManager.ShowPackAcquisitionFailure.86db3e7239", "<color=#FFB090>획득 실패 — 획득 처리에 필요한 데이터나 오브젝트를 준비하지 못했습니다.</color>\n선택 횟수는 차감되지 않았습니다.");
        if (shopPackPresentation != null && isCardPackChoiceMode)
            UpdateWorldPackChoiceHud(selectedCardPackChoiceItem, string.IsNullOrEmpty(reason)
                ? GameText.Get("game.PackOpeningManager.ShowPackAcquisitionFailure.a174924bad", "획득하지 못했습니다. 선택 횟수는 유지됩니다.")
                : GameText.Get("game.PackOpeningManager.ShowPackAcquisitionFailure.b57a5fa74a", "{value0} 선택 횟수는 유지됩니다.", new { value0 = reason }));
        else if (isCardPackChoiceMode && cardPackChoiceDetailText != null)
            cardPackChoiceDetailText.text = message;
        else if (infoText != null)
            infoText.text = message;
        SoundManager.PlaySfx(data => data.Common_Fail);
    }

    /// <summary>
    /// 3D 패 제거 연출에서 선택한 카드를 기존 제거 시스템과 동일하게 삭제합니다.
    /// </summary>
    public bool TryRemoveCardForPresentation(BaseCardSO cardToRemove)
    {
        if (cardToRemove == null
            || GameManager.instance == null
            || GameManager.instance.masterDeck == null
            || !GameManager.instance.masterDeck.Contains(cardToRemove))
        {
            return false;
        }

        if (!GameManager.instance.masterDeck.Remove(cardToRemove))
            return false;

        UnlockManager.instance?.RecordCardsDestroyed(1);
        GameLog.Info($"[제거 시스템] 덱에서 '{cardToRemove.name}' 삭제됨.");

        if (CardManager_2.Instance != null)
            CardManager_2.Instance.SetupCardBuffer();

        return true;
    }

    private void BuildCardPackChoiceLayout(CardpackSO packData, List<PackCandidate> candidates)
    {
        ClearCardPackChoiceRoot();
        packChoiceIntroComplete = false;
        if (shopPackPurchasePosition.HasValue)
            shopPackPresentation = ShopPackWorldPresentation.TryCreate(this, packData,
                shopPackPurchasePosition.Value, shopPackTableHeight, candidates != null ? candidates.Count : 0, BuildCardPackChoiceDetailText);

        cardPackChoiceOverlayCanvasRoot = CreateOverlayCanvasRoot(
            CardPackChoiceOverlayCanvasName,
            CardPackChoiceOverlaySortingOrder);

        cardPackChoiceRoot = CreateRectObject(CardPackChoiceRootName, cardPackChoiceOverlayCanvasRoot.transform);
        RectTransform rootRect = cardPackChoiceRoot.transform as RectTransform;
        StretchToParent(rootRect, new Vector2(150f, 115f), new Vector2(150f, 95f));

        CanvasGroup rootGroup = cardPackChoiceRoot.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.interactable = true;
        rootGroup.blocksRaycasts = true;

        if (shopPackPresentation != null)
        {
            BuildWorldPackHud(packData, candidates);
            return;
        }

        CreateRemovalSkyStars(cardPackChoiceRoot.transform);

        GameObject content = CreateRectObject("Content", cardPackChoiceRoot.transform);
        RectTransform contentRect = content.transform as RectTransform;
        StretchToParent(contentRect, new Vector2(42f, 34f), new Vector2(42f, 34f));
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 18f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        string packName = ResolveCardPackChoiceDisplayName(packData);
        cardPackChoiceTitleText = CreateText(
            content.transform,
            "Title",
            GameText.Get("game.PackOpeningManager.BuildCardPackChoiceLayout.32f0cdb683", "{value0}에서 하나를 고르세요", new { value0 = packName }),
            42f,
            Color.white,
            TextAlignmentOptions.Center);
        EnsureLayoutElement(cardPackChoiceTitleText.gameObject, new Vector2(900f, 72f));

        GameObject choicesPanel = CreateRectObject("ChoicesPanel", content.transform);
        Image choicesImage = choicesPanel.AddComponent<Image>();
        choicesImage.color = cardPackPanelTint;
        choicesImage.raycastTarget = false;
        Outline choicesOutline = choicesPanel.AddComponent<Outline>();
        choicesOutline.effectColor = new Color(cardPackAccentColor.r, cardPackAccentColor.g, cardPackAccentColor.b, 0.55f);
        choicesOutline.effectDistance = new Vector2(2f, -2f);
        LayoutElement choicesLayoutElement = EnsureLayoutElement(choicesPanel, new Vector2(1100f, 360f));
        choicesLayoutElement.flexibleWidth = 1f;

        HorizontalLayoutGroup choicesLayout = choicesPanel.AddComponent<HorizontalLayoutGroup>();
        choicesLayout.spacing = 24f;
        choicesLayout.padding = new RectOffset(28, 28, 32, 28);
        choicesLayout.childAlignment = TextAnchor.MiddleCenter;
        choicesLayout.childControlWidth = true;
        choicesLayout.childControlHeight = true;
        choicesLayout.childForceExpandWidth = false;
        choicesLayout.childForceExpandHeight = false;

        activeCardPackChoiceItems.Clear();
        if (candidates != null)
        {
            for (int i = 0; i < candidates.Count; i++)
                SpawnCardPackChoiceItem(candidates[i], choicesPanel.transform);
        }

        cardPackChoiceDetailText = CreateText(
            content.transform,
            "ChoiceDetail",
            GameText.Get("game.PackOpeningManager.BuildCardPackChoiceLayout.6e8cf440cd", "후보를 선택하면 이름과 효과가 표시됩니다."),
            23f,
            new Color(1f, 0.9f, 0.7f, 0.96f),
            TextAlignmentOptions.Center);
        cardPackChoiceDetailText.lineSpacing = 10f;
        EnsureLayoutElement(cardPackChoiceDetailText.gameObject, new Vector2(940f, 136f));
        string capacityWarning = GetPackCapacityWarning(packData.packType);
        if (!string.IsNullOrEmpty(capacityWarning))
            cardPackChoiceDetailText.text = GameText.Get("game.PackOpeningManager.BuildCardPackChoiceLayout.0476080304", "<color=#FFB090>{value0}</color>\n빈 슬롯이 없어 현재 보상을 획득할 수 없습니다.", new { value0 = capacityWarning });

        cardPackChoiceConfirmButton = CreateCardPackConfirmButton(content.transform);
        cardPackChoiceConfirmButton.interactable = false;
        cardPackChoiceSkipButton = CreateCardPackSkipButton(content.transform);

        if (activeCardPackChoiceItems.Count == 0)
        {
            cardPackChoiceDetailText.text = GameText.Get("game.PackOpeningManager.BuildCardPackChoiceLayout.0455e1048b", "선택 가능한 후보가 없습니다.");
            cardPackChoiceConfirmButton.interactable = true;
            TextMeshProUGUI buttonText = cardPackChoiceConfirmButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (buttonText != null)
                buttonText.text = GameText.Get("game.PackOpeningManager.BuildCardPackChoiceLayout.1e8c10206f", "닫기");
        }
    }

    private void BuildWorldPackHud(CardpackSO packData, List<PackCandidate> candidates)
    {
        RectTransform rootRect = (RectTransform)cardPackChoiceRoot.transform;
        StretchToParent(rootRect, Vector2.zero, Vector2.zero);
        GameObject focus = CreateRectObject("PackFocusVignette", rootRect);
        StretchToParent((RectTransform)focus.transform, Vector2.zero, Vector2.zero);
        Image focusImage = focus.AddComponent<Image>();
        focusImage.material = shopPackPresentation.FocusMaterial;
        focusImage.raycastTarget = false;
        shopPackPresentation.CreateSelectionOverlay(rootRect);
        GameObject typefaces = Resources.Load<GameObject>("UI/SettingsBook/SettingsBookTypefaces");
        TMP_Text sample = typefaces != null ? typefaces.transform.Find("BodyFont")?.GetComponent<TMP_Text>() : null;
        TMP_FontAsset font = sample != null ? sample.font : infoText != null ? infoText.font : null;
        Sprite paperPlate = Resources.Load<Sprite>("ShopPack/UI/pack_action_hanji_v1");
        Sprite counterSeal = Resources.Load<Sprite>("ShopPack/UI/pack_choice_seal_v1");
        Image titleInk = CreateWorldPackInkPlate(rootRect, "TitlePaper", new Vector2(0.5f, 0.9f), new Vector2(640f, 100f), paperPlate);
        ConfigureWorldPackPaper(titleInk);
        Image detailInk = CreateWorldPackInkPlate(rootRect, "ChoiceDetailPaper", new Vector2(0.5f, 0.217f), new Vector2(600f, 126f), paperPlate);
        ConfigureWorldPackPaper(detailInk);
        cardPackChoiceTitleText = CreateText(rootRect, "Title", ResolveCardPackChoiceDisplayName(packData),
            48f, new Color(0.07f, 0.055f, 0.04f), TextAlignmentOptions.Center);
        PlaceWorldPackHud(cardPackChoiceTitleText.rectTransform, new Vector2(0.5f, 0.9f), new Vector2(584f, 76f));
        worldPackChoiceCaptionText = CreateText(rootRect, "ChoiceCaption", "", 26f,
            new Color(0.52f, 0.16f, 0.09f), TextAlignmentOptions.Center);
        PlaceWorldPackHud(worldPackChoiceCaptionText.rectTransform, new Vector2(0.5f, 0.245f), new Vector2(520f, 40f));
        cardPackChoiceDetailText = CreateText(rootRect, "ChoiceName", "", 42f,
            new Color(0.07f, 0.055f, 0.04f), TextAlignmentOptions.Center);
        PlaceWorldPackHud(cardPackChoiceDetailText.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(544f, 60f));
        cardPackChoiceDetailText.enableAutoSizing = true;
        cardPackChoiceDetailText.fontSizeMin = 28f;
        cardPackChoiceDetailText.fontSizeMax = 42f;
        worldPackWarningText = CreateText(rootRect, "ChoiceWarning", "", 21f,
            new Color(1f, 0.69f, 0.56f), TextAlignmentOptions.Center);
        PlaceWorldPackHud(worldPackWarningText.rectTransform, new Vector2(0.5f, 0.16f), new Vector2(920f, 34f));
        worldPackWarningText.enableAutoSizing = true;
        worldPackWarningText.fontSizeMin = 18f;
        worldPackWarningText.fontSizeMax = 21f;
        GameObject countRoot = CreateRectObject("RemainingChoices", rootRect);
        PlaceWorldPackHud((RectTransform)countRoot.transform, Vector2.one, new Vector2(176f, 186f));
        ((RectTransform)countRoot.transform).anchoredPosition = new Vector2(-124f, -124f);
        TextMeshProUGUI countCaption = CreateText(countRoot.transform, "Caption", GameText.Get("game.PackOpeningManager.BuildWorldPackHud.239761b29d", "남은 선택"), 28f,
            new Color(0.96f, 0.9f, 0.76f), TextAlignmentOptions.Center);
        PlaceWorldPackHud(countCaption.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(174f, 38f));
        Image seal = CreateWorldPackInkPlate(countRoot.transform, "CinnabarSeal", new Vector2(0.5f, 0.38f), new Vector2(116f, 116f), counterSeal);
        seal.type = Image.Type.Simple;
        seal.preserveAspect = true;
        seal.color = new Color(0.65f, 0.65f, 0.65f);
        worldPackRemainingText = CreateText(countRoot.transform, "Count", "", 54f,
            new Color(0.99f, 0.92f, 0.77f), TextAlignmentOptions.Center);
        PlaceWorldPackHud(worldPackRemainingText.rectTransform, new Vector2(0.5f, 0.38f), new Vector2(102f, 102f));
        foreach (TMP_Text label in new TMP_Text[] { cardPackChoiceTitleText, worldPackChoiceCaptionText,
            cardPackChoiceDetailText, worldPackWarningText, countCaption, worldPackRemainingText })
        {
            if (font != null) label.font = font;
            label.fontStyle = FontStyles.Normal;
            label.fontWeight = FontWeight.Regular;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }
        Transform proxies = CreateRectObject("WorldChoiceData", rootRect).transform;
        if (candidates != null)
            foreach (PackCandidate candidate in candidates) SpawnCardPackChoiceItem(candidate, proxies);
        cardPackChoiceConfirmButton = CreateWorldPackButton(rootRect, GameText.Get("game.PackOpeningManager.BuildWorldPackHud.8185d1403a", "가져가기"), new Vector2(0.4f, 0.095f), ConfirmSelectedCardPackChoice, font, paperPlate);
        cardPackChoiceSkipButton = CreateWorldPackButton(rootRect, GameText.Get("game.PackOpeningManager.BuildWorldPackHud.d869d38098", "주머니 닫기"), new Vector2(0.6f, 0.095f), SkipCardPackChoice, font, paperPlate);
        if (shopPackPresentation.ActionSeal != null)
        {
            Image actionSeal = CreateWorldPackInkPlate(cardPackChoiceConfirmButton.transform, "ActionSeal", new Vector2(0.8f, 0.5f), new Vector2(22f, 34f), shopPackPresentation.ActionSeal);
            actionSeal.type = Image.Type.Simple;
            actionSeal.preserveAspect = true;
            actionSeal.color = new Color(0.75f, 0.7f, 0.65f);
        }
        BuildWorldPackHover(rootRect, font, paperPlate);
        cardPackChoiceConfirmButton.interactable = false;
        cardPackChoiceSkipButton.interactable = false;
        UpdateWorldPackChoiceHud(null, GetPackCapacityWarning(packData.packType));
        if (activeCardPackChoiceItems.Count == 0)
        {
            worldPackChoiceCaptionText.text = GameText.Get("game.PackOpeningManager.BuildWorldPackHud.4482e9eb71", "선택 가능한 물건이 없습니다.");
            cardPackChoiceDetailText.text = "";
            cardPackChoiceConfirmButton.GetComponentInChildren<TextMeshProUGUI>(true).text = GameText.Get("game.PackOpeningManager.BuildWorldPackHud.1e8c10206f", "닫기");
        }
        CenterWorldPackButtonLabel(cardPackChoiceConfirmButton);
        CenterWorldPackButtonLabel(cardPackChoiceSkipButton);
        rootRect.GetComponent<CanvasGroup>().alpha = 1f;
    }

    private void UpdateWorldPackChoiceHud(PackItemUI item, string warning = null)
    {
        if (worldPackRemainingText != null) worldPackRemainingText.text = Mathf.Max(0, remainingPicks).ToString();
        if (worldPackChoiceCaptionText != null)
            worldPackChoiceCaptionText.text = item == null ? GameText.Get("game.PackOpeningManager.UpdateWorldPackChoiceHud.be711eddf6", "원하는 물건을 고르세요") : item.PackType switch
            {
                PackType.Artifact => GameText.Get("game.PackOpeningManager.UpdateWorldPackChoiceHud.e998e4674f", "선택한 기물"),
                PackType.Planet => GameText.Get("game.PackOpeningManager.UpdateWorldPackChoiceHud.45faece18e", "선택한 신장패"),
                PackType.Amulet => GameText.Get("game.PackOpeningManager.UpdateWorldPackChoiceHud.ac7844fa8d", "선택한 부적"),
                PackType.Normal => GameText.Get("game.PackOpeningManager.UpdateWorldPackChoiceHud.36859edf4b", "선택한 패"),
                _ => GameText.Get("game.PackOpeningManager.UpdateWorldPackChoiceHud.21d8b8b897", "선택한 물건")
            };
        if (cardPackChoiceDetailText != null)
        {
            int count = selectedWorldPackChoices.Count;
            var names = new StringBuilder();
            foreach (PackItemUI selected in selectedWorldPackChoices)
            {
                if (names.Length > 0) names.Append(" / ");
                names.Append(selected.DisplayName);
            }
            cardPackChoiceDetailText.text = count > 0 ? names.ToString() : GameText.Get("game.PackOpeningManager.UpdateWorldPackChoiceHud.dd1e804141", "선택 대기");
            cardPackChoiceDetailText.fontSizeMin = count > 1 ? 22f : 28f;
            cardPackChoiceDetailText.fontSizeMax = count > 1 ? 34f : 42f;
            cardPackChoiceDetailText.overflowMode = TextOverflowModes.Ellipsis;
            if (count > 1 && worldPackChoiceCaptionText != null)
                worldPackChoiceCaptionText.text += GameText.Get("game.PackOpeningManager.UpdateWorldPackChoiceHud.52707a47c4", " / {value0}개", new { value0 = count });
        }
        if (worldPackWarningText != null) worldPackWarningText.text = warning ?? "";
    }

    private Image CreateWorldPackInkPlate(Transform parent, string name, Vector2 anchor, Vector2 size, Sprite sprite)
    {
        GameObject plate = CreateRectObject(name, parent);
        PlaceWorldPackHud((RectTransform)plate.transform, anchor, size);
        Image image = plate.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private void ConfigureWorldPackPaper(Image image)
    {
        // Keep the entire corner in its fixed slice; the shader samples grain at a uniform scale.
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 4f;
        image.color = new Color(0.88f, 0.85f, 0.74f, 1f);
        image.material = shopPackPresentation.CreatePaperMaterial(image.sprite);
        RefreshWorldPackPaperLayout(image);
        Shadow shadow = image.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.012f, 0.018f, 0.028f, 0.42f);
        shadow.effectDistance = new Vector2(3f, -5f);
        shadow.useGraphicAlpha = true;
    }

    private static void RefreshWorldPackPaperLayout(Image image)
    {
        Rect rect = image.rectTransform.rect;
        image.material.SetVector("_PaperRect", new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
        Vector4 border = image.sprite != null ? image.sprite.border / (image.pixelsPerUnit * image.pixelsPerUnitMultiplier) : Vector4.zero;
        image.material.SetVector("_PaperBorder", border);
        if (image.sprite != null)
        {
            image.material.SetVector("_PaperOuterUV", UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite));
            image.material.SetVector("_PaperInnerUV", UnityEngine.Sprites.DataUtility.GetInnerUV(image.sprite));
        }
    }

    private Button CreateWorldPackButton(Transform parent, string label, Vector2 anchor,
        UnityEngine.Events.UnityAction clicked, TMP_FontAsset font, Sprite sprite)
    {
        GameObject obj = CreateRectObject("PackAction_" + label, parent);
        PlaceWorldPackHud((RectTransform)obj.transform, anchor, new Vector2(320f, 92f));
        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        ConfigureWorldPackPaper(image);
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.57f, 0.16f, 0.1f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.48f, 0.1f, 0.06f, 1f);
        colors.disabledColor = new Color(0.72f, 0.72f, 0.72f, 0.85f);
        colors.fadeDuration = 0f;
        button.colors = colors;
        button.onClick.AddListener(clicked);
        TextMeshProUGUI text = CreateText(obj.transform, "Label", label, 36f,
            new Color(0.07f, 0.055f, 0.04f), TextAlignmentOptions.Center);
        if (font != null) text.font = font;
        text.fontStyle = FontStyles.Normal;
        text.fontWeight = FontWeight.Regular;
        text.raycastTarget = false;
        StretchToParent(text.rectTransform, new Vector2(20f, 10f), new Vector2(20f, 10f));
        return button;
    }

    private static void PlaceWorldPackHud(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void CenterWorldPackButtonLabel(Button button)
    {
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        label.ForceMeshUpdate(true);
        // Align the painted glyphs, including the brush font's uneven vertical bearings.
        label.rectTransform.anchoredPosition = -(Vector2)label.textBounds.center;
    }

    private void BuildWorldPackHover(RectTransform parent, TMP_FontAsset font, Sprite paper)
    {
        Image background = CreateWorldPackInkPlate(parent, "PackHoverPaper", new Vector2(0.5f, 0.5f),
            new Vector2(520f, 280f), paper);
        ConfigureWorldPackPaper(background);
        worldPackHoverRoot = background.rectTransform;
        worldPackHoverTitleText = CreateText(worldPackHoverRoot, "Name", "", 30f,
            new Color(0.07f, 0.055f, 0.04f), TextAlignmentOptions.Left);
        worldPackHoverTitleText.rectTransform.anchorMin = worldPackHoverTitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
        worldPackHoverTitleText.rectTransform.pivot = new Vector2(0f, 1f);
        worldPackHoverTitleText.rectTransform.anchoredPosition = new Vector2(42f, -28f);
        worldPackHoverTitleText.enableAutoSizing = true;
        worldPackHoverTitleText.fontSizeMin = 22f;
        worldPackHoverTitleText.fontSizeMax = 30f;
        worldPackHoverTitleText.textWrappingMode = TextWrappingModes.NoWrap;
        worldPackHoverBodyText = CreateText(worldPackHoverRoot, "Effect", "", 23f,
            new Color(0.08f, 0.07f, 0.055f), TextAlignmentOptions.TopLeft);
        StretchToParent(worldPackHoverBodyText.rectTransform, new Vector2(42f, 30f), new Vector2(42f, 76f));
        // Measure at the displayed size instead of shrinking/ellipsizing an undersized plate.
        worldPackHoverBodyText.enableAutoSizing = false;
        worldPackHoverBodyText.fontSizeMin = 18f;
        worldPackHoverBodyText.fontSizeMax = 23f;
        worldPackHoverBodyText.paragraphSpacing = 3f;
        worldPackHoverBodyText.textWrappingMode = TextWrappingModes.Normal;
        foreach (TMP_Text text in new TMP_Text[] { worldPackHoverTitleText, worldPackHoverBodyText })
        {
            if (font != null) text.font = font;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            // Keep the final paper font when localization reapplies styling on enable.
            GameText.Style(text);
        }
        worldPackHoverTagsRoot = new GameObject("InfoTags", typeof(RectTransform)).GetComponent<RectTransform>();
        worldPackHoverTagsRoot.SetParent(worldPackHoverRoot, false);
        worldPackHoverTagsRoot.anchorMin = worldPackHoverTagsRoot.anchorMax = new Vector2(0f, 1f);
        worldPackHoverTagsRoot.pivot = Vector2.zero;
        worldPackHoverTagsRoot.anchoredPosition = new Vector2(0f, 8f);
        Sprite stamp = RuntimeItemInfoTagPresenter.GetAlignedStamp();
        for (int i = 0; i < WorldPackHoverTagCapacity; i++)
        {
            // The entire badge is backed; draw the stamp above it so none of its edges
            // are hidden by an inset child or by the scene behind the transparent artwork.
            Image backing = CreateWorldPackInkPlate(worldPackHoverTagsRoot, $"Tag{i}", Vector2.zero,
                new Vector2(130f, 48f), null);
            backing.rectTransform.pivot = Vector2.zero;
            backing.color = new Color(0.008f, 0.01f, 0.012f, 1f);
            Image frame = CreateWorldPackInkPlate(backing.transform, "StampFrame", Vector2.zero, Vector2.zero, stamp);
            StretchToParent(frame.rectTransform, Vector2.zero, Vector2.zero);
            frame.type = Image.Type.Sliced;
            // Source PPU is 400, so Image.pixelsPerUnit is already 4 at the HUD's
            // reference PPU 100. Keep four source pixels per UI unit, not sixteen.
            frame.pixelsPerUnitMultiplier = 4f / Mathf.Max(0.01f, frame.pixelsPerUnit);
            TextMeshProUGUI label = CreateText(backing.transform, "Text", "", 24f,
                new Color(1f, 0.94f, 0.8f), TextAlignmentOptions.Center);
            StretchToParent(label.rectTransform, new Vector2(14f, 7f), new Vector2(14f, 7f));
            if (font != null) label.font = font;
            label.fontStyle = FontStyles.Normal;
            label.fontWeight = FontWeight.Regular;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            GameText.Style(label);
            worldPackHoverTags.Add(new RuntimeItemInfoTag(backing.rectTransform, label));
            backing.gameObject.SetActive(false);
        }
        worldPackHoverRoot.gameObject.SetActive(false);
    }

    private void ConfigureWorldPackHoverTags(ArtifactData artifact)
    {
        int index = 0;
        RuntimeItemInfoTag edition = worldPackHoverTags[index];
        if (ArtifactEditionUiPresentation.Apply(edition.Rect.gameObject, null, edition.Text, artifact.currentEdition))
        {
            // The inventory's edition labels/palette on a dark, horizontal stamp.
            edition.Text.color = Color.Lerp(edition.Text.color, Color.white, 0.8f);
            index++;
        }
        foreach (ArtifactSynergyType type in ArtifactTooltipSynergyUtility.BuildSynergyTypes(artifact))
        {
            if (index >= worldPackHoverTags.Count) break;
            RuntimeItemInfoTag tag = worldPackHoverTags[index++];
            tag.Rect.gameObject.SetActive(true);
            tag.Text.text = ArtifactSynergyMetadata.GetDisplayName(type);
            tag.Text.color = new Color(1f, 0.94f, 0.8f);
        }
    }

    private void ConfigureWorldPackHoverTags(CardTooltipData data, EditionType edition)
    {
        int index = 0;
        foreach (CardTooltipEnhancementData effect in data.enhancements)
        {
            if (index >= worldPackHoverTags.Count) break;
            RuntimeItemInfoTag tag = worldPackHoverTags[index++];
            bool editionTag = ArtifactEditionUiPresentation.Apply(tag.Rect.gameObject, null, tag.Text, edition)
                && tag.Text.text == effect.displayName;
            tag.Rect.gameObject.SetActive(true);
            tag.Text.color = editionTag ? Color.Lerp(tag.Text.color, Color.white, 0.8f) : new Color(1f, 0.94f, 0.8f);
            // Keep the actual enhancement effect in its badge; the paper body retains
            // the base score/joker explanation without duplicating an unframed list.
            tag.Text.text = string.IsNullOrWhiteSpace(effect.description) ? effect.displayName
                : $"{effect.displayName} <size=80%>{effect.description}</size>";
        }
    }

    private float LayoutWorldPackHoverTags(float width, float pixelScale = 1f)
    {
        pixelScale = Mathf.Max(0.01f, pixelScale);
        float gap = Mathf.Ceil(10f * pixelScale) / pixelScale;
        float maxWidth = Mathf.Floor(Mathf.Min(width, 280f) * pixelScale) / pixelScale;
        float x = 0f, y = 0f, rowHeight = 0f;
        foreach (RuntimeItemInfoTag tag in worldPackHoverTags)
        {
            if (!tag.Rect.gameObject.activeSelf) continue;
            float preferredWidth = Mathf.Max(112f, tag.Text.GetPreferredValues(tag.Text.text, Mathf.Infinity, Mathf.Infinity).x + 28f);
            float tagWidth = Mathf.Min(maxWidth, Mathf.Ceil(preferredWidth * pixelScale) / pixelScale);
            float preferredHeight = Mathf.Max(48f,
                tag.Text.GetPreferredValues(tag.Text.text, tagWidth - 28f, Mathf.Infinity).y + 14f);
            float tagHeight = Mathf.Ceil(preferredHeight * pixelScale) / pixelScale;
            if (x > 0f && x + tagWidth > width) { x = 0f; y += rowHeight + gap; rowHeight = 0f; }
            tag.Rect.anchoredPosition = new Vector2(x, y);
            tag.Rect.sizeDelta = new Vector2(tagWidth, tagHeight);
            x += tagWidth + gap;
            rowHeight = Mathf.Max(rowHeight, tagHeight);
        }
        float totalHeight = y + rowHeight;
        worldPackHoverTagsRoot.sizeDelta = new Vector2(width, totalHeight);
        return totalHeight;
    }

    private static Vector2 SnapWorldPackHoverTopLeft(RectTransform page, Rect placement)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null,
            page.TransformPoint(new Vector3(placement.xMin, placement.yMax, 0f)));
        screen = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(page, screen, null, out Vector2 local);
        return local;
    }

    private void RefreshWorldPackHover()
    {
        if (worldPackHoverRoot == null) return;
        PackItemUI item = CanUseWorldPackInput() ? shopPackPresentation.HoveredItem : null;
        if (item == null || !shopPackPresentation.TryGetHoverScreenBounds(out Rect anchor, out _))
        {
            worldPackHoverRoot.gameObject.SetActive(false);
            worldPackHoveredItem = null;
            return;
        }
        if (worldPackHoveredItem != item)
        {
            worldPackHoveredItem = item;
            worldPackHoverPlacement = default;
            worldPackHoverTitleText.text = item.DisplayName;
            worldPackHoverTitleText.fontSize = 30f;
            worldPackHoverBodyText.fontSize = 23f;
            foreach (RuntimeItemInfoTag tag in worldPackHoverTags) tag.Rect.gameObject.SetActive(false);
            worldPackHoverBodyText.text = AdaptWorldPackPaperText(BuildWorldPackHoverBody(item)).Replace("\n\n", "\n");
        }
        RectTransform page = (RectTransform)cardPackChoiceRoot.transform;
        Canvas canvas = page.GetComponentInParent<Canvas>();
        float pixelScale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        float tagGap = Mathf.Ceil(8f * pixelScale) / pixelScale;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(page, anchor.center, null, out Vector2 anchorCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(page, anchor.min, null, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(page, anchor.max, null, out Vector2 max);
        Rect bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        bool needsLayout = worldPackHoverPlacement.width <= 0f
            || worldPackHoverPageSize != page.rect.size || !Mathf.Approximately(worldPackHoverPixelScale, pixelScale);
        float tagsHeight = worldPackHoverTagsRoot.rect.height;
        if (needsLayout)
        {
            // The paper keeps the same readable width and font size for every candidate.
            // Avoiding a taller neighbour must not widen the paper or shrink its contents.
            float width = Mathf.Min(520f, page.rect.width - 64f);
            tagsHeight = LayoutWorldPackHoverTags(width, pixelScale);
            float preferredHeight = Mathf.Max(170f, 106f + worldPackHoverBodyText.GetPreferredValues(
                worldPackHoverBodyText.text, width - 84f, Mathf.Infinity).y)
                + (tagsHeight > 0f ? tagsHeight + tagGap : 0f);
            Rect clearanceBounds = Rect.MinMaxRect(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax + 12f);
            if (!TryPlaceWorldPackHover(page.rect, clearanceBounds, anchorCenter.x, preferredHeight,
                out worldPackHoverPlacement, width))
            {
                worldPackHoverRoot.gameObject.SetActive(false);
                return;
            }
            worldPackHoverPageSize = page.rect.size;
            worldPackHoverPixelScale = pixelScale;
        }
        else if (TryPlaceWorldPackHover(page.rect, bounds, anchorCenter.x, worldPackHoverPlacement.height,
            out Rect floatedPlacement, worldPackHoverPlacement.width))
        {
            // Reserve headroom on first placement, then move only when the item exceeds it.
            worldPackHoverPlacement.y = Mathf.Max(worldPackHoverPlacement.y, floatedPlacement.y);
        }
        Rect placement = worldPackHoverPlacement;
        // Place the complete envelope (paper + external tags), then size the paper alone.
        float tagsEnvelope = tagsHeight > 0f ? tagsHeight + tagGap : 0f;
        float paperHeight = placement.height - tagsEnvelope;
        Vector2 topLeft = SnapWorldPackHoverTopLeft(page, placement);
        worldPackHoverRoot.anchoredPosition = new Vector2(topLeft.x + placement.width * 0.5f,
            topLeft.y - tagsEnvelope - paperHeight * 0.5f);
        worldPackHoverRoot.sizeDelta = new Vector2(placement.width, paperHeight);
        worldPackHoverRoot.localScale = Vector3.one;
        worldPackHoverTagsRoot.anchoredPosition = new Vector2(0f, tagGap);
        RefreshWorldPackPaperLayout(worldPackHoverRoot.GetComponent<Image>());
        worldPackHoverTitleText.rectTransform.sizeDelta = new Vector2(placement.width - 84f, 40f);
        worldPackHoverRoot.gameObject.SetActive(true);
    }

    private static bool TryPlaceWorldPackHover(Rect page, Rect hovered, float anchorX, float preferredHeight,
        out Rect placement, float preferredWidth = 520f)
    {
        const float gap = 18f;
        float left = page.xMin + 32f, right = page.xMax - 32f;
        float top = page.yMin + page.height * 0.83f;
        float width = Mathf.Min(preferredWidth, page.width - 64f);
        float height = Mathf.Max(preferredHeight, 170f);
        if (width <= 84f)
        {
            placement = default;
            return false;
        }
        float x = Mathf.Clamp(anchorX, left + width * 0.5f, right - width * 0.5f);
        // A short upper area changes only the position. Preserve the paper and text size
        // even when that means overlapping the top of a tall model beneath the tooltip.
        float y = Mathf.Min(hovered.yMax + gap, top - height);
        placement = new Rect(x - width * 0.5f, y, width, height);
        return true;
    }

    private string BuildWorldPackHoverBody(PackItemUI item)
    {
        if ((item.ItemData is CardSO || item.ItemData is JokerSO) && item.ItemData is BaseCardSO card
            && CardTooltipDataBuilder.TryBuild(card, out CardTooltipData data))
        {
            worldPackHoverTitleText.text = data.displayName;
            ConfigureWorldPackHoverTags(data, card is CardSO normal ? normal.currentEdition : ((JokerSO)card).currentEdition);
            return data.effectText;
        }
        if (item.ItemData is ArtifactData artifact)
        {
            worldPackHoverTitleText.text = $"{item.DisplayName} <size=75%><color=#665449>/ {ArtifactRarityDisplay.GetLabel(artifact.artifactType)}</color></size>";
            string effect = artifact.GetTooltipExplanation();
            var builder = new StringBuilder(string.IsNullOrWhiteSpace(effect) ? GameText.Get("game.PackOpeningManager.BuildWorldPackHoverBody.07f72f7634", "등록된 효과 설명이 없습니다.") : effect);
            ConfigureWorldPackHoverTags(artifact);
            return builder.ToString();
        }
        if (item.ItemData is AmuletSO amulet)
            return ItemInfoTextBuilder.BuildConsumableHoverInfoBody(amulet).Replace("\n\n", "\n");
        return shopPackPresentation.HoverDescription?.Replace("\n\n", "\n") ?? "";
    }

    private static string AdaptWorldPackPaperText(string text)
    {
        // Existing shared builders also serve dark HUDs; adapt only this paper presentation.
        return text.Replace("#83D9D9", "#245F62").Replace("#7FE7FF", "#245F62")
            .Replace("#6FE7FF", "#245F62").Replace("#D6F4FF", "#302820")
            .Replace("#D8F4FF", "#302820").Replace("#C6E8F4", "#443D35")
            .Replace("#AFC5D2", "#665449").Replace("#9AC5D3", "#665449")
            .Replace("#FFD58A", "#753526").Replace("#7FB5C8", "#665449");
    }

    private void SpawnCardPackChoiceItem(PackCandidate candidate, Transform parent)
    {
        if (candidate == null || packItemPrefab == null || parent == null)
            return;

        GameObject itemObj = Instantiate(packItemPrefab, parent);
        PackItemUI itemUI = itemObj.GetComponent<PackItemUI>();
        if (itemUI == null)
            itemUI = itemObj.AddComponent<PackItemUI>();

        itemUI.Setup(
            candidate.Data,
            candidate.Type,
            candidate.Icon,
            shopPackPresentation != null ? null : candidate.ModelPrefab,
            candidate.ItemName,
            candidate.Description);
        if (shopPackPresentation == null) itemUI.ConfigurePackChoiceVisual();
        itemUI.SetSelectable(false);

        LayoutElement layoutElement = EnsureLayoutElement(itemObj, cardPackChoiceItemSize);
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        CanvasGroup itemGroup = EnsureCanvasGroup(itemObj);
        itemGroup.alpha = 0f;
        itemObj.transform.localScale = Vector3.one * 0.82f;
        activeCardPackChoiceItems.Add(itemUI);
        if (shopPackPresentation != null)
        {
            foreach (Graphic graphic in itemObj.GetComponentsInChildren<Graphic>(true)) { graphic.raycastTarget = false; graphic.enabled = false; }
            foreach (Renderer renderer in itemObj.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            shopPackPresentation.AddChoice(itemUI, candidate.Icon);
        }
    }

    private Button CreateCardPackConfirmButton(Transform parent)
    {
        GameObject buttonObject = CreateRectObject("ConfirmButton", parent);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.03f, 0.22f, 0.36f, 0.94f);
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = cardPackAccentColor;
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(ConfirmSelectedCardPackChoice);
        EnsureLayoutElement(buttonObject, new Vector2(360f, 72f));

        TextMeshProUGUI label = CreateText(
            buttonObject.transform,
            "Label",
            GameText.Get("game.PackOpeningManager.CreateCardPackConfirmButton.8d1a750c93", "선택"),
            30f,
            Color.white,
            TextAlignmentOptions.Center);
        RectTransform labelRect = label.transform as RectTransform;
        StretchToParent(labelRect, Vector2.zero, Vector2.zero);

        return button;
    }

    private Button CreateCardPackSkipButton(Transform parent)
    {
        GameObject buttonObject = CreateRectObject("SkipButton", parent);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.075f, 0.1f, 0.12f, 0.88f);
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.46f, 0.68f, 0.78f, 0.74f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button button = buttonObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(SkipCardPackChoice);
        EnsureLayoutElement(buttonObject, new Vector2(360f, 56f));

        TextMeshProUGUI label = CreateText(
            buttonObject.transform,
            "Label",
            GameText.Get("game.PackOpeningManager.CreateCardPackSkipButton.b6962f6d1b", "건너뛰기"),
            24f,
            new Color(0.86f, 0.94f, 0.98f, 1f),
            TextAlignmentOptions.Center);
        RectTransform labelRect = label.transform as RectTransform;
        StretchToParent(labelRect, Vector2.zero, Vector2.zero);

        return button;
    }

    private IEnumerator PlayCardPackChoiceIntroRoutine()
    {
        if (shopPackPresentation != null)
        {
            yield return shopPackPresentation.PlayIntro();
            if (shopPackPresentation == null || !isCardPackChoiceMode) yield break;
            foreach (PackItemUI item in activeCardPackChoiceItems) if (item != null) item.SetSelectable(true);
            packChoiceIntroComplete = true;
            cardPackChoiceSkipButton.interactable = true;
            cardPackChoiceConfirmButton.interactable = activeCardPackChoiceItems.Count == 0;
            yield break;
        }
        if (cardPackChoiceRoot == null)
            yield break;

        Canvas.ForceUpdateCanvases();
        CanvasGroup rootGroup = EnsureCanvasGroup(cardPackChoiceRoot);
        Transform rootTransform = cardPackChoiceRoot.transform;
        Vector3 rootBaseLocalPosition = rootTransform.localPosition;
        rootTransform.DOKill();
        rootGroup.DOKill();
        rootTransform.localPosition = rootBaseLocalPosition + new Vector3(0f, -54f, 0f);
        rootTransform.localScale = Vector3.one * 0.93f;
        rootGroup.DOFade(1f, ScalePresentationDuration(0.28f)).SetEase(Ease.OutSine).SetUpdate(true);
        rootTransform.DOLocalMove(rootBaseLocalPosition, ScalePresentationDuration(0.34f)).SetEase(Ease.OutCubic).SetUpdate(true);
        rootTransform.DOScale(1f, ScalePresentationDuration(0.36f)).SetEase(Ease.OutBack).SetUpdate(true);
        yield return new WaitForSecondsRealtime(ScalePresentationDelay(0.12f));

        for (int i = 0; i < activeCardPackChoiceItems.Count; i++)
        {
            PackItemUI item = activeCardPackChoiceItems[i];
            if (item == null)
                continue;

            CanvasGroup itemGroup = EnsureCanvasGroup(item.gameObject);
            Transform itemTransform = item.transform;
            Vector3 itemBaseLocalPosition = itemTransform.localPosition;
            itemTransform.DOKill();
            itemGroup.DOKill();
            itemTransform.localPosition = itemBaseLocalPosition + new Vector3(0f, -46f, 0f);
            itemTransform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-7f, 7f));
            itemTransform.localScale = Vector3.one * 0.72f;
            float revealDuration = ScalePresentationDuration(cardPackChoiceRevealDuration);
            itemGroup.DOFade(1f, revealDuration).SetEase(Ease.OutSine).SetUpdate(true);
            itemTransform.DOLocalMove(itemBaseLocalPosition, revealDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            itemTransform.DOLocalRotate(Vector3.zero, revealDuration).SetEase(Ease.OutSine).SetUpdate(true);
            itemTransform.DOScale(Vector3.one, revealDuration).SetEase(Ease.OutBack).SetUpdate(true);
            yield return new WaitForSecondsRealtime(ScalePresentationDelay(cardPackChoiceCascadeDelay));
        }

        yield return new WaitForSecondsRealtime(ScalePresentationDuration(cardPackChoiceRevealDuration));
        for (int i = 0; i < activeCardPackChoiceItems.Count; i++)
        {
            if (activeCardPackChoiceItems[i] != null)
                activeCardPackChoiceItems[i].SetSelectable(true);
        }
        packChoiceIntroComplete = true;
    }

    private void SelectCardPackChoice(PackItemUI item)
    {
        if (shopPackPresentation != null && !CanUseWorldPackInput()) return;
        if (item == null || !activeCardPackChoiceItems.Contains(item))
            return;

        if (shopPackPresentation != null)
        {
            ToggleWorldPackChoice(item);
            shopPackPresentation.SetSelection(selectedWorldPackChoices);
            UpdateWorldPackChoiceHud(selectedCardPackChoiceItem, selectedCardPackChoiceItem != null
                ? GetPackCapacityWarning(selectedCardPackChoiceItem.PackType, selectedCardPackChoiceItem.ItemData) : null);
            cardPackChoiceConfirmButton.interactable = selectedWorldPackChoices.Count > 0;
            SoundManager.PlaySfx(data => data.Shop_PackChoice);
            return;
        }

        if (selectedCardPackChoiceItem != null)
            selectedCardPackChoiceItem.SetPackChoiceSelected(false);

        selectedCardPackChoiceItem = item;
        selectedCardPackChoiceItem.SetPackChoiceSelected(true);

        if (cardPackChoiceDetailText != null)
        {
            string warning = GetPackCapacityWarning(item.PackType, item.ItemData);
            if (shopPackPresentation != null) UpdateWorldPackChoiceHud(item, warning);
            else
            {
                string detail = BuildCardPackChoiceDetailText(item);
                cardPackChoiceDetailText.text = string.IsNullOrEmpty(warning)
                    ? detail : $"{detail}\n<color=#FFB090>{warning}</color>";
            }
        }

        if (cardPackChoiceConfirmButton != null)
            cardPackChoiceConfirmButton.interactable = true;

        SoundManager.PlaySfx(data => data.Shop_PackChoice);
    }

    private void ToggleWorldPackChoice(PackItemUI item)
    {
        if (selectedWorldPackChoices.Remove(item))
        {
            worldPackSelectionOrder.Remove(item);
        }
        else if (remainingPicks > 0)
        {
            if (selectedWorldPackChoices.Count >= remainingPicks)
            {
                PackItemUI oldest = worldPackSelectionOrder[0];
                int slot = selectedWorldPackChoices.IndexOf(oldest);
                worldPackSelectionOrder.RemoveAt(0);
                selectedWorldPackChoices[slot] = item;
            }
            else selectedWorldPackChoices.Add(item);
            worldPackSelectionOrder.Add(item);
        }
        selectedCardPackChoiceItem = worldPackSelectionOrder.Count > 0
            ? worldPackSelectionOrder[worldPackSelectionOrder.Count - 1] : null;
    }

    private static string BuildCardPackChoiceDetailText(PackItemUI item)
    {
        if (item == null)
            return GameText.Get("game.PackOpeningManager.BuildCardPackChoiceDetailText.6e8cf440cd", "후보를 선택하면 이름과 효과가 표시됩니다.");

        string itemName = string.IsNullOrWhiteSpace(item.DisplayName) ? GameText.Get("game.PackOpeningManager.BuildCardPackChoiceDetailText.046ef64d09", "이름 없는 후보") : item.DisplayName;
        string description = item.ItemData is CardSO normalCard
            ? GetRemovalCardDescription(normalCard) : item.DisplayDescription?.Trim();

        if (item.ItemData is PlanetSO planet)
            return BuildPlanetChoiceDetailText(planet, itemName, description);

        if (item.ItemData is ArtifactData artifact)
            return BuildArtifactChoiceDetailText(artifact, itemName, description);

        if (string.IsNullOrWhiteSpace(description))
            return $"<color=#FFD58A>{itemName}</color>";

        return $"<color=#FFD58A>{itemName}</color>\n{description}";
    }

    private static string BuildArtifactChoiceDetailText(ArtifactData artifact, string itemName, string description)
    {
        StringBuilder builder = new StringBuilder(256);
        builder.Append($"<color=#FFD58A>{itemName}</color>");

        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.AppendLine();
            builder.Append(description);
        }

        string effect = artifact != null ? ArtifactTooltipFormatter.GetDisplayedExplanation(artifact)?.Trim() : null;
        if (!string.IsNullOrWhiteSpace(effect))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.Append($"<color=#7FE7FF>{effect}</color>");
        }

        return builder.ToString();
    }

    private static string BuildPlanetChoiceDetailText(PlanetSO planet, string itemName, string description)
    {
        int currentLevel = 1;
        int currentChip = 0;
        int currentMultiplier = 0;
        int baseChip = 0;
        int baseMultiplier = 1;

        JokboRuleDefinition rule = ResolveJokboRule(planet.jokboType);
        if (rule != null)
        {
            baseChip = Mathf.Max(0, rule.bonusChip);
            baseMultiplier = Mathf.Max(1, rule.multiplier);
        }

        PlanetCardLevelManager levelManager = PlanetCardLevelManager.Instance;
        if (levelManager != null
            && levelManager.jokboLevel != null
            && levelManager.jokboLevel.TryGetValue(planet.jokboType, out PlanetStats stats)
            && stats != null)
        {
            currentLevel = Mathf.Max(1, stats.level);
            currentChip = stats.plusChip;
            currentMultiplier = stats.plusMulti;
        }

        int nextLevel = currentLevel + 1;
        int currentTotalChip = baseChip + currentChip;
        int currentTotalMultiplier = baseMultiplier + currentMultiplier;
        int nextTotalChip = currentTotalChip + planet.plusChip;
        int nextTotalMultiplier = currentTotalMultiplier + planet.plusMulti;
        string title = string.IsNullOrWhiteSpace(description) ? GameText.Get("game.PackOpeningManager.BuildPlanetChoiceDetailText.47035476e4", "{value0} 족보 강화", new { value0 = itemName }) : description;
        return GameText.Get("game.PackOpeningManager.BuildPlanetChoiceDetailText.8edd7322c6", "{value0}\n<size=115%><color=#FFD58A>Lv. 1 상승</color></size>   <color=#AFC5D2>Lv. {value1} → {value2}</color>\n점 {value3} → <color=#6FE7FF>{value4}</color>   배율 {value5} → <color=#6FE7FF>{value6}</color>", new { value0 = title, value1 = currentLevel, value2 = nextLevel, value3 = currentTotalChip, value4 = nextTotalChip, value5 = currentTotalMultiplier, value6 = nextTotalMultiplier });
    }

    private static JokboRuleDefinition ResolveJokboRule(JokBoType jokboType)
    {
        if (jokboType == JokBoType.None)
            return null;

        JokboRuleProfile profile = ScoreManager.Instance != null
            ? ScoreManager.Instance.ActiveJokboRuleProfile
            : JokboRuleProfile.LoadResourcesDefaultOrRuntime();

        return profile != null ? profile.FindRule(jokboType) : null;
    }

    private void ConfirmSelectedCardPackChoice()
    {
        if (shopPackPresentation != null && !CanUseWorldPackInput()) return;
        if (isPackChoiceResolving)
            return;

        if (activeCardPackChoiceItems.Count == 0)
        {
            if (shopPackPresentation != null) SkipCardPackChoice();
            else ClosePackPanel();
            return;
        }

        if (selectedCardPackChoiceItem == null)
            return;

        if (shopPackPresentation != null)
        {
            StartCoroutine(PlayWorldPackChoiceConfirmRoutine(selectedWorldPackChoices.ToArray()));
            return;
        }

        bool isSuccess = TryApplyPickedItem(selectedCardPackChoiceItem.ItemData, selectedCardPackChoiceItem.PackType);
        if (!isSuccess)
            return;

        StartCoroutine(PlayCardPackChoiceConfirmRoutine(selectedCardPackChoiceItem));
    }

    private void SkipCardPackChoice()
    {
        if (shopPackPresentation != null && !CanUseWorldPackInput()) return;
        if (!isCardPackChoiceMode || isPackChoiceResolving)
            return;

        StartCoroutine(SkipCardPackChoiceRoutine());
    }

    private IEnumerator SkipCardPackChoiceRoutine()
    {
        isPackChoiceResolving = true;

        if (cardPackChoiceConfirmButton != null)
            cardPackChoiceConfirmButton.interactable = false;
        if (cardPackChoiceSkipButton != null)
            cardPackChoiceSkipButton.interactable = false;

        for (int i = 0; i < activeCardPackChoiceItems.Count; i++)
        {
            PackItemUI item = activeCardPackChoiceItems[i];
            if (item == null)
                continue;

            if (shopPackPresentation == null) item.SetPackChoiceSelected(false);
            item.SetSelectable(false);
        }

        SoundManager.PlaySfx(data => data.Shop_CardPackCancel);
        if (shopPackPresentation != null) yield return shopPackPresentation.Close();
        yield return FadeOutCardPackChoiceRootRoutine(0.16f);
        ClosePackPanel();
    }

    private IEnumerator PlayCardPackChoiceConfirmRoutine(PackItemUI selectedItem)
    {
        isPackChoiceResolving = true;

        if (cardPackChoiceConfirmButton != null)
            cardPackChoiceConfirmButton.interactable = false;
        if (cardPackChoiceSkipButton != null)
            cardPackChoiceSkipButton.interactable = false;

        for (int i = 0; i < activeCardPackChoiceItems.Count; i++)
        {
            PackItemUI item = activeCardPackChoiceItems[i];
            if (item == null)
                continue;

            item.SetSelectable(false);
            CanvasGroup group = EnsureCanvasGroup(item.gameObject);
            Transform itemTransform = item.transform;
            itemTransform.DOKill();
            group.DOKill();

            if (item == selectedItem)
            {
                itemTransform.DOScale(Vector3.one * 1.16f, ScalePresentationDuration(cardPackChoiceResolveDuration * 0.5f)).SetEase(Ease.OutBack).SetUpdate(true);
                continue;
            }

            group.DOFade(0.36f, ScalePresentationDuration(0.16f)).SetEase(Ease.OutSine).SetUpdate(true);
            itemTransform.DOScale(Vector3.one * 0.92f, ScalePresentationDuration(0.16f)).SetEase(Ease.OutSine).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(ScalePresentationDelay(cardPackChoiceResolveDuration * 0.5f));

        if (selectedItem != null)
        {
            CanvasGroup selectedGroup = EnsureCanvasGroup(selectedItem.gameObject);
            float resolveDuration = ScalePresentationDuration(cardPackChoiceResolveDuration);
            selectedGroup.DOFade(0f, resolveDuration).SetEase(Ease.InSine).SetUpdate(true);
            selectedItem.transform.DOScale(Vector3.one * 0.12f, resolveDuration).SetEase(Ease.InBack).SetUpdate(true);
            yield return new WaitForSecondsRealtime(resolveDuration);

            activeCardPackChoiceItems.Remove(selectedItem);
            Destroy(selectedItem.gameObject);
        }

        remainingPicks--;
        selectedCardPackChoiceItem = null;
        UpdateInfoText();

        if (remainingPicks <= 0 || activeCardPackChoiceItems.Count == 0)
        {
            yield return FadeOutCardPackChoiceRootRoutine(0.16f);
            ClosePackPanel();
            yield break;
        }

        if (cardPackChoiceDetailText != null)
            cardPackChoiceDetailText.text = GameText.Get("game.PackOpeningManager.PlayCardPackChoiceConfirmRoutine.35e34bfa67", "다음 후보를 선택하세요. 남은 선택: {value0}", new { value0 = remainingPicks });
        if (cardPackChoiceSkipButton != null)
            cardPackChoiceSkipButton.interactable = true;

        for (int i = 0; i < activeCardPackChoiceItems.Count; i++)
        {
            PackItemUI item = activeCardPackChoiceItems[i];
            if (item == null)
                continue;

            item.SetPackChoiceSelected(false);
            item.SetSelectable(true);
            CanvasGroup group = EnsureCanvasGroup(item.gameObject);
            group.DOFade(1f, ScalePresentationDuration(0.14f)).SetEase(Ease.OutSine).SetUpdate(true);
            item.transform.DOScale(Vector3.one, ScalePresentationDuration(0.14f)).SetEase(Ease.OutSine).SetUpdate(true);
        }

        isPackChoiceResolving = false;
    }

    private IEnumerator PlayWorldPackChoiceConfirmRoutine(PackItemUI[] pickedItems)
    {
        isPackChoiceResolving = true;
        cardPackChoiceConfirmButton.interactable = false;
        cardPackChoiceSkipButton.interactable = false;
        foreach (PackItemUI item in activeCardPackChoiceItems) if (item != null) item.SetSelectable(false);
        string failure = null;
        bool collected = false;
        List<CardSO> acquiredCards = new List<CardSO>();
        foreach (PackItemUI picked in pickedItems)
        {
            if (shopPackPresentation == null || !isCardPackChoiceMode) yield break;
            if (remainingPicks <= 0) break;
            if (picked == null || !activeCardPackChoiceItems.Contains(picked)) continue;
            long acquireSequence = ConsumableManager.Instance != null
                ? ConsumableManager.Instance.CaptureAcquirePresentationSequence() : 0;
            shopPackPresentation.TryGetAcquirePose(picked, out Vector3 acquirePosition, out float acquireHeight);
            if (!TryApplyWorldPackPickedItem(picked, out ArtifactObject acquiredArtifact))
            {
                failure = GetPackCapacityWarning(picked.PackType, picked.ItemData)
                    ?? GameText.Get("game.PackOpeningManager.PlayWorldPackChoiceConfirmRoutine.27edf29210", "획득하지 못했습니다.");
                failure += GameText.Get("game.PackOpeningManager.PlayWorldPackChoiceConfirmRoutine.29bc7608ef", " 선택 횟수는 유지됩니다.");
                break;
            }
            // Commit each successful grant immediately so interruption cannot leave a reusable candidate.
            remainingPicks--;
            collected = true;
            activeCardPackChoiceItems.Remove(picked);
            selectedWorldPackChoices.Remove(picked);
            worldPackSelectionOrder.Remove(picked);
            selectedCardPackChoiceItem = worldPackSelectionOrder.Count > 0
                ? worldPackSelectionOrder[worldPackSelectionOrder.Count - 1] : null;
            UpdateInfoText();
            UpdateWorldPackChoiceHud(selectedCardPackChoiceItem);
            yield return shopPackPresentation.Collect(picked);
            if (shopPackPresentation == null || !isCardPackChoiceMode) yield break;
            if (picked.PackType == PackType.Normal && picked.ItemData is CardSO acquiredCard)
                acquiredCards.Add(acquiredCard);
            else
                yield return PlayWorldPackAcquireResultRoutine(picked, acquiredArtifact, acquireSequence,
                    acquirePosition, acquireHeight * 1.25f);
            if (shopPackPresentation == null || !isCardPackChoiceMode) yield break;
            Destroy(picked.gameObject);
        }
        if (shopPackPresentation == null || !isCardPackChoiceMode) yield break;
        if (acquiredCards.Count > 0)
        {
            // Fade all confirmed sources before opening the deck once for the batch.
            yield return PlayWorldPackCardsAcquireResultRoutine(acquiredCards);
            if (shopPackPresentation == null || !isCardPackChoiceMode) yield break;
        }
        shopPackPresentation.SetSelection(selectedWorldPackChoices);
        UpdateWorldPackChoiceHud(selectedCardPackChoiceItem, failure);
        if (remainingPicks <= 0 || activeCardPackChoiceItems.Count == 0)
        {
            yield return shopPackPresentation.Close();
            yield return FadeOutCardPackChoiceRootRoutine(0.12f);
            ClosePackPanel();
            yield break;
        }
        if (collected)
        {
            yield return shopPackPresentation.RecenterRemainingChoices();
            if (shopPackPresentation == null || !isCardPackChoiceMode) yield break;
        }
        if (selectedWorldPackChoices.Count == 0) worldPackChoiceCaptionText.text = GameText.Get("game.PackOpeningManager.PlayWorldPackChoiceConfirmRoutine.7bcfad7b11", "다음 물건을 고르세요");
        cardPackChoiceConfirmButton.interactable = selectedWorldPackChoices.Count > 0;
        cardPackChoiceSkipButton.interactable = true;
        foreach (PackItemUI item in activeCardPackChoiceItems) if (item != null) item.SetSelectable(true);
        isPackChoiceResolving = false;
    }

    private bool TryApplyWorldPackPickedItem(PackItemUI picked, out ArtifactObject acquiredArtifact)
    {
        acquiredArtifact = null;
        bool success;
        if (picked.PackType == PackType.Artifact)
        {
            success = ArtifactManager.Instance != null
                && ArtifactManager.Instance.TryAddArtifactForPack(picked.ItemData as ArtifactData, out acquiredArtifact);
            if (success && acquiredArtifact != null) pendingWorldPackArtifacts.Add(acquiredArtifact);
        }
        else if (picked.PackType == PackType.Amulet)
        {
            // Queue before notifying the inventory, so its first render is already hidden.
            success = ConsumableManager.Instance != null
                && ConsumableManager.Instance.TryAddConsumable(picked.ItemData as AmuletSO, GameText.Get("game.PackOpeningManager.TryApplyWorldPackPickedItem.5739db1617", "주머니"));
        }
        else if (picked.PackType == PackType.Normal)
        {
            success = TryApplyPickedItem(picked.ItemData, picked.PackType);
            if (success && picked.ItemData is CardSO card)
            {
                // Commit once through the existing grant path. Only defer its deck visual.
                ResolveWorldPackDeckDisplay()?.PreparePackAcquire(card);
                pendingWorldPackCards.Add(card);
            }
            return success;
        }
        else return TryApplyPickedItem(picked.ItemData, picked.PackType);
        if (!success) ShowPackAcquisitionFailure(picked.PackType, picked.ItemData);
        return success;
    }

    private IEnumerator PlayWorldPackAcquireResultRoutine(PackItemUI picked, ArtifactObject acquiredArtifact,
        long acquireSequence, Vector3 acquirePosition, float acquireHeight)
    {
        if (picked.PackType != PackType.Artifact && picked.PackType != PackType.Amulet
            && picked.PackType != PackType.Planet) yield break;
        HidePackForInventory();
        if (picked.PackType != PackType.Planet) shopPackPresentation?.SetVisible(false);
        try
        {
            IEnumerator acquisition = null;
            if (picked.PackType == PackType.Artifact && ArtifactManager.Instance != null)
                acquisition = ArtifactManager.Instance.PlayPackAcquirePresentationRoutine(acquiredArtifact);
            else if (picked.PackType == PackType.Amulet && ConsumableManager.Instance != null)
                acquisition = ConsumableManager.Instance.PlayPendingAcquirePresentationRoutineAfter(acquireSequence);
            else if (picked.PackType == PackType.Planet && PlanetCardLevelManager.Instance != null)
                acquisition = PlanetCardLevelManager.Instance.PlayAppliedUpgradeAtPositionRoutine(
                    picked.ItemData as PlanetSO, acquirePosition, acquireHeight,
                    visual => worldPackUpgradePresentationRoot = visual);
            if (acquisition != null) yield return RunWorldPackAcquisitionRoutine(acquisition);
        }
        finally
        {
            ArtifactManager.ReleasePackAcquirePresentation(acquiredArtifact);
            pendingWorldPackArtifacts.Remove(acquiredArtifact);
            if (worldPackUpgradePresentationRoot != null)
            {
                worldPackUpgradePresentationRoot.SetActive(false);
                Destroy(worldPackUpgradePresentationRoot);
            }
            worldPackUpgradePresentationRoot = null;
            if (isActiveAndEnabled && isCardPackChoiceMode && shopPackPresentation != null)
                RestorePackInventoryVisibility(true);
        }
    }

    private TableInCardDisplay ResolveWorldPackDeckDisplay()
    {
        if (worldPackDeckDisplay != null && worldPackDeckDisplay.isActiveAndEnabled)
            return worldPackDeckDisplay;

        CameraHeadLookController view = Camera.main != null
            ? Camera.main.GetComponent<CameraHeadLookController>() : null;
        if (view == null) return null;
        foreach (TableInCardDisplay display in FindObjectsByType<TableInCardDisplay>(FindObjectsSortMode.None))
        {
            if (display.isActiveAndEnabled && display.HeadLookController == view)
            {
                worldPackDeckDisplay = display;
                return display;
            }
        }
        return null;
    }

    private IEnumerator PlayWorldPackCardsAcquireResultRoutine(IReadOnlyList<CardSO> cards)
    {
        HidePackForInventory();
        shopPackPresentation?.SetVisible(false);
        try { yield return RunWorldPackAcquisitionRoutine(PlayWorldPackCardAcquireRoutine(cards)); }
        finally
        {
            foreach (CardSO card in cards)
            {
                worldPackDeckDisplay?.ReleasePackAcquire(card);
                pendingWorldPackCards.Remove(card);
            }
            if (isActiveAndEnabled && isCardPackChoiceMode && shopPackPresentation != null)
                RestorePackInventoryVisibility(true);
        }
    }

    private IEnumerator PlayWorldPackCardAcquireRoutine(IReadOnlyList<CardSO> cards)
    {
        TableInCardDisplay display = ResolveWorldPackDeckDisplay();
        CameraHeadLookController view = display != null ? display.HeadLookController : null;
        if (cards.Count == 0 || view == null || !display.isActiveAndEnabled) yield break;
        CameraViewSpaceStage stage = view.CurrentStageContext;
        TableInDeckViewResponder drawer = display.GetComponent<TableInDeckViewResponder>();
        bool moved = false;
        bool returned = false;
        try
        {
            moved = view.MoveToViewSpaceForPresentation(GameText.Get("game.PackOpeningManager.PlayWorldPackCardAcquireRoutine.b0ff41958b", "덱"), ScalePresentationDuration(0.4f));
            if (!moved) yield break;
            while (view != null && view.IsCameraMoving) yield return null;

            // The logical deck node opens the authored drawer after the camera settles.
            float wait = 0f;
            while (display != null && display.isActiveAndEnabled && drawer != null && !drawer.IsFullyOpen && wait < 1.5f)
            {
                if (!OptionWindowManager.IsAnyOptionWindowOpen())
                    wait += PresentationSpeedManager.ScaleDeltaTime(Time.unscaledDeltaTime);
                yield return null;
            }
            if (display == null || !display.isActiveAndEnabled || (drawer != null && !drawer.IsFullyOpen))
                yield break;

            foreach (CardSO card in cards)
            {
                if (display == null || !display.isActiveAndEnabled) yield break;
                yield return display.PlayPackAcquirePresentationRoutine(card);
            }
            float hold = 0f;
            while (hold < 0.3f)
            {
                if (!OptionWindowManager.IsAnyOptionWindowOpen())
                    hold += PresentationSpeedManager.ScaleDeltaTime(Time.unscaledDeltaTime);
                yield return null;
            }

            // Restore the world row during the return pan, as with manual inventory browsing.
            shopPackPresentation?.SetVisible(true);
            if (view != null && view.CurrentStageContext == stage)
            {
                view.MoveToDefaultNode(ScalePresentationDuration(0.4f), Ease.InOutSine);
                while (view != null && view.IsCameraMoving) yield return null;
                returned = true;
            }
        }
        finally
        {
            if (display != null)
                foreach (CardSO card in cards) display.ReleasePackAcquire(card);
            if (moved && !returned && view != null && view.CurrentStageContext == stage)
                view.MoveToDefaultNode(0.01f, Ease.InOutSine);
        }
    }

    private void ReleaseWorldPackAcquisitionVisuals()
    {
        DisposeWorldPackAcquisitionRoutines();
        foreach (ArtifactObject artifact in pendingWorldPackArtifacts)
            ArtifactManager.ReleasePackAcquirePresentation(artifact);
        pendingWorldPackArtifacts.Clear();
        if (worldPackDeckDisplay != null)
            foreach (CardSO card in pendingWorldPackCards) worldPackDeckDisplay.ReleasePackAcquire(card);
        pendingWorldPackCards.Clear();
        worldPackDeckDisplay = null;
        if (worldPackUpgradePresentationRoot != null)
        {
            worldPackUpgradePresentationRoot.SetActive(false);
            Destroy(worldPackUpgradePresentationRoot);
        }
        worldPackUpgradePresentationRoot = null;
    }

    private IEnumerator RunWorldPackAcquisitionRoutine(IEnumerator acquisition)
    {
        // Own nested iterators explicitly: stopping the MonoBehaviour must also release
        // each inventory/camera/input finally block, including a suspended child routine.
        worldPackAcquisitionRoutines.Push(acquisition);
        try
        {
            while (worldPackAcquisitionRoutines.Count > 0)
            {
                IEnumerator current = worldPackAcquisitionRoutines.Peek();
                if (!current.MoveNext())
                {
                    worldPackAcquisitionRoutines.Pop();
                    (current as IDisposable)?.Dispose();
                }
                else if (current.Current is IEnumerator nested)
                    worldPackAcquisitionRoutines.Push(nested);
                else yield return current.Current;
            }
        }
        finally { DisposeWorldPackAcquisitionRoutines(); }
    }

    private void DisposeWorldPackAcquisitionRoutines()
    {
        while (worldPackAcquisitionRoutines.Count > 0)
            (worldPackAcquisitionRoutines.Pop() as IDisposable)?.Dispose();
    }

    private IEnumerator FadeOutCardPackChoiceRootRoutine(float duration)
    {
        if (cardPackChoiceRoot == null)
            yield break;

        CanvasGroup rootGroup = EnsureCanvasGroup(cardPackChoiceRoot);
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        float scaledDuration = ScalePresentationDuration(duration);
        rootGroup.DOFade(0f, scaledDuration).SetEase(Ease.InSine).SetUpdate(true);
        cardPackChoiceRoot.transform.DOScale(0.985f, scaledDuration).SetEase(Ease.InSine).SetUpdate(true);
        yield return new WaitForSecondsRealtime(scaledDuration);
    }

    public void OpenCardRemoval(Action<bool> onClosed = null)
    {
        if (suppressCardRemovalUi)
        {
            GameLog.Info("[카드 제거] 기존 카드 제거 UI가 임시 비활성화되어 표시하지 않습니다.");
            onClosed?.Invoke(false);
            return;
        }

        isCardRemovalMode = true;
        isCardUpgradeMode = false;
        isRemovalResolving = false;
        cardRemovalWasApplied = false;
        packClosedCallback = null;
        cardRemovalClosedCallback = onClosed;
        cardUpgradeClosedCallback = null;
        if (cardRemovalCancelButton != null)
            cardRemovalCancelButton.interactable = true;
        activeRemovalItems.Clear();
        packOpeningPanel.SetActive(true);
        AcquireInputBlock();
        remainingPicks = 1; // 1장만 지울 수 있음
        SetPackOpeningPanelShellForCardRemoval(true);
        if (TableTransitionManager.instance == null || !TableTransitionManager.instance.IsSpecialPanelFocused)
            TiltCameraForCardRemoval();

        UpdateCardRemovalInfoText();

        ClearSpawnedPackItems();

        // 덱에서 제거 가능한 일반 카드와 조커를 골라내기
        List<JokerSO> jokerCards = new List<JokerSO>();
        List<CardSO> normalCards = new List<CardSO>();
        if (GameManager.instance != null)
        {
            foreach (var card in GameManager.instance.masterDeck)
            {
                if (card is JokerSO jokerCard)
                    jokerCards.Add(jokerCard);
                else if (card is CardSO normalCard)
                    normalCards.Add(normalCard);
            }
        }

        normalCards.Sort(CompareCardsForRemoval);

        if (useRemovalSkyPresentation)
        {
            BuildRemovalSkyLayout(jokerCards, normalCards);
            StartCoroutine(PlayCardSelectionIntroRoutine());
            return;
        }

        for (int i = 0; i < jokerCards.Count; i++)
        {
            JokerSO jokerData = jokerCards[i];
            SpawnPackItem(jokerData, PackType.Removal, jokerData.jokerSprite, null, GetRemovalCardName(jokerData), GetRemovalCardDescription(jokerData));
        }

        for (int i = 0; i < normalCards.Count; i++)
        {
            CardSO cardData = normalCards[i];
            SpawnPackItem(cardData, PackType.Removal, GetRemovalCardSprite(cardData), null, GetRemovalCardName(cardData), GetRemovalCardDescription(cardData));
        }
    }

    public void OpenCardUpgrade(Action<bool> onClosed = null)
    {
        isCardRemovalMode = false;
        isCardUpgradeMode = true;
        isRemovalResolving = false;
        isCardPackChoiceMode = false;
        isPackChoiceResolving = false;
        cardRemovalWasApplied = false;
        cardUpgradeWasAttempted = false;
        cardUpgradeIsEnding = false;
        selectedUpgradeCard = null;
        selectedCardPackChoiceItem = null;
        packClosedCallback = null;
        cardRemovalClosedCallback = null;
        cardUpgradeClosedCallback = onClosed;
        activeRemovalItems.Clear();
        packOpeningPanel.SetActive(true);
        AcquireInputBlock();
        remainingPicks = 1;
        SetPackOpeningPanelShellForCardRemoval(true);
        ClearSpawnedPackItems();
        ClearCardUpgradePanel();

        List<CardSO> upgradeableCards = new List<CardSO>();
        if (GameManager.instance != null && GameManager.instance.masterDeck != null)
        {
            for (int i = 0; i < GameManager.instance.masterDeck.Count; i++)
            {
                if (GameManager.instance.masterDeck[i] is not CardSO card || card.isDestroyed)
                    continue;

                if (card.cardUpgradeLevel >= CardUpgradeService.MaxUpgradeLevel)
                    continue;

                upgradeableCards.Add(card);
            }
        }

        upgradeableCards.Sort(CompareCardsForRemoval);
        cardStrengthenPresentationController = TableTransitionManager.instance != null
            ? TableTransitionManager.instance.CardStrengthenPresentation
            : null;
        if (cardStrengthenPresentationController != null
            && cardStrengthenPresentationController.BeginPresentation(
                upgradeableCards,
                TryUpgradeCardForPresentation,
                CloseCardStrengthenPresentation))
        {
            GameLog.Info($"[패 강화] 제단 앞 일렬 캐러셀에 강화 가능한 3D 패 {upgradeableCards.Count}장을 배치했습니다.");
            return;
        }

        // 씬 참조가 없는 격리 테스트에서는 기존 UI 경로를 안전한 fallback으로 유지합니다.
        ShowPackOpeningPanelContentWithoutBackground();
        UpdateCardUpgradeInfoText();
        BuildCardUpgradePanelLayout(upgradeableCards);
        StartCoroutine(PlayCardSelectionIntroRoutine());
        GameLog.Info($"[패 강화] CardStrengthenPanel 위에 강화 가능한 패 {upgradeableCards.Count}장을 표시합니다.");
    }

    private CardUpgradeAttempt TryUpgradeCardForPresentation(CardSO card)
    {
        if (!isCardUpgradeMode || card == null)
            return new CardUpgradeAttempt(0, 0, 0, 0, CardUpgradeResult.InvalidCard);

        selectedUpgradeCard = card;
        CardUpgradeAttempt attempt = CardUpgradeService.TryUpgrade(card);
        SoundManager.PlaySfx(data => attempt.Result == CardUpgradeResult.Success
            ? data.Upgrade_Success
            : data.Upgrade_Fail);
        cardUpgradeWasAttempted |= attempt.Result == CardUpgradeResult.Success
            || attempt.Result == CardUpgradeResult.Failed
            || attempt.Result == CardUpgradeResult.Destroyed;
        return attempt;
    }

    private void CloseCardStrengthenPresentation()
    {
        if (isCardUpgradeMode)
            ClosePackPanel();
    }

    private void UpdateInfoText()
    {
        if (infoText != null)
        {
            infoText.text = GameText.Get("game.PackOpeningManager.UpdateInfoText.a1b5f60a4f", "원하는 패를 선택하세요! (남은 횟수: {value0}){value1}", new { value0 = remainingPicks, value1 = (isCardPackChoiceMode ? "\nA / D : 보유 기물·부적 확인 및 판매" : "") });
        }
    }

    // "건너뛰기" 버튼이나 모두 선택했을 때 닫는 함수
    public void ClosePack()
    {
        if (shopPackPresentation != null)
        {
            if (!isPackChoiceResolving && !isPackCloseResolving) SkipCardPackChoice();
            return;
        }
        SoundManager.PlaySfx(data => isCardRemovalMode || isCardUpgradeMode ? data.UI_Cancel : data.Shop_CardPackCancel);
        ClosePackPanel();
    }

    private void ClosePackAfterSelection()
    {
        ClosePackPanel();
    }

    private void ClosePackPanel()
    {
        if (isPackCloseResolving)
            return;

        bool wasCardRemovalMode = isCardRemovalMode;
        bool wasCardUpgradeMode = isCardUpgradeMode;
        bool hadClosedCallback = packClosedCallback != null;
        Action closedCallback = packClosedCallback;
        Action<bool> removalClosedCallback = cardRemovalClosedCallback;
        Action<bool> upgradeClosedCallback = cardUpgradeClosedCallback;
        bool wasCardRemovalApplied = cardRemovalWasApplied;
        bool wasCardUpgradeAttempted = cardUpgradeWasAttempted;
        CardStrengthenPresentationController strengthenPresentation = cardStrengthenPresentationController;
        long acquirePresentationSequence = packAcquirePresentationSequence;
        bool shouldPlayPackAcquirePresentation = !wasCardRemovalMode
            && !wasCardUpgradeMode
            && hasPackAcquirePresentationSequence;

        isPackCloseResolving = true;
        IsBrowsingPackInventory = false;
        RestorePackInventoryVisibility();
        ReleaseGeneratedPackCards();

        isCardRemovalMode = false;
        isCardUpgradeMode = false;
        isCardPackChoiceMode = false;
        isRemovalResolving = false;
        isPackChoiceResolving = false;
        cardRemovalWasApplied = false;
        cardUpgradeWasAttempted = false;
        selectedUpgradeCard = null;
        cardStrengthenPresentationController = null;
        selectedCardPackChoiceItem = null;
        packClosedCallback = null;
        cardRemovalClosedCallback = null;
        cardUpgradeClosedCallback = null;
        activeRemovalItems.Clear();
        activeCardPackChoiceItems.Clear();
        SetPackOpeningPanelShellForCardRemoval(false);
        packOpeningPanel.SetActive(false);
        ClearCardPackChoiceRoot();
        ClearCardUpgradePanel();
        ClearRemovalSkyRoot();
        strengthenPresentation?.StopAndClear();

        if (wasCardRemovalMode)
        {
            hasPackAcquirePresentationSequence = false;
            packAcquirePresentationSequence = 0;
            RestoreCameraAfterCardRemoval();
            isPackCloseResolving = false;
            ReleaseInputBlock();
            GameLog.Info("[Pack] 카드 제거가 종료되었습니다.");
            removalClosedCallback?.Invoke(wasCardRemovalApplied);
        }
        else if (wasCardUpgradeMode)
        {
            hasPackAcquirePresentationSequence = false;
            packAcquirePresentationSequence = 0;
            RestoreCameraAfterCardRemoval();
            isPackCloseResolving = false;
            ReleaseInputBlock();
            GameLog.Info("[Pack] 카드 강화가 종료되었습니다.");
            upgradeClosedCallback?.Invoke(wasCardUpgradeAttempted);
        }
        else
        {
            StartCoroutine(CompletePackCloseRoutine(
                shouldPlayPackAcquirePresentation,
                acquirePresentationSequence,
                hadClosedCallback ? closedCallback : null));
        }
    }

    private IEnumerator CompletePackCloseRoutine(
        bool shouldPlayAcquirePresentation,
        long acquirePresentationSequence,
        Action closedCallback)
    {
        bool cameraRestoreCompleted = false;
        RestoreCameraAfterCardRemoval(() => cameraRestoreCompleted = true);

        float restoreTimeout = Mathf.Max(
            0.1f,
            ScalePresentationDuration(removalCameraReturnDuration) + 0.25f);
        float elapsed = 0f;
        while (!cameraRestoreCompleted && elapsed < restoreTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!cameraRestoreCompleted && hasRemovalCameraPose && removalCameraTransform != null)
        {
            removalCameraTransform.DOKill();
            removalCameraTransform.rotation = removalOriginalCameraRotation;
            hasRemovalCameraPose = false;
            removalCameraTransform = null;
        }

        try
        {
            if (shouldPlayAcquirePresentation && ConsumableManager.Instance != null)
            {
                yield return ConsumableManager.Instance.PlayPendingAcquirePresentationRoutineAfter(
                    acquirePresentationSequence);
            }
        }
        finally
        {
            hasPackAcquirePresentationSequence = false;
            packAcquirePresentationSequence = 0;
            isPackCloseResolving = false;
            ReleaseInputBlock();
            GameLog.Info("[Pack] 카드팩 개봉 및 후속 획득 연출이 종료되었습니다.");
            shopPackPurchasePosition = null;
            shopPackClosedCallback = null;
            if (this != null)
                closedCallback?.Invoke();
        }
    }

    private void ClearSpawnedPackItems()
    {
        IsBrowsingPackInventory = false;
        RestorePackInventoryVisibility();
        ReleaseGeneratedPackCards();
        activeRemovalItems.Clear();
        activeCardPackChoiceItems.Clear();
        selectedCardPackChoiceItem = null;
        ClearCardPackChoiceRoot();
        ClearRemovalSkyRoot();

        if (cardSpawnParent == null)
            return;

        foreach (Transform child in cardSpawnParent)
        {
            Destroy(child.gameObject);
        }
    }

    private void ClearCardPackChoiceRoot()
    {
        ReleaseWorldPackAcquisitionVisuals();
        shopPackPresentation?.Dispose();
        shopPackPresentation = null;
        pressedWorldPackItem = null;
        selectedCardPackChoiceItem = null;
        activeCardPackChoiceItems.Clear();
        selectedWorldPackChoices.Clear();
        worldPackSelectionOrder.Clear();
        cardPackChoiceTitleText = null;
        cardPackChoiceDetailText = null;
        worldPackChoiceCaptionText = null;
        worldPackRemainingText = null;
        worldPackWarningText = null;
        worldPackHoverRoot = null;
        worldPackHoverTitleText = null;
        worldPackHoverBodyText = null;
        worldPackHoverTagsRoot = null;
        worldPackHoverTags.Clear();
        worldPackHoveredItem = null;
        cardPackChoiceConfirmButton = null;
        cardPackChoiceSkipButton = null;

        if (cardPackChoiceRoot != null)
        {
            cardPackChoiceRoot.transform.DOKill(true);
            PackItemUI[] items = cardPackChoiceRoot.GetComponentsInChildren<PackItemUI>(true);
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                    items[i].transform.DOKill(true);
            }
        }

        if (cardPackChoiceOverlayCanvasRoot != null)
        {
            Destroy(cardPackChoiceOverlayCanvasRoot);
            cardPackChoiceOverlayCanvasRoot = null;
        }

        cardPackChoiceRoot = null;
    }

    private void ClearRemovalSkyRoot()
    {
        if (isUsingSceneRemovalSkyView)
        {
            ClearSceneRemovalSkyView();
            if (cardRemovalSkyViewRoot != null)
                cardRemovalSkyViewRoot.SetActive(false);

            RestoreCardRemovalSceneRootParent();
            isUsingSceneRemovalSkyView = false;
            removalSkyRoot = null;
            return;
        }

        if (removalSkyRoot == null)
            return;

        removalSkyRoot.transform.DOKill(true);
        PackItemUI[] items = removalSkyRoot.GetComponentsInChildren<PackItemUI>(true);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                items[i].transform.DOKill(true);
        }

        Destroy(removalSkyRoot);
        removalSkyRoot = null;
    }

    /// <summary>
    /// 강화 노드에서는 카드 제거용 하늘 배경 대신 씬의 CardStrengthenPanel이
    /// 그대로 보이도록 팩 UI의 내용과 입력만 활성화합니다.
    /// </summary>
    private void ShowPackOpeningPanelContentWithoutBackground()
    {
        if (packOpeningPanel == null)
            return;

        EnsurePackOpeningPanelCanvasGroupState();
        if (packOpeningPanelCanvasGroup != null)
        {
            packOpeningPanelCanvasGroup.alpha = originalPackOpeningPanelCanvasAlpha;
            packOpeningPanelCanvasGroup.interactable = true;
            packOpeningPanelCanvasGroup.blocksRaycasts = true;
        }

        EnsurePackOpeningPanelImageState();
        if (packOpeningPanelImage != null)
            packOpeningPanelImage.enabled = false;
    }

    private void BuildCardUpgradePanelLayout(List<CardSO> cards)
    {
        if (packOpeningPanel == null || ResolveRemovalItemPrefab() == null)
            return;

        removalSkyRoot = CreateRectObject("CardUpgradeSelectionRoot", packOpeningPanel.transform);
        RectTransform rootRect = removalSkyRoot.transform as RectTransform;
        StretchToParent(rootRect, new Vector2(54f, 96f), new Vector2(54f, 96f));

        CanvasGroup rootGroup = removalSkyRoot.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.interactable = true;
        rootGroup.blocksRaycasts = true;

        BuildCardSelectionRuntimeContent(removalSkyRoot.transform, new List<JokerSO>(), cards);
        Canvas.ForceUpdateCanvases();
        ForceRebuildCardRemovalRuntimeContent();
    }

    private void TiltCameraForCardRemoval()
    {
        if (!moveCameraForCardRemoval)
            return;

        Camera mainCamera = Camera.main;
        removalCameraTransform = mainCamera != null ? mainCamera.transform : null;
        if (removalCameraTransform == null)
            return;

        removalOriginalCameraRotation = removalCameraTransform.rotation;
        hasRemovalCameraPose = true;

        Quaternion targetRotation = removalOriginalCameraRotation * Quaternion.Euler(removalSkyCameraEulerOffset);
        CameraDirector director = removalCameraTransform.GetComponent<CameraDirector>();
        if (director != null)
        {
            director.MoveToTarget(removalCameraTransform.position, targetRotation.eulerAngles, ScalePresentationDuration(removalCameraTiltDuration), Ease.OutSine);
            return;
        }

        removalCameraTransform.DOKill();
        removalCameraTransform
            .DORotateQuaternion(targetRotation, ScalePresentationDuration(removalCameraTiltDuration))
            .SetEase(Ease.OutSine)
            .SetUpdate(true);
    }

    private void RestoreCameraAfterCardRemoval(Action onComplete = null)
    {
        if (!hasRemovalCameraPose || removalCameraTransform == null)
        {
            hasRemovalCameraPose = false;
            removalCameraTransform = null;
            onComplete?.Invoke();
            return;
        }

        CameraDirector director = removalCameraTransform.GetComponent<CameraDirector>();
        if (director != null)
        {
            director.MoveToTarget(
                removalCameraTransform.position,
                removalOriginalCameraRotation.eulerAngles,
                ScalePresentationDuration(removalCameraReturnDuration),
                Ease.OutSine,
                () =>
                {
                    hasRemovalCameraPose = false;
                    removalCameraTransform = null;
                    onComplete?.Invoke();
                });
            return;
        }

        removalCameraTransform.DOKill();
        removalCameraTransform
            .DORotateQuaternion(removalOriginalCameraRotation, ScalePresentationDuration(removalCameraReturnDuration))
            .SetEase(Ease.OutSine)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                hasRemovalCameraPose = false;
                removalCameraTransform = null;
                onComplete?.Invoke();
            });
    }

    private void BuildRemovalSkyLayout(List<JokerSO> jokers, List<CardSO> cards)
    {
        if (packOpeningPanel == null || packItemPrefab == null)
            return;

        if (HasSceneRemovalSkyView())
        {
            BuildSceneRemovalSkyLayout(jokers, cards);
            return;
        }

        Transform skyParent = cardSpawnParent != null ? cardSpawnParent : packOpeningPanel.transform;
        removalSkyRoot = CreateRectObject("CardRemovalSkyRoot", skyParent);
        RectTransform rootRect = removalSkyRoot.transform as RectTransform;
        StretchToParent(rootRect, Vector2.zero, Vector2.zero);
        LayoutElement rootLayout = EnsureLayoutElement(removalSkyRoot, new Vector2(980f, 560f));
        rootLayout.flexibleWidth = 1f;
        rootLayout.flexibleHeight = 1f;

        Image background = removalSkyRoot.AddComponent<Image>();
        background.color = removalSkyTint;
        background.raycastTarget = false;
        CreateRemovalSkyStars(removalSkyRoot.transform);

        CanvasGroup rootGroup = removalSkyRoot.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.interactable = true;
        rootGroup.blocksRaycasts = true;

        GameObject viewport = CreateRectObject("SkyViewport", removalSkyRoot.transform);
        RectTransform viewportRect = viewport.transform as RectTransform;
        StretchToParent(viewportRect, new Vector2(30f, 58f), new Vector2(30f, 34f));
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = false;
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject rowsRoot = CreateRectObject("MonthRows", viewport.transform);
        RectTransform rowsRect = rowsRoot.transform as RectTransform;
        StretchToParent(rowsRect, Vector2.zero, Vector2.zero);
        rowsRect.anchorMin = new Vector2(0f, 1f);
        rowsRect.anchorMax = new Vector2(1f, 1f);
        rowsRect.pivot = new Vector2(0.5f, 1f);
        rowsRect.offsetMin = new Vector2(0f, rowsRect.offsetMin.y);
        rowsRect.offsetMax = new Vector2(0f, rowsRect.offsetMax.y);

        ScrollRect scrollRect = removalSkyRoot.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = rowsRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 28f;

        VerticalLayoutGroup rowsLayout = rowsRoot.AddComponent<VerticalLayoutGroup>();
        rowsLayout.spacing = 14f;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;

        ContentSizeFitter rowsFitter = rowsRoot.AddComponent<ContentSizeFitter>();
        rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (jokers != null && jokers.Count > 0)
            CreateRemovalJokerColumn(rowsRoot.transform, jokers);

        Dictionary<_CardMonth, List<CardSO>> cardsByMonth = GroupCardsByMonth(cards);
        for (int rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            GameObject row = CreateRectObject($"MonthRow_{rowIndex + 1}", rowsRoot.transform);
            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.UpperCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            ContentSizeFitter rowFitter = row.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int monthIndex = rowIndex * 5; monthIndex < (rowIndex + 1) * 5; monthIndex++)
            {
                _CardMonth month = (_CardMonth)monthIndex;
                cardsByMonth.TryGetValue(month, out List<CardSO> monthCards);
                CreateRemovalMonthColumn(row.transform, month, monthCards);
            }
        }
    }

    private bool HasSceneRemovalSkyView()
    {
        if (cardRemovalSkyViewRoot == null)
            return false;

        if (cardRemovalJokerGrid != null)
            return true;

        if (cardRemovalMonthGrids == null)
            return false;

        for (int i = 0; i < cardRemovalMonthGrids.Length; i++)
        {
            if (cardRemovalMonthGrids[i] != null)
                return true;
        }

        return false;
    }

    private void BuildSceneRemovalSkyLayout(List<JokerSO> jokers, List<CardSO> cards)
    {
        isUsingSceneRemovalSkyView = true;
        removalSkyRoot = cardRemovalSkyViewRoot;
        MoveCardRemovalSceneRootToOverlay();
        cardRemovalSkyViewRoot.SetActive(true);
        ClearSceneRemovalSkyView();
        HideSceneAuthoredRemovalCardArea();

        // Older scenes leave this reference empty even though the selection view
        // has its own TitleText. Do not update the unrelated pack infoText instead.
        if (cardRemovalTitleText == null)
        {
            Transform title = cardRemovalSkyViewRoot.transform.Find("TitleText");
            if (title != null)
                cardRemovalTitleText = title.GetComponent<TextMeshProUGUI>();
        }
        if (cardRemovalTitleText != null)
            cardRemovalTitleText.text = isCardUpgradeMode
                ? GameText.Get("game.PackOpeningManager.BuildSceneRemovalSkyLayout.660795b0c1", "덱에서 강화할 카드를 고르시오")
                : GameText.Get("game.PackOpeningManager.BuildSceneRemovalSkyLayout.547970907d", "덱에서 제거할 패를 고르세요");

        ApplyCardRemovalTextFont();

        BuildSceneRemovalRuntimeContent(jokers, cards);

        if (cardRemovalSkyCanvasGroup == null)
            cardRemovalSkyCanvasGroup = EnsureCanvasGroup(cardRemovalSkyViewRoot);

        ApplyCardRemovalTextFont();
        cardRemovalSkyCanvasGroup.alpha = 0f;
        cardRemovalSkyCanvasGroup.interactable = true;
        cardRemovalSkyCanvasGroup.blocksRaycasts = true;

        SendCardRemovalControlsToFront();
        Canvas.ForceUpdateCanvases();
        ForceRebuildCardRemovalRuntimeContent();
        GameLog.Info($"[카드 제거] 하늘 선택 UI 표시 준비: 조커 {CountList(jokers)}장, 일반 카드 {CountList(cards)}장");
    }

    private void MoveCardRemovalSceneRootToOverlay()
    {
        if (cardRemovalSkyViewRoot == null)
            return;

        if (!hasCardRemovalOriginalParent)
        {
            cardRemovalOriginalParent = cardRemovalSkyViewRoot.transform.parent;
            cardRemovalOriginalSiblingIndex = cardRemovalSkyViewRoot.transform.GetSiblingIndex();
            hasCardRemovalOriginalParent = true;
        }

        PreserveCardRemovalSkyBackgroundInScene();

        if (cardRemovalOverlayCanvasRoot == null)
        {
            cardRemovalOverlayCanvasRoot = new GameObject(
                "CardRemovalOverlayCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            RectTransform overlayRect = cardRemovalOverlayCanvasRoot.transform as RectTransform;
            StretchToParent(overlayRect, Vector2.zero, Vector2.zero);

            Canvas overlayCanvas = cardRemovalOverlayCanvasRoot.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = CardRemovalOverlaySortingOrder;

            CanvasScaler scaler = cardRemovalOverlayCanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        cardRemovalOverlayCanvasRoot.SetActive(true);
        cardRemovalSkyViewRoot.transform.SetParent(cardRemovalOverlayCanvasRoot.transform, false);
        cardRemovalSkyViewRoot.transform.SetAsLastSibling();
    }

    private void PreserveCardRemovalSkyBackgroundInScene()
    {
        if (cardRemovalSkyViewRoot == null || cardRemovalOriginalParent == null)
            return;

        if (cardRemovalSkyBackgroundTransform == null)
            cardRemovalSkyBackgroundTransform = FindChildRecursive(cardRemovalSkyViewRoot.transform, CardRemovalSkyDimBackgroundName);

        if (cardRemovalSkyBackgroundTransform == null)
            return;

        if (!hasCardRemovalSkyBackgroundOriginalParent)
        {
            cardRemovalSkyBackgroundOriginalParent = cardRemovalSkyBackgroundTransform.parent;
            cardRemovalSkyBackgroundOriginalSiblingIndex = cardRemovalSkyBackgroundTransform.GetSiblingIndex();
            hasCardRemovalSkyBackgroundOriginalParent = true;
        }

        cardRemovalSkyBackgroundTransform.SetParent(cardRemovalOriginalParent, false);
        int siblingIndex = Mathf.Clamp(cardRemovalOriginalSiblingIndex, 0, cardRemovalOriginalParent.childCount - 1);
        cardRemovalSkyBackgroundTransform.SetSiblingIndex(siblingIndex);

        Image backgroundImage = cardRemovalSkyBackgroundTransform.GetComponent<Image>();
        if (backgroundImage != null)
            backgroundImage.raycastTarget = false;

        cardRemovalSkyBackgroundCanvasGroup = EnsureCanvasGroup(cardRemovalSkyBackgroundTransform.gameObject);
        cardRemovalSkyBackgroundCanvasGroup.alpha = 0f;
        cardRemovalSkyBackgroundCanvasGroup.interactable = false;
        cardRemovalSkyBackgroundCanvasGroup.blocksRaycasts = false;
    }

    private void RestoreCardRemovalSceneRootParent()
    {
        if (cardRemovalSkyViewRoot != null && hasCardRemovalOriginalParent && cardRemovalOriginalParent != null)
        {
            cardRemovalSkyViewRoot.transform.SetParent(cardRemovalOriginalParent, false);
            int siblingIndex = Mathf.Clamp(cardRemovalOriginalSiblingIndex, 0, cardRemovalOriginalParent.childCount - 1);
            cardRemovalSkyViewRoot.transform.SetSiblingIndex(siblingIndex);
        }

        RestoreCardRemovalSkyBackgroundParent();

        if (cardRemovalOverlayCanvasRoot != null)
        {
            Destroy(cardRemovalOverlayCanvasRoot);
            cardRemovalOverlayCanvasRoot = null;
        }

        cardRemovalOriginalParent = null;
        cardRemovalOriginalSiblingIndex = 0;
        hasCardRemovalOriginalParent = false;
    }

    private void RestoreCardRemovalSkyBackgroundParent()
    {
        if (cardRemovalSkyBackgroundTransform != null
            && hasCardRemovalSkyBackgroundOriginalParent
            && cardRemovalSkyBackgroundOriginalParent != null)
        {
            cardRemovalSkyBackgroundTransform.SetParent(cardRemovalSkyBackgroundOriginalParent, false);
            int siblingIndex = Mathf.Clamp(
                cardRemovalSkyBackgroundOriginalSiblingIndex,
                0,
                cardRemovalSkyBackgroundOriginalParent.childCount - 1);
            cardRemovalSkyBackgroundTransform.SetSiblingIndex(siblingIndex);
        }

        if (cardRemovalSkyBackgroundCanvasGroup != null)
        {
            cardRemovalSkyBackgroundCanvasGroup.DOKill(true);
            cardRemovalSkyBackgroundCanvasGroup.alpha = 0f;
        }

        cardRemovalSkyBackgroundTransform = null;
        cardRemovalSkyBackgroundOriginalParent = null;
        cardRemovalSkyBackgroundOriginalSiblingIndex = 0;
        cardRemovalSkyBackgroundCanvasGroup = null;
        hasCardRemovalSkyBackgroundOriginalParent = false;
    }

    private void ClearSceneRemovalSkyView()
    {
        ClearSceneRemovalRuntimeContent();
        ClearGridChildren(cardRemovalJokerGrid);

        if (cardRemovalMonthGrids != null)
        {
            for (int i = 0; i < cardRemovalMonthGrids.Length; i++)
                ClearGridChildren(cardRemovalMonthGrids[i]);
        }

        if (cardRemovalSkyViewRoot != null)
            cardRemovalSkyViewRoot.transform.DOKill(true);

        if (cardRemovalSkyCanvasGroup != null)
            cardRemovalSkyCanvasGroup.DOKill(true);

        if (cardRemovalSkyBackgroundCanvasGroup != null)
        {
            cardRemovalSkyBackgroundCanvasGroup.DOKill(true);
            cardRemovalSkyBackgroundCanvasGroup.alpha = 0f;
        }
    }

    private void ClearSceneRemovalRuntimeContent()
    {
        if (cardRemovalSkyViewRoot == null)
            return;

        Transform runtimeContent = cardRemovalSkyViewRoot.transform.Find(CardRemovalRuntimeContentName);
        if (runtimeContent == null)
            return;

        runtimeContent.DOKill(true);
        PackItemUI[] items = runtimeContent.GetComponentsInChildren<PackItemUI>(true);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                items[i].transform.DOKill(true);
        }

        Destroy(runtimeContent.gameObject);
    }

    private void HideSceneAuthoredRemovalCardArea()
    {
        if (cardRemovalJokerSection != null)
            cardRemovalJokerSection.SetActive(false);

        Transform monthScrollView = FindChildRecursive(cardRemovalSkyViewRoot.transform, "MonthScrollView");
        if (monthScrollView != null)
            monthScrollView.gameObject.SetActive(false);
    }

    private void BuildSceneRemovalRuntimeContent(List<JokerSO> jokers, List<CardSO> cards)
    {
        BuildCardSelectionRuntimeContent(cardRemovalSkyViewRoot.transform, jokers, cards);
    }

    private void BuildCardSelectionRuntimeContent(Transform parent, List<JokerSO> jokers, List<CardSO> cards)
    {
        if (parent == null)
            return;

        GameObject contentRoot = CreateRectObject(CardRemovalRuntimeContentName, parent);
        RectTransform contentRect = contentRoot.transform as RectTransform;
        StretchToParent(contentRect, new Vector2(54f, 78f), new Vector2(54f, 48f));

        Image contentImage = contentRoot.AddComponent<Image>();
        contentImage.color = Color.clear;
        contentImage.raycastTarget = false;

        VerticalLayoutGroup rowsLayout = contentRoot.AddComponent<VerticalLayoutGroup>();
        rowsLayout.spacing = 12f;
        rowsLayout.childAlignment = TextAnchor.UpperCenter;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        bool hasJokers = jokers != null && jokers.Count > 0;
        rowsLayout.padding = new RectOffset(0, 0, hasJokers ? Mathf.RoundToInt(SceneRemovalMonthTopPaddingWithJoker) : 0, 0);

        if (hasJokers)
            CreateRemovalJokerColumn(contentRoot.transform, jokers, true);

        if (isCardRemovalMode)
            CreateRemovalMoneyIndicator(contentRoot.transform);

        Dictionary<_CardMonth, List<CardSO>> cardsByMonth = GroupCardsByMonth(cards);
        for (int rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            GameObject row = CreateRectObject($"RuntimeMonthRow_{rowIndex + 1}", contentRoot.transform);
            LayoutElement rowLayoutElement = row.AddComponent<LayoutElement>();
            rowLayoutElement.minHeight = 192f;
            rowLayoutElement.preferredHeight = 192f;
            rowLayoutElement.flexibleWidth = 1f;

            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.UpperCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;

            for (int monthIndex = rowIndex * 5; monthIndex < (rowIndex + 1) * 5; monthIndex++)
            {
                _CardMonth month = (_CardMonth)monthIndex;
                cardsByMonth.TryGetValue(month, out List<CardSO> monthCards);
                CreateRemovalMonthColumn(row.transform, month, monthCards);
            }
        }
    }

    private void CreateRemovalMoneyIndicator(Transform parent)
    {
        GameObject indicator = CreateRectObject("RemovalMoneyIndicator", parent);
        RectTransform indicatorRect = indicator.transform as RectTransform;
        indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
        indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
        indicatorRect.pivot = new Vector2(0.5f, 0.5f);
        // 상단 3월 열과 중앙 카드 툴팁 사이의 빈 공간입니다.
        indicatorRect.anchoredPosition = new Vector2(0f, 108f);
        indicatorRect.sizeDelta = new Vector2(190f, 50f);

        LayoutElement layoutElement = indicator.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        indicator.transform.SetAsLastSibling();

        Image background = indicator.AddComponent<Image>();
        background.color = new Color(0.025f, 0.055f, 0.09f, 0.82f);
        background.raycastTarget = false;
        Outline outline = indicator.AddComponent<Outline>();
        outline.effectColor = new Color(0.64f, 0.83f, 1f, 0.42f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject labelObject = CreateRectObject("Label", indicator.transform);
        RectTransform labelRect = labelObject.transform as RectTransform;
        StretchToParent(labelRect, new Vector2(10f, 4f), new Vector2(10f, 4f));

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        int currentMoney = EconomyManager.instance != null && EconomyManager.instance.ecoData != null
            ? EconomyManager.instance.ecoData.currentMoney
            : 0;
        label.text = GameText.Get("game.PackOpeningManager.CreateRemovalMoneyIndicator.f2f65cf84d", "<size=68%>보유 전</size>  <b>{value0}전</b>", new { value0 = currentMoney });
        label.fontSize = 24f;
        label.color = new Color(1f, 0.84f, 0.38f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        if (infoText != null && infoText.font != null)
        {
            label.font = infoText.font;
            label.UpdateFontAsset();
        }
    }

    private void SelectCardForUpgrade(CardSO card)
    {
        if (!isCardUpgradeMode || card == null || cardUpgradeWasAttempted || cardUpgradeIsEnding)
            return;

        // 강화 버튼을 누르기 전에는 다른 패를 눌러 임시 선택을 자유롭게 바꿀 수 있습니다.
        if (ReferenceEquals(selectedUpgradeCard, card))
            return;

        selectedUpgradeCard = card;
        ShowCardUpgradePanel();
    }

    private void ShowCardUpgradePanel()
    {
        ClearCardUpgradePanel();
        if (packOpeningPanel == null || selectedUpgradeCard == null)
            return;

        cardUpgradePanel = CreateRectObject("CardUpgradeRuntimePanel", packOpeningPanel.transform);
        RectTransform panelRect = cardUpgradePanel.transform as RectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -28f);
        panelRect.sizeDelta = new Vector2(380f, 230f);
        cardUpgradePanel.transform.SetAsLastSibling();

        Image background = cardUpgradePanel.AddComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.07f, 0.94f);
        Outline outline = cardUpgradePanel.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.72f, 0.28f, 0.82f);
        outline.effectDistance = new Vector2(2f, -2f);

        cardUpgradeTitleText = CreateUpgradePanelText(
            "Title",
            GameText.Get("game.PackOpeningManager.ShowCardUpgradePanel.2f26e6da76", "{value0}  <size=70%>기본 점수 {value1}</size>", new { value0 = GetRemovalCardName(selectedUpgradeCard), value1 = selectedUpgradeCard.cardScore }),
            27f,
            new Vector2(0f, 74f),
            new Vector2(340f, 38f),
            new Color(1f, 0.88f, 0.55f, 1f));

        cardUpgradeDetailText = CreateUpgradePanelText(
            "Detail",
            string.Empty,
            22f,
            new Vector2(0f, 29f),
            new Vector2(340f, 48f),
            new Color(0.88f, 0.94f, 1f, 1f));

        cardUpgradeResultText = CreateUpgradePanelText(
            "Result",
            "",
            20f,
            new Vector2(0f, -18f),
            new Vector2(340f, 42f),
            new Color(1f, 0.82f, 0.5f, 1f));

        cardUpgradeAttemptButton = CreateUpgradePanelButton(
            "AttemptButton",
            GameText.Get("game.PackOpeningManager.ShowCardUpgradePanel.fdbb96132e", "강화하기"),
            new Vector2(-88f, -82f),
            AttemptCardUpgrade);
        CreateUpgradePanelButton(
            "ExitButton",
            GameText.Get("game.PackOpeningManager.ShowCardUpgradePanel.22bfa6072a", "나가기"),
            new Vector2(88f, -82f),
            ClosePack);

        RefreshCardUpgradePanel(GameText.Get("game.PackOpeningManager.ShowCardUpgradePanel.aa8977efaa", "강화 시도는 무료입니다."));
    }

    private void AttemptCardUpgrade()
    {
        if (selectedUpgradeCard == null || cardUpgradeIsEnding)
            return;

        // 강화 시도를 누른 순간 현재 선택을 확정하고 다른 패로의 변경을 막습니다.
        SetUpgradeCardSelectionInteractable(false);

        CardUpgradeAttempt attempt = CardUpgradeService.TryUpgrade(selectedUpgradeCard);
        SoundManager.PlaySfx(data => attempt.Result == CardUpgradeResult.Success
            ? data.Upgrade_Success : data.Upgrade_Fail);
        cardUpgradeWasAttempted = attempt.Result == CardUpgradeResult.Success
            || attempt.Result == CardUpgradeResult.Failed
            || attempt.Result == CardUpgradeResult.Destroyed;

        string resultText;
        switch (attempt.Result)
        {
            case CardUpgradeResult.Success:
                resultText = GameText.Get("game.PackOpeningManager.AttemptCardUpgrade.f04b13eebf", "성공! 기본 점수가 {value0}점 증가했습니다.", new { value0 = attempt.ScoreIncrease });
                break;
            case CardUpgradeResult.Failed:
                resultText = GameText.Get("game.PackOpeningManager.AttemptCardUpgrade.720a21ee3c", "강화 실패! 패는 무사합니다. 나가기를 눌러 종료하세요.");
                break;
            case CardUpgradeResult.Destroyed:
                resultText = GameText.Get("game.PackOpeningManager.AttemptCardUpgrade.4362a81a46", "패가 파괴되어 기본 점수가 0이 되었습니다. 덱에는 남고 광/단/끗/에디션 효과는 유지되지만 더 이상 강화할 수 없습니다.");
                break;
            case CardUpgradeResult.AlreadyMaxLevel:
                resultText = GameText.Get("game.PackOpeningManager.AttemptCardUpgrade.32aabec17c", "이미 최대 강화 단계입니다.");
                break;
            default:
                resultText = GameText.Get("game.PackOpeningManager.AttemptCardUpgrade.73a457d800", "강화를 진행할 수 없습니다.");
                break;
        }

        RefreshCardUpgradePanel(resultText);

        if (attempt.Result == CardUpgradeResult.Failed || attempt.Result == CardUpgradeResult.Destroyed)
        {
            cardUpgradeIsEnding = true;
            if (cardUpgradeAttemptButton != null)
                cardUpgradeAttemptButton.interactable = false;
        }
    }

    /// <summary>강화 후보 카드들의 선택 가능 상태를 한 번에 전환합니다.</summary>
    private void SetUpgradeCardSelectionInteractable(bool interactable)
    {
        for (int i = 0; i < activeRemovalItems.Count; i++)
        {
            if (activeRemovalItems[i] != null)
                activeRemovalItems[i].SetSelectable(interactable);
        }
    }

    private void RefreshCardUpgradePanel(string resultText)
    {
        if (selectedUpgradeCard == null)
            return;

        CardUpgradeAttempt nextAttempt = CardUpgradeService.GetNextAttempt(selectedUpgradeCard);
        if (cardUpgradeTitleText != null)
        {
            cardUpgradeTitleText.text =
                GameText.Get("game.PackOpeningManager.RefreshCardUpgradePanel.2f26e6da76", "{value0}  <size=70%>기본 점수 {value1}</size>", new { value0 = GetRemovalCardName(selectedUpgradeCard), value1 = selectedUpgradeCard.cardScore });
        }

        if (cardUpgradeDetailText != null)
        {
            int effectiveSuccessChance = CardUpgradeService.GetEffectiveChancePercent(
                nextAttempt.SuccessChancePercent);
            int effectiveDestructionChance = CardUpgradeService.GetEffectiveChancePercent(
                nextAttempt.DestructionChancePercent);
            cardUpgradeDetailText.text = nextAttempt.Result == CardUpgradeResult.Failed
                ? GameText.Get("game.PackOpeningManager.RefreshCardUpgradePanel.e6473d29fd", "{value0}단계 / 성공 {value1}% / 파괴 {value2}%\n성공 시 기본 점수 +{value3}", new { value0 = nextAttempt.Level, value1 = effectiveSuccessChance, value2 = effectiveDestructionChance, value3 = nextAttempt.ScoreIncrease })
                : GameText.Get("game.PackOpeningManager.RefreshCardUpgradePanel.f4fd52d6ef", "현재 강화 {value0}단계 / 기본 점수 {value1}", new { value0 = selectedUpgradeCard.cardUpgradeLevel, value1 = selectedUpgradeCard.cardScore });
        }

        if (cardUpgradeResultText != null)
            cardUpgradeResultText.text = resultText;

        if (cardUpgradeAttemptButton != null)
        {
            bool canAttempt = nextAttempt.Result == CardUpgradeResult.Failed;
            cardUpgradeAttemptButton.interactable = canAttempt;
            TextMeshProUGUI buttonText = cardUpgradeAttemptButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
                buttonText.text = canAttempt ? GameText.Get("game.PackOpeningManager.RefreshCardUpgradePanel.bd0b288802", "계속 강화") : GameText.Get("game.PackOpeningManager.RefreshCardUpgradePanel.9a2266a48b", "강화 불가");
        }
    }

    private TextMeshProUGUI CreateUpgradePanelText(
        string objectName,
        string text,
        float fontSize,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        GameObject textObject = CreateRectObject(objectName, cardUpgradePanel.transform);
        RectTransform rect = textObject.transform as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (infoText != null && infoText.font != null)
        {
            label.font = infoText.font;
            label.UpdateFontAsset();
        }

        GameText.Style(label);
        return label;
    }

    private Button CreateUpgradePanelButton(string objectName, string text, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateRectObject(objectName, cardUpgradePanel.transform);
        RectTransform rect = buttonObject.transform as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(150f, 42f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.38f, 0.22f, 0.1f, 0.98f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI label = CreateUpgradePanelText("Label", text, 21f, Vector2.zero, rect.sizeDelta, new Color(1f, 0.91f, 0.69f, 1f));
        label.transform.SetParent(buttonObject.transform, false);
        (label.transform as RectTransform).anchoredPosition = Vector2.zero;
        return button;
    }

    private void ClearCardUpgradePanel()
    {
        cardUpgradeTitleText = null;
        cardUpgradeDetailText = null;
        cardUpgradeResultText = null;
        cardUpgradeAttemptButton = null;
        cardUpgradeIsEnding = false;

        if (cardUpgradePanel == null)
            return;

        cardUpgradePanel.transform.DOKill(true);
        cardUpgradePanel.SetActive(false);
        Destroy(cardUpgradePanel);
        cardUpgradePanel = null;
    }

    private void SendCardRemovalControlsToFront()
    {
        if (cardRemovalTitleText != null)
            cardRemovalTitleText.transform.SetAsLastSibling();

        if (cardRemovalCancelButton != null)
            cardRemovalCancelButton.transform.SetAsLastSibling();
    }

    private void ForceRebuildCardRemovalRuntimeContent()
    {
        if (removalSkyRoot == null)
            return;

        Transform runtimeContent = removalSkyRoot.transform.Find(CardRemovalRuntimeContentName);
        if (runtimeContent is RectTransform runtimeRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(runtimeRect);
    }

    private void ClearGridChildren(Transform grid)
    {
        if (grid == null)
            return;

        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            Transform child = grid.GetChild(i);
            if (child == null)
                continue;

            child.DOKill(true);
            Destroy(child.gameObject);
        }
    }

    private void CreateRemovalJokerColumn(Transform parent, List<JokerSO> jokers, bool useSceneOverlayPosition = false)
    {
        GameObject row = CreateRectObject("JokerRow", parent);
        RectTransform rowRect = row.transform as RectTransform;
        if (useSceneOverlayPosition && rowRect != null)
        {
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.offsetMin = new Vector2(0f, rowRect.offsetMin.y);
            rowRect.offsetMax = new Vector2(0f, rowRect.offsetMax.y);
            rowRect.sizeDelta = new Vector2(0f, RemovalJokerRowHeight);
            rowRect.anchoredPosition = new Vector2(0f, -SceneRemovalJokerTopOffset);
        }

        Image rowImage = row.AddComponent<Image>();
        rowImage.color = new Color(0.34f, 0.16f, 0.46f, 0.18f);
        rowImage.raycastTarget = false;
        LayoutElement rowLayoutElement = row.AddComponent<LayoutElement>();
        rowLayoutElement.ignoreLayout = useSceneOverlayPosition;
        rowLayoutElement.flexibleWidth = 1f;
        rowLayoutElement.minHeight = RemovalJokerRowHeight;
        rowLayoutElement.preferredHeight = RemovalJokerRowHeight;

        VerticalLayoutGroup rowGroup = row.AddComponent<VerticalLayoutGroup>();
        rowGroup.spacing = 6f;
        rowGroup.childControlWidth = true;
        rowGroup.childControlHeight = true;
        rowGroup.childForceExpandWidth = true;
        rowGroup.childForceExpandHeight = false;
        rowGroup.padding = new RectOffset(8, 8, 9, 22);

        if (!useSceneOverlayPosition)
        {
            ContentSizeFitter rowFitter = row.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        GameObject labelObject = CreateRectObject("JokerLabel", row.transform);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = GameText.Get("game.PackOpeningManager.CreateRemovalJokerColumn.2d4a8a8b28", "탈");
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 20f;
        label.color = new Color(1f, 0.84f, 0.36f, 0.96f);
        label.raycastTarget = false;
        LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
        labelLayout.preferredHeight = 26f;

        GameObject gridObject = CreateRectObject("JokerCards", row.transform);
        LayoutElement gridLayout = gridObject.AddComponent<LayoutElement>();
        gridLayout.preferredHeight = removalCardCellSize.y + 10f;
        gridLayout.flexibleWidth = 1f;

        List<BaseCardSO> jokerCards = new List<BaseCardSO>(jokers.Count);
        for (int i = 0; i < jokers.Count; i++)
        {
            if (jokers[i] != null)
                jokerCards.Add(jokers[i]);
        }

        CreateRemovalOverlapStack(gridObject.transform, jokerCards);
    }

    private void CreateRemovalMonthColumn(Transform parent, _CardMonth month, List<CardSO> monthCards)
    {
        GameObject column = CreateRectObject($"{GetMonthLabel(month)}Column", parent);
        Image columnImage = column.AddComponent<Image>();
        columnImage.color = removalMonthPanelTint;
        columnImage.raycastTarget = false;

        LayoutElement columnLayout = column.AddComponent<LayoutElement>();
        columnLayout.flexibleWidth = 1f;
        columnLayout.flexibleHeight = 0f;
        columnLayout.minWidth = 96f;
        columnLayout.minHeight = 178f;
        columnLayout.preferredHeight = 178f;

        VerticalLayoutGroup columnGroup = column.AddComponent<VerticalLayoutGroup>();
        columnGroup.spacing = 6f;
        columnGroup.childAlignment = TextAnchor.UpperCenter;
        columnGroup.childControlWidth = true;
        columnGroup.childControlHeight = true;
        columnGroup.childForceExpandWidth = true;
        columnGroup.childForceExpandHeight = false;
        columnGroup.padding = new RectOffset(5, 5, 6, 7);

        ContentSizeFitter columnFitter = column.AddComponent<ContentSizeFitter>();
        columnFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject labelObject = CreateRectObject("MonthLabel", column.transform);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = GetMonthLabel(month);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 20f;
        label.color = removalMonthLabelColor;
        label.raycastTarget = false;
        Outline labelOutline = labelObject.AddComponent<Outline>();
        labelOutline.effectColor = new Color(0f, 0.03f, 0.08f, 0.72f);
        labelOutline.effectDistance = new Vector2(1f, -1f);
        LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
        labelLayout.preferredHeight = 26f;

        GameObject cardGrid = CreateRectObject("Cards", column.transform);
        LayoutElement gridLayoutElement = cardGrid.AddComponent<LayoutElement>();
        gridLayoutElement.flexibleHeight = 1f;
        gridLayoutElement.flexibleWidth = 1f;
        gridLayoutElement.minHeight = removalCardCellSize.y;
        gridLayoutElement.preferredHeight = removalCardCellSize.y + 10f;

        if (monthCards == null)
            return;

        List<BaseCardSO> stackCards = new List<BaseCardSO>(monthCards.Count);
        for (int i = 0; i < monthCards.Count; i++)
        {
            if (monthCards[i] != null)
                stackCards.Add(monthCards[i]);
        }

        CreateRemovalOverlapStack(cardGrid.transform, stackCards);
    }

    private IEnumerator PlayCardSelectionIntroRoutine()
    {
        if (removalSkyRoot == null)
            yield break;

        Canvas.ForceUpdateCanvases();
        ForceRebuildCardRemovalRuntimeContent();

        CanvasGroup rootGroup = isUsingSceneRemovalSkyView && cardRemovalSkyCanvasGroup != null
            ? cardRemovalSkyCanvasGroup
            : EnsureCanvasGroup(removalSkyRoot);
        CanvasGroup sceneBackgroundGroup = isUsingSceneRemovalSkyView ? cardRemovalSkyBackgroundCanvasGroup : null;
        removalSkyRoot.transform.localScale = Vector3.one * 0.985f;
        rootGroup.DOFade(1f, ScalePresentationDuration(0.24f)).SetEase(Ease.OutSine).SetUpdate(true);
        if (sceneBackgroundGroup != null)
            sceneBackgroundGroup.DOFade(1f, ScalePresentationDuration(0.24f)).SetEase(Ease.OutSine).SetUpdate(true);
        removalSkyRoot.transform.DOScale(1f, ScalePresentationDuration(0.28f)).SetEase(Ease.OutSine).SetUpdate(true);
        yield return new WaitForSecondsRealtime(ScalePresentationDelay(0.12f));

        for (int i = 0; i < activeRemovalItems.Count; i++)
        {
            PackItemUI item = activeRemovalItems[i];
            if (item == null)
                continue;

            Transform itemTransform = item.transform;
            CanvasGroup itemGroup = EnsureCanvasGroup(item.gameObject);
            itemTransform.DOKill();
            itemGroup.DOKill();
            float revealDuration = ScalePresentationDuration(removalCardRevealDuration);
            itemGroup.DOFade(1f, revealDuration).SetEase(Ease.OutSine).SetUpdate(true);
            itemTransform.DOScale(item.RemovalBaseLocalScale, revealDuration).SetEase(Ease.OutBack).SetUpdate(true);
            itemTransform.DOLocalRotate(Vector3.zero, revealDuration).SetEase(Ease.OutSine).SetUpdate(true);

            yield return new WaitForSecondsRealtime(ScalePresentationDelay(removalCardCascadeDelay));
        }

        if (activeRemovalItems.Count == 0)
            Debug.LogWarning("[카드 제거] 표시할 카드 UI가 0개입니다. masterDeck 타입 분류 또는 packItemPrefab 연결을 확인하세요.");

        for (int i = 0; i < activeRemovalItems.Count; i++)
        {
            if (activeRemovalItems[i] != null)
                activeRemovalItems[i].SetSelectable(true);
        }
    }

    private IEnumerator PlayCardRemovalSelectionRoutine(GameObject clickedUIObject)
    {
        isRemovalResolving = true;
        if (cardRemovalCancelButton != null)
            cardRemovalCancelButton.interactable = false;

        for (int i = 0; i < activeRemovalItems.Count; i++)
        {
            PackItemUI item = activeRemovalItems[i];
            if (item == null)
                continue;

            item.SetSelectable(false);
            CanvasGroup group = EnsureCanvasGroup(item.gameObject);
            Transform itemTransform = item.transform;
            itemTransform.DOKill();
            group.DOKill();

            if (item.gameObject == clickedUIObject)
            {
                itemTransform.SetAsLastSibling();
                float selectedPopDuration = ScalePresentationDuration(removalSelectedCardDuration * 0.55f);
                itemTransform.DOScale(item.RemovalBaseLocalScale * 1.18f, selectedPopDuration).SetEase(Ease.OutBack).SetUpdate(true);
                itemTransform.DOLocalRotate(new Vector3(0f, 0f, 9f), selectedPopDuration).SetEase(Ease.OutSine).SetUpdate(true);
                continue;
            }

            group.DOFade(removalInactiveCardTint.a, ScalePresentationDuration(0.18f)).SetEase(Ease.OutSine).SetUpdate(true);
            itemTransform.DOScale(item.RemovalBaseLocalScale * 0.92f, ScalePresentationDuration(0.18f)).SetEase(Ease.OutSine).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(ScalePresentationDelay(removalSelectedCardDuration * 0.55f));

        if (clickedUIObject != null)
        {
            CanvasGroup selectedGroup = EnsureCanvasGroup(clickedUIObject);
            Transform selectedTransform = clickedUIObject.transform;
            float selectedDuration = ScalePresentationDuration(removalSelectedCardDuration);
            selectedGroup.DOFade(0f, selectedDuration).SetEase(Ease.InSine).SetUpdate(true);
            selectedTransform.DOScale(0.05f, selectedDuration).SetEase(Ease.InBack).SetUpdate(true);
            selectedTransform.DOLocalRotate(new Vector3(0f, 0f, 44f), selectedDuration).SetEase(Ease.InSine).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(ScalePresentationDelay(removalSelectedCardDuration));
        if (removalSkyRoot != null)
        {
            CanvasGroup rootGroup = isUsingSceneRemovalSkyView && cardRemovalSkyCanvasGroup != null
                ? cardRemovalSkyCanvasGroup
                : EnsureCanvasGroup(removalSkyRoot);
            CanvasGroup sceneBackgroundGroup = isUsingSceneRemovalSkyView ? cardRemovalSkyBackgroundCanvasGroup : null;
            float fadeOutDuration = ScalePresentationDuration(0.14f);
            rootGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.InSine).SetUpdate(true);
            if (sceneBackgroundGroup != null)
                sceneBackgroundGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.InSine).SetUpdate(true);
            yield return new WaitForSecondsRealtime(fadeOutDuration);
        }
        ClosePackPanel();
    }

    private void SpawnRemovalItem(BaseCardSO cardData, Transform parent)
    {
        if (cardData == null || parent == null || ResolveRemovalItemPrefab() == null)
            return;

        PackItemUI itemUI = InstantiateRemovalItemUI(parent);
        if (itemUI == null)
            return;

        GameObject itemObj = itemUI.gameObject;
        itemUI.Setup(
            cardData,
            PackType.Removal,
            GetRemovalCardSprite(cardData),
            null,
            GetRemovalCardName(cardData),
            GetRemovalCardDescription(cardData));
        itemUI.SetSelectable(false);
        EnsureLayoutElement(itemObj, removalCardCellSize);
        itemUI.ApplyRemovalCardVisualTuning();
        float visualScale = ConfigureRemovalItemVisualFrame(itemUI, removalCardCellSize);
        itemUI.SetRemovalBaseLocalScale(visualScale);

        CanvasGroup itemGroup = EnsureCanvasGroup(itemObj);
        itemGroup.alpha = 0f;
        itemObj.transform.localScale = Vector3.one * visualScale * 0.72f;
        itemObj.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-7f, 7f));
        activeRemovalItems.Add(itemUI);
    }

    private void CreateRemovalOverlapStack(Transform parent, List<BaseCardSO> cards)
    {
        if (parent == null || cards == null || cards.Count == 0 || ResolveRemovalItemPrefab() == null)
            return;

        RectTransform parentRect = parent as RectTransform;
        if (parentRect != null)
        {
            parentRect.anchorMin = new Vector2(0.5f, 0.5f);
            parentRect.anchorMax = new Vector2(0.5f, 0.5f);
            parentRect.pivot = new Vector2(0.5f, 0.5f);
            parentRect.sizeDelta = new Vector2(
                Mathf.Max(removalCardCellSize.x, removalCardStackMaxSpread + removalCardCellSize.x),
                removalCardCellSize.y + 10f);
        }

        float halfIndex = (cards.Count - 1) * 0.5f;
        float step = cards.Count > 1
            ? Mathf.Min(removalCardOverlapStep, removalCardStackMaxSpread / Mathf.Max(1, cards.Count - 1))
            : 0f;

        for (int i = 0; i < cards.Count; i++)
        {
            BaseCardSO cardData = cards[i];
            if (cardData == null)
                continue;

            PackItemUI itemUI = InstantiateRemovalItemUI(parent);
            if (itemUI == null)
                continue;

            GameObject itemObj = itemUI.gameObject;
            itemUI.Setup(
                cardData,
                PackType.Removal,
                GetRemovalCardSprite(cardData),
                null,
                GetRemovalCardName(cardData),
                GetRemovalCardDescription(cardData));
            itemUI.SetSelectable(false);
            EnsureLayoutElement(itemObj, removalCardCellSize);
            itemUI.ApplyRemovalCardVisualTuning();
            float visualScale = ConfigureRemovalItemVisualFrame(itemUI, removalCardCellSize);

            RectTransform itemRect = itemObj.transform as RectTransform;
            if (itemRect != null)
            {
                itemRect.anchorMin = new Vector2(0.5f, 0.5f);
                itemRect.anchorMax = new Vector2(0.5f, 0.5f);
                itemRect.pivot = new Vector2(0.5f, 0.5f);
                itemRect.anchoredPosition = new Vector2((i - halfIndex) * step, -Mathf.Abs(i - halfIndex) * 2f);
            }

            CanvasGroup itemGroup = EnsureCanvasGroup(itemObj);
            itemGroup.alpha = 0f;
            itemObj.transform.localScale = Vector3.one * visualScale * 0.72f;
            itemObj.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp((i - halfIndex) * removalCardStackTilt, -10f, 10f));
            itemUI.ConfigureRemovalStackInput(i, cards.Count, step, removalCardCellSize, visualScale);
            itemObj.transform.SetSiblingIndex(i);
            activeRemovalItems.Add(itemUI);
        }
    }

    private float ConfigureRemovalItemVisualFrame(PackItemUI itemUI, Vector2 targetVisualSize)
    {
        if (itemUI == null)
            return 1f;

        RectTransform itemRect = itemUI.transform as RectTransform;
        if (itemRect == null)
            return 1f;

        if (!itemUI.UsesCardVisual)
        {
            itemRect.sizeDelta = targetVisualSize;
            return 1f;
        }

        Vector2 designSize = ResolveRemovalCardDesignSize(itemRect, targetVisualSize);
        itemRect.sizeDelta = designSize;

        float scaleX = targetVisualSize.x / Mathf.Max(1f, designSize.x);
        float scaleY = targetVisualSize.y / Mathf.Max(1f, designSize.y);
        return Mathf.Max(0.01f, Mathf.Min(scaleX, scaleY));
    }

    private static Vector2 ResolveRemovalCardDesignSize(RectTransform itemRect, Vector2 fallbackTargetSize)
    {
        if (itemRect == null)
            return RemovalCardPrefabFallbackSize;

        Vector2 currentSize = itemRect.sizeDelta;
        if (currentSize.x > fallbackTargetSize.x * 1.25f && currentSize.y > fallbackTargetSize.y * 1.25f)
            return currentSize;

        Rect rect = itemRect.rect;
        if (rect.width > fallbackTargetSize.x * 1.25f && rect.height > fallbackTargetSize.y * 1.25f)
            return new Vector2(rect.width, rect.height);

        return RemovalCardPrefabFallbackSize;
    }

    private GameObject ResolveRemovalItemPrefab()
    {
        return removalCardItemPrefab != null ? removalCardItemPrefab : packItemPrefab;
    }

    private PackItemUI InstantiateRemovalItemUI(Transform parent)
    {
        GameObject prefab = ResolveRemovalItemPrefab();
        if (prefab == null || parent == null)
            return null;

        GameObject itemObj = Instantiate(prefab, parent);
        PackItemUI itemUI = itemObj.GetComponent<PackItemUI>();
        if (itemUI == null)
            itemUI = itemObj.AddComponent<PackItemUI>();

        return itemUI;
    }

    private void CreateRemovalSkyStars(Transform parent)
    {
        int count = Mathf.Max(0, removalSkyStarCount);
        for (int i = 0; i < count; i++)
        {
            GameObject star = CreateRectObject($"SkyStar_{i + 1}", parent);
            RectTransform starRect = star.transform as RectTransform;
            starRect.anchorMin = new Vector2(UnityEngine.Random.Range(0.02f, 0.98f), UnityEngine.Random.Range(0.04f, 0.96f));
            starRect.anchorMax = starRect.anchorMin;
            float size = UnityEngine.Random.Range(1.2f, 4.2f);
            starRect.sizeDelta = new Vector2(size, size);
            starRect.anchoredPosition = Vector2.zero;

            Image starImage = star.AddComponent<Image>();
            float alpha = UnityEngine.Random.Range(0.24f, removalStarTint.a);
            starImage.color = new Color(removalStarTint.r, removalStarTint.g, removalStarTint.b, alpha);
            starImage.raycastTarget = false;

            CanvasGroup starGroup = star.AddComponent<CanvasGroup>();
            starGroup.alpha = alpha;
            float pulseDuration = UnityEngine.Random.Range(1.2f, 2.4f);
            starGroup.DOFade(Mathf.Clamp01(alpha + 0.22f), pulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(UnityEngine.Random.Range(0f, 1.4f))
                .SetUpdate(true);
        }
    }

    private void UpdateCardRemovalInfoText()
    {
        if (infoText != null)
            infoText.text = GameText.Get("game.PackOpeningManager.UpdateCardRemovalInfoText.e39d1d9c38", "하늘에 펼쳐진 덱에서 제거할 패를 1장 선택하세요");
    }

    private void UpdateCardUpgradeInfoText()
    {
        if (infoText != null)
            infoText.text = GameText.Get("game.PackOpeningManager.UpdateCardUpgradeInfoText.835305f89d", "강화할 패를 선택하세요");
    }

    private void EnsureCardRemovalCancelButtonBinding()
    {
        if (cardRemovalCancelButton == null)
            return;

        cardRemovalCancelButton.onClick.RemoveListener(ClosePack);
        cardRemovalCancelButton.onClick.AddListener(ClosePack);
    }

    private void SetPackOpeningPanelShellForCardRemoval(bool enabled)
    {
        if (packOpeningPanel == null)
            return;

        EnsurePackOpeningPanelCanvasGroupState();

        if (packOpeningPanelCanvasGroup != null)
        {
            if (enabled)
            {
                packOpeningPanelCanvasGroup.alpha = 0f;
                packOpeningPanelCanvasGroup.interactable = false;
                packOpeningPanelCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                packOpeningPanelCanvasGroup.alpha = originalPackOpeningPanelCanvasAlpha;
                packOpeningPanelCanvasGroup.interactable = originalPackOpeningPanelCanvasInteractable;
                packOpeningPanelCanvasGroup.blocksRaycasts = originalPackOpeningPanelCanvasBlocksRaycasts;
            }
        }

        EnsurePackOpeningPanelImageState();
        if (packOpeningPanelImage == null)
            return;

        if (enabled)
        {
            packOpeningPanelImage.enabled = false;
            return;
        }

        packOpeningPanelImage.enabled = originalPackOpeningPanelImageEnabled;
        packOpeningPanelImage.color = originalPackOpeningPanelImageColor;
    }

    private void EnsurePackOpeningPanelImageState()
    {
        if (packOpeningPanel == null)
            return;

        if (packOpeningPanelImage == null)
            packOpeningPanelImage = packOpeningPanel.GetComponent<Image>();

        if (packOpeningPanelImage == null || hasPackOpeningPanelImageState)
            return;

        hasPackOpeningPanelImageState = true;
        originalPackOpeningPanelImageEnabled = packOpeningPanelImage.enabled;
        originalPackOpeningPanelImageColor = packOpeningPanelImage.color;
    }

    private void EnsurePackOpeningPanelCanvasGroupState()
    {
        if (packOpeningPanel == null)
            return;

        if (packOpeningPanelCanvasGroup == null)
            packOpeningPanelCanvasGroup = packOpeningPanel.GetComponent<CanvasGroup>();

        if (packOpeningPanelCanvasGroup == null)
            packOpeningPanelCanvasGroup = packOpeningPanel.AddComponent<CanvasGroup>();

        if (hasPackOpeningPanelCanvasGroupState)
            return;

        hasPackOpeningPanelCanvasGroupState = true;
        originalPackOpeningPanelCanvasAlpha = packOpeningPanelCanvasGroup.alpha;
        originalPackOpeningPanelCanvasInteractable = packOpeningPanelCanvasGroup.interactable;
        originalPackOpeningPanelCanvasBlocksRaycasts = packOpeningPanelCanvasGroup.blocksRaycasts;
    }

    private void ApplyCardRemovalTextFont()
    {
        if (cardRemovalSkyViewRoot == null || infoText == null || infoText.font == null)
            return;

        TMP_FontAsset fontAsset = infoText.font;
        TextMeshProUGUI[] texts = cardRemovalSkyViewRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
                continue;

            text.font = fontAsset;
            text.UpdateFontAsset();
        }
    }

    private void EnsureMonthGridArray()
    {
        if (cardRemovalMonthGrids == null || cardRemovalMonthGrids.Length != 10)
            Array.Resize(ref cardRemovalMonthGrids, 10);
    }

    private static Sprite GetRemovalCardSprite(BaseCardSO card)
    {
        if (card is JokerSO joker)
            return joker.jokerSprite;

        if (card is CardSO normalCard)
        {
            bool hasGwangVisual = normalCard.isGwang || normalCard.currentGwang == GwangEnhancement.Gwang;
            if (hasGwangVisual && normalCard.gwangSprite != null)
                return normalCard.gwangSprite;

            if (normalCard.normalSprite != null)
                return normalCard.normalSprite;

            if (normalCard.monthSprite != null)
                return normalCard.monthSprite;

            return normalCard.backSprite;
        }

        return null;
    }

    private static string GetRemovalCardName(BaseCardSO card)
    {
        if (card == null)
            return string.Empty;

        if (card is JokerSO joker && !string.IsNullOrWhiteSpace(joker.jokerName))
            return GameContent.Get(joker, "jokerName", joker.jokerName);

        if (!string.IsNullOrWhiteSpace(card.cardName))
            return GameContent.Get(card, "cardName", card.cardName);

        return card.name;
    }

    private static string GetRemovalCardDescription(BaseCardSO card)
    {
        if (card is JokerSO joker)
            return GameContent.Get(joker, "description", joker.description);

        if (card is CardSO normalCard)
        {
            string description = GameContent.Get(normalCard, "cardDescription", normalCard.cardDescription);
            string statLine = GameText.Get("game.PackOpeningManager.GetRemovalCardDescription.68256b8ba1", "{value0} | 점수 {value1} | 끗 {value2}", new { value0 = GetMonthLabel(normalCard.cardMonth), value1 = normalCard.cardScore, value2 = normalCard.ggeutValue });
            if (normalCard.currentDan != DanEnhancement.None)
                statLine += $" | {normalCard.currentDan}";
            if (normalCard.currentYulGgeut != YulGgeutEnhancement.None)
                statLine += $" | {normalCard.currentYulGgeut}";
            if (normalCard.isGwang || normalCard.currentGwang == GwangEnhancement.Gwang)
                statLine += GameText.Get("game.PackOpeningManager.GetRemovalCardDescription.0c18510bc5", " | 광");
            if (normalCard.currentEdition != EditionType.None)
                statLine += $" | {normalCard.currentEdition}";

            return string.IsNullOrWhiteSpace(description)
                ? statLine
                : $"{statLine}\n{description}";
        }

        return string.Empty;
    }

    private static Dictionary<_CardMonth, List<CardSO>> GroupCardsByMonth(List<CardSO> cards)
    {
        Dictionary<_CardMonth, List<CardSO>> result = new Dictionary<_CardMonth, List<CardSO>>();
        if (cards == null)
            return result;

        for (int i = 0; i < cards.Count; i++)
        {
            CardSO card = cards[i];
            if (card == null)
                continue;

            if (!result.TryGetValue(card.cardMonth, out List<CardSO> monthCards))
            {
                monthCards = new List<CardSO>();
                result.Add(card.cardMonth, monthCards);
            }

            monthCards.Add(card);
        }

        return result;
    }

    private static int CompareCardsForRemoval(CardSO x, CardSO y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x == null) return 1;
        if (y == null) return -1;

        int monthCompare = x.cardMonth.CompareTo(y.cardMonth);
        if (monthCompare != 0)
            return monthCompare;

        int scoreCompare = x.cardScore.CompareTo(y.cardScore);
        if (scoreCompare != 0)
            return scoreCompare;

        int ggeutCompare = x.ggeutValue.CompareTo(y.ggeutValue);
        if (ggeutCompare != 0)
            return ggeutCompare;

        return string.Compare(GameContent.Get(x, "cardName", x.cardName), GameContent.Get(y, "cardName", y.cardName), StringComparison.Ordinal);
    }

    private static string GetMonthLabel(_CardMonth month)
    {
        return GameText.Get("game.PackOpeningManager.GetMonthLabel.095f0ac722", "{value0}월", new { value0 = (int)month + 1 });
    }

    private static string ResolveCardPackChoiceDisplayName(CardpackSO packData)
    {
        if (packData == null)
            return GameText.Get("game.PackOpeningManager.ResolveCardPackChoiceDisplayName.59d99e7d87", "패 주머니");

        string sizeLabel = GetPackSizeLabel(packData.packSize);
        string typeLabel = GetPackTypeLabel(packData.packType);
        return string.IsNullOrWhiteSpace(sizeLabel)
            ? typeLabel
            : $"{sizeLabel} {typeLabel}";
    }

    private static string GetPackTypeLabel(PackType type)
    {
        switch (type)
        {
            case PackType.Artifact:
                return GameText.Get("game.PackOpeningManager.GetPackTypeLabel.fb4827325f", "기물 주머니");
            case PackType.Planet:
                return GameText.Get("game.PackOpeningManager.GetPackTypeLabel.e67d2656de", "신장패 주머니");
            case PackType.Amulet:
                return GameText.Get("game.PackOpeningManager.GetPackTypeLabel.3e171199aa", "부적 주머니");
            case PackType.Normal:
                return GameText.Get("game.PackOpeningManager.GetPackTypeLabel.59d99e7d87", "패 주머니");
            default:
                return GameText.Get("game.PackOpeningManager.GetPackTypeLabel.5739db1617", "주머니");
        }
    }

    private static string GetPackSizeLabel(PackSize size)
    {
        switch (size)
        {
            case PackSize.Normal:
                return GameText.Get("game.PackOpeningManager.GetPackSizeLabel.735234fcb2", "하급");
            case PackSize.Jumbo:
                return GameText.Get("game.PackOpeningManager.GetPackSizeLabel.e108f53258", "중급");
            case PackSize.Mega:
                return GameText.Get("game.PackOpeningManager.GetPackSizeLabel.ec5328d0bc", "상급");
            default:
                return string.Empty;
        }
    }

    private static int CountList<T>(List<T> list)
    {
        return list != null ? list.Count : 0;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
                continue;

            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static GameObject CreateOverlayCanvasRoot(string objectName, int sortingOrder)
    {
        GameObject canvasRoot = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        RectTransform rect = canvasRoot.transform as RectTransform;
        StretchToParent(rect, Vector2.zero, Vector2.zero);

        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvasRoot;
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        string text,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateRectObject(objectName, parent);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;

        if (infoText != null && infoText.font != null)
        {
            label.font = infoText.font;
            label.UpdateFontAsset();
        }

        GameText.Style(label);
        return label;
    }

    private static GameObject CreateRectObject(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void StretchToParent(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minOffset;
        rect.offsetMax = -maxOffset;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static LayoutElement EnsureLayoutElement(GameObject target, Vector2 size)
    {
        LayoutElement layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = target.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = size.x;
        layoutElement.preferredHeight = size.y;
        layoutElement.minWidth = size.x;
        layoutElement.minHeight = size.y;
        return layoutElement;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private static float ScalePresentationDuration(float duration)
    {
        return PresentationSpeedManager.ScaleDuration(Mathf.Max(0.01f, duration));
    }

    private static float ScalePresentationDelay(float delay)
    {
        return PresentationSpeedManager.ScaleDelay(Mathf.Max(0f, delay));
    }

    private void AcquireInputBlock()
    {
        if (hasInputBlock)
            return;

        HeadLookInputController.PushExternalInputBlock();
        hasInputBlock = true;
    }

    private void ReleaseInputBlock()
    {
        if (!hasInputBlock)
            return;

        HeadLookInputController.PopExternalInputBlock();
        hasInputBlock = false;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = activePackContentRandom != null
                ? activePackContentRandom.Range(i, list.Count)
                : UnityEngine.Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
