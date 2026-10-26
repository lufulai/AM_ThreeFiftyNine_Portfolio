using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Downgrade Jokbo Level", menuName = "Stage/Downgrade Jokbo Level")]
public class DowngradeJokboLevel : BossDebuffSO
{
    private int lastTriggerFrame = -1;
    public override void ResetDebuff()
    {
        lastTriggerFrame = -1;
    }

    public override void InitDebuff()
    {
        lastTriggerFrame = -1;
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        if (JokboPlayTracker.Instance == null) return;
        JokBoType currentJokbo = JokboPlayTracker.Instance.CurrentEvaluatingJokbo;

        if (currentJokbo == JokBoType.None) return;

        // ★ 핵심 1: 같은 프레임(한 번의 제출) 내에서 두 번 깎이는 것을 원천 차단합니다!
        if (Time.frameCount == lastTriggerFrame) return;
        lastTriggerFrame = Time.frameCount; // 실행 기록 남기기

        if (PlanetCardLevelManager.Instance != null)
        {
            var jokboStats = PlanetCardLevelManager.Instance.jokboLevel;
            
            if (jokboStats.ContainsKey(currentJokbo))
            {
                PlanetStats stats = jokboStats[currentJokbo];
                
                if (stats.level > 1)
                {
                    // ★ 핵심 2: 레벨당 올랐던 수치를 정확히 역산해서 빼줍니다.
                    // (현재 보너스 수치) / (현재 레벨 - 1) = 1업당 오르는 평균 수치
                    int chipDecrease = stats.plusChip / (stats.level - 1);
                    int multiDecrease = stats.plusMulti / (stats.level - 1);

                    // 스펙 롤백
                    stats.plusChip -= chipDecrease;
                    stats.plusMulti -= multiDecrease;
                    stats.level--;
                    
                    Debug.Log($"<color=red>[Boss] 저주 발동! {currentJokbo} 레벨 하락: {stats.level} (칩 -{chipDecrease}, 배수 -{multiDecrease})</color>");
                }
                else
                {
                    Debug.Log($"<color=yellow>[Boss] {currentJokbo} 족보의 레벨이 이미 최저치(1)라서 더 이상 깎이지 않습니다.</color>");
                }
            }
        }
    }
}
