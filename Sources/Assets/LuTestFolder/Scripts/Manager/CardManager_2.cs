using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum HandCardLayoutMode
{
    StraightGrip,
    FanGrip
}

public struct HandCardLayoutPreviewPose
{
    public Vector3 worldPosition;
    public Quaternion worldRotation;
    public Vector3 worldScale;
}

[RequireComponent(typeof(CardSelectionPreviewPresenter))]
public class CardManager_2 : MonoBehaviour
{
    private static readonly List<RaycastResult> PointerRaycastResults = new List<RaycastResult>(32);
    private static readonly List<SpriteRenderer> CardVisualHitTestRenderers = new List<SpriteRenderer>(16);

    public static CardManager_2 Instance { get; private set; }

    [Header("기본 데이터")]
    [SerializeField] List<BaseCardSO> initialDeck; //
    [SerializeField] GameObject cardPrefab;
    // Presentation-only clones use the authored month frame and enhancement layers too.
    internal GameObject PresentationCardPrefab => cardPrefab;
    [SerializeField] Transform cardSpawnPoint;
    [SerializeField] Transform dropCardPoint;
    [SerializeField] Transform handLayoutAnchor;

    [Header("선택 프리뷰 연출 연결")]
    [SerializeField] CardSelectionPreviewPresenter selectionPreviewPresenter;

    [Header("손패 정렬 설정")]
    [SerializeField] float handLayoutWidth = 0.54f;
    [SerializeField] float cardScale = 0.19f;
    [SerializeField] float selectedOffsetY = 0.1f;
    [SerializeField] float fixedY = 0.02f;
    [SerializeField] float fixedZ = 0.08f;
    [SerializeField] float maxInterval = 0.25f;

    [Header("손패 배치 모드")]
    [SerializeField] HandCardLayoutMode handLayoutMode = HandCardLayoutMode.StraightGrip;

    [Header("임시 디버깅용")]
    [SerializeField] Vector3 debugHandLayoutOffset = new Vector3(-0.04f, -0.42f, -0.38f);
    [SerializeField] Vector3 debugHandLayoutRotation = new Vector3(-5.95f, 0f, 0f);

    [Header("전투 시점별 손패 포즈")]
    [SerializeField] bool useBattleCameraViewHandPose = true;
    [SerializeField] CameraHeadLookController battleViewPoseSource;
    [SerializeField] Vector3 battleCenterHandViewOffset = Vector3.zero;
    [SerializeField] Vector3 battleCenterHandViewRotation = Vector3.zero;
    [SerializeField] Vector3 battleQuarterHandViewOffset = new Vector3(0f, -0.33f, 0f);
    [SerializeField] Vector3 battleQuarterHandViewRotation = new Vector3(-37f, 0f, 0f);
    [SerializeField, Min(0.01f)] float handViewPoseTweenDuration = 0.35f;
    [SerializeField] Ease handViewPoseTweenEase = Ease.OutSine;

    [Header("일자 손패")]
    [SerializeField] Vector3 straightBaseRotation = Vector3.zero;
    [SerializeField] float straightInnerLift = 0.01f;
    [SerializeField] float straightCenterDepthOffset = -0.01f;
    [SerializeField] float straightOuterTilt = 3f;

    [Header("부채형 손패")]
    [SerializeField] Vector3 fanBaseRotation = Vector3.zero;
    [SerializeField] float fanArcHeight = 0.055f;
    [SerializeField] float fanCenterDepthOffset = -0.04f;
    [SerializeField] float fanMaxAngle = 18f;
    [SerializeField] float fanScaleMultiplier = 1.04f;
    [SerializeField] float fanSelectedLiftBonus = 0.01f;

    [Header("드로우 애니메이션")]
    [SerializeField] Transform startDeckPoint;
    [SerializeField] float moveDuration = 0.75f;
    [SerializeField] float drawInterval = 0.5f;
    [SerializeField, Min(0.05f)] float drawMoveDurationMultiplier = 0.65f;
    [SerializeField, Min(0.05f)] float drawIntervalMultiplier = 0.55f;
    [SerializeField] float jumpPower = 0.5f;
    //[SerializeField] float punchStrength = 0.1f;
    [SerializeField] Ease moveEase = Ease.OutBack;

