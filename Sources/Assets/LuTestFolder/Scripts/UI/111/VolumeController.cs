using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VolumeController : MonoBehaviour
{

    public enum VolumeType { Master, BGM, SFX }

    [Header("볼륨 타입 설정")]
    [SerializeField] private VolumeType volumeType;

    [Header("UI 요소 연결")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_InputField volumeInputField;
 
    private bool isInitialized = false;

    private void Awake()
    {
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 100f;
        volumeSlider.wholeNumbers = true;
    }

    public void SetupInitialValue()
    {
        if (SoundManager.instance == null || SaveManager.instance == null || SaveManager.instance.settingsData == null) return;

        // 리스너가 오작동하여 세이브를 덮어쓰지 않도록 잠시 해제
        volumeSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        volumeInputField.onEndEdit.RemoveListener(OnInputFieldEndEdit);

        float currentVolume = 0f;
        GlobalSettingsData data = SaveManager.instance.settingsData;

        switch (volumeType)
        {
            case VolumeType.Master: currentVolume = data.masterVolume; break;
            case VolumeType.BGM:    currentVolume = data.bgmVolume; break;
            case VolumeType.SFX:    currentVolume = data.sfxVolume; break;
        }

        int uiValue = Mathf.RoundToInt(currentVolume * 100f);
        
        // 진짜 세이브된 값으로 UI 세팅
        volumeSlider.value = uiValue;
        volumeInputField.text = uiValue.ToString();

        // UI 세팅이 끝난 "후"에 비로소 유저 입력을 감지하도록 리스너를 붙임
        volumeSlider.onValueChanged.AddListener(OnSliderValueChanged);
        volumeInputField.onEndEdit.AddListener(OnInputFieldEndEdit);
        
        isInitialized = true;
    }

    private void OnSliderValueChanged(float value)
    {
        if (!isInitialized) return; // 초기화 전에는 작동 차단

        volumeInputField.text = Mathf.RoundToInt(value).ToString();
        float calculatedVolume = value / 100f;
        ApplyVolumeToManager(calculatedVolume);
    }

    private void OnInputFieldEndEdit(string text)
    {
        if (!isInitialized) return;

        if (float.TryParse(text, out float result))
        {
            result = Mathf.Clamp(result, 0f, 100f);
            volumeSlider.value = Mathf.RoundToInt(result);
        }
        else
        {
            volumeInputField.text = Mathf.RoundToInt(volumeSlider.value).ToString();
        }
    }

    private void ApplyVolumeToManager(float volume)
    {
        if (SoundManager.instance == null) return;

        // 사운드 리조스 조절
        switch (volumeType)
        {
            case VolumeType.Master: SoundManager.instance.SetMasterVolume(volume); break;
            case VolumeType.BGM:    SoundManager.instance.SetBGMVolume(volume); break;
            case VolumeType.SFX:    SoundManager.instance.SetSFXVolume(volume); break;
        }

        // 데이터 저장
        if (SaveManager.instance != null && SaveManager.instance.settingsData != null)
        {
            switch (volumeType)
            {
                case VolumeType.Master: SaveManager.instance.settingsData.masterVolume = volume; break;
                case VolumeType.BGM:    SaveManager.instance.settingsData.bgmVolume = volume; break;
                case VolumeType.SFX:    SaveManager.instance.settingsData.sfxVolume = volume; break;
            }

            SaveManager.instance.settingsData.audioSettingsInitialized = true;
            SaveManager.instance.SaveSettingsData();
        }
    }

    private void OnDestroy()
    {
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        if (volumeInputField != null) volumeInputField.onEndEdit.RemoveListener(OnInputFieldEndEdit);
    }
}
