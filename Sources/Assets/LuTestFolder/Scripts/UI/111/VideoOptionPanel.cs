using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Rendering; // ★ 추가: 포스트 프로세싱 볼륨 제어용
using UnityEngine.Rendering.Universal; // ★ 추가: URP 전용

public class VideoOptionPanel : MonoBehaviour
{
    internal TMP_Text BookScreenModeText => screenModeText;
    internal TMP_Text BookResolutionText => resolutionText;
    internal TMP_Text BookQualityText => qualityText;
    [Header("1. 화면 모드 설정")]
    [SerializeField] private TextMeshProUGUI screenModeText;

    [Header("2. 해상도 설정")]
    [SerializeField] private TextMeshProUGUI resolutionText;
    private readonly List<Resolution> availableResolutions = new List<Resolution>();
    private int currentResolutionIndex = 0;
    private Coroutine screenSettingsVerification;
    private bool screenSettingsApplyFailed;
    private int failedScreenWidth;
    private int failedScreenHeight;

    [Header("3. 그래픽 품질 설정 (현재 기능 없음)")]
    [SerializeField] private TextMeshProUGUI qualityText;
    private readonly string[] qualityLabels = { "낮음", "중간", "높음" };

    [Header("4. 감마 조절 설정 (0~100 슬라이더 & 인풋 연동)")]
    [SerializeField] private Slider gammaSlider;
    [SerializeField] private TMP_InputField gammaInputField;

    [Header("★ 감마 화면 반영을 위한 포스트 프로세싱 볼륨")]
    [SerializeField] private Volume globalVolume; // 인스펙터에서 하이어라키의 글로벌 볼륨을 연결합니다.
    private ColorAdjustments colorAdjustments;    // 볼륨 내부의 컴포넌트 잠정 저장용

    private void OnEnable()
    {
        screenSettingsApplyFailed = false;
        InitializeVideoUI();
        InitVolumeReference();  
    }

    private void OnDisable()
    {
        if (screenSettingsVerification == null)
            return;

        StopCoroutine(screenSettingsVerification);
        screenSettingsVerification = null;
    }