    [Header("버리기 소각 연출")]
    [SerializeField, Min(0.05f)] private float discardBurnDuration = 0.58f;
    [SerializeField, Min(0f)] private float discardBurnStagger = 0.06f;
    [SerializeField, Range(0f, 1f)] private float discardBurnTintStrength = 0.78f;
    [SerializeField, ColorUsage(true, true)] private Color discardBurnTint = new Color(0.12f, 0.58f, 1f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color discardBurnEdgeColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color discardBurnCharColor = new Color(0.008f, 0.025f, 0.12f, 1f);
    [SerializeField, Range(0.001f, 0.2f)] private float discardBurnEdgeWidth = 0.028f;
    [SerializeField, Range(1f, 64f)] private float discardBurnNoiseScale = 22f;

    public List<BaseCardSO> cardBuffer;
    public List<BaseCardSO> handCardBuffer;
    public List<BaseCardSO> dropCardBuffer;

    public List<CardController> myHandCards = new List<CardController>();
    public List<CardController> selectedCards = new List<CardController>();

    private bool isCardInteractionLocked;
    private int externalCardInteractionBlockCount;
    private float cardInteractionLockStartedAt;
    private bool isDrawAnimationRunning;
    private int drawAnimationToken;
    private Tween drawAnimationEndTween;
    private int pendingPostPresentationDrawCount;
    private int suppressCardClickFrames;
    private int consumedPointerClickFrame = -1;
    private BattleHandViewPoseMode currentBattleHandViewPoseMode = BattleHandViewPoseMode.None;
    private bool deckHandPoseApplied;
    private bool handDeckPoseCaptured;
    private Vector3 handDeckOriginalPosition;
    private Quaternion handDeckOriginalRotation;
    private Matrix4x4 handDeckOriginalWorldToLocal;
    private Tween handDeckPoseTween;

    private CardDeckRuntime deckRuntime;
    private HandCardLayoutService layoutService;
    private BattleHandViewPoseResolver battleHandViewPoseResolver;
    private CardSpawnService cardSpawnService;
    private CardSelectionService cardSelectionService;
    private CardScorePreviewService cardScorePreviewService;
    private CardPlayResolutionService cardPlayResolutionService;
    private DiscardJokerEffectService discardJokerEffectService;
    private JokerAccumulatedChipStore jokerAccumulatedChipStore;

    private const float MultiCardSfxStepDelay = 0.05f;
    private const float StaleCardInteractionLockTimeout = 1.5f;
    private ScorePreviewData currentPreviewData;

    public bool CanInteractWithCards
    {
        get
        {
            CombatPresentationManager presentationManager = CombatPresentationManager.Current;
            return IsCombatCardActionAllowed()
                && !isCardInteractionLocked
                && externalCardInteractionBlockCount <= 0
                && !isDrawAnimationRunning
                && (presentationManager == null || !presentationManager.IsPlayPresentationRunning);
        }
    }

    public HandCardLayoutMode CurrentHandLayoutMode => handLayoutMode;
    public Transform HandLayoutAnchor => handLayoutAnchor != null ? handLayoutAnchor : cardSpawnPoint;
    public Transform CardSpawnParent => cardSpawnPoint != null ? cardSpawnPoint : transform;
    public Transform DropCardPoint => dropCardPoint;
    public float HandViewPoseTweenDuration => handViewPoseTweenDuration;
    public Ease HandViewPoseTweenEase => handViewPoseTweenEase;
    public int RemainingDeckCount => cardBuffer != null ? cardBuffer.Count : 0;

    private static bool IsCombatCardActionAllowed()
    {
        CombatManager combatManager = CombatManager.instance;
        return combatManager != null && combatManager.CanAcceptCardActions;
    }

    private void Awake()
    {
        Instance = this;
        EnsureRuntimeServices();

        if (selectionPreviewPresenter == null)
            selectionPreviewPresenter = GetComponentInChildren<CardSelectionPreviewPresenter>(true);

        if (selectionPreviewPresenter == null)
            selectionPreviewPresenter = gameObject.AddComponent<CardSelectionPreviewPresenter>();

        selectionPreviewPresenter.Initialize(this);
    }

    private void EnsureRuntimeServices()
    {
        cardBuffer ??= new List<BaseCardSO>();
        handCardBuffer ??= new List<BaseCardSO>();
        dropCardBuffer ??= new List<BaseCardSO>();
        myHandCards ??= new List<CardController>();
        selectedCards ??= new List<CardController>();

        deckRuntime ??= new CardDeckRuntime(cardBuffer, handCardBuffer, dropCardBuffer);
        layoutService ??= new HandCardLayoutService();
        battleHandViewPoseResolver ??= new BattleHandViewPoseResolver();
        cardSpawnService = new CardSpawnService(cardPrefab, cardSpawnPoint);
        cardSelectionService = new CardSelectionService(
            myHandCards,
            selectedCards,
            () => CanInteractWithCards,
            () => suppressCardClickFrames > 0,
            (card) => CombatManager.instance != null && 
                      CombatManager.instance.currentDebuff is DebuffJangsanbeom jang && 
                      jang.IsForcedCard(card),
            _ =>
            {
                PlayCardSfx(SoundManager.instance != null ? SoundManager.instance.sfxSoundData?.Battle_CardSelect : null);
                RefreshSelectionPreviewAndLayout();
            },
            card =>
            {
                bool deferCancelSfx = selectionPreviewPresenter != null
                    && selectionPreviewPresenter.WillPlayGhostReturn(card);
                if (!deferCancelSfx)
                    PlayGhostReturnLandingSfx();
                RefreshSelectionPreviewAndLayout();
            });
        cardScorePreviewService ??= new CardScorePreviewService();
        cardPlayResolutionService ??= new CardPlayResolutionService();
        discardJokerEffectService ??= new DiscardJokerEffectService();
        jokerAccumulatedChipStore ??= new JokerAccumulatedChipStore();
    }

    private void OnDestroy()
    {
        RestoreHandDeckPoseImmediate();
        drawAnimationEndTween?.Kill();
        drawAnimationEndTween = null;

        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        handViewPoseTweenDuration = Mathf.Max(0.01f, handViewPoseTweenDuration);
        drawMoveDurationMultiplier = Mathf.Max(0.05f, drawMoveDurationMultiplier);
        drawIntervalMultiplier = Mathf.Max(0.05f, drawIntervalMultiplier);

        if (selectionPreviewPresenter == null)
            selectionPreviewPresenter = GetComponent<CardSelectionPreviewPresenter>();

        if (selectionPreviewPresenter == null)
            selectionPreviewPresenter = gameObject.AddComponent<CardSelectionPreviewPresenter>();

        if (selectionPreviewPresenter != null)
            selectionPreviewPresenter.Initialize(this);

        if (!Application.isPlaying)
            return;

        if (myHandCards != null && myHandCards.Count > 0)
            AlignCards(false, null);
    }

    public void RefreshHandLayoutNow()
    {
        if (myHandCards != null && myHandCards.Count > 0)
            AlignCards(false, null);
    }

    public void RefreshSelectionPreviewNow()
    {
        if (selectionPreviewPresenter == null)
            selectionPreviewPresenter = GetComponent<CardSelectionPreviewPresenter>();

        if (selectionPreviewPresenter != null)
            selectionPreviewPresenter.Initialize(this);

        AlignCards(false, null);
    }

    private void Update()
    {
        RecoverStaleCardInteractionLock();

        if (suppressCardClickFrames > 0)
            suppressCardClickFrames--;

        UpdateBattleCameraViewHandPose();
        UpdateTopmostCardHoverPriority();

        if (!CanInteractWithCards)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        HandleCardPointerPressed(mouse.position.ReadValue());
    }

    private void UpdateBattleCameraViewHandPose()
    {
        BattleHandViewPoseMode desiredMode = ResolveBattleHandViewPoseMode();
        CameraHeadLookController view = ResolveBattleViewPoseSource();
        // Modal input locks (for example the ESC option window) must not change the
        // visual pose that belongs to the camera's current view node.
        bool deckPose = useBattleCameraViewHandPose && view != null
            && (view.CurrentStageContext == CameraViewSpaceStage.Battle || view.CurrentStageContext == CameraViewSpaceStage.Boss)
            && view.TryGetCurrentViewNode(out CameraViewSpaceNode node) && node != null
            && node.GridPosition == Vector2Int.down;
        bool parentPoseChanged = deckPose != deckHandPoseApplied;
        if (parentPoseChanged) SetHandDeckPose(deckPose);
        if (desiredMode == currentBattleHandViewPoseMode && !parentPoseChanged)
            return;

        currentBattleHandViewPoseMode = desiredMode;

        if (myHandCards != null && myHandCards.Count > 0)
            AlignCards(false, null);
    }

    private void SetHandDeckPose(bool deckPose)
    {
        if (cardSpawnPoint == null) return;
        handDeckPoseTween?.Kill();
        if (deckPose && !handDeckPoseCaptured)
        {
            handDeckOriginalPosition = cardSpawnPoint is RectTransform rect
                ? rect.anchoredPosition3D : cardSpawnPoint.localPosition;
            handDeckOriginalRotation = cardSpawnPoint.localRotation;
            handDeckOriginalWorldToLocal = cardSpawnPoint.worldToLocalMatrix;
            handDeckPoseCaptured = true;
        }
        deckHandPoseApplied = deckPose;
        Vector3 position = deckPose ? new Vector3(-.08f, .03f, .52f) : handDeckOriginalPosition;
        Quaternion rotation = deckPose ? Quaternion.Euler(38.312f, 0, 0) : handDeckOriginalRotation;
        Sequence sequence = DOTween.Sequence();
        sequence.Join(cardSpawnPoint is RectTransform handRect
            ? handRect.DOAnchorPos3D(position, handViewPoseTweenDuration)
            : cardSpawnPoint.DOLocalMove(position, handViewPoseTweenDuration));
        sequence.Join(cardSpawnPoint.DOLocalRotateQuaternion(rotation, handViewPoseTweenDuration));
        handDeckPoseTween = sequence.SetEase(handViewPoseTweenEase).SetUpdate(true)
            .OnComplete(() => { if (!deckPose) handDeckPoseCaptured = false; });
    }

    private void OnDisable() => RestoreHandDeckPoseImmediate();

    private void RestoreHandDeckPoseImmediate()
    {
        handDeckPoseTween?.Kill();
        handDeckPoseTween = null;
        if (!handDeckPoseCaptured || cardSpawnPoint == null) return;
        if (cardSpawnPoint is RectTransform rect) rect.anchoredPosition3D = handDeckOriginalPosition;
        else cardSpawnPoint.localPosition = handDeckOriginalPosition;
        cardSpawnPoint.localRotation = handDeckOriginalRotation;
        deckHandPoseApplied = false;
        handDeckPoseCaptured = false;
    }

    private BattleHandViewPoseMode ResolveBattleHandViewPoseMode()
    {
        EnsureRuntimeServices();
        CameraHeadLookController viewPoseSource = ResolveBattleViewPoseSource();
        return battleHandViewPoseResolver.Resolve(viewPoseSource, currentBattleHandViewPoseMode, BuildBattleHandViewPoseSettings());
    }

    private CameraHeadLookController ResolveBattleViewPoseSource()
    {
        if (battleViewPoseSource != null)
            return battleViewPoseSource;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            battleViewPoseSource = mainCamera.GetComponent<CameraHeadLookController>();

        return battleViewPoseSource;
    }

    private BattleHandViewPoseSettings BuildBattleHandViewPoseSettings()
    {
        return new BattleHandViewPoseSettings(
            useBattleCameraViewHandPose,
            battleCenterHandViewOffset,
            battleCenterHandViewRotation,
            battleQuarterHandViewOffset,
            battleQuarterHandViewRotation);
    }

    public void SetupCardBuffer()
    {
        EnsureRuntimeServices();
        ClearPreviousBattleCardVisuals();
        SetCardInteractionLocked(false);

        deckRuntime.ResetFromMasterOrInitialDeck(initialDeck);
        RefreshRemainingDeckCountUI();
        RefreshBattleTableDissolveSetup();
    }

    // Called before the battle panel is shown, without resetting deck data or drawing.
    public void ClearPreviousBattleCardVisuals()
    {
        CancelInvoke(nameof(CheckFinalDefeat));
        CancelDrawAnimation();
        pendingPostPresentationDrawCount = 0;
        suppressCardClickFrames = 0;
        selectionPreviewPresenter?.ResetAll();

        HashSet<CardController> cardsToDestroy = new HashSet<CardController>();
        if (myHandCards != null)
        {
            foreach (CardController card in myHandCards)
            {
                if (card != null)
                    cardsToDestroy.Add(card);
            }
        }

        if (selectedCards != null)
        {
            foreach (CardController card in selectedCards)
            {
                if (card != null)
                    cardsToDestroy.Add(card);
            }
        }

        foreach (CardController card in cardsToDestroy)
        {
            if (card == null)
                continue;

            card.transform.DOKill();
            DOTween.Kill(card);
            card.ResetSelection();
            // Destroy is deferred: hide immediately so board reveal cannot capture it.
            card.gameObject.SetActive(false);
            Destroy(card.gameObject);
        }

        myHandCards?.Clear();
        selectedCards?.Clear();
        currentPreviewData = null;
    }

    public List<BaseCardSO> StartDraw(int count)
    {
        EnsureRuntimeServices();
        List<BaseCardSO> drawList = deckRuntime.Draw(count);
        RefreshRemainingDeckCountUI();
        return drawList;
    }

    public void RestoreUsedCardsToDeck()
    {
        EnsureRuntimeServices();
        deckRuntime.ReturnDiscardToDeck();
        RefreshRemainingDeckCountUI();
    }

    public void DrawAndSpawn(int count)
    {
        EnsureRuntimeServices();
        List<BaseCardSO> newCards = StartDraw(count);
        deckRuntime.AddRangeToHand(newCards);

        if (newCards.Count == 0)
            return;

        SFXSoundData sfxData = SoundManager.instance != null ? SoundManager.instance.sfxSoundData : null;
        int drawToken = BeginDrawAnimation();

        for (int i = 0; i < newCards.Count; i++)
        {
            BaseCardSO card = newCards[i];
            float delay = i * GetScaledDrawInterval();
            DOVirtual.DelayedCall(delay, () => SpawnDrawCardIntoHand(card, drawToken)).SetTarget(this);

            if (sfxData != null && sfxData.Battle_CardDraw1 != null)
                PlayCardSfx(sfxData.Battle_CardDraw1, delay);
        }

        RefreshBattleTableDissolveSetup();
        ScheduleDrawAnimationEnd(newCards.Count, drawToken);

        if (CombatManager.instance != null && CombatManager.instance.currentDebuff is DebuffJangsanbeom jang)
        {
            // 카드가 손패로 날아오는 연출이 끝날 때쯤 자연스럽게 홀리도록 딜레이를 줍니다.
            float waitTime = (newCards.Count * GetScaledDrawInterval()) + GetScaledDrawMoveDuration();
            
            DOVirtual.DelayedCall(waitTime, () =>
            {
                // 씬이 넘어가거나 파괴되었으면 실행 취소
                if (this == null || !gameObject.activeInHierarchy) return;

                // 아직 강제 선택된 카드가 없거나, 이전 카드를 이미 버려서/내서 손에 없다면 새로 선택
                if (jang.forcedCard == null || !myHandCards.Contains(jang.forcedCard))
                {
                    jang.ForceSelectRandomCard();
                }
            }).SetUpdate(true);
        }
    }

    private void SpawnDrawCardIntoHand(BaseCardSO card, int drawToken)
    {
        if (drawToken != drawAnimationToken || this == null || !gameObject.activeInHierarchy)
            return;

        CardController controller = cardSpawnService.SpawnDrawCard(card, startDeckPoint);
        if (controller == null)
            return;

        CardSetting setting = controller.GetComponent<CardSetting>();
        if (setting != null)
        {
            bool isFaceUp = true;
            if (CombatManager.instance != null && CombatManager.instance.currentDebuff is DebuffImugi)
                isFaceUp = Random.value >= 0.5f;

            setting.SetFaceUp(isFaceUp);
            // 뒷면 손패는 아직 플레이어에게 공개되지 않았으므로 발견으로 기록하지 않습니다.
            if (isFaceUp)
                CollectionDiscovery.RecordCard(card);
        }

        myHandCards.Add(controller);
        AlignCards(true, new List<CardController> { controller });
    }

    private void RefreshBattleTableDissolveSetup()
    {
        if (TableTransitionManager.instance == null || TableTransitionManager.instance.battleTableRoot == null)
            return;

        UniversalDissolveController dissolveController = TableTransitionManager.instance.battleTableRoot.GetComponent<UniversalDissolveController>();
        if (dissolveController != null)
            dissolveController.RefreshSetup();
    }

    public void AlignCards(bool isDrawing = false, List<CardController> newDraw = null)
    {
        EnsureRuntimeServices();
        // AlignCards는 선택 콜백 외에도 카메라 포즈/드로우 갱신에서 직접 호출된다.
        // 호출자가 이전에 미리보기를 갱신했다는 가정 없이, 항상 현재 selectedCards를
        // 기준으로 족보 카드 목록을 만든 뒤 카드별 연출을 적용한다.
        UpdateJokboPreview();
        int count = myHandCards.Count;
        if (count == 0)
        {
            currentPreviewData = null;
            return;
        }

        HandCardLayoutSettings layoutSettings = BuildHandLayoutSettings();
        JokBoType previewJokbo = currentPreviewData != null ? currentPreviewData.jokbo : JokBoType.None;

        for (int i = 0; i < count; i++)
        {
            CardController card = myHandCards[i]; 
            bool shouldJump = isDrawing && newDraw != null && newDraw.Contains(card);
            int animationIndex = shouldJump ? newDraw.IndexOf(card) : i;
            HandCardLayoutPose pose = layoutService.BuildPose(layoutSettings, i, count, false);

            if (card == null)
                continue;

            if (card.IsSelected && TryGetGhostPreviewPose(i, count, out HandCardLayoutPreviewPose handPose))
            {
                int selectedIndex = selectedCards.IndexOf(card);
                // 점수 계산용 핵심 카드와 화면 표시용 jokboCards는 분리되어 있다.
                // 현재 족보에 실제로 포함된 카드만 테두리 연출을 활성화한다.
                bool isJokboCard = currentPreviewData != null
                    && currentPreviewData.jokbo != JokBoType.None
                    && currentPreviewData.jokboCards != null
                    && currentPreviewData.jokboCards.Contains(card);
                selectionPreviewPresenter?.PresentSelectedCard(
                    card,
                    handPose,
                    Mathf.Max(0, selectedIndex),
                    selectedCards.Count,
                    previewJokbo,
                    isJokboCard);
                continue;
            }

            selectionPreviewPresenter?.PrepareCardForHand(card);
            MoveCardWithTween(card, pose, animationIndex, shouldJump);
        }

        CardUiLayerRoot.RefreshSiblingLayers(cardSpawnPoint);
    }

    public bool TryGetPreviewPose(int index, int count, bool isSelected, out HandCardLayoutPreviewPose previewPose)
    {
        EnsureRuntimeServices();
        return layoutService.TryGetPreviewPose(BuildHandLayoutSettings(), index, count, isSelected, out previewPose);
    }

    private bool TryGetGhostPreviewPose(int index, int count, out HandCardLayoutPreviewPose previewPose)
    {
        previewPose = default;
        if (count <= 0 || index < 0 || index >= count)
            return false;

        HandCardLayoutSettings settings = BuildHandLayoutSettings();
        HandCardLayoutPose pose = layoutService.BuildPose(settings, index, count, false);
        // Ghost slots belong to the default battle view, even while the real hand
        // uses Quarter offsets or its HandDeck parent is moving to the deck view.
        BattleHandViewPoseSettings battleSettings = BuildBattleHandViewPoseSettings();
        BattleHandViewPoseMode ghostMode = currentBattleHandViewPoseMode == BattleHandViewPoseMode.None
            ? BattleHandViewPoseMode.None : BattleHandViewPoseMode.Center;
        pose.localPosition += battleHandViewPoseResolver.GetOffset(ghostMode, battleSettings) - settings.battleViewOffset;
        pose.localRotation += battleHandViewPoseResolver.GetRotation(ghostMode, battleSettings) - settings.battleViewRotation;

        Transform parent = settings.cardParent != null ? settings.cardParent : settings.layoutAnchor;
        Matrix4x4 parentMatrix = handDeckPoseCaptured && cardSpawnPoint != null
            ? handDeckOriginalWorldToLocal.inverse
            : parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
        previewPose.worldPosition = parentMatrix.MultiplyPoint3x4(pose.localPosition);
        previewPose.worldRotation = parentMatrix.rotation * Quaternion.Euler(pose.localRotation);
        previewPose.worldScale = Vector3.Scale(parentMatrix.lossyScale, pose.localScale);
        return true;
    }

    private HandCardLayoutSettings BuildHandLayoutSettings()
    {
        EnsureRuntimeServices();
        BattleHandViewPoseSettings battleSettings = BuildBattleHandViewPoseSettings();
        // The requested HandDeck parent pose supplies the deck tilt/translation; don't double it
        // with the old Quarter offsets. Preserve local hand layout under that moving parent.
        BattleHandViewPoseMode layoutMode = deckHandPoseApplied ? BattleHandViewPoseMode.Center : currentBattleHandViewPoseMode;
        Transform layoutAnchor = HandLayoutAnchor;
        Vector3 layoutPosition = transform.position;
        if (handDeckPoseCaptured && cardSpawnPoint != null)
        {
            Vector3 referencePosition = layoutAnchor != null ? layoutAnchor.position : transform.position;
            layoutPosition = cardSpawnPoint.TransformPoint(handDeckOriginalWorldToLocal.MultiplyPoint3x4(referencePosition));
            layoutAnchor = null;
        }
        return new HandCardLayoutSettings(
            layoutAnchor,
            cardSpawnPoint,
            layoutPosition,
            handLayoutMode,
            handLayoutWidth,
            cardScale,
            selectedOffsetY,
            fixedY,
            fixedZ,
            maxInterval,
            ResolveHandLayoutReferenceCount(),
            debugHandLayoutOffset,
            debugHandLayoutRotation,
            battleHandViewPoseResolver.GetOffset(layoutMode, battleSettings),
            battleHandViewPoseResolver.GetRotation(layoutMode, battleSettings),
            straightBaseRotation,
            straightInnerLift,
            straightCenterDepthOffset,
            straightOuterTilt,
            fanBaseRotation,
            fanArcHeight,
            fanCenterDepthOffset,
            fanMaxAngle,
            fanScaleMultiplier,
            fanSelectedLiftBonus);
    }

    private int ResolveHandLayoutReferenceCount()
    {
        int currentHandCount = myHandCards != null ? myHandCards.Count : 0;
        int combatMaxHandCount = CombatManager.instance != null ? CombatManager.instance.maxHandCard : 0;
        return Mathf.Max(1, currentHandCount, combatMaxHandCount);
    }

    private void MoveCardWithTween(CardController card, HandCardLayoutPose pose, int index, bool isDrawing)
    {
        if (card == null || card.gameObject == null) return;

        if (!isDrawing
            && selectionPreviewPresenter != null
            && selectionPreviewPresenter.TryPlayGhostReturnToHand(
                card,
                pose.localPosition,
                pose.localRotation,
                pose.localScale))
        {
            return;
        }

        card.transform.DOKill();


        if (isDrawing)
        {
            float delay = index * GetScaledDrawInterval();
            float scaledMoveDuration = GetScaledDrawMoveDuration();

            card.transform.DOLocalJump(pose.localPosition, jumpPower, 1, scaledMoveDuration)
                .SetDelay(delay).SetEase(moveEase);

            card.transform.DOScale(pose.localScale, scaledMoveDuration)
                .SetDelay(delay).SetEase(Ease.OutBack);

            card.transform.DOLocalRotate(pose.localRotation, scaledMoveDuration)
                .SetDelay(delay).SetEase(Ease.OutCubic);
        }
        else
        {
            float scaledHandViewDuration = GetScaledDuration(handViewPoseTweenDuration);
            card.transform.DOLocalMove(pose.localPosition, scaledHandViewDuration).SetEase(handViewPoseTweenEase);
            card.transform.DOScale(pose.localScale, scaledHandViewDuration).SetEase(handViewPoseTweenEase);
            card.transform.DOLocalRotate(pose.localRotation, scaledHandViewDuration).SetEase(handViewPoseTweenEase);
        }
    }

    private void ScheduleDrawAnimationEnd(int drawCount, int drawToken)
    {
        if (drawCount <= 0)
            return;

        float scaledMoveDuration = GetScaledDrawMoveDuration();
        float scaledDrawInterval = GetScaledDrawInterval();
        float latestEndTime = ((drawCount - 1) * scaledDrawInterval) + scaledMoveDuration;

        drawAnimationEndTween?.Kill();
        drawAnimationEndTween = DOVirtual
            .DelayedCall(latestEndTime, () => EndDrawAnimation(drawToken))
            .SetTarget(this);
    }

    private int BeginDrawAnimation()
    {
        isDrawAnimationRunning = true;
        drawAnimationToken++;

        CardTooltipPresenter.Instance?.Hide();
        ClearHandCardHoverPriority();

        return drawAnimationToken;
    }

    private void CancelDrawAnimation()
    {
        drawAnimationToken++;
        isDrawAnimationRunning = false;
        drawAnimationEndTween?.Kill();
        drawAnimationEndTween = null;
    }

    private void EndDrawAnimation(int token)
    {
        if (token != drawAnimationToken)
            return;

        isDrawAnimationRunning = false;
        drawAnimationEndTween = null;
        suppressCardClickFrames = Mathf.Max(suppressCardClickFrames, 2);
        RefreshCardPointerHover();
    }

    [ContextMenu("손패 레이아웃/일자형")]
    public void SetStraightGripLayout()
    {
        SetHandLayoutMode(HandCardLayoutMode.StraightGrip);
    }

    [ContextMenu("손패 레이아웃/부채형")]
    public void SetFanGripLayout()
    {
        SetHandLayoutMode(HandCardLayoutMode.FanGrip);
    }

    public void SetHandLayoutMode(HandCardLayoutMode layoutMode)
    {
        handLayoutMode = layoutMode;

        if (myHandCards != null && myHandCards.Count > 0)
            AlignCards(false, null);
    }

    public void OnCardSelected(CardController card) 
    { 
        EnsureRuntimeServices();
        if (!CanInteractWithCards)
            return;

        cardSelectionService.TrySelect(card);
    }
    public void OnCardDeselected(CardController card) 
    { 
        if (!CanInteractWithCards)
            return;

        if (CombatManager.instance.currentDebuff is DebuffJangsanbeom jang && jang.IsForcedCard(card))
        {
            GameLog.Info("장산범에게 홀려 이 카드는 선택을 해제할 수 없습니다!");
            return;
        }

        EnsureRuntimeServices();
        
        cardSelectionService.TryDeselect(card);
    }

    public void HandleCardClicked(CardController card)
    {
        EnsureRuntimeServices();
        if (!CanInteractWithCards)
            return;

        if (CombatManager.instance.currentDebuff is DebuffJangsanbeom jang && jang.IsForcedCard(card))
        {
            GameLog.Info("장산범에게 홀려 이 카드는 선택을 해제할 수 없습니다!");
            return;
        }
        cardSelectionService.TryHandleHandCardClick(card);
    }

    public void HandleCardClicked(CardController card, PointerEventData eventData)
    {
        EnsureRuntimeServices();
        if (!CanInteractWithCards)
            return;

        if (card != null && CombatManager.instance != null && CombatManager.instance.currentDebuff is DebuffJangsanbeom jang && jang.IsForcedCard(card))
        {
            GameLog.Info("장산범에게 홀려 이 카드는 선택을 해제할 수 없습니다!");
            return;
        }

        if (eventData != null)
        {
            HandleCardPointerPressed(eventData.position);
            return;
        }

        if (cardSelectionService.TryHandleHandCardClick(card))
            MarkPointerClickConsumed();
    }

    public bool CanHoverHandCard(CardController card, PointerEventData eventData)
    {
        EnsureRuntimeServices();
        if (!CanInteractWithCards)
            return false;

        return IsTopmostHandCardPointerTarget(card, eventData);
    }

    public void HandleGhostClicked(CardController card)
    {
        EnsureRuntimeServices();
        if (!CanInteractWithCards)
            return;

        if (consumedPointerClickFrame == Time.frameCount)
            return;

        if (CombatManager.instance.currentDebuff is DebuffJangsanbeom jang && jang.IsForcedCard(card))
        {
            GameLog.Info("장산범에게 홀려 이 카드는 선택을 해제할 수 없습니다!");
            return; 
        }

        if (cardSelectionService.TryHandleGhostClick(card))
            MarkPointerClickConsumed();
    }

    public void HandleGhostClicked(CardSelectionGhostProxy ghost, PointerEventData eventData)
    {
        EnsureRuntimeServices();
        if (!CanInteractWithCards)
            return;
        
        if (ghost != null && ghost.TargetCard != null && CombatManager.instance != null && CombatManager.instance.currentDebuff is DebuffJangsanbeom jang && jang.IsForcedCard(ghost.TargetCard))
        {
            GameLog.Info("장산범에게 홀려 이 카드는 선택을 해제할 수 없습니다!");
            return;
        }

        if (eventData != null)
        {
            HandleCardPointerPressed(eventData.position);
            return;
        }
        if (ghost == null || consumedPointerClickFrame == Time.frameCount)
            return;

        if (cardSelectionService.TryHandleGhostClick(ghost.TargetCard))
            MarkPointerClickConsumed();
    }

    public bool HandleCardPointerPressed(Vector2 screenPosition)
    {
        EnsureRuntimeServices();
        if (!CanInteractWithCards)
            return false;

        if (consumedPointerClickFrame == Time.frameCount)
            return false;

        ResolveTopmostCardInputAt(screenPosition, out CardController topHandCard, out CardSelectionGhostProxy topGhost);
        if (topGhost != null)
        {
            if (cardSelectionService.TryHandleGhostClick(topGhost.TargetCard))
            {
                MarkPointerClickConsumed();
                return true;
            }

            return false;
        }

        if (topHandCard != null)
        {
            if (cardSelectionService.TryHandleHandCardClick(topHandCard))
            {
                MarkPointerClickConsumed();
                return true;
            }

            return false;
        }

        return false;
    }

    public void RefreshCardPointerHover()
    {
        UpdateTopmostCardHoverPriority();
    }

    public bool IsTopmostGhostPointerTarget(CardSelectionGhostProxy ghost, Vector2 screenPosition)
    {
        if (ghost == null)
            return false;

        return IsTopmostCardInputTarget(
            screenPosition,
            targetCard: null,
            targetGhost: ghost);
    }

    public bool IsTopmostHandPointerTarget(CardController card, Vector2 screenPosition)
    {
        if (card == null)
            return false;

        return IsTopmostCardInputTarget(
            screenPosition,
            targetCard: card,
            targetGhost: null);
    }

    public int GetCardInputLayerIndex(CardController card)
    {
        if (card == null || myHandCards == null)
            return -1;

        int index = myHandCards.IndexOf(card);
        return index >= 0 ? myHandCards.Count - 1 - index : -1;
    }

    public bool DoesHandCardContainScreenPoint(CardController card, Vector2 screenPosition)
    {
        return card != null && DoesCardVisualContainScreenPoint(card.gameObject, screenPosition);
    }

    public bool DoesCardVisualContainScreenPoint(GameObject cardObject, Vector2 screenPosition)
    {
        if (cardObject == null)
            return false;

        CardSetting cardSetting = cardObject.GetComponent<CardSetting>();
        if (cardSetting != null)
        {
            Card3DVisual card3DVisual = cardSetting.Card3DVisual;
            if (card3DVisual != null && card3DVisual.Use3DVisual && card3DVisual.ContainsScreenPoint(screenPosition))
                return true;

            // Card3D는 BoxCollider를 단일 입력 기준으로 사용합니다.
            return false;
        }

        CardVisualHitTestRenderers.Clear();
        cardObject.GetComponentsInChildren(true, CardVisualHitTestRenderers);
        for (int i = 0; i < CardVisualHitTestRenderers.Count; i++)
        {
            if (DoesCardSpriteRendererContainScreenPoint(CardVisualHitTestRenderers[i], screenPosition))
                return true;
        }

        return DoesCardRectContainScreenPoint(cardObject.transform as RectTransform, screenPosition);
    }

    private bool IsTopmostHandCardPointerTarget(CardController card, PointerEventData eventData)
    {
        if (card == null || eventData == null)
            return true;

        return IsTopmostCardInputTarget(
            eventData.position,
            targetCard: card,
            targetGhost: null);
    }

    private bool IsTopmostGhostPointerTarget(CardSelectionGhostProxy ghost, PointerEventData eventData)
    {
        if (ghost == null || eventData == null)
            return true;

        return IsTopmostCardInputTarget(
            eventData.position,
            targetCard: null,
            targetGhost: ghost);
    }

    private bool IsTopmostCardInputTarget(Vector2 screenPosition, CardController targetCard, CardSelectionGhostProxy targetGhost)
    {
        ResolveTopmostCardInputAt(screenPosition, out CardController ruleTopHandCard, out CardSelectionGhostProxy ruleTopGhost);
        if (ruleTopHandCard != null || ruleTopGhost != null)
        {
            if (targetCard != null)
                return targetCard == ruleTopHandCard;

            if (targetGhost != null)
                return ruleTopGhost != null && targetGhost.TargetCard == ruleTopGhost.TargetCard;
        }

        if (EventSystem.current == null)
            return true;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        PointerRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, PointerRaycastResults);

        for (int i = 0; i < PointerRaycastResults.Count; i++)
        {
            Transform hitTransform = PointerRaycastResults[i].gameObject != null
                ? PointerRaycastResults[i].gameObject.transform
                : null;
            if (hitTransform == null)
                continue;

            CardSelectionGhostProxy hitGhost = hitTransform.GetComponentInParent<CardSelectionGhostProxy>();
            if (hitGhost != null)
                return targetGhost != null && hitGhost.TargetCard == targetGhost.TargetCard;

            CardController hitCard = hitTransform.GetComponentInParent<CardController>();
            if (hitCard != null)
                return targetCard != null && hitCard == targetCard;

            if (hitTransform.GetComponentInParent<CardUiLayerRoot>() != null)
                continue;

            return false;
        }

        return true;
    }

