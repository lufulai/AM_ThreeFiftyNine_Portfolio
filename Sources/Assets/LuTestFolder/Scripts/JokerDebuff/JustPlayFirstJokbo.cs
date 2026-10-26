using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Just Play FirstJokbo", menuName = "Stage/Just Play FirstJokbo")]
public class JustPlayFirstJokbo : BossDebuffSO
{
    public override void ResetDebuff()
    {
    }

    public override void InitDebuff()
    {
        Debug.Log("[Boss] JustPlayFirstJokbo 발동: 이번 라운드의 첫 번째 족보만 점수를 얻습니다!");
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        if (JokboPlayTracker.Instance == null) return;

        JokBoType currentJokbo = JokboPlayTracker.Instance.CurrentEvaluatingJokbo;

        if (currentJokbo == JokBoType.None) return;

        JokBoType firstJokbo = JokboPlayTracker.Instance.GetFirstPlayedJokboThisRound();

        if (firstJokbo == JokBoType.None)
        {
            Debug.Log($"<color=orange>[Boss] 첫 번째 족보가 [{currentJokbo}]로 영구 고정되었습니다!</color>");
            return;
        }

        if (IsJokboBlocked(currentJokbo))
        {
            chip = 0;
            multiplier = 0;
            Debug.Log($"<color=red>[Boss] 실패! 첫 족보({firstJokbo})와 다른 ({currentJokbo})를 제출하여 0점 처리됩니다.</color>");
        }
    }

    public bool IsJokboBlocked(JokBoType currentJokbo)
    {
        if (JokboPlayTracker.Instance == null || currentJokbo == JokBoType.None)
            return false;

        JokBoType firstJokbo = JokboPlayTracker.Instance.GetFirstPlayedJokboThisRound();
        return firstJokbo != JokBoType.None && currentJokbo != firstJokbo;
    }
}