    private void InitVolumeReference()
    {
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out colorAdjustments);
        }
    }

    public void InitializeVideoUI()
    {
        // 1. 슬라이더 속성 초기화
        if (gammaSlider != null)
        {
            gammaSlider.minValue = 0f;
            gammaSlider.maxValue = 100f;
            gammaSlider.wholeNumbers = true;
        }

        // 2. 현재 모니터가 지원하는 16:9 해상도만 확보합니다.
        availableResolutions.Clear();
        availableResolutions.AddRange(UserSettingsRuntimeApplier.GetAvailableSixteenNineResolutions());

        // 3. UserData 세이브 데이터에 맞춰 UI 값 매핑
        if (SaveManager.instance != null && SaveManager.instance.settingsData != null) //
        {
            GlobalSettingsData data = SaveManager.instance.settingsData; //

            Resolution resolved = UserSettingsRuntimeApplier.ResolveSupportedResolution(data.screenWidth, data.screenHeight);
            data.screenWidth = resolved.width;
            data.screenHeight = resolved.height;
            currentResolutionIndex = FindResolutionIndex(resolved.width, resolved.height);
        }
        else
        {
            Resolution resolved = UserSettingsRuntimeApplier.ResolveSupportedResolution(Screen.width, Screen.height);
            currentResolutionIndex = FindResolutionIndex(resolved.width, resolved.height);
        }

        // 4. UI 텍스트 새로고침 및 감마 초기 렌더링 반영
        ApplyAndRefreshTextUI();
        
        if (SaveManager.instance != null && SaveManager.instance.settingsData != null)
        {
            ApplyPhysicalGamma(SaveManager.instance.settingsData.gammaValue);
        }

        // 5. 중복 없이 양방향 리스너 연결 초기화
        InitGammaListeners();
    }

    private void ApplyAndRefreshTextUI()
    {
        if (SaveManager.instance == null || SaveManager.instance.settingsData == null) return;
        GlobalSettingsData data = SaveManager.instance.settingsData;

        // 1. 화면 모드 표시
        if (screenModeText != null) 
            GameText.Bind(screenModeText, data.isFullScreen ? "settings.video.fullscreen" : "settings.video.windowed",
                data.isFullScreen ? "전체 화면" : "창 모드");

        // 2. 해상도 표시
        if (resolutionText != null && screenSettingsApplyFailed)
        {
            GameText.Bind(resolutionText, "settings.video.resolution_failed", "{width} x {height} (적용 실패)",
                new { width = failedScreenWidth, height = failedScreenHeight });
        }
        else if (resolutionText != null && availableResolutions.Count > currentResolutionIndex)
        {
            var res = availableResolutions[currentResolutionIndex];
            GameText.Bind(resolutionText, "settings.video.resolution", "{width} x {height}", new { width = res.width, height = res.height });
        }

        // 3. 그래픽 품질 표시
        if (qualityText != null)
        {
            int quality = Mathf.Clamp(data.graphicQuality, 0, qualityLabels.Length - 1);
            GameText.Bind(qualityText, "settings.video.quality." + quality, qualityLabels[quality]);
        }

        // 4. 감마 슬라이더 및 인풋필드 데이터 동기화
        if (gammaSlider != null)
        {
            gammaSlider.onValueChanged.RemoveListener(OnGammaSliderValueChanged);
            gammaSlider.value = data.gammaValue;
            gammaSlider.onValueChanged.AddListener(OnGammaSliderValueChanged);
        }
        if (gammaInputField != null)
        {
            gammaInputField.text = Mathf.RoundToInt(data.gammaValue).ToString();
        }
    }

    private void InitGammaListeners()
    {
        if (gammaSlider != null)
        {
            gammaSlider.onValueChanged.RemoveListener(OnGammaSliderValueChanged);
            gammaSlider.onValueChanged.AddListener(OnGammaSliderValueChanged);
        }

        if (gammaInputField != null)
        {
            gammaInputField.onEndEdit.RemoveListener(OnGammaInputFieldEndEdit);
            gammaInputField.onEndEdit.AddListener(OnGammaInputFieldEndEdit);
        }
    }

    // ==========================================
    // 감마 제어 (UserData 실시간 세이브)
    // ==========================================
    private void OnGammaSliderValueChanged(float value)
    {
        if (SaveManager.instance != null && SaveManager.instance.settingsData != null)
        {
            SaveManager.instance.settingsData.gammaValue = value;
            SaveManager.instance.SaveSettingsData();
        }

        if (gammaInputField != null)
        {
            gammaInputField.text = Mathf.RoundToInt(value).ToString();
        }

        // 화면 렌더링에 물리적 반영
        ApplyPhysicalGamma(value);
    }

    // 숫자를 직접 입력하고 엔터를 칠 때 작동
    private void OnGammaInputFieldEndEdit(string text)
    {
        if (float.TryParse(text, out float result))
        {
            result = Mathf.Clamp(result, 0f, 100f);
            
            // 슬라이더 값을 바꾸면 OnGammaSliderValueChanged가 연쇄 호출되면서 저장 및 렌더링까지 한 번에 처리됩니다.
            if (gammaSlider != null)
            {
                gammaSlider.value = Mathf.RoundToInt(result);
            }
        }
        else
        {
            // 이상한 문자를 적었을 땐 기존에 세이브되어 있던 안전한 값으로 복구
            if (SaveManager.instance != null && SaveManager.instance.settingsData != null && gammaInputField != null)
            {
                gammaInputField.text = Mathf.RoundToInt(SaveManager.instance.settingsData.gammaValue).ToString();
            }
        }
    }

    // 감마 수치(0~100)를 URP Post Exposure 노출 강도로 치환해 화면을 바꾸는 함수
    private void ApplyPhysicalGamma(float gammaValue)
    {
        if (colorAdjustments == null)
        {
            InitVolumeReference();
            if (colorAdjustments == null) return;
        }

        // 기본값 50일 때 0f (변형 없음)
        // 0일 때 -1.5f (어두움), 100일 때 +1.5f (밝음)
        float mappedExposure = (gammaValue - 50f) / 50f * 1.5f;
        
        // URP 볼륨 오버라이드 실시간 강제 집행
        colorAdjustments.postExposure.Override(mappedExposure);
    }

    // ==========================================
    // 화면 모드 제어 (UserData 실시간 세이브)
    // ==========================================
    public void OnClickNextScreenMode() => ToggleScreenMode();
    public void OnClickPrevScreenMode() => ToggleScreenMode();

    private void ToggleScreenMode()
    {
        if (SaveManager.instance == null || SaveManager.instance.settingsData == null) return;
        
        bool nextFullScreen = !SaveManager.instance.settingsData.isFullScreen;
        SaveManager.instance.settingsData.isFullScreen = nextFullScreen;
        ApplyAndVerifyScreenSettings();
    }

    // ==========================================
    // 해상도 제어 (UserData 실시간 세이브)
    // ==========================================
    public void OnClickNextResolution()
    {
        if (availableResolutions.Count == 0) return;
        currentResolutionIndex = (currentResolutionIndex + 1) % availableResolutions.Count;
        ExecuteResolutionChange();
    }

    public void OnClickPrevResolution()
    {
        if (availableResolutions.Count == 0) return;
        currentResolutionIndex = (currentResolutionIndex - 1 + availableResolutions.Count) % availableResolutions.Count;
        ExecuteResolutionChange();
    }

    private void ExecuteResolutionChange()
    {
        if (SaveManager.instance == null || SaveManager.instance.settingsData == null) return;
        
        Resolution targetRes = availableResolutions[currentResolutionIndex];
        SaveManager.instance.settingsData.screenWidth = targetRes.width;
        SaveManager.instance.settingsData.screenHeight = targetRes.height;
        ApplyAndVerifyScreenSettings();
    }

    private void ApplyAndVerifyScreenSettings()
    {
        GlobalSettingsData data = SaveManager.instance.settingsData;
        Resolution resolved = UserSettingsRuntimeApplier.ApplyScreenSettings(data);
        currentResolutionIndex = FindResolutionIndex(resolved.width, resolved.height);
        screenSettingsApplyFailed = false;

        SaveManager.instance.SaveSettingsData();

        if (screenSettingsVerification != null)
            StopCoroutine(screenSettingsVerification);

        screenSettingsVerification = StartCoroutine(VerifyScreenSettingsAfterFrame(
            resolved.width,
            resolved.height,
            data.isFullScreen));

        ApplyAndRefreshTextUI();
    }

    private IEnumerator VerifyScreenSettingsAfterFrame(int requestedWidth, int requestedHeight, bool requestedFullScreen)
    {
        // Screen.SetResolution과 전체화면 전환은 프레임 끝에 반영됩니다.
        yield return null;
        yield return new WaitForEndOfFrame();

        screenSettingsVerification = null;
        if (UserSettingsRuntimeApplier.IsScreenSettingsApplied(
                requestedWidth,
                requestedHeight,
                requestedFullScreen))
        {
            yield break;
        }

        screenSettingsApplyFailed = true;
        failedScreenWidth = Screen.width;
        failedScreenHeight = Screen.height;
        Debug.LogWarning(
            $"[화면 설정] 요청한 화면 설정이 그대로 적용되지 않았습니다. " +
            $"요청={requestedWidth}x{requestedHeight}/{UserSettingsRuntimeApplier.GetFullScreenMode(requestedFullScreen)}, " +
            $"실제={Screen.width}x{Screen.height}/{Screen.fullScreenMode}");
        ApplyAndRefreshTextUI();
    }

    private int FindResolutionIndex(int width, int height)
    {
        int index = availableResolutions.FindIndex(resolution =>
            resolution.width == width && resolution.height == height);
        return index >= 0 ? index : 0;
    }

    // ==========================================
    // 그래픽 품질 제어 (UserData 실시간 세이브)
    // ==========================================
    public void OnClickNextQuality()
    {
        if (SaveManager.instance == null || SaveManager.instance.settingsData == null) return;
        
        SaveManager.instance.settingsData.graphicQuality = (SaveManager.instance.settingsData.graphicQuality + 1) % 3;
        SaveManager.instance.SaveSettingsData();
        
        ApplyAndRefreshTextUI();
    }

    public void OnClickPrevQuality()
    {
        if (SaveManager.instance == null || SaveManager.instance.settingsData == null) return;
        
        SaveManager.instance.settingsData.graphicQuality = (SaveManager.instance.settingsData.graphicQuality - 1 + 3) % 3;
        SaveManager.instance.SaveSettingsData();
        
        ApplyAndRefreshTextUI();
    }
}
