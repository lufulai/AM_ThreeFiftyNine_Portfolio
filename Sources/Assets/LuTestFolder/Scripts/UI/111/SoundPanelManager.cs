using UnityEngine;

public class SoundPanelManager : MonoBehaviour
{
    // 옵션 창이 켜질 때마다 실행되는 유니티 표준 이벤트 함수
    private void OnEnable()
    {
        // 자식으로 붙어있는 모든 VolumeController들을 다 찾아서
        VolumeController[] controllers = GetComponentsInChildren<VolumeController>(true);
        
        // 세이브 데이터 로드가 끝난 진짜 값을 UI에 강제로 꽂아넣음
        foreach (var controller in controllers)
        {
            controller.SetupInitialValue();
        }
    }
    
}
