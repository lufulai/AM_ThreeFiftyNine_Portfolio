using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;

public class JokboCheckUI : MonoBehaviour
{
    public static JokboCheckUI Instance { get; private set; }

   [Header("족보 리스트 줄(Row)들")]
    public JokboRowUI[] jokboRows;

    [Header("★여기에 'JockboCheck' 오브젝트를 넣으세요!")]
    public GameObject rootPanel;

    [Header("족보 UI 표현")]
    [SerializeField] private TMP_FontAsset jokboFont;

    [Header("★ 3D 두루마리 연출 설정")]
    public Transform scroll3DModel;       // UI_Scroll 모델의 Transform
    public Animator scrollAnimator;       // UI_Scroll_Anime가 들어있는 애니메이터
    
    [Tooltip("중앙으로 날아오는 시간")]
    public float moveDuration = 0.5f;     
    [Tooltip("펼쳐지는 애니메이션이 다 끝날 때까지 기다리는 시간")]
    public float unrollDuration = 1.0f;   

    [Header("위치 및 회전 세팅")]
    public Vector3 startLocalPos = new Vector3(-0.48f, -0.1340001f, 0.5f); // 좌측 구석 (시작 위치)
    public Vector3 centerLocalPos = new Vector3(0.075f, 0.035f, 0.169f);    // 화면 중앙 위치
    public Vector3 startLocalRot = new Vector3(0, 90f, -90f);     // 눕혀진 각도
    public Vector3 centerLocalRot = new Vector3(0, 90f, -90f);    // 똑바로 선 각도

    private bool isOpen = false;
    private float cachedAnimatorSpeed = 1f;
    private bool hasCachedAnimatorSpeed;
    private bool hasCardInputBlock;
    private bool hasHeadLookInputBlock;
    private Tween scrollDelayTween;
    
    private void Awake()
    {
        Instance = this;
        JokboPanelPresentation.Install(rootPanel, this, jokboRows, jokboFont);
    }

    private void Start()
    {
        // 시작할 때 족보가 화면 구석에 누워있는 상태로 초기화
        if (scroll3DModel != null)
        {
            scroll3DModel.localPosition = startLocalPos;
            scroll3DModel.localRotation = Quaternion.Euler(startLocalRot);
        }

        if (rootPanel != null) rootPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isOpen && (rootPanel != null && !rootPanel.activeSelf)) return;
        // 1. 신형 입력 시스템 (New Input System)으로 ESC 감지
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("신형 입력 시스템으로 ESC 눌림!");
            ClosePanel();
            return;
        }

        // 2. 구형 입력 시스템 (Legacy)으로 ESC 감지 (설정이 꼬였을 때를 대비한 2중 방어망)
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("구형 입력 시스템으로 ESC 눌림!");
            ClosePanel();
            return;
        }
