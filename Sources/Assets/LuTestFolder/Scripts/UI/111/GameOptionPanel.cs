using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOptionPanel : MonoBehaviour
{
    [Header("게임 속도 설정 (3단계: 1배속, 2배속, 4배속)")]
    [SerializeField] private Image[] speedDots; // 하이어라키의 Dot 3개를 인스펙터에서 드래그 연동
    [SerializeField] private TextMeshProUGUI speedText;


    [SerializeField] private Color activeDotColor = Color.white; // 활성화된 점 (흰색)
    [SerializeField] private Color inactiveDotColor = new Color(0.2f, 0.2f, 0.2f, 0.6f); // 비활성 점 (회색 반투명)

    private int currentSpeedStep = 1; // UI 단계를 나타냄 (1, 2, 3 단계)
    private int maxSteps = 3;
    private bool isInitialized = false;
    internal TMP_Text BookSpeedText => speedText;
    internal Image[] BookSpeedDots => speedDots;

    internal void ApplyBookPalette(Color active, Color inactive)
    {
        activeDotColor = active;
        inactiveDotColor = inactive;
        if (speedDots == null) return;
        for (int i = 0; i < speedDots.Length; i++)
            if (speedDots[i] != null) speedDots[i].color = i < currentSpeedStep ? active : inactive;
    }

    private void OnEnable()
    {
        SetupInitialValue();
    }

    public void SetupInitialValue()
    {
        maxSteps = (speedDots != null && speedDots.Length > 0) ? speedDots.Length : 3;

        if (speedText == null)
        {
            speedText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (SaveManager.instance == null || SaveManager.instance.settingsData == null)
        {
            UpdateUI(1);
            return;
        }

        // 1. 세이브 데이터에 보관된 진짜 배속 값(1, 2, 4)을 로드
        int savedSpeedValue = SaveManager.instance.settingsData.presentationSpeed;
        
        // 2. 배속 값(1, 2, 4)을 UI 단계(1, 2, 3 단계)로 완벽하게 변환
        int initialStep = TranslateSpeedValueToUiStep(savedSpeedValue);
        
        // 3. UI 갱신 처리
        UpdateUI(initialStep);
    }

    // 오른쪽 화살표 버튼에 연결
    public void OnClickNextSpeed()
    {
        if (!isInitialized) SetupInitialValue();

        int nextStep = currentSpeedStep + 1;
        if (nextStep > maxSteps) nextStep = 1; // 순환
        UpdateUI(nextStep);
    }

    // 왼쪽 화살표 버튼에 연결
    public void OnClickPrevSpeed()
    {
        if (!isInitialized) SetupInitialValue();

        int prevStep = currentSpeedStep - 1;
        if (prevStep < 1) prevStep = maxSteps; // 순환
        UpdateUI(prevStep);
    }

    // ★ 글자 변경과 점 켜기를 한 세트로 묶어 실시간 강제 갱신하는 핵심 함수
    private void UpdateUI(int step)
    {
        currentSpeedStep = step;

        // 1. 점 등 켜기 (현재 선택된 단계의 도트만 흰색으로 켬)
        for (int i = 0; i < speedDots.Length; i++)
        {
            if (speedDots[i] != null)
            {
                speedDots[i].color = (i == step - 1) ? activeDotColor : inactiveDotColor;
            }
        }

        // 2. UI 단계를 인게임용 실제 정수 배속 값(1, 2, 4)으로 변환
        int calculatedSpeedValue = TranslateUiStepToSpeedValue(step);

        // 3. [버그 수정] 중앙의 숫자 글자 텍스트를 실제 정수 값으로 변경!
        if (speedText != null)
        {
            speedText.text = calculatedSpeedValue.ToString();
        }

        // 4. 유저 데이터에 대입 및 영구 저장
        if (SaveManager.instance != null && SaveManager.instance.settingsData != null)
        {
            SaveManager.instance.settingsData.presentationSpeed = calculatedSpeedValue;
            SaveManager.instance.SaveSettingsData();
        }

        // 5. 인게임 배속 매니저 기능 즉시 동기화
        if (PresentationSpeedManager.Instance != null)
        {
            PresentationSpeedManager.Instance.SetSpeedFromStoredValue(calculatedSpeedValue);
        }
    }

    private int TranslateUiStepToSpeedValue(int step)
    {
        if (step == 2) return 2;
        if (step == 3) return 4;
        return 1; 
    }

    private int TranslateSpeedValueToUiStep(int speedValue)
    {
        if (speedValue == 2) return 2;
        if (speedValue == 4) return 3;
        return 1;
    }
}
