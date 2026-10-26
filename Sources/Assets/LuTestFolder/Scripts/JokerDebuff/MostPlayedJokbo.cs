using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Most Played Jokbo", menuName = "Stage/Most Played Jokbo")]
public class MostPlayedJokbo : BossDebuffSO
{
    private JokBoType targetJokbo = JokBoType.None;
    public override void ResetDebuff()
    {
        targetJokbo = JokBoType.None;
    }

    public override void InitDebuff()
    {
        // 라운드 진입 시점 기준으로 가장 많이 플레이된 족보를 저장
        if (JokboPlayTracker.Instance != null)
        {
            targetJokbo = JokboPlayTracker.Instance.GetMostPlayedJokboTotal();
            Debug.Log($"[Boss] MostPlayedJokbo 발동: '{targetJokbo}' 족보를 내면 전 재산을 잃습니다!");
        }
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        if (JokboPlayTracker.Instance == null) return; 
        // 이렇게 바꿔주세요!
        JokBoType currentJokbo = JokboPlayTracker.Instance.CurrentEvaluatingJokbo;

        if (currentJokbo == JokBoType.None) return;

        // 4. 진짜 족보가 들어왔을 때, 금지된 족보와 일치하는지 검사
        if (currentJokbo == targetJokbo)
        {
            if (EconomyManager.instance != null)
            {
                // 돈을 0으로 만듦 (점수는 깎지 않음)
                EconomyManager.instance.ecoData.currentMoney = 0;
                Debug.Log($"<color=red>[Boss] 치명적 저주 발동! {targetJokbo}를 제출하여 소지한 전이 0이 되었습니다.</color>");
            }
        }
    }
}