    private void ResolveTopmostCardInputAt(
        Vector2 screenPosition,
        out CardController topHandCard,
        out CardSelectionGhostProxy topGhost)
    {
        topHandCard = null;
        topGhost = null;

        // Placed cards use board render order, not their original hand/ghost order.
        if (selectionPreviewPresenter != null
            && selectionPreviewPresenter.TryGetTopmostBoardCardAtScreenPoint(screenPosition, out CardController boardCard))
        {
            topHandCard = boardCard;
            return;
        }

        int topLayerIndex = -1;

        if (myHandCards != null)
        {
            for (int i = 0; i < myHandCards.Count; i++)
            {
                CardController card = myHandCards[i];
                if (card == null || !card.isActiveAndEnabled || !card.gameObject.activeInHierarchy)
                    continue;

                if (!DoesHandCardContainScreenPoint(card, screenPosition))
                    continue;

                int cardLayerIndex = GetCardInputLayerIndex(card);
                if (cardLayerIndex > topLayerIndex)
                {
                    topLayerIndex = cardLayerIndex;
                    topHandCard = card;
                    topGhost = null;
                }
            }
        }

        if (selectionPreviewPresenter != null
            && selectionPreviewPresenter.TryGetTopmostGhostAtScreenPoint(screenPosition, out CardSelectionGhostProxy ghost)
            && ghost != null
            && ghost.TargetCard != null)
        {
            int ghostLayerIndex = GetCardInputLayerIndex(ghost.TargetCard);
            if (ghostLayerIndex >= topLayerIndex)
            {
                topLayerIndex = ghostLayerIndex;
                topHandCard = null;
                topGhost = ghost;
            }
        }
    }