#endif
    }
    // 이 패널(gameObject)이 켜질 때마다 자동으로 실행되는 유니티 기본 함수
    private void OnEnable()
    {
        RefreshAllRows();
    }

    private void OnDisable()
    {
        scrollDelayTween?.Kill();
        scrollDelayTween = null;
        RestoreAnimatorSpeed();
        ReleaseInputBlocks();
    }

    public void RefreshAllRows()
    {
        if (jokboRows == null || jokboRows.Length == 0) return;

        foreach (var row in jokboRows)
        {
            if (row != null)
            {
                row.RefreshData();
            }
        }
    }

    public void OpenScroll()
    {
        if (scroll3DModel == null)
        {
            OpenPanel();
            return;
        }

        if (!TryBeginOpen()) return;

        // 1. 구석에서 중앙으로 날아오면서 회전하여 일어섬 (DOTween)
        float scaledMoveDuration = ScaleDuration(moveDuration);
        scroll3DModel.DOLocalMove(centerLocalPos, scaledMoveDuration).SetEase(Ease.OutBack);
        scroll3DModel.DOLocalRotate(centerLocalRot, scaledMoveDuration).SetEase(Ease.OutBack).OnComplete(() =>
        {
            // 2. 중앙 도착 완료! -> 펼치는 애니메이션 실행 (Animator)
            ApplyPresentationAnimatorSpeed();
            if (scrollAnimator != null) scrollAnimator.SetTrigger("Open"); 

            // 3. 애니메이션이 끝날 때까지(unrollDuration) 기다린 후 족보 내용(UI) 띄우기
            scrollDelayTween?.Kill();
            scrollDelayTween = DOVirtual.DelayedCall(ScaleDelay(unrollDuration), () =>
            {
                scrollDelayTween = null;
                RestoreAnimatorSpeed();
                if (rootPanel != null)
                {
                    rootPanel.SetActive(true);
                    RefreshAllRows(); // 켜질 때 데이터 갱신
                }
            });
        });
    }

    public void OpenPanel()
    {
        if (!TryBeginOpen()) return;

        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
            RefreshAllRows();
        }
    }

    private bool TryBeginOpen()
    {
        if (isOpen || OptionWindowManager.IsAnyOptionWindowOpen())
            return false;

        isOpen = true;
        AcquireInputBlocks();
        SoundManager.PlaySfx(data => data.Battle_Scroll);
        return true;
    }

    // 뒤로가기 버튼에 연결할 함수
    public void ClosePanel()
    {
        if (!isOpen) return;
        isOpen = false;

        // 1. 족보 내용물(UI) 즉시 끄기
        if (rootPanel != null) rootPanel.SetActive(false);

        if (scroll3DModel == null)
        {
            RestoreAnimatorSpeed();
            ReleaseInputBlocks();
            return;
        }

        // 2. 족보 다시 말기 애니메이션 실행 (Animator)
        ApplyPresentationAnimatorSpeed();
        if (scrollAnimator != null) scrollAnimator.SetTrigger("Close");

        // 3. 말리는 애니메이션이 끝날 때까지 기다린 후 원래 구석으로 돌아가기 (DOTween)
        scrollDelayTween?.Kill();
        scrollDelayTween = DOVirtual.DelayedCall(ScaleDelay(unrollDuration), () =>
        {
            scrollDelayTween = null;
            RestoreAnimatorSpeed();
            float scaledMoveDuration = ScaleDuration(moveDuration);
            scroll3DModel.DOLocalMove(startLocalPos, scaledMoveDuration).SetEase(Ease.InBack);
            scroll3DModel.DOLocalRotate(startLocalRot, scaledMoveDuration).SetEase(Ease.InBack).OnComplete(ReleaseInputBlocks);
        });
    }

    private void OnDestroy()
    {
        ReleaseInputBlocks();

        if (Instance == this)
            Instance = null;
    }

    private void AcquireInputBlocks()
    {
        AcquireCardInputBlock();
        AcquireHeadLookInputBlock();
    }

    private void ReleaseInputBlocks()
    {
        ReleaseCardInputBlock();
        ReleaseHeadLookInputBlock();
    }

    private void AcquireCardInputBlock()
    {
        if (hasCardInputBlock || CardManager_2.Instance == null)
            return;

        CardManager_2.Instance.PushExternalCardInteractionBlock();
        hasCardInputBlock = true;
    }

    private void ReleaseCardInputBlock()
    {
        if (!hasCardInputBlock)
            return;

        if (CardManager_2.Instance != null)
            CardManager_2.Instance.PopExternalCardInteractionBlock();

        hasCardInputBlock = false;
    }

    private void AcquireHeadLookInputBlock()
    {
        if (hasHeadLookInputBlock)
            return;

        HeadLookInputController.PushExternalInputBlock();
        hasHeadLookInputBlock = true;
    }

    private void ReleaseHeadLookInputBlock()
    {
        if (!hasHeadLookInputBlock)
            return;

        HeadLookInputController.PopExternalInputBlock();
        hasHeadLookInputBlock = false;
    }

    private void ApplyPresentationAnimatorSpeed()
    {
        if (scrollAnimator == null)
            return;

        if (!hasCachedAnimatorSpeed)
        {
            cachedAnimatorSpeed = scrollAnimator.speed;
            hasCachedAnimatorSpeed = true;
        }

        scrollAnimator.speed = cachedAnimatorSpeed * ResolvePresentationSpeed();
    }

    private void RestoreAnimatorSpeed()
    {
        if (scrollAnimator != null && hasCachedAnimatorSpeed)
            scrollAnimator.speed = cachedAnimatorSpeed;
    }

    private static float ResolvePresentationSpeed()
    {
        return PresentationSpeedManager.Instance != null
            ? Mathf.Max(0.01f, PresentationSpeedManager.Instance.SpeedMultiplier)
            : 1f;
    }

    private static float ScaleDuration(float duration)
    {
        return PresentationSpeedManager.ScaleDuration(Mathf.Max(0.01f, duration));
    }

    private static float ScaleDelay(float delay)
    {
        return PresentationSpeedManager.ScaleDelay(Mathf.Max(0f, delay));
    }
}
