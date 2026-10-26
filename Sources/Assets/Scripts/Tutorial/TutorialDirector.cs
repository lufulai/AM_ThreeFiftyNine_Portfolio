using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 단계와 완료 조건을 관리합니다. 실제 전투/상점/이벤트 기능은 기존 시스템이 처리합니다.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class TutorialDirector : MonoBehaviour
{
    private const string TutorialConsumableResourcePath = "Amulets/Tteokguk";
    private const string TutorialArtifactID = "O_Artifact_1";
    [Header("편집 가능한 튜토리얼 UI (비우면 공용 Resources 프리팹)")]
    [SerializeField] private TutorialOverlayView overlayTemplate;

    private static readonly Key[] CameraPracticeSequence =
    {
        Key.A,
        Key.D,
        Key.D,
        Key.A,
        Key.S,
        Key.W
    };

    private static readonly string[] CameraPracticeDestinations =
    {
        "왼쪽 소모품",
        "중앙 복귀",
        "오른쪽 기물",
        "중앙 복귀",
        "아래 덱",
        "중앙 복귀"
    };

    private readonly HashSet<StageType> allowedStages = new HashSet<StageType>();

    private enum ShopGuideStep { None, Inspection, BuyArtifact, Reroll, Complete }

    private TutorialOverlayView overlay;
    private RectTransform mapNodeSpotlightProxy;
    private RectTransform worldSpotlightProxy;
    private RectTransform secondWorldSpotlightProxy;
    private MapGraphNodeView spotlightMapNode;
    private BoxCollider spotlightWorldCollider;
    private BoxCollider secondWorldSpotlightCollider;
    private Renderer spotlightWorldRenderer;
    private Transform spotlightVisualRoot;
    private ShopSlot spotlightShopSlot;
    private ShopRerollBell spotlightShopBell;
    private Button shopExitButton;
    private bool shopExitButtonWasInteractable;
    private ShopGuideStep shopGuideStep;
    private CameraHeadLookController headLookController;
    private HeadLookInputController headLookInputController;
    private Coroutine cameraDestinationGuideRoutine;
    private Coroutine stageEntryGuideRoutine;
    private Coroutine nextStageGuideRoutine;
    private bool cameraPracticeActive;
    private bool cameraDestinationGuidePending;
    private bool cameraMovePending;
    private bool cameraMoveObserved;
    private bool bossCompletionShown;
    private bool goStopGuideShown;
    private bool interestGuideShown;
    private bool ownsCameraInputOverride;
    private bool headLookInputWasEnabled;
    private int cameraPracticeStep;
    private int expectedStageIndex;
    private enum InventoryGuideStep { None, Hover, Inspect, Detail, Closing }
    private InventoryGuideStep inventoryGuideStep;
    private UnityEngine.Object inventoryGuideItem;
    private bool inventoryHoverConfirmed;
    private bool inventoryDetailConfirmed;
    private Coroutine inventoryGuideRoutine;
    private RectTransform inventoryUiTarget;
    private Canvas inventoryInfoCanvas;
    private GraphicRaycaster inventoryInfoRaycaster;
    private bool createdInventoryInfoCanvas;
    private bool createdInventoryInfoRaycaster;
    private bool inventoryInfoOverrideSorting;
    private int inventoryInfoSortingOrder;
    private float cameraMoveDeadline;
    private string cameraMoveStartNodeId;

    private void Awake()
    {
        TutorialSessionContext.Activate();
        TutorialSessionContext.SetStageEntryGuard(CanEnterStage);
        overlay = TutorialOverlayView.Create(overlayTemplate);
        overlay.SetExitAction(ConfirmExit);
        Subscribe();
    }

    private void Start()
    {
        StartCoroutine(BeginTutorialRoutine());
    }

    private void OnEnable() => GameText.Changed += RefreshCameraLanguage;
    private void OnDisable() => GameText.Changed -= RefreshCameraLanguage;

    private void RefreshCameraLanguage()
    {
        if (cameraPracticeActive && overlay != null && !overlay.IsModalVisible
            && cameraPracticeStep < CameraPracticeSequence.Length)
            ShowCameraPracticeHint();
    }

    private void OnDestroy()
    {
        GameText.Changed -= RefreshCameraLanguage;
        if (inventoryGuideRoutine != null) StopCoroutine(inventoryGuideRoutine);
        if (inventoryGuideItem != null) TutorialSessionContext.SetInventoryPractice(null);
        RestoreInventoryInfoLayer();
        if (cameraDestinationGuideRoutine != null)
            StopCoroutine(cameraDestinationGuideRoutine);
        if (stageEntryGuideRoutine != null)
            StopCoroutine(stageEntryGuideRoutine);
        if (nextStageGuideRoutine != null)
            StopCoroutine(nextStageGuideRoutine);
        RestoreCameraInputController();
        RestoreShopExitButton();
        TutorialSessionContext.SetShopHoverOverlayRoot(null);
        TutorialSessionContext.SetShopInteractionGuard(null);
        Unsubscribe();
        if (overlay != null)
            Destroy(overlay.gameObject);
    }

    private void LateUpdate()
    {
        if (spotlightMapNode != null && overlay != null && overlay.IsModalVisible)
            UpdateMapNodeSpotlightProxy(spotlightMapNode);
        if (spotlightWorldCollider != null && overlay != null && overlay.IsModalVisible)
            UpdateWorldSpotlightProxy(spotlightWorldCollider, ref worldSpotlightProxy, "WorldSpotlightTarget");
        if (secondWorldSpotlightCollider != null && overlay != null && overlay.IsModalVisible)
            UpdateWorldSpotlightProxy(secondWorldSpotlightCollider, ref secondWorldSpotlightProxy, "SecondWorldSpotlightTarget");
        if (spotlightWorldRenderer != null && overlay != null && overlay.IsModalVisible)
            UpdateRendererSpotlightProxy(spotlightWorldRenderer, ref worldSpotlightProxy, "WorldSpotlightTarget");
        if (spotlightVisualRoot != null && overlay != null && overlay.IsModalVisible)
            UpdateVisualSpotlightProxy(spotlightVisualRoot, ref worldSpotlightProxy, "WorldSpotlightTarget");
    }

    private void Update()
    {
        // 실제 호버/상세 표시 알림을 받은 다음 프레임에 진행하여 UI 콜백의 재진입을 피합니다.
        if (inventoryGuideStep != InventoryGuideStep.None)
        {
            UpdateInventoryGuide();
            return;
        }
        if (!cameraPracticeActive || cameraDestinationGuidePending || overlay == null || overlay.IsModalVisible)
            return;

        Keyboard keyboard = Keyboard.current;
        CameraHeadLookController headLook = ResolveHeadLookController();
        if (keyboard == null || headLook == null)
            return;

        if (cameraMovePending)
        {
            UpdatePendingCameraMove(headLook);
            return;
        }

        if (headLook.IsCameraMoving || cameraPracticeStep >= CameraPracticeSequence.Length)
            return;

        Key requiredKey = CameraPracticeSequence[cameraPracticeStep];
        KeyControl requiredControl = GetKeyControl(keyboard, requiredKey);
        if (requiredControl == null || !requiredControl.wasPressedThisFrame)
            return;

        if (!headLook.TryGetCurrentViewNode(out CameraViewSpaceNode currentNode) || currentNode == null)
            return;

        bool moveStarted = headLook.MoveInDirection(ToDirection(requiredKey));
        if (!moveStarted)
        {
            ShowCameraPracticeHint(GameText.Get("game.TutorialDirector.Update.5654b89ca1", "현재 위치에서는 이동하지 않았습니다. 안내된 키를 다시 눌러 주세요."));
            return;
        }

        cameraMovePending = true;
        cameraMoveObserved = headLook.IsCameraMoving;
        cameraMoveDeadline = Time.unscaledTime + 1.5f;
        cameraMoveStartNodeId = currentNode.Id;
        ShowCameraPracticeHint(GameText.Get("game.TutorialDirector.Update.b211d77158", "시점 이동을 확인하고 있습니다."));
    }

    private IEnumerator BeginTutorialRoutine()
    {
        while (!TutorialSceneBootstrap.IsGameplayReady && !TutorialSceneBootstrap.HasLoadFailed)
            yield return null;

        if (TutorialSceneBootstrap.HasLoadFailed)
        {
            overlay.ShowModal(
                GameText.Get("game.TutorialDirector.BeginTutorialRoutine.0dc313b573", "튜토리얼 로드 실패"),
                GameText.Get("game.TutorialDirector.BeginTutorialRoutine.396d54d3d5", "게임플레이 씬을 준비하지 못했습니다. 타이틀로 돌아갑니다."),
                GameText.Get("game.TutorialDirector.BeginTutorialRoutine.ebf1eb5c33", "타이틀로"),
                () => TutorialSessionContext.RequestExit(false));
            yield break;
        }

        // Additive RealGame 로드 직후에는 타이틀에서 넘겨온 투명 페이드 Canvas가
        // 화면보다 높은 정렬 순서로 입력을 막고 있을 수 있으므로 전환 종료를 기다립니다.
        while (TitleSceneController.IsPreparingRealGame)
            yield return null;

        // RealGame 씬의 EventSystem은 기본 비활성 상태입니다. 타이틀 씬이 내려간 뒤에는
        // 자동으로 활성화되지 않으므로 직접 복구해야 첫 안내와 종료 버튼이 입력을 받습니다.
        TitleSceneController.EnsureEventSystem();

        yield return null;
        yield return null;
        overlay.ShowModal(
            GameText.Get("game.TutorialDirector.BeginTutorialRoutine.18eb9959d2", "제비의 안내"),
            GameText.Get("game.TutorialDirector.BeginTutorialRoutine.4a06ebc6c3", "선택한 두 탈은 이번 여정의 규칙과 시작 효과를 바꿉니다.\n이곳은 일반 여정과 분리된 연습 공간이므로 기존 이어하기와 해금 기록은 변경되지 않습니다."),
            GameText.Get("game.TutorialDirector.BeginTutorialRoutine.f9749a83fe", "시점 연습"),
            BeginCameraPractice);
    }

    private void BeginCameraPractice()
    {
        headLookController = ResolveHeadLookController();
        TakeCameraInputController();
        cameraPracticeStep = 0;
        cameraMovePending = false;
        cameraMoveObserved = false;
        cameraMoveStartNodeId = string.Empty;
        cameraDestinationGuidePending = false;
        cameraPracticeActive = true;
        if (headLookController != null
            && !headLookController.IsAtStageDefaultView
            && !headLookController.IsCameraMoving)
        {
            headLookController.MoveToDefaultNode();
        }
        ShowCameraPracticeHint();
    }

    private void ShowCameraPracticeHint(string status = null)
    {
        Key nextKey = CameraPracticeSequence[Mathf.Clamp(
            cameraPracticeStep,
            0,
            CameraPracticeSequence.Length - 1)];
        overlay.ShowHint(
            GameText.Get("game.TutorialDirector.ShowCameraPracticeHint.0900426e33", "시점 이동 연습"),
            GameText.Get("game.TutorialDirector.ShowCameraPracticeHint.6a929f736a", "왼쪽 체크리스트에서 완료한 이동을 확인할 수 있습니다.\n빨간 체크는 미완료, 초록 체크는 완료입니다.\n{value0}", new { value0 = (string.IsNullOrWhiteSpace(status)
                ? GameText.Get("tutorial.camera.next_key", "지금 누를 키: {key} ({destination})", new { key = nextKey, destination = CameraDestination(cameraPracticeStep) })
                : status) }));
        RefreshCameraProgress();
    }

    private void RefreshCameraProgress()
    {
        string[] labels = new string[CameraPracticeSequence.Length];
        for (int i = 0; i < CameraPracticeSequence.Length; i++)
            labels[i] = GameText.Get("tutorial.camera.step_label", "{key} / {destination}", new { key = CameraPracticeSequence[i], destination = CameraDestination(i) });
        overlay.SetCameraProgress(labels, cameraPracticeStep);
    }

    private static string CameraDestination(int step)
        => GameText.Get("tutorial.camera.destination." + step, CameraPracticeDestinations[step]);

    /// <summary>키 입력 횟수가 아니라 실제 카메라 이동 완료와 도착 방향을 확인해 연습 단계를 넘깁니다.</summary>
    private void UpdatePendingCameraMove(CameraHeadLookController headLook)
    {
        if (headLook.IsCameraMoving)
        {
            cameraMoveObserved = true;
            return;
        }

        if (!cameraMoveObserved)
        {
            if (Time.unscaledTime >= cameraMoveDeadline)
            {
                cameraMovePending = false;
                ShowCameraPracticeHint(GameText.Get("game.TutorialDirector.UpdatePendingCameraMove.5654b89ca1", "현재 위치에서는 이동하지 않았습니다. 안내된 키를 다시 눌러 주세요."));
            }
            return;
        }

        bool reachedDifferentNode = headLook.TryGetCurrentViewNode(out CameraViewSpaceNode currentNode)
            && currentNode != null
            && !string.Equals(currentNode.Id, cameraMoveStartNodeId, System.StringComparison.Ordinal);
        Vector2Int expectedDirection = ToDirection(CameraPracticeSequence[cameraPracticeStep]);
        bool movedAsRequested = reachedDifferentNode && headLook.LastMoveDirection == expectedDirection;

        cameraMovePending = false;
        cameraMoveObserved = false;
        cameraMoveStartNodeId = string.Empty;

        if (!movedAsRequested)
        {
            ShowCameraPracticeHint(GameText.Get("game.TutorialDirector.UpdatePendingCameraMove.521ab6c864", "시점 이동 완료를 확인하지 못했습니다. 안내된 키를 다시 눌러 주세요."));
            return;
        }

        int completedStep = cameraPracticeStep;
        cameraPracticeStep++;
        RefreshCameraProgress();
        if (TryShowCameraDestinationGuide(completedStep))
            return;

        ContinueOrCompleteCameraPractice();
    }

    private bool TryShowCameraDestinationGuide(int completedStep)
    {
        switch (completedStep)
        {
            case 0:
            {
                AmuletSO example = EnsureTutorialConsumable();
                string exampleText = example != null
                    ? GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.0d81417aeb", "지금 보이는 '{value0}'은 연습용으로 넣어 둔 소모품입니다.", new { value0 = example.cardName })
                    : GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.5f8bd9572e", "여정 중 소모품을 얻으면 이곳에 놓입니다.");
                overlay.ShowModal(
                    GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.1f86410ad6", "소모품 보관함"),
                    GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.7a2dfb6824", "여기는 여정 중 얻은 소모품을 보관하는 공간입니다. 소모품은 사용하면 사라지는 대신 즉시 효과를 내며, 기본 두 개까지 보관할 수 있습니다.\n{value0} 마우스를 올리면 효과를, 클릭하면 상세 정보와 사용/판매 방법을 확인할 수 있습니다.", new { value0 = exampleText }),
                    GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.708afe10aa", "소모품 살펴보기"),
                    () => BeginInventoryGuide(example));
                return true;
            }
            case 2:
            {
                ArtifactData example = EnsureTutorialArtifact();
                string exampleText = example != null
                    ? GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.faa467ac65", "지금 보이는 '{value0}'은 연습용으로 넣어 둔 기물입니다.", new { value0 = example.artifactName })
                    : GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.f02abc3759", "여정 중 기물을 얻으면 이곳에 놓입니다.");
                overlay.ShowModal(
                    GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.9873a2ca3e", "기물 보관 공간"),
                    GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.df9b940a6e", "기물은 소모품과 달리 보유하는 동안 조건에 맞춰 계속 효과를 냅니다. 서로 다른 기물을 조합하면 전투 점수와 여정 운영 방식이 달라집니다.\n{value0} 마우스를 올리면 효과를 확인하고, 클릭하면 상세 정보와 판매 선택지를 볼 수 있습니다.", new { value0 = exampleText }),
                    GameText.Get("game.TutorialDirector.TryShowCameraDestinationGuide.fe4decc0fd", "기물 살펴보기"),
                    () => BeginInventoryGuide(example));
                return true;
            }
            case 4:
                cameraDestinationGuidePending = true;
                overlay.Hide();
                cameraDestinationGuideRoutine = StartCoroutine(ShowDeckDestinationGuideWhenReady());
                return true;
            default:
                return false;
        }
    }

    /// <summary>실제 인벤토리 오브젝트가 등장한 뒤 호버 → 상세 확인 체험을 시작합니다.</summary>
    private void BeginInventoryGuide(UnityEngine.Object example)
    {
        if (example == null) { ContinueOrCompleteCameraPractice(); return; }
        inventoryGuideItem = example;
        inventoryHoverConfirmed = inventoryDetailConfirmed = false;
        cameraDestinationGuidePending = true;
        inventoryGuideStep = InventoryGuideStep.Hover;
        TutorialSessionContext.SetInventoryPractice(example);
        inventoryGuideRoutine = StartCoroutine(PrepareInventoryGuide());
    }

    private IEnumerator PrepareInventoryGuide()
    {
        // 앞 설명 버튼의 클릭이 체험 대상까지 관통하지 않도록 입력이 끝나기를 기다립니다.
        yield return null;
        while (Mouse.current != null && Mouse.current.leftButton.isPressed) yield return null;
        float deadline = Time.unscaledTime + 6f;
        while (!TryResolveInventoryTarget() && Time.unscaledTime < deadline) yield return null;
        inventoryGuideRoutine = null;
        PromoteInventoryInfoLayer();
        if (inventoryUiTarget == null && spotlightVisualRoot == null)
        {
            overlay.ShowModal(GameText.Get("game.TutorialDirector.PrepareInventoryGuide.0e7ec0fbbd", "인벤토리 확인"), GameText.Get("game.TutorialDirector.PrepareInventoryGuide.a63b41c839", "표시 대상을 찾지 못했습니다. 소모품과 기물은 마우스를 올려 효과를 확인하고, 클릭해 상세 정보를 열 수 있습니다."),
                GameText.Get("game.TutorialDirector.PrepareInventoryGuide.4f52aa1388", "다음 이동 안내"), FinishInventoryGuide);
            inventoryGuideStep = InventoryGuideStep.Detail;
            yield break;
        }
        ShowInventoryInteractionGuide(false);
    }

    private bool TryResolveInventoryTarget()
    {
        if (inventoryGuideItem is AmuletSO amulet)
        {
            // 하이라이키 기반 UI 슬롯과 월드 모델 경로를 모두 지원합니다.
            foreach (ConsumableSlotUI slot in FindObjectsByType<ConsumableSlotUI>(FindObjectsSortMode.None))
                if (ReferenceEquals(slot.CurrentAmulet, amulet) && slot.transform is RectTransform rect)
                { inventoryUiTarget = rect; return true; }
            foreach (ConsumableObject item in FindObjectsByType<ConsumableObject>(FindObjectsSortMode.None))
                if (ReferenceEquals(item.Data, amulet) && !item.IsDelayedAppearHidden)
                { spotlightVisualRoot = item.transform; return true; }
        }
        else if (inventoryGuideItem is ArtifactData artifact)
        {
            foreach (ArtifactObject item in FindObjectsByType<ArtifactObject>(FindObjectsSortMode.None))
                if (ReferenceEquals(item.data, artifact) && item.isInteractable)
                { spotlightVisualRoot = item.transform; return true; }
        }
        return false;
    }

    private RectTransform[] InventoryGuideTargets()
    {
        if (inventoryUiTarget != null) return new[] { inventoryUiTarget };
        if (UpdateVisualSpotlightProxy(spotlightVisualRoot, ref worldSpotlightProxy, "InventorySpotlightTarget"))
            return new[] { worldSpotlightProxy };
        return System.Array.Empty<RectTransform>();
    }

    private void ShowInventoryInteractionGuide(bool click)
    {
        bool consumable = inventoryGuideItem is AmuletSO;
        string kind = consumable ? GameText.Get("game.TutorialDirector.ShowInventoryInteractionGuide.905e5ba805", "소모품") : GameText.Get("game.TutorialDirector.ShowInventoryInteractionGuide.7bada8867d", "기물");
        string body = click
            ? GameText.Get("game.TutorialDirector.ShowInventoryInteractionGuide.206bb01791", "밝게 표시된 {value0}을 클릭해 상세 정보를 열어 보세요. 이 단계에서는 사용/판매/재배치를 하지 않습니다.", new { value0 = kind })
            : GameText.Get("game.TutorialDirector.ShowInventoryInteractionGuide.43b00e24ab", "밝게 표시된 {value0}에 마우스를 올려 효과 설명을 확인해 보세요. 호버를 확인한 뒤 클릭 안내로 넘어갑니다.", new { value0 = kind });
        string title = click
            ? GameText.Get("tutorial.inventory.inspect_title", "{kind} 상세 확인", new { kind })
            : GameText.Get("tutorial.inventory.hover_title", "{kind} 살펴보기", new { kind });
        overlay.SetGuideSourceTitle((consumable ? "소모품" : "기물") + (click ? " 상세 확인" : " 살펴보기"));
        overlay.ShowInteractiveSpotlight(title, body,
            InventoryGuideTargets(), TutorialOverlayView.GuidePanelPlacement.Top, 12f, true);
    }

    private void HandleInventoryHovered(UnityEngine.Object item)
    {
        if (ReferenceEquals(item, inventoryGuideItem)) inventoryHoverConfirmed = true;
    }

    private void HandleInventoryInspected(UnityEngine.Object item)
    {
        if (ReferenceEquals(item, inventoryGuideItem)) inventoryDetailConfirmed = true;
    }

    private void UpdateInventoryGuide()
    {
        if (inventoryGuideRoutine != null || OptionWindowManager.IsAnyOptionWindowOpen()) return;
        if (inventoryGuideStep == InventoryGuideStep.Hover && inventoryHoverConfirmed)
        {
            inventoryGuideStep = InventoryGuideStep.Inspect;
            TutorialSessionContext.SetInventoryPractice(inventoryGuideItem, true);
            ShowInventoryInteractionGuide(true);
        }
        else if (inventoryGuideStep == InventoryGuideStep.Inspect && inventoryDetailConfirmed)
        {
            inventoryGuideStep = InventoryGuideStep.Detail;
            bool consumable = inventoryGuideItem is AmuletSO;
            GameObject info = UIManager.Instance != null ? UIManager.Instance.GetInventoryInfoPanel(consumable) : null;
            string body = consumable
                ? GameText.Get("game.TutorialDirector.UpdateInventoryGuide.8926769a8e", "상세창에서 효과와 사용 조건을 확인하세요. '사용'을 누르면 효과가 적용되고 소모품은 사라집니다. 패를 대상으로 하는 소모품은 사용 후 대상 패를 고르고 확정해야 합니다.\n지금은 정보 확인 연습이므로 사용/판매를 막아 두었습니다. 설명을 확인하면 창을 닫고 이동 연습을 이어갑니다.")
                : GameText.Get("game.TutorialDirector.UpdateInventoryGuide.f2eb0bffea", "기물은 사용해서 소모하는 물건이 아닙니다. 보유하는 동안 각 기물의 발동 조건에 맞춰 효과가 적용됩니다. 상세창에서 효과/등급/조건을 확인하세요.\n판매하면 보유 효과를 잃으므로 이번 연습에서는 판매하지 않습니다. 설명을 확인하면 창을 닫고 이동 연습을 이어갑니다.");
            overlay.SetGuideSourceTitle(consumable ? "소모품 사용 방법" : "기물 효과 확인");
            if (info != null && info.transform is RectTransform rect)
                overlay.ShowSpotlightModal(consumable ? GameText.Get("game.TutorialDirector.UpdateInventoryGuide.36c2987964", "소모품 사용 방법") : GameText.Get("game.TutorialDirector.UpdateInventoryGuide.d9c335dd7f", "기물 효과 확인"), body,
                    GameText.Get("game.TutorialDirector.UpdateInventoryGuide.abebb8b46d", "확인 / 다음 이동"), new[] { rect }, TutorialOverlayView.GuidePanelPlacement.Top, FinishInventoryGuide, 8f);
            else overlay.ShowModal(consumable ? GameText.Get("game.TutorialDirector.UpdateInventoryGuide.36c2987964", "소모품 사용 방법") : GameText.Get("game.TutorialDirector.UpdateInventoryGuide.d9c335dd7f", "기물 효과 확인"), body, GameText.Get("game.TutorialDirector.UpdateInventoryGuide.4f52aa1388", "다음 이동 안내"), FinishInventoryGuide);
        }
    }

    private void FinishInventoryGuide()
    {
        inventoryGuideStep = InventoryGuideStep.Closing;
        inventoryGuideRoutine = StartCoroutine(FinishInventoryGuideRoutine());
    }

    private IEnumerator FinishInventoryGuideRoutine()
    {
        UIManager.Instance?.HideTooltip();
        if (inventoryGuideItem is AmuletSO) ConsumableUIManager.Instance?.CloseConsumablePreview();
        else ArtifactManager.Instance?.ClearSellPreview();
        // 기본 프리뷰 복귀 연출에 유예를 줍니다. 더 긴 연출의 입력 잠금은 기존 카메라 정책이 확인합니다.
        yield return new WaitForSecondsRealtime(0.65f);
        RestoreInventoryInfoLayer();
        TutorialSessionContext.SetInventoryPractice(null);
        inventoryGuideItem = null;
        inventoryUiTarget = null;
        inventoryGuideStep = InventoryGuideStep.None;
        inventoryGuideRoutine = null;
        cameraDestinationGuidePending = false;
        ClearWorldSpotlight();
        ContinueOrCompleteCameraPractice();
    }

    /// <summary>기존 족자의 배치는 유지하고 튜토리얼 어둠보다 위에 표시했다가 원래 정렬을 복구합니다.</summary>
    private void PromoteInventoryInfoLayer()
    {
        GameObject info = UIManager.Instance != null ? UIManager.Instance.GetInventoryInfoPanel(inventoryGuideItem is AmuletSO) : null;
        if (info == null) return;
        inventoryInfoCanvas = info.GetComponent<Canvas>();
        createdInventoryInfoCanvas = inventoryInfoCanvas == null;
        if (createdInventoryInfoCanvas) inventoryInfoCanvas = info.AddComponent<Canvas>();
        inventoryInfoOverrideSorting = inventoryInfoCanvas.overrideSorting;
        inventoryInfoSortingOrder = inventoryInfoCanvas.sortingOrder;
        inventoryInfoCanvas.overrideSorting = true;
        inventoryInfoCanvas.sortingOrder = overlay.GetComponent<Canvas>().sortingOrder + 1;
        inventoryInfoRaycaster = info.GetComponent<GraphicRaycaster>();
        createdInventoryInfoRaycaster = inventoryInfoRaycaster == null;
        if (createdInventoryInfoRaycaster) inventoryInfoRaycaster = info.AddComponent<GraphicRaycaster>();
    }

    private void RestoreInventoryInfoLayer()
    {
        if (createdInventoryInfoRaycaster && inventoryInfoRaycaster != null) Destroy(inventoryInfoRaycaster);
        if (inventoryInfoCanvas != null)
        {
            if (createdInventoryInfoCanvas) Destroy(inventoryInfoCanvas);
            else
            {
                inventoryInfoCanvas.overrideSorting = inventoryInfoOverrideSorting;
                inventoryInfoCanvas.sortingOrder = inventoryInfoSortingOrder;
            }
        }
        inventoryInfoCanvas = null;
        inventoryInfoRaycaster = null;
        createdInventoryInfoCanvas = createdInventoryInfoRaycaster = false;
    }

    private IEnumerator ShowDeckDestinationGuideWhenReady()
    {
        yield return null;

        TableInDeckViewResponder responder = FindFirstObjectByType<TableInDeckViewResponder>();
        float timeoutAt = Time.unscaledTime + 2f;
        while (responder != null && !responder.IsFullyOpen && Time.unscaledTime < timeoutAt)
            yield return null;

        // 서랍이 열린 프레임 다음에 카드와 정렬 UI가 표시 상태를 동기화합니다.
        yield return null;

        cameraDestinationGuidePending = false;
        cameraDestinationGuideRoutine = null;
        if (!cameraPracticeActive || overlay == null)
            yield break;

        overlay.ShowModal(
            GameText.Get("game.TutorialDirector.ShowDeckDestinationGuideWhenReady.9e5c0581c2", "현재 덱 확인"),
            GameText.Get("game.TutorialDirector.ShowDeckDestinationGuideWhenReady.302676944e", "이곳에는 이번 여정에서 사용하는 모든 패와 선택한 탈이 모여 있습니다. 전투에서는 이 덱에서 손패를 가져오며, 패 강화/패 제거 결과도 여기에 바로 반영됩니다.\n패의 월과 강화 상태, 현재 덱 구성을 확인해 다음 족보를 계획할 때 사용하세요."),
            GameText.Get("game.TutorialDirector.ShowDeckDestinationGuideWhenReady.4f52aa1388", "다음 이동 안내"),
            ContinueOrCompleteCameraPractice);
    }

    private AmuletSO EnsureTutorialConsumable()
    {
        ConsumableManager manager = ConsumableManager.Instance;
        if (manager == null)
            return null;

        if (manager.inventory != null)
        {
            for (int i = 0; i < manager.inventory.Length; i++)
            {
                if (manager.inventory[i] != null)
                    return manager.inventory[i];
            }
        }

        AmuletSO sample = Resources.Load<AmuletSO>(TutorialConsumableResourcePath);
        if (sample == null)
        {
            Debug.LogWarning($"[튜토리얼] 연습용 소모품을 찾지 못했습니다: {TutorialConsumableResourcePath}");
            return null;
        }

        if (!manager.TryAddConsumable(sample))
        {
            Debug.LogWarning($"[튜토리얼] 연습용 소모품을 지급하지 못했습니다: {sample.cardName}");
            return null;
        }

        if (manager.inventory == null)
            return sample;

        for (int i = 0; i < manager.inventory.Length; i++)
        {
            if (manager.inventory[i] != null)
                return manager.inventory[i];
        }

        return sample;
    }

    private ArtifactData EnsureTutorialArtifact()
    {
        ArtifactManager manager = ArtifactManager.Instance;
        if (manager == null || manager.inventory == null || manager.inventory.items == null)
            return null;

        for (int i = 0; i < manager.inventory.items.Count; i++)
        {
            if (manager.inventory.items[i] != null)
                return manager.inventory.items[i];
        }

        ArtifactData sample = null;
        UnlockManager unlockManager = UnlockManager.instance;
        if (unlockManager != null && unlockManager.itemDatabase != null)
            sample = unlockManager.itemDatabase.GetArtifactByID(TutorialArtifactID);

        if (sample == null && unlockManager != null && unlockManager.unlockedArtifacts != null)
        {
            for (int i = 0; i < unlockManager.unlockedArtifacts.Count; i++)
            {
                ArtifactData candidate = unlockManager.unlockedArtifacts[i];
                if (candidate != null && manager.CanAddArtifact(candidate))
                {
                    sample = candidate;
                    break;
                }
            }
        }

        if (sample == null)
        {
            Debug.LogWarning($"[튜토리얼] 연습용 기물을 찾지 못했습니다: {TutorialArtifactID}");
            return null;
        }

        if (!manager.TryAddArtifact(sample))
        {
            Debug.LogWarning($"[튜토리얼] 연습용 기물을 지급하지 못했습니다: {sample.artifactName}");
            return null;
        }

        return sample;
    }

    private void ContinueOrCompleteCameraPractice()
    {
        if (cameraPracticeStep < CameraPracticeSequence.Length)
        {
            ShowCameraPracticeHint();
            return;
        }

        cameraPracticeActive = false;
        overlay.SetCameraProgressVisible(false);
        RestoreCameraInputController();
        expectedStageIndex = 0;
        ShowExpectedStageGuide();
    }

    private CameraHeadLookController ResolveHeadLookController()
    {
        if (headLookController == null)
            headLookController = FindFirstObjectByType<CameraHeadLookController>();
        return headLookController;
    }

    private void TakeCameraInputController()
    {
        if (ownsCameraInputOverride || headLookController == null)
            return;

        headLookInputController = headLookController.GetComponent<HeadLookInputController>();
        if (headLookInputController == null)
            return;

        headLookInputWasEnabled = headLookInputController.enabled;
        headLookInputController.enabled = false;
        ownsCameraInputOverride = true;
    }

    private void RestoreCameraInputController()
    {
        if (!ownsCameraInputOverride)
            return;

        if (headLookInputController != null)
            headLookInputController.enabled = headLookInputWasEnabled;

        ownsCameraInputOverride = false;
        headLookInputController = null;
    }

    private static KeyControl GetKeyControl(Keyboard keyboard, Key key)
    {
        switch (key)
        {
            case Key.W: return keyboard.wKey;
            case Key.A: return keyboard.aKey;
            case Key.S: return keyboard.sKey;
            case Key.D: return keyboard.dKey;
            default: return null;
        }
    }

    private static Vector2Int ToDirection(Key key)
    {
        switch (key)
        {
            case Key.W: return Vector2Int.up;
            case Key.A: return Vector2Int.left;
            case Key.S: return Vector2Int.down;
            case Key.D: return Vector2Int.right;
            default: return Vector2Int.zero;
        }
    }

    private void ShowExpectedStageGuide()
    {
        if (overlay == null
            || !TutorialMapScenarioFactory.TryGetStageType(expectedStageIndex, out StageType stageType))
            return;

        GetStageGuide(stageType, out string title, out string body);
        spotlightMapNode = FindEnterableMapNode(stageType);
        if (spotlightMapNode != null && UpdateMapNodeSpotlightProxy(spotlightMapNode))
        {
            overlay.ShowSpotlightModal(
                title,
                GameText.Get("game.TutorialDirector.ShowExpectedStageGuide.73ae590f5e", "{value0}\n밝게 표시된 노드를 확인한 뒤 선택하세요.", new { value0 = body }),
                GameText.Get("game.TutorialDirector.ShowExpectedStageGuide.0c1fd4ef58", "노드 확인"),
                new[] { mapNodeSpotlightProxy },
                ResolveMapGuidePlacement(mapNodeSpotlightProxy),
                () => CompleteMapNodeGuide(stageType));
            return;
        }

        spotlightMapNode = null;
        overlay.ShowModal(
            title,
            GameText.Get("game.TutorialDirector.ShowExpectedStageGuide.1f41ea9640", "{value0}\n안내를 확인한 뒤 지도에서 빛나는 노드를 선택하세요.", new { value0 = body }),
            GameText.Get("game.TutorialDirector.ShowExpectedStageGuide.0c1fd4ef58", "노드 확인"),
            () => CompleteMapNodeGuide(stageType));
    }

    private void CompleteMapNodeGuide(StageType stageType)
    {
        spotlightMapNode = null;
        allowedStages.Add(stageType);
    }

    private static MapGraphNodeView FindEnterableMapNode(StageType stageType)
    {
        MapGraphNodeView[] nodes = FindObjectsByType<MapGraphNodeView>(FindObjectsSortMode.None);
        for (int i = 0; i < nodes.Length; i++)
        {
            MapGraphNodeView node = nodes[i];
            if (node != null && node.isActiveAndEnabled && node.CanEnter
                && node.Node?.stage != null && node.Node.stage.type == stageType)
                return node;
        }

        return null;
    }

    private bool UpdateMapNodeSpotlightProxy(MapGraphNodeView node)
    {
        return node != null && UpdateWorldSpotlightProxy(
            node.GetComponent<BoxCollider>(), ref mapNodeSpotlightProxy, "MapNodeSpotlightTarget");
    }

    private bool UpdateWorldSpotlightProxy(BoxCollider collider, ref RectTransform proxy, string proxyName)
    {
        if (collider == null || overlay == null || Camera.main == null)
            return false;

        RectTransform canvasRect = overlay.transform as RectTransform;
        if (canvasRect == null)
            return false;

        Vector3 halfSize = collider.size * 0.5f;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        Camera camera = Camera.main;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 localCorner = collider.center + Vector3.Scale(halfSize, new Vector3(x, y, z));
            Vector3 screenPoint = camera.WorldToScreenPoint(collider.transform.TransformPoint(localCorner));
            if (screenPoint.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPoint, null, out Vector2 localPoint))
                return false;

            min = Vector2.Min(min, localPoint);
            max = Vector2.Max(max, localPoint);
        }

        if (proxy == null)
        {
            GameObject proxyObject = new GameObject(proxyName, typeof(RectTransform));
            proxy = proxyObject.GetComponent<RectTransform>();
            proxy.SetParent(canvasRect, false);
            proxy.anchorMin = new Vector2(0.5f, 0.5f);
            proxy.anchorMax = new Vector2(0.5f, 0.5f);
            proxy.pivot = new Vector2(0.5f, 0.5f);
        }

        proxy.anchoredPosition = (min + max) * 0.5f;
        proxy.sizeDelta = new Vector2(
            Mathf.Max(64f, max.x - min.x),
            Mathf.Max(64f, max.y - min.y));
        return true;
    }

    /// <summary>큰 클릭 판정 대신 보이는 메시 경계의 투영만 사용해 스포트라이트를 맞춥니다.</summary>
    private bool UpdateRendererSpotlightProxy(Renderer renderer, ref RectTransform proxy, string proxyName)
    {
        return renderer != null && UpdateRenderersSpotlightProxy(new[] { renderer }, ref proxy, proxyName);
    }

    private bool UpdateVisualSpotlightProxy(Transform root, ref RectTransform proxy, string proxyName)
    {
        if (root == null)
            return false;

        return UpdateRenderersSpotlightProxy(root.GetComponentsInChildren<Renderer>(true), ref proxy, proxyName);
    }

    private bool UpdateRenderersSpotlightProxy(Renderer[] renderers, ref RectTransform proxy, string proxyName)
    {
        if (overlay == null || Camera.main == null || renderers == null)
            return false;
        RectTransform canvasRect = overlay.transform as RectTransform;
        if (canvasRect == null)
            return false;

        Camera camera = Camera.main;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        bool found = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                continue;

            // 메시의 로컬 경계를 직접 투영하면 회전한 두루마리의 큰 월드 AABB가 강조 영역을 부풀리지 않습니다.
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            bool hasLocalBounds = renderer is SkinnedMeshRenderer || filter != null && filter.sharedMesh != null;
            Bounds bounds = renderer is SkinnedMeshRenderer skinned
                ? skinned.localBounds
                : hasLocalBounds ? filter.sharedMesh.bounds : renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 world = hasLocalBounds ? renderer.transform.TransformPoint(corner) : corner;
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, screen, null, out Vector2 local))
                    return false;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
                found = true;
            }
        }

        if (!found)
            return false;

        if (proxy == null)
        {
            GameObject proxyObject = new GameObject(proxyName, typeof(RectTransform));
            proxy = proxyObject.GetComponent<RectTransform>();
            proxy.SetParent(canvasRect, false);
            proxy.anchorMin = proxy.anchorMax = proxy.pivot = new Vector2(0.5f, 0.5f);
        }
        proxy.anchoredPosition = (min + max) * 0.5f;
        proxy.sizeDelta = new Vector2(Mathf.Max(8f, max.x - min.x), Mathf.Max(8f, max.y - min.y));
        return true;
    }

    private TutorialOverlayView.GuidePanelPlacement ResolveMapGuidePlacement(RectTransform target)
    {
        RectTransform canvasRect = overlay.transform as RectTransform;
        return target.anchoredPosition.y < canvasRect.rect.center.y
            ? TutorialOverlayView.GuidePanelPlacement.Top
            : TutorialOverlayView.GuidePanelPlacement.Bottom;
    }

    private void HandleStageEntryBlocked(StageInfo stageInfo)
    {
        if (stageInfo == null)
            return;

        if (!TutorialMapScenarioFactory.TryGetStageType(expectedStageIndex, out StageType expectedStage))
            return;

        if (stageInfo.type == expectedStage)
        {
            ShowExpectedStageGuide();
            return;
        }

        GetStageGuide(expectedStage, out string title, out _);
        overlay.ShowHint(GameText.Get("game.TutorialDirector.HandleStageEntryBlocked.7eaf5e3d58", "진행할 노드"), GameText.Get("game.TutorialDirector.HandleStageEntryBlocked.639089cf70", "현재는 {value0}를 먼저 진행해야 합니다.", new { value0 = title }));
    }

    /// <summary>노드 클릭 직후 기존 안내를 닫고, 실제 진입 연출 종료를 기다릴 준비를 합니다.</summary>
    private void HandleStageEntryStarted(StageInfo stageInfo)
    {
        if (stageInfo == null)
            return;

        spotlightMapNode = null;
        ClearWorldSpotlight();
        if (stageInfo.type == StageType.Shop)
        {
            shopGuideStep = ShopGuideStep.Inspection;
            TutorialSessionContext.SetShopInteractionGuard(CanUseShopGuideObject);
            overlay.SetExitButtonVisible(false);
            BlockShopExitButton();
        }
        if (stageEntryGuideRoutine != null)
            StopCoroutine(stageEntryGuideRoutine);
        if (nextStageGuideRoutine != null)
        {
            StopCoroutine(nextStageGuideRoutine);
            nextStageGuideRoutine = null;
        }
        overlay.Hide();
        stageEntryGuideRoutine = StartCoroutine(ShowStageEntryGuideWhenReady(stageInfo));
    }

    /// <summary>테이블 전환과 카메라 이동이 모두 끝난 뒤 노드별 설명을 표시합니다.</summary>
    private IEnumerator ShowStageEntryGuideWhenReady(StageInfo stageInfo)
    {
        yield return null;

        CameraViewSpaceStage expectedStage = CameraViewSpaceStageUtility.ResolveFromStageInfo(stageInfo);
        float timeoutAt = Time.unscaledTime + 30f;
        bool targetContextObserved = false;
        bool transitionObserved = false;

        while (Time.unscaledTime < timeoutAt)
        {
            CameraHeadLookController headLook = ResolveHeadLookController();
            TableTransitionManager transition = TableTransitionManager.instance;
            bool transitionBusy = transition != null && transition.IsTransitionInProgress;
            bool cameraBusy = headLook != null && headLook.IsCameraMoving;

            transitionObserved |= transitionBusy;
            targetContextObserved |= headLook != null && headLook.CurrentStageContext == expectedStage;

            if (targetContextObserved && !transitionBusy && !cameraBusy)
                break;

            if (headLook == null && transitionObserved && !transitionBusy)
                break;

            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.15f);

        switch (stageInfo.type)
        {
            case StageType.Battle:
                overlay.ShowModal(
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.22d017b692", "전투의 기본"),
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.3369187c31", "손패에서 패를 골라 족보를 만들고 제출합니다. 점수는 족보의 기본점수와 배율, 탈와 기물 효과를 거쳐 결정됩니다.\n잘못 고른 패는 제출 전 다시 선택할 수 있고, 필요하면 패를 버려 새 손패를 받을 수 있습니다."),
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.aef613c661", "다음"),
                    ShowBattleSwordGuide);
                break;
            case StageType.Shop:
                TutorialSessionContext.SetShopHoverOverlayRoot(overlay.transform);
                overlay.ShowHoverInspectionModal(
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.ec1891e6c2", "상점 체험"),
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.a22397a7a8", "뒤쪽 진열대에는 계속 효과를 내는 기물, 앞쪽에는 새 패를 얻는 주머니가 있습니다. 오른쪽에는 여정 규칙을 바꾸는 특권패가 놓입니다.\n상품에 마우스를 올려 이름/효과/가격을 살펴보세요. 지금은 구매되지 않습니다."),
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.d7ce8b3a6c", "기물 구매해 보기"),
                    BeginShopArtifactPurchaseGuide);
                break;
            case StageType.CardUpgrade:
                overlay.ShowModal(
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.a175058bc3", "패 강화 체험"),
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.e02f93e4d5", "보유한 패 한 장을 선택해 강화합니다.\n강화 버튼을 누르기 전에는 다른 패로 다시 선택할 수 있고, 확정한 뒤에는 변경할 수 없습니다.\n강화를 시도하거나 나가기 버튼을 누르면 이 노드 방문이 완료됩니다."),
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.b0fda93406", "패 강화 살펴보기"),
                    null);
                break;
            case StageType.CardRemoval:
            case StageType.ArtifactExchange:
            case StageType.Observatory:
                GetStageGuide(stageInfo.type, out string eventTitle, out string eventBody);
                overlay.ShowModal(
                    eventTitle,
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.007afc8546", "{value0}\n기능을 사용하거나 나가기 버튼을 누르면 지도로 돌아갑니다.", new { value0 = eventBody }),
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.dcd1189f36", "노드 살펴보기"),
                    null);
                break;
            case StageType.Boss:
                ShowBossMirrorGuide();
                break;
            default:
                overlay.ShowModal(
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.4f2cb4f9db", "이벤트 노드 체험"),
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.d7d47bc30e", "설명을 확인한 뒤 기능을 사용하거나, 노드의 나가기 버튼으로 지도로 돌아가세요."),
                    GameText.Get("game.TutorialDirector.ShowStageEntryGuideWhenReady.dcd1189f36", "노드 살펴보기"),
                    null);
                break;
        }

        stageEntryGuideRoutine = null;
    }

    /// <summary>전투판 왼쪽 검을 비춰 목표 점수 확인 방법을 먼저 알려줍니다.</summary>
    private void ShowBattleSwordGuide()
    {
        overlay.SetGuideSourceTitle("목표 점수 / 점수 검");
        ScoreSwordGaugePresenter sword = ScoreSwordGaugePresenter.Active;
        spotlightWorldRenderer = sword != null ? sword.SpotlightBladeRenderer : null;
        if (UpdateRendererSpotlightProxy(spotlightWorldRenderer, ref worldSpotlightProxy, "WorldSpotlightTarget"))
        {
            TutorialSessionContext.SetShopInteractionGuard(_ => false);
            ResolveHeadLookController();
            TakeCameraInputController();
            overlay.ShowHoverSpotlightModal(
                GameText.Get("game.TutorialDirector.ShowBattleSwordGuide.f7ad92a497", "목표 점수 / 점수 검"),
                GameText.Get("game.TutorialDirector.ShowBattleSwordGuide.1f9c7cec4d", "왼쪽 검에 커서를 올리면 이번 전투의 요구 점수와 현재 점수를 확인할 수 있습니다. 패를 제출해 목표 점수를 채우세요."),
                GameText.Get("game.TutorialDirector.ShowBattleSwordGuide.aef613c661", "다음"), new[] { worldSpotlightProxy }, TutorialOverlayView.GuidePanelPlacement.Bottom,
                ShowBattleBellsGuide, 8f);
            return;
        }
        ClearWorldSpotlight();
        overlay.ShowModal(GameText.Get("game.TutorialDirector.ShowBattleSwordGuide.f7ad92a497", "목표 점수 / 점수 검"), GameText.Get("game.TutorialDirector.ShowBattleSwordGuide.77dd42ee53", "전투판 왼쪽 검에 커서를 올리면 요구 점수와 현재 점수를 확인할 수 있습니다."),
            GameText.Get("game.TutorialDirector.ShowBattleSwordGuide.aef613c661", "다음"), ShowBattleBellsGuide);
    }

    /// <summary>두 종의 실제 클릭 영역을 합쳐 제출과 버리기 사용법을 안내합니다.</summary>
    private void ShowBattleBellsGuide()
    {
        overlay.SetGuideSourceTitle("제출과 버리기 종");
        TutorialSessionContext.SetShopInteractionGuard(null);
        RestoreCameraInputController();
        ClearWorldSpotlight();
        BellCardActionButton[] bells = FindObjectsByType<BellCardActionButton>(FindObjectsSortMode.None);
        foreach (BellCardActionButton bell in bells)
        {
            if (bell == null || !bell.gameObject.activeInHierarchy)
                continue;
            BoxCollider collider = bell.GetComponent<BoxCollider>();
            if (collider == null)
                continue;
            if (bell.Action == BellCardAction.Play) spotlightWorldCollider = collider;
            else secondWorldSpotlightCollider = collider;
        }
        bool playReady = UpdateWorldSpotlightProxy(spotlightWorldCollider, ref worldSpotlightProxy, "WorldSpotlightTarget");
        bool dropReady = UpdateWorldSpotlightProxy(secondWorldSpotlightCollider, ref secondWorldSpotlightProxy, "SecondWorldSpotlightTarget");
        if (playReady && dropReady)
        {
            overlay.ShowSpotlightModal(
                GameText.Get("game.TutorialDirector.ShowBattleBellsGuide.3de6a13954", "제출과 버리기 종"),
                GameText.Get("game.TutorialDirector.ShowBattleBellsGuide.cbb7982b3c", "파란 손잡이 종은 선택한 패를 제출하고, 분홍 손잡이 종은 패를 버려 새 손패를 받습니다. 각 종 아래 숫자는 이번 전투에 남은 사용 횟수입니다."),
                GameText.Get("game.TutorialDirector.ShowBattleBellsGuide.aef613c661", "다음"), new[] { worldSpotlightProxy, secondWorldSpotlightProxy },
                TutorialOverlayView.GuidePanelPlacement.Bottom, ShowBattleJokboGuide, 8f);
            return;
        }
        ClearWorldSpotlight();
        overlay.ShowModal(GameText.Get("game.TutorialDirector.ShowBattleBellsGuide.3de6a13954", "제출과 버리기 종"), GameText.Get("game.TutorialDirector.ShowBattleBellsGuide.9c0d869c70", "파란 종은 선택한 패를 제출하고, 분홍 종은 패를 버립니다. 아래 숫자는 남은 횟수입니다."),
            GameText.Get("game.TutorialDirector.ShowBattleBellsGuide.aef613c661", "다음"), ShowBattleJokboGuide);
    }

    /// <summary>두루마리의 넓은 클릭 판정이 아니라 보이는 메시의 외곽으로 강조 영역을 만듭니다.</summary>
    private void ShowBattleJokboGuide()
    {
        overlay.SetGuideSourceTitle("족보 확인 두루마리");
        string title = GameText.Get("game.TutorialDirector.ShowBattleJokboGuide.2526b19d4d", "족보 확인 두루마리");
        string body = GameText.Get("game.TutorialDirector.ShowBattleJokboGuide.8a03cd31bb", "오른쪽 두루마리를 누르면 현재 만들 수 있는 족보와 각 족보의 칩/배수/강화 레벨을 확인할 수 있습니다.\n어떤 패를 남길지 고민될 때 열어 본 뒤 제출하세요.");
        JokboScrollClickTarget scroll = FindFirstObjectByType<JokboScrollClickTarget>();
        ClearWorldSpotlight();
        spotlightVisualRoot = scroll != null ? scroll.transform : null;
        if (UpdateVisualSpotlightProxy(spotlightVisualRoot, ref worldSpotlightProxy, "WorldSpotlightTarget"))
        {
            overlay.ShowSpotlightModal(title, body, GameText.Get("game.TutorialDirector.ShowBattleJokboGuide.44aff5bc84", "전투 시작"),
                new[] { worldSpotlightProxy }, ResolveMapGuidePlacement(worldSpotlightProxy),
                ClearWorldSpotlight, 3f);
            return;
        }

        ClearWorldSpotlight();
        overlay.ShowModal(title, body, GameText.Get("game.TutorialDirector.ShowBattleJokboGuide.44aff5bc84", "전투 시작"), null);
    }

    /// <summary>보스 전투판의 거울 위치와 디버프 확인 동작을 연결합니다.</summary>
    private void ShowBossMirrorGuide()
    {
        overlay.SetGuideSourceTitle("보스 디버프 / 거울");
        string title = GameText.Get("game.TutorialDirector.ShowBossMirrorGuide.6b65a4caa8", "보스 디버프 / 거울");
        string body = GameText.Get("game.TutorialDirector.ShowBossMirrorGuide.6b8e8d4055", "보스마다 전투 규칙을 바꾸는 디버프가 있습니다. 왼쪽 거울에 커서를 올려 현재 보스의 디버프 내용을 확인한 뒤 전투를 진행하세요.");
        BossDebuffHover mirror = FindFirstObjectByType<BossDebuffHover>();
        spotlightWorldCollider = mirror != null ? mirror.GetComponent<BoxCollider>() : null;
        if (UpdateWorldSpotlightProxy(spotlightWorldCollider, ref worldSpotlightProxy, "WorldSpotlightTarget"))
        {
            TutorialSessionContext.SetShopInteractionGuard(_ => false);
            ResolveHeadLookController();
            TakeCameraInputController();
            overlay.ShowHoverSpotlightModal(title, body, GameText.Get("game.TutorialDirector.ShowBossMirrorGuide.93e2c43b06", "보스 확인"), new[] { worldSpotlightProxy },
                TutorialOverlayView.GuidePanelPlacement.Top, EndBossMirrorGuide, 8f);
            return;
        }
        ClearWorldSpotlight();
        overlay.ShowModal(title, body, GameText.Get("game.TutorialDirector.ShowBossMirrorGuide.93e2c43b06", "보스 확인"), null);
    }

    private void EndBossMirrorGuide()
    {
        TutorialSessionContext.SetShopInteractionGuard(null);
        RestoreCameraInputController();
        ClearWorldSpotlight();
    }

    /// <summary>구매 가능한 기물 한 개만 조작할 수 있도록 상점 입력과 스포트라이트를 전환합니다.</summary>
    private void BeginShopArtifactPurchaseGuide()
    {
        TutorialSessionContext.SetShopHoverOverlayRoot(null);
        ShopManager shop = ShopManager.instance;
        ShopSlot[] slots = shop != null && shop.artifactSlotParent != null
            ? shop.artifactSlotParent.GetComponentsInChildren<ShopSlot>(true)
            : null;
        spotlightShopSlot = null;
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                ShopSlot slot = slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy || slot.data == null
                    || slot.data.isSoldOut || slot.data.itemType != ShopItemType.Artifact)
                    continue;

                if (spotlightShopSlot == null || slot.data.finalPrice < spotlightShopSlot.data.finalPrice)
                    spotlightShopSlot = slot;
            }
        }

        spotlightWorldCollider = spotlightShopSlot != null
            ? spotlightShopSlot.GetComponent<BoxCollider>()
            : null;
        if (spotlightShopSlot == null
            || !UpdateWorldSpotlightProxy(spotlightWorldCollider, ref worldSpotlightProxy, "WorldSpotlightTarget"))
        {
            Debug.LogWarning("[튜토리얼] 상점 기물 진열을 찾지 못해 구매 체험을 건너뜁니다.");
            ShowShopGuideUnavailable();
            return;
        }

        EnsureTutorialShopFunds(Mathf.Max(0, spotlightShopSlot.data.finalPrice));
        shopGuideStep = ShopGuideStep.BuyArtifact;
        overlay.SetGuideSourceTitle("기물 구매 체험");
        overlay.ShowInteractiveSpotlight(
            GameText.Get("game.TutorialDirector.BeginShopArtifactPurchaseGuide.b02ef109c9", "기물 구매 체험"),
            GameText.Get("game.TutorialDirector.BeginShopArtifactPurchaseGuide.0b80eb1cce", "밝게 표시된 기물을 클릭해 실제로 구매해 보세요. 구매하면 기물 보관 공간에 들어가고, 보유 중 조건에 따라 효과가 발동합니다."),
            new[] { worldSpotlightProxy }, ResolveMapGuidePlacement(worldSpotlightProxy));
    }

    /// <summary>지정 기물의 실제 구매 성공을 받은 뒤 왼쪽 리롤 종 체험으로 진행합니다.</summary>
    private void HandleShopArtifactPurchased(ShopSlot slot)
    {
        if (shopGuideStep != ShopGuideStep.BuyArtifact || slot != spotlightShopSlot)
            return;

        spotlightShopSlot = null;
        ShopManager shop = ShopManager.instance;
        spotlightShopBell = shop != null && shop.shopPanel != null
            ? shop.shopPanel.GetComponentInChildren<ShopRerollBell>(true)
            : null;
        spotlightWorldCollider = spotlightShopBell != null
            ? spotlightShopBell.GetComponent<BoxCollider>()
            : null;
        if (spotlightShopBell == null
            || !UpdateWorldSpotlightProxy(spotlightWorldCollider, ref worldSpotlightProxy, "WorldSpotlightTarget"))
        {
            Debug.LogWarning("[튜토리얼] 상점 리롤 종을 찾지 못해 리롤 체험을 건너뜁니다.");
            ShowShopGuideUnavailable();
            return;
        }

        EnsureTutorialShopFunds(Mathf.Max(0, shop.currentRerollCost));
        shopGuideStep = ShopGuideStep.Reroll;
        overlay.SetGuideSourceTitle("기물 진열 다시 뽑기");
        overlay.ShowInteractiveSpotlight(
            GameText.Get("game.TutorialDirector.HandleShopArtifactPurchased.08e4cbf102", "기물 진열 다시 뽑기"),
            GameText.Get("game.TutorialDirector.HandleShopArtifactPurchased.cbc2d6cef8", "왼쪽의 밝게 표시된 종을 누르면 전을 지불하고 뒤쪽 기물 진열을 새로 뽑습니다. 종에 표시된 현재 비용을 확인하고 눌러 보세요."),
            new[] { worldSpotlightProxy }, ResolveMapGuidePlacement(worldSpotlightProxy), 42f, true);
    }

    /// <summary>실제 리롤 성공을 받은 뒤 교체 연출이 끝날 때까지 기다리고 결과를 설명합니다.</summary>
    private void HandleShopRerolled()
    {
        if (shopGuideStep != ShopGuideStep.Reroll)
            return;

        shopGuideStep = ShopGuideStep.Complete;
        spotlightShopBell = null;
        ClearWorldSpotlight();
        StartCoroutine(ShowShopRerollResultWhenReady());
    }

    private IEnumerator ShowShopRerollResultWhenReady()
    {
        ShopManager shop = ShopManager.instance;
        while (shop != null && shop.IsRerollSwapAnimating)
            yield return null;

        yield return new WaitForSecondsRealtime(0.15f);
        if (overlay == null || shopGuideStep != ShopGuideStep.Complete)
            yield break;

        overlay.ShowModal(
            GameText.Get("game.TutorialDirector.ShowShopRerollResultWhenReady.578930578f", "리롤 완료"),
            GameText.Get("game.TutorialDirector.ShowShopRerollResultWhenReady.ea9686648e", "종을 누르면 원하는 기물을 찾기 위해 기물 진열을 다시 뽑을 수 있습니다. 리롤할 때마다 필요한 전이 달라질 수 있으니 남은 재화도 함께 확인하세요.\n이제 다른 상품을 살펴보거나 나가기 버튼으로 지도로 돌아가세요."),
            GameText.Get("game.TutorialDirector.ShowShopRerollResultWhenReady.837ab84653", "상점 안내 마치기"),
            EndShopGuide);
    }

    private void ShowShopGuideUnavailable()
    {
        shopGuideStep = ShopGuideStep.Complete;
        ClearWorldSpotlight();
        overlay.ShowModal(
            GameText.Get("game.TutorialDirector.ShowShopGuideUnavailable.c8d32726b7", "상점 안내"),
            GameText.Get("game.TutorialDirector.ShowShopGuideUnavailable.f700fb2bee", "연습할 상품 또는 종을 찾지 못했습니다. 상점 상품을 살펴본 뒤 나가기 버튼으로 지도로 돌아가세요."),
            GameText.Get("game.TutorialDirector.ShowShopGuideUnavailable.837ab84653", "상점 안내 마치기"),
            EndShopGuide);
    }

    private static void EnsureTutorialShopFunds(int requiredAmount)
    {
        EconomyManager economy = EconomyManager.instance;
        if (economy == null || economy.ecoData == null || economy.CanSpendMoney(requiredAmount))
            return;

        int shortfall = Mathf.Max(0, requiredAmount - economy.ecoData.currentMoney);
        if (shortfall > 0)
            economy.AddMoney(shortfall);
    }

    private bool CanUseShopGuideObject(InteractiveObjectBase target)
    {
        switch (shopGuideStep)
        {
            case ShopGuideStep.BuyArtifact: return target != null && target == spotlightShopSlot;
            case ShopGuideStep.Reroll: return target != null && target == spotlightShopBell;
            case ShopGuideStep.None: return true;
            default: return false;
        }
    }

    private void EndShopGuide()
    {
        shopGuideStep = ShopGuideStep.None;
        spotlightShopSlot = null;
        spotlightShopBell = null;
        ClearWorldSpotlight();
        TutorialSessionContext.SetShopHoverOverlayRoot(null);
        TutorialSessionContext.SetShopInteractionGuard(null);
        overlay?.SetExitButtonVisible(true);
        RestoreShopExitButton();
    }

    private void BlockShopExitButton()
    {
        ShopManager shop = ShopManager.instance;
        if (shop == null || shop.shopPanel == null)
            return;

        Button[] buttons = shop.shopPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || buttons[i].name != "ExitButton")
                continue;

            shopExitButton = buttons[i];
            shopExitButtonWasInteractable = shopExitButton.interactable;
            shopExitButton.interactable = false;
            return;
        }
    }

    private void RestoreShopExitButton()
    {
        if (shopExitButton != null)
            shopExitButton.interactable = shopExitButtonWasInteractable;
        shopExitButton = null;
    }

    private void ClearWorldSpotlight()
    {
        spotlightWorldCollider = null;
        secondWorldSpotlightCollider = null;
        spotlightWorldRenderer = null;
        spotlightVisualRoot = null;
    }

    private void HandleGoStopChoicePresented()
    {
        if (goStopGuideShown || overlay == null)
            return;

        goStopGuideShown = true;
        overlay.ShowModal(
            GameText.Get("game.TutorialDirector.HandleGoStopChoicePresented.2c9d13eaab", "목표 달성 / 제령 선택"),
            GameText.Get("game.TutorialDirector.HandleGoStopChoicePresented.703041cdf4", "목표 점수를 채우면 제령과 관망 중 하나를 선택합니다.\n제령은 전을 지불하고 더 높은 목표에 도전해 추가 보상을 노리며, 관망은 지금까지 확보한 보상을 받고 전투를 끝냅니다."),
            GameText.Get("game.TutorialDirector.HandleGoStopChoicePresented.dc5c92ff8d", "선택창 확인"),
            null);
    }

    /// <summary>첫 일반 전투 보상 내역이 모두 나타난 시점에만 기본 이자 규칙을 설명합니다.</summary>
    private void HandleBattleRewardPresented(int interest)
    {
        if (interestGuideShown || overlay == null
            || CombatManager.instance == null
            || CombatManager.instance.CurrentEncounterStageInfo?.type != StageType.Battle)
            return;

        interestGuideShown = true;
        overlay.ShowModal(
            GameText.Get("game.TutorialDirector.HandleBattleRewardPresented.09dcc2a9b8", "보상과 이자"),
            GameText.Get("game.TutorialDirector.HandleBattleRewardPresented.6e357051ed", "전투 보상에는 기본 보상 외에 이자가 포함될 수 있습니다. 현재 획득한 이자는 {value0}전입니다.\n기본 규칙은 보유한 전 5전마다 이자 1전, 최대 5전이며 기물/특권패 효과에 따라 달라질 수 있습니다.", new { value0 = interest }),
            GameText.Get("game.TutorialDirector.HandleBattleRewardPresented.abc36c19ff", "보상 확인"),
            null);
    }

    private void HandleStageCompleted(StageInfo stageInfo)
    {
        if (stageInfo == null)
            return;

        if (stageInfo.type == StageType.Shop)
            EndShopGuide();

        if (stageInfo.type == StageType.Boss)
        {
            if (bossCompletionShown)
                return;

            bossCompletionShown = true;
            StartCoroutine(ShowCompletionRoutine());
            return;
        }

        int completedStageIndex = TutorialMapScenarioFactory.FindStageIndex(stageInfo.type);
        if (completedStageIndex >= 0)
            expectedStageIndex = completedStageIndex + 1;

        if (nextStageGuideRoutine != null)
            StopCoroutine(nextStageGuideRoutine);
        nextStageGuideRoutine = StartCoroutine(ShowExpectedStageGuideWhenMapReady());
    }

    private IEnumerator ShowExpectedStageGuideWhenMapReady()
    {
        yield return null;

        float timeoutAt = Time.unscaledTime + 30f;
        while (Time.unscaledTime < timeoutAt)
        {
            CameraHeadLookController headLook = ResolveHeadLookController();
            TableTransitionManager transition = TableTransitionManager.instance;
            bool transitionBusy = transition != null && transition.IsTransitionInProgress;
            bool cameraBusy = headLook != null && headLook.IsCameraMoving;
            bool mapReady = headLook != null
                && headLook.CurrentStageContext == CameraViewSpaceStage.Map;
            bool mapInteractable = MapBoardManager.instance == null
                || MapBoardManager.instance.isInteractable;

            if (mapReady && mapInteractable && !transitionBusy && !cameraBusy)
                break;

            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.15f);
        ShowExpectedStageGuide();
        nextStageGuideRoutine = null;
    }

    private IEnumerator ShowCompletionRoutine()
    {
        yield return new WaitForSecondsRealtime(1f);
        overlay.ShowModal(
            GameText.Get("game.TutorialDirector.ShowCompletionRoutine.d26680505e", "튜토리얼 완료"),
            GameText.Get("game.TutorialDirector.ShowCompletionRoutine.c7c056d965", "전투와 상점뿐 아니라 패 강화, 패 제거, 기물 교환, 천문대와 보스 노드까지 확인했습니다.\n실제 여정에서는 노드 배치와 보상이 매번 달라집니다."),
            GameText.Get("game.TutorialDirector.ShowCompletionRoutine.c0970e5d13", "타이틀로 돌아가기"),
            () => TutorialSessionContext.RequestExit(true));
    }

    private bool CanEnterStage(StageInfo stageInfo)
    {
        return stageInfo != null && allowedStages.Contains(stageInfo.type);
    }

    private void ConfirmExit()
    {
        overlay.ShowModal(
            GameText.Get("game.TutorialDirector.ConfirmExit.f72a5d2b41", "튜토리얼 종료"),
            GameText.Get("game.TutorialDirector.ConfirmExit.a9110df867", "지금 종료하면 완료로 기록되지 않으며 기존 여정 데이터는 그대로 유지됩니다."),
            GameText.Get("game.TutorialDirector.ConfirmExit.cef77e1bb2", "종료하기"),
            () => TutorialSessionContext.RequestExit(false));
    }

    private void Subscribe()
    {
        TutorialSessionContext.InventoryItemHovered += HandleInventoryHovered;
        TutorialSessionContext.InventoryItemInspected += HandleInventoryInspected;
        TutorialSessionContext.StageEntryStarted += HandleStageEntryStarted;
        TutorialSessionContext.StageCompleted += HandleStageCompleted;
        TutorialSessionContext.StageEntryBlocked += HandleStageEntryBlocked;
        TutorialSessionContext.GoStopChoicePresented += HandleGoStopChoicePresented;
        TutorialSessionContext.BattleRewardPresented += HandleBattleRewardPresented;
        TutorialSessionContext.ShopArtifactPurchased += HandleShopArtifactPurchased;
        TutorialSessionContext.ShopRerolled += HandleShopRerolled;
    }

    private void Unsubscribe()
    {
        TutorialSessionContext.InventoryItemHovered -= HandleInventoryHovered;
        TutorialSessionContext.InventoryItemInspected -= HandleInventoryInspected;
        TutorialSessionContext.StageEntryStarted -= HandleStageEntryStarted;
        TutorialSessionContext.StageCompleted -= HandleStageCompleted;
        TutorialSessionContext.StageEntryBlocked -= HandleStageEntryBlocked;
        TutorialSessionContext.GoStopChoicePresented -= HandleGoStopChoicePresented;
        TutorialSessionContext.BattleRewardPresented -= HandleBattleRewardPresented;
        TutorialSessionContext.ShopArtifactPurchased -= HandleShopArtifactPurchased;
        TutorialSessionContext.ShopRerolled -= HandleShopRerolled;
    }

    private static void GetStageGuide(StageType type, out string title, out string body)
    {
        switch (type)
        {
            case StageType.Shop:
                title = GameText.Get("game.TutorialDirector.GetStageGuide.7314a28f36", "상점 노드");
                body = GameText.Get("game.TutorialDirector.GetStageGuide.1f7b9ba03a", "기물과 꾸러미, 특권패 등을 구매해 다음 전투를 준비합니다.\n리롤 비용을 지불하면 진열된 기물 상품을 다시 뽑을 수 있습니다.");
                return;
            case StageType.CardUpgrade:
                title = GameText.Get("game.TutorialDirector.GetStageGuide.9e73eacd76", "패 강화 노드");
                body = GameText.Get("game.TutorialDirector.GetStageGuide.55b0889aa5", "보유한 패 한 장을 선택해 강화합니다.\n강화 버튼을 누르기 전에는 다른 패로 다시 선택할 수 있고, 확정한 뒤에는 변경할 수 없습니다.");
                return;
            case StageType.CardRemoval:
                title = GameText.Get("game.TutorialDirector.GetStageGuide.9c430aa189", "패 제거 노드");
                body = GameText.Get("game.TutorialDirector.GetStageGuide.27ccfab689", "덱에서 원하지 않는 패를 골라 제거합니다.\n덱을 작게 만들면 원하는 족보의 패를 다시 만날 확률을 조절할 수 있습니다.");
                return;
            case StageType.ArtifactExchange:
                title = GameText.Get("game.TutorialDirector.GetStageGuide.9caa20f8bf", "기물 교환 노드");
                body = GameText.Get("game.TutorialDirector.GetStageGuide.84f6527eb6", "기물을 가지고 있다면 교환할 기물을 직접 고르고, 해금된 기물 중 중복되지 않는 하나로 바꿉니다.\n기물이 없다면 해금된 일반 등급 기물 하나를 받을 수 있습니다.");
                return;
            case StageType.Observatory:
                title = GameText.Get("game.TutorialDirector.GetStageGuide.a01895d5e8", "천문대 노드");
                body = GameText.Get("game.TutorialDirector.GetStageGuide.34a68a4263", "강화 신장패 다섯 장이 진열됩니다.\n설명을 살펴본 뒤 한 장을 무료로 선택해 해당 족보를 강화합니다. 이번 방문에는 한 장만 선택할 수 있습니다.");
                return;
            case StageType.Boss:
                title = GameText.Get("game.TutorialDirector.GetStageGuide.fbc716c9b3", "보스 노드");
                body = GameText.Get("game.TutorialDirector.GetStageGuide.eb5f48666a", "보스마다 전투 규칙을 바꾸는 전용 디버프가 있습니다.\n노드 정보에서 보스와 디버프를 확인하고, 덱과 기물을 고려해 진입하세요.");
                return;
            case StageType.Battle:
            default:
                title = GameText.Get("game.TutorialDirector.GetStageGuide.796a2b8b38", "전투 노드");
                body = GameText.Get("game.TutorialDirector.GetStageGuide.6253513f24", "손패로 족보를 만들어 목표 점수를 달성하는 기본 노드입니다.\n목표를 넘긴 뒤에는 안전하게 멈추거나 추가 제령에 도전할 수 있습니다.");
                return;
        }
    }
}