    private void UpdateTopmostCardHoverPriority()
    {
        if (myHandCards == null || myHandCards.Count == 0)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            ClearHandCardHoverPriority();
            return;
        }

        ResolveTopmostCardInputAt(mouse.position.ReadValue(), out CardController topHandCard, out CardSelectionGhostProxy topGhost);
        bool canShowHandHover = CanInteractWithCards
            && topGhost == null
            && topHandCard != null
            && !topHandCard.IsSelected
            && (selectionPreviewPresenter == null
                || !selectionPreviewPresenter.IsReturningCardToHand(topHandCard));

        for (int i = 0; i < myHandCards.Count; i++)
        {
            CardController card = myHandCards[i];
            if (card == null)
                continue;

            bool isTopHandCard = canShowHandHover && card == topHandCard;
            bool keepReturnLayerPriority = !isTopHandCard
                && selectionPreviewPresenter != null
                && selectionPreviewPresenter.ShouldKeepHandHoverLayerPriority(card);
            card.ApplyPointerHoverPriority(
                isTopHandCard || keepReturnLayerPriority,
                isTopHandCard);
        }
    }

    private void ClearHandCardHoverPriority()
    {
        if (myHandCards == null)
            return;

        for (int i = 0; i < myHandCards.Count; i++)
        {
            CardController card = myHandCards[i];
            if (card != null)
                card.ApplyPointerHoverPriority(false, false);
        }
    }

    private static bool DoesCardRectContainScreenPoint(RectTransform rectTransform, Vector2 screenPosition)
    {
        if (rectTransform == null)
            return false;

        Camera eventCamera = ResolveRectTransformEventCamera(rectTransform);
        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
    }

    private static bool DoesConfiguredCardRendererContainScreenPoint(
        SpriteRenderer renderer,
        Vector2 screenPosition,
        ref bool hasConfiguredRenderer)
    {
        if (!IsUsableCardInputRenderer(renderer))
            return false;

        hasConfiguredRenderer = true;
        return DoesCardSpriteRendererContainScreenPoint(renderer, screenPosition);
    }

    private static bool DoesCardSpriteRendererContainScreenPoint(SpriteRenderer renderer, Vector2 screenPosition)
    {
        if (!IsUsableCardInputRenderer(renderer))
            return false;

        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Ray ray = camera.ScreenPointToRay(screenPosition);
        Plane rendererPlane = new Plane(renderer.transform.forward, renderer.transform.position);
        if (!rendererPlane.Raycast(ray, out float distance))
            return false;

        Vector3 localPoint = renderer.transform.InverseTransformPoint(ray.GetPoint(distance));
        Bounds bounds = renderer.sprite.bounds;
        const float localPadding = 0.01f;
        return localPoint.x >= bounds.min.x - localPadding
            && localPoint.x <= bounds.max.x + localPadding
            && localPoint.y >= bounds.min.y - localPadding
            && localPoint.y <= bounds.max.y + localPadding;
    }

    private static bool IsUsableCardInputRenderer(SpriteRenderer renderer)
    {
        return renderer != null
            && renderer.enabled
            && renderer.sprite != null
            && renderer.gameObject.activeInHierarchy;
    }

    private static Camera ResolveRectTransformEventCamera(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return Camera.main;

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        if (canvas == null)
            return Camera.main;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return Camera.main;
    }

    private void MarkPointerClickConsumed()
    {
        consumedPointerClickFrame = Time.frameCount;
        suppressCardClickFrames = Mathf.Max(suppressCardClickFrames, 2);
    }

    private void RefreshSelectionPreviewAndLayout()
    {
        AlignCards(false, null);
    }

    private void UpdateJokboPreview()
    {
        EnsureRuntimeServices();
        currentPreviewData = cardScorePreviewService.BuildSelectionPreview(selectedCards);

        if (currentPreviewData != null)
        {
            ApplyBossDebuffPreviewOverrides(currentPreviewData, currentPreviewData.jokbo);
        }

        if (FieldUIManager.Instance != null)
            FieldUIManager.Instance.UpdatePreviewJokboUI(currentPreviewData);
    }

    private void ApplyBossDebuffPreviewOverrides(ScorePreviewData previewData, JokBoType resolvedJokbo)
    {
        if (previewData == null || CombatManager.instance == null)
            return;

        if (CombatManager.instance.currentDebuff is JustPlayFirstJokbo firstJokboDebuff && firstJokboDebuff.IsJokboBlocked(resolvedJokbo))
        {
            previewData.jokboDisplayOverride = GameText.Get("audit.preview.unavailable", "사용 불가");
            previewData.jokboBonusChip = 0;
            previewData.multiplier = 0;
            previewData.finalTotalScore = 0;
            previewData.finalChipValue = 0;
            previewData.finalMultiplier = 0;
            previewData.forceFinalScoreDisplay = true;
            previewData.allowZeroMultiplierDisplay = true;
            if (previewData.cardSteps != null)
            {
                foreach (ScorePreviewCardStep cardStep in previewData.cardSteps)
                    if (cardStep != null) cardStep.chipValue = 0;
            }
            previewData.jokboCards?.Clear();
            previewData.artifactSteps?.Clear();
            previewData.scoreSteps?.Clear();
        }

        bool isVirginGhostBlocked = (CombatManager.instance.currentDebuff is DebuffVirginGhost ghost && ghost.IsScoreBlocked());
        if (isVirginGhostBlocked)
        {
            previewData.jokboDisplayOverride = GameText.Get("audit.preview.grudge", "한 맺힘 (기물 판매 필요)");
            previewData.jokboBonusChip = 0;
            previewData.multiplier = 0;
            previewData.finalTotalScore = 0;
            previewData.finalChipValue = 0;
            previewData.finalMultiplier = 0;
            previewData.forceFinalScoreDisplay = true;
            previewData.allowZeroMultiplierDisplay = true;
            if (previewData.cardSteps != null)
            {
                foreach (ScorePreviewCardStep cardStep in previewData.cardSteps)
                    if (cardStep != null) cardStep.chipValue = 0;
            }
            previewData.jokboCards?.Clear();
            previewData.artifactSteps?.Clear();
            previewData.scoreSteps?.Clear();
        }

        bool hasFaceDownCard = false;
        foreach (var card in selectedCards)
        {
            if (card != null)
            {
                CardSetting setting = card.GetComponent<CardSetting>();
                if (setting != null && !setting.isFaceUp)
                {
                    hasFaceDownCard = true;
                    break;
                }
            }
        }

        if (hasFaceDownCard)
        {
            previewData.jokboDisplayOverride = "???";
            previewData.jokboBonusChip = 0;
            previewData.multiplier = 0;
            previewData.finalTotalScore = 0;
            previewData.finalChipValue = 0;
            previewData.finalMultiplier = 0;
            previewData.forceFinalScoreDisplay = true;
            previewData.allowZeroMultiplierDisplay = true;
            if (previewData.cardSteps != null)
            {
                foreach (ScorePreviewCardStep cardStep in previewData.cardSteps)
                    if (cardStep != null) cardStep.chipValue = 0;
            }
            previewData.jokboCards?.Clear();
            previewData.artifactSteps?.Clear();
            previewData.scoreSteps?.Clear();
        }
    }

    public void OnClickPlay()
    {
        TryOnClickPlay();
    }

    public bool TryOnClickPlay()
    {
        EnsureRuntimeServices();
        if (!CanInteractWithCards)
        {
            PlayActionFailSfx();
            return false;
        }
        if (CombatManager.instance != null && CombatManager.instance.remaningHands <= 0)
        {
            PlayActionFailSfx();
            return false;
        }
        if (selectedCards.Count < 1 || selectedCards.Count > 3)
        {
            PlayActionFailSfx();
            return false;
        }

        // 복사 보스 저주에서 셀렉티드 카드 리스트 관련 제거 로직이 있어서 사용하려면 만들어야 댐
        List<CardController> cardToProcess = new List<CardController>(selectedCards);
        int playedCount = cardToProcess.Count;

        foreach (var card in cardToProcess)
        {
            if (card != null)
            {
                CardSetting setting = card.GetComponent<CardSetting>();
                if (setting != null && !setting.isFaceUp)
                {
                    setting.SetFaceUp(true); 
                    CollectionDiscovery.RecordCard(card.cardData);
                }
            }
        }

        BossDebuffSO submissionDebuff = CombatManager.instance != null
            ? CombatManager.instance.currentDebuff
            : null;
        GameLog.Info(
            $"[BossDebuffSubmit] 제출 직전 디버프: "
            + $"{(submissionDebuff != null ? submissionDebuff.name : "없음")}");
        submissionDebuff?.OnBeforeScoreCalculation(cardToProcess);

        ScoreCalculationResult finalScore;
        CombatManager.instance?.CaptureSubmissionUnlockState();
        try
        {
            finalScore = ScoreManager.Instance.CalculateTotalScore(selectedCards);
        }
        catch
        {
            submissionDebuff?.OnAfterScorePresentation();
            throw;
        }

        if (CombatManager.instance != null && !CombatManager.instance.CanAcceptCardActions)
        {
            submissionDebuff?.OnAfterScorePresentation();
            SetCardInteractionLocked(false);
            PlayActionFailSfx();
            return false;
        }

        CombatManager.instance?.PrepareRustyPliersForHand(finalScore.jokbo);

        // Presentation may burn/return non-scoring views before the hand-play trigger commits.
        var submittedCardDatas = new List<BaseCardSO>(cardToProcess.Count);
        foreach (CardController card in cardToProcess)
            if (card != null && card.cardData != null) submittedCardDatas.Add(card.cardData);

        ScorePreviewData previewData = cardScorePreviewService.BuildPlayPreview(cardToProcess, finalScore);

        if (previewData != null)
        {
            ApplyBossDebuffPreviewOverrides(previewData, finalScore.jokbo);
            AddHandPlayArtifactBonusPreview(previewData, submittedCardDatas, finalScore.totalScore);
        }
        int growthExtraDrawCount = CountGrowthExtraCards(finalScore.scoreSteps);

        SetCardInteractionLocked(true);
        selectionPreviewPresenter?.CommitSelection(cardToProcess);
        SoundManager.PlaySfx(data => data.Battle_PlayButton != null ? data.Battle_PlayButton : data.Common_Button);

        CombatPresentationManager presentationManager = CombatPresentationManager.Current;
        if (presentationManager != null)
        {
            pendingPostPresentationDrawCount = 0;
            presentationManager.PlayCardSubmitPresentation(
                cardToProcess,
                previewData,
                () => ResolvePlayedCards(cardToProcess, playedCount, finalScore.jokbo, finalScore.totalScore, finalScore.multiplier,
                    true, true, growthExtraDrawCount, submittedCardDatas),
                () =>
                {
                    submissionDebuff?.OnAfterScorePresentation();
                    SetCardInteractionLocked(false);
                    DrawPendingPostPresentationCards();
                    CombatManager.instance?.ResolvePendingPostPlayOutcome();
                });
            return true;
        }

        ResolvePlayedCards(cardToProcess, playedCount, finalScore.jokbo, finalScore.totalScore, finalScore.multiplier,
            additionalHandCards: growthExtraDrawCount, submittedCardDatas: submittedCardDatas);
        submissionDebuff?.OnAfterScorePresentation();
        SetCardInteractionLocked(false);
        return true;
    }

    public void OnClickDrop()
    {
        TryOnClickDrop();
    }

    public bool TryOnClickDrop()
    {
        EnsureRuntimeServices();
        if (!CanInteractWithCards)
        {
            PlayActionFailSfx();
            return false;
        }
        if (CombatManager.instance != null && CombatManager.instance.remaningDrops <= 0)
        {
            PlayActionFailSfx();
            return false;
        }
        if (selectedCards.Count < 1)
        {
            PlayActionFailSfx();
            return false;
        }
        int dropCount = selectedCards.Count;
        List<BaseCardSO> discardedCardDatas = new List<BaseCardSO>(dropCount);
        for (int i = 0; i < selectedCards.Count; i++)
        {
            if (selectedCards[i] != null && selectedCards[i].cardData != null)
                discardedCardDatas.Add(selectedCards[i].cardData);
        }

        CombatManager.instance.OnDropCards(dropCount, discardedCardDatas);
        List<ScoreTimelineStep> milestones = GameManager.instance != null
            ? GameManager.instance.ConsumeTriggeredMilestoneSteps() : null;
        SoundManager.PlaySfx(data => data.Battle_DropButton != null ? data.Battle_DropButton : data.Common_Button);
        ActivateDiscardPileJokerAbilities();
        selectionPreviewPresenter?.CommitSelection(selectedCards);
        
        ProcessCards(selectedCards, true, true);
        
        int drawCount = dropCount + CountGrowthExtraCards(milestones);
        if (!CombatManager.instance.isRoundCleared)
            DrawAndSpawn(drawCount);
        CombatPresentationManager presentation = CombatPresentationManager.Current;
        if (milestones != null && milestones.Count > 0 && presentation != null && presentation.isActiveAndEnabled)
        {
            SetCardInteractionLocked(true);
            presentation.PlayDiscardMilestonePresentation(milestones, completed =>
            {
                if (this != null) SetCardInteractionLocked(false);
            });
        }

        return true;
    }

    private static void PlayActionFailSfx()
    {
        SoundManager.PlaySfx(data => data.ButtonFail);
    }

    private void ResolvePlayedCards(
        List<CardController> cardToProcess,
        int playedCount,
        JokBoType jokbo,
        long totalScore,
        long multiplier,
        bool deferPostPlayOutcome = false,
        bool deferDrawUntilPresentationFinished = false,
        int additionalHandCards = 0,
        List<BaseCardSO> submittedCardDatas = null)
    {
        cardPlayResolutionService.RecordPlayedHand(jokbo, totalScore, playedCount);
        cardPlayResolutionService.TryApplyCombatPlay(jokbo, totalScore, multiplier, deferPostPlayOutcome);
        List<BaseCardSO> playedCardDatas = submittedCardDatas ?? new List<BaseCardSO>(cardToProcess.Count);
        if (submittedCardDatas == null)
        {
            for (int i = 0; i < cardToProcess.Count; i++)
                if (cardToProcess[i] != null && cardToProcess[i].cardData != null)
                    playedCardDatas.Add(cardToProcess[i].cardData);
        }

        ArtifactManager.Instance?.ProcessArtifactTrigger(
            ArtifactTriggerType.OnHandPlay,
            playedCardDatas,
            CombatManager.instance != null ? CombatManager.instance.remaningHands : 0,
            CombatManager.instance != null ? CombatManager.instance.remaningDrops : 0,
            cardBuffer != null ? cardBuffer.Count : 0,
            jokbo);
        CombatManager.instance?.SchedulePendingPostPlayOutcome(deferPostPlayOutcome);
        ProcessCards(cardToProcess, false, false);

        playedCount += ApplyRandomHandDiscardDebuff(cardToProcess);
        playedCount += additionalHandCards;

        if (!CombatManager.instance.isRoundCleared)
        {
            if (deferDrawUntilPresentationFinished)
                pendingPostPresentationDrawCount += playedCount;
            else
                DrawAndSpawn(playedCount);
        }
    }

    private static int CountGrowthExtraCards(List<ScoreTimelineStep> steps)
    {
        int count = 0;
        if (steps != null)
            foreach (ScoreTimelineStep step in steps)
                if (step != null) count += step.extraHandCards;
        return count;
    }

    private static void AddHandPlayArtifactBonusPreview(ScorePreviewData preview,
        List<BaseCardSO> cardDatas, long ordinaryScore)
    {
        CombatManager combat = CombatManager.instance;
        var context = new ArtifactEffectContext();
        context.Setup(ArtifactTriggerType.OnHandPlay, 0L, 0d, EconomyManager.instance, cardDatas,
            combat != null ? System.Math.Max(0, combat.remaningHands - 1) : 0,
            combat != null ? combat.remaningDrops : 0, 0, preview.jokbo);
        List<ScoreTimelineStep> bonuses = EffectResolver.BuildHandPlayScoreBonusSteps(context, ordinaryScore);
        foreach (ScoreTimelineStep bonus in bonuses)
        {
            // Find the artifact's slot in the existing timeline; notices never enter chips/mult.
            int insertAt = preview.scoreSteps.Count;
            for (int i = 0; i < preview.scoreSteps.Count; i++)
            {
                ScoreTimelineStep step = preview.scoreSteps[i];
                if ((step.kind == ScoreTimelineStepKind.Artifact && step.artifactInventoryIndex > bonus.artifactInventoryIndex)
                    || step.kind == ScoreTimelineStepKind.HandPassive || step.kind == ScoreTimelineStepKind.FinalLock
                    || step.kind == ScoreTimelineStepKind.Synergy)
                {
                    insertAt = i;
                    break;
                }
            }
            preview.scoreSteps.Insert(insertAt, bonus);
            if (bonus.IsTargetReduction) preview.finalTargetScore = bonus.targetScoreAfter;
            preview.finalTotalScore = ScoreSafeMath.AddScore(preview.finalTotalScore, bonus.flatScoreBonus);
            preview.artifactFlatScoreBonus = ScoreSafeMath.AddScore(preview.artifactFlatScoreBonus, bonus.flatScoreBonus);
        }
    }

    private void DrawPendingPostPresentationCards()
    {
        int drawCount = pendingPostPresentationDrawCount;
        pendingPostPresentationDrawCount = 0;

        if (drawCount <= 0)
            return;

        if (CombatManager.instance != null && CombatManager.instance.isRoundCleared)
            return;

        DrawAndSpawn(drawCount);
    }

    private int ApplyRandomHandDiscardDebuff(List<CardController> playedCards)
    {
        if (!cardPlayResolutionService.TryPickRandomHandDiscard(myHandCards, playedCards, out CardController cardToDiscard))
            return 0;

        ProcessCards(new List<CardController> { cardToDiscard }, true, true);
        UnlockManager.instance?.RecordCardsConsumed(cardToDiscard != null ? 1 : 0);
        return 1;
    }

    public void ProcessCards(List<CardController> targets, bool playDropSfx, bool showDiscardMotion = true)
    {
        EnsureRuntimeServices();
        SFXSoundData sfxData = SoundManager.instance != null ? SoundManager.instance.sfxSoundData : null;

        for (int i = 0; i < targets.Count; i++)
        {
            CardController card = targets[i];
            if (card == null)
                continue;

            bool returnedToDeck = ShouldReturnProcessedCardToDeck();
            if (returnedToDeck)
            {
                deckRuntime.ReturnHandCardToDeck(card.cardData);
                GameLog.Info("[시너지] 가택신 2: 사용하거나 버린 패가 덱으로 돌아갔습니다.");
            }
            else
            {
                deckRuntime.MoveHandCardToDiscard(card.cardData);
            }

            // 버려지는 카드가 JokerSO이고, OnCardDiscard 능력이 있다면 발동
            if (!returnedToDeck && card.cardData is JokerSO jokerSO)
            {
                DiscardJokerCard(jokerSO);
            }

            myHandCards.Remove(card);
            selectionPreviewPresenter?.RemoveCard(card);

            card.transform.DOKill(); // 버려지는 애니메이션 재생 전에 기존 딜레이 애니메이션 취소

            if (showDiscardMotion)
            {
                PlayDiscardBurnEffect(card, i);
            }
            else
            {
                // 플레이 연출에서는 묘지 이동을 따로 보여주지 않고 시스템상으로만 discard 처리합니다.
                // 다음 점수 에너지 연출이 바로 이어져야 해서 시각적 하강 모션은 생략합니다.
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }

            if (playDropSfx && sfxData != null && sfxData.Battle_CardDrop1 != null)
                PlayCardSfx(sfxData.Battle_CardDrop1, i * GetScaledDelay(MultiCardSfxStepDelay));
        }

        // 어떤 카드든 버려질 때마다, 저승에 있는 조커들의 능력을 발동시킵니다.
        //ActivateDiscardPileJokerAbilities();

        selectedCards.Clear();
        currentPreviewData = null;
        RefreshRemainingDeckCountUI();
        AlignCards(false, null);
        UpdateJokboPreview(); // 드랍시 족보 None으로 초기화해야함

        // 낼 수 있는 카드가 아예 없고(0장) 덱도 비었을 때 패배 체크
        if (cardBuffer.Count == 0 && myHandCards.Count < 1)
        {
            Invoke(nameof(CheckFinalDefeat), GetScaledDelay(0.6f));
        }
    }

    private void PlayDiscardBurnEffect(CardController card, int index)
    {
        if (card == null)
            return;

        List<Card3DSurface> surfaces = new List<Card3DSurface>(8);
        CardSetting setting = card.GetComponent<CardSetting>();
        if (setting != null)
            setting.CollectVisibleSurfaces(surfaces);

        if (surfaces.Count == 0)
            surfaces.AddRange(card.GetComponentsInChildren<Card3DSurface>(true));

        List<Color> originalColors = new List<Color>(surfaces.Count);
        for (int i = 0; i < surfaces.Count; i++)
            originalColors.Add(surfaces[i] != null ? surfaces[i].Color : Color.white);

        float delay = index * GetScaledDelay(discardBurnStagger);
        float duration = GetScaledDuration(discardBurnDuration);
        float tintDuration = Mathf.Max(0.05f, duration * 0.28f);
        float dissolveDuration = Mathf.Max(0.05f, duration * 0.72f);
        float fadeDuration = Mathf.Max(0.05f, duration * 0.24f);
        Vector3 startScale = card.transform.localScale;
        float seed = card.GetInstanceID() * 0.017f;

        Sequence burnSequence = DOTween.Sequence().SetTarget(card);
        burnSequence.AppendInterval(delay);
        burnSequence.AppendCallback(() =>
        {
            if (card == null || !card.gameObject.activeInHierarchy)
                return;

            ApplyDiscardBurnVisual(card, surfaces, originalColors, 0f, 0f, 1f, seed);

            GameObject burnEffectObject = new GameObject("CardDiscardBurnParticles_Runtime");
            // Scene-owned transient object: do not let DontSave effects survive Play Mode teardown.
            ArtifactSellBurnParticles burnParticles = burnEffectObject.AddComponent<ArtifactSellBurnParticles>();
            burnParticles.PlayCardBurnInPlace(card.transform, duration);
        });
        burnSequence.Append(
            DOVirtual.Float(0f, 1f, tintDuration, value =>
            {
                if (card == null)
                    return;

                ApplyDiscardBurnVisual(card, surfaces, originalColors, value, 0.08f * value, 1f, seed);
                card.transform.localScale = startScale * Mathf.Lerp(1f, 1.03f, value);
            })
            .SetEase(Ease.OutSine)
            .SetTarget(card));
        burnSequence.Append(
            DOVirtual.Float(0.08f, 1f, dissolveDuration, value =>
            {
                if (card == null)
                    return;

                ApplyDiscardBurnVisual(card, surfaces, originalColors, 1f, value, 1f, seed);
                card.transform.localScale = startScale * Mathf.Lerp(1.03f, 0.82f, value);
            })
            .SetEase(Ease.InCubic)
            .SetTarget(card));
        burnSequence.Append(
            DOVirtual.Float(1f, 0f, fadeDuration, value =>
            {
                if (card == null)
                    return;

                ApplyDiscardBurnVisual(card, surfaces, originalColors, 1f, 1f, value, seed);
            })
            .SetEase(Ease.InCubic)
            .SetTarget(card));
        burnSequence.OnComplete(() =>
        {
            if (card != null)
                Destroy(card.gameObject);
        });
    }

    private void ApplyDiscardBurnVisual(
        CardController card,
        List<Card3DSurface> surfaces,
        List<Color> originalColors,
        float tintAmount,
        float burnAmount,
        float opacity,
        float seed)
    {
        if (card == null || surfaces == null || originalColors == null)
            return;

        int surfaceCount = Mathf.Min(surfaces.Count, originalColors.Count);
        for (int i = 0; i < surfaceCount; i++)
        {
            Card3DSurface surface = surfaces[i];
            if (surface == null)
                continue;

            Color originalColor = originalColors[i];
            Color tintedColor = Color.Lerp(
                originalColor,
                discardBurnTint,
                Mathf.Clamp01(tintAmount) * discardBurnTintStrength);
            tintedColor.a = originalColor.a * Mathf.Clamp01(opacity);
            surface.SetColor(tintedColor);
            surface.SetCardBurnProperties(
                burnAmount,
                discardBurnEdgeColor,
                discardBurnCharColor,
                discardBurnEdgeWidth,
                discardBurnNoiseScale,
                seed + (i * 1.37f));
        }
    }

    private static bool ShouldReturnProcessedCardToDeck()
    {
        return ArtifactManager.Instance != null
            && ArtifactManager.Instance.HasSynergyTier(ArtifactSynergyType.HouseholdGod, 2)
            && UnityEngine.Random.value < ArtifactManager.Instance.ScaleSynergyChance(1f / 3f);
    }

    private void SetCardInteractionLocked(bool locked)
    {
        isCardInteractionLocked = locked;
        cardInteractionLockStartedAt = locked ? Time.unscaledTime : 0f;
    }

    public void PushExternalCardInteractionBlock()
    {
        externalCardInteractionBlockCount++;
        ClearHandCardHoverPriority();
    }

    public void PopExternalCardInteractionBlock()
    {
        externalCardInteractionBlockCount = Mathf.Max(0, externalCardInteractionBlockCount - 1);
        ClearHandCardHoverPriority();
    }

    private void RecoverStaleCardInteractionLock()
    {
        if (!isCardInteractionLocked)
            return;

        CombatPresentationManager presentationManager = CombatPresentationManager.Current;
        if (presentationManager != null && presentationManager.IsPlayPresentationRunning)
            return;

        if (Time.unscaledTime - cardInteractionLockStartedAt < StaleCardInteractionLockTimeout)
            return;

        Debug.LogWarning("[CardManager_2] 카드 입력 잠금이 제출 연출 없이 남아 있어 자동 복구합니다.");
        SetCardInteractionLocked(false);
    }

    private void CheckFinalDefeat()
    {
        CombatPresentationManager presentationManager = CombatPresentationManager.Current;
        if (presentationManager != null && presentationManager.IsPlayPresentationRunning)
        {
            Invoke(nameof(CheckFinalDefeat), GetScaledDelay(0.2f));
            return;
        }

        if (CombatManager.instance.isRoundCleared) return;

        if (CombatManager.instance.curRoundScore < CombatManager.instance.targetScore)
        {
            CombatManager.instance.GameOver();
        }
    }

    private void PlayCardSfx(AudioClip clip, float delay = 0f)
    {
        if (clip == null || SoundManager.instance == null)
            return;

        if (delay <= 0f)
        {
            SoundManager.instance.PlaySFX(clip);
            return;
        }

        DOVirtual.DelayedCall(delay, () =>
        {
            if (SoundManager.instance != null)
                SoundManager.instance.PlaySFX(clip);
        }).SetUpdate(true);
    }

    public void PlayGhostReturnLandingSfx()
    {
        PlayCardSfx(SoundManager.instance != null ? SoundManager.instance.sfxSoundData?.Battle_CardCancel1 : null);
    }

    private static float GetScaledDuration(float duration)
    {
        return PresentationSpeedManager.ScaleDuration(Mathf.Max(0.01f, duration));
    }

    private static float GetScaledDelay(float delay)
    {
        return PresentationSpeedManager.ScaleDelay(Mathf.Max(0f, delay));
    }

    private float GetScaledDrawMoveDuration()
    {
        return GetScaledDuration(moveDuration * Mathf.Max(0.05f, drawMoveDurationMultiplier));
    }

    private float GetScaledDrawInterval()
    {
        return GetScaledDelay(drawInterval * Mathf.Max(0.05f, drawIntervalMultiplier));
    }

    // 게임 시작 시 GameManager의 빈 가방(저장 덱)을 채워주는 역할
    // public void InitializeMasterDeck()
    // {
    //     if (GameManager.instance != null && initialDeck != null)
    //     {
    //         GameManager.instance.masterDeck.Clear();
    //         GameManager.instance.masterDeck.AddRange(initialDeck);
    //         GameLog.Info($"[CardManager_2] 시작 덱 {initialDeck.Count}장을 GameManager의 마스터 덱으로 지급했습니다.");
    //     }
    // }

     // Joker의 누적 칩 보너스를 추가하는 메서드
    public void AddJokerAccumulatedChip(string jokerName, int amount)
    {
        EnsureRuntimeServices();
        jokerAccumulatedChipStore.Add(jokerName, amount);
    }

    // Joker의 현재 누적 칩 보너스를 가져오는 메서드
    public int GetJokerAccumulatedChip(string jokerName)
    {
        EnsureRuntimeServices();
        return jokerAccumulatedChipStore.Get(jokerName);
    }

    // Joker 카드를 버릴 때 이벤트를 트리거하는 메서드
    public void DiscardJokerCard(JokerSO discardedJoker)
    {
        EnsureRuntimeServices();
        discardJokerEffectService.ActivateDiscardedJoker(discardedJoker);
    }

    // 저승에 있는 모든 조커 카드 중 'OnCardDiscard' 트리거에 반응하는 능력을 발동시키는 메서드
    private void ActivateDiscardPileJokerAbilities()
    {
        EnsureRuntimeServices();
        if (CombatManager.instance == null)
            return;

        discardJokerEffectService.ActivateDiscardPileJokers(
            dropCardBuffer,
            CombatManager.instance.remaningHands,
            CombatManager.instance.remaningDrops,
            RemainingDeckCount);
    }

    public void RefreshRemainingDeckCountUI()
    {
        if (FieldUIManager.Instance != null)
            FieldUIManager.Instance.UpdateRemainingDeckCountUI(RemainingDeckCount);
    }
    
    public void ForceAddCardToHand(BaseCardSO newCardData)
    {
        EnsureRuntimeServices();
        if (newCardData == null) return;

        deckRuntime.AddToHand(newCardData);
        CardController controller = cardSpawnService.SpawnHandCard(newCardData, transform);
        if (controller == null)
            return;

        // 성공적으로 앞면 손패에 추가된 생성·복제 카드만 기록합니다.
        CollectionDiscovery.RecordCard(newCardData);
        myHandCards.Add(controller);
        AlignCards(false, null); 
    }
}
